using System;
using SessionRpg.Core;

namespace SessionRpg.Networking
{
    /// <summary>
    /// Allocation-free-after-construction 30 Hz scheduler. A bounded backlog avoids
    /// an unbounded catch-up spiral after a host stall.
    /// </summary>
    public sealed class HostTickClock
    {
        public const int TickRate = 30;
        public const double TickSeconds = 1.0 / TickRate;
        public const int DefaultMaximumPendingTicks = 8;

        private readonly int _maximumPendingTicks;
        private double _accumulatedSeconds;

        public HostTickClock(int maximumPendingTicks = DefaultMaximumPendingTicks)
        {
            if (maximumPendingTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPendingTicks));
            }

            _maximumPendingTicks = maximumPendingTicks;
        }

        public int CurrentTick { get; private set; }

        public int PendingTickCount
        {
            get
            {
                int pending = (int)(_accumulatedSeconds / TickSeconds);
                return Math.Min(pending, _maximumPendingTicks);
            }
        }

        public void AddElapsedSeconds(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) ||
                double.IsInfinity(elapsedSeconds) ||
                elapsedSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            _accumulatedSeconds = Math.Min(
                _accumulatedSeconds + elapsedSeconds,
                TickSeconds * _maximumPendingTicks);
        }

        public bool TryConsumeTick(out int tick)
        {
            if (_accumulatedSeconds + 0.000000001 < TickSeconds)
            {
                tick = CurrentTick;
                return false;
            }

            _accumulatedSeconds -= TickSeconds;
            if (_accumulatedSeconds < 0.0)
            {
                _accumulatedSeconds = 0.0;
            }

            CurrentTick++;
            tick = CurrentTick;
            return true;
        }

        public void Reset()
        {
            CurrentTick = 0;
            _accumulatedSeconds = 0.0;
        }
    }

    /// <summary>
    /// Pure host-authority software boundary. NGO delivery submits envelopes here;
    /// the host-local human and host bots submit through the same method. The caller
    /// drains accepted envelopes once per host tick and passes their Core commands
    /// to CombatSimulation before advancing that simulation tick.
    /// </summary>
    public sealed class HostAuthoritativeSession
    {
        public const int MaximumParticipants = AuthoritativeRoomRoster.SlotCount;
        public const int DefaultCommandsPerParticipant = 32;
        public const int DefaultMaximumPastTicks = 6;
        public const int DefaultMaximumFutureTicks = 2;

        private readonly ParticipantCommandQueue[] _queues =
            new ParticipantCommandQueue[MaximumParticipants];
        private readonly int _maximumPastTicks;
        private readonly int _maximumFutureTicks;

        private uint _snapshotSequence;
        private uint _eventSequence;

        public HostAuthoritativeSession(
            ulong hostClientId,
            int commandsPerParticipant = DefaultCommandsPerParticipant,
            int maximumPastTicks = DefaultMaximumPastTicks,
            int maximumFutureTicks = DefaultMaximumFutureTicks)
        {
            if (commandsPerParticipant <= 0 ||
                commandsPerParticipant > DefaultCommandsPerParticipant)
            {
                throw new ArgumentOutOfRangeException(nameof(commandsPerParticipant));
            }

            if (maximumPastTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPastTicks));
            }

            if (maximumFutureTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumFutureTicks));
            }

            Room = new AuthoritativeRoomRoster(hostClientId);
            _maximumPastTicks = maximumPastTicks;
            _maximumFutureTicks = maximumFutureTicks;

            for (int index = 0; index < _queues.Length; index++)
            {
                _queues[index] = new ParticipantCommandQueue(commandsPerParticipant);
            }
        }

        public AuthoritativeRoomRoster Room { get; }

        public int CurrentHostTick { get; private set; }

        public int PendingCommandCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _queues.Length; index++)
                {
                    count += _queues[index].Count;
                }

                return count;
            }
        }

        public RoomJoinResult TryJoinClient(ulong clientId, out ParticipantId participant)
        {
            return Room.TryJoinClient(clientId, out participant);
        }

        public bool BeginSession()
        {
            if (!Room.BeginSession())
            {
                return false;
            }

            ResetRoundState();
            return true;
        }

        public bool EnterResults()
        {
            if (!Room.EnterResults())
            {
                return false;
            }

            ClearPendingCommands(false);
            return true;
        }

        public bool StartReplay()
        {
            if (!Room.StartReplay())
            {
                return false;
            }

            ResetRoundState();
            return true;
        }

        public RoomDisconnectResult HandleDisconnect(ulong clientId)
        {
            Room.TryGetParticipantForClient(clientId, out ParticipantId participant);
            RoomDisconnectResult result = Room.Disconnect(clientId);

            if (result == RoomDisconnectResult.HostAbortedSession)
            {
                ClearPendingCommands(true);
            }
            else if (result == RoomDisconnectResult.ClientReplacedByBot &&
                     participant.IsValid)
            {
                _queues[participant.Value - 1].Clear(true);
            }

            return result;
        }

        public NetworkCommandResult SubmitHostCommand(
            int requestedTick,
            CombatCommand command)
        {
            return SubmitCommand(Room.HostClientId, new NetworkCommandEnvelope(
                Room.SessionEpoch,
                requestedTick,
                command));
        }

        public NetworkCommandResult SubmitCommand(
            ulong authenticatedSenderClientId,
            NetworkCommandEnvelope envelope)
        {
            NetworkCommandDisposition disposition = ValidateForEnqueue(
                authenticatedSenderClientId,
                envelope);
            if (disposition != NetworkCommandDisposition.Accepted)
            {
                return Result(disposition, envelope);
            }

            ParticipantCommandQueue queue = _queues[envelope.Actor.Value - 1];
            queue.Enqueue(envelope);
            return Result(NetworkCommandDisposition.Accepted, envelope);
        }

        /// <summary>
        /// Advances exactly one host simulation tick and drains ready commands into
        /// caller-owned storage. Commands remain globally ordered by requested tick,
        /// actor identity, then sequence. If the buffer is too small, remaining
        /// commands stay queued for the next call rather than being dropped.
        /// </summary>
        public int AdvanceHostTick(NetworkCommandEnvelope[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (Room.Phase != AuthoritativeRoomPhase.Playing)
            {
                return 0;
            }

            CurrentHostTick++;
            int written = 0;
            while (written < destination.Length &&
                   TryFindNextReadyQueue(out int queueIndex))
            {
                destination[written++] = _queues[queueIndex].Dequeue();
            }

            return written;
        }

        public OwnerReconciliationMetadata GetReconciliationMetadata(
            ParticipantId participant)
        {
            int index = participant.Value - 1;
            if (index < 0 || index >= _queues.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(participant));
            }

            ParticipantCommandQueue queue = _queues[index];
            return new OwnerReconciliationMetadata(
                participant,
                CurrentHostTick,
                queue.LastAcceptedSequence,
                queue.LastProcessedSequence,
                queue.LastProcessedRequestedTick);
        }

        public AuthoritativeSnapshotEnvelope CreateSnapshotEnvelope(
            CombatantSnapshot[] snapshots,
            int count)
        {
            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (count < 0 ||
                count > snapshots.Length ||
                count > AuthoritativeSnapshotEnvelope.MaximumSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _snapshotSequence = NextNonZero(_snapshotSequence);
            AuthoritativeSnapshotEnvelope envelope = new AuthoritativeSnapshotEnvelope(
                Room.SessionEpoch,
                CurrentHostTick,
                _snapshotSequence);

            for (int index = 0; index < count; index++)
            {
                CombatantSnapshot snapshot = snapshots[index];
                OwnerReconciliationMetadata reconciliation =
                    snapshot.Id.Value >= 1 && snapshot.Id.Value <= MaximumParticipants
                        ? GetReconciliationMetadata(snapshot.Id)
                        : new OwnerReconciliationMetadata(
                            snapshot.Id,
                            CurrentHostTick,
                            0,
                            0,
                            0);
                envelope.TryAppend(new AuthoritativeSnapshotSlot(
                    new NetworkCombatantSnapshot(snapshot),
                    reconciliation));
            }

            return envelope;
        }

        public AuthoritativeEventEnvelope CreateEventEnvelope(CombatEvent combatEvent)
        {
            _eventSequence = NextNonZero(_eventSequence);
            return new AuthoritativeEventEnvelope(
                Room.SessionEpoch,
                _eventSequence,
                combatEvent);
        }

        private NetworkCommandDisposition ValidateForEnqueue(
            ulong authenticatedSenderClientId,
            NetworkCommandEnvelope envelope)
        {
            if (Room.Phase == AuthoritativeRoomPhase.Aborted)
            {
                return NetworkCommandDisposition.SessionAborted;
            }

            if (Room.Phase != AuthoritativeRoomPhase.Playing)
            {
                return NetworkCommandDisposition.OutOfPhase;
            }

            if (envelope.SessionEpoch != Room.SessionEpoch)
            {
                return NetworkCommandDisposition.WrongSessionEpoch;
            }

            if (!Room.TryGetSlot(envelope.Actor, out _))
            {
                return NetworkCommandDisposition.UnknownParticipant;
            }

            if (!Room.IsAuthorized(authenticatedSenderClientId, envelope.Actor))
            {
                return NetworkCommandDisposition.Unauthorized;
            }

            if (envelope.Sequence == 0)
            {
                return NetworkCommandDisposition.InvalidSequence;
            }

            ParticipantCommandQueue queue = _queues[envelope.Actor.Value - 1];
            NetworkCommandDisposition sequenceDisposition =
                queue.ClassifySequence(envelope.Sequence);
            if (sequenceDisposition != NetworkCommandDisposition.Accepted)
            {
                return sequenceDisposition;
            }

            if (envelope.RequestedTick < 0 ||
                (long)envelope.RequestedTick <
                (long)CurrentHostTick - _maximumPastTicks)
            {
                return NetworkCommandDisposition.RequestedTickTooOld;
            }

            if ((long)envelope.RequestedTick >
                (long)CurrentHostTick + _maximumFutureTicks)
            {
                return NetworkCommandDisposition.RequestedTickTooFarAhead;
            }

            if (queue.Count >= queue.Capacity)
            {
                return NetworkCommandDisposition.QueueFull;
            }

            return NetworkCommandDisposition.Accepted;
        }

        private bool TryFindNextReadyQueue(out int bestQueueIndex)
        {
            bestQueueIndex = -1;
            NetworkCommandEnvelope best = default;

            for (int index = 0; index < _queues.Length; index++)
            {
                if (!_queues[index].TryPeek(out NetworkCommandEnvelope candidate) ||
                    candidate.RequestedTick > CurrentHostTick)
                {
                    continue;
                }

                if (bestQueueIndex < 0 || IsEarlier(candidate, best))
                {
                    bestQueueIndex = index;
                    best = candidate;
                }
            }

            return bestQueueIndex >= 0;
        }

        private void ResetRoundState()
        {
            CurrentHostTick = 0;
            _snapshotSequence = 0;
            _eventSequence = 0;
            ClearPendingCommands(true);
        }

        private void ClearPendingCommands(bool resetSequenceState)
        {
            for (int index = 0; index < _queues.Length; index++)
            {
                _queues[index].Clear(resetSequenceState);
            }
        }

        private NetworkCommandResult Result(
            NetworkCommandDisposition disposition,
            NetworkCommandEnvelope envelope)
        {
            return new NetworkCommandResult(
                disposition,
                envelope.Actor,
                envelope.Sequence,
                CurrentHostTick);
        }

        private static bool IsEarlier(
            NetworkCommandEnvelope left,
            NetworkCommandEnvelope right)
        {
            if (left.RequestedTick != right.RequestedTick)
            {
                return left.RequestedTick < right.RequestedTick;
            }

            int actorComparison = left.Actor.CompareTo(right.Actor);
            return actorComparison != 0
                ? actorComparison < 0
                : left.Sequence < right.Sequence;
        }

        private static uint NextNonZero(uint value)
        {
            value++;
            return value == 0 ? 1u : value;
        }

        private sealed class ParticipantCommandQueue
        {
            private readonly NetworkCommandEnvelope[] _items;
            private bool _hasAcceptedSequence;

            public ParticipantCommandQueue(int capacity)
            {
                _items = new NetworkCommandEnvelope[capacity];
            }

            public int Count { get; private set; }

            public int Capacity => _items.Length;

            public uint LastAcceptedSequence { get; private set; }

            public uint LastProcessedSequence { get; private set; }

            public int LastProcessedRequestedTick { get; private set; }

            public NetworkCommandDisposition ClassifySequence(uint sequence)
            {
                if (!_hasAcceptedSequence)
                {
                    return NetworkCommandDisposition.Accepted;
                }

                if (sequence == LastAcceptedSequence)
                {
                    return NetworkCommandDisposition.DuplicateSequence;
                }

                return IsNewerSequence(sequence, LastAcceptedSequence)
                    ? NetworkCommandDisposition.Accepted
                    : NetworkCommandDisposition.StaleSequence;
            }

            public void Enqueue(NetworkCommandEnvelope envelope)
            {
                int insertionIndex = Count;
                while (insertionIndex > 0 &&
                       IsEarlier(envelope, _items[insertionIndex - 1]))
                {
                    _items[insertionIndex] = _items[insertionIndex - 1];
                    insertionIndex--;
                }

                _items[insertionIndex] = envelope;
                Count++;
                LastAcceptedSequence = envelope.Sequence;
                _hasAcceptedSequence = true;
            }

            public bool TryPeek(out NetworkCommandEnvelope envelope)
            {
                if (Count == 0)
                {
                    envelope = default;
                    return false;
                }

                envelope = _items[0];
                return true;
            }

            public NetworkCommandEnvelope Dequeue()
            {
                if (Count == 0)
                {
                    throw new InvalidOperationException("The command queue is empty.");
                }

                NetworkCommandEnvelope envelope = _items[0];
                Count--;
                for (int index = 0; index < Count; index++)
                {
                    _items[index] = _items[index + 1];
                }

                _items[Count] = default;
                LastProcessedSequence = envelope.Sequence;
                LastProcessedRequestedTick = envelope.RequestedTick;
                return envelope;
            }

            public void Clear(bool resetSequenceState)
            {
                for (int index = 0; index < Count; index++)
                {
                    _items[index] = default;
                }

                Count = 0;
                if (!resetSequenceState)
                {
                    return;
                }

                LastAcceptedSequence = 0;
                LastProcessedSequence = 0;
                LastProcessedRequestedTick = 0;
                _hasAcceptedSequence = false;
            }

            private static bool IsNewerSequence(uint candidate, uint reference)
            {
                uint distance = candidate - reference;
                return distance != 0 && distance < 0x80000000u;
            }
        }
    }
}
