using System;
using SessionRpg.Core;
using Unity.Netcode;

namespace SessionRpg.Networking
{
    /// <summary>
    /// A transport-safe command request. Sender identity is deliberately excluded:
    /// the authority receives that identity from NGO RPC metadata so a client cannot
    /// forge it in the payload. Actor, target, and ability remain stable Core IDs.
    /// SessionEpoch prevents packets from an earlier replay being accepted.
    /// </summary>
    public struct NetworkCommandEnvelope : INetworkSerializable
    {
        private uint _sessionEpoch;
        private int _requestedTick;
        private int _actor;
        private uint _sequence;
        private float _movementX;
        private float _movementY;
        private float _movementZ;
        private float _facingX;
        private float _facingY;
        private float _facingZ;
        private int _ability;
        private int _target;
        private float _targetPointX;
        private float _targetPointY;
        private float _targetPointZ;
        private float _aimDirectionX;
        private float _aimDirectionY;
        private float _aimDirectionZ;

        public NetworkCommandEnvelope(
            uint sessionEpoch,
            int requestedTick,
            CombatCommand command)
        {
            _sessionEpoch = sessionEpoch;
            _requestedTick = requestedTick;
            _actor = command.Actor.Value;
            _sequence = command.Sequence;
            _movementX = command.Movement.X;
            _movementY = command.Movement.Y;
            _movementZ = command.Movement.Z;
            _facingX = command.Facing.X;
            _facingY = command.Facing.Y;
            _facingZ = command.Facing.Z;
            _ability = (int)command.Ability;
            _target = command.Target.Value;
            _targetPointX = command.TargetPoint.X;
            _targetPointY = command.TargetPoint.Y;
            _targetPointZ = command.TargetPoint.Z;
            _aimDirectionX = command.AimDirection.X;
            _aimDirectionY = command.AimDirection.Y;
            _aimDirectionZ = command.AimDirection.Z;
        }

        public uint SessionEpoch => _sessionEpoch;

        public int RequestedTick => _requestedTick;

        public ParticipantId Actor => new ParticipantId(_actor);

        public uint Sequence => _sequence;

        public AbilityId Ability => (AbilityId)_ability;

        public ParticipantId Target => new ParticipantId(_target);

