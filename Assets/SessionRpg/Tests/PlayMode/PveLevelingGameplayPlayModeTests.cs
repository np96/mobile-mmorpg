using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.UI;
using SessionRpg.Unity.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class PveLevelingGameplayPlayModeTests
    {
        private static readonly ParticipantId Player = new ParticipantId(1);
        private PrototypeGameController _controller;
        private PrototypeHud _hud;
        private string _saveDirectory;
        private uint _commandSequence;

        [UnitySetUp]
        public IEnumerator LoadBootstrapWithIsolatedProgress()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(),
                "session-rpg-pve-playmode-" + Guid.NewGuid().ToString("N"));
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            _controller = Object.FindAnyObjectByType<PrototypeGameController>();
            _hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(_controller, Is.Not.Null);
            Assert.That(_hud, Is.Not.Null);
            Assert.That(_hud.Screen, Is.EqualTo(PrototypeHudScreen.Home));
            _controller.enabled = false;
            SetField(_controller, "_grindingSaveDirectory", _saveDirectory);
            _commandSequence = 0;
        }

        [UnityTearDown]
        public IEnumerator ReleaseIsolatedProgress()
        {
            if (_controller != null)
            {
                _controller.ReturnHome();
                // A deliberately failed-save test must not retry after its
                // temporary directory is removed during fixture cleanup.
                SetField(_controller, "_grindingDirty", false);
                SetField(_controller, "_grindingStore", null);
                SetField(_controller, "_grindingRun", null);
            }

            yield return null;
            if (Directory.Exists(_saveDirectory))
            {
                Directory.Delete(_saveDirectory, true);
            }
        }

        [UnityTest]
        public IEnumerator AuthoredHome_RoutesPersistentPveSeparatelyFromTraining()
        {
            Button[] buttons = _hud.GetComponentsInChildren<Button>(true);
            Button grinding = buttons.Single(button => button.name == "PveLeveling");
            Button training = buttons.Single(button => button.name == "LevelingExpedition");
            Button start = GetField<Button>(_hud, "_startButton");
            Assert.That(grinding, Is.Not.SameAs(training));
            Assert.That(grinding.GetComponentInChildren<Text>().text, Does.Contain("SAVED ON THIS DEVICE"));

            grinding.onClick.Invoke();
            Assert.That(_hud.SelectedMode, Is.EqualTo(PrototypeMode.PveLeveling));
            start.onClick.Invoke();
            Assert.That(Flow.Mode, Is.EqualTo(GameModeId.PveLeveling));
            Assert.That(GetField<GrindingRegionRunController>(_controller, "_grindingRun"), Is.Not.Null);
            Assert.That(GetField<GuidedTrainingController>(_controller, "_training"), Is.Null);

            _controller.ReturnHome();
            training.onClick.Invoke();
            Assert.That(_hud.SelectedMode, Is.EqualTo(PrototypeMode.Training));
            start.onClick.Invoke();
            Assert.That(Flow.Mode, Is.EqualTo(GameModeId.Leveling));
            Assert.That(GetField<GuidedTrainingController>(_controller, "_training"), Is.Not.Null);
            Assert.That(GetField<GrindingRegionRunController>(_controller, "_grindingRun"), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllSevenSpecializations_StartAloneAtRefugeWithTheirFullKit()
        {
            CombatantView[] pool = Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Include);
            Assert.That(pool, Has.Length.EqualTo(12));

            for (int value = 1; value <= 7; value++)
            {
                SpecializationId specialization = (SpecializationId)value;
                AbilityKit kit = SpecializationCatalog.GetKit(specialization);
                _controller.StartLocalSession(GameModeId.PveLeveling, kit.OwnerClass, specialization);

                Assert.That(Flow.Mode, Is.EqualTo(GameModeId.PveLeveling));
                Assert.That(Simulation.CombatantCount, Is.EqualTo(1));
                CombatantView[] active = Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
                Assert.That(active, Has.Length.EqualTo(1));
                Assert.That(active[0].IsHuman, Is.True);
                Assert.That(active[0].Team.Value, Is.EqualTo(1));
                Assert.That(pool.Contains(active[0]), Is.True);
                Assert.That(GetPlayer().Position, Is.EqualTo(PveRegionLayout.Refuge));
                Assert.That(_controller.GrindingExperience, Is.Zero);
                Assert.That(_controller.GrindingLevel, Is.EqualTo(1));
                Assert.That(GetField<LevelingRunController>(_controller, "_levelingRun"), Is.Null);
                Assert.That(GetField<GuidedTrainingController>(_controller, "_training"), Is.Null);

                WorldZoneStreamController world = GetField<WorldZoneStreamController>(_controller, "_worldStreamer");
                Assert.That(world.IsZoneActive, Is.True);
                Assert.That(world.ActiveWorldId, Is.EqualTo(1u));
                Assert.That(world.ActiveZoneId, Is.EqualTo(PveRegionLayout.ZoneIdValue));
                for (int slot = 0; slot < AbilityKit.Capacity; slot++)
                {
                    AbilitySlotView view = _hud.GetAbilitySlot(slot);
                    Assert.That(view.gameObject.activeSelf, Is.True, specialization + " slot " + slot);
                    Assert.That(view.AbilityId, Is.EqualTo(kit.GetAbility(slot)));
                    for (int direction = 1; direction <= 4; direction++)
                    {
                        AbilitySlotDirection binding = (AbilitySlotDirection)direction;
                        Assert.That(view.GetDirectionalAbility(binding), Is.EqualTo(kit.GetAbility(slot, binding)));
                    }
                }

                _controller.ReturnHome();
                Assert.That(Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude), Is.Empty);
                yield return null;
            }

            Assert.That(Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Include), Is.EquivalentTo(pool));
        }

        [UnityTest]
        public IEnumerator BasinStreaming_VisitsBothAxesAndReturnsToTrainingWithoutStaleChunks()
        {
            StartFireAndFinishCountdown();
            WorldZoneStreamController streamer = GetField<WorldZoneStreamController>(_controller, "_worldStreamer");
            Assert.That(streamer.Catalog.TryGetZone(1u, 2u, out WorldZoneEntry basin), Is.True);
            Assert.That(basin.ChunkCount, Is.EqualTo(9));
            Assert.That(basin.MaxLoadedChunks, Is.EqualTo(9));

            // Deliberately drive only the visual streamer here. Core traversal
            // and camp authority are covered separately; chunks never move actors.
            Float3 original = GetPlayer().Position;
            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    streamer.SetFocus(new Vector3(x * 64f + 32f, 0f, z * 64f + 32f));
                    yield return WaitForStreamReady(streamer);
                    Assert.That(streamer.FocusCoordinate, Is.EqualTo(new WorldChunkCoordinate(x, z)));
                    Assert.That(streamer.ResidentChunkCount, Is.LessThanOrEqualTo(9));
                    bool foundFocus = false;
                    for (int index = 0; index < streamer.LoadedChunkCount; index++)
                    {
                        Assert.That(streamer.TryGetLoadedChunk(index, out LoadedWorldChunk chunk), Is.True);
                        Assert.That(chunk.WorldId, Is.EqualTo(1u));
                        Assert.That(chunk.ZoneId, Is.EqualTo(2u));
                        foundFocus |= chunk.Coordinate == new WorldChunkCoordinate(x, z);
                    }
                    Assert.That(foundFocus, Is.True);
                    Assert.That(GetPlayer().Position, Is.EqualTo(original));
                }
            }

            _controller.StartLocalSession(GameModeId.Leveling, ClassId.Mage, SpecializationId.Fire);
            yield return WaitForStreamReady(streamer);
            Assert.That(streamer.ActiveZoneId, Is.EqualTo(1u));
            for (int index = 0; index < streamer.LoadedChunkCount; index++)
            {
                Assert.That(streamer.TryGetLoadedChunk(index, out LoadedWorldChunk chunk), Is.True);
                Assert.That(chunk.ZoneId, Is.EqualTo(1u));
            }
            Assert.That(GetPlayer().Position.X, Is.LessThan(-175f));
            _controller.ReturnHome();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (streamer.Status != WorldZoneStreamStatus.Inactive && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(streamer.ResidentChunkCount, Is.Zero);
        }

        [Test]
        public void ObjectiveCache_TracksCampIdentityAndIgnoresUnusedCompassChangesInCombat()
        {
            StartFireAndFinishCountdown();
            GrindingRegionRunController run = GetField<GrindingRegionRunController>(_controller, "_grindingRun");
            Assert.That(run.TryActivateCamp(0, PveRegionLayout.GetCampCenter(0), out _), Is.True);
            MethodInfo method = typeof(PrototypeGameController).GetMethod("RefreshGrindingObjective",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Action refresh = (Action)Delegate.CreateDelegate(typeof(Action), _controller, method);
            refresh();
            Text objective = GetField<Text>(_hud, "_objectiveLabel");
            Assert.That(objective.text, Does.Contain("CINDER OUTRIDERS"));

            // Force a stale exploration key while two enemies remain. That key
            // is not visible during combat and must not rebuild the same text.
            SetField(_controller, "_grindingLastNearestCamp", -1);
            SetField(_controller, "_grindingLastCampDirection", -1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 100; index++) { refresh(); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);

            // Two camps can exchange in one tick without changing enemy count
            // or kills. The objective must still change its stable camp identity.
            Assert.That(run.TryAbandonCamp(1, PveRegionLayout.GetCampCenter(2), out _), Is.True);
            Assert.That(run.TryActivateCamp(1, PveRegionLayout.GetCampCenter(2), out _), Is.True);
            refresh();
            Assert.That(objective.text, Does.Contain("BROKEN GATE GUARD"));
            Assert.That(objective.text, Does.Not.Contain("CINDER OUTRIDERS"));
        }

        [Test]
        public void BasinGameplay_AllowsNorthwardMovementBeyondTheOldLaneWidth()
        {
            StartFireAndFinishCountdown();
            Float3 start = GetPlayer().Position;
            for (int tick = 0; tick < 100; tick++)
            {
                Float3 north = new Float3(0f, 0f, 1f);
                Assert.That(Simulation.SubmitCommand(CombatCommand.Move(
                    Player, ++_commandSequence, north, north)), Is.True);
                Tick();
            }
            Assert.That(GetPlayer().Position.Z - start.Z, Is.GreaterThan(15f));
            Assert.That(GetPlayer().Position.X, Is.EqualTo(start.X));
            Assert.That(CombatRules.PveLevelingDefault.CollisionMap.IsPositionValid(
                GetPlayer().Position, ArenaLayout.CombatantCollisionRadius), Is.True);
        }

        [Test]
        public void OrdinaryLeashCrossing_RetainsNearbyCreepViewsUntilAllSurvivorsAreDistant()
        {
            StartFireAndFinishCountdown();
            ApproachFirstCamp(true);
            GrindingRegionRunController run = GetField<GrindingRegionRunController>(_controller, "_grindingRun");
            CombatantView[] pool = Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Include);
            CombatantView[] originalEnemies = pool.Where(view => view.gameObject.activeSelf && !view.IsHuman).ToArray();
            ParticipantId[] originalIds = originalEnemies.Select(view => view.Id).ToArray();
            Assert.That(originalEnemies, Has.Length.EqualTo(2));
            Float3 center = PveRegionLayout.GetCampCenter(0);
            Float3 playerPosition = center + new Float3(46f, 0f, 0f);

            // A bounded fixture reconstructs exactly the same active identities
            // at a chased leash crossing; no diagnostic movement enters runtime.
            ReplaceGrindingSnapshotFixture(playerPosition,
                playerPosition + new Float3(-2f, 0f, 0f), originalIds);
            SetField(_controller, "_focusedTarget", originalIds[0]);
            Invoke("PresentSnapshots");
            Invoke("AdvanceGrindingCamps");

            Assert.That(run.IsWithinActiveCampLeash(playerPosition), Is.False);
            Assert.That(run.ActiveCampIndex, Is.Zero);
            Assert.That(Simulation.CombatantCount, Is.EqualTo(3));
            Assert.That(GetField<ParticipantId>(_controller, "_focusedTarget"), Is.EqualTo(originalIds[0]));
            for (int index = 0; index < originalEnemies.Length; index++)
            {
                Assert.That(originalEnemies[index].gameObject.activeSelf, Is.True,
                    "The previous implementation pooled this living nearby creep at 45 m.");
                Assert.That(originalEnemies[index].Id, Is.EqualTo(originalIds[index]));
                Assert.That(originalEnemies[index].IsAlive, Is.True);
                Assert.That(originalEnemies[index].IsStealthed, Is.False);
            }

            // Exercise the actual controller tick/brain path as well as the
            // retirement gate: the retained creep must now walk toward home.
            Assert.That(run.TryGetMob(originalIds[0], out GrindingMobDefinition returningMob), Is.True);
            CombatantView[] assignedViews = GetField<CombatantView[]>(_controller, "_combatantViews");
            int returningSlot = Array.IndexOf(assignedViews, originalEnemies[0]);
            Assert.That(returningSlot, Is.GreaterThan(0));
            GetField<DeterministicLevelingMobBrain[]>(_controller, "_levelingMobBrains")[returningSlot] =
                new DeterministicLevelingMobBrain(returningMob.Archetype);
            Assert.That(Simulation.TryGetSnapshot(originalIds[0], out CombatantSnapshot beforeReturn), Is.True);
            Tick();
            Assert.That(Simulation.TryGetSnapshot(originalIds[0], out CombatantSnapshot afterReturn), Is.True);
            Assert.That(Float3.PlanarDistance(afterReturn.Position, center),
                Is.LessThan(Float3.PlanarDistance(beforeReturn.Position, center)));
            Assert.That(afterReturn.CastingAbility, Is.EqualTo(AbilityId.None));
            Assert.That(originalEnemies[0].gameObject.activeSelf, Is.True);
            Assert.That(originalEnemies[0].Id, Is.EqualTo(originalIds[0]));

            // Once every survivor is distant, normal retirement and the next
            // camp can reuse the same bounded views with fresh participant IDs.
            ReplaceGrindingSnapshotFixture(PveRegionLayout.GetCampCenter(6), center, originalIds);
            Invoke("AdvanceGrindingCamps");
            Assert.That(run.ActiveCampIndex, Is.EqualTo(6));
            Assert.That(run.ActiveMobCount, Is.LessThanOrEqualTo(GrindingRegionRunController.MaximumActiveMobCount));
            foreach (ParticipantId original in originalIds)
            {
                Assert.That(Simulation.TryGetSnapshot(original, out _), Is.False);
            }
            Assert.That(Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Include), Is.EquivalentTo(pool));
            Assert.That(Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude).Length,
                Is.EqualTo(1 + run.ActiveMobCount));
            Assert.That(run.TotalExperience, Is.Zero);
            Assert.That(run.Kills, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FocusedCampTargets_ShowArchetypeLevelAndClassAfterEveryFocusChange()
        {
            StartFireAndFinishCountdown();
            ApproachFirstCamp(true);
            GrindingRegionRunController run = GetField<GrindingRegionRunController>(_controller, "_grindingRun");
            CombatantSnapshot[] enemies = GetSnapshots().Where(snapshot => snapshot.Team.Value == 2).ToArray();
            Assert.That(enemies, Has.Length.EqualTo(2));
            Assert.That(enemies[0].Class, Is.Not.EqualTo(enemies[1].Class));
            Text targetLabel = GetField<Text>(_hud, "_targetLabel");

            // Revisit the first identity to exercise cache invalidation both ways.
            for (int selection = 0; selection < 3; selection++)
            {
                CombatantSnapshot enemy = enemies[selection % 2];
                Assert.That(run.TryGetMob(enemy.Id, out GrindingMobDefinition mob), Is.True);
                LevelingMobArchetypeDefinition archetype = LevelingMobArchetypeCatalog.Get(mob.Archetype);
                SetField(_controller, "_focusedTarget", enemy.Id);
                Invoke("RefreshHud");

                Assert.That(_hud.FocusedFighterId, Is.EqualTo(enemy.Id.Value));
                Assert.That(targetLabel.text, Is.EqualTo(archetype.DisplayName +
                    "  •  LV " + mob.Level + "  •  " + enemy.Class.ToString().ToUpperInvariant()));
                Assert.That(targetLabel.text, Does.Not.Contain("Opponent"));
                Assert.That(Simulation.TryGetSnapshot(enemy.Id, out CombatantSnapshot unchanged), Is.True);
                Assert.That(unchanged.Class, Is.EqualTo(archetype.Class),
                    "Presenting the archetype name must not replace its Core class identity.");
            }

            yield return null;
        }

        [Test]
        public void CaptureReadiness_RequiresActualGameplayTicksRatherThanWallTime()
        {
            Assert.That(_controller.IsGrindingCaptureReady, Is.False);
            _controller.ConfigureGrindingCapture(_saveDirectory, 0);
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            SetField(_controller, "_inputSource", null);
            Assert.That(_controller.IsGrindingCaptureReady, Is.False);
            for (int tick = 0; tick < 90; tick++) { Tick(); }
            Assert.That(_controller.IsGrindingCaptureReady, Is.False,
                "Completing the countdown alone is not settled combat evidence.");
            for (int tick = 0; tick < 37; tick++) { Tick(); }
            Assert.That(_controller.IsGrindingCaptureReady, Is.False);
            Tick();
            Assert.That(_controller.IsGrindingCaptureReady, Is.True);
            _controller.ReturnHome();
            Assert.That(_controller.IsGrindingCaptureReady, Is.False);
        }

        [UnityTest]
        public IEnumerator CaptureLateralOffsets_StayInsideTheBasinAndResetToRefuge()
        {
            float[] offsets = { -7f, 7f, -99f, 99f, 0f };
            foreach (float offset in offsets)
            {
                _controller.ConfigureGrindingCapture(_saveDirectory, 0, offset);
                _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
                float expectedZ = GrindingRegionRunController.GetCampDefinition(0).Center.Z +
                    Mathf.Clamp(offset, -7f, 7f);
                Assert.That(GetPlayer().Position.Z, Is.EqualTo(expectedZ).Within(0.005f));
                Assert.That(CombatRules.PveLevelingDefault.CollisionMap.IsPositionValid(
                    GetPlayer().Position, ArenaLayout.CombatantCollisionRadius), Is.True);
                _controller.ReturnHome();
                yield return null;
            }

            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            Assert.That(GetPlayer().Position, Is.EqualTo(PveRegionLayout.Refuge));
        }

        [UnityTest]
        public IEnumerator FocusedEliteTarget_ShowsPersistentLevelAlongsideBossIdentity()
        {
            _controller.ConfigureGrindingCapture(_saveDirectory, 6);
            StartFireAndFinishCountdown();
            Tick();
            CombatantSnapshot elite = GetSnapshots().Single(snapshot => snapshot.Team.Value == 2);
            Assert.That(elite.Class, Is.EqualTo(ClassId.Boss));
            SetField(_controller, "_focusedTarget", elite.Id);
            Invoke("RefreshHud");

            Assert.That(_hud.FocusedFighterId, Is.EqualTo(elite.Id.Value));
            Assert.That(GetField<Text>(_hud, "_targetLabel").text,
                Is.EqualTo("Rift Warden  •  LV 9  •  BOSS"));
            Assert.That(Simulation.TryGetSnapshot(elite.Id, out CombatantSnapshot unchanged), Is.True);
            Assert.That(unchanged.Class, Is.EqualTo(ClassId.Boss));
            Assert.That(unchanged.Level, Is.EqualTo(9));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoritativeKill_LevelsUpFlushesOutsideTickAndPersistsAfterReentry()
        {
            const int initialExperience = 145;
            LocalGrindingProgressStore saved = new LocalGrindingProgressStore(_saveDirectory);
            saved.SaveExperience(SpecializationId.Fire, initialExperience);
            StartFireAndFinishCountdown();
            GrindingRegionRunController run = GetField<GrindingRegionRunController>(_controller, "_grindingRun");
            int expectedExperience = initialExperience + DefeatFirstCampMob();
            Assert.That(run.Kills, Is.EqualTo(1));
            Assert.That(run.ActiveMobCount, Is.EqualTo(1));
            Assert.That(_controller.GrindingExperience, Is.EqualTo(expectedExperience));
            Assert.That(_controller.GrindingLevel, Is.EqualTo(2));
            Assert.That(GetPlayer().Level, Is.EqualTo(2));
            Assert.That(GetPlayer().MaxHealth, Is.EqualTo(122f));
            Assert.That(saved.LoadExperience(SpecializationId.Fire), Is.EqualTo(initialExperience),
                "TickSession must mark progress dirty without doing file IO.");

            UpdateWithoutSimulationTick();
            Assert.That(saved.LoadExperience(SpecializationId.Fire), Is.EqualTo(expectedExperience));
            Assert.That(_controller.HasGrindingSaveError, Is.False);
            Assert.That(_hud.IsLevelProgressionVisible, Is.True);
            Assert.That(_hud.LevelProgressionLabel, Is.EqualTo("LEVEL 2"));
            Assert.That(Flow.Phase, Is.EqualTo(SessionPhase.Playing));

            _controller.ReturnHome();
            SetField(_controller, "_grindingStore", null);
            StartFireAndFinishCountdown();
            Assert.That(_controller.GrindingExperience, Is.EqualTo(expectedExperience));
            Assert.That(_controller.GrindingLevel, Is.EqualTo(2));
            Assert.That(GetPlayer().Level, Is.EqualTo(2));
            Assert.That(Simulation.CombatantCount, Is.EqualTo(1));
            Assert.That(GetPlayer().Position, Is.EqualTo(PveRegionLayout.Refuge));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyAfterDiskRepair_ForcesPendingSaveBeforeRetryDeadline()
        {
            LocalGrindingProgressStore saved = new LocalGrindingProgressStore(_saveDirectory);
            saved.SaveExperience(SpecializationId.Fire, 0);
            string path = Path.Combine(_saveDirectory, "pve-character-v1-2.save");
            byte[] original = File.ReadAllBytes(path);
            StartFireAndFinishCountdown();
            int earned = DefeatFirstCampMob();

            File.WriteAllText(path, "temporarily unreadable save");
            LogAssert.Expect(LogType.Warning,
                "PvE saving failed. Play paused; the previous save was preserved.");
            UpdateWithoutSimulationTick();
            Assert.That(_controller.HasGrindingSaveError, Is.True);
            Assert.That(GetField<bool>(_controller, "_grindingDirty"), Is.True);
            Assert.That(File.ReadAllText(path), Is.EqualTo("temporarily unreadable save"));

            File.WriteAllBytes(path, original);
            SetField(_controller, "_grindingSaveRetryAt", Time.unscaledTime + 10f);
            UpdateWithoutSimulationTick();
            Assert.That(saved.LoadExperience(SpecializationId.Fire), Is.Zero,
                "Ordinary Update must still respect the retry throttle.");
            Assert.That(_controller.HasGrindingSaveError, Is.True);

            Object.Destroy(_controller);
            yield return null;
            _controller = null;

            Assert.That(saved.LoadExperience(SpecializationId.Fire), Is.EqualTo(earned),
                "OnDestroy must attempt the terminal flush even before the next Update retry.");
        }

        [UnityTest]
        public IEnumerator AuthoritativePlayerDeath_RecoversAtRefugeWithoutLosingSavedExperience()
        {
            const int initialExperience = 420;
            LocalGrindingProgressStore saved = new LocalGrindingProgressStore(_saveDirectory);
            saved.SaveExperience(SpecializationId.Fire, initialExperience);
            StartFireAndFinishCountdown();
            ApproachFirstCamp(false);
            CombatSimulation beforeRecovery = Simulation;
            foreach (CombatantSnapshot enemy in GetSnapshots().Where(snapshot => snapshot.Team.Value == 2))
            {
                Assert.That(Simulation.TrySetRunCombatModifiers(enemy.Id, 4000, 1000), Is.True);
            }

            for (int tick = 0; tick < 1800 && GetPlayer().IsAlive; tick++)
            {
                Tick();
            }

            Assert.That(GetPlayer().IsAlive, Is.False, "Unanswered authoritative NPC attacks must kill the player.");
            Assert.That(GetField<bool>(_controller, "_grindingRecoveryPending"), Is.True);
            Assert.That(Flow.Phase, Is.EqualTo(SessionPhase.Playing));
            UpdateWithoutSimulationTick();

            Assert.That(Simulation, Is.Not.SameAs(beforeRecovery));
            Assert.That(Simulation.CombatantCount, Is.EqualTo(1));
            Assert.That(GetPlayer().IsAlive, Is.True);
            Assert.That(GetPlayer().Health, Is.EqualTo(GetPlayer().MaxHealth));
            Assert.That(GetPlayer().Position, Is.EqualTo(PveRegionLayout.Refuge));
            Assert.That(_controller.GrindingExperience, Is.EqualTo(initialExperience));
            Assert.That(saved.LoadExperience(SpecializationId.Fire), Is.EqualTo(initialExperience));
            Assert.That(_controller.GrindingLevel, Is.EqualTo(3));
            Assert.That(Flow.Phase, Is.EqualTo(SessionPhase.Playing));
            Assert.That(_hud.Screen, Is.EqualTo(PrototypeHudScreen.Gameplay));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyRegionAndElapsedTime_DoNotCompleteTheGrindingMode()
        {
            StartFireAndFinishCountdown();
            for (int tick = 0; tick <= 30 * 1000; tick++)
            {
                Flow.Tick();
            }

            Tick();
            Assert.That(Flow.ViewState.ElapsedTicks, Is.GreaterThan(30 * 900));
            Assert.That(Flow.Phase, Is.EqualTo(SessionPhase.Playing));
            Assert.That(_hud.Screen, Is.EqualTo(PrototypeHudScreen.Gameplay));
            Assert.That(Simulation.CombatantCount, Is.EqualTo(1));
            Assert.That(_controller.GrindingExperience, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CorruptSave_RefusesToStartWithoutResettingTheFile()
        {
            Directory.CreateDirectory(_saveDirectory);
            string path = Path.Combine(_saveDirectory, "pve-character-v1-2.save");
            const string corrupted = "pve-character-v99\n2\n1000000\nunknown";
            File.WriteAllText(path, corrupted);
            LogAssert.Expect(LogType.Warning, "PvE save could not be loaded; existing data was not reset.");

            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);

            Assert.That(_controller.HasGrindingSaveError, Is.True);
            Assert.That(Flow.Phase, Is.EqualTo(SessionPhase.Home));
            Assert.That(Simulation, Is.Null);
            Assert.That(_hud.Screen, Is.EqualTo(PrototypeHudScreen.Results));
            Assert.That(File.ReadAllText(path), Is.EqualTo(corrupted));
            yield return null;
        }

        private SessionFlowController Flow => GetField<SessionFlowController>(_controller, "_flow");
        private CombatSimulation Simulation => GetField<CombatSimulation>(_controller, "_simulation");

        private static IEnumerator WaitForStreamReady(WorldZoneStreamController streamer)
        {
            // SetFocus may leave Ready until the next streamer Update.
            yield return null;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (streamer.Status != WorldZoneStreamStatus.Ready && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(streamer.Status, Is.Not.EqualTo(WorldZoneStreamStatus.Faulted), streamer.LastError);
                yield return null;
            }
            Assert.That(streamer.Status, Is.EqualTo(WorldZoneStreamStatus.Ready), streamer.LastError);
        }

        private void StartFireAndFinishCountdown()
        {
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            // Deterministic commands below replace device input only in this fixture.
            SetField(_controller, "_inputSource", null);
            _commandSequence = 0;
            for (int tick = 0; tick < 90; tick++)
            {
                Tick();
            }

            Assert.That(Flow.Phase, Is.EqualTo(SessionPhase.Playing));
        }

        private void ApproachFirstCamp(bool holdNpcBrains)
        {
            Float3 approach = PveRegionLayout.GetCampCenter(0) + new Float3(-10f, 0f, 0f);
            for (int tick = 0; tick < 240 && Float3.PlanarDistance(GetPlayer().Position, approach) > 0.25f; tick++)
            {
                Float3 direction = (approach - GetPlayer().Position).PlanarNormalized;
                Assert.That(Simulation.SubmitCommand(CombatCommand.Move(
                    Player, ++_commandSequence, direction, direction)), Is.True);
                if (holdNpcBrains)
                {
                    HoldNpcBrains();
                }
                Tick();
            }

            Assert.That(Float3.PlanarDistance(GetPlayer().Position, approach), Is.LessThanOrEqualTo(0.25f));
            Assert.That(Simulation.SubmitCommand(CombatCommand.Move(
                Player, ++_commandSequence, default, new Float3(1f, 0f, 0f))), Is.True);
            if (holdNpcBrains)
            {
                HoldNpcBrains();
            }
            Tick();
            GrindingRegionRunController run = GetField<GrindingRegionRunController>(_controller, "_grindingRun");
            Assert.That(run.ActiveCampIndex, Is.Zero);
            Assert.That(run.ActiveMobCount, Is.EqualTo(2));
            Assert.That(Simulation.CombatantCount, Is.EqualTo(3));
        }

        private int DefeatFirstCampMob()
        {
            Assert.That(Simulation.TrySetRunCombatModifiers(Player, 4000, 1000), Is.True);
            ApproachFirstCamp(true);
            GrindingRegionRunController run = GetField<GrindingRegionRunController>(_controller, "_grindingRun");
            CombatantSnapshot target = GetSnapshots().First(snapshot => snapshot.Team.Value == 2);
            Assert.That(run.TryGetMob(target.Id, out GrindingMobDefinition mob), Is.True);
            Float3 facing = (target.Position - GetPlayer().Position).PlanarNormalized;
            CombatCommand cast = CombatCommand.Cast(
                Player, ++_commandSequence, default, facing, AbilityId.Fireball,
                target.Id, target.Position, facing);
            Assert.That(Simulation.CanStartAbility(cast, out string reason), Is.True, reason);
            Assert.That(Simulation.SubmitCommand(cast), Is.True);
            CombatantSnapshot defeated = target;
            for (int tick = 0; tick < 120 && defeated.IsAlive; tick++)
            {
                Tick();
                Assert.That(Simulation.TryGetSnapshot(target.Id, out defeated), Is.True);
            }

            Assert.That(defeated.IsAlive, Is.False,
                "A real Core-resolved Fireball, including its cast time, must defeat the mob.");
            return mob.ExperienceReward;
        }

        private void HoldNpcBrains()
        {
            DeterministicLevelingMobBrain[] brains = GetField<DeterministicLevelingMobBrain[]>(
                _controller, "_levelingMobBrains");
            Array.Clear(brains, 0, brains.Length);
            foreach (CombatantSnapshot enemy in GetSnapshots().Where(snapshot => snapshot.Team.Value == 2))
            {
                Simulation.SubmitCommand(CombatCommand.Move(enemy.Id,
                    10000u + _commandSequence, default, enemy.Facing));
            }
        }

        private CombatantSnapshot[] GetSnapshots()
        {
            CombatantSnapshot[] snapshots = new CombatantSnapshot[Simulation.CombatantCount];
            Simulation.CopySnapshots(snapshots);
            return snapshots;
        }

        private void ReplaceGrindingSnapshotFixture(Float3 playerPosition, Float3 firstMobPosition, ParticipantId[] ids)
        {
            GrindingRegionRunController run = GetField<GrindingRegionRunController>(_controller, "_grindingRun");
            CombatSimulation replacement = new CombatSimulation(CombatRules.PveLevelingDefault);
            Assert.That(replacement.AddCombatant(Player, new TeamId(1), ClassId.Mage,
                SpecializationId.Fire, playerPosition), Is.True);
            for (int index = 0; index < ids.Length; index++)
            {
                Assert.That(run.TryGetMob(ids[index], out GrindingMobDefinition mob), Is.True);
                LevelingMobArchetypeDefinition archetype = LevelingMobArchetypeCatalog.Get(mob.Archetype);
                Float3 position = index == 0 ? firstMobPosition : mob.Position;
                Assert.That(replacement.Rules.CollisionMap.IsPositionValid(position,
                    ArenaLayout.CombatantCollisionRadius), Is.True);
                Assert.That(replacement.AddCombatant(mob.Participant, new TeamId(2), archetype.Class,
                    archetype.Specialization, position, mob.BaseMaximumHealth, false), Is.True);
            }
            Assert.That(replacement.TryGetSnapshot(Player, out CombatantSnapshot player), Is.True);
            Assert.That(player.Position, Is.EqualTo(playerPosition));
            for (int tick = 0; tick < Simulation.CurrentTick; tick++) { replacement.Tick(); }
            SetField(_controller, "_simulation", replacement);
        }

        private CombatantSnapshot GetPlayer()
        {
            Assert.That(Simulation.TryGetSnapshot(Player, out CombatantSnapshot player), Is.True);
            return player;
        }

        private void Tick()
        {
            Invoke("TickSession");
        }

        private void UpdateWithoutSimulationTick()
        {
            SetField(_controller, "_accumulator", -1f);
            Invoke("Update");
        }

        private void Invoke(string method)
        {
            MethodInfo found = typeof(PrototypeGameController).GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(found, Is.Not.Null, method);
            found.Invoke(_controller, null);
        }

        private static T GetField<T>(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(owner);
        }

        private static void SetField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(owner, value);
        }
    }
}
