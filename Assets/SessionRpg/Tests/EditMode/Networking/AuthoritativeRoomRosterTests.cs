using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Networking.Tests
{
    public sealed class AuthoritativeRoomRosterTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void BeginSession_FillsFourSlotsForOneThroughFourHumans(int humanCount)
        {
            AuthoritativeRoomRoster room = new AuthoritativeRoomRoster(10);
            for (int index = 1; index < humanCount; index++)
            {
                Assert.That(
                    room.TryJoinClient((ulong)(10 + index), out ParticipantId participant),
                    Is.EqualTo(RoomJoinResult.Joined));
                Assert.That(participant.Value, Is.EqualTo(index + 1));
            }

            Assert.That(room.BeginSession(), Is.True);
            Assert.That(room.Phase, Is.EqualTo(AuthoritativeRoomPhase.Playing));
            Assert.That(room.HumanCount, Is.EqualTo(humanCount));
            Assert.That(room.BotCount, Is.EqualTo(4 - humanCount));
            Assert.That(room.AdmissionClosed, Is.True);

            for (int index = 0; index < AuthoritativeRoomRoster.SlotCount; index++)
            {
                AuthoritativeRoomSlot slot = room.GetSlot(index);
                Assert.That(slot.Participant.Value, Is.EqualTo(index + 1));
                Assert.That(slot.Team.Value, Is.EqualTo(index < 2 ? 1 : 2));
                Assert.That(slot.Controller, Is.Not.EqualTo(RoomSlotControllerKind.Empty));
            }
        }

        [Test]
        public void Room_RejectsOverflowLateJoinAndReconnect()
        {
            AuthoritativeRoomRoster room = new AuthoritativeRoomRoster(10);
            Assert.That(room.TryJoinClient(20, out _), Is.EqualTo(RoomJoinResult.Joined));
            Assert.That(room.TryJoinClient(30, out _), Is.EqualTo(RoomJoinResult.Joined));
            Assert.That(room.TryJoinClient(40, out _), Is.EqualTo(RoomJoinResult.Joined));
            Assert.That(room.TryJoinClient(50, out _), Is.EqualTo(RoomJoinResult.RoomFull));

            Assert.That(
                room.Disconnect(20),
                Is.EqualTo(RoomDisconnectResult.ClientReplacedByBot));
            Assert.That(
                room.TryJoinClient(20, out _),
                Is.EqualTo(RoomJoinResult.ReconnectRejected));

            Assert.That(room.BeginSession(), Is.True);
            Assert.That(
                room.TryJoinClient(60, out _),
                Is.EqualTo(RoomJoinResult.AdmissionClosed));
        }

        [Test]
        public void ClientDisconnect_BecomesBotButHostDisconnectAborts()
        {
            AuthoritativeRoomRoster room = new AuthoritativeRoomRoster(10);
            Assert.That(room.TryJoinClient(20, out ParticipantId client),
                Is.EqualTo(RoomJoinResult.Joined));
            Assert.That(room.BeginSession(), Is.True);

            Assert.That(
                room.Disconnect(20),
                Is.EqualTo(RoomDisconnectResult.ClientReplacedByBot));
            Assert.That(room.TryGetSlot(client, out AuthoritativeRoomSlot slot), Is.True);
            Assert.That(slot.Controller, Is.EqualTo(RoomSlotControllerKind.Bot));
            Assert.That(slot.IsConnected, Is.False);

            Assert.That(
                room.Disconnect(10),
                Is.EqualTo(RoomDisconnectResult.HostAbortedSession));
            Assert.That(room.Phase, Is.EqualTo(AuthoritativeRoomPhase.Aborted));
            Assert.That(room.StartReplay(), Is.False);
        }

        [Test]
        public void ResultsReplay_PreservesRosterAndPermanentlyClosesAdmission()
        {
            AuthoritativeRoomRoster room = new AuthoritativeRoomRoster(10);
            Assert.That(room.TryJoinClient(20, out ParticipantId client),
                Is.EqualTo(RoomJoinResult.Joined));
            Assert.That(room.BeginSession(), Is.True);
            uint firstEpoch = room.SessionEpoch;
            Assert.That(room.Disconnect(20),
                Is.EqualTo(RoomDisconnectResult.ClientReplacedByBot));

            Assert.That(room.EnterResults(), Is.True);
            Assert.That(room.StartReplay(), Is.True);
            Assert.That(room.SessionEpoch, Is.Not.EqualTo(firstEpoch));
            Assert.That(room.Phase, Is.EqualTo(AuthoritativeRoomPhase.Playing));
            Assert.That(room.TryGetSlot(client, out AuthoritativeRoomSlot slot), Is.True);
            Assert.That(slot.Controller, Is.EqualTo(RoomSlotControllerKind.Bot));
            Assert.That(room.TryJoinClient(30, out _),
                Is.EqualTo(RoomJoinResult.AdmissionClosed));
        }
    }
}