        public CombatCommand ToCombatCommand()
        {
            return new CombatCommand(
                Actor,
                _sequence,
                new Float3(_movementX, _movementY, _movementZ),
                new Float3(_facingX, _facingY, _facingZ),
                (AbilityId)_ability,
                Target,
                new Float3(_targetPointX, _targetPointY, _targetPointZ),
                new Float3(_aimDirectionX, _aimDirectionY, _aimDirectionZ));
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref _sessionEpoch);
            serializer.SerializeValue(ref _requestedTick);
            serializer.SerializeValue(ref _actor);
            serializer.SerializeValue(ref _sequence);
            serializer.SerializeValue(ref _movementX);
            serializer.SerializeValue(ref _movementY);
            serializer.SerializeValue(ref _movementZ);
            serializer.SerializeValue(ref _facingX);
            serializer.SerializeValue(ref _facingY);
            serializer.SerializeValue(ref _facingZ);
            serializer.SerializeValue(ref _ability);
            serializer.SerializeValue(ref _target);
            serializer.SerializeValue(ref _targetPointX);
            serializer.SerializeValue(ref _targetPointY);
            serializer.SerializeValue(ref _targetPointZ);
            serializer.SerializeValue(ref _aimDirectionX);
            serializer.SerializeValue(ref _aimDirectionY);
            serializer.SerializeValue(ref _aimDirectionZ);
        }
    }

    public enum NetworkCommandDisposition
    {
        Accepted,
        SessionAborted,
        OutOfPhase,
        WrongSessionEpoch,
        UnknownParticipant,
        Unauthorized,
        InvalidSequence,
        DuplicateSequence,
        StaleSequence,
        RequestedTickTooOld,
        RequestedTickTooFarAhead,
        QueueFull,
    }

    public readonly struct NetworkCommandResult
    {
        public NetworkCommandResult(
            NetworkCommandDisposition disposition,
            ParticipantId actor,
            uint sequence,
            int hostTick)
        {
            Disposition = disposition;
            Actor = actor;
            Sequence = sequence;
            HostTick = hostTick;
        }

        public NetworkCommandDisposition Disposition { get; }

        public ParticipantId Actor { get; }

        public uint Sequence { get; }

        public int HostTick { get; }

        public bool Accepted => Disposition == NetworkCommandDisposition.Accepted;
    }

    /// <summary>
    /// Full deterministic combatant state needed to present and reconcile one
    /// authoritative participant. It deliberately mirrors Core values rather than
    /// relying on GameObject or NetworkObject identities.
    /// </summary>
    public struct NetworkCombatantSnapshot : INetworkSerializable
    {
        private int _participant;
        private int _team;
        private int _class;
        private int _specialization;
        private int _level;
        private float _positionX;
        private float _positionY;
        private float _positionZ;
        private float _facingX;
        private float _facingY;
        private float _facingZ;
        private float _health;
        private float _maxHealth;
        private float _barrier;
        private int _castingAbility;
        private int _castStartTick;
        private int _castEndTick;
        private int _castingActivationType;
        private int _castingSchool;
        private int _resourceType;
        private float _resource;
        private float _maxResource;
        private int _comboPoints;
        private int _comboTarget;
        private int _globalCooldownEndTick;
        private int _lockedSchools;
        private bool _isAlive;
        private bool _isInLava;
        private int _activeStatuses;
        private int _activeAuraTags;
        private int _dispellableBeneficialAuraTypes;

        public NetworkCombatantSnapshot(CombatantSnapshot snapshot)
        {
            _participant = snapshot.Id.Value;
            _team = snapshot.Team.Value;
            _class = (int)snapshot.Class;
            _specialization = (int)snapshot.Specialization;
            _level = snapshot.Level;
            _positionX = snapshot.Position.X;
            _positionY = snapshot.Position.Y;
            _positionZ = snapshot.Position.Z;
            _facingX = snapshot.Facing.X;
            _facingY = snapshot.Facing.Y;
            _facingZ = snapshot.Facing.Z;
            _health = snapshot.Health;
            _maxHealth = snapshot.MaxHealth;
            _barrier = snapshot.Barrier;
            _castingAbility = (int)snapshot.CastingAbility;
            _castStartTick = snapshot.CastStartTick;
            _castEndTick = snapshot.CastEndTick;
            _castingActivationType = (int)snapshot.CastingActivationType;
            _castingSchool = (int)snapshot.CastingSchool;
            _resourceType = (int)snapshot.ResourceType;
            _resource = snapshot.Resource;
            _maxResource = snapshot.MaxResource;
            _comboPoints = snapshot.ComboPoints;
            _comboTarget = snapshot.ComboTarget.Value;
            _globalCooldownEndTick = snapshot.GlobalCooldownEndTick;
            _lockedSchools = (int)snapshot.LockedSchools;
            _isAlive = snapshot.IsAlive;
            _isInLava = snapshot.IsInLava;
            _activeStatuses = (int)snapshot.ActiveStatuses;
            _activeAuraTags = (int)snapshot.ActiveAuraTags;
            _dispellableBeneficialAuraTypes =
                (int)snapshot.DispellableBeneficialAuraTypes;
        }

        public ParticipantId Participant => new ParticipantId(_participant);

        public TeamId Team => new TeamId(_team);

        public ClassId Class => (ClassId)_class;

        public SpecializationId Specialization => (SpecializationId)_specialization;

        public int Level => _level;

        public Float3 Position => new Float3(_positionX, _positionY, _positionZ);

        public Float3 Facing => new Float3(_facingX, _facingY, _facingZ);

        public float Health => _health;

        public float MaxHealth => _maxHealth;

        public float Barrier => _barrier;

        public AbilityId CastingAbility => (AbilityId)_castingAbility;

        public uint CastStartTick => (uint)Math.Max(0, _castStartTick);

        public uint CastEndTick => (uint)Math.Max(0, _castEndTick);

        public bool IsAlive => _isAlive;

        public bool IsInLava => _isInLava;

        public CombatantSnapshot ToCombatantSnapshot()
        {
            return new CombatantSnapshot(
                Participant,
                Team,
                (ClassId)_class,
                (SpecializationId)_specialization,
                Position,
                Facing,
                _health,
                _maxHealth,
                _barrier,
                (AbilityId)_castingAbility,
                _castStartTick,
                _castEndTick,
                (AbilityActivationType)_castingActivationType,
                (SpellSchoolMask)_castingSchool,
                (ResourceType)_resourceType,
                _resource,
                _maxResource,
                _comboPoints,
                new ParticipantId(_comboTarget),
                _globalCooldownEndTick,
                (SpellSchoolMask)_lockedSchools,
                _isAlive,
                _isInLava,
                (StatusFlags)_activeStatuses,
                (AuraTag)_activeAuraTags,
                (DispelType)_dispellableBeneficialAuraTypes,
                _level);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref _participant);
            serializer.SerializeValue(ref _team);
            serializer.SerializeValue(ref _class);
            serializer.SerializeValue(ref _specialization);
            serializer.SerializeValue(ref _level);
            serializer.SerializeValue(ref _positionX);
            serializer.SerializeValue(ref _positionY);
            serializer.SerializeValue(ref _positionZ);
            serializer.SerializeValue(ref _facingX);
            serializer.SerializeValue(ref _facingY);
            serializer.SerializeValue(ref _facingZ);
            serializer.SerializeValue(ref _health);
            serializer.SerializeValue(ref _maxHealth);
            serializer.SerializeValue(ref _barrier);
            serializer.SerializeValue(ref _castingAbility);
            serializer.SerializeValue(ref _castStartTick);
            serializer.SerializeValue(ref _castEndTick);
            serializer.SerializeValue(ref _castingActivationType);
            serializer.SerializeValue(ref _castingSchool);
            serializer.SerializeValue(ref _resourceType);
            serializer.SerializeValue(ref _resource);
            serializer.SerializeValue(ref _maxResource);
            serializer.SerializeValue(ref _comboPoints);
            serializer.SerializeValue(ref _comboTarget);
            serializer.SerializeValue(ref _globalCooldownEndTick);
            serializer.SerializeValue(ref _lockedSchools);
            serializer.SerializeValue(ref _isAlive);
            serializer.SerializeValue(ref _isInLava);
            serializer.SerializeValue(ref _activeStatuses);
            serializer.SerializeValue(ref _activeAuraTags);
            serializer.SerializeValue(ref _dispellableBeneficialAuraTypes);
        }
    }

    /// <summary>
    /// Acknowledgement data used by an owner to discard predicted commands through
    /// LastProcessedSequence, retain later commands, and replay them from the
    /// accompanying authoritative combatant snapshot.
    /// </summary>
    public struct OwnerReconciliationMetadata : INetworkSerializable
    {
        private int _participant;
        private int _serverTick;
        private uint _lastAcceptedSequence;
        private uint _lastProcessedSequence;
        private int _lastProcessedRequestedTick;

        public OwnerReconciliationMetadata(
            ParticipantId participant,
            int serverTick,
            uint lastAcceptedSequence,
            uint lastProcessedSequence,
            int lastProcessedRequestedTick)
        {
            _participant = participant.Value;
            _serverTick = serverTick;
            _lastAcceptedSequence = lastAcceptedSequence;
            _lastProcessedSequence = lastProcessedSequence;
            _lastProcessedRequestedTick = lastProcessedRequestedTick;
        }

        public ParticipantId Participant => new ParticipantId(_participant);

        public int ServerTick => _serverTick;

        public uint LastAcceptedSequence => _lastAcceptedSequence;

        public uint LastProcessedSequence => _lastProcessedSequence;

        public int LastProcessedRequestedTick => _lastProcessedRequestedTick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref _participant);
            serializer.SerializeValue(ref _serverTick);
            serializer.SerializeValue(ref _lastAcceptedSequence);
            serializer.SerializeValue(ref _lastProcessedSequence);
            serializer.SerializeValue(ref _lastProcessedRequestedTick);
        }
    }

    public struct AuthoritativeSnapshotSlot : INetworkSerializable
    {
        private NetworkCombatantSnapshot _combatant;
        private OwnerReconciliationMetadata _reconciliation;

        public AuthoritativeSnapshotSlot(
            NetworkCombatantSnapshot combatant,
            OwnerReconciliationMetadata reconciliation)
        {
            _combatant = combatant;
            _reconciliation = reconciliation;
        }

        public NetworkCombatantSnapshot Combatant => _combatant;

        public OwnerReconciliationMetadata Reconciliation => _reconciliation;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeNetworkSerializable(ref _combatant);
            serializer.SerializeNetworkSerializable(ref _reconciliation);
        }
    }

    /// <summary>
    /// Fixed eight-slot authoritative snapshot frame. The room authority remains a
    /// four-player Arena policy, while the larger state envelope can carry the
    /// roadmap's eight active PvE combatants. Fixed storage keeps the wire and
    /// runtime bound explicit and avoids retaining Core's reused snapshot data.
    /// </summary>
    public struct AuthoritativeSnapshotEnvelope : INetworkSerializable
    {
        public const int MaximumSlots = 8;

        private uint _sessionEpoch;
        private int _serverTick;
        private uint _snapshotSequence;
        private byte _slotCount;
        private AuthoritativeSnapshotSlot _slot0;
        private AuthoritativeSnapshotSlot _slot1;
        private AuthoritativeSnapshotSlot _slot2;
        private AuthoritativeSnapshotSlot _slot3;
        private AuthoritativeSnapshotSlot _slot4;
        private AuthoritativeSnapshotSlot _slot5;
        private AuthoritativeSnapshotSlot _slot6;
        private AuthoritativeSnapshotSlot _slot7;

        public AuthoritativeSnapshotEnvelope(
            uint sessionEpoch,
            int serverTick,
            uint snapshotSequence)
        {
            _sessionEpoch = sessionEpoch;
            _serverTick = serverTick;
            _snapshotSequence = snapshotSequence;
            _slotCount = 0;
            _slot0 = default;
            _slot1 = default;
            _slot2 = default;
            _slot3 = default;
            _slot4 = default;
            _slot5 = default;
            _slot6 = default;
            _slot7 = default;
        }

        public uint SessionEpoch => _sessionEpoch;

        public int ServerTick => _serverTick;

        public uint SnapshotSequence => _snapshotSequence;

        public int SlotCount => _slotCount;

        public bool TryAppend(AuthoritativeSnapshotSlot slot)
        {
            switch (_slotCount)
            {
                case 0:
                    _slot0 = slot;
                    break;
                case 1:
                    _slot1 = slot;
                    break;
                case 2:
                    _slot2 = slot;
                    break;
                case 3:
                    _slot3 = slot;
                    break;
                case 4:
                    _slot4 = slot;
                    break;
                case 5:
                    _slot5 = slot;
                    break;
                case 6:
                    _slot6 = slot;
                    break;
                case 7:
                    _slot7 = slot;
                    break;
                default:
                    return false;
            }

            _slotCount++;
            return true;
        }

        public AuthoritativeSnapshotSlot GetSlot(int index)
        {
            if (index < 0 || index >= _slotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0:
                    return _slot0;
                case 1:
                    return _slot1;
                case 2:
                    return _slot2;
                case 3:
                    return _slot3;
                case 4:
                    return _slot4;
                case 5:
                    return _slot5;
                case 6:
                    return _slot6;
                default:
                    return _slot7;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref _sessionEpoch);
            serializer.SerializeValue(ref _serverTick);
            serializer.SerializeValue(ref _snapshotSequence);
            serializer.SerializeValue(ref _slotCount);
            serializer.SerializeNetworkSerializable(ref _slot0);
            serializer.SerializeNetworkSerializable(ref _slot1);
            serializer.SerializeNetworkSerializable(ref _slot2);
            serializer.SerializeNetworkSerializable(ref _slot3);
            serializer.SerializeNetworkSerializable(ref _slot4);
            serializer.SerializeNetworkSerializable(ref _slot5);
            serializer.SerializeNetworkSerializable(ref _slot6);
            serializer.SerializeNetworkSerializable(ref _slot7);

            if (serializer.IsReader && _slotCount > MaximumSlots)
            {
                _slotCount = MaximumSlots;
            }
        }
    }

    /// <summary>
    /// Ordered reliable-event payload. EventSequence establishes a total stream
    /// order independently of tick ordering, while the Core event keeps stable
    /// actor/target/ability identities.
    /// </summary>
    public struct AuthoritativeEventEnvelope : INetworkSerializable
    {
        public const int MaximumReasonLength = 96;

        private uint _sessionEpoch;
        private uint _eventSequence;
        private int _tick;
        private int _kind;
        private int _actor;
        private int _target;
        private int _ability;
        private int _status;
        private int _aura;
        private float _amount;
        private float _vectorX;
        private float _vectorY;
        private float _vectorZ;
        private string _reason;

        public AuthoritativeEventEnvelope(
            uint sessionEpoch,
            uint eventSequence,
            CombatEvent combatEvent)
        {
            _sessionEpoch = sessionEpoch;
            _eventSequence = eventSequence;
            _tick = combatEvent.Tick;
            _kind = (int)combatEvent.Kind;
            _actor = combatEvent.Actor.Value;
            _target = combatEvent.Target.Value;
            _ability = (int)combatEvent.Ability;
            _status = (int)combatEvent.Status;
            _aura = (int)combatEvent.Aura;
            _amount = combatEvent.Amount;
            _vectorX = combatEvent.Vector.X;
            _vectorY = combatEvent.Vector.Y;
            _vectorZ = combatEvent.Vector.Z;
            _reason = BoundReason(combatEvent.Reason);
        }

        public uint SessionEpoch => _sessionEpoch;

        public uint EventSequence => _eventSequence;

        public int Tick => _tick;

        public CombatEventKind Kind => (CombatEventKind)_kind;

        public ParticipantId Actor => new ParticipantId(_actor);

        public ParticipantId Target => new ParticipantId(_target);

        public AbilityId Ability => (AbilityId)_ability;

        public string Reason => _reason ?? string.Empty;

        public CombatEvent ToCombatEvent()
        {
            return CombatEvent.Create(
                _tick,
                (CombatEventKind)_kind,
                Actor,
                Target,
                (AbilityId)_ability,
                (StatusId)_status,
                _amount,
                new Float3(_vectorX, _vectorY, _vectorZ),
                Reason,
                (AuraId)_aura);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            if (serializer.IsWriter)
            {
                _reason = BoundReason(_reason);
            }

            serializer.SerializeValue(ref _sessionEpoch);
            serializer.SerializeValue(ref _eventSequence);
            serializer.SerializeValue(ref _tick);
            serializer.SerializeValue(ref _kind);
            serializer.SerializeValue(ref _actor);
            serializer.SerializeValue(ref _target);
            serializer.SerializeValue(ref _ability);
            serializer.SerializeValue(ref _status);
            serializer.SerializeValue(ref _aura);
            serializer.SerializeValue(ref _amount);
            serializer.SerializeValue(ref _vectorX);
            serializer.SerializeValue(ref _vectorY);
            serializer.SerializeValue(ref _vectorZ);
            serializer.SerializeValue(ref _reason, true);

            if (serializer.IsReader)
            {
                _reason = BoundReason(_reason);
            }
        }

        private static string BoundReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return string.Empty;
            }

            return reason.Length <= MaximumReasonLength
                ? reason
                : reason.Substring(0, MaximumReasonLength);
        }
    }
}
