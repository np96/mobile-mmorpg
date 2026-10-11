using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Networking.Tests
{
    public sealed class HostAuthoritativeSessionTests
    {
        [Test]
        public void Submission_UsesOnePathForHostClientAndBots()
        {
            HostAuthoritativeSession session = new HostAuthoritativeSession(10);
            Assert.That(session.TryJoinClient(20, out ParticipantId client),
                Is.EqualTo(RoomJoinResult.Joined));

            NetworkCommandResult lobbyResult = session.SubmitHostCommand(
                0,
                Move(1, 1));
            Assert.That(lobbyResult.Disposition,
                Is.EqualTo(NetworkCommandDisposition.OutOfPhase));

            Assert.That(session.BeginSession(), Is.True);
            Assert.That(session.SubmitHostCommand(0, Move(1, 1)).Accepted, Is.True);
            Assert.That(session.SubmitCommand(20, Envelope(
                session,
                client.Value,
                1,
                0)).Accepted, Is.True);

            NetworkCommandResult unauthorized = session.SubmitCommand(20, Envelope(
                session,
                1,
                2,
                0));
            Assert.That(unauthorized.Disposition,
                Is.EqualTo(NetworkCommandDisposition.Unauthorized));

            ParticipantId bot = new ParticipantId(3);
            Assert.That(session.SubmitHostCommand(0, Move(bot.Value, 1)).Accepted, Is.True);
            Assert.That(session.SubmitCommand(20, Envelope(
                session,
                bot.Value,
                2,
                0)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.Unauthorized));

            NetworkCommandEnvelope[] drained = new NetworkCommandEnvelope[8];
            Assert.That(session.AdvanceHostTick(drained), Is.EqualTo(3));
            Assert.That(drained[0].Actor.Value, Is.EqualTo(1));
            Assert.That(drained[1].Actor.Value, Is.EqualTo(2));
            Assert.That(drained[2].Actor.Value, Is.EqualTo(3));
        }

        [Test]
        public void Submission_DeterministicallyRejectsDuplicateStaleAndReordered()
        {
            HostAuthoritativeSession session = StartedHostSession();
            Assert.That(session.SubmitHostCommand(0, Move(1, 1)).Accepted, Is.True);
            Assert.That(session.SubmitHostCommand(0, Move(1, 1)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.DuplicateSequence));

            Assert.That(session.SubmitHostCommand(0, Move(1, 3)).Accepted, Is.True);
            Assert.That(session.SubmitHostCommand(0, Move(1, 2)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.StaleSequence));
            Assert.That(session.SubmitHostCommand(0, Move(1, 0)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.InvalidSequence));
        }

        [Test]
        public void Submission_RejectsDelayedAndExcessivelyFutureTicks()
        {
            HostAuthoritativeSession session = StartedHostSession();
            NetworkCommandEnvelope[] drained = new NetworkCommandEnvelope[1];
            for (int index = 0; index < 8; index++)
            {
                session.AdvanceHostTick(drained);
            }

            Assert.That(session.SubmitHostCommand(1, Move(1, 1)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.RequestedTickTooOld));
            Assert.That(session.SubmitHostCommand(11, Move(1, 1)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.RequestedTickTooFarAhead));
            Assert.That(session.SubmitHostCommand(8, Move(1, 1)).Accepted, Is.True);
        }

        [Test]
        public void Queue_IsBoundedPerParticipantAndDoesNotConsumeRejectedSequence()
        {
            HostAuthoritativeSession session = new HostAuthoritativeSession(
                10,
                commandsPerParticipant: 2);
            Assert.That(session.BeginSession(), Is.True);
            Assert.That(session.SubmitHostCommand(0, Move(1, 1)).Accepted, Is.True);
            Assert.That(session.SubmitHostCommand(0, Move(1, 2)).Accepted, Is.True);
            Assert.That(session.SubmitHostCommand(0, Move(1, 3)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.QueueFull));
            Assert.That(session.PendingCommandCount, Is.EqualTo(2));

            NetworkCommandEnvelope[] drained = new NetworkCommandEnvelope[2];
            Assert.That(session.AdvanceHostTick(drained), Is.EqualTo(2));
            Assert.That(session.SubmitHostCommand(1, Move(1, 3)).Accepted, Is.True);
        }

        [Test]
        public void Drain_OrdersByRequestedTickThenActorThenSequence()
        {
            HostAuthoritativeSession session = new HostAuthoritativeSession(
                10,
                maximumFutureTicks: 4);
            Assert.That(session.TryJoinClient(20, out ParticipantId client),
                Is.EqualTo(RoomJoinResult.Joined));
            Assert.That(session.BeginSession(), Is.True);

            Assert.That(session.SubmitHostCommand(1, Move(1, 1)).Accepted, Is.True);
            Assert.That(session.SubmitCommand(20, Envelope(
                session,
                client.Value,
                1,
                0)).Accepted, Is.True);
            Assert.That(session.SubmitHostCommand(0, Move(3, 1)).Accepted, Is.True);

            NetworkCommandEnvelope[] drained = new NetworkCommandEnvelope[8];
            Assert.That(session.AdvanceHostTick(drained), Is.EqualTo(3));
            Assert.That(drained[0].Actor.Value, Is.EqualTo(2));
            Assert.That(drained[1].Actor.Value, Is.EqualTo(3));
            Assert.That(drained[2].Actor.Value, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectAndReplay_ReplaceClientResetQueueAndRejectOldEpoch()
        {
            HostAuthoritativeSession session = new HostAuthoritativeSession(10);
            Assert.That(session.TryJoinClient(20, out ParticipantId client),
                Is.EqualTo(RoomJoinResult.Joined));
            Assert.That(session.BeginSession(), Is.True);
            uint firstEpoch = session.Room.SessionEpoch;
            NetworkCommandEnvelope oldRound = Envelope(
                session,
                client.Value,
                1,
                0);
            Assert.That(session.SubmitCommand(20, oldRound).Accepted, Is.True);

            Assert.That(session.HandleDisconnect(20),
                Is.EqualTo(RoomDisconnectResult.ClientReplacedByBot));
            Assert.That(session.PendingCommandCount, Is.Zero);
            Assert.That(session.SubmitCommand(20, oldRound).Disposition,
                Is.EqualTo(NetworkCommandDisposition.Unauthorized));
            Assert.That(session.SubmitHostCommand(0, Move(client.Value, 1)).Accepted, Is.True);

            Assert.That(session.EnterResults(), Is.True);
            Assert.That(session.SubmitHostCommand(0, Move(1, 2)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.OutOfPhase));
            Assert.That(session.StartReplay(), Is.True);
            Assert.That(session.Room.SessionEpoch, Is.Not.EqualTo(firstEpoch));
            Assert.That(session.SubmitCommand(20, oldRound).Disposition,
                Is.EqualTo(NetworkCommandDisposition.WrongSessionEpoch));
            Assert.That(session.SubmitHostCommand(0, Move(1, 1)).Accepted, Is.True);
            Assert.That(session.TryJoinClient(30, out _),
                Is.EqualTo(RoomJoinResult.AdmissionClosed));
        }

        [Test]
        public void HostDisconnect_AbortsAndRejectsCommands()
        {
            HostAuthoritativeSession session = StartedHostSession();
            Assert.That(session.HandleDisconnect(10),
                Is.EqualTo(RoomDisconnectResult.HostAbortedSession));
            Assert.That(session.Room.Phase, Is.EqualTo(AuthoritativeRoomPhase.Aborted));
            Assert.That(session.SubmitHostCommand(0, Move(1, 1)).Disposition,
                Is.EqualTo(NetworkCommandDisposition.SessionAborted));
            Assert.That(session.StartReplay(), Is.False);
        }

        private static HostAuthoritativeSession StartedHostSession()
        {
            HostAuthoritativeSession session = new HostAuthoritativeSession(10);
            Assert.That(session.BeginSession(), Is.True);
            return session;
        }

        private static NetworkCommandEnvelope Envelope(
            HostAuthoritativeSession session,
            int actor,
            uint sequence,
            int requestedTick)
        {
            return new NetworkCommandEnvelope(
                session.Room.SessionEpoch,
                requestedTick,
                Move(actor, sequence));
        }

        private static CombatCommand Move(int actor, uint sequence)
        {
            return CombatCommand.Move(
                new ParticipantId(actor),
                sequence,
                new Float3(1f, 0f, 0f),
                new Float3(1f, 0f, 0f));
        }
    }
}
