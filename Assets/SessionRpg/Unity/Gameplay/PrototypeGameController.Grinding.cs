using System;
using System.IO;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Presentation;
using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    public sealed partial class PrototypeGameController
    {
        private GrindingRegionRunController _grindingRun;
        private LocalGrindingProgressStore _grindingStore;
        private SpecializationId _grindingSaveSpecialization;
        private bool _grindingDirty;
        private bool _grindingSaveError;
        private float _grindingSaveRetryAt;
        private bool _grindingRecoveryPending;
        private int _grindingNoticeExperience;
        private bool _grindingNoticeLevel;
        private int _grindingLastKills = -1;
        private bool _grindingLastAtRefuge;
        private int _grindingLastNearestCamp = -1;
        private int _grindingLastCampDirection = -1;
        private int _grindingLastActiveCamp = -1;
        private ParticipantId _grindingLabelId;
        private int _grindingLabelLevel;
        private LevelingMobArchetypeId _grindingLabelArchetype;
        private string _grindingLabel;
        private string _grindingSaveDirectory;
        private Float3? _grindingCaptureSpawn;
        private readonly DeterministicLevelingMobBrain[,] _grindingBrains =
            new DeterministicLevelingMobBrain[3, 8];

        public bool HasGrindingSaveError => _grindingSaveError;
        public int GrindingExperience => _grindingRun != null ? _grindingRun.TotalExperience : 0;
        public int GrindingLevel => _grindingRun != null ? _grindingRun.Level : 1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD || SESSION_RPG_CAPTURE
        public bool IsGrindingCaptureReady => _selectedMode == GameModeId.PveLeveling &&
            _flow != null && _flow.Phase == SessionPhase.Playing && _simulation != null &&
            _simulation.CurrentTick >= Mathf.CeilToInt(_rules.TickRate * 1.25f);

        public void ConfigureGrindingCapture(string directory, int campIndex, float lateralOffset = 0f)
        {
            if (_simulation != null) { throw new InvalidOperationException("Configure capture before starting a session."); }
            _grindingSaveDirectory = directory;
            _grindingStore = null;
            if (campIndex >= 0)
            {
                Float3 center = GrindingRegionRunController.GetCampDefinition(campIndex).Center;
                _grindingCaptureSpawn = CombatRules.PveLevelingDefault.CollisionMap.ResolvePlacement(
                    new Float3(center.X - 14f, 0f, center.Z + Mathf.Clamp(lateralOffset, -7f, 7f)),
                    ArenaLayout.CombatantCollisionRadius, new Float3(-1f, 0f, 0f));
            }
        }
#endif

        private bool PrepareGrindingSession(GameModeId mode)
        {
            _grindingRun = null;
            _grindingRecoveryPending = false;
            _grindingSaveError = false;
            _grindingDirty = false;
            _grindingNoticeExperience = 0;
            _grindingLastKills = -1;
            _grindingLastNearestCamp = -1;
            _grindingLastCampDirection = -1;
            _grindingLastActiveCamp = -1;
            if (mode != GameModeId.PveLeveling) { return true; }
            try
            {
                if (_grindingStore == null)
                {
                    _grindingStore = new LocalGrindingProgressStore(
                        _grindingSaveDirectory ?? Path.Combine(UnityEngine.Application.persistentDataPath, "pve-characters"));
                }
                _grindingSaveSpecialization = _selectedSpecialization;
                _grindingRun = new GrindingRegionRunController(
                    _grindingStore.LoadExperience(_grindingSaveSpecialization));
                for (int slot = 0; slot < 3; slot++)
                {
                    for (int archetype = 0; archetype < 8; archetype++)
                    {
                        if (_grindingBrains[slot, archetype] == null)
                        {
                            _grindingBrains[slot, archetype] = new DeterministicLevelingMobBrain(
                                (LevelingMobArchetypeId)(archetype + 1));
                        }
                    }
                }
                return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException ||
                error is UnauthorizedAccessException)
            {
                _grindingSaveError = true;
                _hud?.ShowResults("LOCAL SAVE UNAVAILABLE",
                    "Your PvE save was preserved. Check the save folder before retrying. Training and Arena use separate data.",
                    "RETRY LOAD");
                Debug.LogWarning("PvE save could not be loaded; existing data was not reset.");
                return false;
            }
        }

        private void SetupGrindingPlayer()
        {
            Float3 position = _grindingCaptureSpawn ?? PveRegionLayout.Refuge;
            AddCombatant(0, 1, LocalTeamValue, _selectedClass, _selectedSpecialization, position, true, 110f);
            _grindingCaptureSpawn = null;
            if (_grindingRun.Level > 1)
            {
                _simulation.ApplyPersistentProgressionLevel(_humanId, _grindingRun.Level);
            }
        }

        private void AdvanceGrindingCamps()
        {
            if (_grindingRun == null || _grindingRecoveryPending ||
                !_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot player) || !player.IsAlive)
            {
                return;
            }

            // Retire only after the previous tick's complete event batch was read.
            // Identities are never reused within this simulation epoch.
            if (_grindingRun.TryAbandonCamp(_simulation, out GrindingCampDefinition abandoned))
            {
                for (int index = 0; index < abandoned.MobCount; index++)
                {
                    ParticipantId participant = abandoned.GetMob(index).Participant;
                    _simulation.TryDespawnRegionCombatant(participant);
                    int slot = FindViewIndex(participant);
                    if (slot >= 1)
                    {
                        _levelingMobBrains[slot] = null;
                        _combatantViews[slot]?.ReleaseToPool();
                    }
                }
                _focusedTarget = default;
                _targeting?.ClearSelection();
                _fighterHudBound = false;
            }
            for (int slot = 1; slot <= 3; slot++)
            {
                CombatantView view = _combatantViews[slot];
                if (view == null || !view.gameObject.activeSelf) { continue; }
                ParticipantId participant = view.Id;
                if (_simulation.TryGetSnapshot(participant, out CombatantSnapshot enemy) && !enemy.IsAlive)
                {
                    _simulation.TryRemoveDefeatedCombatant(participant);
                    _levelingMobBrains[slot] = null;
                    view.ReleaseToPool();
                    _fighterHudBound = false;
                    if (_focusedTarget == participant)
                    {
                        _focusedTarget = default;
                        _targeting?.ClearSelection();
                    }
                }
            }

            if (_grindingRun.ActiveMobCount == 0 && PveRegionLayout.IsAtRefuge(player.Position))
            {
                _simulation.TryRestoreHealth(_humanId, 14f * _rules.TickSeconds, out _);
                _simulation.TryRestoreResource(_humanId, player.ResourceType, 18f * _rules.TickSeconds, out _);
            }
            if (!_grindingRun.TryActivateCamp(_simulation.CurrentTick, player.Position, out GrindingCampDefinition camp))
            {
                return;
            }
            for (int index = 0; index < camp.MobCount; index++)
            {
                GrindingMobDefinition mob = camp.GetMob(index);
                LevelingMobArchetypeDefinition archetype = LevelingMobArchetypeCatalog.Get(mob.Archetype);
                int slot = index + 1;
                AddCombatant(slot, mob.Participant.Value, 2, archetype.Class, archetype.Specialization,
                    mob.Position, false, mob.BaseMaximumHealth, false);
                if (mob.Level > 1) { _simulation.ApplyPersistentProgressionLevel(mob.Participant, mob.Level); }
                DeterministicLevelingMobBrain brain = _grindingBrains[index, (int)mob.Archetype - 1];
                brain.Reset();
                _levelingMobBrains[slot] = brain;
                _combatantViews[slot].transform.localScale = Vector3.one * archetype.PresentationScale;
                _combatantViews[slot].ConfigureLevelingArchetype(mob.Archetype);
            }
            _fighterHudBound = false;
            _targeting?.RefreshNow();
            _hud?.ShowCombatNotice(camp.RoleHint, new Color(1f, 0.75f, 0.35f), 2.5f);
        }

        private void ObserveGrindingCombatEvent(CombatEvent combatEvent)
        {
            if (_grindingRun == null) { return; }
            GrindingCombatObservation observation = _grindingRun.ObserveCombatEvent(combatEvent);
            if (!observation.WasHandled) { return; }
            if (observation.ExperienceAwarded > 0)
            {
                _grindingDirty = true;
                _grindingNoticeExperience += observation.ExperienceAwarded;
                _grindingNoticeLevel |= observation.DidLevelUp;
                if (observation.DidLevelUp)
                {
                    _simulation.ApplyPersistentProgressionLevel(_humanId, _grindingRun.Level);
                }
            }
            if (observation.PlayerDied) { _grindingRecoveryPending = true; }
        }

        private bool FlushGrindingProgress(bool force = false)
        {
            if (!_grindingDirty || _grindingRun == null || _grindingStore == null) { return true; }
            if (!force && _grindingSaveError && Time.unscaledTime < _grindingSaveRetryAt) { return false; }
            try
            {
                // File IO is at an Update/leave boundary, never inside TickSession.
                _grindingStore.SaveExperience(_grindingSaveSpecialization, _grindingRun.TotalExperience);
                _grindingDirty = false;
                if (_grindingSaveError)
                {
                    _inputSource?.ClearTransientInput();
                    _hud?.SetAbilityControlsInteractable(true);
                }
                _grindingSaveError = false;
                if (_grindingNoticeExperience > 0)
                {
                    _hud?.ShowCombatNotice(_grindingNoticeLevel
                        ? "LEVEL " + _grindingRun.Level + "  •  HEALTH +12  •  BASE DAMAGE +8%  •  SAVED"
                        : "+" + _grindingNoticeExperience + " XP  •  SAVED ON THIS DEVICE",
                        new Color(1f, 0.77f, 0.3f), _grindingNoticeLevel ? 2.5f : 1.2f);
                    if (_grindingNoticeLevel) { _combatEffects?.PresentUiConfirm(); }
                }
                _grindingNoticeExperience = 0;
                _grindingNoticeLevel = false;
                return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
                error is InvalidOperationException)
            {
                if (!_grindingSaveError)
                {
                    _hud?.ShowCombatNotice("SAVE FAILED — PLAY PAUSED; PROGRESS KEPT IN MEMORY. CHECK DISK ACCESS.",
                        new Color(1f, 0.35f, 0.2f), 60f);
                    Debug.LogWarning("PvE saving failed. Play paused; the previous save was preserved.");
                }
                _grindingSaveError = true;
                _grindingSaveRetryAt = Time.unscaledTime + 2f;
                _hud?.SetAbilityControlsInteractable(false, "SAVE PAUSED");
                _inputSource?.ClearTransientInput();
                return false;
            }
        }

        private void RecoverGrindingAtRefuge()
        {
            if (_grindingRun == null || _grindingSaveError) { return; }
            _grindingRecoveryPending = false;
            _grindingRun.ResetAfterDeath(0);
            _simulation = new CombatSimulation(_rules);
            _humanSequence = 0;
            _accumulator = 0f;
            _focusedTarget = default;
            _spectatorTarget = default;
            _hasHumanDeathRecap = false;
            _deathRecapBuilder.Reset(_rules.TickRate);
            Array.Clear(_levelingMobBrains, 0, _levelingMobBrains.Length);
            DeactivateCombatants();
            SetupGrindingPlayer();
            _cameraRig?.SetFollowTarget(FindView(_humanId).transform, true);
            _offscreenThreats?.Clear();
            _targeting?.ClearSelection();
            _inputSource?.ClearTransientInput();
            _fighterHudBound = false;
            _hud?.SetAbilityControlsInteractable(true);
            _hud?.ShowCombatNotice("RECOVERED AT SUNBREAK REFUGE  •  EARNED XP RETAINED", new Color(0.8f, 0.85f, 1f), 3f);
            UpdateLevelingWorldFocus();
            PresentSnapshots();
        }

        private void RefreshGrindingProgressionHud()
        {
            if (_lastProgressionLevel == _grindingRun.Level &&
                _lastProgressionExperience == _grindingRun.TotalExperience) { return; }
            int level = _grindingRun.Level;
            int start = PersistentLevelProgression.GetCumulativeExperienceForLevel(level);
            int end = level >= 10 ? start : PersistentLevelProgression.GetCumulativeExperienceForLevel(level + 1);
            _hud?.SetLevelProgression(level, _grindingRun.TotalExperience, start, end, true);
            _lastProgressionLevel = level;
            _lastProgressionExperience = _grindingRun.TotalExperience;
        }

        private string DescribeGrindingMob(GrindingMobDefinition mob)
        {
            if (_grindingLabelId != mob.Participant || _grindingLabelLevel != mob.Level ||
                _grindingLabelArchetype != mob.Archetype)
            {
                _grindingLabelId = mob.Participant;
                _grindingLabelLevel = mob.Level;
                _grindingLabelArchetype = mob.Archetype;
                _grindingLabel = LevelingMobArchetypeCatalog.Get(mob.Archetype).DisplayName +
                    "  •  LV " + mob.Level;
            }
            return _grindingLabel;
        }

        private void RefreshGrindingObjective()
        {
            if (!_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot player)) { return; }
            bool atRefuge = PveRegionLayout.IsAtRefuge(player.Position);
            int nearestCamp = 0;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < PveRegionLayout.CampCount; index++)
            {
                float distance = (PveRegionLayout.GetCampCenter(index) - player.Position).PlanarLengthSquared;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCamp = index;
                }
            }
            Float3 campOffset = PveRegionLayout.GetCampCenter(nearestCamp) - player.Position;
            int direction = (Mathf.RoundToInt(Mathf.Atan2(campOffset.X, campOffset.Z) * Mathf.Rad2Deg / 45f) + 8) % 8;
            bool exploring = !atRefuge && _grindingRun.ActiveMobCount == 0;
            if ((!exploring || (_grindingLastNearestCamp == nearestCamp && _grindingLastCampDirection == direction)) &&
                _grindingLastActiveCamp == _grindingRun.ActiveCampIndex &&
                _lastObjectiveMobCount == _grindingRun.ActiveMobCount &&
                _grindingLastKills == _grindingRun.Kills && _lastObjectiveMode == _selectedMode &&
                _grindingLastAtRefuge == atRefuge) { return; }
            GrindingCampDefinition nearest = GrindingRegionRunController.GetCampDefinition(nearestCamp);
            string activity = _grindingRun.ActiveMobCount > 0
                ? GrindingRegionRunController.GetCampDefinition(_grindingRun.ActiveCampIndex).DisplayName +
                  "  •  " + _grindingRun.ActiveMobCount + (_grindingRun.ActiveMobCount == 1 ? " ENEMY" : " ENEMIES")
                : atRefuge ? "REFUGE  •  REST AND RECOVER  •  FIRST CAMP EAST"
                : nearest.DisplayName + "  •  LV " + nearest.Level + "  •  " + DescribeRegionDirection(direction);
            _hud.SetObjective("SUNBREAK BASIN  /  " + activity + "  •  " + _grindingRun.Kills + " KILLS");
            _grindingLastNearestCamp = nearestCamp;
            _grindingLastCampDirection = direction;
            _grindingLastActiveCamp = _grindingRun.ActiveCampIndex;
            _lastObjectiveMobCount = _grindingRun.ActiveMobCount;
            _grindingLastKills = _grindingRun.Kills;
            _grindingLastAtRefuge = atRefuge;
            _lastObjectiveMode = _selectedMode;
        }

        private static string DescribeRegionDirection(int direction)
        {
            switch (direction)
            {
                case 0: return "NORTH";
                case 1: return "NORTHEAST";
                case 2: return "EAST";
                case 3: return "SOUTHEAST";
                case 4: return "SOUTH";
                case 5: return "SOUTHWEST";
                case 6: return "WEST";
                default: return "NORTHWEST";
            }
        }
    }
}
