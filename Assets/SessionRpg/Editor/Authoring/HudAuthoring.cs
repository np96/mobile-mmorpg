using System;
using System.Collections.Generic;
using SessionRpg.Core;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Editor.Authoring
{
    internal readonly struct AuthoredHud
    {
        internal AuthoredHud(
            GameObject root,
            PrototypeHud hud,
            CombatInputSource inputSource,
            FloatingJoystick joystick,
            WorldTapSurface worldSurface,
            AbilityButton[] abilityButtons,
            OffscreenThreatPresenter offscreenThreatPresenter)
        {
            Root = root;
            Hud = hud;
            InputSource = inputSource;
            Joystick = joystick;
            WorldSurface = worldSurface;
            AbilityButtons = abilityButtons;
            OffscreenThreatPresenter = offscreenThreatPresenter;
        }

        internal GameObject Root { get; }

        internal PrototypeHud Hud { get; }

        internal CombatInputSource InputSource { get; }

        internal FloatingJoystick Joystick { get; }

        internal WorldTapSurface WorldSurface { get; }

        internal AbilityButton[] AbilityButtons { get; }

        internal OffscreenThreatPresenter OffscreenThreatPresenter { get; }
    }

    internal static class HudAuthoring
    {
        private const float LargeGestureControlScale = 1.3f;
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Color Ink = new Color32(9, 14, 25, 255);
        private static readonly Color Surface = new Color32(18, 28, 45, 244);
        private static readonly Color SurfaceRaised = new Color32(29, 43, 65, 248);
        private static readonly Color Line = new Color32(79, 103, 132, 150);
        private static readonly Color TextPrimary = new Color32(239, 245, 250, 255);
        private static readonly Color TextSecondary = new Color32(159, 180, 201, 255);
        private static readonly Color TextWarm = new Color32(228, 198, 127, 255);
        private static readonly Color Cyan = new Color32(67, 208, 229, 255);
        private static readonly Color Ember = new Color32(244, 143, 54, 255);
        private static readonly Color Healer = new Color32(75, 205, 151, 255);
        private static readonly Color Rogue = new Color32(238, 183, 71, 255);
        private static readonly Color Mage = new Color32(103, 171, 244, 255);
        private static readonly Color Danger = new Color32(224, 76, 68, 255);
        private static readonly Color Obsidian = new Color32(7, 6, 10, 255);
        private static readonly Color BlackenedIron = new Color32(38, 35, 37, 255);
        private static readonly Color IronHighlight = new Color32(75, 67, 63, 255);
        private static readonly Color AgedBrass = new Color32(126, 91, 48, 255);
        private static readonly Color BrassHighlight = new Color32(190, 145, 76, 255);

        internal static AuthoredHud Create(Transform parent, Font font = null)
        {
            return BuildInternal(parent, font);
        }

        internal static AuthoredHud Build(Font font)
        {
            return BuildInternal(null, font);
        }

        private static AuthoredHud BuildInternal(Transform parent, Font font)
        {
            Font resolvedFont = ResolveFont(font);
            Sprite panelSprite = BuiltinUiSprite();
            Sprite roundSprite = BuiltinKnobSprite(panelSprite);
            Sprite medallionFrameSprite = HudMedallionFrameAuthoring.Build();
            AbilityIconCatalog abilityIconCatalog = AbilityIconAuthoring.Build();

            RectTransform rootRect = CreateRect(
                "HUD",
                parent,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero);
            Canvas canvas = rootRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 30;

            CanvasScaler scaler = rootRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100f;
            rootRect.gameObject.AddComponent<GraphicRaycaster>();

            PrototypeHud hud = rootRect.gameObject.AddComponent<PrototypeHud>();
            CombatInputSource inputSource = rootRect.gameObject.AddComponent<CombatInputSource>();

            RectTransform safeArea = CreateRect(
                "SafeArea",
                rootRect,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            GameplayReferences gameplay = BuildGameplay(
                safeArea,
                resolvedFont,
                panelSprite,
                roundSprite,
                medallionFrameSprite);
            HomeReferences home = BuildHome(safeArea, resolvedFont, panelSprite);
            ResultsReferences results = BuildResults(safeArea, resolvedFont, panelSprite);

            WireInput(inputSource, gameplay);
            WireHud(hud, home, gameplay, results, abilityIconCatalog);

            home.Root.SetActive(true);
            gameplay.Root.SetActive(false);
            results.Root.SetActive(false);
            gameplay.CastRoot.SetActive(false);
            SetAllSelectionMarkers(home, false);
            home.StartButton.interactable = false;

            return new AuthoredHud(
                rootRect.gameObject,
                hud,
                inputSource,
                gameplay.Joystick,
                gameplay.WorldSurface,
                gameplay.AbilityButtons,
                gameplay.OffscreenThreatPresenter);
        }

        private static HomeReferences BuildHome(RectTransform parent, Font font, Sprite panelSprite)
        {
            RectTransform root = CreatePanel(
                "Home",
                parent,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color(0.025f, 0.04f, 0.065f, 0.97f),
                null,
                true);

            CreatePanel(
                "TopGlow",
                root,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -4f),
                new Vector2(0f, 8f),
                Cyan,
                null,
                false);

            RectTransform card = CreatePanel(
                "SelectionCard",
                root,
                Center,
                Center,
                Center,
                new Vector2(0f, -12f),
                new Vector2(1380f, 790f),
                Surface,
                panelSprite,
                true);
            AddOutline(card.gameObject, new Color(0.25f, 0.55f, 0.67f, 0.36f), new Vector2(2f, -2f));

            CreateText(
                "Eyebrow",
                card,
                font,
                "ARENA-FIRST ACTION RPG",
                20,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Cyan,
                new Vector2(0.2f, 0.88f),
                new Vector2(0.8f, 0.97f),
                Vector2.zero,
                Vector2.zero);
            Text title = CreateText(
                "Title",
                card,
                font,
                "ARENA  //  PVE ADVENTURES",
                52,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                new Vector2(0.1f, 0.75f),
                new Vector2(0.9f, 0.9f),
                Vector2.zero,
                Vector2.zero);
            AddShadow(title.gameObject, new Color(0f, 0f, 0f, 0.55f), new Vector2(3f, -3f));

            CreateText(
                "RoleHeading",
                card,
                font,
                "CHOOSE YOUR CLASS",
                22,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                TextSecondary,
                new Vector2(0.065f, 0.64f),
                new Vector2(0.56f, 0.72f),
                Vector2.zero,
                Vector2.zero);
            CreateText(
                "ModeHeading",
                card,
                font,
                "CHOOSE A MODE",
                22,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                TextSecondary,
                new Vector2(0.61f, 0.64f),
                new Vector2(0.95f, 0.72f),
                Vector2.zero,
                Vector2.zero);

            Button healerButton = CreateButton(
                "Healer",
                card,
                font,
                "PRIEST",
                new Vector2(0.065f, 0.43f),
                new Vector2(0.18f, 0.63f),
                Healer,
                panelSprite,
                24,
                out GameObject healerMarker);
            Button rogueButton = CreateButton(
                "Rogue",
                card,
                font,
                "ROGUE",
                new Vector2(0.19f, 0.43f),
                new Vector2(0.305f, 0.63f),
                Rogue,
                panelSprite,
                24,
                out GameObject rogueMarker);
            Button mageButton = CreateButton(
                "Mage",
                card,
                font,
                "MAGE",
                new Vector2(0.315f, 0.43f),
                new Vector2(0.43f, 0.63f),
                Mage,
                panelSprite,
                24,
                out GameObject mageMarker);
            Button hunterButton = CreateButton(
                "Hunter",
                card,
                font,
                "HUNTER",
                new Vector2(0.44f, 0.43f),
                new Vector2(0.555f, 0.63f),
                new Color32(138, 184, 86, 255),
                panelSprite,
                24,
                out GameObject hunterMarker);

            CreateText(
                "SpecializationHeading",
                card,
                font,
                "SPECIALIZATION",
                17,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                TextSecondary,
                new Vector2(0.065f, 0.355f),
                new Vector2(0.555f, 0.425f),
                Vector2.zero,
                Vector2.zero);

            RectTransform disciplineSpecializationRoot = CreateRect(
                "DisciplineSpecialization",
                card,
                new Vector2(0.065f, 0.245f),
                new Vector2(0.555f, 0.35f),
                Center,
                Vector2.zero,
                Vector2.zero);
            Button disciplineButton = CreateButton(
                "Discipline",
                disciplineSpecializationRoot,
                font,
                "DISCIPLINE",
                Vector2.zero,
                new Vector2(0.485f, 1f),
                Healer,
                panelSprite,
                19,
                out GameObject disciplineMarker);
            Button shadowButton = CreateButton(
                "Shadow",
                disciplineSpecializationRoot,
                font,
                "SHADOW",
                new Vector2(0.515f, 0f),
                Vector2.one,
                new Color32(151, 91, 216, 255),
                panelSprite,
                19,
                out GameObject shadowMarker);

            RectTransform rogueSpecializationRoot = CreateRect(
                "RogueSpecialization",
                card,
                new Vector2(0.065f, 0.245f),
                new Vector2(0.555f, 0.35f),
                Center,
                Vector2.zero,
                Vector2.zero);
            Button shadowDanceButton = CreateButton(
                "ShadowDance",
                rogueSpecializationRoot,
                font,
                "SHADOW DANCE",
                Vector2.zero,
                new Vector2(0.485f, 1f),
                new Color32(159, 111, 229, 255),
                panelSprite,
                19,
                out GameObject shadowDanceMarker);
            Button mutilateButton = CreateButton(
                "Mutilate",
                rogueSpecializationRoot,
                font,
                "MUTILATE",
                new Vector2(0.515f, 0f),
                Vector2.one,
                Rogue,
                panelSprite,
                19,
                out GameObject mutilateMarker);

            RectTransform mageSpecializationRoot = CreateRect(
                "MageSpecialization",
                card,
                new Vector2(0.065f, 0.245f),
                new Vector2(0.555f, 0.35f),
                Center,
                Vector2.zero,
                Vector2.zero);
            Button fireButton = CreateButton(
                "Fire",
                mageSpecializationRoot,
                font,
                "FIRE",
                Vector2.zero,
                new Vector2(0.485f, 1f),
                Ember,
                panelSprite,
                19,
                out GameObject fireMarker);
            Button frostButton = CreateButton(
                "Frost",
                mageSpecializationRoot,
                font,
                "FROST",
                new Vector2(0.515f, 0f),
                Vector2.one,
                Mage,
                panelSprite,
                19,
                out GameObject frostMarker);

            RectTransform hunterSpecializationRoot = CreateRect(
                "HunterSpecialization",
                card,
                new Vector2(0.065f, 0.245f),
                new Vector2(0.555f, 0.35f),
                Center,
                Vector2.zero,
                Vector2.zero);
            Button marksmanshipButton = CreateButton(
                "Marksmanship",
                hunterSpecializationRoot,
                font,
                "MARKSMANSHIP",
                Vector2.zero,
                Vector2.one,
                new Color32(138, 184, 86, 255),
                panelSprite,
                19,
                out GameObject marksmanshipMarker);

            disciplineSpecializationRoot.gameObject.SetActive(false);
            rogueSpecializationRoot.gameObject.SetActive(false);
            mageSpecializationRoot.gameObject.SetActive(false);
            hunterSpecializationRoot.gameObject.SetActive(false);

            Button arenaButton = CreateButton(
                "Arena2v2",
                card,
                font,
                "2 v 2  ARENA\nPRIMARY  •  BOT FILL",
                new Vector2(0.61f, 0.54f),
                new Vector2(0.935f, 0.63f),
                Cyan,
                panelSprite,
                20,
                out GameObject arenaMarker);
            Button siegeButton = CreateButton(
                "HeroSiege",
                card,
                font,
                "HERO SIEGE\nSOLO / 4 LANES  •  12 TIMED WAVES",
                new Vector2(0.61f, 0.45f),
                new Vector2(0.935f, 0.53f),
                new Color32(89, 173, 116, 255),
                panelSprite,
                17,
                out GameObject siegeMarker);
            Button bossButton = CreateButton(
                "BossTrio",
                card,
                font,
                "DUNGEON\nLOCAL  •  3 BOTS  •  4 FLOORS",
                new Vector2(0.61f, 0.36f),
                new Vector2(0.935f, 0.44f),
                Ember,
                panelSprite,
                20,
                out GameObject bossMarker);
            Button levelingButton = CreateButton(
                "LevelingExpedition",
                card,
                font,
                "TRAINING  •  SKIPPABLE\n3 → 7 ACTIONS  •  ASHEN CAUSEWAY",
                new Vector2(0.61f, 0.27f),
                new Vector2(0.935f, 0.35f),
                new Color32(174, 138, 69, 255),
                panelSprite,
                19,
                out GameObject levelingMarker);

            Button pveLevelingButton = CreateButton(
                "PveLeveling", card, font,
                "PVE LEVELING\nMOB HUNTING  •  SAVED ON THIS DEVICE",
                new Vector2(0.61f, 0.18f), new Vector2(0.935f, 0.26f),
                new Color32(174, 138, 69, 255), panelSprite, 17,
                out GameObject pveLevelingMarker);

            RectTransform siegeVariantRoot = CreateRect(
                "SiegeVariantSelection",
                card,
                new Vector2(0.065f, 0.155f),
                new Vector2(0.555f, 0.235f),
                Center,
                Vector2.zero,
                Vector2.zero);
            Button siegeSoloLaneButton = CreateButton(
                "SiegeSoloLane",
                siegeVariantRoot,
                font,
                "SOLO LANE\n1 HERO  •  EAST",
                Vector2.zero,
                new Vector2(0.485f, 1f),
                new Color32(89, 173, 116, 255),
                panelSprite,
                11,
                out GameObject siegeSoloLaneMarker);
            Button siegeFourLanesButton = CreateButton(
                "SiegeFourLanes",
                siegeVariantRoot,
                font,
                "FOUR LANES\n1 HERO + 3 BOTS",
                new Vector2(0.515f, 0f),
                Vector2.one,
                Cyan,
                panelSprite,
                15,
                out GameObject siegeFourLanesMarker);
            siegeVariantRoot.gameObject.SetActive(false);

            Text classDetailLabel = CreateText(
                "ClassDetail",
                card,
                font,
                string.Empty,
                16,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                TextSecondary,
                new Vector2(0.065f, 0.175f),
                new Vector2(0.555f, 0.235f),
                new Vector2(10f, 2f),
                new Vector2(-10f, -2f));
            classDetailLabel.resizeTextForBestFit = true;
            classDetailLabel.resizeTextMinSize = 12;
            classDetailLabel.resizeTextMaxSize = 16;
            Button startButton = CreateButton(
                "Start",
                card,
                font,
                "ENTER COMBAT",
                new Vector2(0.34f, 0.035f),
                new Vector2(0.66f, 0.15f),
                Ember,
                panelSprite,
                29,
                out GameObject unusedStartMarker);
            unusedStartMarker.SetActive(false);
            Text startActionLabel = startButton.transform
                .Find("Label")
                .GetComponent<Text>();
            startActionLabel.resizeTextForBestFit = true;
            startActionLabel.resizeTextMinSize = 18;
            startActionLabel.resizeTextMaxSize = 29;
            Button skipTrainingButton = CreateButton(
                "SkipTraining",
                card,
                font,
                "SKIP TO ARENA",
                new Vector2(0.68f, 0.035f),
                new Vector2(0.935f, 0.15f),
                Cyan,
                panelSprite,
                22,
                out GameObject unusedSkipMarker);
            unusedSkipMarker.SetActive(false);
            skipTrainingButton.gameObject.SetActive(false);
            BuildKitViewer(root, card, font, panelSprite);

            return new HomeReferences(
                root.gameObject,
                healerButton,
                rogueButton,
                mageButton,
                hunterButton,
                healerMarker,
                rogueMarker,
                mageMarker,
                hunterMarker,
                disciplineSpecializationRoot.gameObject,
                rogueSpecializationRoot.gameObject,
                mageSpecializationRoot.gameObject,
                hunterSpecializationRoot.gameObject,
                disciplineButton,
                shadowButton,
                shadowDanceButton,
                mutilateButton,
                fireButton,
                frostButton,
                marksmanshipButton,
                disciplineMarker,
                shadowMarker,
                shadowDanceMarker,
                mutilateMarker,
                fireMarker,
                frostMarker,
                marksmanshipMarker,
                arenaButton,
                siegeButton,
                bossButton,
                levelingButton,
                arenaMarker,
                siegeMarker,
                bossMarker,
                levelingMarker,
                startButton,
                skipTrainingButton,
                classDetailLabel,
                siegeVariantRoot.gameObject,
                siegeSoloLaneButton,
                siegeFourLanesButton,
                siegeSoloLaneMarker,
                siegeFourLanesMarker,
                pveLevelingButton,
                pveLevelingMarker);
        }

        private static GameplayReferences BuildGameplay(
            RectTransform parent,
            Font font,
            Sprite panelSprite,
            Sprite roundSprite,
            Sprite medallionFrameSprite)
        {
            RectTransform root = CreateRect(
                "Gameplay",
                parent,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero);

            RectTransform lookRect = CreatePanel(
                "WorldSurface",
                root,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color(0f, 0f, 0f, 0f),
                null,
                true);
            WorldTapSurface worldSurface = lookRect.gameObject.AddComponent<WorldTapSurface>();
            Text cameraOrbitHint = CreateText(
                "CameraOrbitHint",
                root,
                font,
                "DRAG EMPTY FIELD TO ORBIT",
                13,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b, 0.58f),
                Vector2.zero,
                Vector2.zero,
                new Vector2(32f, 316f),
                new Vector2(360f, 340f));
            cameraOrbitHint.raycastTarget = false;
            CanvasGroup cameraOrbitHintCanvasGroup =
                cameraOrbitHint.gameObject.AddComponent<CanvasGroup>();
            cameraOrbitHintCanvasGroup.interactable = false;
            cameraOrbitHintCanvasGroup.blocksRaycasts = false;

            FloatingJoystick joystick = BuildJoystick(root, panelSprite, roundSprite);

            Text teamLabel = CreateText(
                "FriendlyHeading",
                root,
                font,
                "ALLIES",
                18,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Cyan,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(46f, -60f),
                new Vector2(430f, -28f));
            teamLabel.gameObject.SetActive(false);
            Text hostileHeading = CreateText(
                "HostileHeading",
                root,
                font,
                "OPPONENTS",
                18,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                Danger,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-430f, -60f),
                new Vector2(-46f, -28f));
            hostileHeading.gameObject.SetActive(false);

            RectTransform friendlyFighterRoot = BuildFighterColumn(
                "FriendlyFighters",
                root,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -20f));
            RectTransform hostileFighterRoot = BuildFighterColumn(
                "HostileFighters",
                root,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -20f));

            CombatantStatusView[] fighterViews =
                new CombatantStatusView[PrototypeHud.MaximumFighterViewCount];
            FighterCardReferences firstFighter = default;
            for (int slot = 0; slot < fighterViews.Length; slot++)
            {
                RectTransform column = slot < 4 ? friendlyFighterRoot : hostileFighterRoot;
                FighterCardReferences fighter = BuildFighterCard(
                    "Fighter_" + (slot + 1),
                    column,
                    font,
                    roundSprite);
                fighterViews[slot] = fighter.View;
                fighter.View.gameObject.SetActive(false);
                if (slot == 0)
                {
                    firstFighter = fighter;
                }
            }

            OffscreenThreatPresenter offscreenThreatPresenter =
                BuildOffscreenThreatPresenter(root, font, panelSprite, roundSprite);

            Text healthLabel = firstFighter.HealthLabel;
            Image healthFill = firstFighter.HealthFill;

            RectTransform objectiveCard = CreatePanel(
                "Objective",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f),
                new Vector2(680f, 54f),
                new Color32(7, 11, 18, 248),
                panelSprite,
                false);
            AddOutline(
                objectiveCard.gameObject,
                new Color32(111, 82, 46, 235),
                new Vector2(2f, -2f));
            AddShadow(
                objectiveCard.gameObject,
                new Color(0f, 0f, 0f, 0.72f),
                new Vector2(4f, -5f));
            RectTransform objectiveInnerFrame = CreatePanel(
                "InnerFrame",
                objectiveCard,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                new Vector2(-10f, -10f),
                new Color32(10, 15, 24, 250),
                panelSprite,
                false);
            AddOutline(
                objectiveInnerFrame.gameObject,
                new Color32(164, 126, 72, 135),
                new Vector2(1f, -1f));
            Text objectiveLabel = CreateText(
                "ObjectiveLabel",
                objectiveCard,
                font,
                "DEFEAT THE OPPOSING TEAM",
                19,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                Vector2.zero,
                Vector2.one,
                new Vector2(18f, 2f),
                new Vector2(-18f, -2f));
            objectiveLabel.resizeTextForBestFit = true;
            objectiveLabel.resizeTextMinSize = 12;
            objectiveLabel.resizeTextMaxSize = 19;
            CreatePanel(
                "Accent",
                objectiveCard,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 2f),
                new Vector2(-28f, 4f),
                Ember,
                null,
                false);

            RectTransform progressionRoot = CreatePanel(
                "LevelProgression",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -174f),
                new Vector2(440f, 34f),
                new Color32(5, 9, 15, 244),
                panelSprite,
                false);
            AddOutline(
                progressionRoot.gameObject,
                new Color32(162, 119, 56, 205),
                new Vector2(1f, -1f));
            Image experienceFill = CreateFill(
                progressionRoot,
                "ExperienceFill",
                new Color32(190, 129, 48, 190),
                Image.FillMethod.Horizontal);
            experienceFill.rectTransform.sizeDelta = new Vector2(-8f, -8f);
            experienceFill.fillAmount = 0f;
            Text levelLabel = CreateText(
                "LevelLabel",
                progressionRoot,
                font,
                "LEVEL 1",
                16,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                TextPrimary,
                new Vector2(0f, 0f),
                new Vector2(0.45f, 1f),
                new Vector2(14f, 0f),
                Vector2.zero);
            Text experienceLabel = CreateText(
                "ExperienceLabel",
                progressionRoot,
                font,
                "0 / 80 XP",
                15,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                TextPrimary,
                new Vector2(0.45f, 0f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-14f, 0f));
            progressionRoot.gameObject.SetActive(false);

            RectTransform targetRoot = CreatePanel(
                "Target",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -82f),
                new Vector2(440f, 80f),
                new Color(0.055f, 0.08f, 0.12f, 0.92f),
                panelSprite,
                false);
            Image targetSurface = targetRoot.GetComponent<Image>();
            AddOutline(targetRoot.gameObject, new Color(0.8f, 0.3f, 0.25f, 0.36f), new Vector2(2f, -2f));
            RectTransform targetRelationAccent = CreatePanel(
                "RelationAccent",
                targetRoot,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(3f, 0f),
                new Vector2(5f, -8f),
                Danger,
                null,
                false);
            Text targetRelationLabel = CreateText(
                "Relation",
                targetRoot,
                font,
                "ENEMY",
                10,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Danger,
                new Vector2(0.04f, 0.72f),
                new Vector2(0.18f, 0.96f),
                Vector2.zero,
                Vector2.zero);
            targetRelationLabel.gameObject.SetActive(false);
            Text targetParticipantLabel = CreateText(
                "Participant",
                targetRoot,
                font,
                "#3",
                10,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                TextSecondary,
                new Vector2(0.82f, 0.79f),
                new Vector2(0.96f, 0.96f),
                Vector2.zero,
                Vector2.zero);
            targetParticipantLabel.gameObject.SetActive(false);
            Text targetLabel = CreateText(
                "TargetName",
                targetRoot,
                font,
                "NO TARGET",
                17,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                new Vector2(0.06f, 0.72f),
                new Vector2(0.66f, 0.96f),
                Vector2.zero,
                Vector2.zero);
            RectTransform targetTrack = CreatePanel(
                "TargetHealthTrack",
                targetRoot,
                new Vector2(0.06f, 0.46f),
                new Vector2(0.94f, 0.68f),
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color32(5, 9, 16, 230),
                panelSprite,
                false);
            Image targetBarrierFill = CreateFill(
                targetTrack,
                "BarrierFill",
                new Color32(84, 178, 246, 235),
                Image.FillMethod.Horizontal);
            targetBarrierFill.fillAmount = 0f;
            Image targetHealthFill = CreateFill(
                targetTrack,
                "HealthFill",
                Healer,
                Image.FillMethod.Horizontal);
            targetHealthFill.fillAmount = 1f;
            Text targetHealthLabel = CreateText(
                "Health",
                targetTrack,
                font,
                "110 / 110  •  100%",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                Vector2.zero,
                Vector2.one,
                new Vector2(8f, 2f),
                new Vector2(-8f, -2f));
            Text targetBarrierLabel = CreateText(
                "Barrier",
                targetRoot,
                font,
                string.Empty,
                10,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                Mage,
                new Vector2(0.70f, 0.46f),
                new Vector2(0.94f, 0.68f),
                Vector2.zero,
                Vector2.zero);
            RectTransform targetResourceRoot = CreatePanel(
                "TargetResource",
                targetRoot,
                new Vector2(0.06f, 0.32f),
                new Vector2(0.94f, 0.42f),
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color32(5, 9, 16, 230),
                panelSprite,
                false);
            Image targetResourceFill = CreateFill(
                targetResourceRoot,
                "Fill",
                Mage,
                Image.FillMethod.Horizontal);
            targetResourceFill.fillAmount = 1f;
            Text targetResourceTypeLabel = CreateText(
                "Type",
                targetResourceRoot,
                font,
                "MANA",
                9,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Mage,
                new Vector2(0.03f, 0f),
                new Vector2(0.42f, 1f),
                Vector2.zero,
                Vector2.zero);
            Text targetResourceValueLabel = CreateText(
                "Value",
                targetResourceRoot,
                font,
                "100 / 100",
                9,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                TextPrimary,
                new Vector2(0.58f, 0f),
                new Vector2(0.97f, 1f),
                Vector2.zero,
                Vector2.zero);
            targetResourceRoot.gameObject.SetActive(false);
            Text targetStatusLabel = CreateText(
                "Status",
                targetRoot,
                font,
                "READY",
                11,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                TextSecondary,
                new Vector2(0.06f, 0.06f),
                new Vector2(0.30f, 0.28f),
                Vector2.zero,
                Vector2.zero);
            RectTransform targetCastRoot = CreatePanel(
                "TargetCast",
                targetRoot,
                new Vector2(0.30f, 0.06f),
                new Vector2(0.94f, 0.29f),
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color32(5, 9, 16, 230),
                panelSprite,
                false);
            Image targetCastSurface = targetCastRoot.GetComponent<Image>();
            Image targetCastFill = CreateFill(
                targetCastRoot,
                "Fill",
                Mage,
                Image.FillMethod.Horizontal);
            targetCastFill.fillAmount = 0f;
            Text targetCastLabel = CreateText(
                "Label",
                targetCastRoot,
                font,
                "CASTING",
                11,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                TextPrimary,
                new Vector2(0.04f, 0f),
                new Vector2(0.63f, 1f),
                Vector2.zero,
                Vector2.zero);
            targetCastLabel.resizeTextForBestFit = true;
            targetCastLabel.resizeTextMinSize = 8;
            targetCastLabel.resizeTextMaxSize = 11;
            Text targetCastStateLabel = CreateText(
                "State",
                targetCastRoot,
                font,
                "CAST  •  INTERRUPTIBLE",
                8,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                Ember,
                new Vector2(0.61f, 0f),
                new Vector2(0.97f, 1f),
                Vector2.zero,
                Vector2.zero);
            targetCastStateLabel.resizeTextForBestFit = true;
            targetCastStateLabel.resizeTextMinSize = 6;
            targetCastStateLabel.resizeTextMaxSize = 8;
            targetCastRoot.gameObject.SetActive(false);

            GameObject[] targetAuraRoots = new GameObject[PrototypeHud.TargetAuraSlotCount];
            Graphic[] targetAuraSurfaces = new Graphic[PrototypeHud.TargetAuraSlotCount];
            Text[] targetAuraLabels = new Text[PrototypeHud.TargetAuraSlotCount];
            const float auraWidth = 0.065f;
            const float auraSpacing = 0.008f;
            for (int slot = 0; slot < PrototypeHud.TargetAuraSlotCount; slot++)
            {
                float minimumX = 0.68f + (slot * (auraWidth + auraSpacing));
                RectTransform auraRoot = CreatePanel(
                    "Aura_" + (slot + 1),
                    targetRoot,
                    new Vector2(minimumX, 0.72f),
                    new Vector2(minimumX + auraWidth, 0.96f),
                    Center,
                    Vector2.zero,
                    Vector2.zero,
                    new Color(0.2f, 0.68f, 0.82f, 0.96f),
                    panelSprite,
                    false);
                Text auraLabel = CreateText(
                    "Label",
                    auraRoot,
                    font,
                    "AURA  8s",
                    8,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    TextPrimary,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(4f, 1f),
                    new Vector2(-4f, -1f));
                auraLabel.resizeTextForBestFit = true;
                auraLabel.resizeTextMinSize = 6;
                auraLabel.resizeTextMaxSize = 8;
                targetAuraRoots[slot] = auraRoot.gameObject;
                targetAuraSurfaces[slot] = auraRoot.GetComponent<Image>();
                targetAuraLabels[slot] = auraLabel;
                auraRoot.gameObject.SetActive(false);
            }

            RectTransform combatNoticeRoot = CreatePanel(
                "CombatNotice",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -220f),
                new Vector2(360f, 38f),
                new Color(0.035f, 0.055f, 0.08f, 0.92f),
                panelSprite,
                false);
            CanvasGroup combatNoticeCanvasGroup = combatNoticeRoot.gameObject.AddComponent<CanvasGroup>();
            combatNoticeCanvasGroup.alpha = 0f;
            combatNoticeCanvasGroup.interactable = false;
            combatNoticeCanvasGroup.blocksRaycasts = false;
            Text combatNoticeLabel = CreateText(
                "Label",
                combatNoticeRoot,
                font,
                "OUT OF RANGE",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Danger,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 3f),
                new Vector2(-12f, -3f));
            combatNoticeLabel.resizeTextForBestFit = true;
            combatNoticeLabel.resizeTextMinSize = 10;
            combatNoticeLabel.resizeTextMaxSize = 14;

            RectTransform castRoot = CreatePanel(
                "Cast",
                root,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 88f),
                new Vector2(360f, 42f),
                Surface,
                panelSprite,
                false);
            Image castFill = CreateFill(castRoot, "CastFill", Mage, Image.FillMethod.Horizontal);
            castFill.fillAmount = 0f;
            Text castLabel = CreateText(
                "CastLabel",
                castRoot,
                font,
                "CASTING",
                16,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 4f),
                new Vector2(-12f, -4f));

            RectTransform playerResourceRoot = CreatePanel(
                "PlayerResource",
                root,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -112f),
                new Vector2(360f, 20f),
                new Color(0.012f, 0.018f, 0.029f, 0.96f),
                panelSprite,
                false);
            AddOutline(
                playerResourceRoot.gameObject,
                new Color(0.18f, 0.28f, 0.42f, 0.82f),
                new Vector2(1f, -1f));
            Image playerResourceFill = CreateFill(
                playerResourceRoot,
                "Fill",
                Mage,
                Image.FillMethod.Horizontal);
            playerResourceFill.rectTransform.sizeDelta = new Vector2(-6f, -6f);
            playerResourceFill.fillAmount = 1f;
            Text playerResourceTypeLabel = CreateText(
                "Type",
                playerResourceRoot,
                font,
                "MANA",
                10,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Mage,
                new Vector2(0.04f, 0f),
                new Vector2(0.44f, 1f),
                Vector2.zero,
                Vector2.zero);
            Text playerResourceValueLabel = CreateText(
                "Value",
                playerResourceRoot,
                font,
                "100 / 100",
                10,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                TextPrimary,
                new Vector2(0.56f, 0f),
                new Vector2(0.96f, 1f),
                Vector2.zero,
                Vector2.zero);
            playerResourceRoot.gameObject.SetActive(false);

            RectTransform comboPointRoot = CreatePanel(
                "PlayerComboPoints",
                root,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -138f),
                new Vector2(220f, 18f),
                new Color(0.035f, 0.05f, 0.075f, 0.96f),
                panelSprite,
                false);
            Text comboPointLabel = CreateText(
                "Label",
                comboPointRoot,
                font,
                "COMBO",
                9,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                TextSecondary,
                new Vector2(0.04f, 0f),
                new Vector2(0.42f, 1f),
                Vector2.zero,
                Vector2.zero);
            Graphic[] comboPointPips = new Graphic[5];
            for (int index = 0; index < comboPointPips.Length; index++)
            {
                RectTransform pip = CreatePanel(
                    "Pip_" + index,
                    comboPointRoot,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    Center,
                    new Vector2(105f + (index * 22f), 0f),
                    new Vector2(11f, 11f),
                    new Color(0.16f, 0.2f, 0.27f, 0.9f),
                    roundSprite,
                    false);
                pip.localRotation = Quaternion.Euler(0f, 0f, 45f);
                comboPointPips[index] = pip.GetComponent<Image>();
            }

            comboPointRoot.gameObject.SetActive(false);

            AbilityButton[] abilityButtons = new AbilityButton[AbilityCatalog.AbilitySlotCount];
            AbilitySlotView[] abilitySlots = new AbilitySlotView[AbilityCatalog.AbilitySlotCount];
            Vector2[] positions =
            {
                // The two five-action gesture families form the large lower
                // anchors of one right-thumb combat cluster. Their aligned
                // bases clear the centered utilities on narrower screens and
                // retain room for a full outward flick, including at 720p.
                new Vector2(-240f, 292f),
                new Vector2(-564f, 292f),

                // Four enlarged direct actions hug the gesture pair in one
                // arc. These keep their existing size; their new anchors leave
                // the larger gesture faces, transient chrome and shadows clear.
                new Vector2(-182f, 526f),
                new Vector2(-326f, 535f),
                new Vector2(-474f, 537f),
                new Vector2(-676f, 495f),

                // Utilities use their own bottom-center anchor, like a mobile
                // MOBA recall/regen strip, so they never dilute the combat arc.
                new Vector2(-156f, 74f),
                new Vector2(-52f, 74f),
                new Vector2(52f, 74f),
                new Vector2(156f, 74f),
            };
            Color[] accents =
            {
                TextPrimary,
                Cyan,
                Healer,
                Mage,
                Ember,
                Rogue,
                new Color32(159, 111, 229, 255),
                TextSecondary,
                new Color32(239, 207, 94, 255),
                new Color32(137, 204, 255, 255),
            };

            if (abilityButtons.Length > positions.Length)
            {
                throw new InvalidOperationException(
                    "The authored HUD supports at most " + positions.Length + " ability slots.");
            }

            for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
            {
                BuildAbilitySlot(
                    root,
                    font,
                    panelSprite,
                    roundSprite,
                    medallionFrameSprite,
                    slot,
                    positions[slot],
                    accents[slot],
                    out abilityButtons[slot],
                    out abilitySlots[slot]);
            }

            RectTransform abilityGestureOverlay = CreateRect(
                "AbilityGestureOverlay",
                root,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero);
            CanvasGroup abilityGestureOverlayCanvasGroup =
                abilityGestureOverlay.gameObject.AddComponent<CanvasGroup>();
            abilityGestureOverlayCanvasGroup.interactable = false;
            abilityGestureOverlayCanvasGroup.blocksRaycasts = false;

            SiegeSupplyReferences siegeSupply = BuildSiegeSupplyPanel(
                root,
                font,
                panelSprite);

            targetRoot.gameObject.SetActive(false);
            offscreenThreatPresenter.transform.SetAsLastSibling();
            BuildPauseSettings(root, font, panelSprite);

            return new GameplayReferences(
                root.gameObject,
                worldSurface,
                joystick,
                cameraOrbitHintCanvasGroup,
                abilityButtons,
                abilitySlots,
                friendlyFighterRoot,
                hostileFighterRoot,
                fighterViews,
                healthLabel,
                healthFill,
                teamLabel,
                objectiveLabel,
                progressionRoot.gameObject,
                levelLabel,
                experienceFill,
                experienceLabel,
                targetRoot.gameObject,
                targetSurface,
                targetRelationAccent.GetComponent<Image>(),
                targetParticipantLabel,
                targetRelationLabel,
                targetLabel,
                targetHealthFill,
                targetBarrierFill,
                targetHealthLabel,
                targetBarrierLabel,
                targetResourceRoot.gameObject,
                targetResourceFill,
                targetResourceTypeLabel,
                targetResourceValueLabel,
                targetStatusLabel,
                targetCastRoot.gameObject,
                targetCastSurface,
                targetCastLabel,
                targetCastStateLabel,
                targetCastFill,
                targetAuraRoots,
                targetAuraSurfaces,
                targetAuraLabels,
                combatNoticeCanvasGroup,
                combatNoticeLabel,
                castRoot.gameObject,
                castLabel,
                castFill,
                playerResourceRoot.gameObject,
                playerResourceFill,
                playerResourceTypeLabel,
                playerResourceValueLabel,
                comboPointRoot.gameObject,
                comboPointPips,
                comboPointLabel,
                siegeSupply,
                offscreenThreatPresenter);
        }

        private static SiegeSupplyReferences BuildSiegeSupplyPanel(
            RectTransform parent,
            Font font,
            Sprite panelSprite)
        {
            RectTransform root = CreatePanel(
                "SiegeSupplies",
                parent,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-430f, 24f),
                new Vector2(366f, 72f),
                new Color32(7, 11, 18, 248),
                panelSprite,
                true);
            AddOutline(
                root.gameObject,
                new Color32(153, 111, 55, 235),
                new Vector2(2f, -2f));
            AddShadow(
                root.gameObject,
                new Color(0f, 0f, 0f, 0.72f),
                new Vector2(4f, -5f));
            CreatePanel(
                "Accent",
                root,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(3f, 0f),
                new Vector2(5f, -10f),
                TextWarm,
                null,
                false);
            Text goldLabel = CreateText(
                "Gold",
                root,
                font,
                "G 50",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Rogue,
                new Vector2(0.02f, 0.08f),
                new Vector2(0.24f, 0.92f),
                Vector2.zero,
                Vector2.zero);

            Button useHealthButton = CreateCompactButton(
                "UseHealthPotion",
                root,
                font,
                "HP ×0",
                Center,
                new Vector2(-48f, 0f),
                new Vector2(78f, 52f),
                new Color32(45, 104, 74, 250),
                panelSprite,
                13);
            Text healthPotionLabel = useHealthButton.transform
                .Find("Label")
                .GetComponent<Text>();
            Button useManaButton = CreateCompactButton(
                "UseManaPotion",
                root,
                font,
                "MP ×0",
                Center,
                new Vector2(42f, 0f),
                new Vector2(78f, 52f),
                new Color32(46, 87, 137, 250),
                panelSprite,
                13);
            Text manaPotionLabel = useManaButton.transform
                .Find("Label")
                .GetComponent<Text>();
            Button shopButton = CreateCompactButton(
                "OpenSupplyShop",
                root,
                font,
                "SHOP",
                Center,
                new Vector2(132f, 0f),
                new Vector2(72f, 52f),
                new Color32(91, 72, 45, 250),
                panelSprite,
                11);
            Text shopLabel = shopButton.transform.Find("Label").GetComponent<Text>();

            RectTransform purchaseDrawer = CreatePanel(
                "PurchaseDrawer",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 10f),
                new Vector2(372f, 132f),
                new Color32(7, 11, 18, 250),
                panelSprite,
                true);
            AddOutline(
                purchaseDrawer.gameObject,
                new Color32(153, 111, 55, 220),
                new Vector2(2f, -2f));
            CreateText(
                "Heading",
                purchaseDrawer,
                font,
                "RESTOCK BETWEEN PUSHES",
                13,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextWarm,
                new Vector2(0.04f, 0.60f),
                new Vector2(0.96f, 0.92f),
                Vector2.zero,
                Vector2.zero);
            Button buyHealthButton = CreateCompactButton(
                "BuyHealthPotion",
                purchaseDrawer,
                font,
                "BUY HP  •  45G",
                Center,
                new Vector2(-88f, -24f),
                new Vector2(166f, 56f),
                new Color32(42, 83, 63, 250),
                panelSprite,
                13);
            Button buyManaButton = CreateCompactButton(
                "BuyManaPotion",
                purchaseDrawer,
                font,
                "BUY MANA  •  35G",
                Center,
                new Vector2(88f, -24f),
                new Vector2(166f, 56f),
                new Color32(40, 68, 106, 250),
                panelSprite,
                13);

            Text buyHealthLabel = buyHealthButton.transform.Find("Label").GetComponent<Text>();
            Text buyManaLabel = buyManaButton.transform.Find("Label").GetComponent<Text>();
            purchaseDrawer.gameObject.SetActive(false);
            root.gameObject.SetActive(false);

            return new SiegeSupplyReferences(
                root.gameObject,
                purchaseDrawer.gameObject,
                goldLabel,
                healthPotionLabel,
                manaPotionLabel,
                shopButton,
                shopLabel,
                buyHealthButton,
                useHealthButton,
                buyManaButton,
                useManaButton,
                buyHealthLabel,
                buyManaLabel);
        }

        private static ResultsReferences BuildResults(RectTransform parent, Font font, Sprite panelSprite)
        {
            RectTransform root = CreatePanel(
                "Results",
                parent,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color(0.025f, 0.04f, 0.065f, 0.9f),
                null,
                true);
            RectTransform card = CreatePanel(
                "ResultCard",
                root,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(820f, 520f),
                Surface,
                panelSprite,
                true);
            AddOutline(card.gameObject, new Color(0.95f, 0.48f, 0.18f, 0.42f), new Vector2(3f, -3f));

            Text title = CreateText(
                "ResultTitle",
                card,
                font,
                "SESSION COMPLETE",
                55,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                new Vector2(0.08f, 0.67f),
                new Vector2(0.92f, 0.92f),
                Vector2.zero,
                Vector2.zero);
            Text recap = CreateText(
                "ResultRecap",
                card,
                font,
                "Positioning created the final opening.",
                24,
                FontStyle.Normal,
                TextAnchor.UpperCenter,
                TextSecondary,
                new Vector2(0.1f, 0.39f),
                new Vector2(0.9f, 0.68f),
                Vector2.zero,
                Vector2.zero);
            Button replay = CreateButton(
                "Replay",
                card,
                font,
                "PLAY AGAIN",
                new Vector2(0.12f, 0.1f),
                new Vector2(0.54f, 0.3f),
                Ember,
                panelSprite,
                27,
                out GameObject unusedReplayMarker);
            unusedReplayMarker.SetActive(false);
            Text replayLabel = replay.transform.Find("Label").GetComponent<Text>();
            replayLabel.resizeTextForBestFit = true;
            replayLabel.resizeTextMinSize = 17;
            replayLabel.resizeTextMaxSize = 27;
            Button leave = CreateButton(
                "Leave",
                card,
                font,
                "CHANGE SETUP",
                new Vector2(0.58f, 0.1f),
                new Vector2(0.88f, 0.3f),
                SurfaceRaised,
                panelSprite,
                23,
                out GameObject unusedLeaveMarker);
            unusedLeaveMarker.SetActive(false);

            return new ResultsReferences(root.gameObject, title, recap, replay, leave);
        }

        private static void BuildKitViewer(
            RectTransform homeRoot,
            RectTransform selectionCard,
            Font font,
            Sprite panelSprite)
        {
            Button open = CreateButton(
                "KitViewerOpen",
                selectionCard,
                font,
                "VIEW FULL ARENA KIT",
                new Vector2(0.065f, 0.035f),
                new Vector2(0.30f, 0.15f),
                Cyan,
                panelSprite,
                20,
                out GameObject openMarker);
            openMarker.SetActive(false);

            RectTransform overlay = CreatePanel(
                "KitViewerOverlay",
                homeRoot,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color(0.008f, 0.012f, 0.02f, 0.985f),
                null,
                true);
            RectTransform card = CreatePanel(
                "KitViewerCard",
                overlay,
                new Vector2(0.08f, 0.06f),
                new Vector2(0.92f, 0.94f),
                Center,
                Vector2.zero,
                Vector2.zero,
                Surface,
                panelSprite,
                true);
            AddOutline(card.gameObject, new Color(0.25f, 0.7f, 0.82f, 0.55f), new Vector2(2f, -2f));
            Text title = CreateText(
                "Title",
                card,
                font,
                "ARENA KIT",
                30,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                Cyan,
                new Vector2(0.04f, 0.89f),
                new Vector2(0.96f, 0.97f),
                Vector2.zero,
                Vector2.zero);
            Text body = CreateText(
                "Guide",
                card,
                font,
                string.Empty,
                15,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                TextPrimary,
                new Vector2(0.04f, 0.13f),
                new Vector2(0.96f, 0.88f),
                Vector2.zero,
                Vector2.zero);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.08f;
            Button close = CreateButton(
                "Close",
                card,
                font,
                "CLOSE",
                new Vector2(0.76f, 0.025f),
                new Vector2(0.96f, 0.105f),
                Ember,
                panelSprite,
                20,
                out GameObject closeMarker);
            closeMarker.SetActive(false);

            KitViewerPanel viewer = homeRoot.gameObject.AddComponent<KitViewerPanel>();
            viewer.ConfigureForAuthoring(
                overlay.gameObject,
                open,
                close,
                title,
                body);
            overlay.gameObject.SetActive(false);
        }

        private static void BuildPauseSettings(
            RectTransform gameplayRoot,
            Font font,
            Sprite panelSprite)
        {
            Button open = CreateButton(
                "Pause",
                gameplayRoot,
                font,
                "Ⅱ",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                TextSecondary,
                panelSprite,
                26,
                out GameObject openMarker);
            openMarker.SetActive(false);
            RectTransform openRect = (RectTransform)open.transform;
            openRect.pivot = new Vector2(0.5f, 1f);
            // At narrow development aspect ratios the compact team roster moves
            // inward, so keep Pause on its own row instead of over a portrait.
            openRect.anchoredPosition = new Vector2(376f, -112f);
            openRect.sizeDelta = new Vector2(64f, 64f);

            RectTransform overlay = CreatePanel(
                "PauseSettingsOverlay",
                gameplayRoot,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color(0.006f, 0.01f, 0.018f, 0.965f),
                null,
                true);
            RectTransform card = CreatePanel(
                "PauseSettingsCard",
                overlay,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(840f, 790f),
                Surface,
                panelSprite,
                true);
            AddOutline(card.gameObject, new Color(0.38f, 0.58f, 0.68f, 0.55f), new Vector2(2f, -2f));
            CreateText(
                "Title",
                card,
                font,
                "PAUSED  •  LOCAL SESSION",
                31,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                new Vector2(0.06f, 0.86f),
                new Vector2(0.94f, 0.96f),
                Vector2.zero,
                Vector2.zero);

            Button masterDown = CreateButton(
                "MasterDown", card, font, "−", new Vector2(0.08f, 0.72f),
                new Vector2(0.20f, 0.81f), Cyan, panelSprite, 27,
                out GameObject masterDownMarker);
            masterDownMarker.SetActive(false);
            Text masterLabel = CreateText(
                "MasterLabel", card, font, "MASTER  100%", 21,
                FontStyle.Bold, TextAnchor.MiddleCenter, TextPrimary,
                new Vector2(0.22f, 0.72f), new Vector2(0.78f, 0.81f),
                Vector2.zero, Vector2.zero);
            Button masterUp = CreateButton(
                "MasterUp", card, font, "+", new Vector2(0.80f, 0.72f),
                new Vector2(0.92f, 0.81f), Cyan, panelSprite, 27,
                out GameObject masterUpMarker);
            masterUpMarker.SetActive(false);

            Button effectsDown = CreateButton(
                "EffectsDown", card, font, "−", new Vector2(0.08f, 0.60f),
                new Vector2(0.20f, 0.69f), Ember, panelSprite, 27,
                out GameObject effectsDownMarker);
            effectsDownMarker.SetActive(false);
            Text effectsLabel = CreateText(
                "EffectsLabel", card, font, "EFFECTS  100%", 21,
                FontStyle.Bold, TextAnchor.MiddleCenter, TextPrimary,
                new Vector2(0.22f, 0.60f), new Vector2(0.78f, 0.69f),
                Vector2.zero, Vector2.zero);
            Button effectsUp = CreateButton(
                "EffectsUp", card, font, "+", new Vector2(0.80f, 0.60f),
                new Vector2(0.92f, 0.69f), Ember, panelSprite, 27,
                out GameObject effectsUpMarker);
            effectsUpMarker.SetActive(false);

            Button haptics = SettingsToggle(
                "Haptics", "HAPTICS  ON", card, font, 0.48f, panelSprite,
                out Text hapticsLabel);
            Button reducedMotion = SettingsToggle(
                "ReducedMotion", "REDUCED MOTION  OFF", card, font, 0.37f,
                panelSprite, out Text reducedMotionLabel);
            Button highContrast = SettingsToggle(
                "HighContrast", "HIGH CONTRAST  OFF", card, font, 0.26f,
                panelSprite, out Text highContrastLabel);
            Button resetTutorial = CreateButton(
                "ResetTraining", card, font, "RESET TRAINING FOR THIS SPECIALIZATION",
                new Vector2(0.15f, 0.14f), new Vector2(0.85f, 0.22f),
                TextWarm, panelSprite, 18, out GameObject resetMarker);
            resetMarker.SetActive(false);
            Button resume = CreateButton(
                "Resume", card, font, "RESUME", new Vector2(0.08f, 0.035f),
                new Vector2(0.47f, 0.115f), Cyan, panelSprite, 22,
                out GameObject resumeMarker);
            resumeMarker.SetActive(false);
            Button leave = CreateButton(
                "Leave", card, font, "LEAVE TO HOME", new Vector2(0.53f, 0.035f),
                new Vector2(0.92f, 0.115f), Danger, panelSprite, 22,
                out GameObject leaveMarker);
            leaveMarker.SetActive(false);

            PauseSettingsPanel pause = gameplayRoot.gameObject
                .AddComponent<PauseSettingsPanel>();
            pause.ConfigureForAuthoring(
                overlay.gameObject,
                open,
                resume,
                leave,
                masterDown,
                masterUp,
                effectsDown,
                effectsUp,
                haptics,
                reducedMotion,
                highContrast,
                resetTutorial,
                masterLabel,
                effectsLabel,
                hapticsLabel,
                reducedMotionLabel,
                highContrastLabel);
            overlay.gameObject.SetActive(false);
            overlay.SetAsLastSibling();
        }

        private static Button SettingsToggle(
            string name,
            string label,
            RectTransform parent,
            Font font,
            float minimumY,
            Sprite panelSprite,
            out Text text)
        {
            Button button = CreateButton(
                name,
                parent,
                font,
                label,
                new Vector2(0.15f, minimumY),
                new Vector2(0.85f, minimumY + 0.085f),
                TextSecondary,
                panelSprite,
                19,
                out GameObject marker);
            marker.SetActive(false);
            text = button.transform.Find("Label").GetComponent<Text>();
            return button;
        }

        private static RectTransform BuildFighterColumn(
            string name,
            RectTransform parent,
            Vector2 anchorMinimum,
            Vector2 anchorMaximum,
            Vector2 pivot,
            Vector2 anchoredPosition)
        {
            RectTransform column = CreateRect(
                name,
                parent,
                anchorMinimum,
                anchorMaximum,
                pivot,
                anchoredPosition,
                new Vector2(360f, 84f));
            HorizontalLayoutGroup layout = column.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = pivot.x > 0.5f
                ? TextAnchor.UpperRight
                : TextAnchor.UpperLeft;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);
            return column;
        }

        private static FighterCardReferences BuildFighterCard(
            string name,
            RectTransform parent,
            Font font,
            Sprite roundSprite)
        {
            RectTransform root = CreatePanel(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(78f, 84f),
                new Color(1f, 1f, 1f, 0.002f),
                roundSprite,
                true);
            Image hitSurface = root.GetComponent<Image>();

            LayoutElement layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = 78f;
            layoutElement.preferredWidth = 78f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.minHeight = 84f;
            layoutElement.preferredHeight = 84f;
            layoutElement.flexibleHeight = 0f;

            CanvasGroup canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hitSurface;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock buttonColors = button.colors;
            buttonColors.normalColor = Color.white;
            buttonColors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
            buttonColors.pressedColor = new Color(0.78f, 0.88f, 0.94f, 0.92f);
            buttonColors.selectedColor = Color.white;
            buttonColors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            buttonColors.fadeDuration = 0.06f;
            button.colors = buttonColors;

            RectTransform selectedMarker = CreatePanel(
                "Selected",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(70f, 70f),
                new Color32(255, 196, 67, 255),
                roundSprite,
                false);
            AddOutline(
                selectedMarker.gameObject,
                new Color(1f, 0.86f, 0.38f, 0.9f),
                new Vector2(2f, -2f));
            selectedMarker.gameObject.SetActive(false);

            RectTransform relationAccent = CreatePanel(
                "RelationAccent",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(66f, 66f),
                Cyan,
                roundSprite,
                false);
            Image relationImage = relationAccent.GetComponent<Image>();
            AddOutline(
                relationAccent.gameObject,
                new Color(0f, 0f, 0f, 0.92f),
                new Vector2(3f, -4f));
            button.targetGraphic = relationImage;

            CreatePanel(
                "OuterMetal",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(62f, 62f),
                new Color32(48, 54, 61, 255),
                roundSprite,
                false);

            CreatePanel(
                "MetalHighlight",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(58f, 58f),
                new Color32(126, 111, 78, 255),
                roundSprite,
                false);

            CreatePanel(
                "HealthBackground",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(56f, 56f),
                new Color32(5, 9, 16, 255),
                roundSprite,
                false);

            RectTransform barrierRect = CreatePanel(
                "BarrierFill",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(54f, 54f),
                new Color32(84, 196, 255, 255),
                roundSprite,
                false);
            Image barrierFill = barrierRect.GetComponent<Image>();
            ConfigureRadialFill(barrierFill, 0f);
            barrierRect.gameObject.SetActive(false);

            RectTransform healthRect = CreatePanel(
                "HealthFill",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(52f, 52f),
                Healer,
                roundSprite,
                false);
            Image healthFill = healthRect.GetComponent<Image>();
            ConfigureRadialFill(healthFill, 1f);

            RectTransform innerSurface = CreatePanel(
                "InnerSurface",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(36f, 36f),
                new Color32(12, 19, 31, 255),
                roundSprite,
                false);
            Image surface = innerSurface.GetComponent<Image>();

            Text relationLabel = CreateText(
                "Relation",
                root,
                font,
                "ALLY",
                10,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Cyan,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 2f),
                new Vector2(0f, 18f));
            relationLabel.gameObject.SetActive(false);
            Text participantLabel = CreateText(
                "ParticipantName",
                root,
                font,
                "COMBATANT",
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 20f),
                new Vector2(0f, 40f));
            participantLabel.resizeTextForBestFit = true;
            participantLabel.resizeTextMinSize = 8;
            participantLabel.resizeTextMaxSize = 11;
            participantLabel.gameObject.SetActive(false);
            Text healthLabel = CreateText(
                "Health",
                root,
                font,
                "100%",
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-28f, -44f),
                new Vector2(28f, -20f));

            RectTransform classBadge = CreatePanel(
                "ClassBadge",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(21f, -54f),
                new Vector2(22f, 22f),
                Mage,
                roundSprite,
                false);
            AddOutline(classBadge.gameObject, new Color32(8, 14, 24, 245), new Vector2(2f, -2f));
            Text identityLabel = CreateText(
                "BadgeLabel",
                classBadge,
                font,
                "M",
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Ink,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            RectTransform deadOverlay = CreatePanel(
                "DeadOverlay",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Center,
                new Vector2(0f, -32f),
                new Vector2(62f, 62f),
                new Color(0.025f, 0.035f, 0.055f, 0.82f),
                roundSprite,
                false);
            CreateText(
                "Label",
                deadOverlay,
                font,
                "×",
                34,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color32(232, 119, 107, 255),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            deadOverlay.gameObject.SetActive(false);

            CombatantStatusView view = root.gameObject.AddComponent<CombatantStatusView>();
            HudSerialized.Apply(
                view,
                serialized =>
                {
                    HudSerialized.SetObject(serialized, "_button", button);
                    HudSerialized.SetObject(serialized, "_surface", surface);
                    HudSerialized.SetObject(serialized, "_relationAccent", relationAccent.GetComponent<Image>());
                    HudSerialized.SetObject(serialized, "_canvasGroup", canvasGroup);
                    HudSerialized.SetObject(serialized, "_participantLabel", participantLabel);
                    HudSerialized.SetObject(serialized, "_relationLabel", relationLabel);
                    HudSerialized.SetObject(serialized, "_identityLabel", identityLabel);
                    HudSerialized.SetObject(serialized, "_classBadge", classBadge.GetComponent<Image>());
                    HudSerialized.SetObject(serialized, "_healthFill", healthFill);
                    HudSerialized.SetObject(serialized, "_barrierFill", barrierFill);
                    HudSerialized.SetObject(serialized, "_healthLabel", healthLabel);
                    HudSerialized.SetObject(serialized, "_selectedMarker", selectedMarker.gameObject);
                    HudSerialized.SetObject(serialized, "_deadOverlay", deadOverlay.gameObject);
                });
            return new FighterCardReferences(view, healthLabel, healthFill);
        }

        private static OffscreenThreatPresenter BuildOffscreenThreatPresenter(
            RectTransform parent,
            Font font,
            Sprite panelSprite,
            Sprite roundSprite)
        {
            RectTransform root = CreateRect(
                "OffscreenThreats",
                parent,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero);
            OffscreenThreatPresenter presenter =
                root.gameObject.AddComponent<OffscreenThreatPresenter>();
            var indicators = new OffscreenThreatIndicatorView[
                OffscreenThreatPresenter.MaximumIndicatorCount];
            for (int index = 0; index < indicators.Length; index++)
            {
                RectTransform card = CreatePanel(
                    "Threat_" + (index + 1),
                    root,
                    Center,
                    Center,
                    Center,
                    Vector2.zero,
                    new Vector2(138f, 50f),
                    new Color32(18, 28, 45, 244),
                    panelSprite,
                    false);
                AddOutline(
                    card.gameObject,
                    new Color32(224, 76, 68, 205),
                    new Vector2(1f, -1f));

                Text directionArrowLabel = CreateText(
                    "Direction",
                    card,
                    font,
                    "▲",
                    19,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Danger,
                    Center,
                    Center,
                    new Vector2(-68f, -11f),
                    new Vector2(-46f, 11f));
                RectTransform classBadge = CreatePanel(
                    "ClassBadge",
                    card,
                    Center,
                    Center,
                    Center,
                    new Vector2(-30f, 0f),
                    new Vector2(32f, 32f),
                    Mage,
                    roundSprite,
                    false);
                AddOutline(
                    classBadge.gameObject,
                    new Color32(8, 14, 24, 245),
                    new Vector2(1f, -1f));
                Text classBadgeLabel = CreateText(
                    "Label",
                    classBadge,
                    font,
                    "M",
                    15,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Ink,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                Text healthLabel = CreateText(
                    "Health",
                    card,
                    font,
                    "100%",
                    17,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    TextPrimary,
                    Center,
                    Center,
                    new Vector2(-8f, 1f),
                    new Vector2(64f, 23f));
                Text castLabel = CreateText(
                    "Cast",
                    card,
                    font,
                    "CAST",
                    11,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    Ember,
                    Center,
                    Center,
                    new Vector2(-8f, -20f),
                    new Vector2(64f, -2f));
                castLabel.gameObject.SetActive(false);

                OffscreenThreatIndicatorView view =
                    card.gameObject.AddComponent<OffscreenThreatIndicatorView>();
                HudSerialized.Apply(
                    view,
                    serialized =>
                    {
                        HudSerialized.SetObject(serialized, "_root", card);
                        HudSerialized.SetObject(
                            serialized,
                            "_directionArrow",
                            directionArrowLabel.rectTransform);
                        HudSerialized.SetObject(
                            serialized,
                            "_badgeSurface",
                            classBadge.GetComponent<Image>());
                        HudSerialized.SetObject(
                            serialized,
                            "_classBadgeLabel",
                            classBadgeLabel);
                        HudSerialized.SetObject(serialized, "_healthLabel", healthLabel);
                        HudSerialized.SetObject(serialized, "_castRoot", castLabel.gameObject);
                    });
                indicators[index] = view;
                card.gameObject.SetActive(false);
            }

            HudSerialized.Apply(
                presenter,
                serialized =>
                {
                    HudSerialized.SetObject(serialized, "_boundsRoot", root);
                    HudSerialized.SetObjects(serialized, "_indicators", indicators);
                });
            return presenter;
        }

        private static void ConfigureRadialFill(Image fill, float amount)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = Mathf.Clamp01(amount);
            fill.raycastTarget = false;
        }

        private static FloatingJoystick BuildJoystick(
            RectTransform parent,
            Sprite panelSprite,
            Sprite roundSprite)
        {
            RectTransform zone = CreatePanel(
                "MovementZone",
                parent,
                new Vector2(0f, 0f),
                new Vector2(0.34f, 0.55f),
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color(0f, 0f, 0f, 0f),
                null,
                true);
            FloatingJoystick joystick = zone.gameObject.AddComponent<FloatingJoystick>();

            RectTransform joystickBase = CreatePanel(
                "JoystickBase",
                zone,
                Center,
                Center,
                Center,
                new Vector2(-220f, -125f),
                new Vector2(180f, 180f),
                new Color(0.055f, 0.09f, 0.13f, 0.60f),
                roundSprite,
                false);
            AddOutline(joystickBase.gameObject, new Color(0.25f, 0.7f, 0.78f, 0.28f), new Vector2(2f, -2f));
            CreatePanel(
                "InnerRing",
                joystickBase,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(100f, 100f),
                new Color(0.16f, 0.27f, 0.34f, 0.42f),
                roundSprite,
                false);
            RectTransform handle = CreatePanel(
                "Handle",
                joystickBase,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(72f, 72f),
                new Color(0.35f, 0.84f, 0.9f, 0.78f),
                roundSprite,
                false);

            HudSerialized.Apply(
                joystick,
                serialized =>
                {
                    HudSerialized.SetObject(serialized, "_baseRect", joystickBase);
                    HudSerialized.SetObject(serialized, "_handleRect", handle);
                    HudSerialized.SetBoolean(serialized, "_moveBaseToTouch", true);
                    HudSerialized.SetFloat(serialized, "_radius", 70f);
                    HudSerialized.SetFloat(serialized, "_deadZone", 0.08f);
                });
            return joystick;
        }

        private static void BuildAbilitySlot(
            RectTransform parent,
            Font font,
            Sprite panelSprite,
            Sprite roundSprite,
            Sprite medallionFrameSprite,
            int slot,
            Vector2 anchoredPosition,
            Color accent,
            out AbilityButton abilityButton,
            out AbilitySlotView slotView)
        {
            bool isGroupedAction = slot < AbilityKit.GestureSlotCount;
            bool isDirectAction = slot >= AbilityKit.GestureSlotCount &&
                slot < AbilityKit.GestureSlotCount + AbilityKit.CommonSlotCount;
            float size = isGroupedAction
                ? 232f
                : isDirectAction
                    ? 132f
                    : 92f;
            float visualSize = isGroupedAction ? 84f : size;
            float primaryIconSize = isGroupedAction
                ? 48f
                : isDirectAction
                    ? 82f
                    : 56f;
            Vector2 anchor = isGroupedAction || isDirectAction
                ? new Vector2(1f, 0f)
                : new Vector2(0.5f, 0f);
            RectTransform root = CreateRect(
                "Ability_" + (slot + 1),
                parent,
                anchor,
                anchor,
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                new Vector2(size, size));
            // Scale the complete gesture family once, including painted icons,
            // captions and its circular hit surface. Its local sector geometry
            // stays stable across resting/held states and runtime refreshes.
            // Flick recognition remains in screen pixels, so larger art does
            // not make a quick swipe require 30% more finger travel.
            if (isGroupedAction)
            {
                root.localScale = new Vector3(LargeGestureControlScale, LargeGestureControlScale, 1f);
            }
            CanvasGroup canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            abilityButton = root.gameObject.AddComponent<AbilityButton>();
            slotView = root.gameObject.AddComponent<AbilitySlotView>();

            RectTransform visualRoot = root;
            if (isGroupedAction)
            {
                // One compact circular touch surface owns all five actions:
                // tap resolves the center and a cardinal flick resolves a wedge.
                RectTransform gestureTouchSurface = CreatePanel(
                    "GestureTouchSurface",
                    root,
                    Vector2.zero,
                    Vector2.one,
                    Center,
                    Vector2.zero,
                    Vector2.zero,
                    new Color(1f, 1f, 1f, 0.001f),
                    roundSprite,
                    true);
                gestureTouchSurface.gameObject.AddComponent<CircularRaycastFilter>();
                CreatePanel(
                    "ClusterShadow",
                    root,
                    Vector2.zero,
                    Vector2.one,
                    Center,
                    new Vector2(0f, -5f),
                    new Vector2(10f, 10f),
                    new Color(0f, 0f, 0f, 0.64f),
                    roundSprite,
                    false);
                CreatePanel(
                    "ClusterBacking",
                    root,
                    Vector2.zero,
                    Vector2.one,
                    Center,
                    Vector2.zero,
                    new Vector2(-8f, -8f),
                    new Color32(8, 10, 16, 252),
                    roundSprite,
                    false);
                visualRoot = CreateRect(
                    "PrimaryMedallion",
                    root,
                    Center,
                    Center,
                    Center,
                    Vector2.zero,
                    new Vector2(visualSize, visualSize));
            }

            CreatePanel(
                "MedallionShadow",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Center,
                new Vector2(0f, -5f),
                new Vector2(10f, 10f),
                new Color(0f, 0f, 0f, 0.66f),
                roundSprite,
                false);

            RectTransform rootSurfaceRect = CreatePanel(
                "RootSurface",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color32(12, 10, 15, 250),
                roundSprite,
                true);
            rootSurfaceRect.gameObject.AddComponent<CircularRaycastFilter>();

            CreateAbilityRing(
                "RootOutline",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color32(9, 7, 10, 250),
                7f);

            RectTransform readyPulse = CreateAbilityRing(
                "ReadyPulse",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(12f, 12f),
                new Color(accent.r, accent.g, accent.b, 0f),
                6f);
            AbilityRingGraphic readyPulseGraphic =
                readyPulse.GetComponent<AbilityRingGraphic>();

            RectTransform outerBezel = CreateAbilityRing(
                "OuterBezel",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-4f, -4f),
                new Color(0.149f, 0.137f, 0.145f, 0.96f),
                9f);
            AbilityRingGraphic outerFrame =
                outerBezel.GetComponent<AbilityRingGraphic>();
            AddOutline(
                outerBezel.gameObject,
                new Color32(12, 9, 11, 246),
                new Vector2(1f, -1f));

            RectTransform bezelHighlight = CreateAbilityRing(
                "BezelHighlight",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-10f, -10f),
                new Color(0.263f, 0.235f, 0.227f, 0.58f),
                4f);
            AbilityRingGraphic frameHighlight =
                bezelHighlight.GetComponent<AbilityRingGraphic>();

            RectTransform accentRing = CreateAbilityRing(
                "AccentRing",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-17f, -17f),
                new Color(accent.r, accent.g, accent.b, 0.78f),
                5f);
            AbilityRingGraphic rim = accentRing.GetComponent<AbilityRingGraphic>();

            RectTransform innerBezel = CreateAbilityRing(
                "InnerBezel",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-24f, -24f),
                new Color(0.032f, 0.026f, 0.04f, 0.92f),
                3.5f);
            AbilityRingGraphic innerFrame =
                innerBezel.GetComponent<AbilityRingGraphic>();

            RectTransform backplate = CreatePanel(
                "Backplate",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                new Vector2(-30f, -30f),
                Color.clear,
                roundSprite,
                false);
            Image stateSurface = backplate.GetComponent<Image>();

            RectTransform interactionHaloRect = CreateAbilityRing(
                "InteractionHalo",
                visualRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-27f, -27f),
                Color.clear,
                5f);
            AbilityRingGraphic interactionHalo =
                interactionHaloRect.GetComponent<AbilityRingGraphic>();

            RectTransform iconGlowRect = CreatePanel(
                "IconGlow",
                visualRoot,
                Center,
                Center,
                Center,
                new Vector2(0f, isGroupedAction ? 0f : 3f),
                new Vector2(primaryIconSize + 12f, primaryIconSize + 12f),
                Color.clear,
                roundSprite,
                false);
            Image iconGlow = iconGlowRect.GetComponent<Image>();

            CreatePanel(
                "GlassSheen",
                visualRoot,
                Center,
                Center,
                Center,
                new Vector2(-visualSize * 0.09f, visualSize * 0.14f),
                new Vector2(visualSize * 0.48f, visualSize * 0.48f),
                new Color(1f, 0.9f, 0.72f, 0.055f),
                roundSprite,
                false);

            RectTransform iconRect = CreatePanel(
                "Icon",
                visualRoot,
                Center,
                Center,
                Center,
                new Vector2(0f, isGroupedAction ? 0f : 4f),
                new Vector2(primaryIconSize, primaryIconSize),
                Color.clear,
                null,
                false);
            Image icon = iconRect.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.enabled = false;

            RectTransform glyphRect = CreateRect(
                "Glyph",
                visualRoot,
                Center,
                Center,
                Center,
                new Vector2(0f, isGroupedAction ? 0f : 4f),
                new Vector2(primaryIconSize, primaryIconSize));
            AbilityGlyphGraphic glyph = glyphRect.gameObject.AddComponent<AbilityGlyphGraphic>();
            glyph.raycastTarget = false;

            RectTransform cooldownRect = CreatePanel(
                "Cooldown",
                visualRoot,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(visualSize - 26f, visualSize - 26f),
                new Color(0.018f, 0.016f, 0.022f, 0.14f),
                roundSprite,
                false);
            Image cooldownFill = cooldownRect.GetComponent<Image>();
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Radial360;
            cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            cooldownFill.fillClockwise = false;
            cooldownFill.fillAmount = 0f;
            RectTransform globalCooldownRect = CreatePanel(
                "GlobalCooldown",
                visualRoot,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(visualSize - 22f, visualSize - 22f),
                new Color(0.022f, 0.024f, 0.028f, 0.12f),
                roundSprite,
                false);
            Image globalCooldownFill = globalCooldownRect.GetComponent<Image>();
            globalCooldownFill.type = Image.Type.Filled;
            globalCooldownFill.fillMethod = Image.FillMethod.Radial360;
            globalCooldownFill.fillOrigin = (int)Image.Origin360.Top;
            globalCooldownFill.fillClockwise = false;
            globalCooldownFill.fillAmount = 0f;
            globalCooldownFill.enabled = false;

            // The metal sprite is the visual aperture. Keep radial state fills
            // underneath it so cooldowns darken the jewel without painting over
            // the forged rim or leaking beyond the circular control.
            CreateAbilityFrameArt(visualRoot, medallionFrameSprite);

            RectTransform captionPlate = CreatePanel(
                "CaptionPlate",
                visualRoot,
                new Vector2(0.18f, 0.085f),
                new Vector2(0.82f, 0.255f),
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color32(13, 10, 14, 218),
                panelSprite,
                false);
            AddOutline(
                captionPlate.gameObject,
                new Color(0.54f, 0.39f, 0.2f, 0.76f),
                new Vector2(1f, -1f));
            AddShadow(
                captionPlate.gameObject,
                new Color(0f, 0f, 0f, 0.86f),
                new Vector2(1f, -2f));
            captionPlate.gameObject.SetActive(false);

            Text actionLabel = CreateText(
                "Action",
                visualRoot,
                font,
                "ACTION",
                isGroupedAction ? 14 : isDirectAction ? 12 : 10,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextWarm,
                new Vector2(0.08f, 0.03f),
                new Vector2(0.92f, 0.22f),
                Vector2.zero,
                Vector2.zero);
            actionLabel.resizeTextForBestFit = true;
            actionLabel.resizeTextMinSize = 8;
            actionLabel.resizeTextMaxSize = isGroupedAction ? 14 : isDirectAction ? 12 : 10;
            AddShadow(
                actionLabel.gameObject,
                new Color(0f, 0f, 0f, 0.96f),
                new Vector2(1f, -2f));
            AddOutline(
                actionLabel.gameObject,
                new Color(0.08f, 0.045f, 0.025f, 0.94f),
                new Vector2(1f, -1f));
            Text label = CreateText(
                "Label",
                visualRoot,
                font,
                string.Empty,
                8,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                new Vector2(0.1f, 0.34f),
                new Vector2(0.9f, 0.55f),
                Vector2.zero,
                Vector2.zero);
            label.enabled = false;

            RectTransform stateBadgeRoot = CreatePanel(
                "StateBadge",
                visualRoot,
                Center,
                Center,
                Center,
                new Vector2(visualSize * 0.28f, visualSize * 0.27f),
                new Vector2(isGroupedAction ? 50f : isDirectAction ? 54f : 48f, 22f),
                new Color32(19, 15, 20, 247),
                panelSprite,
                false);
            Image stateBadgeSurface = stateBadgeRoot.GetComponent<Image>();
            AddOutline(
                stateBadgeRoot.gameObject,
                new Color(0f, 0f, 0f, 0.74f),
                new Vector2(1f, -1f));

            Text targetBadge = CreateText(
                "Label",
                stateBadgeRoot,
                font,
                "FOE",
                isGroupedAction ? 11 : isDirectAction ? 9 : 8,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(0.82f, 0.9f, 0.96f, 0.9f),
                new Vector2(0.04f, 0.04f),
                new Vector2(0.96f, 0.96f),
                Vector2.zero,
                Vector2.zero);
            targetBadge.resizeTextForBestFit = true;
            targetBadge.resizeTextMinSize = 7;
            targetBadge.resizeTextMaxSize = isGroupedAction ? 11 : isDirectAction ? 9 : 8;
            targetBadge.raycastTarget = false;

            Text cooldownText = CreateText(
                "CooldownSeconds",
                visualRoot,
                font,
                string.Empty,
                isGroupedAction ? 20 : isDirectAction ? 24 : 20,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            AddOutline(
                cooldownText.gameObject,
                new Color(0f, 0f, 0f, 0.98f),
                new Vector2(2f, -2f));
            AddShadow(
                cooldownText.gameObject,
                new Color(0f, 0f, 0f, 0.92f),
                new Vector2(1f, -3f));
            Text globalCooldownLock = CreateText(
                "GlobalCooldownLock",
                visualRoot,
                font,
                "GCD",
                isGroupedAction ? 11 : isDirectAction ? 10 : 9,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Cyan,
                new Vector2(0.31f, 0.39f),
                new Vector2(0.69f, 0.58f),
                Vector2.zero,
                Vector2.zero);
            globalCooldownLock.raycastTarget = false;
            AddOutline(
                globalCooldownLock.gameObject,
                new Color(0f, 0f, 0f, 0.96f),
                new Vector2(1f, -1f));
            globalCooldownLock.gameObject.SetActive(false);

            RectTransform blockedMark = CreateRect(
                "BlockedMark",
                visualRoot,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(visualSize * 0.72f, visualSize * 0.72f));
            RectTransform blockedSlashForward = CreatePanel(
                "SlashForward",
                blockedMark,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(visualSize * 0.66f, 7f),
                new Color32(190, 43, 39, 245),
                panelSprite,
                false);
            blockedSlashForward.localRotation = Quaternion.Euler(0f, 0f, 45f);
            RectTransform blockedSlashBack = CreatePanel(
                "SlashBack",
                blockedMark,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(visualSize * 0.66f, 7f),
                new Color32(190, 43, 39, 245),
                panelSprite,
                false);
            blockedSlashBack.localRotation = Quaternion.Euler(0f, 0f, -45f);
            AddOutline(
                blockedSlashForward.gameObject,
                new Color(0f, 0f, 0f, 0.9f),
                new Vector2(1f, -1f));
            AddOutline(
                blockedSlashBack.gameObject,
                new Color(0f, 0f, 0f, 0.9f),
                new Vector2(1f, -1f));
            blockedMark.gameObject.SetActive(false);

            RectTransform radialGuideRect = CreateRect(
                "DirectionalRadialGuide",
                root,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(
                    isGroupedAction ? 232f : size + 24f,
                    isGroupedAction ? 232f : size + 24f));
            AbilityRadialGuideGraphic radialGuide =
                radialGuideRect.gameObject.AddComponent<AbilityRadialGuideGraphic>();
            radialGuide.raycastTarget = false;
            radialGuideRect.SetSiblingIndex(isGroupedAction ? 3 : 0);

            if (isGroupedAction)
            {
                RectTransform clusterOuterBezel = CreateAbilityRing(
                    "ClusterOuterBezel",
                    root,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(-2f, -2f),
                    new Color32(28, 27, 32, 252),
                    6f);
                clusterOuterBezel.SetSiblingIndex(4);
                RectTransform clusterOuterHighlight = CreateAbilityRing(
                    "ClusterOuterHighlight",
                    root,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(-10f, -10f),
                    new Color(0.42f, 0.48f, 0.52f, 0.64f),
                    2.5f);
                clusterOuterHighlight.SetSiblingIndex(5);
                RectTransform clusterAccentRing = CreateAbilityRing(
                    "ClusterAccentRing",
                    root,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(-18f, -18f),
                    new Color(accent.r, accent.g, accent.b, 0.58f),
                    2.25f);
                clusterAccentRing.SetSiblingIndex(6);

                Color cardinalOrnament = Color.Lerp(
                    new Color32(126, 91, 48, 255),
                    accent,
                    0.22f);
                cardinalOrnament.a = 0.56f;
                CreatePanel(
                    "ClusterCardinalTickUp",
                    root,
                    Center,
                    Center,
                    Center,
                    new Vector2(0f, 105f),
                    new Vector2(16f, 4f),
                    cardinalOrnament,
                    roundSprite,
                    false);
                CreatePanel(
                    "ClusterCardinalTickDown",
                    root,
                    Center,
                    Center,
                    Center,
                    new Vector2(0f, -105f),
                    new Vector2(16f, 4f),
                    cardinalOrnament,
                    roundSprite,
                    false);
                CreatePanel(
                    "ClusterCardinalTickLeft",
                    root,
                    Center,
                    Center,
                    Center,
                    new Vector2(-105f, 0f),
                    new Vector2(4f, 16f),
                    cardinalOrnament,
                    roundSprite,
                    false);
                CreatePanel(
                    "ClusterCardinalTickRight",
                    root,
                    Center,
                    Center,
                    Center,
                    new Vector2(105f, 0f),
                    new Vector2(4f, 16f),
                    cardinalOrnament,
                    roundSprite,
                    false);
            }

            float directionOffset = isGroupedAction ? 78f : (size * 0.5f) + 8f;
            Vector2 directionMarkerSize = isGroupedAction
                ? new Vector2(60f, 60f)
                : new Vector2(64f, 64f);
            RectTransform alternateUpRoot = CreateRect(
                "AlternateUp",
                root,
                Center,
                Center,
                Center,
                new Vector2(0f, directionOffset),
                directionMarkerSize);
            CreateDirectionalTagArt(
                alternateUpRoot,
                out Image alternateUpIcon,
                out AbilityGlyphGraphic alternateUpGlyph);
            Text alternateUpLabel = CreateText(
                "Label",
                alternateUpRoot,
                font,
                "↑ ALT",
                9,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                Center,
                Center,
                new Vector2(-30f, -20f),
                new Vector2(30f, -8f));
            alternateUpLabel.resizeTextForBestFit = true;
            alternateUpLabel.resizeTextMinSize = 7;
            alternateUpLabel.resizeTextMaxSize = 9;
            alternateUpLabel.raycastTarget = false;
            AddOutline(
                alternateUpLabel.gameObject,
                new Color(0f, 0f, 0f, 0.98f),
                new Vector2(1f, -1f));
            AddShadow(
                alternateUpLabel.gameObject,
                new Color(0f, 0f, 0f, 0.94f),
                new Vector2(1f, -2f));
            RectTransform alternateUpCooldownRect = CreatePanel(
                "Cooldown",
                alternateUpRoot,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                new Vector2(-8f, -8f),
                new Color(0.01f, 0.02f, 0.035f, 0.82f),
                roundSprite,
                false);
            Image alternateUpCooldownFill =
                alternateUpCooldownRect.GetComponent<Image>();
            alternateUpCooldownFill.type = Image.Type.Filled;
            alternateUpCooldownFill.fillMethod = Image.FillMethod.Radial360;
            alternateUpCooldownFill.fillOrigin = (int)Image.Origin360.Top;
            alternateUpCooldownFill.fillClockwise = false;
            alternateUpCooldownFill.fillAmount = 0f;
            alternateUpCooldownFill.enabled = false;
            Text alternateUpCooldownText = CreateText(
                "CooldownText",
                alternateUpRoot,
                font,
                string.Empty,
                14,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.72f, 0.24f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            alternateUpCooldownText.resizeTextForBestFit = true;
            alternateUpCooldownText.resizeTextMinSize = 9;
            alternateUpCooldownText.resizeTextMaxSize = 14;
            alternateUpCooldownText.raycastTarget = false;
            alternateUpCooldownText.enabled = false;
            // Direction art remains readable under the radial shade, while both
            // interaction labels stay above it.
            alternateUpCooldownRect.SetSiblingIndex(
                alternateUpGlyph.transform.GetSiblingIndex() + 1);
            alternateUpRoot.gameObject.SetActive(false);

            RectTransform alternateDownTag = CreateRect(
                "AlternateDownTag",
                root,
                Center,
                Center,
                Center,
                new Vector2(0f, -directionOffset),
                directionMarkerSize);
            RectTransform alternateLeftTag = CreateRect(
                "AlternateLeftTag",
                root,
                Center,
                Center,
                Center,
                new Vector2(-directionOffset, 0f),
                directionMarkerSize);
            RectTransform alternateRightTag = CreateRect(
                "AlternateRightTag",
                root,
                Center,
                Center,
                Center,
                new Vector2(directionOffset, 0f),
                directionMarkerSize);
            CreateDirectionalTagArt(
                alternateDownTag,
                out Image alternateDownIcon,
                out AbilityGlyphGraphic alternateDownGlyph);
            CreateDirectionalTagArt(
                alternateLeftTag,
                out Image alternateLeftIcon,
                out AbilityGlyphGraphic alternateLeftGlyph);
            CreateDirectionalTagArt(
                alternateRightTag,
                out Image alternateRightIcon,
                out AbilityGlyphGraphic alternateRightGlyph);
            Text alternateDownHint = CreateText(
                "AlternateDownHint",
                root,
                font,
                string.Empty,
                9,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextSecondary,
                Center,
                Center,
                new Vector2(-30f, -directionOffset - 20f),
                new Vector2(30f, -directionOffset - 8f));
            Text alternateLeftHint = CreateText(
                "AlternateLeftHint",
                root,
                font,
                string.Empty,
                9,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextSecondary,
                Center,
                Center,
                new Vector2(-directionOffset - 30f, -20f),
                new Vector2(-directionOffset + 30f, -8f));
            Text alternateRightHint = CreateText(
                "AlternateRightHint",
                root,
                font,
                string.Empty,
                9,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextSecondary,
                Center,
                Center,
                new Vector2(directionOffset - 30f, -20f),
                new Vector2(directionOffset + 30f, -8f));
            alternateDownHint.resizeTextForBestFit = true;
            alternateDownHint.resizeTextMinSize = 7;
            alternateDownHint.resizeTextMaxSize = 9;
            alternateLeftHint.resizeTextForBestFit = true;
            alternateLeftHint.resizeTextMinSize = 7;
            alternateLeftHint.resizeTextMaxSize = 9;
            alternateRightHint.resizeTextForBestFit = true;
            alternateRightHint.resizeTextMinSize = 7;
            alternateRightHint.resizeTextMaxSize = 9;
            AddShadow(
                alternateDownHint.gameObject,
                new Color(0f, 0f, 0f, 0.9f),
                new Vector2(1f, -1f));
            AddOutline(
                alternateDownHint.gameObject,
                new Color(0f, 0f, 0f, 0.98f),
                new Vector2(1f, -1f));
            AddShadow(
                alternateLeftHint.gameObject,
                new Color(0f, 0f, 0f, 0.9f),
                new Vector2(1f, -1f));
            AddOutline(
                alternateLeftHint.gameObject,
                new Color(0f, 0f, 0f, 0.98f),
                new Vector2(1f, -1f));
            AddShadow(
                alternateRightHint.gameObject,
                new Color(0f, 0f, 0f, 0.9f),
                new Vector2(1f, -1f));
            AddOutline(
                alternateRightHint.gameObject,
                new Color(0f, 0f, 0f, 0.98f),
                new Vector2(1f, -1f));
            alternateDownHint.raycastTarget = false;
            alternateLeftHint.raycastTarget = false;
            alternateRightHint.raycastTarget = false;
            alternateDownHint.gameObject.SetActive(false);
            alternateLeftHint.gameObject.SetActive(false);
            alternateRightHint.gameObject.SetActive(false);
            alternateDownTag.gameObject.SetActive(false);
            alternateLeftTag.gameObject.SetActive(false);
            alternateRightTag.gameObject.SetActive(false);

            AbilityId initialAbility = AbilityId.None;
            glyph.Bind(initialAbility, accent);
            HudSerialized.Apply(
                abilityButton,
                serialized =>
                {
                    HudSerialized.SetInteger(serialized, "_abilityId", (int)initialAbility);
                    HudSerialized.SetBoolean(serialized, "_scaleFlickThresholdWithDpi", true);
                    HudSerialized.SetFloat(serialized, "_flickThresholdDp", 20f);
                    HudSerialized.SetFloat(serialized, "_flickThresholdPixels", 32f);
                    HudSerialized.SetFloat(serialized, "_maximumFlickSeconds", 0.32f);
                });
            AbilityButton boundAbilityButton = abilityButton;
            HudSerialized.Apply(
                slotView,
                serialized =>
                {
                    HudSerialized.SetInteger(serialized, "_abilityId", (int)initialAbility);
                    HudSerialized.SetObject(serialized, "_inputButton", boundAbilityButton);
                    HudSerialized.SetObject(serialized, "_icon", icon);
                    HudSerialized.SetObject(serialized, "_glyph", glyph);
                    HudSerialized.SetObject(serialized, "_iconGlow", iconGlow);
                    HudSerialized.SetObject(serialized, "_rim", rim);
                    HudSerialized.SetObject(serialized, "_outerFrame", outerFrame);
                    HudSerialized.SetObject(
                        serialized,
                        "_frameHighlight",
                        frameHighlight);
                    HudSerialized.SetObject(serialized, "_innerFrame", innerFrame);
                    HudSerialized.SetObject(
                        serialized,
                        "_interactionHalo",
                        interactionHalo);
                    HudSerialized.SetObject(serialized, "_actionLabel", actionLabel);
                    HudSerialized.SetObject(serialized, "_label", label);
                    HudSerialized.SetObject(
                        serialized,
                        "_stateBadgeSurface",
                        stateBadgeSurface);
                    HudSerialized.SetObject(serialized, "_targetBadge", targetBadge);
                    HudSerialized.SetObject(serialized, "_cooldownFill", cooldownFill);
                    HudSerialized.SetObject(serialized, "_cooldownText", cooldownText);
                    HudSerialized.SetObject(
                        serialized,
                        "_globalCooldownFill",
                        globalCooldownFill);
                    HudSerialized.SetObject(
                        serialized,
                        "_globalCooldownLockRoot",
                        globalCooldownLock.gameObject);
                    HudSerialized.SetObject(serialized, "_stateSurface", stateSurface);
                    HudSerialized.SetObject(serialized, "_canvasGroup", canvasGroup);
                    HudSerialized.SetObject(serialized, "_readyPulse", readyPulse);
                    HudSerialized.SetObject(serialized, "_readyPulseGraphic", readyPulseGraphic);
                    HudSerialized.SetObject(serialized, "_visualRoot", visualRoot);
                    HudSerialized.SetObject(serialized, "_blockedMark", blockedMark.gameObject);
                    HudSerialized.SetObject(
                        serialized,
                        "_directionalGuide",
                        radialGuide);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateUpRoot",
                        alternateUpRoot.gameObject);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateUpIcon",
                        alternateUpIcon);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateUpGlyph",
                        alternateUpGlyph);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateUpSurface",
                        null);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateUpHighlight",
                        null);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateUpLabel",
                        alternateUpLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateUpCooldownFill",
                        alternateUpCooldownFill);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateUpCooldownText",
                        alternateUpCooldownText);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateDownHint",
                        alternateDownHint);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateDownHintRoot",
                        alternateDownTag.gameObject);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateDownHintSurface",
                        null);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateDownIcon",
                        alternateDownIcon);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateDownGlyph",
                        alternateDownGlyph);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateLeftHint",
                        alternateLeftHint);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateLeftHintRoot",
                        alternateLeftTag.gameObject);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateLeftHintSurface",
                        null);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateLeftIcon",
                        alternateLeftIcon);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateLeftGlyph",
                        alternateLeftGlyph);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateRightHint",
                        alternateRightHint);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateRightHintRoot",
                        alternateRightTag.gameObject);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateRightHintSurface",
                        null);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateRightIcon",
                        alternateRightIcon);
                    HudSerialized.SetObject(
                        serialized,
                        "_alternateRightGlyph",
                        alternateRightGlyph);
                });
        }

        private static void WireInput(CombatInputSource inputSource, GameplayReferences gameplay)
        {
            HudSerialized.Apply(
                inputSource,
                serialized =>
                {
                    HudSerialized.SetObject(serialized, "_movementJoystick", gameplay.Joystick);
                    HudSerialized.SetObject(serialized, "_worldSurface", gameplay.WorldSurface);
                    HudSerialized.SetObjects(serialized, "_abilityButtons", gameplay.AbilityButtons);
                    HudSerialized.SetBoolean(serialized, "_enableDesktopFallback", true);
                    HudSerialized.SetFloat(
                        serialized,
                        "_touchOrbitDegreesPerScreen",
                        180f);
                    HudSerialized.SetFloat(
                        serialized,
                        "_desktopOrbitDegreesPerSecond",
                        90f);
                });
        }

        private static void WireHud(
            PrototypeHud hud,
            HomeReferences home,
            GameplayReferences gameplay,
            ResultsReferences results,
            AbilityIconCatalog abilityIconCatalog)
        {
            HudSerialized.Apply(
                hud,
                serialized =>
                {
                    HudSerialized.SetObject(serialized, "_homeRoot", home.Root);
                    HudSerialized.SetObject(serialized, "_gameplayRoot", gameplay.Root);
                    HudSerialized.SetObject(serialized, "_resultsRoot", results.Root);
                    HudSerialized.SetObject(serialized, "_healerButton", home.HealerButton);
                    HudSerialized.SetObject(serialized, "_rogueButton", home.RogueButton);
                    HudSerialized.SetObject(serialized, "_mageButton", home.MageButton);
                    HudSerialized.SetObject(serialized, "_hunterButton", home.HunterButton);
                    HudSerialized.SetObject(serialized, "_healerSelectedMarker", home.HealerMarker);
                    HudSerialized.SetObject(serialized, "_rogueSelectedMarker", home.RogueMarker);
                    HudSerialized.SetObject(serialized, "_mageSelectedMarker", home.MageMarker);
                    HudSerialized.SetObject(serialized, "_hunterSelectedMarker", home.HunterMarker);
                    HudSerialized.SetObject(
                        serialized,
                        "_disciplineSpecializationRoot",
                        home.DisciplineSpecializationRoot);
                    HudSerialized.SetObject(
                        serialized,
                        "_rogueSpecializationRoot",
                        home.RogueSpecializationRoot);
                    HudSerialized.SetObject(
                        serialized,
                        "_mageSpecializationRoot",
                        home.MageSpecializationRoot);
                    HudSerialized.SetObject(
                        serialized,
                        "_hunterSpecializationRoot",
                        home.HunterSpecializationRoot);
                    HudSerialized.SetObject(serialized, "_disciplineButton", home.DisciplineButton);
                    HudSerialized.SetObject(serialized, "_shadowButton", home.ShadowButton);
                    HudSerialized.SetObject(serialized, "_shadowDanceButton", home.ShadowDanceButton);
                    HudSerialized.SetObject(serialized, "_mutilateButton", home.MutilateButton);
                    HudSerialized.SetObject(serialized, "_fireButton", home.FireButton);
                    HudSerialized.SetObject(serialized, "_frostButton", home.FrostButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_marksmanshipButton",
                        home.MarksmanshipButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_disciplineSelectedMarker",
                        home.DisciplineMarker);
                    HudSerialized.SetObject(
                        serialized,
                        "_shadowSelectedMarker",
                        home.ShadowMarker);
                    HudSerialized.SetObject(
                        serialized,
                        "_shadowDanceSelectedMarker",
                        home.ShadowDanceMarker);
                    HudSerialized.SetObject(
                        serialized,
                        "_mutilateSelectedMarker",
                        home.MutilateMarker);
                    HudSerialized.SetObject(serialized, "_fireSelectedMarker", home.FireMarker);
                    HudSerialized.SetObject(serialized, "_frostSelectedMarker", home.FrostMarker);
                    HudSerialized.SetObject(
                        serialized,
                        "_marksmanshipSelectedMarker",
                        home.MarksmanshipMarker);
                    HudSerialized.SetObject(serialized, "_arenaModeButton", home.ArenaButton);
                    HudSerialized.SetObject(serialized, "_siegeModeButton", home.SiegeButton);
                    HudSerialized.SetObject(serialized, "_bossModeButton", home.BossButton);
                    HudSerialized.SetObject(serialized, "_levelingModeButton", home.LevelingButton);
                    HudSerialized.SetObject(serialized, "_pveLevelingModeButton", home.PveLevelingButton);
                    HudSerialized.SetObject(serialized, "_pveLevelingSelectedMarker", home.PveLevelingMarker);
                    HudSerialized.SetObject(serialized, "_arenaSelectedMarker", home.ArenaMarker);
                    HudSerialized.SetObject(serialized, "_siegeSelectedMarker", home.SiegeMarker);
                    HudSerialized.SetObject(serialized, "_bossSelectedMarker", home.BossMarker);
                    HudSerialized.SetObject(serialized, "_levelingSelectedMarker", home.LevelingMarker);
                    HudSerialized.SetObject(serialized, "_startButton", home.StartButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_skipTrainingButton",
                        home.SkipTrainingButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_startButtonLabel",
                        home.StartButton.transform.Find("Label").GetComponent<Text>());
                    HudSerialized.SetObject(
                        serialized,
                        "_trainingModeLabel",
                        home.LevelingButton.transform.Find("Label").GetComponent<Text>());
                    HudSerialized.SetObject(serialized, "_classDetailLabel", home.ClassDetailLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeVariantRoot",
                        home.SiegeVariantRoot);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeSoloLaneButton",
                        home.SiegeSoloLaneButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeFourLanesButton",
                        home.SiegeFourLanesButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeSoloLaneSelectedMarker",
                        home.SiegeSoloLaneMarker);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeFourLanesSelectedMarker",
                        home.SiegeFourLanesMarker);
                    HudSerialized.SetObjects(serialized, "_abilitySlots", gameplay.AbilitySlots);
                    HudSerialized.SetObject(
                        serialized,
                        "_abilityIconCatalog",
                        abilityIconCatalog);
                    HudSerialized.SetObject(serialized, "_friendlyFighterRoot", gameplay.FriendlyFighterRoot);
                    HudSerialized.SetObject(serialized, "_hostileFighterRoot", gameplay.HostileFighterRoot);
                    HudSerialized.SetObjects(serialized, "_fighterViews", gameplay.FighterViews);
                    HudSerialized.SetObject(serialized, "_healthLabel", gameplay.HealthLabel);
                    HudSerialized.SetObject(serialized, "_healthFill", gameplay.HealthFill);
                    HudSerialized.SetObject(serialized, "_teamLabel", gameplay.TeamLabel);
                    HudSerialized.SetObject(serialized, "_objectiveLabel", gameplay.ObjectiveLabel);
                    HudSerialized.SetObject(serialized, "_progressionRoot", gameplay.ProgressionRoot);
                    HudSerialized.SetObject(serialized, "_levelLabel", gameplay.LevelLabel);
                    HudSerialized.SetObject(serialized, "_experienceFill", gameplay.ExperienceFill);
                    HudSerialized.SetObject(serialized, "_experienceLabel", gameplay.ExperienceLabel);
                    HudSerialized.SetBoolean(serialized, "_showDetailedTargetPanel", true);
                    HudSerialized.SetObject(serialized, "_targetRoot", gameplay.TargetRoot);
                    HudSerialized.SetObject(serialized, "_targetSurface", gameplay.TargetSurface);
                    HudSerialized.SetObject(serialized, "_targetRelationAccent", gameplay.TargetRelationAccent);
                    HudSerialized.SetObject(serialized, "_targetParticipantLabel", gameplay.TargetParticipantLabel);
                    HudSerialized.SetObject(serialized, "_targetRelationLabel", gameplay.TargetRelationLabel);
                    HudSerialized.SetObject(serialized, "_targetLabel", gameplay.TargetLabel);
                    HudSerialized.SetObject(serialized, "_targetHealthFill", gameplay.TargetHealthFill);
                    HudSerialized.SetObject(serialized, "_targetBarrierFill", gameplay.TargetBarrierFill);
                    HudSerialized.SetObject(serialized, "_targetHealthLabel", gameplay.TargetHealthLabel);
                    HudSerialized.SetObject(serialized, "_targetBarrierLabel", gameplay.TargetBarrierLabel);
                    HudSerialized.SetObject(serialized, "_targetResourceRoot", gameplay.TargetResourceRoot);
                    HudSerialized.SetObject(serialized, "_targetResourceFill", gameplay.TargetResourceFill);
                    HudSerialized.SetObject(
                        serialized,
                        "_targetResourceTypeLabel",
                        gameplay.TargetResourceTypeLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_targetResourceValueLabel",
                        gameplay.TargetResourceValueLabel);
                    HudSerialized.SetObject(serialized, "_targetStatusLabel", gameplay.TargetStatusLabel);
                    HudSerialized.SetObject(serialized, "_targetCastRoot", gameplay.TargetCastRoot);
                    HudSerialized.SetObject(serialized, "_targetCastSurface", gameplay.TargetCastSurface);
                    HudSerialized.SetObject(serialized, "_targetCastLabel", gameplay.TargetCastLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_targetCastStateLabel",
                        gameplay.TargetCastStateLabel);
                    HudSerialized.SetObject(serialized, "_targetCastFill", gameplay.TargetCastFill);
                    HudSerialized.SetObjects(serialized, "_targetAuraRoots", gameplay.TargetAuraRoots);
                    HudSerialized.SetObjects(
                        serialized,
                        "_targetAuraSurfaces",
                        gameplay.TargetAuraSurfaces);
                    HudSerialized.SetObjects(serialized, "_targetAuraLabels", gameplay.TargetAuraLabels);
                    HudSerialized.SetObject(
                        serialized,
                        "_cameraOrbitHintCanvasGroup",
                        gameplay.CameraOrbitHintCanvasGroup);
                    HudSerialized.SetObject(
                        serialized,
                        "_combatNoticeCanvasGroup",
                        gameplay.CombatNoticeCanvasGroup);
                    HudSerialized.SetObject(serialized, "_combatNoticeLabel", gameplay.CombatNoticeLabel);
                    HudSerialized.SetObject(serialized, "_castRoot", gameplay.CastRoot);
                    HudSerialized.SetObject(serialized, "_castLabel", gameplay.CastLabel);
                    HudSerialized.SetObject(serialized, "_castFill", gameplay.CastFill);
                    HudSerialized.SetObject(serialized, "_playerResourceRoot", gameplay.PlayerResourceRoot);
                    HudSerialized.SetObject(serialized, "_playerResourceFill", gameplay.PlayerResourceFill);
                    HudSerialized.SetObject(
                        serialized,
                        "_playerResourceTypeLabel",
                        gameplay.PlayerResourceTypeLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_playerResourceValueLabel",
                        gameplay.PlayerResourceValueLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_comboPointRoot",
                        gameplay.ComboPointRoot);
                    HudSerialized.SetObjects(
                        serialized,
                        "_comboPointPips",
                        gameplay.ComboPointPips);
                    HudSerialized.SetObject(
                        serialized,
                        "_comboPointLabel",
                        gameplay.ComboPointLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeSupplyRoot",
                        gameplay.SiegeSupply.Root);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegePurchaseDrawer",
                        gameplay.SiegeSupply.PurchaseDrawer);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeGoldLabel",
                        gameplay.SiegeSupply.GoldLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeHealthPotionLabel",
                        gameplay.SiegeSupply.HealthPotionLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeManaPotionLabel",
                        gameplay.SiegeSupply.ManaPotionLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeShopButton",
                        gameplay.SiegeSupply.ShopButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeShopLabel",
                        gameplay.SiegeSupply.ShopLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeBuyHealthButton",
                        gameplay.SiegeSupply.BuyHealthButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeUseHealthButton",
                        gameplay.SiegeSupply.UseHealthButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeBuyManaButton",
                        gameplay.SiegeSupply.BuyManaButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeUseManaButton",
                        gameplay.SiegeSupply.UseManaButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeBuyHealthLabel",
                        gameplay.SiegeSupply.BuyHealthLabel);
                    HudSerialized.SetObject(
                        serialized,
                        "_siegeBuyManaLabel",
                        gameplay.SiegeSupply.BuyManaLabel);
                    HudSerialized.SetObject(serialized, "_resultTitleLabel", results.Title);
                    HudSerialized.SetObject(serialized, "_resultRecapLabel", results.Recap);
                    HudSerialized.SetObject(serialized, "_replayButton", results.ReplayButton);
                    HudSerialized.SetObject(
                        serialized,
                        "_replayButtonLabel",
                        results.ReplayButton.transform.Find("Label").GetComponent<Text>());
                    HudSerialized.SetObject(serialized, "_leaveButton", results.LeaveButton);
                });
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            Font font,
            string label,
            Vector2 anchorMinimum,
            Vector2 anchorMaximum,
            Color accent,
            Sprite panelSprite,
            int fontSize,
            out GameObject selectedMarker)
        {
            RectTransform rect = CreatePanel(
                name,
                parent,
                anchorMinimum,
                anchorMaximum,
                Center,
                Vector2.zero,
                Vector2.zero,
                SurfaceRaised,
                panelSprite,
                true);
            Image image = rect.GetComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.2f);
            colors.pressedColor = Color.Lerp(Color.white, accent, 0.42f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.36f, 0.4f, 0.46f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreatePanel(
                "Accent",
                rect,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -3f),
                new Vector2(-20f, 6f),
                accent,
                null,
                false);
            Text text = CreateText(
                "Label",
                rect,
                font,
                label,
                fontSize,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 8f),
                new Vector2(-12f, -8f));
            text.lineSpacing = 1.15f;

            RectTransform marker = CreatePanel(
                "Selected",
                rect,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 3f),
                new Vector2(-16f, 8f),
                accent,
                null,
                false);
            selectedMarker = marker.gameObject;
            return button;
        }

        private static Button CreateCompactButton(
            string name,
            RectTransform parent,
            Font font,
            string label,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            Color color,
            Sprite sprite,
            int fontSize)
        {
            RectTransform rect = CreatePanel(
                name,
                parent,
                anchor,
                anchor,
                Center,
                position,
                size,
                color,
                sprite,
                true);
            Image image = rect.GetComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            CreateText(
                "Label",
                rect,
                font,
                label,
                fontSize,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                TextPrimary,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private static Image CreateFill(
            RectTransform parent,
            string name,
            Color color,
            Image.FillMethod fillMethod)
        {
            RectTransform fillRect = CreatePanel(
                name,
                parent,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                new Vector2(-8f, -8f),
                color,
                BuiltinUiSprite(),
                false);
            Image fill = fillRect.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = fillMethod;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;
            return fill;
        }

        private static void CreateAbilityFrameArt(
            RectTransform parent,
            Sprite medallionFrameSprite)
        {
            if (medallionFrameSprite == null)
            {
                throw new ArgumentNullException(nameof(medallionFrameSprite));
            }

            RectTransform frame = CreateRect(
                "MedallionFrameArt",
                parent,
                Vector2.zero,
                Vector2.one,
                Center,
                Vector2.zero,
                Vector2.zero);
            Image image = frame.gameObject.AddComponent<Image>();
            image.sprite = medallionFrameSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static void AddAbilityTagPolish(
            RectTransform tag,
            Sprite panelSprite)
        {
            AddShadow(
                tag.gameObject,
                new Color(0f, 0f, 0f, 0.88f),
                new Vector2(2f, -3f));
            CreatePanel(
                "TopHighlight",
                tag,
                new Vector2(0.08f, 0.79f),
                new Vector2(0.92f, 0.92f),
                Center,
                Vector2.zero,
                Vector2.zero,
                new Color(1f, 0.88f, 0.62f, 0.22f),
                panelSprite,
                false);
        }

        private static void CreateDirectionalTagArt(
            RectTransform tag,
            out Image icon,
            out AbilityGlyphGraphic glyph)
        {
            RectTransform iconRect = CreatePanel(
                "Icon",
                tag,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(48f, 48f),
                Color.clear,
                null,
                false);
            icon = iconRect.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.enabled = false;

            RectTransform glyphRect = CreateRect(
                "Glyph",
                tag,
                Center,
                Center,
                Center,
                Vector2.zero,
                new Vector2(48f, 48f));
            glyph = glyphRect.gameObject.AddComponent<AbilityGlyphGraphic>();
            glyph.raycastTarget = false;
            glyph.enabled = false;
        }

        private static RectTransform CreateAbilityRing(
            string name,
            RectTransform parent,
            Vector2 anchorMinimum,
            Vector2 anchorMaximum,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color,
            float thickness)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMinimum,
                anchorMaximum,
                Center,
                anchoredPosition,
                sizeDelta);
            AbilityRingGraphic ring =
                rect.gameObject.AddComponent<AbilityRingGraphic>();
            ring.color = color;
            ring.raycastTarget = false;
            // These rings are prominent at phone-scale HUD sizes. The maximum
            // supported segment count keeps the circular silhouette smooth
            // without relying on a texture mask that would crop the icon art.
            ring.Configure(thickness, 96);
            return rect;
        }

        private static RectTransform CreatePanel(
            string name,
            RectTransform parent,
            Vector2 anchorMinimum,
            Vector2 anchorMaximum,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color,
            Sprite sprite,
            bool raycastTarget)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMinimum,
                anchorMaximum,
                pivot,
                anchoredPosition,
                sizeDelta);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = raycastTarget;
            return rect;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            Font font,
            string value,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color,
            Vector2 anchorMinimum,
            Vector2 anchorMaximum,
            Vector2 offsetMinimum,
            Vector2 offsetMaximum)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMinimum,
                anchorMaximum,
                Center,
                Vector2.zero,
                Vector2.zero);
            rect.offsetMin = offsetMinimum;
            rect.offsetMax = offsetMaximum;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMinimum,
            Vector2 anchorMaximum,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMinimum;
            rect.anchorMax = anchorMaximum;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static void AddShadow(GameObject target, Color color, Vector2 distance)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static Font ResolveFont(Font font)
        {
            if (font != null)
            {
                return font;
            }

            Font builtIn = AssetDatabase.GetBuiltinExtraResource<Font>("LegacyRuntime.ttf");
            if (builtIn == null)
            {
                builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (builtIn == null)
            {
                throw new InvalidOperationException("Unity built-in LegacyRuntime font is unavailable.");
            }

            return builtIn;
        }

        private static Sprite BuiltinUiSprite()
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite == null)
            {
                throw new InvalidOperationException("Unity built-in UI sprite is unavailable.");
            }

            return sprite;
        }

        private static Sprite BuiltinKnobSprite(Sprite fallback)
        {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            return sprite != null ? sprite : fallback;
        }

        private static void SetAllSelectionMarkers(HomeReferences home, bool isActive)
        {
            home.HealerMarker.SetActive(isActive);
            home.RogueMarker.SetActive(isActive);
            home.MageMarker.SetActive(isActive);
            home.HunterMarker.SetActive(isActive);
            home.DisciplineMarker.SetActive(isActive);
            home.ShadowMarker.SetActive(isActive);
            home.ShadowDanceMarker.SetActive(isActive);
            home.MutilateMarker.SetActive(isActive);
            home.FireMarker.SetActive(isActive);
            home.FrostMarker.SetActive(isActive);
            home.MarksmanshipMarker.SetActive(isActive);
            home.ArenaMarker.SetActive(isActive);
            home.SiegeMarker.SetActive(isActive);
            home.BossMarker.SetActive(isActive);
            home.LevelingMarker.SetActive(isActive);
            home.SiegeSoloLaneMarker.SetActive(isActive);
            home.SiegeFourLanesMarker.SetActive(isActive);
        }

        private readonly struct HomeReferences
        {
            internal HomeReferences(
                GameObject root,
                Button healerButton,
                Button rogueButton,
                Button mageButton,
                Button hunterButton,
                GameObject healerMarker,
                GameObject rogueMarker,
                GameObject mageMarker,
                GameObject hunterMarker,
                GameObject disciplineSpecializationRoot,
                GameObject rogueSpecializationRoot,
                GameObject mageSpecializationRoot,
                GameObject hunterSpecializationRoot,
                Button disciplineButton,
                Button shadowButton,
                Button shadowDanceButton,
                Button mutilateButton,
                Button fireButton,
                Button frostButton,
                Button marksmanshipButton,
                GameObject disciplineMarker,
                GameObject shadowMarker,
                GameObject shadowDanceMarker,
                GameObject mutilateMarker,
                GameObject fireMarker,
                GameObject frostMarker,
                GameObject marksmanshipMarker,
                Button arenaButton,
                Button siegeButton,
                Button bossButton,
                Button levelingButton,
                GameObject arenaMarker,
                GameObject siegeMarker,
                GameObject bossMarker,
                GameObject levelingMarker,
                Button startButton,
                Button skipTrainingButton,
                Text classDetailLabel,
                GameObject siegeVariantRoot,
                Button siegeSoloLaneButton,
                Button siegeFourLanesButton,
                GameObject siegeSoloLaneMarker,
                GameObject siegeFourLanesMarker,
                Button pveLevelingButton,
                GameObject pveLevelingMarker)
            {
                Root = root;
                HealerButton = healerButton;
                RogueButton = rogueButton;
                MageButton = mageButton;
                HunterButton = hunterButton;
                HealerMarker = healerMarker;
                RogueMarker = rogueMarker;
                MageMarker = mageMarker;
                HunterMarker = hunterMarker;
                DisciplineSpecializationRoot = disciplineSpecializationRoot;
                RogueSpecializationRoot = rogueSpecializationRoot;
                MageSpecializationRoot = mageSpecializationRoot;
                HunterSpecializationRoot = hunterSpecializationRoot;
                DisciplineButton = disciplineButton;
                ShadowButton = shadowButton;
                ShadowDanceButton = shadowDanceButton;
                MutilateButton = mutilateButton;
                FireButton = fireButton;
                FrostButton = frostButton;
                MarksmanshipButton = marksmanshipButton;
                DisciplineMarker = disciplineMarker;
                ShadowMarker = shadowMarker;
                ShadowDanceMarker = shadowDanceMarker;
                MutilateMarker = mutilateMarker;
                FireMarker = fireMarker;
                FrostMarker = frostMarker;
                MarksmanshipMarker = marksmanshipMarker;
                ArenaButton = arenaButton;
                SiegeButton = siegeButton;
                BossButton = bossButton;
                LevelingButton = levelingButton;
                ArenaMarker = arenaMarker;
                SiegeMarker = siegeMarker;
                BossMarker = bossMarker;
                LevelingMarker = levelingMarker;
                StartButton = startButton;
                SkipTrainingButton = skipTrainingButton;
                ClassDetailLabel = classDetailLabel;
                SiegeVariantRoot = siegeVariantRoot;
                SiegeSoloLaneButton = siegeSoloLaneButton;
                SiegeFourLanesButton = siegeFourLanesButton;
                SiegeSoloLaneMarker = siegeSoloLaneMarker;
                SiegeFourLanesMarker = siegeFourLanesMarker;
                PveLevelingButton = pveLevelingButton;
                PveLevelingMarker = pveLevelingMarker;
            }

            internal GameObject Root { get; }
            internal Button HealerButton { get; }
            internal Button RogueButton { get; }
            internal Button MageButton { get; }
            internal Button HunterButton { get; }
            internal GameObject HealerMarker { get; }
            internal GameObject RogueMarker { get; }
            internal GameObject MageMarker { get; }
            internal GameObject HunterMarker { get; }
            internal GameObject DisciplineSpecializationRoot { get; }
            internal GameObject RogueSpecializationRoot { get; }
            internal GameObject MageSpecializationRoot { get; }
            internal GameObject HunterSpecializationRoot { get; }
            internal Button DisciplineButton { get; }
            internal Button ShadowButton { get; }
            internal Button ShadowDanceButton { get; }
            internal Button MutilateButton { get; }
            internal Button FireButton { get; }
            internal Button FrostButton { get; }
            internal Button MarksmanshipButton { get; }
            internal GameObject DisciplineMarker { get; }
            internal GameObject ShadowMarker { get; }
            internal GameObject ShadowDanceMarker { get; }
            internal GameObject MutilateMarker { get; }
            internal GameObject FireMarker { get; }
            internal GameObject FrostMarker { get; }
            internal GameObject MarksmanshipMarker { get; }
            internal Button ArenaButton { get; }
            internal Button SiegeButton { get; }
            internal Button BossButton { get; }
            internal Button LevelingButton { get; }
            internal Button PveLevelingButton { get; }
            internal GameObject PveLevelingMarker { get; }
            internal GameObject ArenaMarker { get; }
            internal GameObject SiegeMarker { get; }
            internal GameObject BossMarker { get; }
            internal GameObject LevelingMarker { get; }
            internal Button StartButton { get; }
            internal Button SkipTrainingButton { get; }
            internal Text ClassDetailLabel { get; }
            internal GameObject SiegeVariantRoot { get; }
            internal Button SiegeSoloLaneButton { get; }
            internal Button SiegeFourLanesButton { get; }
            internal GameObject SiegeSoloLaneMarker { get; }
            internal GameObject SiegeFourLanesMarker { get; }
        }

        private readonly struct GameplayReferences
        {
            internal GameplayReferences(
                GameObject root,
                WorldTapSurface worldSurface,
                FloatingJoystick joystick,
                CanvasGroup cameraOrbitHintCanvasGroup,
                AbilityButton[] abilityButtons,
                AbilitySlotView[] abilitySlots,
                RectTransform friendlyFighterRoot,
                RectTransform hostileFighterRoot,
                CombatantStatusView[] fighterViews,
                Text healthLabel,
                Image healthFill,
                Text teamLabel,
                Text objectiveLabel,
                GameObject progressionRoot,
                Text levelLabel,
                Image experienceFill,
                Text experienceLabel,
                GameObject targetRoot,
                Graphic targetSurface,
                Graphic targetRelationAccent,
                Text targetParticipantLabel,
                Text targetRelationLabel,
                Text targetLabel,
                Image targetHealthFill,
                Image targetBarrierFill,
                Text targetHealthLabel,
                Text targetBarrierLabel,
                GameObject targetResourceRoot,
                Image targetResourceFill,
                Text targetResourceTypeLabel,
                Text targetResourceValueLabel,
                Text targetStatusLabel,
                GameObject targetCastRoot,
                Graphic targetCastSurface,
                Text targetCastLabel,
                Text targetCastStateLabel,
                Image targetCastFill,
                GameObject[] targetAuraRoots,
                Graphic[] targetAuraSurfaces,
                Text[] targetAuraLabels,
                CanvasGroup combatNoticeCanvasGroup,
                Text combatNoticeLabel,
                GameObject castRoot,
                Text castLabel,
                Image castFill,
                GameObject playerResourceRoot,
                Image playerResourceFill,
                Text playerResourceTypeLabel,
                Text playerResourceValueLabel,
                GameObject comboPointRoot,
                Graphic[] comboPointPips,
                Text comboPointLabel,
                SiegeSupplyReferences siegeSupply,
                OffscreenThreatPresenter offscreenThreatPresenter)
            {
                Root = root;
                WorldSurface = worldSurface;
                Joystick = joystick;
                CameraOrbitHintCanvasGroup = cameraOrbitHintCanvasGroup;
                AbilityButtons = abilityButtons;
                AbilitySlots = abilitySlots;
                FriendlyFighterRoot = friendlyFighterRoot;
                HostileFighterRoot = hostileFighterRoot;
                FighterViews = fighterViews;
                HealthLabel = healthLabel;
                HealthFill = healthFill;
                TeamLabel = teamLabel;
                ObjectiveLabel = objectiveLabel;
                ProgressionRoot = progressionRoot;
                LevelLabel = levelLabel;
                ExperienceFill = experienceFill;
                ExperienceLabel = experienceLabel;
                TargetRoot = targetRoot;
                TargetSurface = targetSurface;
                TargetRelationAccent = targetRelationAccent;
                TargetParticipantLabel = targetParticipantLabel;
                TargetRelationLabel = targetRelationLabel;
                TargetLabel = targetLabel;
                TargetHealthFill = targetHealthFill;
                TargetBarrierFill = targetBarrierFill;
                TargetHealthLabel = targetHealthLabel;
                TargetBarrierLabel = targetBarrierLabel;
                TargetResourceRoot = targetResourceRoot;
                TargetResourceFill = targetResourceFill;
                TargetResourceTypeLabel = targetResourceTypeLabel;
                TargetResourceValueLabel = targetResourceValueLabel;
                TargetStatusLabel = targetStatusLabel;
                TargetCastRoot = targetCastRoot;
                TargetCastSurface = targetCastSurface;
                TargetCastLabel = targetCastLabel;
                TargetCastStateLabel = targetCastStateLabel;
                TargetCastFill = targetCastFill;
                TargetAuraRoots = targetAuraRoots;
                TargetAuraSurfaces = targetAuraSurfaces;
                TargetAuraLabels = targetAuraLabels;
                CombatNoticeCanvasGroup = combatNoticeCanvasGroup;
                CombatNoticeLabel = combatNoticeLabel;
                CastRoot = castRoot;
                CastLabel = castLabel;
                CastFill = castFill;
                PlayerResourceRoot = playerResourceRoot;
                PlayerResourceFill = playerResourceFill;
                PlayerResourceTypeLabel = playerResourceTypeLabel;
                PlayerResourceValueLabel = playerResourceValueLabel;
                ComboPointRoot = comboPointRoot;
                ComboPointPips = comboPointPips;
                ComboPointLabel = comboPointLabel;
                SiegeSupply = siegeSupply;
                OffscreenThreatPresenter = offscreenThreatPresenter;
            }

            internal GameObject Root { get; }
            internal WorldTapSurface WorldSurface { get; }
            internal FloatingJoystick Joystick { get; }
            internal CanvasGroup CameraOrbitHintCanvasGroup { get; }
            internal AbilityButton[] AbilityButtons { get; }
            internal AbilitySlotView[] AbilitySlots { get; }
            internal RectTransform FriendlyFighterRoot { get; }
            internal RectTransform HostileFighterRoot { get; }
            internal CombatantStatusView[] FighterViews { get; }
            internal Text HealthLabel { get; }
            internal Image HealthFill { get; }
            internal Text TeamLabel { get; }
            internal Text ObjectiveLabel { get; }
            internal GameObject ProgressionRoot { get; }
            internal Text LevelLabel { get; }
            internal Image ExperienceFill { get; }
            internal Text ExperienceLabel { get; }
            internal GameObject TargetRoot { get; }
            internal Graphic TargetSurface { get; }
            internal Graphic TargetRelationAccent { get; }
            internal Text TargetParticipantLabel { get; }
            internal Text TargetRelationLabel { get; }
            internal Text TargetLabel { get; }
            internal Image TargetHealthFill { get; }
            internal Image TargetBarrierFill { get; }
            internal Text TargetHealthLabel { get; }
            internal Text TargetBarrierLabel { get; }
            internal GameObject TargetResourceRoot { get; }
            internal Image TargetResourceFill { get; }
            internal Text TargetResourceTypeLabel { get; }
            internal Text TargetResourceValueLabel { get; }
            internal Text TargetStatusLabel { get; }
            internal GameObject TargetCastRoot { get; }
            internal Graphic TargetCastSurface { get; }
            internal Text TargetCastLabel { get; }
            internal Text TargetCastStateLabel { get; }
            internal Image TargetCastFill { get; }
            internal GameObject[] TargetAuraRoots { get; }
            internal Graphic[] TargetAuraSurfaces { get; }
            internal Text[] TargetAuraLabels { get; }
            internal CanvasGroup CombatNoticeCanvasGroup { get; }
            internal Text CombatNoticeLabel { get; }
            internal GameObject CastRoot { get; }
            internal Text CastLabel { get; }
            internal Image CastFill { get; }
            internal GameObject PlayerResourceRoot { get; }
            internal Image PlayerResourceFill { get; }
            internal Text PlayerResourceTypeLabel { get; }
            internal Text PlayerResourceValueLabel { get; }
            internal GameObject ComboPointRoot { get; }
            internal Graphic[] ComboPointPips { get; }
            internal Text ComboPointLabel { get; }
            internal SiegeSupplyReferences SiegeSupply { get; }
            internal OffscreenThreatPresenter OffscreenThreatPresenter { get; }
        }

        private readonly struct SiegeSupplyReferences
        {
            internal SiegeSupplyReferences(
                GameObject root,
                GameObject purchaseDrawer,
                Text goldLabel,
                Text healthPotionLabel,
                Text manaPotionLabel,
                Button shopButton,
                Text shopLabel,
                Button buyHealthButton,
                Button useHealthButton,
                Button buyManaButton,
                Button useManaButton,
                Text buyHealthLabel,
                Text buyManaLabel)
            {
                Root = root;
                PurchaseDrawer = purchaseDrawer;
                GoldLabel = goldLabel;
                HealthPotionLabel = healthPotionLabel;
                ManaPotionLabel = manaPotionLabel;
                ShopButton = shopButton;
                ShopLabel = shopLabel;
                BuyHealthButton = buyHealthButton;
                UseHealthButton = useHealthButton;
                BuyManaButton = buyManaButton;
                UseManaButton = useManaButton;
                BuyHealthLabel = buyHealthLabel;
                BuyManaLabel = buyManaLabel;
            }

            internal GameObject Root { get; }
            internal GameObject PurchaseDrawer { get; }
            internal Text GoldLabel { get; }
            internal Text HealthPotionLabel { get; }
            internal Text ManaPotionLabel { get; }
            internal Button ShopButton { get; }
            internal Text ShopLabel { get; }
            internal Button BuyHealthButton { get; }
            internal Button UseHealthButton { get; }
            internal Button BuyManaButton { get; }
            internal Button UseManaButton { get; }
            internal Text BuyHealthLabel { get; }
            internal Text BuyManaLabel { get; }
        }

        private readonly struct FighterCardReferences
        {
            internal FighterCardReferences(
                CombatantStatusView view,
                Text healthLabel,
                Image healthFill)
            {
                View = view;
                HealthLabel = healthLabel;
                HealthFill = healthFill;
            }

            internal CombatantStatusView View { get; }

            internal Text HealthLabel { get; }

            internal Image HealthFill { get; }
        }

        private readonly struct ResultsReferences
        {
            internal ResultsReferences(
                GameObject root,
                Text title,
                Text recap,
                Button replayButton,
                Button leaveButton)
            {
                Root = root;
                Title = title;
                Recap = recap;
                ReplayButton = replayButton;
                LeaveButton = leaveButton;
            }

            internal GameObject Root { get; }
            internal Text Title { get; }
            internal Text Recap { get; }
            internal Button ReplayButton { get; }
            internal Button LeaveButton { get; }
        }

        private static class HudSerialized
        {
            internal static void Apply(Component component, Action<SerializedObject> configure)
            {
                SerializedObject serialized = new SerializedObject(component);
                configure(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
            }

            internal static void SetObject(
                SerializedObject serialized,
                string propertyName,
                UnityEngine.Object value)
            {
                Require(serialized, propertyName).objectReferenceValue = value;
            }

            internal static void SetBoolean(SerializedObject serialized, string propertyName, bool value)
            {
                Require(serialized, propertyName).boolValue = value;
            }

            internal static void SetInteger(SerializedObject serialized, string propertyName, int value)
            {
                Require(serialized, propertyName).intValue = value;
            }

            internal static void SetFloat(SerializedObject serialized, string propertyName, float value)
            {
                Require(serialized, propertyName).floatValue = value;
            }

            internal static void SetObjects<T>(
                SerializedObject serialized,
                string propertyName,
                IReadOnlyList<T> values)
                where T : UnityEngine.Object
            {
                SerializedProperty property = Require(serialized, propertyName);
                property.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            }

            private static SerializedProperty Require(SerializedObject serialized, string propertyName)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null)
                {
                    throw new InvalidOperationException(
                        $"{serialized.targetObject.GetType().Name} is missing serialized property '{propertyName}'.");
                }

                return property;
            }
        }
    }
}
