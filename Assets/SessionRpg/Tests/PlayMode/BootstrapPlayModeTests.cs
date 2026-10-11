using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Networking;
using SessionRpg.Unity.Camera;
using SessionRpg.Unity.Arenas;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.Performance;
using SessionRpg.Unity.UI;
using SessionRpg.Unity.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class BootstrapPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_HasOneCompositionAndStartsAtHome()
        {
            Assert.That(Object.FindObjectsByType<PrototypeGameController>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<MobileQualityController>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Camera>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioListener>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<NetworkSessionController>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<CombatEffectsPresenter>(), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<CombatAudioPresenter>(), Has.Length.EqualTo(1));

            NetworkManager networkManager = Object.FindAnyObjectByType<NetworkManager>();
            NetworkSessionController networkSession =
                Object.FindAnyObjectByType<NetworkSessionController>();
            Assert.That(networkManager.enabled, Is.False);
            Assert.That(NetworkManager.Singleton, Is.Null);
            Assert.That(networkSession.State, Is.EqualTo(NetworkSessionState.Offline));

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.Screen, Is.EqualTo(PrototypeHudScreen.Home));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_HudUsesTrackedProductFont()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Text[] labels = hud.GetComponentsInChildren<Text>(true);

            Assert.That(labels, Is.Not.Empty);
            for (int index = 0; index < labels.Length; index++)
            {
                Assert.That(labels[index].font, Is.Not.Null, labels[index].name);
                Assert.That(
                    labels[index].font.name,
                    Does.Contain("Alegreya"),
                    labels[index].name + " fell back to Unity's prototype font.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Home_PrimaryActionExplainsModeAndTrainingRecommendation()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);

            hud.SetTrainingRecommendation(true);
            hud.SetSelectedMode(PrototypeMode.Training);
            Assert.That(
                hud.StartActionLabel,
                Is.EqualTo("START RECOMMENDED TRAINING"));
            Assert.That(hud.TrainingModeLabel, Does.Contain("RECOMMENDED"));
            Assert.That(GameObject.Find("SkipTraining"), Is.Not.Null);

            hud.SetSelectedMode(PrototypeMode.Arena2V2);
            Assert.That(hud.StartActionLabel, Is.EqualTo("PLAY 2 v 2 ARENA"));
            hud.SetSelectedMode(PrototypeMode.HeroSiege);
            Assert.That(hud.StartActionLabel, Is.EqualTo("START SOLO-LANE SIEGE"));
            hud.SetSelectedMode(PrototypeMode.Dungeon);
            Assert.That(hud.StartActionLabel, Is.EqualTo("ENTER DUNGEON"));

            hud.SetTrainingRecommendation(false);
            hud.SetSelectedMode(PrototypeMode.Training);
            Assert.That(hud.StartActionLabel, Is.EqualTo("START TRAINING"));
            Assert.That(hud.TrainingModeLabel, Does.Not.Contain("RECOMMENDED"));
            hud.SetResult(
                "TRAINING COMPLETE",
                "TRAINING  •  FROST  •  2:14",
                "CONTINUE TO ARENA");
            Assert.That(hud.ReplayActionLabel, Is.EqualTo("CONTINUE TO ARENA"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeroSiegeHud_AuthorsVariantsAndExplicitSupplyActions()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud.HasAuthoredHeroSiegeContract, Is.True);
            Assert.That(hud.SelectedSiegeVariant, Is.EqualTo(SiegeVariantId.SoloLane));

            hud.SetSelectedMode(PrototypeMode.HeroSiege);
            Assert.That(hud.IsSiegeVariantSelectionVisible, Is.True);
            Button siegeMode = hud.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "HeroSiege");
            Assert.That(
                siegeMode.transform.Find("Label").GetComponent<Text>().text,
                Does.Contain("12 TIMED WAVES"));

            SiegeVariantId selectedVariant = SiegeVariantId.None;
            hud.SiegeVariantSelected += variant => selectedVariant = variant;
            Button fourLanes = hud.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SiegeFourLanes");
            fourLanes.onClick.Invoke();
            Assert.That(selectedVariant, Is.EqualTo(SiegeVariantId.FourLanes));
            Assert.That(hud.SelectedSiegeVariant, Is.EqualTo(SiegeVariantId.FourLanes));
            Assert.That(hud.StartActionLabel, Is.EqualTo("START FOUR-LANE SIEGE"));

            SiegePotionId purchased = SiegePotionId.None;
            SiegePotionId used = SiegePotionId.None;
            hud.SiegePotionPurchaseRequested += potion => purchased = potion;
            hud.SiegePotionUseRequested += potion => used = potion;
            hud.ShowGameplay();
            hud.SetSiegeSupplyState(
                true,
                135,
                2,
                1,
                45,
                35,
                true,
                true,
                false,
                true);

            Assert.That(hud.IsSiegeVariantSelectionVisible, Is.False);
            Assert.That(hud.IsSiegeSupplyVisible, Is.True);
            Assert.That(hud.IsSiegeShopOpen, Is.False);
            Assert.That(hud.SiegeGoldLabel, Is.EqualTo("G 135"));
            Assert.That(hud.SiegeHealthPotionLabel, Is.EqualTo("HP ×2"));
            Assert.That(hud.SiegeManaPotionLabel, Is.EqualTo("MP ×1"));

            Button[] buttons = hud.GetComponentsInChildren<Button>(true);
            Button openShop = buttons.Single(button => button.name == "OpenSupplyShop");
            Button buyHealth = buttons.Single(button => button.name == "BuyHealthPotion");
            Button useHealth = buttons.Single(button => button.name == "UseHealthPotion");
            Button buyMana = buttons.Single(button => button.name == "BuyManaPotion");
            Button useMana = buttons.Single(button => button.name == "UseManaPotion");
            RectTransform supplies = (RectTransform)hud.transform.Find(
                "SafeArea/Gameplay/SiegeSupplies");
            RectTransform purchaseDrawer = (RectTransform)supplies.Find(
                "PurchaseDrawer");
            Assert.That(supplies.rect.size, Is.EqualTo(new Vector2(366f, 72f)));
            Assert.That(
                supplies.anchoredPosition,
                Is.EqualTo(new Vector2(-430f, 24f)));
            Assert.That(
                ((RectTransform)useHealth.transform).rect.size,
                Is.EqualTo(new Vector2(78f, 52f)));
            Assert.That(
                ((RectTransform)useMana.transform).rect.size,
                Is.EqualTo(new Vector2(78f, 52f)));
            Assert.That(
                ((RectTransform)openShop.transform).rect.size,
                Is.EqualTo(new Vector2(72f, 52f)));
            Assert.That(
                purchaseDrawer.rect.size,
                Is.EqualTo(new Vector2(372f, 132f)));
            Assert.That(
                ((RectTransform)buyHealth.transform).rect.size,
                Is.EqualTo(new Vector2(166f, 56f)));
            Assert.That(
                ((RectTransform)buyMana.transform).rect.size,
                Is.EqualTo(new Vector2(166f, 56f)));
            openShop.onClick.Invoke();
            Assert.That(hud.IsSiegeShopOpen, Is.True);
            Assert.That(buyHealth.interactable, Is.True);
            Assert.That(useHealth.interactable, Is.True);
            Assert.That(buyMana.interactable, Is.False);
            Assert.That(useMana.interactable, Is.True);

            buyHealth.onClick.Invoke();
            useMana.onClick.Invoke();
            Assert.That(purchased, Is.EqualTo(SiegePotionId.Health));
            Assert.That(used, Is.EqualTo(SiegePotionId.Mana));

            hud.SetSelectedMode(PrototypeMode.Arena2V2);
            Assert.That(hud.IsSiegeSupplyVisible, Is.False);
            Assert.That(hud.IsSiegeShopOpen, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeroSiege_FirstSpawnReceivesSnapshotAuthoritativeFocus()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSiegeSession(
                SiegeVariantId.SoloLane,
                ClassId.Mage,
                SpecializationId.Frost);

            HeroSiegeRunController run =
                GetPrivateField<HeroSiegeRunController>(controller, "_siegeRun");
            SiegeAdvanceObservation advance = run.Advance(
                HeroSiegeRunCatalog.InitialPreparationTicks);
            Assert.That(advance.WaveStarted, Is.True);
            Assert.That(run.TryDequeueSpawn(out SiegeSpawnDefinition spawn), Is.True);

            System.Reflection.MethodInfo addHostile =
                typeof(PrototypeGameController).GetMethod(
                    "AddSiegeHostile",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            System.Reflection.MethodInfo presentSnapshots =
                typeof(PrototypeGameController).GetMethod(
                    "PresentSnapshots",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.That(addHostile, Is.Not.Null);
            Assert.That(presentSnapshots, Is.Not.Null);
            addHostile.Invoke(controller, new object[] { 1, spawn });
            presentSnapshots.Invoke(controller, null);

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            SoftTargetSelectionService targeting =
                Object.FindAnyObjectByType<SoftTargetSelectionService>();
            Assert.That(hud.FocusedFighterId, Is.EqualTo(spawn.Participant.Value));
            Assert.That(
                targeting.CurrentTarget,
                Is.Null,
                "Siege fallback focus must not depend on presentation colliders.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Home_KitViewerTracksModeOnlySiegeActions()
        {
            KitViewerPanel viewer = Object.FindAnyObjectByType<KitViewerPanel>();
            Assert.That(viewer, Is.Not.Null);

            viewer.Bind(PlayableModeId.Arena, SpecializationId.Mutilate);
            Assert.That(viewer.VisibleGuide, Does.Not.Contain("BLADE TEMPEST"));

            viewer.Bind(PlayableModeId.HeroSiege, SpecializationId.Mutilate);
            Assert.That(viewer.VisibleGuide, Does.Contain("BLADE TEMPEST"));
            Assert.That(
                viewer.VisibleGuide,
                Does.Contain("mode-only crowd-clearing").IgnoreCase);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrainingVictory_PrimaryResultActionContinuesIntoArena()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            SessionFlowController flow = GetPrivateField<SessionFlowController>(
                controller,
                "_flow");
            SetPrivateField(controller, "_selectedMode", GameModeId.Leveling);
            flow.Start(GameModeId.Leveling, 0);
            flow.Complete(true, "Arena ready");
            hud.SetSelectedMode(PrototypeMode.Training);
            hud.ShowResults(
                "TRAINING COMPLETE",
                "TRAINING  •  FROST  •  1:30",
                "CONTINUE TO ARENA");

            Button replay = GameObject.Find("Replay").GetComponent<Button>();
            replay.onClick.Invoke();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(hud.Screen, Is.EqualTo(PrototypeHudScreen.Gameplay));
            Assert.That(hud.SelectedMode, Is.EqualTo(PrototypeMode.Arena2V2));
            Assert.That(
                FindActiveArenaPresentation(ArenaContentMode.Arena),
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator OfflineNetworking_DoesNotPersistAcrossBootstrapReload()
        {
            NetworkManager firstManager = Object.FindAnyObjectByType<NetworkManager>();
            Assert.That(firstManager, Is.Not.Null);
            Assert.That(firstManager.enabled, Is.False);
            Assert.That(NetworkManager.Singleton, Is.Null);

            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            NetworkManager secondManager = Object.FindAnyObjectByType<NetworkManager>();
            Assert.That(Object.FindObjectsByType<NetworkManager>(), Has.Length.EqualTo(1));
            Assert.That(secondManager, Is.Not.Null);
            Assert.That(secondManager, Is.Not.SameAs(firstManager));
            Assert.That(firstManager == null, Is.True);
            Assert.That(secondManager.enabled, Is.False);
            Assert.That(NetworkManager.Singleton, Is.Null);
        }

        [UnityTest]
        public IEnumerator Arena_ActivatesFourCombatantsAndGameplayHud()
        {
            PrototypeGameController controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud.Screen, Is.EqualTo(PrototypeHudScreen.Gameplay));
            Assert.That(
                Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude),
                Has.Length.EqualTo(4));
            Assert.That(
                Object.FindObjectsByType<CombatantAnimationPresenter>(FindObjectsInactive.Exclude),
                Has.Length.EqualTo(4));
            ArenaPresentation presentation = FindActiveArenaPresentation(
                ArenaContentMode.Arena);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.ContentId.Value, Is.EqualTo("rift_crucible"));
            Assert.That(presentation.Mode, Is.EqualTo(ArenaContentMode.Arena));
            Assert.That(
                presentation.HasCapability(
                    ArenaPresentationCapabilities.AuthoritativeCover),
                Is.True);
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTest]
        public IEnumerator Arena_CoverGeometryMatchesAuthoritativeCollisionMap()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForFixedUpdate();
            yield return null;

            ArenaCollisionMap collisionMap = ArenaCollisionMap.ArenaDefault;
            string[] names = { "ArenaPillar_South", "ArenaPillar_North" };
            Assert.That(collisionMap.ObstacleCount, Is.EqualTo(names.Length));
            for (int index = 0; index < names.Length; index++)
            {
                ArenaCircleObstacle obstacle = collisionMap.GetObstacle(index);
                GameObject pillar = GameObject.Find(names[index]);
                Assert.That(pillar, Is.Not.Null, "Missing authored cover " + names[index] + ".");
                Assert.That(pillar.layer, Is.EqualTo(8));
                CapsuleCollider collider = pillar.GetComponent<CapsuleCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.direction, Is.EqualTo(1));
                Assert.That(collider.radius, Is.EqualTo(obstacle.Radius).Within(0.001f));
                Assert.That(collider.height, Is.EqualTo(ArenaLayout.PillarColliderHeight).Within(0.001f));
                Assert.That(collider.center.y, Is.EqualTo(ArenaLayout.PillarColliderHeight * 0.5f).Within(0.001f));
                Assert.That(pillar.transform.position.x, Is.EqualTo(obstacle.Center.X).Within(0.001f));
                Assert.That(pillar.transform.position.z, Is.EqualTo(obstacle.Center.Z).Within(0.001f));
                Assert.That(pillar.transform.localScale, Is.EqualTo(Vector3.one));
            }

            ArenaCircleObstacle south = collisionMap.GetObstacle(0);
            Assert.That(
                Physics.Linecast(
                    new Vector3(-5f, 1.45f, south.Center.Z),
                    new Vector3(5f, 1.45f, south.Center.Z),
                    1 << 8,
                    QueryTriggerInteraction.Ignore),
                Is.True,
                "The rendered south pillar must block the same lane as core LOS.");
        }

        [UnityTest]
        public IEnumerator ExplicitTarget_RemainsFocusedWhenCoverBlocksLineOfSight()
        {
            GameObject origin = new GameObject("TargetRetentionOrigin");
            origin.transform.position = new Vector3(-5f, 1f, 0f);
            GameObject cameraObject = new GameObject(
                "TargetRetentionCamera",
                typeof(Camera));
            cameraObject.transform.forward = Vector3.right;
            Camera viewCamera = cameraObject.GetComponent<Camera>();

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "TargetRetentionCandidate";
            target.layer = 9;
            target.transform.position = new Vector3(5f, 1f, 0f);
            SoftTargetCandidate candidate = target.AddComponent<SoftTargetCandidate>();
            candidate.Configure(99, 2, target.transform);

            GameObject decoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            decoy.name = "TargetRetentionDecoy";
            decoy.layer = 9;
            decoy.transform.position = new Vector3(3f, 1f, 3f);
            SoftTargetCandidate decoyCandidate =
                decoy.AddComponent<SoftTargetCandidate>();
            decoyCandidate.Configure(100, 2, decoy.transform);

            GameObject selectionObject = new GameObject(
                "TargetRetentionService",
                typeof(SoftTargetSelectionService));
            SoftTargetSelectionService selection =
                selectionObject.GetComponent<SoftTargetSelectionService>();
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "TargetRetentionCover";
            cover.layer = 8;
            cover.transform.position = Vector3.up;
            cover.transform.localScale = new Vector3(1f, 3f, 2f);
            Physics.SyncTransforms();
            selection.SetContext(origin.transform, viewCamera, 1);
            Assert.That(
                selection.CurrentTarget,
                Is.Null,
                "Visible enemies are candidates, not implicit player targets.");
            Assert.That(
                selection.TrySetExplicit(candidate),
                Is.True,
                "The compact HUD must be able to select a known target through cover.");
            Assert.That(selection.CurrentTarget, Is.SameAs(candidate));
            Assert.That(selection.CurrentTargetWasExplicitlySelected, Is.True);
            selection.RefreshNow();

            Assert.That(
                selection.CurrentTarget,
                Is.SameAs(candidate),
                "Temporary occlusion must not replace a deliberate player target.");
            Assert.That(selection.CurrentTargetWasExplicitlySelected, Is.True);

            target.transform.position = new Vector3(50f, 1f, 0f);
            Physics.SyncTransforms();
            selection.RefreshNow();
            Assert.That(
                selection.CurrentTarget,
                Is.SameAs(candidate),
                "Walking out of targeting range must preserve a deliberate player target.");
            Assert.That(selection.CurrentTargetWasExplicitlySelected, Is.True);

            target.transform.position = new Vector3(-10f, 1f, 0f);
            Physics.SyncTransforms();
            selection.RefreshNow();
            Assert.That(
                selection.CurrentTarget,
                Is.SameAs(candidate),
                "Leaving the camera retention angle must preserve a deliberate player target.");
            Assert.That(selection.CurrentTargetWasExplicitlySelected, Is.True);

            GameObject rejectedTarget = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rejectedTarget.name = "SpatiallyRejectedTarget";
            rejectedTarget.layer = 9;
            rejectedTarget.transform.position = new Vector3(80f, 1f, 0f);
            SoftTargetCandidate rejectedCandidate =
                rejectedTarget.AddComponent<SoftTargetCandidate>();
            rejectedCandidate.Configure(101, 2, rejectedTarget.transform);
            Physics.SyncTransforms();
            Assert.That(selection.TrySetExplicit(rejectedCandidate), Is.False);
            Assert.That(
                selection.CurrentTarget,
                Is.Null,
                "Rejecting a newly tapped enemy must clear the old world-pick marker.");

            target.transform.position = new Vector3(5f, 1f, 0f);
            Physics.SyncTransforms();
            Assert.That(selection.TrySetExplicit(candidate), Is.True);

            candidate.SetTargetable(false);
            selection.RefreshNow();
            Assert.That(
                selection.CurrentTarget,
                Is.Null,
                "Death or hidden-state invalidation must clear focus, never choose a decoy.");

            Object.Destroy(selectionObject);
            Object.Destroy(cover);
            Object.Destroy(rejectedTarget);
            Object.Destroy(decoy);
            Object.Destroy(target);
            Object.Destroy(cameraObject);
            Object.Destroy(origin);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rogue_HudShowsFiveTargetBoundComboPointPips()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Rogue,
                SpecializationId.Mutilate);
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud.ComboPointsVisible, Is.True);
            hud.SetComboPoints(3, 5, true, true);
            Assert.That(hud.VisibleComboPoints, Is.EqualTo(3));
            Assert.That(hud.ComboTargetMatches, Is.True);

            RectTransform comboRoot = Object
                .FindObjectsByType<RectTransform>(FindObjectsInactive.Include)
                .Single(value => value.name == "PlayerComboPoints");
            Image[] pips = Enumerable
                .Range(0, 5)
                .Select(index => comboRoot.Find("Pip_" + index).GetComponent<Image>())
                .ToArray();
            Assert.That(pips.Take(3).All(value => value.color == pips[0].color), Is.True);
            Assert.That(pips.Skip(3).All(value => value.color == pips[4].color), Is.True);
            Assert.That(pips[0].color, Is.Not.EqualTo(pips[4].color));
            Assert.That(ReadChildText(comboRoot, "Label"), Is.EqualTo("COMBO • TARGET"));

            hud.SetComboPoints(3, 5, false, true);
            Assert.That(hud.ComboTargetMatches, Is.False);
            Assert.That(ReadChildText(comboRoot, "Label"), Is.EqualTo("COMBO • SWITCH"));

            hud.SetComboPoints(0, 5, false, false);
            Assert.That(hud.ComboPointsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Boss_ActivatesTrioAndWardenComposition()
        {
            PrototypeGameController controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Boss, ClassId.Rogue);
            yield return null;

            CombatantView[] views = Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
            Assert.That(views, Has.Length.EqualTo(4));
            Assert.That(System.Array.Exists(views, view => view.Class == ClassId.Boss), Is.True);
            Assert.That(
                FindActiveArenaPresentation(ArenaContentMode.Boss),
                Is.Not.Null);
            AssertFlatCylinderCollider(GameObject.Find("RaidFloor"));
            AssertFlatCylinderCollider(GameObject.Find("Lava"));
            StrategyFollowCamera cameraRig =
                Object.FindAnyObjectByType<StrategyFollowCamera>();
            Camera camera = cameraRig.GetComponent<Camera>();
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, StrategyFollowCamera.BossYaw),
                Is.EqualTo(0f).Within(0.01f));
            Assert.That(
                cameraRig.Distance,
                Is.EqualTo(StrategyFollowCamera.BossDistance).Within(0.01f));
            Assert.That(
                cameraRig.ForwardFocusOffset,
                Is.EqualTo(StrategyFollowCamera.BossForwardFocusOffset).Within(0.01f));
            Assert.That(
                cameraRig.RightFocusOffset,
                Is.EqualTo(StrategyFollowCamera.BossRightFocusOffset).Within(0.01f));
            Assert.That(
                camera.fieldOfView,
                Is.EqualTo(StrategyFollowCamera.BossFieldOfView).Within(0.01f));
            CombatantView boss = views.Single(view => view.Class == ClassId.Boss);
            float bossViewportHeight = camera.WorldToViewportPoint(boss.AimPoint.position).y;
            float partyViewportHeight = views
                .Where(view => view.Team.Value == 1)
                .Average(view => camera.WorldToViewportPoint(view.AimPoint.position).y);
            Assert.That(
                bossViewportHeight,
                Is.GreaterThan(partyViewportHeight),
                "The fixed boss heading must place the objective above the party.");
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTest]
        public IEnumerator Boss_UsesFirstPartyEmberVaultEnvironmentWithinMobileBudget()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Boss, ClassId.Rogue);
            yield return null;

            ArenaPresentation bossPresentation = FindActiveArenaPresentation(
                ArenaContentMode.Boss);
            Assert.That(bossPresentation, Is.Not.Null);
            Assert.That(bossPresentation.VisualRoot, Is.Not.Null);

            Transform bossFloor = bossPresentation.VisualRoot.Find(
                "RaidFloorVisual");
            Assert.That(
                bossFloor,
                Is.Not.Null,
                "Ember Vault must reuse the authored first-party arena floor.");
            Assert.That(bossFloor.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(bossFloor.localRotation, Is.EqualTo(Quaternion.identity));
            float expectedFloorScale = 13f / ArenaLayout.SafeRadius;
            Assert.That(
                bossFloor.localScale.x,
                Is.EqualTo(expectedFloorScale).Within(0.001f));
            Assert.That(
                bossFloor.localScale.y,
                Is.EqualTo(expectedFloorScale).Within(0.001f));
            Assert.That(
                bossFloor.localScale.z,
                Is.EqualTo(expectedFloorScale).Within(0.001f));

            MeshFilter[] bossFloorFilters =
                bossFloor.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] bossFloorRenderers =
                bossFloor.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(bossFloorFilters, Has.Length.EqualTo(1));
            Assert.That(bossFloorRenderers, Has.Length.EqualTo(1));
            Assert.That(bossFloorFilters[0].sharedMesh, Is.Not.Null);
            Assert.That(bossFloorFilters[0].sharedMesh.subMeshCount, Is.EqualTo(3));
            Assert.That(
                bossFloorRenderers[0].sharedMaterials,
                Has.Length.EqualTo(3));
            Assert.That(
                bossFloorRenderers[0].sharedMaterials[0].name,
                Does.Contain("ArenaFloor_Stone"));
            Assert.That(
                bossFloorRenderers[0].sharedMaterials[1].name,
                Does.Contain("ArenaFloor_Trim"));
            Assert.That(
                bossFloorRenderers[0].sharedMaterials[2].name,
                Does.Contain("ArenaFloor_Grout"));

            Transform bossFissures = bossPresentation.VisualRoot.Find(
                "BossLavaFissures");
            Assert.That(bossFissures, Is.Not.Null);
            MeshFilter bossFissureFilter = bossFissures.GetComponent<MeshFilter>();
            MeshRenderer bossFissureRenderer =
                bossFissures.GetComponent<MeshRenderer>();
            Assert.That(
                bossFissureFilter.sharedMesh.name,
                Is.EqualTo("BossLavaFissures"));
            Assert.That(
                CountTriangles(new[] { bossFissureFilter }),
                Is.EqualTo(128));
            Assert.That(
                bossFissureRenderer.sharedMaterial.name,
                Does.Contain("LavaSeam"));
            Assert.That(
                bossFissureRenderer.shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(bossFissureRenderer.receiveShadows, Is.False);
            Assert.That(
                bossFissures.GetComponentsInChildren<Collider>(true),
                Is.Empty);

            ArenaPresentation arenaPresentation = Object
                .FindObjectsByType<ArenaPresentation>(FindObjectsInactive.Include)
                .Single(value => value.Mode == ArenaContentMode.Arena);
            MeshFilter arenaFloorFilter = arenaPresentation.VisualRoot
                .Find("ArenaFloorVisual")
                .GetComponentsInChildren<MeshFilter>(true)
                .Single();
            Assert.That(
                bossFloorFilters[0].sharedMesh,
                Is.SameAs(arenaFloorFilter.sharedMesh),
                "Arena and Ember Vault must share the tracked ArenaFloorVisual source mesh.");

            string[] retiredFilledFloorNames =
            {
                "BossDais",
                "RecoveryLane",
            };
            for (int index = 0; index < retiredFilledFloorNames.Length; index++)
            {
                Transform inlayRoot = bossPresentation.VisualRoot.Find(
                    retiredFilledFloorNames[index]);
                Assert.That(inlayRoot, Is.Not.Null, retiredFilledFloorNames[index]);
                Assert.That(
                    inlayRoot.GetComponent<Renderer>(),
                    Is.Null,
                    retiredFilledFloorNames[index] +
                    " may remain as an empty inlay root but may not retain its filled renderer.");
                Assert.That(
                    inlayRoot.GetComponent<MeshFilter>(),
                    Is.Null,
                    retiredFilledFloorNames[index] +
                    " may not retain its old primitive mesh.");
                Assert.That(
                    inlayRoot.GetComponentsInChildren<LineRenderer>(true),
                    Is.Not.Empty,
                    retiredFilledFloorNames[index] +
                    " must retain its readable rune/inlay child.");
            }

            Transform perimeter = bossPresentation.VisualRoot.Find(
                "RaidPerimeterDressing");
            Assert.That(perimeter, Is.Not.Null);
            Assert.That(
                perimeter.GetComponentsInChildren<Component>(true).Any(value =>
                    value != null &&
                    (value.name.Contains("KayKit") ||
                     value.GetType().Name.Contains("KayKit"))),
                Is.False,
                "Ember Vault may not retain KayKit objects, renderers, or components.");
            Assert.That(
                perimeter.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Fortress dressing may not replace Core-owned encounter bounds.");
            Assert.That(
                perimeter.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty,
                "Fortress dressing must remain presentation-only.");

            Transform[] perimeterTransforms =
                perimeter.GetComponentsInChildren<Transform>(true);
            Transform[] foundations = perimeterTransforms
                .Where(value =>
                    value.parent == perimeter &&
                    value.name == "FortressFoundationRing")
                .ToArray();
            Assert.That(foundations, Has.Length.EqualTo(1));
            Assert.That(foundations[0].localScale.x, Is.GreaterThan(0f));
            Assert.That(foundations[0].localScale.x, Is.LessThan(1f));
            Assert.That(
                foundations[0].localScale.y,
                Is.EqualTo(foundations[0].localScale.x).Within(0.001f));
            Assert.That(
                foundations[0].localScale.z,
                Is.EqualTo(foundations[0].localScale.x).Within(0.001f));

            Transform[] bays = perimeterTransforms
                .Where(value =>
                    value.parent == perimeter &&
                    value.name.StartsWith("FortressBay_"))
                .ToArray();
            Assert.That(bays, Has.Length.EqualTo(18));
            Transform[] towers = perimeterTransforms
                .Where(value =>
                    value.parent != null &&
                    value.parent.parent == perimeter &&
                    value.parent.name.StartsWith("FortressBay_") &&
                    value.name == "FortressTower")
                .ToArray();
            Transform[] walls = perimeterTransforms
                .Where(value =>
                    value.parent != null &&
                    value.parent.parent == perimeter &&
                    value.parent.name.StartsWith("FortressBay_") &&
                    (value.name == "FortressWallStraight" ||
                     value.name == "FortressWallBroken"))
                .ToArray();
            Assert.That(towers, Has.Length.EqualTo(6));
            Assert.That(walls, Has.Length.EqualTo(12));
            Assert.That(
                towers.All(value => value.parent.localPosition.z > 0f),
                Is.True,
                "The six boss towers must remain on the far side of the encounter.");

            for (int index = 0; index < bays.Length; index++)
            {
                FortressSurfaceVariationPresenter[] surfacePresenters = bays[index]
                    .GetComponentsInChildren<FortressSurfaceVariationPresenter>(true);
                Assert.That(
                    surfacePresenters,
                    Has.Length.EqualTo(1),
                    bays[index].name +
                    " needs one deterministic stone-surface variation.");
                Assert.That(surfacePresenters[0].Surface, Is.Not.Null);
                Assert.That(surfacePresenters[0].StoneMaterialIndex, Is.Zero);
            }

            for (int index = 0; index < towers.Length; index++)
            {
                FortressFireAnchorPresenter[] firePresenters = towers[index]
                    .GetComponentsInChildren<FortressFireAnchorPresenter>(true);
                Assert.That(
                    firePresenters,
                    Has.Length.EqualTo(1),
                    towers[index].name + " needs one ember-slot presenter.");
                Assert.That(firePresenters[0].Surface, Is.Not.Null);
                Assert.That(firePresenters[0].EmberMaterialIndex, Is.EqualTo(1));
            }

            Transform bossBraziers = perimeter.Find("BossBraziers");
            Assert.That(bossBraziers, Is.Not.Null);
            Transform[] braziers = bossBraziers
                .GetComponentsInChildren<Transform>(true)
                .Where(value =>
                    value.parent == bossBraziers &&
                    value.name.StartsWith("BossBrazier_"))
                .ToArray();
            Assert.That(braziers, Has.Length.EqualTo(2));
            for (int index = 0; index < braziers.Length; index++)
            {
                FortressFireAnchorPresenter firePresenter = braziers[index]
                    .GetComponent<FortressFireAnchorPresenter>();
                Assert.That(firePresenter, Is.Not.Null, braziers[index].name);
                Assert.That(firePresenter.Surface, Is.Not.Null);
                Assert.That(firePresenter.EmberMaterialIndex, Is.EqualTo(1));
            }

            Light[] bossLocalLights = bossPresentation
                .GetComponentsInChildren<Light>(true)
                .Where(value => value.isActiveAndEnabled)
                .ToArray();
            Assert.That(bossLocalLights, Has.Length.EqualTo(2));
            Assert.That(
                bossLocalLights.All(value =>
                    value.type == LightType.Point &&
                    value.shadows == LightShadows.None),
                Is.True,
                "Ember Vault supports exactly two unshadowed local point lights.");
            Light globalKey = GameObject.Find("MoonKey").GetComponent<Light>();
            Assert.That(globalKey.isActiveAndEnabled, Is.True);
            Assert.That(globalKey.type, Is.EqualTo(LightType.Directional));

            Assert.That(
                CountTriangles(
                    bossPresentation.GetComponentsInChildren<MeshFilter>(true)),
                Is.LessThanOrEqualTo(65000),
                "The complete static Ember Vault environment must stay inside its mobile triangle budget.");
        }

        [UnityTest]
        public IEnumerator Leveling_ActivatesAshenCausewayProgressionAndFirstPack()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Leveling,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            WorldZoneStreamController worldStreamer =
                Object.FindAnyObjectByType<WorldZoneStreamController>();
            Assert.That(worldStreamer, Is.Not.Null);
            yield return WaitForWorldStream(
                worldStreamer,
                () => worldStreamer.IsInitialWindowReady &&
                    worldStreamer.Status == WorldZoneStreamStatus.Ready);

            Assert.That(worldStreamer.ActiveZoneId, Is.EqualTo(1u));
            Assert.That(
                worldStreamer.FocusCoordinate,
                Is.EqualTo(new WorldChunkCoordinate(-3, 0)));
            Assert.That(
                worldStreamer.LoadedChunkCount,
                Is.InRange(1, 7));
            Assert.That(
                worldStreamer.TryGetLoadedChunk(0, out LoadedWorldChunk loadedChunk),
                Is.True);
            GameObject environment = loadedChunk.Presentation.VisualRoot.gameObject;
            Assert.That(environment, Is.Not.Null);
            Assert.That(
                loadedChunk.Presentation.SourceFingerprint,
                Has.Length.EqualTo(64),
                "Loaded chunk identity must include its generated-source fingerprint.");
            Transform westRefuge = environment.transform.Find(
                "AshenCausewayLandmarks/WestRefuge");
            Assert.That(westRefuge, Is.Not.Null);
            Assert.That(
                westRefuge.childCount,
                Is.GreaterThan(0),
                "The starting chunk must retain its authored refuge POI.");
            Assert.That(
                westRefuge.Find("RefugeWatchTower"),
                Is.Not.Null,
                "West Refuge needs a forward, gameplay-camera-scale landmark silhouette.");
            Assert.That(
                environment.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "The first leveling route is flat presentation over Core-owned bounds.");
            Assert.That(
                environment.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty,
                "Leveling environment dressing may not participate in simulation physics.");

            Light[] activePointLights = Object
                .FindObjectsByType<Light>(FindObjectsInactive.Exclude)
                .Where(light => light.type == LightType.Point)
                .ToArray();
            Assert.That(activePointLights, Has.Length.LessThanOrEqualTo(4));
            Assert.That(
                activePointLights.All(
                    light => light.GetComponentInParent<WorldChunkPresentation>() != null),
                Is.True,
                "Leveling must not inherit active point lights from an inactive arena.");

            Transform floorRoot = environment.transform.Find("AshenCausewayFloor");
            Assert.That(floorRoot, Is.Not.Null);
            Assert.That(floorRoot.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(floorRoot.GetComponent<MeshRenderer>(), Is.Not.Null);

            Transform lavaRoot = environment.transform.Find(
                "AshenCausewayLavaGullies");
            Assert.That(lavaRoot, Is.Not.Null);
            Assert.That(
                lavaRoot.GetComponent<HazardSurfaceAnimator>(),
                Is.Not.Null,
                "The route lava needs its low-cost animated surface language.");
            Assert.That(
                lavaRoot.GetComponentsInChildren<MeshRenderer>(true),
                Is.Not.Empty);

            Transform atmosphereRoot = environment.transform.Find(
                "AshenCausewayAtmosphere");
            Assert.That(atmosphereRoot, Is.Not.Null);
            ParticleSystem[] atmosphereParticles = atmosphereRoot
                .GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(atmosphereParticles, Is.Not.Empty);
            for (int particleIndex = 0;
                 particleIndex < atmosphereParticles.Length;
                 particleIndex++)
            {
                ParticleSystem particles = atmosphereParticles[particleIndex];
                Assert.That(
                    particles.main.maxParticles,
                    Is.LessThanOrEqualTo(32),
                    particles.name + " exceeds the authored mobile atmosphere cap.");
                Assert.That(
                    particles.collision.enabled,
                    Is.False,
                    particles.name + " must not run particle collision.");
            }

            Assert.That(GameObject.Find("LevelProgression"), Is.Not.Null);

            CombatantView[] countdownViews = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude);
            Assert.That(countdownViews, Has.Length.EqualTo(1));
            Assert.That(countdownViews[0].IsHuman, Is.True);

            StrategyFollowCamera cameraRig =
                Object.FindAnyObjectByType<StrategyFollowCamera>();
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, StrategyFollowCamera.LevelingYaw),
                Is.EqualTo(0f).Within(0.01f));
            Assert.That(
                cameraRig.Distance,
                Is.EqualTo(StrategyFollowCamera.LevelingDistance).Within(0.01f));
            Assert.That(
                cameraRig.GetComponent<Camera>().fieldOfView,
                Is.EqualTo(StrategyFollowCamera.LevelingFieldOfView).Within(0.01f));

            const float firstPackTimeoutSeconds = 5f;
            float firstPackDeadline =
                Time.realtimeSinceStartup + firstPackTimeoutSeconds;
            CombatantView[] firstPackViews =
                Object.FindObjectsByType<CombatantView>(
                    FindObjectsInactive.Exclude);
            while (firstPackViews.Length != 3 &&
                   Time.realtimeSinceStartup < firstPackDeadline)
            {
                yield return null;
                firstPackViews = Object.FindObjectsByType<CombatantView>(
                    FindObjectsInactive.Exclude);
            }

            Assert.That(firstPackViews, Has.Length.EqualTo(3));
            Assert.That(
                firstPackViews.Count(view => view.Team.Value == 2),
                Is.EqualTo(2));
            CombatSimulation trainingSimulation =
                GetPrivateField<CombatSimulation>(controller, "_simulation");
            Assert.That(
                trainingSimulation.TryGetSnapshot(
                    new ParticipantId(1),
                    out CombatantSnapshot untouchedLearner),
                Is.True);
            Assert.That(
                untouchedLearner.Health,
                Is.EqualTo(untouchedLearner.MaxHealth).Within(0.001f),
                "Pack one must wait while a new player learns movement and focus selection.");
            PrototypeHud trainingHud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(trainingHud.FocusedFighterId, Is.Zero);
            Assert.That(
                firstPackViews
                    .Where(view => view.Team.Value == 2)
                    .All(view => !view.transform.Find("TargetRing").gameObject.activeSelf),
                Is.True,
                "The focus lesson must begin without silently selecting an enemy.");
            foreach (CombatantView enemy in firstPackViews.Where(
                         view => view.Team.Value == 2))
            {
                Assert.That(
                    enemy.IsStealthed,
                    Is.False,
                    "Open-world NPC proxies must not inherit the Rogue player's opening stealth.");
                Assert.That(enemy.IsHiddenFromViewer, Is.False);
                SoftTargetCandidate enemyCandidate =
                    enemy.GetComponent<SoftTargetCandidate>();
                Assert.That(
                    enemy.IsTargetable,
                    Is.True,
                    "Leveling enemy " + enemy.Id.Value +
                    " targetability mismatch: alive=" + enemy.IsAlive +
                    ", rootActive=" + enemy.gameObject.activeInHierarchy +
                    ", candidatePresent=" + (enemyCandidate != null) +
                    ", candidateEnabled=" +
                    (enemyCandidate != null && enemyCandidate.enabled) + ".");
                CombatantRelationMarker marker = enemy
                    .GetComponentInChildren<CombatantRelationMarker>(true);
                Assert.That(marker, Is.Not.Null);
                Assert.That(
                    marker.ClassBadgeText,
                    Is.EqualTo(enemy.Id.Value == 2 ? "RA" : "MK"),
                    "Leveling profiles need readable overhead identities.");
            }

            CombatantView badgeProbe = firstPackViews.Single(view => view.Id.Value == 2);
            CombatantRelationMarker badgeProbeMarker = badgeProbe
                .GetComponentInChildren<CombatantRelationMarker>(true);
            LevelingMobArchetypeId[] badgeArchetypes =
            {
                LevelingMobArchetypeId.AshMelee,
                LevelingMobArchetypeId.AshRanged,
                LevelingMobArchetypeId.AshBrute,
                LevelingMobArchetypeId.AshCaster,
                LevelingMobArchetypeId.AshElite,
                LevelingMobArchetypeId.RimeFrostweaver,
                LevelingMobArchetypeId.ReliquaryWarder,
                LevelingMobArchetypeId.RiftWarden,
            };
            string[] expectedBadges =
            {
                "RA", "MK", "BR", "HX", "PR", "FW", "WD", "WN",
            };
            for (int badgeIndex = 0; badgeIndex < badgeArchetypes.Length; badgeIndex++)
            {
                badgeProbe.ConfigureLevelingArchetype(badgeArchetypes[badgeIndex]);
                Assert.That(
                    badgeProbeMarker.ClassBadgeText,
                    Is.EqualTo(expectedBadges[badgeIndex]),
                    badgeArchetypes[badgeIndex] + " lost its overhead identity.");
            }
            badgeProbe.ConfigureLevelingArchetype(LevelingMobArchetypeId.AshMelee);

            CombatantStatusView[] hostileCards = Object
                .FindObjectsByType<CombatantStatusView>(FindObjectsInactive.Include)
                .Where(card => card.IsBound && !card.IsFriendly)
                .ToArray();
            Assert.That(hostileCards, Has.Length.EqualTo(2));
            Assert.That(
                hostileCards.Select(card => card.DisplayName),
                Is.EquivalentTo(new[] { "RAIDER", "MARKSMAN" }),
                "Leveling enemy cards must expose both authored NPC identities.");

            controller.StartLocalSession(
                GameModeId.Leveling,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return null;

            CombatantView[] replayCountdownViews =
                Object.FindObjectsByType<CombatantView>(
                    FindObjectsInactive.Exclude);
            Assert.That(
                replayCountdownViews,
                Has.Length.EqualTo(1),
                "Starting a fresh expedition must hide pooled enemies from the previous run.");
            Assert.That(replayCountdownViews[0].IsHuman, Is.True);
        }

        [UnityTest]
        public IEnumerator Leveling_HomeButtonsEnterExpeditionWithAuthoredProgressionHud()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.HasAuthoredLevelingContract, Is.True);

            GameObject levelingButtonObject = GameObject.Find("LevelingExpedition");
            GameObject startButtonObject = GameObject.Find("Start");
            Assert.That(levelingButtonObject, Is.Not.Null);
            Assert.That(startButtonObject, Is.Not.Null);
            Button levelingButton = levelingButtonObject.GetComponent<Button>();
            Button startButton = startButtonObject.GetComponent<Button>();
            Assert.That(levelingButton, Is.Not.Null);
            Assert.That(startButton, Is.Not.Null);

            levelingButton.onClick.Invoke();
            Assert.That(hud.SelectedMode, Is.EqualTo(PrototypeMode.Leveling));
            startButton.onClick.Invoke();
            WorldZoneStreamController worldStreamer =
                Object.FindAnyObjectByType<WorldZoneStreamController>();
            Assert.That(worldStreamer, Is.Not.Null);
            yield return WaitForWorldStream(
                worldStreamer,
                () => worldStreamer.IsInitialWindowReady);

            Assert.That(hud.Screen, Is.EqualTo(PrototypeHudScreen.Gameplay));
            Assert.That(worldStreamer.LoadedChunkCount, Is.GreaterThan(0));
            Assert.That(hud.IsLevelProgressionVisible, Is.True);
            Assert.That(hud.LevelProgressionLabel, Is.EqualTo("LEVEL 1"));
            Assert.That(hud.ExperienceProgressionLabel, Is.EqualTo("0 / 80 XP"));

            SpecializationId specialization = hud.SelectedSpecialization;
            AbilityKit levelingKit = LevelingAbilityUnlockCatalog.GetKit(
                specialization);
            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView, Is.Not.Null, "Ability slot " + slot);
                bool visible = slot < LevelingAbilityUnlockCatalog.InitialAbilityCount;
                Assert.That(
                    slotView.gameObject.activeSelf,
                    Is.EqualTo(visible),
                    "Level-one visibility mismatch for slot " + slot + ".");
                if (slot >= LevelingAbilityUnlockCatalog.TotalAbilityCount)
                {
                    continue;
                }

                Assert.That(
                    slotView.AbilityId,
                    Is.EqualTo(levelingKit.GetAbility(slot)));
                Assert.That(slotView.HasAlternateUp, Is.False);
                Assert.That(
                    slotView.IsInteractionEnabled,
                    Is.EqualTo(visible),
                    "Only learned Training skills may be interactive.");
                Assert.That(
                    slotView.GetComponent<CanvasGroup>().blocksRaycasts,
                    Is.EqualTo(visible),
                    "Hidden Training skills must not retain a raycast surface.");
            }

            Vector2 levelFivePosition = ((RectTransform)hud
                .GetAbilitySlot(LevelingAbilityUnlockCatalog.LevelFiveUnlockSlot)
                .transform).anchoredPosition;
            for (int slot = LevelingAbilityUnlockCatalog.InitialAbilityCount;
                 slot < LevelingAbilityUnlockCatalog.TotalAbilityCount;
                 slot++)
            {
                Assert.That(
                    hud.GetAbilitySlot(slot).gameObject.activeSelf,
                    Is.False,
                    "An unlearned skill must not draw a placeholder circle.");
            }

            hud.BindLevelingKit(specialization, 3);
            for (int slot = 0;
                 slot <= LevelingAbilityUnlockCatalog.LevelThreeUnlockSlot;
                 slot++)
            {
                Assert.That(hud.GetAbilitySlot(slot).gameObject.activeSelf, Is.True);
                Assert.That(hud.GetAbilitySlot(slot).IsInteractionEnabled, Is.True);
            }
            Assert.That(
                hud.GetAbilitySlot(4).ReadinessState,
                Is.EqualTo(AbilityReadinessState.Ready));
            Assert.That(hud.GetAbilitySlot(5).gameObject.activeSelf, Is.False);
            Assert.That(hud.GetAbilitySlot(6).gameObject.activeSelf, Is.False);

            hud.BindLevelingKit(specialization, 5);
            Assert.That(hud.GetAbilitySlot(5).IsInteractionEnabled, Is.True);
            Assert.That(hud.GetAbilitySlot(6).IsInteractionEnabled, Is.True);
            Assert.That(
                hud.GetAbilitySlot(6).ReadinessState,
                Is.EqualTo(AbilityReadinessState.Ready));
            Assert.That(
                ((RectTransform)hud.GetAbilitySlot(6).transform).anchoredPosition,
                Is.EqualTo(levelFivePosition),
                "Unlocking must reveal the skill in its authored slot without reflow.");

            hud.BindSpecializationKit(specialization);
            AbilityKit arenaKit = SpecializationCatalog.GetKit(specialization);
            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView.gameObject.activeSelf, Is.True);
                Assert.That(slotView.IsInteractionEnabled, Is.True);
                Assert.That(slotView.AbilityId, Is.EqualTo(arenaKit.GetAbility(slot)));
            }
        }

        [UnityTest]
        public IEnumerator Leveling_XpThresholdsRebindHudAndCoreAtLevelsThreeAndFive()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            CombatInputSource input = Object.FindAnyObjectByType<CombatInputSource>();
            controller.StartLocalSession(
                GameModeId.Leveling,
                ClassId.Mage,
                SpecializationId.Fire);

            LevelingRunController run = GetPrivateField<LevelingRunController>(
                controller,
                "_levelingRun");
            CombatSimulation simulation = GetPrivateField<CombatSimulation>(
                controller,
                "_simulation");
            float activationDeadline = Time.realtimeSinceStartup + 6f;
            while (run.ActiveMobCount == 0 &&
                   Time.realtimeSinceStartup < activationDeadline)
            {
                yield return null;
            }

            Assert.That(run.ActivePackIndex, Is.EqualTo(0));
            controller.enabled = false;

            int eventTick = simulation.CurrentTick;
            ResolveLevelingPackForController(
                controller,
                run.Zone.GetPack(0),
                eventTick);
            Assert.That(run.Progression.Level, Is.EqualTo(2));

            eventTick += LevelingRunController.InterPackDelayTicks;
            Assert.That(
                run.TryActivateNextPack(
                    eventTick,
                    float.MaxValue,
                    out LevelingPackDefinition levelThreePack),
                Is.True);
            ResolveLevelingPackForController(
                controller,
                levelThreePack,
                eventTick);

            Assert.That(run.Progression.Level, Is.EqualTo(3));
            Assert.That(
                simulation.TryGetSnapshot(
                    new ParticipantId(LevelingRunController.PlayerParticipantValue),
                    out CombatantSnapshot levelThreePlayer),
                Is.True);
            Assert.That(levelThreePlayer.Level, Is.EqualTo(3));
            Assert.That(
                hud.GetAbilitySlot(LevelingAbilityUnlockCatalog.LevelThreeUnlockSlot)
                    .gameObject.activeSelf,
                Is.True);
            Assert.That(
                hud.GetAbilitySlot(LevelingAbilityUnlockCatalog.LevelFiveUnlockSlot)
                    .gameObject.activeSelf,
                Is.False);

            AbilityId[] desktopBindings = GetPrivateField<AbilityId[]>(
                input,
                "_desktopAbilityBindings");
            AbilityKit kit = LevelingAbilityUnlockCatalog.GetKit(
                SpecializationId.Fire);
            Assert.That(
                desktopBindings[LevelingAbilityUnlockCatalog.LevelThreeUnlockSlot],
                Is.EqualTo(kit.GetAbility(
                    LevelingAbilityUnlockCatalog.LevelThreeUnlockSlot)));

            for (int packIndex = 2; packIndex <= 3; packIndex++)
            {
                eventTick += LevelingRunController.InterPackDelayTicks;
                Assert.That(
                    run.TryActivateNextPack(
                        eventTick,
                        float.MaxValue,
                        out LevelingPackDefinition pack),
                    Is.True,
                    "Could not activate authored pack " + (packIndex + 1) + ".");
                Assert.That(pack.Index, Is.EqualTo(packIndex));
                ResolveLevelingPackForController(controller, pack, eventTick);
            }

            Assert.That(run.Progression.Level, Is.EqualTo(5));
            Assert.That(
                simulation.TryGetSnapshot(
                    new ParticipantId(LevelingRunController.PlayerParticipantValue),
                    out CombatantSnapshot levelFivePlayer),
                Is.True);
            Assert.That(levelFivePlayer.Level, Is.EqualTo(5));
            AbilitySlotView levelFiveSlot = hud.GetAbilitySlot(
                LevelingAbilityUnlockCatalog.LevelFiveUnlockSlot);
            Assert.That(levelFiveSlot.gameObject.activeSelf, Is.True);
            Assert.That(levelFiveSlot.IsInteractionEnabled, Is.True);
            Assert.That(levelFiveSlot.ReadinessState, Is.EqualTo(AbilityReadinessState.Ready));
            Assert.That(
                desktopBindings[LevelingAbilityUnlockCatalog.LevelFiveUnlockSlot],
                Is.EqualTo(kit.GetAbility(
                    LevelingAbilityUnlockCatalog.LevelFiveUnlockSlot)));
        }

        [UnityTest]
        public IEnumerator Leveling_StreamWindowLoadsAcrossRouteAndUnloadsOnHome()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            WorldZoneStreamController streamer =
                Object.FindAnyObjectByType<WorldZoneStreamController>();
            Assert.That(streamer, Is.Not.Null);

            controller.StartLocalSession(
                GameModeId.Leveling,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return WaitForWorldStream(
                streamer,
                () => streamer.Status == WorldZoneStreamStatus.Ready);
            Assert.That(
                streamer.IsChunkLoaded(new WorldChunkCoordinate(-3, 0)),
                Is.True);
            controller.enabled = false;

            string[] districtRoots =
            {
                "WestRefuge",
                "CinderApproach",
                "BrokenGate",
                "CollapsedCourt",
                "OssuaryBend",
                "ReliquaryYard",
                "EastWatch",
            };
            for (int chunkX = -3; chunkX <= 3; chunkX++)
            {
                WorldChunkCoordinate coordinate = new WorldChunkCoordinate(
                    chunkX,
                    0);
                streamer.SetFocus(new Vector3(
                    (chunkX * 64f) + 32f,
                    0f,
                    ArenaCollisionMap.LevelingCenterZ));
                yield return WaitForWorldStream(
                    streamer,
                    () => streamer.Status == WorldZoneStreamStatus.Ready &&
                        streamer.FocusCoordinate == coordinate &&
                        streamer.IsChunkLoaded(coordinate));

                bool foundFocusChunk = false;
                for (int loadedIndex = 0;
                     loadedIndex < streamer.LoadedChunkCount;
                     loadedIndex++)
                {
                    if (!streamer.TryGetLoadedChunk(
                            loadedIndex,
                            out LoadedWorldChunk loaded) ||
                        loaded.Coordinate != coordinate)
                    {
                        continue;
                    }

                    Transform district = loaded.Presentation.VisualRoot.Find(
                        "AshenCausewayLandmarks/" + districtRoots[chunkX + 3]);
                    Assert.That(district, Is.Not.Null, coordinate.ToString());
                    Assert.That(
                        district.childCount,
                        Is.GreaterThan(0),
                        districtRoots[chunkX + 3] +
                        " must contain bespoke authored landmark geometry.");
                    foundFocusChunk = true;
                    break;
                }

                Assert.That(foundFocusChunk, Is.True, coordinate.ToString());
                Assert.That(streamer.LoadedChunkCount, Is.LessThanOrEqualTo(7));
            }

            Assert.That(
                streamer.IsChunkLoaded(new WorldChunkCoordinate(-3, 0)),
                Is.False,
                "Crossing the full route must unload the west refuge outside " +
                "the retain radius.");

            controller.ReturnHome();
            yield return WaitForWorldStream(
                streamer,
                () => streamer.Status == WorldZoneStreamStatus.Inactive);
            Assert.That(streamer.LoadedChunkCount, Is.Zero);
            Assert.That(streamer.ResidentChunkCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Leveling_DisablingStreamerDrainsOwnedAndInflightScenes()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            WorldZoneStreamController streamer =
                Object.FindAnyObjectByType<WorldZoneStreamController>();
            Assert.That(streamer, Is.Not.Null);

            controller.StartLocalSession(
                GameModeId.Leveling,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            float operationDeadline = Time.realtimeSinceStartup + 5f;
            while (streamer.PendingOperation != WorldZoneStreamOperation.Load &&
                   streamer.Status != WorldZoneStreamStatus.Ready &&
                   Time.realtimeSinceStartup < operationDeadline)
            {
                yield return null;
            }

            Assert.That(
                streamer.PendingOperation == WorldZoneStreamOperation.Load ||
                streamer.LoadedChunkCount > 0,
                Is.True,
                "The lifecycle test requires owned or in-flight chunk content.");
            streamer.enabled = false;

            float cleanupDeadline = Time.realtimeSinceStartup + 15f;
            while ((streamer.LifecycleCleanupPendingCount > 0 ||
                    CountLoadedWorldChunkScenes() > 0) &&
                   Time.realtimeSinceStartup < cleanupDeadline)
            {
                yield return null;
            }

            Assert.That(streamer.LifecycleCleanupPendingCount, Is.Zero);
            Assert.That(CountLoadedWorldChunkScenes(), Is.Zero);
            Assert.That(streamer.LoadedChunkCount, Is.Zero);
            Assert.That(streamer.ResidentChunkCount, Is.Zero);
            Assert.That(streamer.Status, Is.EqualTo(WorldZoneStreamStatus.Inactive));
        }

        private static ArenaPresentation FindActiveArenaPresentation(
            ArenaContentMode mode)
        {
            ArenaPresentation[] presentations =
                Object.FindObjectsByType<ArenaPresentation>(FindObjectsInactive.Exclude);
            return System.Array.Find(
                presentations,
                presentation => presentation.Mode == mode);
        }

        private static int CountLoadedWorldChunkScenes()
        {
            const string sceneRoot =
                "Assets/SessionRpg/Content/Worlds/";
            int loadedCount = 0;
            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.isLoaded &&
                    scene.path.StartsWith(
                        sceneRoot,
                        System.StringComparison.Ordinal))
                {
                    loadedCount++;
                }
            }

            return loadedCount;
        }

        [UnityTest]
        public IEnumerator Arena_UsesUrpAndLockedStrategyFraming()
        {
            PrototypeGameController controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForSeconds(0.2f);

            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null);
            UniversalRenderPipelineAsset activePipeline =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(activePipeline, Is.Not.Null);
            Assert.That(
                activePipeline.supportsHDR,
                Is.True,
                "High must retain its HDR buffer for authored emission, " +
                "bloom, and ACES tone mapping.");
            ArenaPresentation presentation = FindActiveArenaPresentation(
                ArenaContentMode.Arena);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(
                presentation.TryGetCaptureAnchor(
                    ArenaCaptureAnchorKind.Gameplay,
                    out ArenaCaptureAnchor gameplayAnchor),
                Is.True);
            Quaternion expectedCaptureRotation = Quaternion.Euler(
                StrategyFollowCamera.ArenaPitch,
                StrategyFollowCamera.ArenaYaw,
                0f);
            Vector3 expectedCaptureFocus = new Vector3(
                StrategyFollowCamera.ArenaForwardFocusOffset,
                0.9f,
                0f);
            Vector3 expectedCapturePosition = expectedCaptureFocus +
                (expectedCaptureRotation * Vector3.back *
                 StrategyFollowCamera.ArenaDistance);
            Assert.That(
                gameplayAnchor.FieldOfView,
                Is.EqualTo(StrategyFollowCamera.ArenaFieldOfView).Within(0.001f));
            Assert.That(
                Vector3.Distance(
                    gameplayAnchor.Pose.position,
                    expectedCapturePosition),
                Is.LessThan(0.001f),
                "The catalog gameplay anchor must match the runtime Arena composition.");
            Assert.That(
                Quaternion.Angle(
                    gameplayAnchor.Pose.rotation,
                    expectedCaptureRotation),
                Is.LessThan(0.001f));

            Camera camera = Object.FindAnyObjectByType<Camera>();
            camera.aspect = 16f / 9f;
            StrategyFollowCamera cameraRig = camera.GetComponent<StrategyFollowCamera>();
            Assert.That(camera.orthographic, Is.False);
            Assert.That(camera.allowHDR, Is.True);
            Assert.That(camera.allowMSAA, Is.True);
            Assert.That(camera.allowDynamicResolution, Is.False);
            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            Assert.That(cameraData, Is.Not.Null);
            Assert.That(cameraData.renderPostProcessing, Is.True);
            Assert.That(cameraData.stopNaN, Is.True);
            Assert.That(cameraData.dithering, Is.True);
            Assert.That(cameraData.renderShadows, Is.True);
            Assert.That(cameraData.volumeLayerMask.value, Is.EqualTo(~0));
            Assert.That(
                camera.fieldOfView,
                Is.EqualTo(StrategyFollowCamera.ArenaFieldOfView).Within(0.01f));
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(
                camera.backgroundColor,
                Is.EqualTo(new Color(0.003f, 0.0045f, 0.01f, 1f)),
                "The empty strategy-frame corners need to fall behind the " +
                "fortress silhouette without changing the lit environment.");
            GameObject abyssBackdrop = GameObject.Find("ArenaAbyssBackdrop");
            Assert.That(abyssBackdrop, Is.Not.Null);
            Renderer abyssRenderer = abyssBackdrop.GetComponent<Renderer>();
            Assert.That(abyssRenderer, Is.Not.Null);
            Assert.That(
                abyssRenderer.shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(abyssRenderer.receiveShadows, Is.False);
            Material abyssMaterial = abyssRenderer.sharedMaterial;
            Assert.That(
                abyssMaterial.shader.name,
                Does.Contain("Unlit"),
                "The large abyss plane must not turn grey when ambient lighting changes.");
            Color abyssColor = abyssMaterial.color;
            Assert.That(abyssColor.r, Is.EqualTo(0.018f).Within(0.001f));
            Assert.That(abyssColor.g, Is.EqualTo(0.012f).Within(0.001f));
            Assert.That(abyssColor.b, Is.EqualTo(0.014f).Within(0.001f));
            Assert.That(abyssColor.a, Is.EqualTo(1f).Within(0.001f));
            Texture abyssTexture = abyssMaterial.GetTexture("_BaseMap");
            Assert.That(abyssTexture, Is.Not.Null);
            Assert.That(abyssTexture.name, Does.Contain("ArenaLavaAlbedo"));
            Assert.That(
                abyssMaterial.GetTextureScale("_BaseMap"),
                Is.EqualTo(Vector2.one * 0.85f));
            Assert.That(
                abyssMaterial.GetTextureOffset("_BaseMap"),
                Is.EqualTo(new Vector2(0.37f, 0.19f)));
            GameObject cliffBackdrop = GameObject.Find("ArenaCliffBackdrop");
            Assert.That(
                cliffBackdrop,
                Is.Not.Null,
                "The strategy frame needs a textured cavern wall behind the " +
                "fortress instead of exposed black corners.");
            Assert.That(cliffBackdrop.layer, Is.EqualTo(2));
            Assert.That(
                cliffBackdrop.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "The distant cliff is presentation-only.");
            MeshFilter cliffFilter = cliffBackdrop.GetComponent<MeshFilter>();
            MeshRenderer cliffRenderer = cliffBackdrop.GetComponent<MeshRenderer>();
            Assert.That(cliffFilter.sharedMesh.name, Is.EqualTo("ArenaCliffBackdrop"));
            Assert.That(CountTriangles(new[] { cliffFilter }), Is.EqualTo(192));
            Assert.That(cliffFilter.sharedMesh.HasVertexAttribute(
                UnityEngine.Rendering.VertexAttribute.Tangent), Is.True);
            Assert.That(cliffRenderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(cliffRenderer.sharedMaterial.name, Does.Contain("RuinStone"));
            Assert.That(
                cliffRenderer.shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off),
                "The enclosing visual shell may not cast one arena-wide shadow.");
            Assert.That(cliffRenderer.receiveShadows, Is.True);
            Assert.That(
                cliffRenderer.reflectionProbeUsage,
                Is.EqualTo(ReflectionProbeUsage.Simple));
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<StrategyFollowCamera>(), Has.Length.EqualTo(1));
            Assert.That(
                cameraRig.Pitch,
                Is.EqualTo(StrategyFollowCamera.ArenaPitch).Within(0.01f));
            Assert.That(StrategyFollowCamera.ArenaPitch, Is.EqualTo(40f));
            Assert.That(
                cameraRig.Distance,
                Is.EqualTo(StrategyFollowCamera.ArenaDistance).Within(0.01f));
            Assert.That(StrategyFollowCamera.ArenaDistance, Is.EqualTo(18f));
            Assert.That(StrategyFollowCamera.ArenaFieldOfView, Is.EqualTo(46f));
            Assert.That(
                cameraRig.ForwardFocusOffset,
                Is.EqualTo(StrategyFollowCamera.ArenaForwardFocusOffset).Within(0.01f));
            Assert.That(
                cameraRig.RightFocusOffset,
                Is.EqualTo(StrategyFollowCamera.ArenaRightFocusOffset).Within(0.01f));
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, StrategyFollowCamera.ArenaYaw),
                Is.EqualTo(0f).Within(0.01f));

            CombatantView[] combatants =
                Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
            Assert.That(combatants, Has.Length.EqualTo(4));
            float friendlyHeight = 0f;
            float hostileHeight = 0f;
            int friendlyCount = 0;
            int hostileCount = 0;
            for (int index = 0; index < combatants.Length; index++)
            {
                CombatantView combatant = combatants[index];
                Vector3 viewport = camera.WorldToViewportPoint(combatant.AimPoint.position);
                Assert.That(viewport.z, Is.GreaterThan(0f));
                Assert.That(viewport.x, Is.InRange(0.04f, 0.96f));
                Assert.That(viewport.y, Is.InRange(0.08f, 0.94f));
                if (combatant.Team.Value == 1)
                {
                    friendlyHeight += viewport.y;
                    friendlyCount++;
                }
                else
                {
                    hostileHeight += viewport.y;
                    hostileCount++;
                }
            }

            Assert.That(
                hostileHeight / hostileCount,
                Is.GreaterThan(friendlyHeight / friendlyCount),
                "The fixed arena heading must place opponents above allies on screen.");

            GameObject safeStone = GameObject.Find("SafeStone");
            Assert.That(safeStone, Is.Not.Null);
            Assert.That(
                safeStone.transform.localScale.x,
                Is.EqualTo(ArenaLayout.SafeRadius * 2f).Within(0.001f));
            Assert.That(safeStone.layer, Is.EqualTo(10));
            AssertFlatCylinderCollider(safeStone);
            GameObject lava = GameObject.Find("Lava");
            AssertFlatCylinderCollider(lava);
            Assert.That(
                lava.transform.localScale.x,
                Is.EqualTo((ArenaLayout.OuterKillRadius + 8f) * 2f).Within(0.001f),
                "The visual moat must fill the target frame without changing the " +
                "authoritative kill radius.");
            Texture lavaTexture = lava.GetComponent<Renderer>()
                .sharedMaterial.GetTexture("_BaseMap");
            Assert.That(lavaTexture, Is.Not.Null);
            Assert.That(lavaTexture.name, Does.Contain("ArenaLavaAlbedo"));
            Assert.That(
                lava.GetComponent<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"),
                "The lava hazard now combines authored emission with the High " +
                "profile's directional and ambient surface response.");
            HazardSurfaceAnimator lavaAnimator =
                lava.GetComponent<HazardSurfaceAnimator>();
            Assert.That(
                lavaAnimator,
                Is.Not.Null,
                "The authored lava surface still needs its low-cost material animation.");
            Assert.That(
                GetPrivateField<float>(lavaAnimator, "_pulseSpeed"),
                Is.EqualTo(0.22f).Within(0.001f),
                "Cooled crust should breathe slowly instead of flashing like a combat telegraph.");
            Assert.That(
                GetPrivateField<float>(lavaAnimator, "_surfaceTiling"),
                Is.EqualTo(1.7f).Within(0.001f));
            MaterialPropertyBlock lavaProperties = new MaterialPropertyBlock();
            lava.GetComponent<Renderer>().GetPropertyBlock(lavaProperties);
            Color animatedLavaColor = lavaProperties.GetColor("_BaseColor");
            Assert.That(
                animatedLavaColor.r,
                Is.InRange(0.82f, 1f),
                "The warm pulse must preserve the dark cooled plates in the authored texture.");
            Assert.That(
                animatedLavaColor.g,
                Is.InRange(0.42f, 0.58f),
                "The lava tint must stay orange rather than washing the moat grey.");
            Assert.That(
                animatedLavaColor.b,
                Is.InRange(0.2f, 0.26f),
                "Encoded orange fissures, rather than a global tint, own the hazard color.");
            Assert.That(
                animatedLavaColor.r,
                Is.GreaterThan(animatedLavaColor.g * 1.25f));
            Assert.That(
                animatedLavaColor.g,
                Is.GreaterThan(animatedLavaColor.b * 1.25f));
            Assert.That(
                lavaProperties.GetVector("_BaseMap_ST").x,
                Is.EqualTo(1.7f).Within(0.001f),
                "The lava texture must retain broad organic shapes at strategy distance.");
            Assert.That(
                lavaProperties.GetVector("_EmissionMap_ST"),
                Is.EqualTo(lavaProperties.GetVector("_BaseMap_ST")),
                "Lava crust and emissive cracks must share one coherent texture phase.");
            Assert.That(
                GetPrivateField<Color>(lavaAnimator, "_lowSurfaceEmissionColor"),
                Is.EqualTo(new Color(0.55f, 0.32f, 0.12f, 1f)));
            Assert.That(
                GetPrivateField<Color>(lavaAnimator, "_highSurfaceEmissionColor"),
                Is.EqualTo(new Color(2.8f, 1.3f, 0.38f, 1f)));
            Assert.That(
                GetPrivateField<Color>(lavaAnimator, "_lowSeamColor"),
                Is.EqualTo(new Color(0.003f, 0f, 0f, 1f)));
            Assert.That(
                GetPrivateField<Color>(lavaAnimator, "_highSeamColor"),
                Is.EqualTo(new Color(0.012f, 0.001f, 0f, 1f)));
            GameObject lavaFissures = GameObject.Find("ArenaLavaFissures");
            Assert.That(lavaFissures, Is.Not.Null);
            Assert.That(lavaFissures.layer, Is.EqualTo(2));
            Assert.That(lavaFissures.GetComponentsInChildren<Collider>(true), Is.Empty);
            MeshFilter fissureFilter = lavaFissures.GetComponent<MeshFilter>();
            MeshRenderer fissureRenderer = lavaFissures.GetComponent<MeshRenderer>();
            Assert.That(fissureFilter.sharedMesh.name, Is.EqualTo("ArenaLavaFissures"));
            Assert.That(CountTriangles(new[] { fissureFilter }), Is.EqualTo(144));
            Assert.That(fissureRenderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(
                fissureRenderer.sharedMaterial.name,
                Does.Contain("LavaSeam"));
            Assert.That(
                fissureRenderer.shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(fissureRenderer.receiveShadows, Is.False);
            Assert.That(
                fissureRenderer.reflectionProbeUsage,
                Is.EqualTo(ReflectionProbeUsage.Off));
            Assert.That(
                GetPrivateField<Renderer>(lavaAnimator, "_seams"),
                Is.SameAs(fissureRenderer));
            Assert.That(
                GameObject.Find("LavaSeams"),
                Is.Null,
                "The authored lava texture replaces the old debug-like procedural ribbons.");
            Assert.That(GameObject.Find("HighGround"), Is.Null);
            Transform floorVisualTransform = presentation.VisualRoot.Find(
                "ArenaFloorVisual");
            Assert.That(
                floorVisualTransform,
                Is.Not.Null,
                "The arena floor must be the direct authored visual root; the FBX may retain an internal axis-conversion child.");
            GameObject floorVisual = floorVisualTransform.gameObject;
            Assert.That(
                floorVisual.transform.IsChildOf(presentation.VisualRoot),
                Is.True,
                "The source floor is presentation-only and must live under Visuals.");
            Assert.That(floorVisual.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(floorVisual.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(floorVisual.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(
                floorVisual.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "The imported floor may never replace the SafeStone gameplay proxy.");
            Assert.That(
                floorVisual.GetComponentsInChildren<Transform>(true)
                    .All(value => value.gameObject.layer == 2),
                Is.True,
                "Every imported visual child must remain on IgnoreRaycast.");

            MeshRenderer[] floorRenderers =
                floorVisual.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] floorFilters =
                floorVisual.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(floorRenderers, Has.Length.EqualTo(1));
            Assert.That(floorFilters, Has.Length.EqualTo(1));
            MeshRenderer floorRenderer = floorRenderers[0];
            Assert.That(floorRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(floorRenderer.receiveShadows, Is.True);
            Assert.That(
                floorRenderer.reflectionProbeUsage,
                Is.EqualTo(ReflectionProbeUsage.Simple),
                "The PBR floor must sample the authored environment reflection.");
            Assert.That(floorRenderer.sharedMaterials, Has.Length.EqualTo(3));
            Assert.That(
                floorRenderer.sharedMaterials[0].name,
                Does.Contain("ArenaFloor_Stone"));
            Assert.That(
                floorRenderer.sharedMaterials[1].name,
                Does.Contain("ArenaFloor_Trim"));
            Assert.That(
                floorRenderer.sharedMaterials[2].name,
                Does.Contain("ArenaFloor_Grout"));
            Material floorStoneMaterial = floorRenderer.sharedMaterials[0];
            Assert.That(
                floorStoneMaterial.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"),
                "The authored floor maps require the URP Lit channel contract.");
            Assert.That(
                floorStoneMaterial.HasProperty("_BaseMap") &&
                floorStoneMaterial.GetTexture("_BaseMap") != null &&
                floorStoneMaterial.GetTexture("_BaseMap").name.Contains(
                    "ArenaFloorMacroAlbedo"),
                Is.True,
                "The stone slot must use the single-mapped full-floor composition.");
            Color floorStoneColor = floorStoneMaterial.color;
            Assert.That(floorStoneColor.r, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(floorStoneColor.g, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(floorStoneColor.b, Is.EqualTo(0.94f).Within(0.001f));
            Assert.That(floorStoneColor.a, Is.EqualTo(1f).Within(0.001f));
            Texture2D floorTexture = floorStoneMaterial
                .GetTexture("_BaseMap") as Texture2D;
            Assert.That(floorTexture, Is.Not.Null);
            Assert.That(
                floorTexture.anisoLevel,
                Is.EqualTo(8),
                "The oblique strategy camera needs the strongest-phone anisotropic " +
                "contract on the shared floor texture.");
            Assert.That(
                floorStoneMaterial.GetTextureScale("_BaseMap"),
                Is.EqualTo(Vector2.one),
                "The macro composition must map exactly once across the 30 m floor.");
            Assert.That(
                floorStoneMaterial.GetTextureOffset("_BaseMap"),
                Is.EqualTo(Vector2.zero),
                "The 30 m planar UV owns the macro texture phase.");

            Texture floorMacroNormal = floorStoneMaterial.GetTexture("_BumpMap");
            Texture floorDetailNormal = floorStoneMaterial.GetTexture(
                "_DetailNormalMap");
            Assert.That(
                floorMacroNormal,
                Is.Not.Null,
                "The complete carved-floor composition needs a phase-aligned " +
                "macro normal for joints and inlays.");
            Assert.That(
                floorMacroNormal.name,
                Does.Contain("ArenaFloorMacroNormal"),
                "The base normal must be an authored tangent-space map, not an " +
                "albedo copy converted as height during import.");
            Assert.That(
                floorStoneMaterial.GetTextureScale("_BumpMap"),
                Is.EqualTo(Vector2.one));
            Assert.That(
                floorStoneMaterial.GetTextureOffset("_BumpMap"),
                Is.EqualTo(Vector2.zero));
            Assert.That(
                floorStoneMaterial.GetFloat("_BumpScale"),
                Is.EqualTo(0.78f).Within(0.001f));
            Assert.That(floorDetailNormal, Is.Not.Null);
            Assert.That(
                floorDetailNormal.name,
                Does.Contain("ArenaFloorSurfaceNormal"));
            Texture floorMacroMask =
                floorStoneMaterial.GetTexture("_MetallicGlossMap");
            Assert.That(floorMacroMask, Is.Not.Null);
            Assert.That(
                floorMacroMask.name,
                Does.Contain("ArenaFloorMacroMask"),
                "Metallic, ambient occlusion, and smoothness must use the " +
                "single-mapped full-floor phase bake.");
            Assert.That(
                floorStoneMaterial.GetTexture("_OcclusionMap"),
                Is.SameAs(floorMacroMask));
            Assert.That(
                floorStoneMaterial.GetTextureScale("_MetallicGlossMap"),
                Is.EqualTo(Vector2.one));
            Assert.That(
                floorStoneMaterial.GetTextureOffset("_MetallicGlossMap"),
                Is.EqualTo(Vector2.zero));
            Assert.That(
                floorStoneMaterial.GetTextureScale("_OcclusionMap"),
                Is.EqualTo(Vector2.one));
            Assert.That(
                floorStoneMaterial.GetTextureOffset("_OcclusionMap"),
                Is.EqualTo(Vector2.zero));
            Assert.That(
                floorStoneMaterial.GetTextureScale("_DetailAlbedoMap"),
                Is.EqualTo(Vector2.one * 4.6875f),
                "URP Lit's detail-albedo transform is the UV carrier shared by " +
                "the independently tiled detail normal.");
            Assert.That(
                floorStoneMaterial.GetTextureOffset("_DetailAlbedoMap"),
                Is.EqualTo(new Vector2(0.47125f, 0.64125f)));
            Assert.That(
                floorStoneMaterial.GetTexture("_DetailAlbedoMap"),
                Is.Not.Null,
                "The bounded micro albedo must break up broad macro stone without " +
                "changing the carved full-floor composition.");
            Assert.That(
                floorStoneMaterial.GetTexture("_DetailAlbedoMap").name,
                Does.Contain("ArenaFloorSurfaceAlbedo"));
            Assert.That(
                floorStoneMaterial.GetFloat("_DetailAlbedoMapScale"),
                Is.EqualTo(0.055f).Within(0.001f));
            Assert.That(
                floorStoneMaterial.GetTextureScale("_DetailNormalMap"),
                Is.EqualTo(Vector2.one),
                "The URP shader ignores _DetailNormalMap_ST; keep it neutral so " +
                "the operative transform is unambiguous.");
            Assert.That(
                floorStoneMaterial.GetTextureOffset("_DetailNormalMap"),
                Is.EqualTo(Vector2.zero));
            Assert.That(
                floorStoneMaterial.IsKeywordEnabled("_NORMALMAP"),
                Is.True);
            Assert.That(
                floorStoneMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),
                Is.True);
            Assert.That(
                floorStoneMaterial.IsKeywordEnabled("_OCCLUSIONMAP"),
                Is.True);
            Assert.That(
                floorStoneMaterial.IsKeywordEnabled("_DETAIL_MULX2"),
                Is.False);
            Assert.That(
                floorStoneMaterial.IsKeywordEnabled("_DETAIL_SCALED"),
                Is.True,
                "The scaled detail variant must combine bounded albedo and normal " +
                "microstructure at one shared frequency.");
            Assert.That(
                floorStoneMaterial.IsKeywordEnabled(
                    "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"),
                Is.False,
                "Constant stone smoothness must never be replaced by macro " +
                "albedo alpha.");
            Assert.That(
                floorStoneMaterial.GetFloat("_DetailNormalMapScale"),
                Is.EqualTo(0.38f).Within(0.001f),
                "Micro relief must support rather than overpower the flagstone " +
                "value structure at strategy distance.");
            Assert.That(
                floorStoneMaterial.GetFloat("_SmoothnessTextureChannel"),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                floorStoneMaterial.GetFloat("_WorkflowMode"),
                Is.EqualTo(1f).Within(0.001f),
                "Keep the shared floor material on URP's stable metallic workflow.");
            Assert.That(
                floorStoneMaterial.GetFloat("_Smoothness"),
                Is.EqualTo(1f).Within(0.001f),
                "The smoothness scalar modulates the packed map's restrained " +
                "per-texel alpha response.");
            Assert.That(
                floorStoneMaterial.GetFloat("_OcclusionStrength"),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                floorRenderer.sharedMaterials[1].color.r,
                Is.EqualTo(0.34f).Within(0.001f),
                "Non-emissive carved trim needs a restrained bronze value break, not pale checkerboard slabs.");
            Color floorTrimColor = floorRenderer.sharedMaterials[1].color;
            Assert.That(floorTrimColor.r, Is.EqualTo(0.34f).Within(0.001f));
            Assert.That(floorTrimColor.g, Is.EqualTo(0.22f).Within(0.001f));
            Assert.That(floorTrimColor.b, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(floorTrimColor.a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                floorRenderer.sharedMaterials[1].GetFloat("_Metallic"),
                Is.EqualTo(0.34f).Within(0.001f),
                "Narrow inlays use a restrained metallic response to separate them from stone.");
            Assert.That(
                floorRenderer.sharedMaterials[1].GetFloat("_Smoothness"),
                Is.EqualTo(0.34f).Within(0.001f));
            Assert.That(
                floorRenderer.sharedMaterials[1].IsKeywordEnabled("_EMISSION"),
                Is.False,
                "Carved trim must not become another glowing gameplay ring.");
            Color floorGroutColor = floorRenderer.sharedMaterials[2].color;
            Assert.That(floorGroutColor.r, Is.EqualTo(0.018f).Within(0.001f));
            Assert.That(floorGroutColor.g, Is.EqualTo(0.022f).Within(0.001f));
            Assert.That(floorGroutColor.b, Is.EqualTo(0.03f).Within(0.001f));
            Assert.That(floorGroutColor.a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                floorRenderer.sharedMaterials[2].GetFloat("_Smoothness"),
                Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(
                floorRenderer.sharedMaterials[2].IsKeywordEnabled("_EMISSION"),
                Is.False,
                "Recessed grout must remain a neutral material.");

            Mesh floorMesh = floorFilters[0].sharedMesh;
            Assert.That(floorMesh.subMeshCount, Is.EqualTo(3));
            Assert.That(
                CountTriangles(floorFilters),
                Is.LessThanOrEqualTo(28000),
                "The one-renderer source floor must stay inside its mobile budget.");
            Assert.That(
                floorMesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.Normal),
                Is.True,
                "The imported floor must retain its authored bevel normals.");
            Assert.That(
                floorMesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.Tangent),
                Is.True,
                "The imported floor needs MikkTSpace tangents for its URP normal map.");
            Assert.That(
                floorMesh.isReadable,
                Is.False,
                "The static floor must keep Read/Write disabled for the mobile memory budget.");
            Assert.That(
                floorRenderer.bounds.max.y,
                Is.EqualTo(0f).Within(0.015f),
                "The visual may not create false elevation above the combat plane.");
            Assert.That(
                floorRenderer.bounds.min.y,
                Is.EqualTo(-0.12f).Within(0.015f));
            Assert.That(
                floorRenderer.bounds.size.x,
                Is.EqualTo(ArenaLayout.SafeRadius * 2f).Within(0.02f));
            Assert.That(
                floorRenderer.bounds.size.z,
                Is.EqualTo(ArenaLayout.SafeRadius * 2f).Within(0.02f));
            Assert.That(GameObject.Find("ArenaFloorVisual_Underlay"), Is.Null);
            Assert.That(GameObject.Find("ArenaFloorVisual_Etching"), Is.Null);
            Assert.That(GameObject.Find("CentralInlay"), Is.Null);
            Assert.That(
                safeStone.GetComponent<Renderer>().enabled,
                Is.False,
                "SafeStone remains the exact invisible collider under the source visual.");

            Transform architecturalDepth = presentation.VisualRoot.Find(
                "ArenaArchitecturalDepth");
            Assert.That(
                architecturalDepth,
                Is.Not.Null,
                "The arena needs an integrated presentation-only curb and low " +
                "far-side rubble silhouette outside the quiet combat floor.");
            Assert.That(architecturalDepth.childCount, Is.EqualTo(3));
            Assert.That(
                architecturalDepth.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Architectural depth may never become authoritative collision.");
            Assert.That(
                architecturalDepth.GetComponentsInChildren<Light>(true),
                Is.Empty,
                "The stonework must reuse the bounded fortress lighting budget.");
            Assert.That(
                architecturalDepth.GetComponentsInChildren<Transform>(true)
                    .All(value => value.gameObject.layer == 2),
                Is.True,
                "Every boundary-detail transform must remain on Ignore Raycast.");
            MeshRenderer[] depthRenderers =
                architecturalDepth.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] depthFilters =
                architecturalDepth.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(depthRenderers, Has.Length.EqualTo(3));
            Assert.That(depthFilters, Has.Length.EqualTo(3));
            Transform boundaryStonework =
                architecturalDepth.Find("BoundaryStonework");
            Assert.That(boundaryStonework, Is.Not.Null);
            MeshRenderer depthRenderer =
                boundaryStonework.GetComponent<MeshRenderer>();
            Mesh depthMesh = boundaryStonework.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(depthMesh, Is.Not.Null);
            Assert.That(depthMesh.name, Is.EqualTo("ArenaBoundaryStonework"));
            Assert.That(depthMesh.subMeshCount, Is.EqualTo(3));
            Assert.That(
                depthMesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.Tangent),
                Is.True,
                "The project-native masonry detail normal needs generated tangents.");
            Assert.That(
                CountTriangles(new[] { boundaryStonework.GetComponent<MeshFilter>() }),
                Is.InRange(500, 900),
                "The combined depth pass should stay below one thousand triangles.");
            Assert.That(depthRenderer.sharedMaterials, Has.Length.EqualTo(3));
            Assert.That(
                depthRenderer.sharedMaterials[0].name,
                Does.Contain("RuinStone"));
            Assert.That(
                depthRenderer.sharedMaterials[1].name,
                Does.Contain("ArenaFloor_Grout"));
            Assert.That(
                depthRenderer.sharedMaterials[2].name,
                Does.Contain("LavaSeam"));
            Assert.That(
                depthRenderer.shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.On));
            Assert.That(depthRenderer.receiveShadows, Is.True);
            Assert.That(
                depthRenderer.reflectionProbeUsage,
                Is.EqualTo(ReflectionProbeUsage.Simple));
            Assert.That(
                depthRenderer.bounds.min.y,
                Is.InRange(-0.61f, -0.55f),
                "The narrow curb bevel and perimeter rubble must seat into the " +
                "foundation instead of floating as walkable lava platforms.");
            Assert.That(
                depthRenderer.bounds.max.y,
                Is.InRange(1.25f, 1.5f),
                "Far-side reliquary columns need depth without obscuring combatants.");

            Transform outerCrust = architecturalDepth.Find("OuterBasaltCrust");
            Assert.That(outerCrust, Is.Not.Null);
            MeshFilter outerCrustFilter = outerCrust.GetComponent<MeshFilter>();
            MeshRenderer outerCrustRenderer = outerCrust.GetComponent<MeshRenderer>();
            Assert.That(outerCrustFilter.sharedMesh.name, Is.EqualTo("ArenaOuterBasaltCrust"));
            Assert.That(
                CountTriangles(new[] { outerCrustFilter }),
                Is.EqualTo(612));
            Assert.That(outerCrustRenderer.sharedMaterials, Has.Length.EqualTo(2));
            Assert.That(
                outerCrustRenderer.shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(outerCrustRenderer.receiveShadows, Is.True);
            Assert.That(
                outerCrustRenderer.reflectionProbeUsage,
                Is.EqualTo(ReflectionProbeUsage.Simple));

            Transform flankSilhouettes =
                architecturalDepth.Find("FlankSilhouettes");
            Assert.That(flankSilhouettes, Is.Not.Null);
            MeshFilter flankFilter = flankSilhouettes.GetComponent<MeshFilter>();
            MeshRenderer flankRenderer =
                flankSilhouettes.GetComponent<MeshRenderer>();
            Assert.That(flankFilter.sharedMesh.name, Is.EqualTo("ArenaFlankSilhouettes"));
            Assert.That(CountTriangles(new[] { flankFilter }), Is.EqualTo(660));
            Assert.That(flankFilter.sharedMesh.subMeshCount, Is.EqualTo(3));
            Assert.That(flankRenderer.sharedMaterials, Has.Length.EqualTo(3));
            Assert.That(
                flankRenderer.sharedMaterials[0].name,
                Does.Contain("RuinStone"));
            Assert.That(
                flankRenderer.sharedMaterials[1].name,
                Does.Contain("ArenaFloor_Grout"));
            Assert.That(
                flankRenderer.sharedMaterials[2].name,
                Does.Contain("LavaSeam"));
            Assert.That(
                flankRenderer.shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off),
                "The framing silhouettes may receive the key but cannot cast an " +
                "arena-wide shadow.");
            Assert.That(flankRenderer.receiveShadows, Is.True);
            Assert.That(
                flankRenderer.reflectionProbeUsage,
                Is.EqualTo(ReflectionProbeUsage.Simple));
            Assert.That(
                presentation.GameplayProxyRoot.Find("ArenaArchitecturalDepth"),
                Is.Null,
                "The generated gameplay hierarchy may contain only simulation proxies.");

            GameObject perimeter = GameObject.Find("ArenaPerimeterDressing");
            Assert.That(perimeter, Is.Not.Null);
            Assert.That(perimeter.GetComponentsInChildren<Collider>(true), Is.Empty);
            MeshRenderer[] perimeterRenderers =
                perimeter.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(perimeterRenderers, Has.Length.EqualTo(23));
            Assert.That(perimeter.transform.childCount, Is.EqualTo(20));
            Assert.That(
                perimeterRenderers.Sum(value => value.sharedMaterials.Length),
                Is.EqualTo(33),
                "The bounded fortress budget is one foundation draw, twelve " +
                "walls, six ember towers, and four two-submesh fire anchors.");
            Assert.That(
                perimeterRenderers.All(value =>
                    value.receiveShadows &&
                    value.sharedMaterials.All(material =>
                        material != null &&
                        (material.name.Contains("RuinStone") ||
                         material.name.Contains("LavaSeam")))),
                Is.True,
                "Original fortress modules may reuse only RuinStone and LavaSeam.");
            HashSet<Material> perimeterMaterials = perimeterRenderers
                .SelectMany(value => value.sharedMaterials)
                .ToHashSet();
            Assert.That(perimeterMaterials, Has.Count.EqualTo(2));
            Assert.That(
                perimeterMaterials.Any(value => value.name.Contains("RuinStone")),
                Is.True);
            Assert.That(
                perimeterMaterials.Any(value => value.name.Contains("LavaSeam")),
                Is.True);
            Assert.That(
                perimeterMaterials.Single(value => value.name.Contains("LavaSeam"))
                    .shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"),
                "Integrated tower embers need both directional surface response " +
                "and controlled emission in shadowed bays.");
            ParticleSystemRenderer[] perimeterParticleRenderers =
                perimeter.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Assert.That(perimeterParticleRenderers, Has.Length.EqualTo(12));
            Assert.That(
                perimeterParticleRenderers.All(value =>
                    value.sharedMaterial != null &&
                    value.sharedMaterial.name.Contains("CombatParticle")),
                Is.True,
                "All three layers of the four bounded brazier flames must share " +
                "the existing combat particle material.");

            Transform foundation = perimeter.transform.Find(
                "FortressFoundationRing");
            Assert.That(foundation, Is.Not.Null);
            Assert.That(
                foundation.GetSiblingIndex(),
                Is.EqualTo(0),
                "The recessed foundation must be authored before the repeated modules.");
            Assert.That(foundation.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(
                Quaternion.Angle(
                    foundation.localRotation,
                    Quaternion.identity),
                Is.LessThan(0.001f));
            Assert.That(foundation.localScale, Is.EqualTo(Vector3.one));
            Assert.That(
                foundation.GetComponentsInChildren<Transform>(true)
                    .All(value => value.gameObject.layer == 2),
                Is.True,
                "The foundation is visual-only Ignore Raycast dressing.");
            Assert.That(
                foundation.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            MeshRenderer[] foundationRenderers =
                foundation.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] foundationFilters =
                foundation.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(foundationRenderers, Has.Length.EqualTo(1));
            Assert.That(foundationFilters, Has.Length.EqualTo(1));
            Assert.That(foundationFilters[0].sharedMesh, Is.Not.Null);
            Assert.That(foundationFilters[0].sharedMesh.subMeshCount, Is.EqualTo(1));
            Assert.That(
                CountTriangles(foundationFilters),
                Is.InRange(1, 8000),
                "The continuous foundation must remain one bounded mobile draw.");
            MeshRenderer foundationRenderer = foundationRenderers[0];
            Assert.That(foundationRenderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(
                foundationRenderer.sharedMaterial.name,
                Does.Contain("RuinStone"));
            Assert.That(
                foundationRenderer.shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.On));
            Assert.That(foundationRenderer.receiveShadows, Is.True);
            Assert.That(
                foundationRenderer.bounds.min.y,
                Is.EqualTo(-1.8f).Within(0.03f));
            Assert.That(
                foundationRenderer.bounds.max.y,
                Is.EqualTo(-0.45f).Within(0.03f));
            Assert.That(
                foundationRenderer.bounds.size.x,
                Is.InRange(50.7f, 50.9f));
            Assert.That(
                foundationRenderer.bounds.size.z,
                Is.InRange(50.4f, 50.65f));
            Transform gameplayProxies = GameObject.Find("GameplayProxies").transform;
            Assert.That(
                gameplayProxies.GetComponentsInChildren<Transform>(true)
                    .Any(value => value.name == "FortressFoundationRing"),
                Is.False,
                "Presentation foundations may not create gameplay geometry.");

            int towerCount = 0;
            int straightWallCount = 0;
            int brokenWallCount = 0;
            HashSet<Color> bayStoneTints = new HashSet<Color>();
            HashSet<Vector2> bayTextureOffsets = new HashSet<Vector2>();
            for (int index = 0; index < 18; index++)
            {
                Transform bay = perimeter.transform.Find(
                    "FortressBay_" + (index + 1).ToString("00"));
                Assert.That(bay, Is.Not.Null);
                Vector3 position = bay.localPosition;
                float planarRadius = new Vector2(position.x, position.z).magnitude;
                float expectedAngle = 18f + (index * 20f);
                Vector3 expectedBayPosition =
                    Quaternion.Euler(0f, expectedAngle, 0f) *
                    new Vector3(0f, 0f, 21.65f);
                Assert.That(bay.name, Does.StartWith("FortressBay_"));
                Assert.That(
                    Vector3.Distance(position, expectedBayPosition),
                    Is.LessThan(0.001f),
                    bay.name +
                    " must keep the capture-reviewed shoulder rotation.");
                Assert.That(bay.childCount, Is.EqualTo(1));
                Transform module = bay.GetChild(0);
                switch (module.name)
                {
                    case "FortressTower":
                        towerCount++;
                        Assert.That(
                            position.x,
                            Is.GreaterThan(0f),
                            "Tall towers must stay on the far/+X half of the strategy view.");
                        break;

                    case "FortressWallStraight":
                        straightWallCount++;
                        break;

                    case "FortressWallBroken":
                        brokenWallCount++;
                        break;

                    default:
                        Assert.Fail(
                            "Unexpected fortress module '" + module.name + "'.");
                        break;
                }

                if (position.x <= 0f)
                {
                    Assert.That(
                        module.name,
                        Does.StartWith("FortressWall"),
                        bay.name + " is on the near half and must retain a low sightline.");
                }

                float expectedSink;
                if (module.name == "FortressTower")
                {
                    expectedSink = bay.name == "FortressBay_02" ||
                        bay.name == "FortressBay_08"
                            ? -0.78f
                            : -0.72f;
                }
                else
                {
                    expectedSink = -0.72f + (((index * 5) % 3) * -0.06f);
                }

                Vector3 expectedModulePosition =
                    new Vector3(
                        0f,
                        expectedSink,
                        module.name == "FortressTower" ? 0.7f : 0.4f);
                Assert.That(module.localPosition, Is.EqualTo(expectedModulePosition));
                Assert.That(
                    module.localScale,
                    Is.EqualTo(new Vector3(
                        1f,
                        ExpectedFortressVerticalScale(index, module.name),
                        1f)),
                    bay.name +
                    " must keep its chord width while using the reviewed vertical silhouette.");
                Assert.That(
                    module.GetComponentsInChildren<Transform>(true)
                        .All(value => value.gameObject.layer == 2),
                    Is.True);
                MeshRenderer moduleRenderer =
                    module.GetComponentInChildren<MeshRenderer>(true);
                Assert.That(moduleRenderer, Is.Not.Null);
                FortressSurfaceVariationPresenter surfaceVariation = module
                    .GetComponentInChildren<FortressSurfaceVariationPresenter>(true);
                Assert.That(
                    surfaceVariation,
                    Is.Not.Null,
                    bay.name + " needs deterministic material variation without " +
                    "creating a material instance.");
                Assert.That(
                    surfaceVariation.Surface,
                    Is.SameAs(moduleRenderer),
                    bay.name + " variation must target its only authored renderer.");
                Assert.That(
                    surfaceVariation.StoneMaterialIndex,
                    Is.Zero,
                    bay.name + " variation may affect only the stone material slot.");
                Color surfaceTint = surfaceVariation.SurfaceTint;
                if (module.name == "FortressTower")
                {
                    Assert.That(surfaceTint.r, Is.InRange(0.729f, 0.841f), bay.name);
                    Assert.That(surfaceTint.g, Is.InRange(0.63f, 0.7f), bay.name);
                    Assert.That(surfaceTint.b, Is.InRange(0.51f, 0.59f), bay.name);
                    Assert.That(surfaceTint.r, Is.GreaterThan(surfaceTint.g), bay.name);
                    Assert.That(surfaceTint.g, Is.GreaterThan(surfaceTint.b), bay.name);
                }
                else
                {
                    Assert.That(surfaceTint.r, Is.InRange(0.729f, 0.841f), bay.name);
                    Assert.That(surfaceTint.g, Is.InRange(0.72f, 0.82f), bay.name);
                    Assert.That(surfaceTint.b, Is.InRange(0.71f, 0.81f), bay.name);
                }
                Assert.That(surfaceTint.a, Is.EqualTo(1f).Within(0.001f), bay.name);
                Assert.That(
                    surfaceTint,
                    Is.Not.EqualTo(Color.white),
                    bay.name + " must not retain a neutral white property-block tint.");
                Vector2 textureOffset = surfaceVariation.TextureOffset;
                Assert.That(textureOffset.x, Is.GreaterThanOrEqualTo(0f), bay.name);
                Assert.That(textureOffset.x, Is.LessThan(1f), bay.name);
                Assert.That(textureOffset.y, Is.GreaterThanOrEqualTo(0f), bay.name);
                Assert.That(textureOffset.y, Is.LessThan(1f), bay.name);
                bayStoneTints.Add(surfaceTint);
                bayTextureOffsets.Add(textureOffset);

                Material stoneMaterial = moduleRenderer.sharedMaterials[0];
                Vector2 expectedTextureScale =
                    stoneMaterial.GetTextureScale("_BaseMap");
                Vector2 expectedTextureOffset =
                    stoneMaterial.GetTextureOffset("_BaseMap") + textureOffset;
                MaterialPropertyBlock stoneVariationProperties =
                    new MaterialPropertyBlock();
                moduleRenderer.GetPropertyBlock(stoneVariationProperties, 0);
                Assert.That(
                    stoneVariationProperties.isEmpty,
                    Is.False,
                    bay.name + " must apply its authored variation at runtime.");
                Color appliedSurfaceTint =
                    stoneVariationProperties.GetColor("_BaseColor");
                Assert.That(
                    appliedSurfaceTint.r,
                    Is.EqualTo(surfaceTint.r).Within(0.001f),
                    bay.name);
                Assert.That(
                    appliedSurfaceTint.g,
                    Is.EqualTo(surfaceTint.g).Within(0.001f),
                    bay.name);
                Assert.That(
                    appliedSurfaceTint.b,
                    Is.EqualTo(surfaceTint.b).Within(0.001f),
                    bay.name);
                Assert.That(
                    appliedSurfaceTint.a,
                    Is.EqualTo(surfaceTint.a).Within(0.001f),
                    bay.name);
                Vector4 baseMapTransform =
                    stoneVariationProperties.GetVector("_BaseMap_ST");
                Assert.That(
                    baseMapTransform.x,
                    Is.EqualTo(expectedTextureScale.x).Within(0.001f),
                    bay.name);
                Assert.That(
                    baseMapTransform.y,
                    Is.EqualTo(expectedTextureScale.y).Within(0.001f),
                    bay.name);
                Assert.That(
                    baseMapTransform.z,
                    Is.EqualTo(expectedTextureOffset.x).Within(0.001f),
                    bay.name);
                Assert.That(
                    baseMapTransform.w,
                    Is.EqualTo(expectedTextureOffset.y).Within(0.001f),
                    bay.name);

                FortressFireAnchorPresenter bayFire = module
                    .GetComponentInChildren<FortressFireAnchorPresenter>(true);
                if (module.name == "FortressTower")
                {
                    Assert.That(
                        bayFire,
                        Is.Not.Null,
                        bay.name + " needs a restrained asynchronous ember pulse.");
                    Assert.That(bayFire.Surface, Is.SameAs(moduleRenderer));
                    Assert.That(bayFire.EmberMaterialIndex, Is.EqualTo(1));
                    Assert.That(
                        bayFire.EmissionMultiplier,
                        Is.EqualTo(index == 2 || index == 5 ? 2.35f : 1.5f)
                            .Within(0.001f),
                        bay.name +
                        " must preserve the two showcased tower flames without " +
                        "making every repeated ember equally loud.");
                }
                else
                {
                    Assert.That(
                        bayFire,
                        Is.Null,
                        bay.name + " has no ember slot to animate.");
                }
                Assert.That(
                    moduleRenderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.On));
                Assert.That(
                    moduleRenderer.reflectionProbeUsage,
                    Is.EqualTo(ReflectionProbeUsage.Simple),
                    bay.name + " must sample the shared authored reflection.");
                Assert.That(
                    moduleRenderer.bounds.min.y,
                    Is.LessThanOrEqualTo(-0.71f),
                    bay.name + " must be seated at or below the authored lava plane.");
                if (position.x <= 0f)
                {
                    Assert.That(
                        moduleRenderer.bounds.max.y,
                        Is.LessThanOrEqualTo(1.25f),
                        bay.name +
                        " is on the near arc and may not become a cropped wall panel.");
                }

                Assert.That(
                    MinimumPlanarBoundsRadius(moduleRenderer),
                    Is.GreaterThanOrEqualTo(21.25f),
                    module.name + " must remain visibly beyond the lava kill edge.");
                float modulePlanarRadius = new Vector2(
                    module.position.x,
                    module.position.z).magnitude;
                Assert.That(
                    modulePlanarRadius,
                    Is.GreaterThan(ArenaLayout.OuterKillRadius + 1.5f),
                    bay.name + " must leave lava visibly separating combat from architecture.");
                Assert.That(modulePlanarRadius, Is.LessThan(23f));
            }

            Assert.That(towerCount, Is.EqualTo(6));
            Assert.That(straightWallCount, Is.EqualTo(6));
            Assert.That(brokenWallCount, Is.EqualTo(6));
            Assert.That(
                bayStoneTints.Count,
                Is.GreaterThanOrEqualTo(4),
                "The eighteen-bay ring needs several restrained stone values, " +
                "not one repeated property block.");
            Assert.That(
                bayTextureOffsets.Count,
                Is.GreaterThanOrEqualTo(6),
                "The eighteen-bay ring needs several deterministic masonry phases.");

            int visibleTowerCount = 0;
            for (int index = 0; index < perimeter.transform.childCount; index++)
            {
                Transform bay = perimeter.transform.GetChild(index);
                Transform tower = bay.Find("FortressTower");
                if (tower == null)
                {
                    continue;
                }

                Bounds bounds = tower.GetComponentInChildren<MeshRenderer>(true).bounds;
                if (BoundsIntersectsViewport(camera, bounds))
                {
                    visibleTowerCount++;
                }
            }

            Assert.That(
                visibleTowerCount,
                Is.GreaterThanOrEqualTo(2),
                "The authored strategy frame must retain at least two readable fortress towers.");

            Transform fireAnchors = perimeter.transform.Find("ArenaBraziers");
            Assert.That(fireAnchors, Is.Not.Null);
            Assert.That(fireAnchors.childCount, Is.EqualTo(4));
            Assert.That(fireAnchors.GetComponentsInChildren<Collider>(true), Is.Empty);
            string[] fireAnchorNames =
            {
                "FortressBrazier_LeftFlank",
                "FortressBrazier_LeftShoulder",
                "FortressBrazier_RightShoulder",
                "FortressBrazier_RightFlank",
            };
            float[] fireAnchorRadii = { 22.1f, 22f, 22f, 22.1f };
            float[] fireAnchorAngles = { 30f, 54f, 126f, 150f };
            for (int index = 0; index < fireAnchorNames.Length; index++)
            {
                Transform fireAnchor = fireAnchors.Find(fireAnchorNames[index]);
                Assert.That(fireAnchor, Is.Not.Null);
                Assert.That(
                    fireAnchor.localScale,
                    Is.EqualTo(new Vector3(0.9f, 1.37f, 0.9f)),
                    "A narrow planar footprint keeps the complete tall brazier " +
                    "inside the gameplay frame and outside the kill boundary.");
                Vector3 expectedPosition =
                    Quaternion.Euler(0f, fireAnchorAngles[index], 0f) *
                    new Vector3(0f, 0f, fireAnchorRadii[index]);
                expectedPosition.y = -0.9f;
                Assert.That(
                    Vector3.Distance(fireAnchor.localPosition, expectedPosition),
                    Is.LessThan(0.001f),
                    fireAnchorNames[index] +
                    " must retain its authored radius and angular landmark slot.");
                Assert.That(fireAnchor.localPosition.y, Is.EqualTo(-0.9f).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        fireAnchor.localRotation,
                        Quaternion.Euler(0f, fireAnchorAngles[index] + 180f, 0f)),
                    Is.LessThan(0.001f),
                    fireAnchorNames[index]);
                MeshRenderer fireRenderer =
                    fireAnchor.GetComponentInChildren<MeshRenderer>(true);
                Assert.That(fireRenderer, Is.Not.Null);
                Assert.That(fireRenderer.sharedMaterials, Has.Length.EqualTo(2));
                Assert.That(
                    fireRenderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.On),
                    "Brazier bodies need contact and silhouette shadows under the " +
                    "single directional key; their point lights remain unshadowed.");
                Assert.That(fireRenderer.receiveShadows, Is.True);
                Assert.That(
                    fireRenderer.reflectionProbeUsage,
                    Is.EqualTo(ReflectionProbeUsage.Simple));
                Assert.That(
                    MinimumPlanarBoundsRadius(fireRenderer),
                    Is.GreaterThanOrEqualTo(21.25f),
                    fireAnchorNames[index] +
                    " must stay wholly outside the recovery and kill space.");
                Assert.That(
                    BoundsIntersectsViewport(camera, fireRenderer.bounds),
                    Is.True,
                    fireAnchorNames[index] +
                    " must retain a readable fragment in the target-wide authored frame. " +
                    "Viewport center=" +
                    camera.WorldToViewportPoint(fireRenderer.bounds.center) +
                    ", camera aspect=" + camera.aspect.ToString("0.###") + ".");
                Vector3 fireViewportCenter = camera.WorldToViewportPoint(
                    fireRenderer.bounds.center);
                Assert.That(fireViewportCenter.z, Is.GreaterThan(0f));
                Assert.That(
                    fireViewportCenter.y,
                    Is.InRange(0.55f, 1.08f),
                    fireAnchorNames[index] +
                    " must remain a high perimeter landmark outside the central combat read.");
                if (index < 2)
                {
                    Assert.That(
                        fireViewportCenter.x,
                        Is.LessThanOrEqualTo(0.25f),
                        fireAnchorNames[index] +
                        " must frame the left margin of the wider strategy view.");
                }
                else
                {
                    Assert.That(
                        fireViewportCenter.x,
                        Is.GreaterThanOrEqualTo(0.62f),
                        fireAnchorNames[index] +
                        " must frame the right margin of the wider strategy view.");
                }

                Assert.That(
                    fireRenderer.bounds.size.y,
                    Is.GreaterThanOrEqualTo(3.25f),
                    "The fire landmark must remain legible above the wall shoulder.");

                FortressSurfaceVariationPresenter surfaceVariation =
                    fireAnchor.GetComponent<FortressSurfaceVariationPresenter>();
                Assert.That(surfaceVariation, Is.Not.Null, fireAnchorNames[index]);
                Assert.That(surfaceVariation.Surface, Is.SameAs(fireRenderer));
                Assert.That(surfaceVariation.StoneMaterialIndex, Is.Zero);
                Color surfaceTint = surfaceVariation.SurfaceTint;
                Assert.That(surfaceTint.r, Is.InRange(0.729f, 0.841f));
                Assert.That(surfaceTint.g, Is.InRange(0.63f, 0.7f));
                Assert.That(surfaceTint.b, Is.InRange(0.51f, 0.59f));
                Assert.That(surfaceTint.r, Is.GreaterThan(surfaceTint.g));
                Assert.That(surfaceTint.g, Is.GreaterThan(surfaceTint.b));

                FortressFireAnchorPresenter firePresenter =
                    fireAnchor.GetComponent<FortressFireAnchorPresenter>();
                Assert.That(firePresenter, Is.Not.Null);
                Assert.That(firePresenter.Surface, Is.SameAs(fireRenderer));
                Assert.That(firePresenter.EmberMaterialIndex, Is.EqualTo(1));
                Assert.That(
                    firePresenter.EmissionMultiplier,
                    Is.EqualTo(2.4f).Within(0.001f));
                MaterialPropertyBlock emberProperties = new MaterialPropertyBlock();
                fireRenderer.GetPropertyBlock(emberProperties, 1);
                Color emberColor = emberProperties.GetColor("_BaseColor");
                Color emberEmission = emberProperties.GetColor("_EmissionColor");
                Assert.That(emberColor.r, Is.GreaterThanOrEqualTo(0.95f));
                Assert.That(emberColor.g, Is.InRange(0.25f, 0.72f));
                Assert.That(emberColor.b, Is.LessThanOrEqualTo(0.12f));
                Assert.That(
                    emberEmission.r,
                    Is.EqualTo(emberColor.r * 2.4f).Within(0.001f));
                Assert.That(
                    emberEmission.g,
                    Is.EqualTo(emberColor.g * 2.4f).Within(0.001f));
                Assert.That(
                    emberEmission.b,
                    Is.EqualTo(emberColor.b * 2.4f).Within(0.001f));
                Assert.That(emberEmission.a, Is.EqualTo(emberColor.a).Within(0.001f));
                MaterialPropertyBlock stoneProperties = new MaterialPropertyBlock();
                fireRenderer.GetPropertyBlock(stoneProperties, 0);
                Assert.That(
                    stoneProperties.isEmpty,
                    Is.False,
                    "Warm fire structures need their deterministic stone variation.");
                Color appliedFireStoneTint =
                    stoneProperties.GetColor("_BaseColor");
                Assert.That(
                    appliedFireStoneTint.r,
                    Is.EqualTo(surfaceTint.r).Within(0.001f));
                Assert.That(
                    appliedFireStoneTint.g,
                    Is.EqualTo(surfaceTint.g).Within(0.001f));
                Assert.That(
                    appliedFireStoneTint.b,
                    Is.EqualTo(surfaceTint.b).Within(0.001f));
                Assert.That(
                    appliedFireStoneTint.a,
                    Is.EqualTo(surfaceTint.a).Within(0.001f));

                Transform firePlume = fireAnchor.Find("FirePlume");
                Assert.That(firePlume, Is.Not.Null, fireAnchorNames[index]);
                Assert.That(
                    firePlume.localPosition,
                    Is.EqualTo(new Vector3(0f, 2.35f, 0f)));
                Assert.That(firePlume.gameObject.layer, Is.EqualTo(2));
                ParticleSystem particles = firePlume.GetComponent<ParticleSystem>();
                Assert.That(particles, Is.Not.Null, fireAnchorNames[index]);
                Assert.That(particles.useAutoRandomSeed, Is.False);
                Assert.That(particles.main.maxParticles, Is.EqualTo(16));
                Assert.That(particles.main.playOnAwake, Is.True);
                Assert.That(particles.main.loop, Is.True);
                Assert.That(
                    particles.main.startLifetime.constantMin,
                    Is.EqualTo(0.55f).Within(0.001f));
                Assert.That(
                    particles.main.startLifetime.constantMax,
                    Is.EqualTo(1.05f).Within(0.001f));
                Assert.That(
                    particles.main.startSize.constantMin,
                    Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(
                    particles.main.startSize.constantMax,
                    Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(
                    particles.emission.rateOverTime.constant,
                    Is.EqualTo(18f).Within(0.001f));
                Assert.That(
                    particles.main.simulationSpace,
                    Is.EqualTo(ParticleSystemSimulationSpace.Local));
                Assert.That(
                    particles.collision.enabled,
                    Is.False,
                    "Decorative flames may not participate in gameplay or particle collision.");
                ParticleSystemRenderer particleRenderer =
                    firePlume.GetComponent<ParticleSystemRenderer>();
                Assert.That(particleRenderer, Is.Not.Null);
                Assert.That(
                    particleRenderer.renderMode,
                    Is.EqualTo(ParticleSystemRenderMode.Billboard));
                Assert.That(
                    particleRenderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(particleRenderer.receiveShadows, Is.False);

                Transform fireCore = fireAnchor.Find("FireCore");
                Assert.That(fireCore, Is.Not.Null, fireAnchorNames[index]);
                ParticleSystem coreParticles =
                    fireCore.GetComponent<ParticleSystem>();
                Assert.That(coreParticles.useAutoRandomSeed, Is.False);
                Assert.That(coreParticles.main.maxParticles, Is.EqualTo(10));
                Assert.That(coreParticles.main.playOnAwake, Is.True);
                Assert.That(coreParticles.main.loop, Is.True);
                Assert.That(
                    coreParticles.main.simulationSpace,
                    Is.EqualTo(ParticleSystemSimulationSpace.Local));
                Assert.That(coreParticles.collision.enabled, Is.False);
                ParticleSystemRenderer coreRenderer =
                    fireCore.GetComponent<ParticleSystemRenderer>();
                Assert.That(
                    coreRenderer.renderMode,
                    Is.EqualTo(ParticleSystemRenderMode.Billboard));
                Assert.That(
                    coreRenderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(coreRenderer.receiveShadows, Is.False);

                Transform emberSparks = fireAnchor.Find("EmberSparks");
                Assert.That(emberSparks, Is.Not.Null, fireAnchorNames[index]);
                ParticleSystem sparkParticles =
                    emberSparks.GetComponent<ParticleSystem>();
                Assert.That(sparkParticles.useAutoRandomSeed, Is.False);
                Assert.That(sparkParticles.main.maxParticles, Is.EqualTo(12));
                Assert.That(sparkParticles.main.playOnAwake, Is.True);
                Assert.That(sparkParticles.main.loop, Is.True);
                Assert.That(
                    sparkParticles.main.simulationSpace,
                    Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(sparkParticles.collision.enabled, Is.False);
                ParticleSystemRenderer sparkRenderer =
                    emberSparks.GetComponent<ParticleSystemRenderer>();
                Assert.That(
                    sparkRenderer.renderMode,
                    Is.EqualTo(ParticleSystemRenderMode.Stretch));
                Assert.That(
                    sparkRenderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(sparkRenderer.receiveShadows, Is.False);
            }

            Assert.That(
                fireAnchors.GetComponentsInChildren<ParticleSystem>(true),
                Has.Length.EqualTo(12));

            Assert.That(
                CountTriangles(perimeter.GetComponentsInChildren<MeshFilter>(true)),
                Is.LessThanOrEqualTo(45000),
                "The instantiated LOD0 fortress ring must stay inside its mobile budget.");

            Material fortressStone = perimeterMaterials.Single(value =>
                value.name.Contains("RuinStone"));
            Material fortressEmber = perimeterMaterials.Single(value =>
                value.name.Contains("LavaSeam"));
            Assert.That(
                fortressStone.HasProperty("_BaseMap") &&
                fortressStone.GetTexture("_BaseMap") != null &&
                fortressStone.GetTexture("_BaseMap").name.Contains(
                    "stone_brick_wall_001_diff_2k"),
                Is.True,
                "Fortress stone must use the audited Poly Haven masonry diffuse " +
                "instead of the retired flat first-party placeholder.");
            Assert.That(
                fortressStone.color.maxColorComponent,
                Is.InRange(0.77f, 0.79f),
                "PBR masonry needs enough tint headroom to retain photographed " +
                "surface variation behind the combat floor.");
            Color fortressStoneColor = fortressStone.color;
            Assert.That(fortressStoneColor.r, Is.EqualTo(0.78f).Within(0.001f));
            Assert.That(fortressStoneColor.g, Is.EqualTo(0.76f).Within(0.001f));
            Assert.That(fortressStoneColor.b, Is.EqualTo(0.76f).Within(0.001f));
            Assert.That(fortressStoneColor.a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                fortressStone.GetFloat("_Smoothness"),
                Is.EqualTo(0.72f).Within(0.001f),
                "The scalar must preserve the authored roughness variation in the " +
                "packed mask rather than forcing a flat material response.");
            Texture2D fortressTexture = fortressStone.GetTexture("_BaseMap") as Texture2D;
            Assert.That(fortressTexture, Is.Not.Null);
            Assert.That(
                fortressTexture.anisoLevel,
                Is.GreaterThanOrEqualTo(4),
                "Fortress faces need bounded anisotropic filtering at the arena perimeter.");
            Assert.That(
                fortressStone.GetTextureScale("_BaseMap"),
                Is.EqualTo(Vector2.one * 2.65f),
                "The 2.5 m calibrated source must repeat at the authored fortress scale.");
            Assert.That(
                fortressStone.GetTextureOffset("_BaseMap"),
                Is.EqualTo(new Vector2(0.31f, 0.18f)));
            Texture2D fortressNormal = fortressStone.GetTexture("_BumpMap")
                as Texture2D;
            Assert.That(
                fortressNormal,
                Is.Not.Null,
                "The shared fortress material needs tangent-space masonry depth.");
            Assert.That(
                fortressNormal.name,
                Does.Contain("stone_brick_wall_001_nor_gl_2k"),
                "The normal must use the audited authored OpenGL/+Y source, not " +
                "Unity height conversion.");
            Assert.That(
                fortressStone.GetTextureScale("_BumpMap"),
                Is.EqualTo(fortressStone.GetTextureScale("_BaseMap")),
                "Masonry color and relief must use the same UV frequency.");
            Assert.That(
                fortressStone.GetTextureOffset("_BumpMap"),
                Is.EqualTo(fortressStone.GetTextureOffset("_BaseMap")),
                "Masonry color and relief must stay phase aligned.");
            Assert.That(
                fortressStone.GetFloat("_BumpScale"),
                Is.EqualTo(0.92f).Within(0.001f),
                "Relief must retain the source masonry and mortar response at " +
                "strategy distance.");
            Assert.That(
                fortressStone.IsKeywordEnabled("_NORMALMAP"),
                Is.True,
                "The shared URP Lit normal-map variant must remain enabled.");
            Texture fortressMask =
                fortressStone.GetTexture("_MetallicGlossMap");
            Assert.That(fortressMask, Is.Not.Null);
            Assert.That(
                fortressMask.name,
                Does.Contain("FortressMasonryMask"));
            Assert.That(
                fortressStone.GetTexture("_OcclusionMap"),
                Is.SameAs(fortressMask));
            Assert.That(
                fortressStone.GetTextureScale("_MetallicGlossMap"),
                Is.EqualTo(fortressStone.GetTextureScale("_BaseMap")));
            Assert.That(
                fortressStone.GetTextureOffset("_MetallicGlossMap"),
                Is.EqualTo(fortressStone.GetTextureOffset("_BaseMap")));
            Assert.That(
                fortressStone.GetTextureScale("_OcclusionMap"),
                Is.EqualTo(fortressStone.GetTextureScale("_BaseMap")));
            Assert.That(
                fortressStone.GetTextureOffset("_OcclusionMap"),
                Is.EqualTo(fortressStone.GetTextureOffset("_BaseMap")));
            Assert.That(
                fortressStone.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),
                Is.True);
            Assert.That(
                fortressStone.IsKeywordEnabled("_OCCLUSIONMAP"),
                Is.True);
            Assert.That(
                fortressStone.GetFloat("_OcclusionStrength"),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                fortressStone.IsKeywordEnabled("_EMISSION"),
                Is.False,
                "Rough fortress masonry must respond to the authored lights, not glow flat.");
            Assert.That(
                fortressStone.GetTexture("_EmissionMap"),
                Is.Null);
            Color fortressAmbient = fortressStone.GetColor("_EmissionColor");
            Assert.That(fortressAmbient, Is.EqualTo(Color.black));
            Assert.That(
                fortressEmber.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"),
                "Ember seams must receive surface lighting while retaining a " +
                "controlled emissive response.");
            Assert.That(fortressEmber.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(
                fortressEmber.color.r,
                Is.EqualTo(0.92f).Within(0.001f),
                "Lit tower and wardstone embers need strong red-orange separation from stone.");
            Assert.That(
                fortressEmber.color.r,
                Is.GreaterThan(fortressEmber.color.g * 2f));

            Assert.That(
                GameObject.Find("ArenaFactionLandmarks"),
                Is.Null,
                "Legacy KayKit banners obscure the authored fortress and strategy camera sightline.");

            Assert.That(
                GameObject.Find("ArenaChains"),
                Is.Null,
                "Legacy view-facing chains create clipped foreground shapes and are intentionally omitted.");

            GameObject southWardstone = GameObject.Find("ArenaPillar_South_Visual");
            Assert.That(southWardstone, Is.Not.Null);
            Assert.That(southWardstone.GetComponentsInChildren<Collider>(true), Is.Empty);
            Transform southBody = southWardstone.transform.Find(
                "ArenaPillar_South_Wardstone");
            Assert.That(southBody, Is.Not.Null);
            MeshRenderer southWardstoneRenderer =
                southBody.GetComponentInChildren<MeshRenderer>(true);
            Assert.That(southWardstoneRenderer, Is.Not.Null);
            Assert.That(
                southWardstoneRenderer.sharedMaterials,
                Has.Length.EqualTo(2));
            Assert.That(
                southWardstoneRenderer.sharedMaterials[0].name,
                Is.EqualTo("RuinStone"),
                "Gameplay cover uses the same PBR masonry language as the fortress.");
            Assert.That(
                southWardstoneRenderer.sharedMaterials[0],
                Is.SameAs(fortressStone));
            Assert.That(
                southWardstoneRenderer.sharedMaterials[0].GetTexture("_BaseMap")
                    .name,
                Does.Contain("stone_brick_wall_001_diff_2k"));
            Assert.That(
                southWardstoneRenderer.sharedMaterials[1].name,
                Does.Contain("LavaSeam"));
            Assert.That(
                southWardstoneRenderer.bounds.size.y,
                Is.LessThanOrEqualTo(ArenaLayout.PillarHeight + 0.35f));
            Assert.That(
                Mathf.Max(
                    southWardstoneRenderer.bounds.size.x,
                    southWardstoneRenderer.bounds.size.z),
                Is.LessThanOrEqualTo(ArenaLayout.PillarRadius * 2f));
            Assert.That(
                Mathf.Max(
                    southWardstoneRenderer.bounds.size.x,
                    southWardstoneRenderer.bounds.size.z),
                Is.GreaterThanOrEqualTo((ArenaLayout.PillarRadius * 2f) - 0.12f),
                "The visual wardstone footprint must closely communicate its collision proxy.");
            Assert.That(
                southWardstone.transform.Find("ArenaPillar_South_BaseBevel"),
                Is.Null,
                "The old primitive wardstone dressing must not survive the source-art replacement.");
            Assert.That(
                southWardstone.GetComponentsInChildren<LineRenderer>(true)
                    .Any(value => value.name == "WardstoneCrownRune"),
                Is.False);

            Assert.That(
                CountTriangles(
                    presentation.GetComponentsInChildren<MeshFilter>(true)),
                Is.LessThanOrEqualTo(65000),
                "The complete static arena environment must stay inside its mobile triangle budget.");

            LineRenderer[] runeRings = Object.FindObjectsByType<LineRenderer>()
                .Where(value => value.name == "RuneRing")
                .ToArray();
            Assert.That(
                runeRings,
                Has.Length.EqualTo(ArenaCollisionMap.ArenaDefault.ObstacleCount),
                "Only cover-footprint rings remain; the source floor replaces old radial clutter.");

            Assert.That(RenderSettings.skybox, Is.Null);
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(
                RenderSettings.ambientSkyColor,
                Is.EqualTo(new Color(0.14f, 0.15f, 0.19f, 1f)));
            Assert.That(
                RenderSettings.ambientEquatorColor,
                Is.EqualTo(new Color(0.22f, 0.13f, 0.09f, 1f)),
                "Vertical fortress faces need a warm ambient lift while local " +
                "fire remains the dominant perimeter light.");
            Assert.That(
                RenderSettings.ambientGroundColor,
                Is.EqualTo(new Color(0.04f, 0.045f, 0.06f, 1f)));
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.ExponentialSquared));
            Assert.That(
                RenderSettings.fogColor,
                Is.EqualTo(new Color(0.042f, 0.024f, 0.029f, 1f)));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.0065f).Within(0.0001f));
            Assert.That(
                RenderSettings.subtractiveShadowColor,
                Is.EqualTo(new Color(0.045f, 0.05f, 0.075f, 1f)));
            Assert.That(
                RenderSettings.defaultReflectionMode,
                Is.EqualTo(DefaultReflectionMode.Custom));
            Assert.That(RenderSettings.customReflection, Is.Not.Null);
            Assert.That(
                RenderSettings.customReflection.name,
                Does.Contain("ArenaEnvironmentReflection"));
            Assert.That(
                RenderSettings.reflectionIntensity,
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(RenderSettings.reflectionBounces, Is.EqualTo(1));

            GameObject environmentLookRoot = GameObject.Find("ArenaEnvironmentLook");
            Assert.That(environmentLookRoot, Is.Not.Null);
            Volume environmentLook = environmentLookRoot.GetComponent<Volume>();
            Assert.That(environmentLook, Is.Not.Null);
            Assert.That(environmentLook.isGlobal, Is.True);
            Assert.That(environmentLook.priority, Is.EqualTo(0f).Within(0.001f));
            Assert.That(environmentLook.weight, Is.EqualTo(1f).Within(0.001f));
            VolumeProfile environmentProfile = environmentLook.sharedProfile;
            Assert.That(environmentProfile, Is.Not.Null);
            Assert.That(environmentProfile.name, Does.Contain("ArenaEnvironmentLook"));

            Assert.That(
                environmentProfile.TryGet(out ColorAdjustments colorAdjustments),
                Is.True);
            Assert.That(colorAdjustments.active, Is.True);
            Assert.That(
                colorAdjustments.postExposure.value,
                Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(
                colorAdjustments.contrast.value,
                Is.EqualTo(4f).Within(0.001f));
            Assert.That(
                colorAdjustments.colorFilter.value,
                Is.EqualTo(new Color(1f, 0.99f, 0.975f, 1f)));
            Assert.That(
                colorAdjustments.saturation.value,
                Is.EqualTo(-2f).Within(0.001f));

            Assert.That(
                environmentProfile.TryGet(out WhiteBalance whiteBalance),
                Is.True);
            Assert.That(whiteBalance.active, Is.True);
            Assert.That(
                whiteBalance.temperature.value,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                whiteBalance.tint.value,
                Is.EqualTo(0f).Within(0.001f));

            Assert.That(
                environmentProfile.TryGet(out Tonemapping tonemapping),
                Is.True);
            Assert.That(tonemapping.active, Is.True);
            Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.ACES));

            Assert.That(environmentProfile.TryGet(out Bloom bloom), Is.True);
            Assert.That(bloom.active, Is.True);
            Assert.That(bloom.threshold.value, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(bloom.intensity.value, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(bloom.scatter.value, Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(bloom.clamp.value, Is.EqualTo(8f).Within(0.001f));
            Assert.That(
                bloom.tint.value,
                Is.EqualTo(new Color(1f, 0.82f, 0.64f, 1f)));
            Assert.That(bloom.highQualityFiltering.value, Is.True);
            Assert.That(bloom.downscale.value, Is.EqualTo(BloomDownscaleMode.Half));
            Assert.That(bloom.maxIterations.value, Is.EqualTo(5));

            Assert.That(environmentProfile.TryGet(out Vignette vignette), Is.True);
            Assert.That(vignette.active, Is.True);
            Assert.That(
                vignette.color.value,
                Is.EqualTo(new Color(0.006f, 0.008f, 0.016f, 1f)));
            Assert.That(
                vignette.center.value,
                Is.EqualTo(new Vector2(0.5f, 0.49f)));
            Assert.That(vignette.intensity.value, Is.EqualTo(0.065f).Within(0.001f));
            Assert.That(vignette.smoothness.value, Is.EqualTo(0.48f).Within(0.001f));
            Assert.That(vignette.rounded.value, Is.False);
            Light key = GameObject.Find("MoonKey").GetComponent<Light>();
            Assert.That(RenderSettings.sun, Is.SameAs(key));
            Assert.That(key.type, Is.EqualTo(LightType.Directional));
            Assert.That(key.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(
                Quaternion.Angle(
                    key.transform.rotation,
                    Quaternion.Euler(35f, -35f, 0f)),
                Is.LessThan(0.01f),
                "The grazing 35-degree key reveals masonry relief and readable cover shadows.");
            Assert.That(
                key.color,
                Is.EqualTo(new Color(0.86f, 0.88f, 0.94f, 1f)));
            Assert.That(key.intensity, Is.EqualTo(1.95f).Within(0.001f));
            Assert.That(key.shadowStrength, Is.EqualTo(0.86f).Within(0.001f));
            Assert.That(key.shadowBias, Is.EqualTo(0.045f).Within(0.001f));
            Assert.That(key.shadowNormalBias, Is.EqualTo(0.18f).Within(0.001f));
            Vector3 keyRay = key.transform.forward;
            Vector3 planarShadowDirection = Vector3.ProjectOnPlane(
                keyRay,
                Vector3.up);
            float wardstoneShadowLength = ArenaLayout.PillarHeight *
                (planarShadowDirection.magnitude / -keyRay.y);
            planarShadowDirection.Normalize();
            Assert.That(keyRay.y, Is.LessThan(-0.55f));
            Assert.That(
                wardstoneShadowLength,
                Is.InRange(3.3f, 3.4f),
                "The stronger-phone key uses a long but bounded cover shadow to expose surface relief.");
            Assert.That(
                Vector3.Dot(planarShadowDirection, camera.transform.right),
                Is.LessThan(-0.6f),
                "Wardstone shadows must travel toward screen-left in the locked strategy view.");
            Assert.That(
                Vector3.Dot(planarShadowDirection, camera.transform.up),
                Is.LessThan(-0.3f),
                "Wardstone shadows must travel downward in the locked strategy view.");
            MobileQualityController quality =
                Object.FindAnyObjectByType<MobileQualityController>();
            Assert.That(quality.ActiveTier, Is.EqualTo(MobileQualityTier.High));
            Assert.That(
                quality.ActiveContract.Rendering.ShadowDistance,
                Is.EqualTo(40f));
            Assert.That(
                quality.ActiveContract.Rendering.MsaaSampleCount,
                Is.EqualTo(4));
            Assert.That(
                quality.ActiveContract.Rendering.MainLightShadowmapResolution,
                Is.EqualTo(4096));
            Assert.That(
                quality.ActiveContract.Rendering.ShadowCascadeCount,
                Is.EqualTo(2));
            Assert.That(
                quality.ActiveContract.Rendering.ShadowCascade2Split,
                Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(quality.ActiveContract.Rendering.SoftShadows, Is.True);
            Assert.That(
                quality.ActiveContract.Rendering.AdditionalLightsMode,
                Is.EqualTo(MobileAdditionalLightsMode.PerPixel));
            Assert.That(
                quality.ActiveContract.Rendering.MaxAdditionalLightsPerObject,
                Is.EqualTo(4));
            string[] fireLightNames =
            {
                "FortressGlowLeftFlank",
                "FortressGlowLeftShoulder",
                "FortressGlowRightShoulder",
                "FortressGlowRightFlank",
            };
            for (int index = 0; index < fireLightNames.Length; index++)
            {
                Light glow = GameObject.Find(fireLightNames[index]).GetComponent<Light>();
                Transform fireAnchor = fireAnchors.Find(fireAnchorNames[index]);
                Assert.That(glow.type, Is.EqualTo(LightType.Point));
                Assert.That(glow.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(
                    Vector2.Distance(
                        new Vector2(
                            glow.transform.position.x,
                            glow.transform.position.z),
                        new Vector2(
                            fireAnchor.position.x,
                            fireAnchor.position.z)),
                    Is.LessThan(0.001f),
                    fireLightNames[index]);
                Assert.That(
                    glow.transform.position.y,
                    Is.EqualTo(1.55f).Within(0.001f));
                Assert.That(
                    glow.color,
                    Is.EqualTo(new Color(1f, 0.36f, 0.08f, 1f)));
                Assert.That(glow.range, Is.EqualTo(8.25f).Within(0.001f));
                Assert.That(glow.intensity, Is.EqualTo(9.5f).Within(0.001f));
            }
            Assert.That(
                Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude),
                Has.Length.EqualTo(5),
                "The arena uses one shadowed key plus four localized, unshadowed " +
                "brazier lights within the High tier's four-light per-object cap.");
        }

        [UnityTest]
        public IEnumerator StrategyCamera_FollowsTranslationWithoutInheritingFacing()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForFixedUpdate();
            yield return null;

            StrategyFollowCamera cameraRig =
                Object.FindAnyObjectByType<StrategyFollowCamera>();
            CombatantView human = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.IsHuman);
            controller.enabled = false;
            cameraRig.SnapToTarget();

            Vector3 originalCameraPosition = cameraRig.transform.position;
            Quaternion originalCameraRotation = cameraRig.transform.rotation;
            Vector3 targetDelta = new Vector3(2.75f, 0f, -1.25f);
            human.transform.position += targetDelta;
            human.transform.rotation = Quaternion.Euler(0f, 137f, 0f);
            cameraRig.SnapToTarget();

            Vector3 cameraDelta = cameraRig.transform.position - originalCameraPosition;
            Assert.That(cameraDelta.x, Is.EqualTo(targetDelta.x).Within(0.001f));
            Assert.That(cameraDelta.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(cameraDelta.z, Is.EqualTo(targetDelta.z).Within(0.001f));
            Assert.That(
                Quaternion.Angle(cameraRig.transform.rotation, originalCameraRotation),
                Is.EqualTo(0f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator StrategyCamera_FixedCompositionRemainsLockedAndClamped()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Hunter);
            yield return new WaitForFixedUpdate();
            yield return null;

            StrategyFollowCamera cameraRig =
                Object.FindAnyObjectByType<StrategyFollowCamera>();
            Transform followTarget = cameraRig.FollowTarget;
            Assert.That(followTarget, Is.Not.Null);

            cameraRig.SetFixedComposition(450f, 20f, 0f, -3f, 100f, 90f, true);

            Assert.That(Mathf.DeltaAngle(cameraRig.Yaw, 90f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(cameraRig.Pitch, Is.EqualTo(35f).Within(0.001f));
            Assert.That(cameraRig.Distance, Is.EqualTo(1f).Within(0.001f));
            Assert.That(cameraRig.ForwardFocusOffset, Is.EqualTo(0f).Within(0.001f));
            Assert.That(cameraRig.RightFocusOffset, Is.EqualTo(10f).Within(0.001f));
            Assert.That(cameraRig.FieldOfView, Is.EqualTo(70f).Within(0.001f));
            Assert.That(cameraRig.GetComponent<Camera>().fieldOfView, Is.EqualTo(70f).Within(0.001f));
            Assert.That(cameraRig.FollowTarget, Is.SameAs(followTarget));
            Assert.That(
                Quaternion.Angle(
                    cameraRig.transform.rotation,
                    Quaternion.Euler(35f, 90f, 0f)),
                Is.EqualTo(0f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator StrategyCamera_GameplayCompositionsBoundOrbitAtReviewedEndpoints()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Hunter);
            yield return new WaitForFixedUpdate();
            yield return null;

            StrategyFollowCamera cameraRig =
                Object.FindAnyObjectByType<StrategyFollowCamera>();
            Assert.That(cameraRig, Is.Not.Null);

            cameraRig.SetGameplayComposition(GameModeId.Arena, true);
            Assert.That(
                Mathf.DeltaAngle(
                    cameraRig.OrbitCenterYaw,
                    StrategyFollowCamera.ArenaYaw),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                cameraRig.MaximumOrbitOffset,
                Is.EqualTo(StrategyFollowCamera.ArenaMaximumOrbitOffset)
                    .Within(0.001f));
            cameraRig.RotateHeading(100f, true);
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, 150f),
                Is.EqualTo(0f).Within(0.001f),
                "Arena positive orbit must stop 60 degrees from its 90-degree center.");
            cameraRig.SetGameplayComposition(GameModeId.Arena, true);
            cameraRig.RotateHeading(-100f, true);
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, 30f),
                Is.EqualTo(0f).Within(0.001f),
                "Arena negative orbit must stop 60 degrees from its 90-degree center.");

            cameraRig.SetGameplayComposition(GameModeId.Boss, true);
            Assert.That(
                Mathf.DeltaAngle(
                    cameraRig.OrbitCenterYaw,
                    StrategyFollowCamera.BossOrbitCenterYaw),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                cameraRig.MaximumOrbitOffset,
                Is.EqualTo(StrategyFollowCamera.BossMaximumOrbitOffset)
                    .Within(0.001f));
            cameraRig.RotateHeading(100f, true);
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, 65f),
                Is.EqualTo(0f).Within(0.001f),
                "Boss positive orbit must stop before the party enters the left HUD.");
            cameraRig.SetGameplayComposition(GameModeId.Boss, true);
            cameraRig.RotateHeading(-100f, true);
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, 325f),
                Is.EqualTo(0f).Within(0.001f),
                "Boss negative orbit must stop before the boss enters the ability HUD.");

            cameraRig.SetGameplayComposition(GameModeId.Leveling, true);
            Assert.That(
                Mathf.DeltaAngle(
                    cameraRig.OrbitCenterYaw,
                    StrategyFollowCamera.LevelingYaw),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                cameraRig.MaximumOrbitOffset,
                Is.EqualTo(StrategyFollowCamera.LevelingMaximumOrbitOffset)
                    .Within(0.001f));
            cameraRig.RotateHeading(100f, true);
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, 145f),
                Is.EqualTo(0f).Within(0.001f));
            cameraRig.SetGameplayComposition(GameModeId.Leveling, true);
            cameraRig.RotateHeading(-100f, true);
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, 35f),
                Is.EqualTo(0f).Within(0.001f));

            cameraRig.SetGameplayComposition(GameModeId.Arena, true);
            cameraRig.SetFixedComposition(
                270f,
                StrategyFollowCamera.ArenaPitch,
                StrategyFollowCamera.ArenaDistance,
                StrategyFollowCamera.ArenaForwardFocusOffset,
                StrategyFollowCamera.ArenaRightFocusOffset,
                StrategyFollowCamera.ArenaFieldOfView,
                true);
            Assert.That(
                Mathf.DeltaAngle(cameraRig.Yaw, 270f),
                Is.EqualTo(0f).Within(0.001f),
                "Capture/tooling composition must accept headings outside the " +
                "runtime Arena orbit arc.");
            Assert.That(
                Mathf.DeltaAngle(cameraRig.OrbitCenterYaw, 270f),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(cameraRig.MaximumOrbitOffset, Is.EqualTo(180f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator HeroSiege_SoloCameraKeepsTheObjectiveAndIncomingLaneInFrame()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSiegeSession(
                SiegeVariantId.SoloLane,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            controller.enabled = false;
            yield return null;

            StrategyFollowCamera cameraRig =
                Object.FindAnyObjectByType<StrategyFollowCamera>();
            Camera camera = cameraRig.GetComponent<Camera>();
            CombatantView human = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.IsHuman);
            SiegeVariantDefinition variant = HeroSiegeRunCatalog.GetVariant(
                SiegeVariantId.SoloLane);
            Float3 firstSpawn = variant.GetLaneSpawnPosition(SiegeLaneId.East);
            SiegeSpawnDefinition deepestSpawn = HeroSiegeRunCatalog.CreateSpawn(
                HeroSiegeRunCatalog.GetWave(HeroSiegeRunCatalog.WaveCount),
                SiegeLaneId.East,
                HeroSiegeRunCatalog.MaximumSpawnsPerLane - 1);

            camera.aspect = 16f / 9f;
            cameraRig.SetSiegeComposition(false, true);
            float closeHeroHeight = GetViewportHeight(camera, human);
            cameraRig.SetFixedComposition(
                90f,
                58f,
                22f,
                2.8f,
                0f,
                50f,
                true);
            float previousHeroHeight = GetViewportHeight(camera, human);
            Assert.That(
                closeHeroHeight,
                Is.GreaterThan(previousHeroHeight * 1.2f),
                "The closer Solo Lane composition must make the human materially " +
                "larger than the previous 22 m / 50 degree frame.");

            float[] supportedAspects =
            {
                16f / 9f,
                20f / 9f,
                2.4f,
            };
            for (int aspectIndex = 0;
                 aspectIndex < supportedAspects.Length;
                 aspectIndex++)
            {
                float aspect = supportedAspects[aspectIndex];
                camera.aspect = aspect;
                cameraRig.SetSiegeComposition(false, true);

                Assert.That(
                    Mathf.DeltaAngle(cameraRig.Yaw, 90f),
                    Is.EqualTo(0f).Within(0.001f));
                Assert.That(cameraRig.Pitch, Is.EqualTo(58f).Within(0.001f));
                Assert.That(cameraRig.Distance, Is.EqualTo(19f).Within(0.001f));
                Assert.That(
                    cameraRig.ForwardFocusOffset,
                    Is.EqualTo(2.4f).Within(0.001f));
                Assert.That(cameraRig.FieldOfView, Is.EqualTo(46f).Within(0.001f));
                Assert.That(camera.fieldOfView, Is.EqualTo(46f).Within(0.001f));
                AssertSiegeVerticalSpanInFrame(
                    camera,
                    human.transform.position,
                    human.AimPoint.position,
                    0.04f,
                    0.96f,
                    "Solo Lane human at aspect " + aspect);
                AssertSiegeVerticalSpanInFrame(
                    camera,
                    Vector3.zero,
                    Vector3.up * 3.5f,
                    0.04f,
                    0.96f,
                    "Solo Lane fortress objective at aspect " + aspect);
                AssertSiegeVerticalSpanInFrame(
                    camera,
                    new Vector3(firstSpawn.X, firstSpawn.Y, firstSpawn.Z),
                    new Vector3(firstSpawn.X, firstSpawn.Y + 2.2f, firstSpawn.Z),
                    0.04f,
                    0.96f,
                    "Solo Lane first incoming pressure at aspect " + aspect);
                AssertSiegeVerticalSpanInFrame(
                    camera,
                    new Vector3(
                        deepestSpawn.Position.X,
                        deepestSpawn.Position.Y,
                        deepestSpawn.Position.Z),
                    new Vector3(
                        deepestSpawn.Position.X,
                        deepestSpawn.Position.Y + 2.2f,
                        deepestSpawn.Position.Z),
                    0.04f,
                    0.96f,
                    "Solo Lane deepest authored spawn at aspect " + aspect);
            }
        }

        [UnityTest]
        public IEnumerator HeroSiege_FourLaneCameraKeepsDefendersAndPressureInFrame()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSiegeSession(
                SiegeVariantId.FourLanes,
                ClassId.Mage,
                SpecializationId.Frost);
            controller.enabled = false;
            yield return null;

            StrategyFollowCamera cameraRig =
                Object.FindAnyObjectByType<StrategyFollowCamera>();
            Camera camera = cameraRig.GetComponent<Camera>();
            CombatantView[] defenders = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .OrderBy(value => value.Id.Value)
                .ToArray();
            SiegeVariantDefinition variant = HeroSiegeRunCatalog.GetVariant(
                SiegeVariantId.FourLanes);
            Assert.That(defenders, Has.Length.EqualTo(4));
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            RectTransform[] actionControls = new RectTransform[
                AbilityCatalog.AbilitySlotCount];
            for (int slot = 0; slot < actionControls.Length; slot++)
            {
                actionControls[slot] = (RectTransform)hud.GetAbilitySlot(slot).transform;
            }

            camera.aspect = 16f / 9f;
            cameraRig.SetSiegeComposition(true, true);
            float[] closeDefenderHeights = defenders
                .Select(value => GetViewportHeight(camera, value))
                .ToArray();
            cameraRig.SetFixedComposition(
                45f,
                65f,
                28f,
                0f,
                0f,
                54f,
                true);
            for (int defenderIndex = 0;
                 defenderIndex < defenders.Length;
                 defenderIndex++)
            {
                float previousHeight = GetViewportHeight(
                    camera,
                    defenders[defenderIndex]);
                Assert.That(
                    closeDefenderHeights[defenderIndex],
                    Is.GreaterThan(previousHeight * 1.18f),
                    "The closer Four Lanes composition must make every defender " +
                    "materially larger than the previous 28 m / 54 degree frame. " +
                    "Defender=" + defenders[defenderIndex].Id.Value);
            }

            float[] supportedAspects =
            {
                16f / 9f,
                20f / 9f,
                2.4f,
            };
            for (int aspectIndex = 0;
                 aspectIndex < supportedAspects.Length;
                 aspectIndex++)
            {
                float aspect = supportedAspects[aspectIndex];
                camera.aspect = aspect;
                cameraRig.SetSiegeComposition(true, true);

                Assert.That(
                    Mathf.DeltaAngle(cameraRig.Yaw, 45f),
                    Is.EqualTo(0f).Within(0.001f));
                Assert.That(cameraRig.Pitch, Is.EqualTo(65f).Within(0.001f));
                Assert.That(cameraRig.Distance, Is.EqualTo(24f).Within(0.001f));
                Assert.That(
                    cameraRig.RightFocusOffset,
                    Is.EqualTo(3f).Within(0.001f));
                Assert.That(cameraRig.FieldOfView, Is.EqualTo(50f).Within(0.001f));
                Assert.That(camera.fieldOfView, Is.EqualTo(50f).Within(0.001f));
                AssertSiegeVerticalSpanInFrame(
                    camera,
                    Vector3.zero,
                    Vector3.up * 3.5f,
                    0.06f,
                    0.94f,
                    "Four Lanes fortress objective at aspect " + aspect);

                for (int defenderIndex = 0;
                     defenderIndex < defenders.Length;
                     defenderIndex++)
                {
                    CombatantView defender = defenders[defenderIndex];
                    AssertSiegeVerticalSpanInFrame(
                        camera,
                        defender.transform.position,
                        defender.AimPoint.position,
                        0.06f,
                        0.94f,
                        "Four Lanes defender " + defender.Id.Value +
                        " at aspect " + aspect);
                    AssertSiegeSpanAvoidsActionHud(
                        camera,
                        defender.transform.position,
                        defender.AimPoint.position,
                        actionControls,
                        aspect,
                        "Four Lanes defender " + defender.Id.Value);
                }

                for (int laneIndex = 0;
                     laneIndex < variant.LaneCount;
                     laneIndex++)
                {
                    SiegeLaneId lane = variant.GetLane(laneIndex);
                    Float3 spawn = variant.GetLaneSpawnPosition(lane);
                    Vector3 spawnFeet = new Vector3(
                        spawn.X,
                        spawn.Y,
                        spawn.Z);
                    Vector3 spawnTop = new Vector3(
                        spawn.X,
                        spawn.Y + 2.2f,
                        spawn.Z);
                    AssertSiegeVerticalSpanInFrame(
                        camera,
                        spawnFeet,
                        spawnTop,
                        0.06f,
                        0.94f,
                        "Four Lanes incoming " + lane +
                        " pressure at aspect " + aspect);
                    AssertSiegeSpanAvoidsActionHud(
                        camera,
                        spawnFeet,
                        spawnTop,
                        actionControls,
                        aspect,
                        "Four Lanes incoming " + lane + " pressure");
                    Assert.That(
                        Mathf.Max(
                            camera.WorldToViewportPoint(spawnFeet).x,
                            camera.WorldToViewportPoint(spawnTop).x),
                        Is.LessThanOrEqualTo(0.68f),
                        "The Four Lanes HUD-aware offset must reserve the rightmost " +
                        "32% of the 16:9-or-wider frame for the action cluster. " +
                        "Lane=" + lane + ", aspect=" + aspect);
                }
            }
        }

        [UnityTest]
        public IEnumerator Arena_TargetObliqueCompositionRetainsInitialCombatFrame()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            controller.enabled = false;
            yield return null;

            StrategyFollowCamera cameraRig =
                Object.FindAnyObjectByType<StrategyFollowCamera>();
            Camera camera = cameraRig.GetComponent<Camera>();
            CombatantView[] combatants = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
            CombatantView human = combatants.Single(value => value.IsHuman);
            GameObject southWardstone = GameObject.Find("ArenaPillar_South");
            GameObject northWardstone = GameObject.Find("ArenaPillar_North");
            Assert.That(combatants, Has.Length.EqualTo(4));
            Assert.That(southWardstone, Is.Not.Null);
            Assert.That(northWardstone, Is.Not.Null);

            camera.aspect = 16f / 9f;
            cameraRig.SetGameplayComposition(GameModeId.Arena, true);
            float targetHeroHeight = Mathf.Abs(
                camera.WorldToViewportPoint(human.AimPoint.position).y -
                camera.WorldToViewportPoint(human.transform.position).y);

            Assert.That(
                targetHeroHeight,
                Is.InRange(0.06f, 0.095f),
                "The target-oblique frame must keep the controlled hero readable " +
                "while exposing the fortress silhouette.");

            float[] supportedAspects =
            {
                16f / 9f,
                20f / 9f,
                2.4f,
            };
            for (int aspectIndex = 0; aspectIndex < supportedAspects.Length; aspectIndex++)
            {
                float aspect = supportedAspects[aspectIndex];
                camera.aspect = aspect;
                cameraRig.SetGameplayComposition(GameModeId.Arena, true);

                for (int combatantIndex = 0;
                     combatantIndex < combatants.Length;
                     combatantIndex++)
                {
                    CombatantView combatant = combatants[combatantIndex];
                    Vector3 viewport = camera.WorldToViewportPoint(
                        combatant.AimPoint.position);
                    Assert.That(
                        viewport.z,
                        Is.GreaterThan(0f),
                        "Combatant is behind the camera at aspect " + aspect + ".");
                    Assert.That(
                        viewport.x,
                        Is.InRange(0.025f, 0.975f),
                        "Combatant left the horizontal frame at aspect " + aspect + ".");
                    Assert.That(
                        viewport.y,
                        Is.InRange(0.06f, 0.94f),
                        "Combatant left the vertical frame at aspect " + aspect + ".");
                }

                AssertWardstoneCenterInFrame(camera, southWardstone, aspect);
                AssertWardstoneCenterInFrame(camera, northWardstone, aspect);
                Assert.That(
                    camera.WorldToViewportPoint(human.AimPoint.position).x,
                    Is.LessThan(0.58f),
                    "The controlled hero must stay clear of the right-hand action cluster " +
                    "without shifting the complete arena off center.");
            }
        }

        [UnityTest]
        public IEnumerator Arena_CombatantsAnimateWithoutRootMotionOnFlatGround()
        {
            PrototypeGameController controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForSeconds(3.2f);

            CombatantAnimationPresenter[] presenters =
                Object.FindObjectsByType<CombatantAnimationPresenter>(FindObjectsInactive.Exclude);
            Assert.That(presenters, Has.Length.EqualTo(4));
            Assert.That(
                presenters.All(value => value.ActiveAnimator != null),
                Is.True,
                "Every active class must have a bound Animator.");
            Assert.That(
                presenters.All(value => value.ActiveAnimator.runtimeAnimatorController != null),
                Is.True,
                "Every active class must have an authored locomotion controller.");
            Assert.That(
                presenters.All(value => !value.ActiveAnimator.applyRootMotion),
                Is.True,
                "Simulation movement must remain authoritative.");
            bool observedLocomotion = false;
            float observationDeadline = Time.realtimeSinceStartup + 1.5f;
            while (!observedLocomotion && Time.realtimeSinceStartup < observationDeadline)
            {
                observedLocomotion = presenters.Any(value =>
                    value.ActiveAnimator.GetFloat(
                        CombatantAnimationPresenter.MoveSpeedParameter) > 0.05f);
                yield return null;
            }

            Assert.That(
                observedLocomotion,
                Is.True,
                "At least one bot should visibly locomote after the countdown.");

            CombatantView[] views =
                Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
            Assert.That(
                views.All(value => Mathf.Abs(value.transform.position.y) < 0.05f),
                Is.True,
                "The first arena pass is deliberately flat until terrain authority is implemented.");
            Assert.That(GameObject.Find("ContactShadow"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Arena_CastAnimationUsesStartHoldReleaseChannelExitAndHardBreak()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return null;

            CombatantView human = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.IsHuman);
            CombatantAnimationPresenter presenter =
                human.GetComponent<CombatantAnimationPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Animator animator = presenter.ActiveAnimator;
            Assert.That(animator, Is.Not.Null);
            controller.enabled = false;

            presenter.PlayCast(AbilityId.Frostbolt);
            yield return new WaitForSeconds(0.12f);
            AssertAnimatorState(animator, 0, "CastEnter");
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.CastAnticipation));
            yield return new WaitForSeconds(0.42f);
            AssertAnimatorState(animator, 0, "CastLoop");

            presenter.PlayCastRelease(AbilityId.Frostbolt);
            yield return new WaitForSeconds(0.12f);
            AssertAnimatorState(animator, 0, "CastRelease");
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.Release));

            presenter.PlayCast(AbilityId.Penance);
            yield return new WaitForSeconds(0.54f);
            AssertAnimatorState(animator, 0, "CastLoop");
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.ChannelHold));
            presenter.PlayChannelPulse(AbilityId.Penance);
            yield return new WaitForSeconds(0.08f);
            AssertAnimatorState(animator, 1, "ChannelPulse");
            presenter.PlayCastRelease(AbilityId.Penance);
            yield return new WaitForSeconds(0.1f);
            AssertAnimatorState(animator, 0, "CastCancel");

            presenter.PlayCast(AbilityId.Frostbolt);
            yield return new WaitForSeconds(0.08f);
            presenter.PlayCastCanceled(true);
            yield return new WaitForSeconds(0.1f);
            AssertAnimatorState(animator, 0, "Interrupt");
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.InterruptReaction));
        }

        [UnityTest]
        public IEnumerator Arena_RogueStrikeExposesWindupContactAndRecoveryPhases()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Rogue,
                SpecializationId.Mutilate);
            yield return null;

            CombatantAnimationPresenter presenter = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.IsHuman)
                .GetComponent<CombatantAnimationPresenter>();
            Assert.That(presenter, Is.Not.Null);
            controller.enabled = false;

            presenter.PlayCast(AbilityId.Mutilate);
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.MeleeWindup));
            yield return new WaitForSeconds(0.13f);
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.MeleeContact));
            yield return new WaitForSeconds(0.14f);
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.MeleeRecovery));
            yield return new WaitForSeconds(0.16f);
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.Idle));
        }

        [UnityTest]
        public IEnumerator Arena_HumanoidHeroPosesStayGroundedOnOneSkeleton()
        {
            PrototypeGameController controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForSeconds(0.25f);

            CombatantAnimationPresenter[] presenters =
                Object.FindObjectsByType<CombatantAnimationPresenter>(FindObjectsInactive.Exclude);
            Assert.That(presenters, Has.Length.EqualTo(4));
            for (int presenterIndex = 0; presenterIndex < presenters.Length; presenterIndex++)
            {
                CombatantAnimationPresenter presenter = presenters[presenterIndex];
                Animator animator = presenter.ActiveAnimator;
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.avatar, Is.Not.Null);
                Assert.That(animator.avatar.isValid, Is.True);
                Assert.That(animator.avatar.isHuman, Is.True);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Assert.That(hips, Is.Not.Null);
                Assert.That(
                    animator.GetComponentsInChildren<Transform>(true)
                        .Count(value => value.name == hips.name),
                    Is.EqualTo(1),
                    animator.name +
                    " must not retain a duplicate skeleton with the mapped Hips bone name.");

                presenter.enabled = false;
                Transform visual = animator.transform;
                visual.localPosition = new Vector3(0f, 0.03f, 0f);
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one * 1.05f;
                animator.Rebind();

                Assert.That(
                    Vector3.Dot(visual.forward, presenter.transform.forward),
                    Is.GreaterThan(0.99f),
                    animator.name + " must face the combatant simulation direction.");

                AssertGroundedPose(animator, "Locomotion", 0f, -0.035f, 0.05f);
                AssertGroundedPose(animator, "Locomotion", 0.45f, -0.045f, 0.07f);
                AssertGroundedPose(animator, "Locomotion", 1f, -0.075f, 0.18f);
                AssertGroundedPose(animator, "Basic", 0f, -0.06f, 0.12f);
                AssertGroundedPose(animator, "Power", 0f, -0.06f, 0.14f);
                AssertGroundedPose(animator, "Utility", 0f, -0.08f, 0.18f);
                AssertGroundedPose(animator, "Guard", 0f, -0.06f, 0.14f);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Arena_HudUsesOneCrossTeamFocusTarget()
        {
            PrototypeGameController controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);
            Canvas.ForceUpdateCanvases();

            CombatantStatusView[] fighters = Object
                .FindObjectsByType<CombatantStatusView>(FindObjectsInactive.Include)
                .Where(value => value.IsBound)
                .OrderBy(value => value.ParticipantId)
                .ToArray();
            Assert.That(fighters, Has.Length.EqualTo(4));
            Assert.That(fighters.Count(value => value.IsFriendly), Is.EqualTo(2));
            Assert.That(fighters.Count(value => !value.IsFriendly), Is.EqualTo(2));
            CombatantStatusView hiddenEnemy = fighters.Single(value =>
                !value.IsFriendly && value.IsHidden);
            Assert.That(
                fighters.Count(value => value.gameObject.activeInHierarchy),
                Is.EqualTo(4),
                "Known arena roster slots stay visible while stealth obscures live information.");
            Assert.That(ReadChildText(fighters[0].transform, "Relation"), Is.EqualTo("YOU"));
            Assert.That(ReadChildText(fighters[1].transform, "Relation"), Is.EqualTo("ALLY"));
            Assert.That(ReadChildText(fighters[2].transform, "Relation"), Is.EqualTo("HIDDEN"));
            Assert.That(ReadChildText(fighters[3].transform, "Relation"), Is.EqualTo("ENEMY"));
            Assert.That(fighters.All(HasCompactCircularSelector), Is.True);

            SoftTargetSelectionService targeting =
                Object.FindAnyObjectByType<SoftTargetSelectionService>();
            Assert.That(targeting, Is.Not.Null);
            Assert.That(targeting.TargetLayerMask, Is.EqualTo(1 << 9));
            Assert.That(targeting.OcclusionLayerMask, Is.EqualTo(1 << 8));
            Assert.That(
                targeting.CurrentTarget,
                Is.Null,
                "Automatic focus must use authoritative snapshots, not the physics picker.");

            CombatantStatusView human = fighters.Single(value =>
                ReadChildText(value.transform, "Relation") == "YOU");
            CombatantStatusView ally = fighters.Single(value =>
                ReadChildText(value.transform, "Relation") == "ALLY");
            CombatantStatusView visibleEnemy = fighters.Single(value =>
                !value.IsFriendly && !value.IsHidden);
            Assert.That(IsSelectorSelected(human), Is.False);
            Assert.That(IsSelectorSelected(ally), Is.False);
            Assert.That(IsSelectorSelected(visibleEnemy), Is.True);
            Assert.That(fighters.Count(IsSelectorSelected), Is.EqualTo(1));
            Assert.That(
                hud.FocusedFighterId,
                Is.EqualTo(visibleEnemy.ParticipantId));

            Transform targetFrame = hud.transform.Find("SafeArea/Gameplay/Target");
            Assert.That(targetFrame, Is.Not.Null);
            Assert.That(
                targetFrame.gameObject.activeInHierarchy,
                Is.True,
                "The compact target strip presents the deterministic fallback focus.");
            Assert.That(ReadChildText(targetFrame, "Relation"), Is.EqualTo("ENEMY"));

            int selectedParticipant = 0;
            hud.FighterSelected += participantId => selectedParticipant = participantId;
            ally.GetComponent<Button>().onClick.Invoke();
            Assert.That(selectedParticipant, Is.EqualTo(ally.ParticipantId));
            Assert.That(IsSelectorSelected(human), Is.False);
            Assert.That(IsSelectorSelected(ally), Is.True);
            Assert.That(IsSelectorSelected(visibleEnemy), Is.False);
            Assert.That(fighters.Count(IsSelectorSelected), Is.EqualTo(1));
            Assert.That(
                hud.FocusedFighterId,
                Is.EqualTo(ally.ParticipantId),
                "The compact selector must remain the authoritative HUD focus.");
            Assert.That(
                targeting.CurrentTarget,
                Is.Null,
                "Friendly focus must clear the enemy-only world-pick marker.");
            Assert.That(targetFrame.gameObject.activeInHierarchy, Is.True);
            Assert.That(ReadChildText(targetFrame, "Relation"), Is.EqualTo("ALLY"));
            Assert.That(ReadChildText(targetFrame, "TargetName"), Does.Contain("PRIEST"));

            visibleEnemy.GetComponent<Button>().onClick.Invoke();
            Assert.That(selectedParticipant, Is.EqualTo(visibleEnemy.ParticipantId));
            Assert.That(IsSelectorSelected(visibleEnemy), Is.True);
            Assert.That(IsSelectorSelected(hiddenEnemy), Is.False);
            Assert.That(IsSelectorSelected(ally), Is.False);
            Assert.That(fighters.Count(IsSelectorSelected), Is.EqualTo(1));
            Assert.That(targeting.CurrentTarget, Is.Not.Null);
            Assert.That(
                targeting.CurrentTarget.TargetId,
                Is.EqualTo(visibleEnemy.ParticipantId),
                "Enemy focus remains synchronized with the world target service.");
            Assert.That(targetFrame.gameObject.activeInHierarchy, Is.True);
            Assert.That(ReadChildText(targetFrame, "Relation"), Is.EqualTo("ENEMY"));

            human.GetComponent<Button>().onClick.Invoke();
            Assert.That(hud.FocusedFighterId, Is.EqualTo(human.ParticipantId));
            Assert.That(IsSelectorSelected(human), Is.True);
            Assert.That(fighters.Count(IsSelectorSelected), Is.EqualTo(1));
            Assert.That(
                targeting.CurrentTarget,
                Is.Null,
                "Selecting self must clear the previous enemy world-pick marker.");
        }

        [UnityTest]
        public IEnumerator Arena_StealthedEnemyRosterCardIsObscuredUnselectableAndRestores()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            CombatantStatusView[] opponents = Object
                .FindObjectsByType<CombatantStatusView>(FindObjectsInactive.Include)
                .Where(value => value.IsBound && !value.IsFriendly)
                .OrderBy(value => value.ParticipantId)
                .ToArray();
            Assert.That(opponents, Has.Length.EqualTo(2));
            Assert.That(opponents.All(value => value.gameObject.activeInHierarchy), Is.True);

            CombatantStatusView hidden = opponents.Single(value => value.IsHidden);
            Button hiddenButton = hidden.GetComponent<Button>();
            CanvasGroup hiddenCanvas = hidden.GetComponent<CanvasGroup>();
            Assert.That(ReadChildText(hidden.transform, "Relation"), Is.EqualTo("HIDDEN"));
            Assert.That(ReadChildText(hidden.transform, "Health"), Is.EqualTo("?"));
            Assert.That(ReadChildText(hidden.transform.Find("ClassBadge"), "BadgeLabel"), Is.EqualTo("R"));
            Assert.That(hiddenButton.interactable, Is.False);
            Assert.That(hiddenCanvas.alpha, Is.LessThan(1f));
            Assert.That(hiddenCanvas.blocksRaycasts, Is.False);
            Assert.That(IsSelectorSelected(hidden), Is.False);

            int originalFocus = hud.FocusedFighterId;
            Assert.That(originalFocus, Is.GreaterThan(0));
            Assert.That(originalFocus, Is.Not.EqualTo(hidden.ParticipantId));
            int selectedParticipant = 0;
            hud.FighterSelected += participantId => selectedParticipant = participantId;
            hiddenButton.onClick.Invoke();
            Assert.That(selectedParticipant, Is.Zero);
            Assert.That(hud.FocusedFighterId, Is.EqualTo(originalFocus));
            Assert.That(hud.SelectFighter(hidden.ParticipantId), Is.False);
            Assert.That(selectedParticipant, Is.Zero);
            Assert.That(
                controller.TryEvaluateFocusedAbility(
                    AbilityId.Frostbolt,
                    out ParticipantId fallbackTarget,
                    out AbilityReadinessState fallbackState,
                    out string fallbackReason),
                Is.True,
                "Rejecting a hidden roster entry must preserve the valid fallback target.");
            Assert.That(fallbackTarget.Value, Is.EqualTo(originalFocus));
            Assert.That(fallbackState, Is.Not.EqualTo(AbilityReadinessState.NoTarget));
            Assert.That(fallbackReason, Is.Not.EqualTo("SELECT TARGET"));

            Assert.That(
                hud.SetFighterHidden(new ParticipantId(hidden.ParticipantId), false),
                Is.True);
            Assert.That(
                hud.PresentFighter(
                    hidden.ParticipantId,
                    73f,
                    100f,
                    0f,
                    string.Empty,
                    0f,
                    string.Empty,
                    true,
                    false),
                Is.True);
            Assert.That(hidden.IsHidden, Is.False);
            Assert.That(ReadChildText(hidden.transform, "Relation"), Is.EqualTo("ENEMY"));
            Assert.That(ReadChildText(hidden.transform, "Health"), Is.EqualTo("73%"));
            Assert.That(hiddenButton.interactable, Is.True);
            Assert.That(hiddenCanvas.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hiddenCanvas.blocksRaycasts, Is.True);
            Assert.That(IsSelectorSelected(hidden), Is.False);
        }

        [UnityTest]
        public IEnumerator Arena_WorldTapFocusesHeroAndCameraDragOrbitsWithoutRetargeting()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            WorldTapSurface worldSurface = Object.FindAnyObjectByType<WorldTapSurface>();
            SoftTargetSelectionService targeting =
                Object.FindAnyObjectByType<SoftTargetSelectionService>();
            Camera camera = Object.FindAnyObjectByType<Camera>();
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(worldSurface, Is.Not.Null);
            Assert.That(targeting, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(eventSystem, Is.Not.Null);

            RectTransform worldRect = worldSurface.GetComponent<RectTransform>();
            Assert.That(worldRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(worldRect.anchorMax, Is.EqualTo(Vector2.one));

            CombatantView[] views =
                Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
            CombatantView ally = views.Single(value => value.Team.Value == 1 && !value.IsHuman);
            CombatantView enemy = views.Single(value =>
                value.Team.Value == 2 && !value.IsHiddenFromViewer);
            Physics.SyncTransforms();

            Assert.That(
                TryFindUnobstructedWorldTargetPoint(
                    camera,
                    eventSystem,
                    worldSurface,
                    targeting,
                    enemy,
                    out Vector2 enemyScreen),
                Is.True,
                "The visible enemy needs at least one screen point that is not covered " +
                "by HUD controls and still resolves through world targeting.");
            PointerEventData tap = new PointerEventData(eventSystem)
            {
                pointerId = 71,
                button = PointerEventData.InputButton.Left,
                position = enemyScreen,
            };
            List<RaycastResult> uiHits = new List<RaycastResult>();
            eventSystem.RaycastAll(tap, uiHits);
            Assert.That(uiHits, Is.Not.Empty);
            Assert.That(
                uiHits[0].gameObject,
                Is.EqualTo(worldSurface.gameObject),
                "Verified blank UI space must reach the full-screen world-tap surface.");
            Assert.That(
                targeting.TryGetScreenCandidate(
                    tap.position,
                    out SoftTargetCandidate tappedCandidate),
                Is.True);
            Assert.That(tappedCandidate.TargetId, Is.EqualTo(enemy.Id.Value));
            worldSurface.OnPointerDown(tap);
            worldSurface.OnPointerUp(tap);
            yield return null;

            Assert.That(hud.FocusedFighterId, Is.EqualTo(enemy.Id.Value));
            Assert.That(enemy.transform.Find("TargetRing").gameObject.activeSelf, Is.True);
            Assert.That(enemy.transform.Find("WorldStatusBars/HealthTrack"), Is.Not.Null);
            Assert.That(
                enemy.GetComponentInChildren<CombatantRelationMarker>(true)
                    .transform.Find("StateLabel")
                    .GetComponent<TextMesh>().text,
                Is.Empty,
                "World labels should stay quiet unless an actionable combat state is active.");

            StrategyFollowCamera cameraRig = camera.GetComponent<StrategyFollowCamera>();
            cameraRig.SnapToTarget();
            float cameraYawBeforeDrag = cameraRig.Yaw;

            Vector3 allyScreen = camera.WorldToScreenPoint(ally.AimPoint.position);
            PointerEventData drag = new PointerEventData(eventSystem)
            {
                pointerId = 72,
                button = PointerEventData.InputButton.Left,
                position = allyScreen,
            };
            worldSurface.OnPointerDown(drag);
            drag.delta = new Vector2(80f, 0f);
            drag.position += drag.delta;
            worldSurface.OnDrag(drag);
            worldSurface.OnPointerUp(drag);
            yield return null;

            Assert.That(
                hud.FocusedFighterId,
                Is.EqualTo(enemy.Id.Value),
                "Dragging across a hero must not change focus.");
            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(cameraYawBeforeDrag, cameraRig.Yaw)),
                Is.GreaterThan(0.1f),
                "A horizontal world drag must orbit the presentation camera.");
        }

        [UnityTest]
        public IEnumerator Arena_AbilitiesUseOnlyTheExplicitlyFocusedHero()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            CombatantView[] views =
                Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
            CombatantView ally = views.Single(value => value.Team.Value == 1 && !value.IsHuman);
            CombatantView enemy = views.First(value =>
                value.Team.Value == 2 && !value.IsHiddenFromViewer);

            Assert.That(hud.SelectFighter(ally.Id), Is.True);
            Assert.That(
                controller.TryResolveFocusedTargetForAbility(
                    AbilityId.Frostbolt,
                    out ParticipantId invalidEnemyTarget),
                Is.False,
                "A hostile spell must not silently replace an explicitly selected ally.");
            Assert.That(invalidEnemyTarget.IsValid, Is.False);
            Assert.That(
                controller.TryEvaluateFocusedAbility(
                    AbilityId.Frostbolt,
                    out ParticipantId evaluatedWrongRelation,
                    out AbilityReadinessState wrongRelationState,
                    out string wrongRelationReason),
                Is.False);
            Assert.That(evaluatedWrongRelation.IsValid, Is.False);
            Assert.That(wrongRelationState, Is.EqualTo(AbilityReadinessState.NoTarget));
            Assert.That(wrongRelationReason, Is.EqualTo("SELECT TARGET"));

            Assert.That(hud.SelectFighter(enemy.Id), Is.True);
            Assert.That(
                controller.TryResolveFocusedTargetForAbility(
                    AbilityId.Frostbolt,
                    out ParticipantId selectedEnemy),
                Is.True);
            Assert.That(selectedEnemy, Is.EqualTo(enemy.Id));

            Assert.That(
                controller.TryResolveFocusedTargetForAbility(
                    AbilityId.IceBarrier,
                    out ParticipantId selfTarget),
                Is.True);
            Assert.That(selfTarget.Value, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Arena_AbilityButtonCastsOnExactlyTheTappedEnemy()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return new WaitForSeconds(3.2f);

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            CombatantView[] visibleEnemies = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Where(value => value.Team.Value == 2 && !value.IsHiddenFromViewer)
                .OrderBy(value => value.Id.Value)
                .ToArray();
            Assert.That(visibleEnemies, Has.Length.GreaterThanOrEqualTo(1));
            CombatantView selectedEnemy = visibleEnemies[0];

            CombatantStatusView selectedCircle = Object
                .FindObjectsByType<CombatantStatusView>(FindObjectsInactive.Include)
                .Single(value => value.IsBound && value.ParticipantId == selectedEnemy.Id.Value);
            selectedCircle.GetComponent<Button>().onClick.Invoke();
            Assert.That(hud.FocusedFighterId, Is.EqualTo(selectedEnemy.Id.Value));

            AbilitySlotView slotView = hud.GetAbilitySlot(3);
            Assert.That(slotView.AbilityId, Is.EqualTo(AbilityId.IceLance));
            AbilityButton abilityButton = slotView.GetComponent<AbilityButton>();
            Assert.That(abilityButton, Is.Not.Null);
            PointerEventData pointer = new PointerEventData(
                Object.FindAnyObjectByType<EventSystem>())
            {
                pointerId = 901,
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    slotView.transform.position),
            };

            int previousAcceptedCount = controller.AcceptedHumanCastCount;
            abilityButton.OnPointerDown(pointer);
            abilityButton.OnPointerUp(pointer);

            float timeout = Time.realtimeSinceStartup + 0.5f;
            while (controller.AcceptedHumanCastCount == previousAcceptedCount &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                controller.TryGetLastAcceptedHumanCast(out CombatEvent acceptedCast),
                Is.True);
            Assert.That(acceptedCast.Kind, Is.EqualTo(CombatEventKind.CastAccepted));
            Assert.That(acceptedCast.Actor.Value, Is.EqualTo(1));
            Assert.That(acceptedCast.Ability, Is.EqualTo(AbilityId.IceLance));
            Assert.That(
                acceptedCast.Target,
                Is.EqualTo(selectedEnemy.Id),
                "The ability button must keep the exact tapped focus instead of selecting another enemy.");
        }

        [UnityTest]
        public IEnumerator Arena_FlickUpCastsPrayerOfMendingOnExactlyTheSelectedAlly()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Healer,
                SpecializationId.Discipline);
            yield return new WaitForSeconds(3.2f);

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            CombatantView selectedAlly = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.Team.Value == 1 && !value.IsHuman);
            Assert.That(hud.SelectFighter(selectedAlly.Id), Is.True);

            AbilitySlotView slotView = hud.GetAbilitySlot(0);
            Assert.That(slotView.AbilityId, Is.EqualTo(AbilityId.Renew));
            Assert.That(
                slotView.AlternateUpAbilityId,
                Is.EqualTo(AbilityId.PrayerOfMending));
            Assert.That(slotView.IsAlternateUpVisible, Is.False);
            Image alternateIcon = slotView.transform
                .Find("AlternateUp/Icon")
                .GetComponent<Image>();
            AbilityGlyphGraphic alternateGlyph = slotView.transform
                .Find("AlternateUp/Glyph")
                .GetComponent<AbilityGlyphGraphic>();
            Assert.That(alternateIcon.enabled, Is.True);
            Assert.That(alternateIcon.sprite, Is.Not.Null);
            Assert.That(alternateIcon.color.a, Is.GreaterThan(0f));
            Assert.That(
                alternateGlyph.enabled,
                Is.False,
                "Every playable swipe action now requires painted catalog art instead of a vector fallback.");

            AbilityButton abilityButton = slotView.GetComponent<AbilityButton>();
            PointerEventData pointer = new PointerEventData(
                Object.FindAnyObjectByType<EventSystem>())
            {
                pointerId = 902,
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    slotView.transform.position),
            };

            int previousAcceptedCount = controller.AcceptedHumanCastCount;
            abilityButton.OnPointerDown(pointer);
            Assert.That(slotView.IsAlternateUpVisible, Is.True);
            pointer.position += new Vector2(0f, 60f);
            abilityButton.OnDrag(pointer);
            Assert.That(slotView.IsAlternateUpSelected, Is.True);
            Assert.That(slotView.VisibleCaption, Is.EqualTo("PRAYER OF MENDING"));
            abilityButton.OnPointerUp(pointer);

            float timeout = Time.realtimeSinceStartup + 0.5f;
            while (controller.AcceptedHumanCastCount == previousAcceptedCount &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                controller.TryGetLastAcceptedHumanCast(out CombatEvent acceptedCast),
                Is.True);
            Assert.That(acceptedCast.Kind, Is.EqualTo(CombatEventKind.CastAccepted));
            Assert.That(acceptedCast.Actor.Value, Is.EqualTo(1));
            Assert.That(acceptedCast.Ability, Is.EqualTo(AbilityId.PrayerOfMending));
            Assert.That(acceptedCast.Target, Is.EqualTo(selectedAlly.Id));
            Assert.That(slotView.VisibleCaption, Is.EqualTo("RENEW"));
        }

        [UnityTest]
        public IEnumerator Arena_FocusedEnemyPersistsWhenWorldMarkerRejectsAndExplainsRange()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Rogue,
                SpecializationId.Mutilate);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            SoftTargetSelectionService targeting =
                Object.FindAnyObjectByType<SoftTargetSelectionService>();
            CombatantView firstEnemy = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.Id.Value == 4);
            CombatantView selectedEnemyView = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.Id.Value == 3);

            Assert.That(hud.SelectFighter(firstEnemy.Id), Is.True);
            Assert.That(targeting.CurrentTarget, Is.Not.Null);
            Assert.That(targeting.CurrentTarget.TargetId, Is.EqualTo(firstEnemy.Id.Value));
            Assert.That(targeting.CurrentTargetWasExplicitlySelected, Is.True);

            System.Reflection.FieldInfo maximumDistanceField =
                typeof(SoftTargetSelectionService).GetField(
                    "_maxDistance",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.That(maximumDistanceField, Is.Not.Null);
            maximumDistanceField.SetValue(targeting, 0.1f);

            Assert.That(hud.SelectFighter(selectedEnemyView.Id), Is.True);
            Assert.That(hud.FocusedFighterId, Is.EqualTo(selectedEnemyView.Id.Value));
            Assert.That(
                targeting.CurrentTarget,
                Is.Null,
                "A rejected world marker must not leave the previously selected enemy behind.");

            Assert.That(
                controller.TryEvaluateFocusedAbility(
                    AbilityId.Mutilate,
                    out ParticipantId selectedEnemy,
                    out AbilityReadinessState state,
                    out string reason),
                Is.True);
            Assert.That(selectedEnemy, Is.EqualTo(selectedEnemyView.Id));
            Assert.That(state, Is.EqualTo(AbilityReadinessState.OutOfRange));
            Assert.That(reason, Is.EqualTo("RANGE"));

            targeting.RefreshNow();
            Assert.That(targeting.CurrentTarget, Is.Null);
            Assert.That(hud.FocusedFighterId, Is.EqualTo(selectedEnemyView.Id.Value));
        }

        [UnityTest]
        public IEnumerator Arena_SelfAreaAndMobilityRemainUsableWithoutEnemyFocus()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            CombatantView human = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.IsHuman);
            Assert.That(hud.SelectFighter(human.Id), Is.True);

            AssertAbilityReadyWithoutEnemy(
                controller,
                AbilityId.IceBarrier,
                human.Id);
            AssertAbilityReadyWithoutEnemy(
                controller,
                AbilityId.FrostNova,
                default);
            AssertAbilityReadyWithoutEnemy(
                controller,
                AbilityId.Blink,
                default);
        }

        [UnityTest]
        public IEnumerator Arena_BindsAllTenControlsFromTheSelectedSpecialization()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Rogue,
                SpecializationId.Mutilate);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);
            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView, Is.Not.Null, "Missing authored action slot " + slot + ".");
                Assert.That(slotView.gameObject.activeInHierarchy, Is.True);
                Assert.That(
                    slotView.AbilityId,
                    Is.EqualTo(AbilityCatalog.GetSpecializationAbility(
                        SpecializationId.Mutilate,
                        slot)));
                AssertPrimaryActionIdentity(slotView, slotView.AbilityId);
                Assert.That(AbilityCatalog.TryGet(slotView.AbilityId, out AbilityDefinition definition));
                Assert.That(
                    slotView.VisibleCaption,
                    Is.Not.Empty,
                    "Touch captions must remain readable keywords rather than long spell names.");
            }
        }

        [UnityTest]
        public IEnumerator Arena_RebindingFromRogueToMageReplacesDirectionalSatellite()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Rogue,
                SpecializationId.ShadowDance);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            AbilitySlotView rogueSlot = hud.GetAbilitySlot(1);
            Assert.That(rogueSlot.AbilityId, Is.EqualTo(AbilityId.Shadowstep));
            Assert.That(
                rogueSlot.GetDirectionalAbility(AbilitySlotDirection.Left),
                Is.EqualTo(AbilityId.Evasion));

            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return new WaitForFixedUpdate();
            yield return null;

            AbilitySlotView mageSlot = hud.GetAbilitySlot(1);
            Assert.That(mageSlot.AbilityId, Is.EqualTo(AbilityId.IceBarrier));
            Assert.That(
                mageSlot.GetComponent<AbilityButton>().GetDirectionalAbility(
                    AbilityFlickDirection.Left),
                Is.EqualTo(AbilityId.Blink));
        }

        [UnityTest]
        public IEnumerator GameplayHud_AuthorsUnifiedGestureDiscsInTwoFourFourHierarchy()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Fire);
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(AbilityCatalog.AbilitySlotCount, Is.EqualTo(10));
            Assert.That(AbilityKit.GestureSlotCount, Is.EqualTo(2));
            Assert.That(AbilityKit.CommonSlotCount, Is.EqualTo(4));
            Assert.That(AbilityKit.UtilitySlotCount, Is.EqualTo(4));
            Assert.That(AbilityKit.MaximumActionCount, Is.EqualTo(18));

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            CanvasScaler canvasScaler = hud.GetComponent<CanvasScaler>();
            Assert.That(canvasScaler, Is.Not.Null);
            Assert.That(
                canvasScaler.referenceResolution,
                Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(canvasScaler.matchWidthOrHeight, Is.EqualTo(1f));
            RectTransform gameplay = (RectTransform)hud.transform.Find("SafeArea/Gameplay");
            Assert.That(gameplay, Is.Not.Null);

            RectTransform[] slots = new RectTransform[AbilityCatalog.AbilitySlotCount];
            Vector2[] expectedPositions =
            {
                new Vector2(-240f, 292f),
                new Vector2(-564f, 292f),
                new Vector2(-182f, 526f),
                new Vector2(-326f, 535f),
                new Vector2(-474f, 537f),
                new Vector2(-676f, 495f),
                new Vector2(-156f, 74f),
                new Vector2(-52f, 74f),
                new Vector2(52f, 74f),
                new Vector2(156f, 74f),
            };
            float[] expectedTouchDiameters =
            {
                301.6f, 301.6f,
                132f, 132f, 132f, 132f,
                92f, 92f, 92f, 92f,
            };
            float[] centeredVisualRadii =
            {
                // The gesture guide is the largest centered layer. Common and
                // utility ReadyPulse rings are their largest centered layers.
                150.8f, 150.8f,
                72f, 72f, 72f, 72f,
                52f, 52f, 52f, 52f,
            };
            float[] shadowVisualRadii =
            {
                // ClusterShadow is 242 local px, enlarged with its entire
                // gesture root; direct MedallionShadow remains size + 10.
                157.3f, 157.3f,
                71f, 71f, 71f, 71f,
                51f, 51f, 51f, 51f,
            };
            Vector2[] shadowOffsets =
            {
                new Vector2(0f, -6.5f), new Vector2(0f, -6.5f),
                new Vector2(0f, -5f), new Vector2(0f, -5f),
                new Vector2(0f, -5f), new Vector2(0f, -5f),
                new Vector2(0f, -5f), new Vector2(0f, -5f),
                new Vector2(0f, -5f), new Vector2(0f, -5f),
            };
            var authoredSlotViews = new HashSet<AbilitySlotView>();
            Sprite sharedMedallionFrameSprite = null;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView, Is.Not.Null, "Missing ability touch target " + slot + ".");
                Assert.That(slotView.name, Is.EqualTo("Ability_" + (slot + 1)));
                Assert.That(authoredSlotViews.Add(slotView), Is.True);
                RectTransform slotRect = (RectTransform)slotView.transform;
                slots[slot] = slotRect;

                bool isGestureControl = slot < AbilityKit.GestureSlotCount;
                float expectedControlScale = isGestureControl ? 1.3f : 1f;
                float expectedSize = isGestureControl ? 232f : expectedTouchDiameters[slot];
                float expectedIconSize = slot < AbilityKit.GestureSlotCount
                    ? 48f
                    : slot < AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount
                        ? 82f
                        : 56f;
                Vector2 expectedAnchor = slot <
                    AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount
                        ? new Vector2(1f, 0f)
                        : new Vector2(0.5f, 0f);
                Assert.That(
                    slotRect.anchoredPosition,
                    Is.EqualTo(expectedPositions[slot]),
                    "Ability " + slot + " has the wrong compact-cluster position.");
                Assert.That(
                    slotRect.anchorMin,
                    Is.EqualTo(expectedAnchor),
                    "Ability " + slot + " has the wrong role-specific anchor.");
                Assert.That(slotRect.anchorMax, Is.EqualTo(expectedAnchor));
                Assert.That(
                    slotRect.localScale,
                    Is.EqualTo(new Vector3(expectedControlScale, expectedControlScale, 1f)),
                    "Only the two large gesture controls should grow by 30%.");
                Rect renderedTouchRect = GetRectRelativeTo(gameplay, slotRect);
                Assert.That(renderedTouchRect.width,
                    Is.EqualTo(expectedTouchDiameters[slot]).Within(0.01f));
                Assert.That(renderedTouchRect.height,
                    Is.EqualTo(expectedTouchDiameters[slot]).Within(0.01f));
                Assert.That(
                    slotRect.rect.width,
                    Is.EqualTo(expectedSize).Within(0.01f),
                    "Ability " + slot + " has the wrong role-specific touch width.");
                Assert.That(
                    slotRect.rect.height,
                    Is.EqualTo(expectedSize).Within(0.01f),
                    "Ability " + slot + " has the wrong role-specific touch height.");

                RectTransform gestureTouchSurface = (RectTransform)slotRect.Find(
                    "GestureTouchSurface");
                RectTransform primaryMedallion = (RectTransform)slotRect.Find(
                    "PrimaryMedallion");
                if (isGestureControl)
                {
                    Assert.That(gestureTouchSurface, Is.Not.Null);
                    Assert.That(
                        gestureTouchSurface.rect.size,
                        Is.EqualTo(new Vector2(232f, 232f)));
                    Assert.That(gestureTouchSurface.anchoredPosition, Is.EqualTo(Vector2.zero));
                    Assert.That(gestureTouchSurface.anchorMin, Is.EqualTo(Vector2.zero));
                    Assert.That(gestureTouchSurface.anchorMax, Is.EqualTo(Vector2.one));
                    Image gestureInput = gestureTouchSurface.GetComponent<Image>();
                    Assert.That(gestureInput, Is.Not.Null);
                    Assert.That(gestureInput.raycastTarget, Is.True);
                    Assert.That(
                        gestureTouchSurface.GetComponents<CircularRaycastFilter>(),
                        Has.Length.EqualTo(1),
                        "The transparent square Graphic needs one circular hit filter.");
                    Assert.That(
                        gestureInput.color.a,
                        Is.EqualTo(0.001f).Within(0.0001f),
                        "The generous grouped-action hit target must remain visually transparent.");
                    RectTransform clusterShadow = (RectTransform)slotRect.Find(
                        "ClusterShadow");
                    RectTransform clusterBacking = (RectTransform)slotRect.Find(
                        "ClusterBacking");
                    RectTransform clusterOuterBezel = (RectTransform)slotRect.Find(
                        "ClusterOuterBezel");
                    RectTransform clusterOuterHighlight = (RectTransform)slotRect.Find(
                        "ClusterOuterHighlight");
                    RectTransform clusterAccentRing = (RectTransform)slotRect.Find(
                        "ClusterAccentRing");
                    Assert.That(clusterShadow, Is.Not.Null);
                    Assert.That(clusterBacking, Is.Not.Null);
                    Assert.That(clusterOuterBezel, Is.Not.Null);
                    Assert.That(clusterOuterHighlight, Is.Not.Null);
                    Assert.That(clusterAccentRing, Is.Not.Null);
                    Assert.That(
                        clusterShadow.rect.size,
                        Is.EqualTo(new Vector2(242f, 242f)));
                    Assert.That(
                        clusterShadow.anchoredPosition,
                        Is.EqualTo(new Vector2(0f, -5f)));
                    Assert.That(
                        clusterShadow.GetComponent<Image>().raycastTarget,
                        Is.False);
                    Image clusterBackingImage = clusterBacking.GetComponent<Image>();
                    Assert.That(
                        clusterBacking.rect.size,
                        Is.EqualTo(new Vector2(224f, 224f)));
                    Assert.That(clusterBackingImage.raycastTarget, Is.False);
                    Assert.That(clusterBackingImage.color.a, Is.GreaterThan(0.98f));
                    Assert.That(
                        Mathf.Max(
                            clusterBackingImage.color.r,
                            Mathf.Max(
                                clusterBackingImage.color.g,
                                clusterBackingImage.color.b)),
                        Is.LessThan(0.08f),
                        "The shared gesture disc needs an opaque dark backing.");
                    Assert.That(
                        clusterOuterBezel.rect.size,
                        Is.EqualTo(new Vector2(230f, 230f)));
                    Assert.That(
                        clusterOuterHighlight.rect.size,
                        Is.EqualTo(new Vector2(222f, 222f)));
                    Assert.That(
                        clusterAccentRing.rect.size,
                        Is.EqualTo(new Vector2(214f, 214f)));
                    Assert.That(
                        clusterOuterBezel.GetComponent<AbilityRingGraphic>().raycastTarget,
                        Is.False);
                    Assert.That(
                        clusterOuterHighlight.GetComponent<AbilityRingGraphic>().raycastTarget,
                        Is.False);
                    AbilityRingGraphic clusterAccent =
                        clusterAccentRing.GetComponent<AbilityRingGraphic>();
                    Assert.That(clusterAccent.raycastTarget, Is.False);
                    Assert.That(clusterAccent.Thickness, Is.EqualTo(2.25f).Within(0.01f));
                    Assert.That(clusterAccent.color.a, Is.EqualTo(0.58f).Within(0.01f));
                    Assert.That(clusterOuterBezel.GetComponent<Image>(), Is.Null);
                    Assert.That(clusterOuterHighlight.GetComponent<Image>(), Is.Null);
                    Assert.That(clusterAccentRing.GetComponent<Image>(), Is.Null);

                    string[] cardinalTickNames =
                    {
                        "ClusterCardinalTickUp",
                        "ClusterCardinalTickDown",
                        "ClusterCardinalTickLeft",
                        "ClusterCardinalTickRight",
                    };
                    Vector2[] cardinalTickPositions =
                    {
                        new Vector2(0f, 105f),
                        new Vector2(0f, -105f),
                        new Vector2(-105f, 0f),
                        new Vector2(105f, 0f),
                    };
                    Vector2[] cardinalTickSizes =
                    {
                        new Vector2(16f, 4f),
                        new Vector2(16f, 4f),
                        new Vector2(4f, 16f),
                        new Vector2(4f, 16f),
                    };
                    for (int tickIndex = 0;
                         tickIndex < cardinalTickNames.Length;
                         tickIndex++)
                    {
                        RectTransform tick = (RectTransform)slotRect.Find(
                            cardinalTickNames[tickIndex]);
                        Assert.That(tick, Is.Not.Null, cardinalTickNames[tickIndex]);
                        Assert.That(
                            tick.anchoredPosition,
                            Is.EqualTo(cardinalTickPositions[tickIndex]));
                        Assert.That(tick.rect.size, Is.EqualTo(cardinalTickSizes[tickIndex]));
                        Image tickImage = tick.GetComponent<Image>();
                        Assert.That(tickImage.raycastTarget, Is.False);
                        Assert.That(tickImage.color.a, Is.EqualTo(0.56f).Within(0.01f));
                    }
                    Assert.That(primaryMedallion, Is.Not.Null);
                    Assert.That(
                        primaryMedallion.rect.size,
                        Is.EqualTo(new Vector2(84f, 84f)));
                    Assert.That(primaryMedallion.anchoredPosition, Is.EqualTo(Vector2.zero));
                    Assert.That(primaryMedallion.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(
                        GetRectRelativeTo(gameplay, primaryMedallion).width,
                        Is.EqualTo(109.2f).Within(0.01f),
                        "The center primary must grow together with the outer disc.");
                    Assert.That(
                        GetRectRelativeTo(gameplay,
                            (RectTransform)primaryMedallion.Find("Icon")).width,
                        Is.EqualTo(62.4f).Within(0.01f),
                        "The request enlarges the actual readable icon, not just its empty hit area.");
                }
                else
                {
                    Assert.That(gestureTouchSurface, Is.Null);
                    Assert.That(primaryMedallion, Is.Null);
                    primaryMedallion = slotRect;
                }

                RectTransform visualRoot = primaryMedallion;
                float expectedMedallionSize = isGestureControl ? 84f : expectedSize;
                RectTransform outerBezel =
                    (RectTransform)visualRoot.Find("OuterBezel");
                RectTransform bezelHighlight =
                    (RectTransform)visualRoot.Find("BezelHighlight");
                RectTransform accentRing =
                    (RectTransform)visualRoot.Find("AccentRing");
                RectTransform innerBezel =
                    (RectTransform)visualRoot.Find("InnerBezel");
                RectTransform rootOutline =
                    (RectTransform)visualRoot.Find("RootOutline");
                RectTransform readyPulse =
                    (RectTransform)visualRoot.Find("ReadyPulse");
                RectTransform rootSurface =
                    (RectTransform)visualRoot.Find("RootSurface");
                RectTransform medallionShadow =
                    (RectTransform)visualRoot.Find("MedallionShadow");
                RectTransform backplate =
                    (RectTransform)visualRoot.Find("Backplate");
                RectTransform glassSheen =
                    (RectTransform)visualRoot.Find("GlassSheen");
                RectTransform captionPlate =
                    (RectTransform)visualRoot.Find("CaptionPlate");
                RectTransform interactionHalo =
                    (RectTransform)visualRoot.Find("InteractionHalo");
                RectTransform medallionFrameArt =
                    (RectTransform)visualRoot.Find("MedallionFrameArt");
                Assert.That(outerBezel, Is.Not.Null);
                Assert.That(bezelHighlight, Is.Not.Null);
                Assert.That(accentRing, Is.Not.Null);
                Assert.That(innerBezel, Is.Not.Null);
                Assert.That(rootOutline, Is.Not.Null);
                Assert.That(readyPulse, Is.Not.Null);
                Assert.That(rootSurface, Is.Not.Null);
                Assert.That(medallionShadow, Is.Not.Null);
                Assert.That(backplate, Is.Not.Null);
                Assert.That(glassSheen, Is.Not.Null);
                Assert.That(captionPlate, Is.Not.Null);
                Assert.That(interactionHalo, Is.Not.Null);
                Assert.That(medallionFrameArt, Is.Not.Null);
                Assert.That(outerBezel.rect.width, Is.LessThan(expectedMedallionSize));
                Assert.That(bezelHighlight.rect.width, Is.LessThan(outerBezel.rect.width));
                Assert.That(accentRing.rect.width, Is.LessThan(bezelHighlight.rect.width));
                Assert.That(innerBezel.rect.width, Is.LessThan(accentRing.rect.width));
                Assert.That(backplate.rect.width, Is.LessThan(innerBezel.rect.width));
                Assert.That(interactionHalo.rect.width, Is.LessThan(innerBezel.rect.width));
                Assert.That(interactionHalo.rect.width, Is.GreaterThan(backplate.rect.width));
                Assert.That(visualRoot.GetComponent<Image>(), Is.Null);
                Assert.That(rootSurface.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(
                    rootSurface.GetComponents<CircularRaycastFilter>(),
                    Has.Length.EqualTo(1),
                    "Every visible ability face must use a circular hit filter.");
                RectTransform[] hollowRingTransforms =
                {
                    rootOutline,
                    readyPulse,
                    outerBezel,
                    bezelHighlight,
                    accentRing,
                    innerBezel,
                    interactionHalo,
                };
                for (int ringIndex = 0;
                     ringIndex < hollowRingTransforms.Length;
                     ringIndex++)
                {
                    RectTransform ringTransform = hollowRingTransforms[ringIndex];
                    AbilityRingGraphic ring =
                        ringTransform.GetComponent<AbilityRingGraphic>();
                    Assert.That(ring, Is.Not.Null, ringTransform.name);
                    Assert.That(ring.raycastTarget, Is.False, ringTransform.name);
                    Assert.That(
                        ring.Segments,
                        Is.EqualTo(96),
                        ringTransform.name +
                        " must use smooth complete-circle geometry at HUD scale.");
                    Assert.That(ring.Thickness, Is.GreaterThan(0f));
                    Assert.That(
                        ring.Thickness,
                        Is.LessThan(Mathf.Min(
                            ringTransform.rect.width,
                            ringTransform.rect.height) * 0.5f),
                        ringTransform.name + " must retain a transparent center.");
                    Assert.That(
                        ringTransform.GetComponent<Image>(),
                        Is.Null,
                        ringTransform.name + " must be hollow geometry, not a filled disc.");
                }
                Assert.That(
                    rootOutline.GetComponent<Graphic>().color.a,
                    Is.GreaterThanOrEqualTo(0.95f),
                    "RootOutline must remain a complete, visible outer circle.");
                Assert.That(
                    outerBezel.GetComponent<Graphic>().color.a,
                    Is.EqualTo(0.96f).Within(0.005f),
                    "OuterBezel must provide the dark-iron circular frame.");
                Assert.That(
                    bezelHighlight.GetComponent<Graphic>().color.a,
                    Is.EqualTo(0.58f).Within(0.005f),
                    "BezelHighlight must shape the idle circular frame.");
                Assert.That(
                    accentRing.GetComponent<Graphic>().color.a,
                    Is.InRange(0.78f, 0.96f),
                    "AccentRing must carry the ready or readiness-state color around the full circle.");
                Assert.That(
                    accentRing.GetComponent<AbilityRingGraphic>().Thickness,
                    Is.EqualTo(5f).Within(0.01f),
                    "The mobile action ring needs a strong five-pixel accent.");
                Assert.That(
                    innerBezel.GetComponent<Graphic>().color.a,
                    Is.EqualTo(0.92f).Within(0.005f),
                    "InnerBezel must finish the circular frame without filling it.");
                Assert.That(
                    readyPulse.GetComponent<Graphic>().color.a,
                    Is.Zero.Within(0.001f),
                    "ReadyPulse is transient and must be clear at idle.");
                Assert.That(
                    interactionHalo.GetComponent<Graphic>().color.a,
                    Is.Zero.Within(0.001f),
                    "InteractionHalo is transient and must be clear at idle.");
                Color backplateColor = backplate.GetComponent<Image>().color;
                Assert.That(
                    backplateColor.a,
                    Is.EqualTo(0.94f).Within(0.005f),
                    "The reference-style medallion needs a dark jewel well behind true-alpha art.");
                float backplateChannelRange = Mathf.Max(
                        backplateColor.r,
                        Mathf.Max(backplateColor.g, backplateColor.b)) -
                    Mathf.Min(
                        backplateColor.r,
                        Mathf.Min(backplateColor.g, backplateColor.b));
                Assert.That(
                    backplateChannelRange,
                    Is.GreaterThan(0.025f),
                    "The jewel well must retain a restrained ability-color tint.");
                Image authoredIcon = visualRoot.Find("Icon").GetComponent<Image>();
                Assert.That(authoredIcon, Is.Not.Null);
                Assert.That(authoredIcon.preserveAspect, Is.True);
                Assert.That(authoredIcon.raycastTarget, Is.False);
                Assert.That(
                    authoredIcon.rectTransform.rect.width,
                    Is.LessThan(innerBezel.rect.width),
                    "The full icon silhouette must sit inside, not cover, its circular frame.");
                Assert.That(
                    authoredIcon.rectTransform.rect.width,
                    Is.EqualTo(expectedIconSize).Within(0.01f));
                Assert.That(
                    visualRoot.Find("Glyph").GetComponent<RectTransform>().rect.width,
                    Is.EqualTo(expectedIconSize).Within(0.01f));
                Image frameArt = medallionFrameArt.GetComponent<Image>();
                Assert.That(frameArt, Is.Not.Null);
                Assert.That(frameArt.sprite, Is.Not.Null);
                Assert.That(frameArt.type, Is.EqualTo(Image.Type.Simple));
                Assert.That(frameArt.preserveAspect, Is.True);
                Assert.That(frameArt.raycastTarget, Is.False);
                Assert.That(frameArt.color, Is.EqualTo(Color.white));
                Assert.That(
                    medallionFrameArt.rect.size,
                    Is.EqualTo(new Vector2(expectedMedallionSize, expectedMedallionSize)));
                Assert.That(
                    medallionFrameArt.GetSiblingIndex(),
                    Is.GreaterThan(visualRoot.Find("Glyph").GetSiblingIndex()),
                    "The authored frame must finish the icon edge.");
                Assert.That(
                    medallionFrameArt.GetSiblingIndex(),
                    Is.GreaterThan(visualRoot.Find("Cooldown").GetSiblingIndex()),
                    "Cooldown shading must remain under the forged aperture.");
                Assert.That(
                    medallionFrameArt.GetSiblingIndex(),
                    Is.GreaterThan(visualRoot.Find("GlobalCooldown").GetSiblingIndex()),
                    "GCD shading must remain under the forged aperture.");
                Assert.That(
                    medallionFrameArt.GetSiblingIndex(),
                    Is.LessThan(captionPlate.GetSiblingIndex()),
                    "Labels must remain above the decorative frame.");
                if (sharedMedallionFrameSprite == null)
                {
                    sharedMedallionFrameSprite = frameArt.sprite;
                }
                else
                {
                    Assert.That(
                        frameArt.sprite,
                        Is.SameAs(sharedMedallionFrameSprite),
                        "Every class and slot must reuse one imported frame Sprite.");
                }
                int iconSiblingIndex = authoredIcon.transform.GetSiblingIndex();
                Image[] filledImagesBehindIcon = visualRoot
                    .GetComponentsInChildren<Image>(true)
                    .Where(image => image.transform == visualRoot ||
                        (image.transform.parent == visualRoot &&
                         image.transform.GetSiblingIndex() < iconSiblingIndex))
                    .Where(image => !image.name.StartsWith("Stud_"))
                    .ToArray();
                Assert.That(
                    filledImagesBehindIcon.Select(image => image.name),
                    Is.EquivalentTo(new[]
                    {
                        "MedallionShadow",
                        "RootSurface",
                        "Backplate",
                        "IconGlow",
                        "GlassSheen",
                    }),
                    "The shared forged frame must use only its semantic shadow, input face, jewel well, bloom, and sheen behind the icon.");
                Assert.That(
                    filledImagesBehindIcon.Single(image => image.name == "MedallionShadow")
                        .color.a,
                    Is.InRange(0.6f, 0.72f));
                Assert.That(
                    filledImagesBehindIcon.Single(image => image.name == "RootSurface")
                        .color.a,
                    Is.GreaterThanOrEqualTo(0.95f));
                Assert.That(
                    filledImagesBehindIcon.Single(image => image.name == "IconGlow")
                        .color.a,
                    Is.InRange(0.08f, 0.18f));
                Assert.That(
                    filledImagesBehindIcon.Single(image => image.name == "GlassSheen")
                        .color.a,
                    Is.InRange(0.04f, 0.08f));
                Assert.That(
                    filledImagesBehindIcon
                        .Where(image => image.name != "RootSurface")
                        .All(image => !image.raycastTarget),
                    Is.True,
                    "Only the full circular input surface may receive ability taps.");
                Assert.That(
                    slotRect.GetComponentsInChildren<Mask>(true),
                    Is.Empty,
                    "True-alpha ability art must not be circularly cropped.");
                Image[] obsoleteProceduralStuds = visualRoot
                    .GetComponentsInChildren<Image>(true)
                    .Where(image => image.transform.parent == visualRoot &&
                        image.name.StartsWith("Stud_"))
                    .ToArray();
                Assert.That(
                    obsoleteProceduralStuds,
                    Is.Empty,
                    "The authored metal texture replaces 88 procedural stud Images.");

                Image captionPlateSurface = captionPlate.GetComponent<Image>();
                Assert.That(captionPlateSurface, Is.Not.Null);
                Assert.That(captionPlateSurface.raycastTarget, Is.False);
                Assert.That(captionPlateSurface.color.a, Is.InRange(0.8f, 0.9f));
                Assert.That(captionPlate.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(captionPlate.GetComponent<Shadow>(), Is.Not.Null);

                Text cooldownText = visualRoot.Find("CooldownSeconds").GetComponent<Text>();
                Assert.That(cooldownText.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(cooldownText.GetComponent<Shadow>(), Is.Not.Null);
                Transform blockedMark = visualRoot.Find("BlockedMark");
                Assert.That(blockedMark.Find("SlashForward"), Is.Not.Null);
                Assert.That(blockedMark.Find("SlashBack"), Is.Not.Null);
                float coreAlpha = visualRoot.Find("IconGlow")
                    .GetComponent<Image>().color.a;
                Assert.That(
                    coreAlpha,
                    Is.InRange(0.08f, 0.18f),
                    "The idle jewel bloom must remain subtle behind the icon.");
                Image cooldownOverlay = visualRoot.Find("Cooldown").GetComponent<Image>();
                Image globalCooldownOverlay = visualRoot
                    .Find("GlobalCooldown")
                    .GetComponent<Image>();
                Assert.That(cooldownOverlay.enabled, Is.False);
                Assert.That(globalCooldownOverlay.enabled, Is.False);
                Assert.That(cooldownOverlay.color.a, Is.Zero.Within(0.001f));
                Assert.That(globalCooldownOverlay.color.a, Is.Zero.Within(0.001f));
                Assert.That(
                    ColorChannelRange(cooldownOverlay.color),
                    Is.LessThanOrEqualTo(0.01f));
                Assert.That(
                    ColorChannelRange(globalCooldownOverlay.color),
                    Is.LessThanOrEqualTo(0.01f));
                Color32 actionColor = visualRoot.Find("Action")
                    .GetComponent<Text>().color;
                int expectedCaptionSize = slot < AbilityKit.GestureSlotCount
                    ? 14
                    : slot < AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount
                        ? 12
                        : 10;
                Assert.That(
                    visualRoot.Find("Action").GetComponent<Text>().fontSize,
                    Is.EqualTo(expectedCaptionSize));
                Assert.That(
                    visualRoot.Find("Action").GetComponent<Outline>(),
                    Is.Not.Null);
                Assert.That(
                    actionColor.r,
                    Is.InRange(215, 240),
                    "Conditional actions may carry the restrained cyan readiness tint.");
                Assert.That(actionColor.g, Is.InRange(185, 210));
                Assert.That(actionColor.b, Is.InRange(110, 145));

                Transform alternateUp = slotRect.Find("AlternateUp");
                AbilityRadialGuideGraphic radialGuide = slotRect
                    .Find("DirectionalRadialGuide")
                    .GetComponent<AbilityRadialGuideGraphic>();
                Assert.That(radialGuide, Is.Not.Null);
                Assert.That(radialGuide.raycastTarget, Is.False);
                Assert.That(radialGuide.IsExpanded, Is.False);
                Assert.That(
                    ((RectTransform)radialGuide.transform).rect.size,
                    Is.EqualTo(new Vector2(
                        isGestureControl ? 232f : expectedSize + 24f,
                        isGestureControl ? 232f : expectedSize + 24f)));
                if (isGestureControl)
                {
                    Assert.That(
                        radialGuide.gameObject.activeInHierarchy,
                        Is.True,
                        "The unified sector background must remain visible at rest.");
                    Assert.That(radialGuide.enabled, Is.True);
                    Assert.That(radialGuide.BoundDirectionCount, Is.EqualTo(4));
                    int guideSibling = radialGuide.transform.GetSiblingIndex();
                    Assert.That(
                        slotRect.Find("ClusterBacking").GetSiblingIndex(),
                        Is.LessThan(guideSibling),
                        "The dark backing must sit behind the shared sector mesh.");
                    Assert.That(
                        guideSibling,
                        Is.LessThan(slotRect.Find("ClusterOuterBezel").GetSiblingIndex()));
                    Assert.That(
                        slotRect.Find("ClusterOuterBezel").GetSiblingIndex(),
                        Is.LessThan(slotRect.Find("ClusterOuterHighlight").GetSiblingIndex()));
                    Assert.That(
                        slotRect.Find("ClusterOuterHighlight").GetSiblingIndex(),
                        Is.LessThan(slotRect.Find("ClusterAccentRing").GetSiblingIndex()));
                    Assert.That(
                        slotRect.Find("ClusterAccentRing").GetSiblingIndex(),
                        Is.LessThan(primaryMedallion.GetSiblingIndex()));
                    AssertDirectionalIconRegion(alternateUp, "Ability " + slot + " up");
                    Transform alternateUpCooldown = alternateUp.Find("Cooldown");
                    Transform alternateUpLabel = alternateUp.Find("Label");
                    Transform alternateUpCooldownText = alternateUp.Find("CooldownText");
                    Assert.That(
                        alternateUpCooldown.GetSiblingIndex(),
                        Is.GreaterThan(alternateUp.Find("Icon").GetSiblingIndex()),
                        "The Up cooldown shade must draw above icon art.");
                    Assert.That(
                        alternateUpCooldown.GetSiblingIndex(),
                        Is.GreaterThan(alternateUp.Find("Glyph").GetSiblingIndex()),
                        "The Up cooldown shade must draw above fallback glyph art.");
                    Assert.That(
                        alternateUpCooldown.GetSiblingIndex(),
                        Is.LessThan(alternateUpLabel.GetSiblingIndex()),
                        "The interaction caption must draw above the Up cooldown shade.");
                    Assert.That(
                        alternateUpCooldown.GetSiblingIndex(),
                        Is.LessThan(alternateUpCooldownText.GetSiblingIndex()),
                        "Cooldown seconds must draw above the Up cooldown shade.");
                }
                if (slotView.AlternateUpAbilityId != AbilityId.None)
                {
                    RectTransform alternateTag = (RectTransform)alternateUp;
                    Assert.That(alternateTag.gameObject.activeInHierarchy, Is.True);
                    Assert.That(
                        alternateTag.localScale,
                        Is.EqualTo(Vector3.one));
                    Assert.That(alternateTag.rect.width, Is.EqualTo(60f));
                    Assert.That(alternateTag.rect.height, Is.EqualTo(60f));
                    Assert.That(alternateTag.anchoredPosition, Is.EqualTo(new Vector2(0f, 78f)));
                    Assert.That(
                        alternateTag.Find("Label").gameObject.activeInHierarchy,
                        Is.False,
                        "Resting direction markers must not expose their labels.");
                    Image alternateArtwork = alternateTag
                        .Find("Icon")
                        .GetComponent<Image>();
                    Assert.That(alternateArtwork.preserveAspect, Is.True);
                    Assert.That(alternateArtwork.raycastTarget, Is.False);
                    Assert.That(
                        alternateArtwork.rectTransform.rect.size,
                        Is.EqualTo(new Vector2(48f, 48f)));
                    Assert.That(alternateTag.GetComponent<Image>(), Is.Null,
                        "Directional icons must sit inside one shared radial wheel, not separate rectangular chips.");
                }
                else
                {
                    Assert.That(alternateUp.gameObject.activeInHierarchy, Is.False);
                }

                string[] cardinalTagNames =
                {
                    "AlternateDownTag",
                    "AlternateLeftTag",
                    "AlternateRightTag",
                };
                for (int tagIndex = 0; tagIndex < cardinalTagNames.Length; tagIndex++)
                {
                    Transform cardinalTag = slotRect.Find(cardinalTagNames[tagIndex]);
                    Assert.That(cardinalTag, Is.Not.Null);
                    RectTransform cardinalRect = (RectTransform)cardinalTag;
                    float expectedDirectionSize = isGestureControl ? 60f : 64f;
                    Assert.That(cardinalRect.rect.width, Is.EqualTo(expectedDirectionSize));
                    Assert.That(cardinalRect.rect.height, Is.EqualTo(expectedDirectionSize));
                    if (isGestureControl)
                    {
                        Assert.That(cardinalRect.localScale, Is.EqualTo(Vector3.one));
                        Vector2 expectedDirectionPosition = tagIndex == 0
                            ? new Vector2(0f, -78f)
                            : tagIndex == 1
                                ? new Vector2(-78f, 0f)
                                : new Vector2(78f, 0f);
                        Assert.That(
                            cardinalRect.anchoredPosition,
                            Is.EqualTo(expectedDirectionPosition));
                        Assert.That(
                            cardinalRect.Find("Icon").GetComponent<RectTransform>().rect.size,
                            Is.EqualTo(new Vector2(48f, 48f)));
                        AssertDirectionalIconRegion(
                            cardinalTag,
                            "Ability " + slot + " " + cardinalTagNames[tagIndex]);
                    }
                    Assert.That(cardinalTag.GetComponent<Image>(), Is.Null,
                        "Only the shared four-sector wheel may paint a directional background.");
                }

                bool expectsDirectionalRoots = slot < AbilityKit.GestureSlotCount;
                Assert.That(
                    slotRect.Find("AlternateDownTag").gameObject.activeInHierarchy,
                    Is.EqualTo(expectsDirectionalRoots));
                Assert.That(
                    slotRect.Find("AlternateLeftTag").gameObject.activeInHierarchy,
                    Is.EqualTo(expectsDirectionalRoots));
                Assert.That(
                    slotRect.Find("AlternateRightTag").gameObject.activeInHierarchy,
                    Is.EqualTo(expectsDirectionalRoots));
            }

            Assert.That(authoredSlotViews, Has.Count.EqualTo(10));

            for (int first = 0; first < slots.Length; first++)
            {
                Rect firstRect = GetRectRelativeTo(gameplay, slots[first]);
                for (int second = first + 1; second < slots.Length; second++)
                {
                    Rect secondRect = GetRectRelativeTo(gameplay, slots[second]);
                    float requiredCenterDistance =
                        (firstRect.width + secondRect.width) * 0.5f;
                    Assert.That(
                        Vector2.Distance(firstRect.center, secondRect.center),
                        Is.GreaterThanOrEqualTo(requiredCenterDistance),
                        "Circular ability touch targets " + first + " " + firstRect +
                        " and " + second + " " + secondRect + " overlap.");
                }
            }

            // CanvasScaler matches height, so 16:9 is the narrowest supported
            // landscape canvas and wider aspects only move the right-anchored
            // combat cluster farther from the centered utility strip. Verify
            // all three review aspects explicitly, including visible chrome and
            // the scaled downward shadow offsets rather than touch roots only.
            const float referenceHeight = 1080f;
            const float edgePadding = 24f;
            float[] supportedHudAspects = { 16f / 9f, 20f / 9f, 2.4f };
            for (int aspectIndex = 0;
                 aspectIndex < supportedHudAspects.Length;
                 aspectIndex++)
            {
                float aspect = supportedHudAspects[aspectIndex];
                float halfWidth = referenceHeight * aspect * 0.5f;
                float halfHeight = referenceHeight * 0.5f;
                var authoredCenters = new Vector2[AbilityCatalog.AbilitySlotCount];
                for (int slot = 0; slot < authoredCenters.Length; slot++)
                {
                    bool isRightAnchored = slot <
                        AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount;
                    authoredCenters[slot] = new Vector2(
                        (isRightAnchored ? halfWidth : 0f) + expectedPositions[slot].x,
                        -halfHeight + expectedPositions[slot].y);

                    float touchRadius = expectedTouchDiameters[slot] * 0.5f;
                    Assert.That(
                        authoredCenters[slot].x - touchRadius,
                        Is.GreaterThanOrEqualTo(-halfWidth + edgePadding),
                        "Ability " + slot + " crosses the left safe edge at aspect " +
                        aspect + ".");
                    Assert.That(
                        authoredCenters[slot].x + touchRadius,
                        Is.LessThanOrEqualTo(halfWidth - edgePadding),
                        "Ability " + slot + " crosses the right safe edge at aspect " +
                        aspect + ".");
                    Assert.That(
                        authoredCenters[slot].y - touchRadius,
                        Is.GreaterThanOrEqualTo(-halfHeight + edgePadding),
                        "Ability " + slot + " crosses the lower safe edge at aspect " +
                        aspect + ".");
                    Assert.That(
                        authoredCenters[slot].y + touchRadius,
                        Is.LessThanOrEqualTo(halfHeight - edgePadding),
                        "Ability " + slot + " crosses the upper safe edge at aspect " +
                        aspect + ".");

                    Vector2 shadowCenter = authoredCenters[slot] + shadowOffsets[slot];
                    Assert.That(
                        authoredCenters[slot].x - centeredVisualRadii[slot],
                        Is.GreaterThanOrEqualTo(-halfWidth));
                    Assert.That(
                        authoredCenters[slot].x + centeredVisualRadii[slot],
                        Is.LessThanOrEqualTo(halfWidth));
                    Assert.That(
                        authoredCenters[slot].y - centeredVisualRadii[slot],
                        Is.GreaterThanOrEqualTo(-halfHeight));
                    Assert.That(
                        authoredCenters[slot].y + centeredVisualRadii[slot],
                        Is.LessThanOrEqualTo(halfHeight));
                    Assert.That(
                        shadowCenter.x - shadowVisualRadii[slot],
                        Is.GreaterThanOrEqualTo(-halfWidth));
                    Assert.That(
                        shadowCenter.x + shadowVisualRadii[slot],
                        Is.LessThanOrEqualTo(halfWidth));
                    Assert.That(
                        shadowCenter.y - shadowVisualRadii[slot],
                        Is.GreaterThanOrEqualTo(-halfHeight));
                    Assert.That(
                        shadowCenter.y + shadowVisualRadii[slot],
                        Is.LessThanOrEqualTo(halfHeight));
                }

                for (int first = 0; first < authoredCenters.Length; first++)
                {
                    Vector2 firstShadow = authoredCenters[first] + shadowOffsets[first];
                    for (int second = first + 1;
                         second < authoredCenters.Length;
                         second++)
                    {
                        Vector2 secondShadow = authoredCenters[second] + shadowOffsets[second];
                        string context = "Ability circles " + first + " and " + second +
                            " overlap at aspect " + aspect + ".";
                        Assert.That(
                            Vector2.Distance(authoredCenters[first], authoredCenters[second]),
                            Is.GreaterThanOrEqualTo(
                                (expectedTouchDiameters[first] +
                                 expectedTouchDiameters[second]) * 0.5f),
                            context);
                        Assert.That(
                            Vector2.Distance(authoredCenters[first], authoredCenters[second]),
                            Is.GreaterThanOrEqualTo(
                                centeredVisualRadii[first] +
                                centeredVisualRadii[second]),
                            context);
                        Assert.That(
                            Vector2.Distance(firstShadow, secondShadow),
                            Is.GreaterThanOrEqualTo(
                                shadowVisualRadii[first] + shadowVisualRadii[second]),
                            context);
                        Assert.That(
                            Vector2.Distance(authoredCenters[first], secondShadow),
                            Is.GreaterThanOrEqualTo(
                                centeredVisualRadii[first] + shadowVisualRadii[second]),
                            context);
                        Assert.That(
                            Vector2.Distance(firstShadow, authoredCenters[second]),
                            Is.GreaterThanOrEqualTo(
                                shadowVisualRadii[first] + centeredVisualRadii[second]),
                            context);
                    }
                }
            }

            RectTransform resource =
                (RectTransform)gameplay.Find("PlayerResource");
            Assert.That(resource, Is.Not.Null);
            Rect resourceRect = GetRectRelativeTo(gameplay, resource);
            Rect gameplayRect = gameplay.rect;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                Rect slotRect = GetRectRelativeTo(gameplay, slots[slot]);
                Assert.That(
                    slotRect.Overlaps(resourceRect),
                    Is.False,
                    "Ability " + slot + " overlaps the player resource bar.");
                Assert.That(slotRect.xMin, Is.GreaterThanOrEqualTo(gameplayRect.xMin + edgePadding));
                Assert.That(slotRect.xMax, Is.LessThanOrEqualTo(gameplayRect.xMax - edgePadding));
                Assert.That(slotRect.yMin, Is.GreaterThanOrEqualTo(gameplayRect.yMin + edgePadding));
                Assert.That(slotRect.yMax, Is.LessThanOrEqualTo(gameplayRect.yMax - edgePadding));
            }

            Rect lowerRightPrimary = GetRectRelativeTo(gameplay, slots[0]);
            const float flickClearance = 72f;
            Assert.That(
                gameplayRect.xMax - lowerRightPrimary.xMax,
                Is.GreaterThanOrEqualTo(flickClearance));
            Assert.That(
                lowerRightPrimary.yMin - gameplayRect.yMin,
                Is.GreaterThanOrEqualTo(flickClearance));

            float directBottomEdge = float.PositiveInfinity;
            for (int slot = AbilityKit.GestureSlotCount;
                 slot < AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount;
                 slot++)
            {
                directBottomEdge = Mathf.Min(
                    directBottomEdge,
                    GetRectRelativeTo(gameplay, slots[slot]).yMin);
            }

            for (int slot = AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount;
                 slot < AbilityKit.Capacity;
                 slot++)
            {
                Rect utility = GetRectRelativeTo(gameplay, slots[slot]);
                Assert.That(
                    utility.yMax,
                    Is.LessThanOrEqualTo(directBottomEdge),
                    "Utility " + slot + " must remain in the compact bottom row.");
            }

            RectTransform firstUtility = slots[
                AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount];
            RectTransform lastUtility = slots[AbilityKit.Capacity - 1];
            Assert.That(
                Mathf.Abs(firstUtility.anchoredPosition.x),
                Is.EqualTo(Mathf.Abs(lastUtility.anchoredPosition.x)).Within(0.01f),
                "The utility strip must remain centered around the screen midpoint.");
            RectTransform siegeSupplies =
                (RectTransform)gameplay.Find("SiegeSupplies");
            Assert.That(siegeSupplies, Is.Not.Null);
            Assert.That(
                siegeSupplies.anchoredPosition,
                Is.EqualTo(new Vector2(-430f, 24f)));
            Rect siegeSuppliesRect = GetRectRelativeTo(gameplay, siegeSupplies);
            for (int slot = AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount;
                 slot < AbilityKit.Capacity;
                 slot++)
            {
                Assert.That(
                    siegeSuppliesRect.Overlaps(GetRectRelativeTo(gameplay, slots[slot])),
                    Is.False,
                    "The Siege supply dock must not compete with the centered utilities.");
            }
            Assert.That(
                gameplay.Find("UtilityActions"),
                Is.Null,
                "The four utilities remain direct HUD controls in their centered strip.");
            Assert.That(resource.rect.width, Is.EqualTo(360f).Within(0.01f));
            Assert.That(resource.rect.height, Is.EqualTo(20f).Within(0.01f));
            RectTransform resourceFill =
                (RectTransform)resource.Find("Fill");
            Assert.That(resourceFill, Is.Not.Null);
            Assert.That(resourceFill.rect.width, Is.GreaterThan(0f).And.LessThan(resource.rect.width));
            Assert.That(resourceFill.rect.height, Is.GreaterThan(0f).And.LessThanOrEqualTo(resource.rect.height));
            Rect objectiveRect = GetRectRelativeTo(
                gameplay,
                (RectTransform)gameplay.Find("Objective"));
            RectTransform objective = (RectTransform)gameplay.Find("Objective");
            Assert.That(objective.rect.width, Is.EqualTo(680f).Within(0.01f));
            Assert.That(objective.rect.height, Is.EqualTo(54f).Within(0.01f));
            Assert.That(
                resourceRect.Overlaps(objectiveRect),
                Is.False,
                "The resource strip must remain separate from the objective hierarchy.");
            Transform objectiveInnerFrame = gameplay.Find("Objective/InnerFrame");
            Assert.That(objectiveInnerFrame, Is.Not.Null);
            Color32 objectiveInnerColor =
                objectiveInnerFrame.GetComponent<Image>().color;
            Assert.That(objectiveInnerColor.r, Is.LessThanOrEqualTo(12));
            Assert.That(objectiveInnerColor.g, Is.LessThanOrEqualTo(18));
            Assert.That(objectiveInnerColor.b, Is.LessThanOrEqualTo(28));

            RectTransform targetStrip =
                (RectTransform)gameplay.Find("Target");
            Rect targetRect = GetRectRelativeTo(gameplay, targetStrip);
            Assert.That(targetStrip.rect.width, Is.EqualTo(440f).Within(0.01f));
            Assert.That(targetStrip.rect.height, Is.EqualTo(80f).Within(0.01f));
            Assert.That(
                targetRect.Overlaps(objectiveRect),
                Is.False,
                "The compact target strip must sit below the match objective.");
            Assert.That(
                targetRect.Overlaps(GetRectRelativeTo(
                    gameplay,
                    (RectTransform)gameplay.Find("LevelProgression"))),
                Is.False,
                "Target and Training progression strips need distinct readable rows.");

            RectTransform movementZone =
                (RectTransform)gameplay.Find("MovementZone");
            Assert.That(movementZone, Is.Not.Null);
            Assert.That(movementZone.anchorMax.x, Is.EqualTo(0.34f).Within(0.001f));
            Assert.That(movementZone.anchorMax.y, Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(
                GetRectRelativeTo(gameplay, movementZone).Contains(Vector2.zero),
                Is.False,
                "The invisible movement surface must not steal center-screen targeting or orbit drags.");

            RectTransform pause = (RectTransform)gameplay.Find("Pause");
            Assert.That(pause, Is.Not.Null);
            Assert.That(ReadChildText(pause, "Label"), Is.EqualTo("Ⅱ"));
            Assert.That(pause.rect.size, Is.EqualTo(new Vector2(64f, 64f)));
            Rect pauseRect = GetRectRelativeTo(gameplay, pause);
            Assert.That(pauseRect.Overlaps(objectiveRect), Is.False);
            Transform friendlyRoster = gameplay.Find("FriendlyFighters");
            Assert.That(friendlyRoster, Is.Not.Null);
            RectTransform friendlyRosterRect = (RectTransform)friendlyRoster;
            Assert.That(friendlyRosterRect.rect.width, Is.EqualTo(360f).Within(0.01f));
            Assert.That(friendlyRosterRect.rect.height, Is.EqualTo(84f).Within(0.01f));
            Rect hostileRosterRect = GetRectRelativeTo(
                gameplay,
                (RectTransform)gameplay.Find("HostileFighters"));
            RectTransform hostileRoster = (RectTransform)gameplay.Find("HostileFighters");
            Assert.That(hostileRoster.rect.width, Is.EqualTo(360f).Within(0.01f));
            Assert.That(hostileRoster.rect.height, Is.EqualTo(84f).Within(0.01f));
            Assert.That(
                pauseRect.Overlaps(hostileRosterRect),
                Is.False,
                "Pause must not cover the hostile roster.");
            Transform rosterCard = friendlyRoster.GetChild(0);
            RectTransform rosterCardRect = (RectTransform)rosterCard;
            Assert.That(rosterCardRect.rect.width, Is.EqualTo(78f).Within(0.01f));
            Assert.That(rosterCardRect.rect.height, Is.EqualTo(84f).Within(0.01f));
            Assert.That(rosterCard.Find("OuterMetal"), Is.Not.Null);
            Assert.That(rosterCard.Find("MetalHighlight"), Is.Not.Null);
            Transform gestureOverlayTransform = gameplay.Find("AbilityGestureOverlay");
            Assert.That(gestureOverlayTransform, Is.Not.Null);
            CanvasGroup gestureOverlay = gestureOverlayTransform.GetComponent<CanvasGroup>();
            Assert.That(gestureOverlay, Is.Not.Null);
            Assert.That(gestureOverlay.interactable, Is.False);
            Assert.That(gestureOverlay.blocksRaycasts, Is.False);
            Assert.That(
                gestureOverlay.transform.GetSiblingIndex(),
                Is.GreaterThan(slots[slots.Length - 1].GetSiblingIndex()),
                "Directional gesture petals must render above every ability button.");
            Assert.That(
                ReadChildText(gameplay, "CameraOrbitHint"),
                Is.EqualTo("DRAG EMPTY FIELD TO ORBIT"));
        }

        [UnityTest]
        public IEnumerator EnlargedGestureControls_KeepSizeAndTouchClearanceAcrossPhoneLayouts()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Fire);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            RectTransform safeArea = (RectTransform)hud.transform.Find("SafeArea");
            RectTransform gameplay = (RectTransform)safeArea.Find("Gameplay");
            Vector2 originalAnchorMin = safeArea.anchorMin;
            Vector2 originalAnchorMax = safeArea.anchorMax;
            Vector2 originalPosition = safeArea.anchoredPosition;
            Vector2 originalSize = safeArea.sizeDelta;
            Vector2[] phoneResolutions =
            {
                new Vector2(960f, 720f), // 4:3 runner/tablet robustness, not a phone target
                new Vector2(1280f, 720f),
                new Vector2(1600f, 720f),
                new Vector2(1728f, 720f),
                new Vector2(1920f, 1080f),
                new Vector2(2400f, 1080f),
                new Vector2(2592f, 1080f),
            };
            // These are simulated insets, not a claimed physical-device safe
            // area. Exercise the actual authored RectTransforms without
            // changing the test runner's display or Screen.safeArea.
            Vector4[] insetCases =
            {
                Vector4.zero,
                new Vector4(72f, 24f, 18f, 0f), // left, right, bottom, top pixels
            };
            try
            {
                foreach (Vector2 resolution in phoneResolutions)
                {
                    float referenceToPixels = resolution.y / 1080f;
                    foreach (Vector4 insets in insetCases)
                    {
                        safeArea.anchorMin = Vector2.one * 0.5f;
                        safeArea.anchorMax = Vector2.one * 0.5f;
                        safeArea.anchoredPosition = new Vector2(
                            insets.x - insets.y,
                            insets.z - insets.w) / (2f * referenceToPixels);
                        safeArea.sizeDelta = new Vector2(
                            resolution.x - insets.x - insets.y,
                            resolution.y - insets.z - insets.w) / referenceToPixels;
                        Canvas.ForceUpdateCanvases();

                        string context = resolution + " insets " + insets;
                        Rect[] touchRects = new Rect[AbilityCatalog.AbilitySlotCount];
                        for (int slot = 0; slot < touchRects.Length; slot++)
                        {
                            RectTransform slotRoot = (RectTransform)hud.GetAbilitySlot(slot).transform;
                            Rect touchRect = GetRectRelativeTo(gameplay, slotRoot);
                            touchRects[slot] = touchRect;
                            Assert.That(touchRect.xMin,
                                Is.GreaterThanOrEqualTo(gameplay.rect.xMin + 24f), context);
                            Assert.That(touchRect.xMax,
                                Is.LessThanOrEqualTo(gameplay.rect.xMax - 24f), context);
                            Assert.That(touchRect.yMin,
                                Is.GreaterThanOrEqualTo(gameplay.rect.yMin + 24f), context);
                            Assert.That(touchRect.yMax,
                                Is.LessThanOrEqualTo(gameplay.rect.yMax - 24f), context);
                            if (slot >= AbilityKit.GestureSlotCount)
                            {
                                continue;
                            }

                            Assert.That(touchRect.width * referenceToPixels,
                                Is.EqualTo(232f * 1.3f * referenceToPixels).Within(0.01f),
                                context + " gesture width must not shrink on short displays");
                            Rect primaryIcon = GetRectRelativeTo(gameplay,
                                (RectTransform)slotRoot.Find("PrimaryMedallion/Icon"));
                            Assert.That(primaryIcon.width * referenceToPixels,
                                Is.EqualTo(48f * 1.3f * referenceToPixels).Within(0.01f),
                                context + " actual icon size");
                        }

                        for (int first = 0; first < touchRects.Length; first++)
                        {
                            for (int second = first + 1; second < touchRects.Length; second++)
                            {
                                Assert.That(
                                    Vector2.Distance(touchRects[first].center, touchRects[second].center),
                                    Is.GreaterThanOrEqualTo(
                                        (touchRects[first].width + touchRects[second].width) * 0.5f),
                                    context + " touch overlap " + first + "/" + second);
                            }
                        }

                        Assert.That(
                            (gameplay.rect.xMax - touchRects[0].xMax) * referenceToPixels,
                            Is.GreaterThanOrEqualTo(52f),
                            context + " an outward flick from the right disc edge must fit");
                        Assert.That(
                            (touchRects[1].yMin - gameplay.rect.yMin) * referenceToPixels,
                            Is.GreaterThanOrEqualTo(52f),
                            context + " an outward flick from the lower disc edge must fit");
                    }
                }
            }
            finally
            {
                safeArea.anchorMin = originalAnchorMin;
                safeArea.anchorMax = originalAnchorMax;
                safeArea.anchoredPosition = originalPosition;
                safeArea.sizeDelta = originalSize;
                Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTest]
        public IEnumerator Arena_FireMageRebindsEverySlotToTheCurrentFireKit()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Fire);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);
            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                AbilityId expected = AbilityCatalog.GetSpecializationAbility(
                    SpecializationId.Fire,
                    slot);
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView, Is.Not.Null);
                Assert.That(slotView.AbilityId, Is.EqualTo(expected));
                AssertPrimaryActionIdentity(slotView, expected);
                Assert.That(AbilityCatalog.TryGet(expected, out AbilityDefinition definition));
                Assert.That(
                    slotView.VisibleCaption,
                    Is.Not.Empty,
                    "Every Fire action needs a compact readable keyword.");
            }
        }

        [UnityTest]
        public IEnumerator Home_ClassRoundTripPreservesTheRememberedFireKit()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);

            hud.SetSelectedClass(ClassId.Mage, true);
            hud.SetSelectedSpecialization(SpecializationId.Fire, true);
            hud.SetSelectedClass(ClassId.Rogue, true);
            hud.SetSelectedClass(ClassId.Mage, true);

            Assert.That(hud.SelectedClass, Is.EqualTo(ClassId.Mage));
            Assert.That(hud.SelectedSpecialization, Is.EqualTo(SpecializationId.Fire));

            // First launch can preselect recommended Training. This test is
            // specifically about the normalized Arena kit.
            hud.SetSelectedMode(PrototypeMode.Arena2V2, true);

            Button start = hud.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Start");
            start.onClick.Invoke();
            yield return new WaitForFixedUpdate();
            yield return null;

            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                AbilityId expected = AbilityCatalog.GetSpecializationAbility(
                    SpecializationId.Fire,
                    slot);
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView.AbilityId, Is.EqualTo(expected));
                AssertPrimaryActionIdentity(slotView, expected);
            }
        }

        [UnityTest]
        public IEnumerator Arena_HudAuthorsResourcesCastStateAurasAndGlobalCooldown()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Rogue,
                SpecializationId.Mutilate);
            yield return new WaitForFixedUpdate();
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);
            Transform gameplay = hud.transform.Find("SafeArea/Gameplay");
            Assert.That(gameplay, Is.Not.Null);

            Transform playerResource = gameplay.Find("PlayerResource");
            Assert.That(playerResource, Is.Not.Null);
            hud.SetPlayerResource(ResourceType.Energy, 37f, 100f);
            Assert.That(playerResource.gameObject.activeInHierarchy, Is.True);
            Assert.That(ReadChildText(playerResource, "Type"), Is.EqualTo("ENERGY"));
            Assert.That(ReadChildText(playerResource, "Value"), Is.EqualTo("37 / 100"));
            Assert.That(
                playerResource.Find("Fill").GetComponent<Image>().fillAmount,
                Is.EqualTo(0.37f).Within(0.0001f));

            Transform target = gameplay.Find("Target");
            Assert.That(target, Is.Not.Null);
            Transform targetResource = target.Find("TargetResource");
            Assert.That(targetResource, Is.Not.Null);
            hud.SetTargetResource(ResourceType.Mana, 61f, 100f);
            Assert.That(
                target.gameObject.activeInHierarchy,
                Is.True,
                "Arena should expose the snapshot-authoritative nearest target immediately.");
            Assert.That(targetResource.gameObject.activeSelf, Is.True);
            Assert.That(ReadChildText(targetResource, "Type"), Is.EqualTo("MANA"));
            Assert.That(ReadChildText(targetResource, "Value"), Is.EqualTo("61 / 100"));

            Transform targetCast = target.Find("TargetCast");
            Assert.That(targetCast, Is.Not.Null);
            Assert.That(targetCast.Find("State"), Is.Not.Null);
            hud.SetTargetCast("Penance", 0.5f, true, true, true);
            Assert.That(targetCast.gameObject.activeSelf, Is.True);
            Assert.That(ReadChildText(targetCast, "Label"), Is.EqualTo("Penance"));
            Assert.That(
                ReadChildText(targetCast, "State"),
                Is.EqualTo("CHANNEL  •  INTERRUPTIBLE"));
            Assert.That(
                targetCast.Find("Fill").GetComponent<Image>().fillAmount,
                Is.EqualTo(0.5f).Within(0.0001f));

            hud.SetTargetAuras(
                new HudAuraIndicator("Soul Ward", 9.2f),
                new HudAuraIndicator("Poison", 5.1f, 2, HudAuraKind.Harmful),
                new HudAuraIndicator("Fear", 2.2f, kind: HudAuraKind.Control),
                new HudAuraIndicator("Weakened Soul", 11f, kind: HudAuraKind.Harmful));
            for (int slot = 0; slot < PrototypeHud.TargetAuraSlotCount; slot++)
            {
                Transform aura = target.Find("Aura_" + (slot + 1));
                Assert.That(aura, Is.Not.Null, "Missing authored aura slot " + slot + ".");
                Assert.That(aura.gameObject.activeSelf, Is.True);
                Assert.That(ReadChildText(aura, "Label"), Is.Not.Empty);
            }

            hud.SetGlobalCooldown(1f, 1.5f, true);
            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView, Is.Not.Null);
                Assert.That(slotView.gameObject.activeInHierarchy, Is.True);
                AbilityDefinition ability = AbilityCatalog.Get(slotView.AbilityId);
                bool expectsGlobalCooldown = ability.GlobalCooldownSeconds > 0.001f &&
                    (ability.Requirements & AbilityRequirementFlags.IgnoresGlobalCooldown) == 0;
                Assert.That(
                    slotView.IsGlobalCooldownLocked,
                    Is.EqualTo(expectsGlobalCooldown));
                Transform medallion = FindPrimaryMedallion(slotView);
                Transform sweep = medallion.Find("GlobalCooldown");
                Transform lockLabel = medallion.Find("GlobalCooldownLock");
                Assert.That(sweep, Is.Not.Null);
                Assert.That(lockLabel, Is.Not.Null);
                Assert.That(
                    sweep.GetComponent<Image>().enabled,
                    Is.False,
                    "Global cooldown uses the lock label and icon tint; the circular sweep must stay hidden.");
                Assert.That(
                    sweep.GetComponent<Image>().fillAmount,
                    Is.EqualTo(expectsGlobalCooldown ? 2f / 3f : 0f).Within(0.0001f));
                Assert.That(
                    lockLabel.gameObject.activeInHierarchy,
                    Is.EqualTo(expectsGlobalCooldown));
            }

            hud.ClearTargetAuras();
            hud.SetTargetCast(string.Empty, 0f, false, false, false);
            hud.SetGlobalCooldown(0f, 1.5f, false);

            AbilitySlotView firstSlot = hud.GetAbilitySlot(0);
            Transform firstMedallion = FindPrimaryMedallion(firstSlot);
            firstSlot.SetCooldown(0f, 0f, true);
            Transform stateBadge = firstMedallion.Find("StateBadge");
            Assert.That(stateBadge, Is.Not.Null);
            firstSlot.SetReadiness(
                AbilityReadinessState.NoTarget,
                "SELECT TARGET");
            Assert.That(firstSlot.BlockedReason, Is.EqualTo("SELECT TARGET"));
            Assert.That(
                ReadChildText(stateBadge, "Label"),
                Is.EqualTo("FOE"),
                "A missing focus should keep the compact relation badge; the exact " +
                "rejection reason appears only after an attempted cast.");
            Assert.That(
                stateBadge.gameObject.activeInHierarchy,
                Is.False,
                "Resting target relations should not cover every spell icon.");

            firstSlot.SetReadiness(AbilityReadinessState.MissingSetup, "NEEDS FROZEN");
            Assert.That(
                firstSlot.ReadinessState,
                Is.EqualTo(AbilityReadinessState.MissingSetup));
            Assert.That(firstSlot.BlockedReason, Is.EqualTo("NEEDS FROZEN"));
            Assert.That(stateBadge.gameObject.activeInHierarchy, Is.True);
            Assert.That(ReadChildText(stateBadge, "Label"), Is.EqualTo("FROZEN"));
            Assert.That(
                firstSlot.transform.Find("BlockedReason"),
                Is.Null,
                "Readiness sentences must not be stamped across the spell icon.");
            Assert.That(firstMedallion.Find("BlockedMark").gameObject.activeSelf, Is.False);

            firstSlot.SetGlobalCooldown(1f, 1.5f, true);
            Assert.That(
                firstSlot.ReadinessState,
                Is.EqualTo(AbilityReadinessState.GlobalCooldown));
            Assert.That(
                firstMedallion.Find("GlobalCooldownLock").GetComponent<Text>().text,
                Is.EqualTo("GCD"));
            Assert.That(ReadChildText(stateBadge, "Label"), Is.EqualTo("GCD"));
            Assert.That(stateBadge.gameObject.activeInHierarchy, Is.False);
            Assert.That(firstMedallion.Find("BlockedMark").gameObject.activeSelf, Is.False);
            firstSlot.SetGlobalCooldown(0f, 1.5f, false);
            Assert.That(
                firstSlot.ReadinessState,
                Is.EqualTo(AbilityReadinessState.MissingSetup));
            Assert.That(ReadChildText(stateBadge, "Label"), Is.EqualTo("FROZEN"));

            firstSlot.SetCooldown(4.2f, 8f, false);
            Assert.That(firstSlot.ReadinessState, Is.EqualTo(AbilityReadinessState.Cooldown));
            Assert.That(ReadChildText(stateBadge, "Label"), Is.EqualTo("CD"));
            Assert.That(stateBadge.gameObject.activeInHierarchy, Is.False);
            Assert.That(
                firstMedallion.Find("CooldownSeconds").GetComponent<Text>().text,
                Is.EqualTo("5"));
            Assert.That(
                firstMedallion.Find("GlobalCooldown").GetComponent<Image>().enabled,
                Is.False);
            firstSlot.SetCooldown(0f, 8f, true);
            firstSlot.SetReadiness(AbilityReadinessState.Ready);
        }

        [UnityTest]
        public IEnumerator GameplayHud_SpectatingLocksAndDimsEveryAbility()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.SetAbilityControlsInteractable(false, "SPECTATING");

            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView.IsInteractionEnabled, Is.False);
                Assert.That(
                    slotView.ReadinessState,
                    Is.EqualTo(AbilityReadinessState.Spectating));
                Assert.That(slotView.BlockedReason, Is.EqualTo("SPECTATING"));
                Assert.That(slotView.GetComponent<AbilityButton>().enabled, Is.False);
                CanvasGroup canvasGroup = slotView.GetComponent<CanvasGroup>();
                Assert.That(canvasGroup, Is.Not.Null);
                Assert.That(canvasGroup.interactable, Is.False);
                Assert.That(canvasGroup.blocksRaycasts, Is.False);
                Assert.That(canvasGroup.alpha, Is.LessThan(1f));
            }

            hud.SetAbilityControlsInteractable(true);
            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                Assert.That(slotView.IsInteractionEnabled, Is.True);
                Assert.That(slotView.GetComponent<AbilityButton>().enabled, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CombatEffects_ReuseBoundedAuthoredPools()
        {
            CombatEffectsPresenter[] presenters =
                Object.FindObjectsByType<CombatEffectsPresenter>();
            Assert.That(presenters, Has.Length.EqualTo(1));
            CombatEffectsPresenter presenter = presenters[0];

            CombatEffectView[] effectsBefore = Object
                .FindObjectsByType<CombatEffectView>(FindObjectsInactive.Include)
                .Where(value => value.transform.IsChildOf(presenter.transform))
                .OrderBy(value => value.name)
                .ToArray();
            CombatTextView[] textsBefore = Object
                .FindObjectsByType<CombatTextView>(FindObjectsInactive.Include)
                .Where(value => value.transform.IsChildOf(presenter.transform))
                .OrderBy(value => value.name)
                .ToArray();
            Assert.That(effectsBefore, Is.Not.Empty);
            Assert.That(textsBefore, Is.Not.Empty);

            int presentationCount = Mathf.Max(effectsBefore.Length, textsBefore.Length) * 3;
            for (int index = 0; index < presentationCount; index++)
            {
                presenter.PresentDamage(
                    AbilityId.ArcaneBolt,
                    presenter.transform,
                    presenter.transform,
                    7f);
            }

            AssertPoolIdentity(presenter, effectsBefore, textsBefore);
            Assert.That(effectsBefore.Count(value => value.IsPlaying), Is.EqualTo(effectsBefore.Length));
            Assert.That(textsBefore.Count(value => value.IsPlaying), Is.EqualTo(textsBefore.Length));

            yield return new WaitForSeconds(0.8f);

            Assert.That(effectsBefore.Any(value => value.IsPlaying), Is.False);
            Assert.That(textsBefore.Any(value => value.IsPlaying), Is.False);
            presenter.PresentDamage(
                AbilityId.QuickStrike,
                presenter.transform,
                presenter.transform,
                9f);
            AssertPoolIdentity(presenter, effectsBefore, textsBefore);
            Assert.That(effectsBefore.Count(value => value.IsPlaying), Is.EqualTo(1));
            Assert.That(textsBefore.Count(value => value.IsPlaying), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PresentationCatalog_CoversEverySpecAbilityWithAStableVisualIdentity()
        {
            SpecializationId[] specializations =
            {
                SpecializationId.Discipline,
                SpecializationId.Shadow,
                SpecializationId.Fire,
                SpecializationId.Frost,
                SpecializationId.ShadowDance,
                SpecializationId.Mutilate,
                SpecializationId.Marksmanship,
            };
            for (int specializationIndex = 0;
                 specializationIndex < specializations.Length;
                 specializationIndex++)
            {
                SpecializationId specialization = specializations[specializationIndex];
                AbilityPresentation[] kit = Enumerable
                    .Range(0, AbilityCatalog.AbilitySlotCount)
                    .Select(slot => AbilityCatalog.GetSpecializationAbility(specialization, slot))
                    .Select(ability =>
                    {
                        Assert.That(
                            AbilityPresentationCatalog.TryGet(
                                ability,
                                out AbilityPresentation presentation),
                            Is.True,
                            specialization + " ability " + ability +
                            " needs presentation metadata.");
                        Assert.That(presentation.Ability, Is.EqualTo(ability));
                        Assert.That(string.IsNullOrWhiteSpace(presentation.Style), Is.False);
                        Assert.That(presentation.Size, Is.GreaterThan(0f));
                        Assert.That(presentation.Duration, Is.GreaterThan(0f));
                        return presentation;
                    })
                    .ToArray();

                for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
                {
                    AbilitySlotBinding binding =
                        SpecializationCatalog.GetKit(specialization).GetSlotBinding(slot);
                    AssertDirectionalPresentation(binding.Up);
                    AssertDirectionalPresentation(binding.Down);
                    AssertDirectionalPresentation(binding.Left);
                    AssertDirectionalPresentation(binding.Right);
                }

                Assert.That(
                    kit.Select(value => (value.Motion, value.Size)).Distinct().Count(),
                    Is.EqualTo(AbilityCatalog.AbilitySlotCount),
                    specialization +
                    " abilities need distinct shape/motion identities even without color.");

                void AssertDirectionalPresentation(AbilityId ability)
                {
                    if (ability == AbilityId.None)
                    {
                        return;
                    }

                    Assert.That(
                        AbilityPresentationCatalog.TryGet(
                            ability,
                            out AbilityPresentation presentation),
                        Is.True,
                        specialization + " directional ability " + ability +
                        " needs presentation metadata.");
                    Assert.That(presentation.Ability, Is.EqualTo(ability));
                    Assert.That(string.IsNullOrWhiteSpace(presentation.Style), Is.False);
                    Assert.That(presentation.Size, Is.GreaterThan(0f));
                    Assert.That(presentation.Duration, Is.GreaterThan(0f));
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatAudio_UsesCompleteAuthoredPaletteAndBoundedSourcePools()
        {
            CombatAudioPresenter audio =
                Object.FindAnyObjectByType<CombatAudioPresenter>();
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.Palette, Is.Not.Null);
            Assert.That(audio.Palette.HasEveryRequiredClip(), Is.True);
            Assert.That(audio.WorldSourceCount, Is.EqualTo(10));
            Assert.That(audio.UiSourceCount, Is.EqualTo(3));
            Assert.That(audio.TotalSourceCount, Is.EqualTo(13));

            AudioSource[] sources = audio.GetComponentsInChildren<AudioSource>(true)
                .OrderBy(value => value.name)
                .ToArray();
            Assert.That(sources, Has.Length.EqualTo(audio.TotalSourceCount));
            Assert.That(sources.All(value => !value.playOnAwake), Is.True);
            Assert.That(sources.All(value => !value.loop), Is.True);
            Assert.That(sources.All(value => value.clip == null), Is.True);

            AudioClip[] clips = Enumerable
                .Range(1, CombatSfxPalette.RequiredClipCount)
                .Select(value => audio.Palette.GetClip((CombatSfxCue)value))
                .ToArray();
            Assert.That(clips.All(value => value != null), Is.True);
            Assert.That(
                clips.Distinct().Count(),
                Is.EqualTo(CombatSfxPalette.RequiredClipCount));
            Assert.That(clips.All(value => value.channels == 1), Is.True);
            Assert.That(clips.All(value => value.frequency == 22050), Is.True);

            int initialPlaybackCount = audio.PlaybackCount;
            Assert.That(audio.PlayUi(CombatSfxCue.UiSelect), Is.True);
            Assert.That(audio.PlayWorld(CombatSfxCue.ImpactMagic, Vector3.zero), Is.True);
            Assert.That(audio.PlayUi(CombatSfxCue.ResultVictory), Is.True);
            Assert.That(audio.PlaybackCount, Is.EqualTo(initialPlaybackCount + 3));
            Assert.That(audio.LastCue, Is.EqualTo(CombatSfxCue.ResultVictory));
            for (int index = 0; index < audio.TotalSourceCount * 3; index++)
            {
                Assert.That(
                    audio.PlayWorld(CombatSfxCue.ImpactPhysical, Vector3.zero),
                    Is.True);
            }

            AudioSource[] sourcesAfter = audio.GetComponentsInChildren<AudioSource>(true)
                .OrderBy(value => value.name)
                .ToArray();
            Assert.That(sourcesAfter, Has.Length.EqualTo(13));
            CollectionAssert.AreEqual(sources, sourcesAfter);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatantView_StealthHidesBodyAndOnlyEnemiesLoseTargetability()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Rogue,
                SpecializationId.ShadowDance);
            yield return null;

            controller.enabled = false;
            CombatantView[] views =
                Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
            CombatantView friendlyRogue = views.Single(value => value.IsHuman);
            CombatantView hostileRogue = views.First(value => value.Team.Value == 2);
            CombatantSnapshot friendlySnapshot = CreateStealthedSnapshot(
                friendlyRogue.Id,
                friendlyRogue.Team,
                SpecializationId.ShadowDance);
            CombatantSnapshot hostileSnapshot = CreateStealthedSnapshot(
                hostileRogue.Id,
                hostileRogue.Team,
                hostileRogue.Team.Value == 2
                    ? SpecializationId.Mutilate
                    : SpecializationId.ShadowDance);

            friendlyRogue.Present(friendlySnapshot, 1, 30, 6f, 1);
            hostileRogue.Present(hostileSnapshot, 1, 30, 6f, 1);

            Assert.That(friendlyRogue.IsStealthed, Is.True);
            Assert.That(friendlyRogue.IsModelVisible, Is.False);
            Assert.That(friendlyRogue.IsHiddenFromViewer, Is.False);
            Assert.That(friendlyRogue.IsTargetable, Is.True);
            Assert.That(friendlyRogue.transform.Find("TeamRing").gameObject.activeSelf, Is.True);

            Assert.That(hostileRogue.IsStealthed, Is.True);
            Assert.That(hostileRogue.IsModelVisible, Is.False);
            Assert.That(hostileRogue.IsHiddenFromViewer, Is.True);
            Assert.That(hostileRogue.IsTargetable, Is.False);
            Assert.That(hostileRogue.transform.Find("WorldStatusBars").gameObject.activeSelf, Is.False);
            Assert.That(
                hostileRogue.GetComponentInChildren<CombatantRelationMarker>(true)
                    .gameObject.activeSelf,
                Is.False);
            yield return null;
        }

        private static void AssertAnimatorState(
            Animator animator,
            int layer,
            string stateName)
        {
            int expectedHash = Animator.StringToHash(stateName);
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
            bool matches = current.shortNameHash == expectedHash;
            if (!matches && animator.IsInTransition(layer))
            {
                matches = animator.GetNextAnimatorStateInfo(layer).shortNameHash == expectedHash;
            }

            Assert.That(
                matches,
                Is.True,
                animator.name + " layer " + layer + " did not enter " + stateName + ".");
        }

        private static CombatantSnapshot CreateStealthedSnapshot(
            ParticipantId id,
            TeamId team,
            SpecializationId specialization)
        {
            return new CombatantSnapshot(
                id,
                team,
                ClassId.Rogue,
                specialization,
                default,
                new Float3(0f, 0f, 1f),
                110f,
                110f,
                0f,
                AbilityId.None,
                0,
                0,
                AbilityActivationType.Instant,
                SpellSchoolMask.None,
                ResourceType.Energy,
                100f,
                100f,
                0,
                default,
                0,
                SpellSchoolMask.None,
                true,
                false,
                StatusFlags.Stealthed,
                AuraTag.Stealth | AuraTag.StealthAccess);
        }

        private static void AssertPoolIdentity(
            CombatEffectsPresenter presenter,
            CombatEffectView[] expectedEffects,
            CombatTextView[] expectedTexts)
        {
            CombatEffectView[] actualEffects = Object
                .FindObjectsByType<CombatEffectView>(FindObjectsInactive.Include)
                .Where(value => value.transform.IsChildOf(presenter.transform))
                .OrderBy(value => value.name)
                .ToArray();
            CombatTextView[] actualTexts = Object
                .FindObjectsByType<CombatTextView>(FindObjectsInactive.Include)
                .Where(value => value.transform.IsChildOf(presenter.transform))
                .OrderBy(value => value.name)
                .ToArray();

            Assert.That(actualEffects, Has.Length.EqualTo(expectedEffects.Length));
            Assert.That(actualTexts, Has.Length.EqualTo(expectedTexts.Length));
            for (int index = 0; index < expectedEffects.Length; index++)
            {
                Assert.That(actualEffects[index], Is.SameAs(expectedEffects[index]));
            }

            for (int index = 0; index < expectedTexts.Length; index++)
            {
                Assert.That(actualTexts[index], Is.SameAs(expectedTexts[index]));
            }
        }

        private static void AssertAbilityReadyWithoutEnemy(
            PrototypeGameController controller,
            AbilityId ability,
            ParticipantId expectedTarget)
        {
            Assert.That(
                controller.TryEvaluateFocusedAbility(
                    ability,
                    out ParticipantId target,
                    out AbilityReadinessState state,
                    out string reason),
                Is.True,
                ability + " should resolve without an enemy target.");
            Assert.That(target, Is.EqualTo(expectedTarget));
            Assert.That(state, Is.EqualTo(AbilityReadinessState.Ready));
            Assert.That(reason, Is.Empty);
        }

        private static void AssertGroundedPose(
            Animator animator,
            string state,
            float moveSpeed,
            float minimumAllowedY,
            float maximumAllowedY)
        {
            animator.SetFloat(CombatantAnimationPresenter.MoveSpeedParameter, moveSpeed);
            for (int sampleIndex = 0; sampleIndex <= 8; sampleIndex++)
            {
                float normalizedTime = sampleIndex / 10f;
                animator.Play(state, 0, normalizedTime);
                animator.Update(0.001f);
                float minimumY = GetBakedMinimumY(animator.transform);
                Assert.That(
                    minimumY,
                    Is.InRange(minimumAllowedY, maximumAllowedY),
                    animator.name + " state " + state + " at " + normalizedTime.ToString("F1") +
                    " has implausible ground clearance " + minimumY.ToString("F3") + "m.");
            }
        }

        private static float GetBakedMinimumY(Transform model)
        {
            float minimumY = float.PositiveInfinity;
            SkinnedMeshRenderer[] renderers =
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                Mesh bakedMesh = new Mesh();
                renderer.BakeMesh(bakedMesh);
                Vector3[] vertices = bakedMesh.vertices;
                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    float worldY = renderer.transform.TransformPoint(vertices[vertexIndex]).y;
                    minimumY = Mathf.Min(minimumY, worldY);
                }

                Object.DestroyImmediate(bakedMesh);
            }

            Assert.That(minimumY, Is.Not.EqualTo(float.PositiveInfinity));
            return minimumY;
        }

        private static string ReadChildText(Transform parent, string childName)
        {
            Assert.That(parent, Is.Not.Null);
            Transform child = parent.Find(childName);
            Assert.That(child, Is.Not.Null, "Missing authored HUD child '" + childName + "'.");
            Text label = child.GetComponent<Text>();
            Assert.That(label, Is.Not.Null, "HUD child '" + childName + "' is not a Text label.");
            return label.text;
        }

        private static Rect GetRectRelativeTo(
            RectTransform parent,
            RectTransform child)
        {
            Vector3[] worldCorners = new Vector3[4];
            child.GetWorldCorners(worldCorners);
            Vector3 minimum = parent.InverseTransformPoint(worldCorners[0]);
            Vector3 maximum = minimum;
            for (int corner = 1; corner < worldCorners.Length; corner++)
            {
                Vector3 point = parent.InverseTransformPoint(worldCorners[corner]);
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }

            return Rect.MinMaxRect(
                minimum.x,
                minimum.y,
                maximum.x,
                maximum.y);
        }

        private static bool HasCompactCircularSelector(CombatantStatusView fighter)
        {
            RectTransform root = fighter.RectTransform;
            Assert.That(root, Is.Not.Null);
            Assert.That(root.rect.width, Is.GreaterThan(0f).And.LessThanOrEqualTo(140f));
            Assert.That(root.rect.height, Is.GreaterThan(0f).And.LessThanOrEqualTo(140f));

            Transform healthTransform = root.Find("HealthFill");
            Assert.That(healthTransform, Is.Not.Null, "Compact selector needs a HealthFill ring.");
            RectTransform healthRect = healthTransform as RectTransform;
            Assert.That(healthRect, Is.Not.Null);
            Assert.That(healthRect.rect.width, Is.EqualTo(healthRect.rect.height).Within(0.01f));

            Image healthFill = healthTransform.GetComponent<Image>();
            Assert.That(healthFill, Is.Not.Null);
            Assert.That(healthFill.sprite, Is.Not.Null);
            Assert.That(healthFill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(healthFill.fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
            Assert.That(healthFill.fillAmount, Is.InRange(0f, 1f));

            string healthText = ReadChildText(root, "Health");
            if (fighter.IsHidden)
            {
                Assert.That(healthText, Is.EqualTo("?"));
            }
            else
            {
                Assert.That(healthText, Does.Match(@"^\d{1,3}%$"));
                Assert.That(
                    int.TryParse(
                        healthText.Substring(0, healthText.Length - 1),
                        out int healthPercent),
                    Is.True);
                Assert.That(healthPercent, Is.InRange(0, 100));
            }

            Transform classBadge = root.Find("ClassBadge");
            Assert.That(classBadge, Is.Not.Null, "Compact selector needs a class badge.");
            Assert.That(classBadge.GetComponent<Image>(), Is.Not.Null);
            Assert.That(
                ReadChildText(classBadge, "BadgeLabel"),
                Does.Match("^[PRMHB]$"));
            return true;
        }

        private static bool IsSelectorSelected(CombatantStatusView fighter)
        {
            Transform selected = fighter.transform.Find("Selected");
            Assert.That(selected, Is.Not.Null, "Compact selector needs visible focus feedback.");
            return selected.gameObject.activeInHierarchy;
        }

        private static void AssertWardstoneCenterInFrame(
            Camera camera,
            GameObject wardstone,
            float aspect)
        {
            Collider collision = wardstone.GetComponent<Collider>();
            Assert.That(collision, Is.Not.Null, wardstone.name);
            Vector3 viewport = camera.WorldToViewportPoint(collision.bounds.center);
            Assert.That(
                viewport.z,
                Is.GreaterThan(0f),
                wardstone.name + " is behind the camera at aspect " + aspect + ".");
            Assert.That(
                viewport.x,
                Is.InRange(0.05f, 0.95f),
                wardstone.name + " left the horizontal frame at aspect " + aspect + ".");
            Assert.That(
                viewport.y,
                Is.InRange(0.08f, 0.92f),
                wardstone.name + " left the vertical frame at aspect " + aspect + ".");
        }

        private static void AssertFlatCylinderCollider(GameObject value)
        {
            Assert.That(value, Is.Not.Null);
            Assert.That(value.GetComponent<MeshCollider>(), Is.Not.Null);
            Assert.That(value.GetComponent<CapsuleCollider>(), Is.Null);
        }

        private static int CountTriangles(IEnumerable<MeshFilter> filters)
        {
            int triangles = 0;
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                Assert.That(mesh, Is.Not.Null, filter.name);
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Assert.That(
                        mesh.GetTopology(submesh),
                        Is.EqualTo(MeshTopology.Triangles),
                        filter.name);
                    triangles += (int)(mesh.GetIndexCount(submesh) / 3);
                }
            }

            return triangles;
        }

        private static float ExpectedFortressVerticalScale(
            int bayIndex,
            string moduleName)
        {
            if (moduleName == "FortressTower")
            {
                switch (bayIndex)
                {
                    case 1:
                    case 7:
                        return 0.92f;

                    case 2:
                    case 6:
                        return 1.12f;

                    case 3:
                    case 5:
                        return 1.25f;

                    default:
                        Assert.Fail(
                            "Unexpected tower bay " + bayIndex + ".");
                        return 0f;
                }
            }

            switch (bayIndex)
            {
                case 0:
                case 8:
                    return 1.25f;

                case 4:
                    return 1.42f;

                case 9:
                case 17:
                    return 0.6f;

                case 10:
                case 16:
                    return 0.54f;

                case 11:
                case 15:
                    return 0.48f;

                case 12:
                case 14:
                    return 0.43f;

                case 13:
                    return 0.4f;

                default:
                    Assert.Fail(
                        "Unexpected wall bay " + bayIndex + ".");
                    return 0f;
            }
        }

        private static void AssertPrimaryActionIdentity(
            AbilitySlotView slotView,
            AbilityId expected)
        {
            Assert.That(slotView.Glyph, Is.Not.Null, "Every action needs a vector fallback.");
            Assert.That(slotView.Glyph.AbilityId, Is.EqualTo(expected));
            Assert.That(slotView.Glyph.VisualIdentity, Is.EqualTo((int)expected));

            Image primaryIcon = FindPrimaryMedallion(slotView)
                .Find("Icon")
                .GetComponent<Image>();
            Assert.That(primaryIcon, Is.Not.Null);
            if (slotView.UsesAuthoredIcon)
            {
                Assert.That(slotView.Glyph.enabled, Is.False);
                Assert.That(primaryIcon.enabled, Is.True);
                Assert.That(primaryIcon.sprite, Is.Not.Null);
                Assert.That(
                    primaryIcon.sprite.name,
                    Is.EqualTo(expected + "_Transparent"),
                    "Authored primary actions must not regress to opaque square source art.");
                return;
            }

            Assert.That(primaryIcon.enabled, Is.False);
            Assert.That(slotView.Glyph.enabled, Is.True);
            Assert.That(
                AbilityGlyphGraphic.HasExplicitGlyph(expected),
                Is.True,
                expected + " needs either a true-alpha source icon or an explicit vector identity.");
        }

        private static Transform FindPrimaryMedallion(AbilitySlotView slotView)
        {
            Transform authoredMedallion = slotView.transform.Find("PrimaryMedallion");
            return authoredMedallion != null
                ? authoredMedallion
                : slotView.transform;
        }

        private static bool TryFindUnobstructedWorldTargetPoint(
            Camera camera,
            EventSystem eventSystem,
            WorldTapSurface worldSurface,
            SoftTargetSelectionService targeting,
            CombatantView target,
            out Vector2 screenPoint)
        {
            screenPoint = default;
            Collider[] colliders = target.GetComponentsInChildren<Collider>(false);
            bool hasBounds = false;
            Bounds targetBounds = default;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (hasBounds)
                {
                    targetBounds.Encapsulate(collider.bounds);
                }
                else
                {
                    targetBounds = collider.bounds;
                    hasBounds = true;
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            Vector3 minimum = targetBounds.min;
            Vector3 maximum = targetBounds.max;
            float minimumX = float.PositiveInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float maximumY = float.NegativeInfinity;
            for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                Vector3 corner = new Vector3(
                    (cornerIndex & 1) == 0 ? minimum.x : maximum.x,
                    (cornerIndex & 2) == 0 ? minimum.y : maximum.y,
                    (cornerIndex & 4) == 0 ? minimum.z : maximum.z);
                Vector3 projected = camera.WorldToScreenPoint(corner);
                if (projected.z <= 0f)
                {
                    continue;
                }

                minimumX = Mathf.Min(minimumX, projected.x);
                minimumY = Mathf.Min(minimumY, projected.y);
                maximumX = Mathf.Max(maximumX, projected.x);
                maximumY = Mathf.Max(maximumY, projected.y);
            }

            if (float.IsInfinity(minimumX) || float.IsInfinity(minimumY))
            {
                return false;
            }

            float[] sampleFractions =
            {
                0.5f,
                0.35f,
                0.65f,
                0.2f,
                0.8f,
                0.05f,
                0.95f,
            };
            PointerEventData probe = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
            };
            List<RaycastResult> uiHits = new List<RaycastResult>();
            for (int yIndex = 0; yIndex < sampleFractions.Length; yIndex++)
            {
                for (int xIndex = 0; xIndex < sampleFractions.Length; xIndex++)
                {
                    Vector2 candidatePoint = new Vector2(
                        Mathf.Lerp(minimumX, maximumX, sampleFractions[xIndex]),
                        Mathf.Lerp(minimumY, maximumY, sampleFractions[yIndex]));
                    if (!camera.pixelRect.Contains(candidatePoint))
                    {
                        continue;
                    }

                    probe.position = candidatePoint;
                    uiHits.Clear();
                    eventSystem.RaycastAll(probe, uiHits);
                    if (uiHits.Count == 0 ||
                        uiHits[0].gameObject != worldSurface.gameObject ||
                        !targeting.TryGetScreenCandidate(
                            candidatePoint,
                            out SoftTargetCandidate candidate) ||
                        candidate.TargetId != target.Id.Value)
                    {
                        continue;
                    }

                    screenPoint = candidatePoint;
                    return true;
                }
            }

            return false;
        }

        private static bool BoundsIntersectsViewport(Camera camera, Bounds bounds)
        {
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            float minimumX = float.PositiveInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float maximumY = float.NegativeInfinity;
            bool hasPointInFront = false;
            for (int index = 0; index < 8; index++)
            {
                Vector3 corner = new Vector3(
                    (index & 1) == 0 ? minimum.x : maximum.x,
                    (index & 2) == 0 ? minimum.y : maximum.y,
                    (index & 4) == 0 ? minimum.z : maximum.z);
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                if (viewport.z <= 0f)
                {
                    continue;
                }

                hasPointInFront = true;
                minimumX = Mathf.Min(minimumX, viewport.x);
                minimumY = Mathf.Min(minimumY, viewport.y);
                maximumX = Mathf.Max(maximumX, viewport.x);
                maximumY = Mathf.Max(maximumY, viewport.y);
            }

            return hasPointInFront &&
                maximumX >= 0f && minimumX <= 1f &&
                maximumY >= 0f && minimumY <= 1f;
        }

        private static bool BoundsFitsViewport(
            Camera camera,
            Bounds bounds,
            float edgeTolerance)
        {
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            for (int index = 0; index < 8; index++)
            {
                Vector3 corner = new Vector3(
                    (index & 1) == 0 ? minimum.x : maximum.x,
                    (index & 2) == 0 ? minimum.y : maximum.y,
                    (index & 4) == 0 ? minimum.z : maximum.z);
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                if (viewport.z <= 0f ||
                    viewport.x < -edgeTolerance ||
                    viewport.x > 1f + edgeTolerance ||
                    viewport.y < -edgeTolerance ||
                    viewport.y > 1f + edgeTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private static float GetViewportHeight(
            Camera camera,
            CombatantView combatant)
        {
            return Mathf.Abs(
                camera.WorldToViewportPoint(combatant.AimPoint.position).y -
                camera.WorldToViewportPoint(combatant.transform.position).y);
        }

        private static void AssertSiegeVerticalSpanInFrame(
            Camera camera,
            Vector3 feet,
            Vector3 top,
            float minimumViewportY,
            float maximumViewportY,
            string context)
        {
            Vector3 feetViewport = camera.WorldToViewportPoint(feet);
            Vector3 topViewport = camera.WorldToViewportPoint(top);
            Assert.That(
                feetViewport.z,
                Is.GreaterThan(camera.nearClipPlane),
                context + " feet crossed the near clip plane.");
            Assert.That(
                topViewport.z,
                Is.GreaterThan(camera.nearClipPlane),
                context + " top crossed the near clip plane.");
            Assert.That(
                feetViewport.x,
                Is.InRange(0.025f, 0.975f),
                context + " feet left the horizontal frame.");
            Assert.That(
                topViewport.x,
                Is.InRange(0.025f, 0.975f),
                context + " top left the horizontal frame.");
            Assert.That(
                feetViewport.y,
                Is.InRange(minimumViewportY, maximumViewportY),
                context + " feet left the vertical frame.");
            Assert.That(
                topViewport.y,
                Is.InRange(minimumViewportY, maximumViewportY),
                context + " top left the vertical frame.");
        }

        private static void AssertSiegeSpanAvoidsActionHud(
            Camera camera,
            Vector3 feet,
            Vector3 top,
            RectTransform[] actionControls,
            float aspect,
            string context)
        {
            const float referenceHeight = 1080f;
            const float actorHalfWidth = 28f;
            const float actorVerticalPadding = 8f;
            float referenceWidth = referenceHeight * aspect;
            Vector3 feetViewport = camera.WorldToViewportPoint(feet);
            Vector3 topViewport = camera.WorldToViewportPoint(top);
            Rect actorRect = Rect.MinMaxRect(
                (Mathf.Min(feetViewport.x, topViewport.x) - 0.5f) *
                    referenceWidth - actorHalfWidth,
                (Mathf.Min(feetViewport.y, topViewport.y) - 0.5f) *
                    referenceHeight - actorVerticalPadding,
                (Mathf.Max(feetViewport.x, topViewport.x) - 0.5f) *
                    referenceWidth + actorHalfWidth,
                (Mathf.Max(feetViewport.y, topViewport.y) - 0.5f) *
                    referenceHeight + actorVerticalPadding);

            for (int slot = 0; slot < actionControls.Length; slot++)
            {
                RectTransform control = actionControls[slot];
                Vector2 anchor = control.anchorMin;
                Vector2 anchorPoint = new Vector2(
                    Mathf.Lerp(-referenceWidth * 0.5f, referenceWidth * 0.5f, anchor.x),
                    Mathf.Lerp(-referenceHeight * 0.5f, referenceHeight * 0.5f, anchor.y));
                Vector2 pivotPoint = anchorPoint + control.anchoredPosition;
                Vector2 controlScale = new Vector2(
                    Mathf.Abs(control.localScale.x),
                    Mathf.Abs(control.localScale.y));
                Assert.That(controlScale.x, Is.EqualTo(controlScale.y).Within(0.001f),
                    "Circular action controls require uniform presentation scale.");
                Vector2 center = pivotPoint + Vector2.Scale(control.rect.center, controlScale);
                float radius = Mathf.Min(control.rect.width, control.rect.height) *
                    controlScale.x * 0.5f;
                AssertSiegeActorAvoidsHudCircle(actorRect, center, radius,
                    context + " action control " + slot + " at aspect " + aspect);

                // Test actual visible chrome too: the gesture shadow is wider
                // than its hit disc and displaced downward. Transparent square
                // corners are not occlusion; every authored face is circular.
                string[] visibleCircleNames = slot < AbilityKit.GestureSlotCount
                    ? new[] { "ClusterShadow" }
                    : new[] { "MedallionShadow", "ReadyPulse" };
                foreach (string childName in visibleCircleNames)
                {
                    RectTransform circle = (RectTransform)control.Find(childName);
                    Assert.That(circle, Is.Not.Null, childName);
                    Rect localCircle = GetRectRelativeTo(control, circle);
                    Vector2 circleCenter = pivotPoint +
                        Vector2.Scale(localCircle.center, controlScale);
                    float circleRadius = Mathf.Min(localCircle.width, localCircle.height) *
                        controlScale.x * 0.5f;
                    AssertSiegeActorAvoidsHudCircle(actorRect, circleCenter, circleRadius,
                        context + " action " + slot + " " + childName +
                        " at aspect " + aspect);
                }
            }
        }

        private static void AssertSiegeActorAvoidsHudCircle(
            Rect actorRect,
            Vector2 circleCenter,
            float circleRadius,
            string context)
        {
            Vector2 closestActorPoint = new Vector2(
                Mathf.Clamp(circleCenter.x, actorRect.xMin, actorRect.xMax),
                Mathf.Clamp(circleCenter.y, actorRect.yMin, actorRect.yMax));
            Assert.That(
                Vector2.Distance(circleCenter, closestActorPoint),
                Is.GreaterThanOrEqualTo(circleRadius),
                context + " overlaps the padded actor span. Actor=" + actorRect +
                ", circle=" + circleCenter + ", radius=" + circleRadius + ".");
        }

        private static Rect GetViewportBounds(Camera camera, Bounds bounds)
        {
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            float minimumX = float.PositiveInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float maximumY = float.NegativeInfinity;
            for (int index = 0; index < 8; index++)
            {
                Vector3 corner = new Vector3(
                    (index & 1) == 0 ? minimum.x : maximum.x,
                    (index & 2) == 0 ? minimum.y : maximum.y,
                    (index & 4) == 0 ? minimum.z : maximum.z);
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                Assert.That(
                    viewport.z,
                    Is.GreaterThan(0f),
                    "Viewport bounds require every corner to remain in front of the camera.");
                minimumX = Mathf.Min(minimumX, viewport.x);
                minimumY = Mathf.Min(minimumY, viewport.y);
                maximumX = Mathf.Max(maximumX, viewport.x);
                maximumY = Mathf.Max(maximumY, viewport.y);
            }

            return Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY);
        }

        private static float MinimumPlanarBoundsRadius(MeshRenderer renderer)
        {
            Assert.That(renderer, Is.Not.Null);
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Assert.That(filter, Is.Not.Null, renderer.name);
            Assert.That(filter.sharedMesh, Is.Not.Null, renderer.name);

            Bounds bounds = filter.sharedMesh.bounds;
            Matrix4x4 matrix = filter.transform.localToWorldMatrix;
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 planarCenter = new Vector3(center.x, 0f, center.z);
            Assert.That(planarCenter.sqrMagnitude, Is.GreaterThan(0.001f));
            Vector3 radial = planarCenter.normalized;
            Vector3 extentX = matrix.MultiplyVector(
                new Vector3(bounds.extents.x, 0f, 0f));
            Vector3 extentY = matrix.MultiplyVector(
                new Vector3(0f, bounds.extents.y, 0f));
            Vector3 extentZ = matrix.MultiplyVector(
                new Vector3(0f, 0f, bounds.extents.z));
            float radialExtent =
                Mathf.Abs(Vector3.Dot(extentX, radial)) +
                Mathf.Abs(Vector3.Dot(extentY, radial)) +
                Mathf.Abs(Vector3.Dot(extentZ, radial));
            return planarCenter.magnitude - radialExtent;
        }

        private static IEnumerator WaitForWorldStream(
            WorldZoneStreamController streamer,
            System.Func<bool> condition)
        {
            const float timeoutSeconds = 15f;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                condition(),
                Is.True,
                "World streaming did not settle within " + timeoutSeconds +
                " seconds. Status=" + streamer.Status +
                ", pending=" + streamer.PendingOperation +
                ", error='" + streamer.LastError + "'.");
        }

        private static void ResolveLevelingPackForController(
            PrototypeGameController controller,
            LevelingPackDefinition pack,
            int eventTick)
        {
            System.Reflection.MethodInfo observe =
                typeof(PrototypeGameController).GetMethod(
                    "ObserveLevelingCombatEvent",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            Assert.That(observe, Is.Not.Null);
            for (int mobIndex = 0; mobIndex < pack.MobCount; mobIndex++)
            {
                CombatEvent death = CombatEvent.Create(
                    eventTick,
                    CombatEventKind.Death,
                    new ParticipantId(LevelingRunController.PlayerParticipantValue),
                    pack.GetMob(mobIndex).Participant);
                observe.Invoke(controller, new object[] { death });
            }
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            Assert.That(instance, Is.Not.Null);
            System.Reflection.FieldInfo field = instance.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(instance);
        }

        private static void SetPrivateField<T>(
            object instance,
            string fieldName,
            T value)
        {
            Assert.That(instance, Is.Not.Null);
            System.Reflection.FieldInfo field = instance.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(instance, value);
        }

        private static void AssertDirectionalIconRegion(
            Transform region,
            string context)
        {
            Assert.That(region, Is.Not.Null, context);
            Assert.That(
                region.GetComponent<Image>(),
                Is.Null,
                context + " must remain a transparent region inside one shared disc.");
            Assert.That(region.Find("MedallionShadow"), Is.Null, context);
            Assert.That(region.Find("Surface"), Is.Null, context);
            Assert.That(region.Find("OuterBezel"), Is.Null, context);
            Assert.That(region.Find("DirectionAccent"), Is.Null, context);
            Assert.That(region.Find("MedallionFrameArt"), Is.Null, context);

            RectTransform icon = (RectTransform)region.Find("Icon");
            RectTransform glyph = (RectTransform)region.Find("Glyph");
            Assert.That(icon, Is.Not.Null, context + " icon");
            Assert.That(glyph, Is.Not.Null, context + " glyph");
            Assert.That(icon.rect.size, Is.EqualTo(new Vector2(48f, 48f)));
            Assert.That(glyph.rect.size, Is.EqualTo(new Vector2(48f, 48f)));
            Assert.That(icon.GetComponent<Image>().raycastTarget, Is.False);
            string[] expectedChildren = region.name == "AlternateUp"
                ? new[] { "Icon", "Glyph", "Label", "Cooldown", "CooldownText" }
                : new[] { "Icon", "Glyph" };
            Assert.That(
                region.Cast<Transform>().Select(child => child.name),
                Is.EquivalentTo(expectedChildren),
                context + " may not reintroduce per-direction chrome.");
        }

        private static float ColorChannelRange(Color color)
        {
            float minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            float maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            return maximum - minimum;
        }
    }
}
