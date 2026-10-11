using System;
using SessionRpg.Core;

namespace SessionRpg.Networking
{
    public enum AuthoritativeRoomPhase
    {
        Lobby,
        Playing,
        Results,
        Aborted,
    }

    public enum RoomSlotControllerKind
    {
        Empty,
        Host,
        Client,
        Bot,
    }

    public enum RoomJoinResult
    {
        Joined,
        AdmissionClosed,
        RoomFull,
        AlreadyJoined,
        ReconnectRejected,
        HostIdentityReserved,
    }

    public enum RoomDisconnectResult
    {
        ClientReplacedByBot,
        HostAbortedSession,
        NotFound,
    }

    public readonly struct AuthoritativeRoomSlot
    {
        public AuthoritativeRoomSlot(
            int index,
            ParticipantId participant,
            TeamId team,
            RoomSlotControllerKind controller,
            ulong clientId,
            bool connected)
        {
            Index = index;
            Participant = participant;
            Team = team;
            Controller = controller;
            ClientId = clientId;
            IsConnected = connected;
        }

        public int Index { get; }

        public ParticipantId Participant { get; }

        public TeamId Team { get; }

        public RoomSlotControllerKind Controller { get; }

        public ulong ClientId { get; }

        public bool IsConnected { get; }

        public bool IsHuman =>
            Controller == RoomSlotControllerKind.Host ||
            Controller == RoomSlotControllerKind.Client;
    }

    /// <summary>
    /// Deterministic four-slot room policy for the first 2v2 network proof. Slot
    /// identity and team never change. Once play begins, admission stays closed;
    /// a departed client becomes a bot and the host departure aborts the room.
    /// </summary>
    public sealed class AuthoritativeRoomRoster
    {
        public const int SlotCount = 4;

        private readonly RoomSlotControllerKind[] _controllers =
            new RoomSlotControllerKind[SlotCount];
        private readonly ulong[] _clientIds = new ulong[SlotCount];
        private readonly bool[] _connected = new bool[SlotCount];
        private readonly ulong[] _retiredClientIds = new ulong[SlotCount];

        private int _retiredClientCount;
        private bool _admissionClosed;
        private uint _sessionEpoch;

        public AuthoritativeRoomRoster(ulong hostClientId)
        {
            HostClientId = hostClientId;
            Phase = AuthoritativeRoomPhase.Lobby;
            _controllers[0] = RoomSlotControllerKind.Host;
            _clientIds[0] = hostClientId;
            _connected[0] = true;
        }

        public ulong HostClientId { get; }

        public AuthoritativeRoomPhase Phase { get; private set; }

        public bool AdmissionClosed => _admissionClosed;

        public uint SessionEpoch => _sessionEpoch;

