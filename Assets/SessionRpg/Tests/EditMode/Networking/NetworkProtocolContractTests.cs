using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Networking.Tests
{
    public sealed class NetworkProtocolContractTests
    {
        [Test]
        public void HostTickClock_AdvancesAtThirtyHertzAndCapsBacklog()
        {
            HostTickClock clock = new HostTickClock(maximumPendingTicks: 3);
            clock.AddElapsedSeconds(1.0 / 15.0);
            Assert.That(clock.PendingTickCount, Is.EqualTo(2));
            Assert.That(clock.TryConsumeTick(out int first), Is.True);
            Assert.That(clock.TryConsumeTick(out int second), Is.True);
            Assert.That(clock.TryConsumeTick(out _), Is.False);
            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(2));

            clock.AddElapsedSeconds(10.0);
            Assert.That(clock.PendingTickCount, Is.EqualTo(3));
            Assert.That(HostTickClock.TickRate, Is.EqualTo(30));
        }

        [Test]
        public void CommandEnvelope_RoundTripsCoreCommandAndStableIds()
        {
            CombatCommand command = CombatCommand.Cast(
                new ParticipantId(2),
                9,
                new Float3(1f, 0f, 2f),
                new Float3(0f, 0f, 1f),
                AbilityId.Blink,
                new ParticipantId(4),
                new Float3(3f, 0f, 4f),
                new Float3(0.5f, 0f, 0.5f));
            NetworkCommandEnvelope envelope = new NetworkCommandEnvelope(
                3,
                17,
                command);
            CombatCommand restored = envelope.ToCombatCommand();

            Assert.That(envelope.SessionEpoch, Is.EqualTo(3));
            Assert.That(envelope.RequestedTick, Is.EqualTo(17));
            Assert.That(restored.Actor, Is.EqualTo(command.Actor));
            Assert.That(restored.Sequence, Is.EqualTo(command.Sequence));
            Assert.That(restored.Ability, Is.EqualTo(command.Ability));
            Assert.That(restored.Target, Is.EqualTo(command.Target));
            Assert.That(restored.Movement, Is.EqualTo(command.Movement));
            Assert.That(restored.TargetPoint, Is.EqualTo(command.TargetPoint));
        }

        [Test]
        public void SnapshotEnvelope_ContainsOwnerAcknowledgementForPredictionReplay()
        {
            HostAuthoritativeSession session = new HostAuthoritativeSession(10);
            Assert.That(session.BeginSession(), Is.True);
            Assert.That(session.SubmitHostCommand(0, CombatCommand.Move(
                new ParticipantId(1),
                1,
                new Float3(1f, 0f, 0f),
                new Float3(1f, 0f, 0f))).Accepted, Is.True);
            Assert.That(session.AdvanceHostTick(new NetworkCommandEnvelope[4]), Is.EqualTo(1));

            CombatantSnapshot source = new CombatantSnapshot(
                new ParticipantId(1),
                new TeamId(1),
                ClassId.Mage,
                new Float3(2f, 0f, 3f),
                new Float3(0f, 0f, 1f),
                75f,
                100f,
                12f,
                AbilityId.Fireball,
                14,
                true,
                false);
            AuthoritativeSnapshotEnvelope envelope = session.CreateSnapshotEnvelope(
                new[] { source },
                1);
            AuthoritativeSnapshotSlot slot = envelope.GetSlot(0);

            Assert.That(envelope.SessionEpoch, Is.EqualTo(session.Room.SessionEpoch));
            Assert.That(envelope.ServerTick, Is.EqualTo(1));
            Assert.That(envelope.SnapshotSequence, Is.EqualTo(1));
            Assert.That(envelope.SlotCount, Is.EqualTo(1));
            Assert.That(slot.Combatant.Participant, Is.EqualTo(source.Id));
            Assert.That(slot.Combatant.Position, Is.EqualTo(source.Position));
            Assert.That(slot.Combatant.Health, Is.EqualTo(source.Health));
            Assert.That(slot.Reconciliation.Participant, Is.EqualTo(source.Id));
            Assert.That(slot.Reconciliation.LastAcceptedSequence, Is.EqualTo(1));
            Assert.That(slot.Reconciliation.LastProcessedSequence, Is.EqualTo(1));
            Assert.That(slot.Reconciliation.LastProcessedRequestedTick, Is.EqualTo(0));

            CombatantSnapshot restored = slot.Combatant.ToCombatantSnapshot();
            Assert.That(restored.Id, Is.EqualTo(source.Id));
            Assert.That(restored.Class, Is.EqualTo(source.Class));
            Assert.That(restored.Health, Is.EqualTo(source.Health));
        }

        [Test]
        public void SnapshotEnvelope_IsHardBoundedToEightPveSlots()
        {
            AuthoritativeSnapshotEnvelope envelope =
                new AuthoritativeSnapshotEnvelope(1, 2, 3);
            AuthoritativeSnapshotSlot slot = default;
            for (int index = 0; index < AuthoritativeSnapshotEnvelope.MaximumSlots; index++)
            {
                Assert.That(envelope.TryAppend(slot), Is.True);
            }

            Assert.That(envelope.TryAppend(slot), Is.False);
            Assert.That(envelope.SlotCount,
                Is.EqualTo(AuthoritativeSnapshotEnvelope.MaximumSlots));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                envelope.GetSlot(AuthoritativeSnapshotEnvelope.MaximumSlots));
        }

        [Test]
        public void EventEnvelope_PreservesOrderingAndBoundsReason()
        {
            string longReason = new string('x', 120);
            CombatEvent source = CombatEvent.Create(
                21,
                CombatEventKind.CastRejected,
                new ParticipantId(1),
                new ParticipantId(4),
                AbilityId.Fireball,
                reason: longReason);
            HostAuthoritativeSession session = new HostAuthoritativeSession(10);
            Assert.That(session.BeginSession(), Is.True);

            AuthoritativeEventEnvelope first = session.CreateEventEnvelope(source);
            AuthoritativeEventEnvelope second = session.CreateEventEnvelope(source);
            CombatEvent restored = first.ToCombatEvent();

            Assert.That(first.EventSequence, Is.EqualTo(1));
            Assert.That(second.EventSequence, Is.EqualTo(2));
            Assert.That(first.Reason.Length,
                Is.EqualTo(AuthoritativeEventEnvelope.MaximumReasonLength));
            Assert.That(restored.Tick, Is.EqualTo(source.Tick));
            Assert.That(restored.Kind, Is.EqualTo(source.Kind));
            Assert.That(restored.Actor, Is.EqualTo(source.Actor));
            Assert.That(restored.Target, Is.EqualTo(source.Target));
            Assert.That(restored.Ability, Is.EqualTo(source.Ability));
        }
    }
}