        public int HumanCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < SlotCount; index++)
                {
                    if ((_controllers[index] == RoomSlotControllerKind.Host ||
                         _controllers[index] == RoomSlotControllerKind.Client) &&
                        _connected[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int BotCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < SlotCount; index++)
                {
                    if (_controllers[index] == RoomSlotControllerKind.Bot)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public RoomJoinResult TryJoinClient(ulong clientId, out ParticipantId participant)
        {
            participant = default;

            if (clientId == HostClientId)
            {
                return RoomJoinResult.HostIdentityReserved;
            }

            if (FindClientIndex(clientId) >= 0)
            {
                return RoomJoinResult.AlreadyJoined;
            }

            if (WasRetired(clientId))
            {
                return RoomJoinResult.ReconnectRejected;
            }

            if (_admissionClosed || Phase != AuthoritativeRoomPhase.Lobby)
            {
                return RoomJoinResult.AdmissionClosed;
            }

            for (int index = 1; index < SlotCount; index++)
            {
                if (_controllers[index] != RoomSlotControllerKind.Empty)
                {
                    continue;
                }

                _controllers[index] = RoomSlotControllerKind.Client;
                _clientIds[index] = clientId;
                _connected[index] = true;
                participant = ParticipantForIndex(index);
                return RoomJoinResult.Joined;
            }

            return RoomJoinResult.RoomFull;
        }

        public bool BeginSession()
        {
            if (Phase != AuthoritativeRoomPhase.Lobby)
            {
                return false;
            }

            _admissionClosed = true;
            FillEmptySlotsWithBots();
            IncrementSessionEpoch();
            Phase = AuthoritativeRoomPhase.Playing;
            return true;
        }

        public bool EnterResults()
        {
            if (Phase != AuthoritativeRoomPhase.Playing)
            {
                return false;
            }

            Phase = AuthoritativeRoomPhase.Results;
            return true;
        }

        public bool StartReplay()
        {
            if (Phase != AuthoritativeRoomPhase.Results)
            {
                return false;
            }

            IncrementSessionEpoch();
            Phase = AuthoritativeRoomPhase.Playing;
            return true;
        }

        public RoomDisconnectResult Disconnect(ulong clientId)
        {
            if (clientId == HostClientId)
            {
                _connected[0] = false;
                Phase = AuthoritativeRoomPhase.Aborted;
                _admissionClosed = true;
                return RoomDisconnectResult.HostAbortedSession;
            }

            int index = FindClientIndex(clientId);
            if (index < 0)
            {
                return RoomDisconnectResult.NotFound;
            }

            RetireClient(clientId);
            _controllers[index] = RoomSlotControllerKind.Bot;
            _clientIds[index] = default;
            _connected[index] = false;
            return RoomDisconnectResult.ClientReplacedByBot;
        }

        public AuthoritativeRoomSlot GetSlot(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new AuthoritativeRoomSlot(
                index,
                ParticipantForIndex(index),
                TeamForIndex(index),
                _controllers[index],
                _clientIds[index],
                _connected[index]);
        }

        public bool TryGetSlot(ParticipantId participant, out AuthoritativeRoomSlot slot)
        {
            int index = participant.Value - 1;
            if (index < 0 || index >= SlotCount)
            {
                slot = default;
                return false;
            }

            slot = GetSlot(index);
            return slot.Controller != RoomSlotControllerKind.Empty;
        }

        public bool TryGetParticipantForClient(
            ulong clientId,
            out ParticipantId participant)
        {
            int index = FindClientIndex(clientId);
            if (index < 0)
            {
                participant = default;
                return false;
            }

            participant = ParticipantForIndex(index);
            return true;
        }

        public bool IsAuthorized(ulong senderClientId, ParticipantId actor)
        {
            if (!TryGetSlot(actor, out AuthoritativeRoomSlot slot))
            {
                return false;
            }

            switch (slot.Controller)
            {
                case RoomSlotControllerKind.Host:
                case RoomSlotControllerKind.Client:
                    return slot.IsConnected && slot.ClientId == senderClientId;
                case RoomSlotControllerKind.Bot:
                    return senderClientId == HostClientId;
                default:
                    return false;
            }
        }

        private int FindClientIndex(ulong clientId)
        {
            for (int index = 0; index < SlotCount; index++)
            {
                if ((_controllers[index] == RoomSlotControllerKind.Host ||
                     _controllers[index] == RoomSlotControllerKind.Client) &&
                    _connected[index] &&
                    _clientIds[index] == clientId)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool WasRetired(ulong clientId)
        {
            for (int index = 0; index < _retiredClientCount; index++)
            {
                if (_retiredClientIds[index] == clientId)
                {
                    return true;
                }
            }

            return false;
        }

        private void RetireClient(ulong clientId)
        {
            if (_retiredClientCount >= _retiredClientIds.Length || WasRetired(clientId))
            {
                return;
            }

            _retiredClientIds[_retiredClientCount++] = clientId;
        }

        private void FillEmptySlotsWithBots()
        {
            for (int index = 0; index < SlotCount; index++)
            {
                if (_controllers[index] != RoomSlotControllerKind.Empty)
                {
                    continue;
                }

                _controllers[index] = RoomSlotControllerKind.Bot;
                _clientIds[index] = default;
                _connected[index] = false;
            }
        }

        private void IncrementSessionEpoch()
        {
            _sessionEpoch++;
            if (_sessionEpoch == 0)
            {
                _sessionEpoch = 1;
            }
        }

        private static ParticipantId ParticipantForIndex(int index)
        {
            return new ParticipantId(index + 1);
        }

        private static TeamId TeamForIndex(int index)
        {
            return new TeamId(index < 2 ? 1 : 2);
        }
    }
}
