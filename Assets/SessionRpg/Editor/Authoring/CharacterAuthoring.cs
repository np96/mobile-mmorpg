using System;
using System.Collections.Generic;
using System.Linq;
using SessionRpg.Core;
using SessionRpg.Editor.Assets;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    internal static class CharacterAuthoring
    {
        private const string PrefabFolder = "Assets/SessionRpg/Prefabs";
        private const string PrefabPath = PrefabFolder + "/Combatant.prefab";

        private const string CharacterArtFolder = AuthoringAssets.ArtFolder + "/Characters";

        // This is deliberately a source-level A/B switch, not a runtime option.
        // The first-party repaint retains the CC0 Ranger UV layout and may be
        // reverted for visual diagnosis without touching the third-party source.
        private const bool UseFirstPartyHunterOutfitAlbedo = true;

        private const string HunterFallbackOutfitAlbedoPath =
            "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Ranger_BaseColor.png";

        private const string FemaleHeadMeshPath = CharacterArtFolder + "/Quaternius_Female_Head.asset";

        private const string MaleHeadMeshPath = CharacterArtFolder + "/Quaternius_Male_Head.asset";

        private const string MixamoErikaTrialBowMeshPath =
            CharacterArtFolder + "/MixamoErikaTrial_Bow.asset";

        private const string MixamoErikaTrialArrowMeshPath =
            CharacterArtFolder + "/MixamoErikaTrial_Arrow.asset";

        private const string BossModelPath =
            "Assets/ThirdParty/KayKit/Skeletons/Characters/Skeleton_Warrior.fbx";

        private const string DaggerPath =
            "Assets/ThirdParty/KayKit/Adventurers/Equipment/dagger.fbx";

        private const string SpellbookPath =
            "Assets/ThirdParty/KayKit/Adventurers/Equipment/spellbook_open.fbx";

        private const string StaffPath =
            "Assets/ThirdParty/KayKit/Adventurers/Equipment/staff.fbx";

        private const string WandPath =
            "Assets/ThirdParty/KayKit/Adventurers/Equipment/wand.fbx";

        private const string ProductFontPath =
            "Assets/ThirdParty/Fonts/AlegreyaSansSC/AlegreyaSansSC-Medium.ttf";

        private const string SkeletonAxePath =
            "Assets/ThirdParty/KayKit/Skeletons/Equipment/Skeleton_Axe.fbx";

        private const string SkeletonShieldPath =
            "Assets/ThirdParty/KayKit/Skeletons/Equipment/Skeleton_Shield_Large_A.fbx";

        private const int RingPointCount = 64;

        // The fixed strategy camera shows almost the full arena. The female and
        // male Quaternius sources have slightly different authored heights, so a
        // small class-family correction keeps all four playable silhouettes near
        // the same 130-140 px read at the close arena spawns. These values scale
        // only the direct render hierarchy: the combatant root, 2.35 m targeting
        // capsule, aim point, rings, and deterministic simulation stay at 1:1.
        private const float HealerCharacterVisualScale = 1.48f;

        private const float RogueCharacterVisualScale = 1.46f;

        private const float MageCharacterVisualScale = 1.48f;

        private const float HunterCharacterVisualScale = 1.46f;

        // Erika is authored at approximately 1.919 m already. Reusing the
        // Quaternius correction would make this reversible trial almost 2.8 m
        // tall and invalidate the locked-camera comparison.
        private const float MixamoErikaTrialVisualScale = 1.0f;

        // Screen-facing status presentation remains a sibling of the scaled
        // character hierarchy. Raising it independently preserves a clear strip
        // above the enlarged head without moving targeting or effects authority.
        private const float WorldStatusHeight = 2.98f;

        private const float RelationMarkerHeight = 3.36f;

        private const float HumanMarkerHeight = 3.38f;

        // The companion is intentionally low and subordinate to a combatant, but
        // needs enough projected mass to read as a creature instead of spell
        // debris in the fixed strategy camera. This scales only the authored
        // render hierarchy; companion simulation and targeting stay untouched.
        private const float HunterCompanionVisualScale = 1.28f;

        private static MixamoPlayableClassTrialSpec ResolveSelectedPlayableClassTrial(
            ClassId classId)
        {
            if (!PlayableClassTrialSourceSelection.UsesMixamoTrial(classId))
            {
                return null;
            }

            return MixamoPlayableClassTrialAssetValidator.GetSpecOrThrow(classId);
        }

        private static string ResolveHunterModelPath()
        {
            switch (HunterCharacterSourceSelection.Selected)
            {
                case HunterCharacterSource.ProductionV1:
                    return HunterProductionAssetValidator.ModelAssetPath;
                case HunterCharacterSource.MixamoErikaTrial:
                    return MixamoErikaTrialAssetValidator.ModelAssetPath;
                default:
                    return QuaterniusAssetValidator.RogueModelPath;
            }
        }

        private static string ResolveHunterModelInstanceName()
        {
            switch (HunterCharacterSourceSelection.Selected)
            {
                case HunterCharacterSource.ProductionV1:
                    return "HunterModel_ProductionV1";
                case HunterCharacterSource.MixamoErikaTrial:
                    return "HunterModel_MixamoErikaTrial";
                default:
                    return "HunterModel_Quaternius";
            }
        }

        private static float ResolveHunterVisualScale()
        {
            return HunterCharacterSourceSelection.UsesMixamoErikaTrial
                ? MixamoErikaTrialVisualScale
                : HunterCharacterVisualScale;
        }

        internal static GameObject BuildCombatantPrefab(
            Material teamRingMaterial,
            Material targetMaterial,
            Material humanMaterial,
            Material healthMaterial,
            Material barrierMaterial,
            Material castMaterial,
            Material contactShadowMaterial,
            Material auraEffectMaterial,
            CombatantAnimationControllers animationControllers)
        {
            RequireMaterial(teamRingMaterial, nameof(teamRingMaterial));
            RequireMaterial(targetMaterial, nameof(targetMaterial));
            RequireMaterial(humanMaterial, nameof(humanMaterial));
            RequireMaterial(healthMaterial, nameof(healthMaterial));
            RequireMaterial(barrierMaterial, nameof(barrierMaterial));
            RequireMaterial(castMaterial, nameof(castMaterial));
            RequireMaterial(contactShadowMaterial, nameof(contactShadowMaterial));
            RequireMaterial(auraEffectMaterial, nameof(auraEffectMaterial));
            EnsurePrefabFolder();

            bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = prefabExists
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("Combatant");

            try
            {
                ResetRootTransform(root);
                ClearChildren(root.transform);

                SoftTargetCandidate targetCandidate = GetOrAddComponent<SoftTargetCandidate>(root);
                CapsuleCollider targetCollider = GetOrAddComponent<CapsuleCollider>(root);
                CombatantView combatantView = GetOrAddComponent<CombatantView>(root);
                CombatantAnimationPresenter animationPresenter =
                    GetOrAddComponent<CombatantAnimationPresenter>(root);
                AuraVisualPresenter auraVisualPresenter =
                    GetOrAddComponent<AuraVisualPresenter>(root);
                ConfigureTargetCollider(targetCollider);

                MixamoPlayableClassTrialSpec healerTrial =
                    ResolveSelectedPlayableClassTrial(ClassId.Healer);
                MixamoPlayableClassTrialSpec rogueTrial =
                    ResolveSelectedPlayableClassTrial(ClassId.Rogue);
                MixamoPlayableClassTrialSpec mageTrial =
                    ResolveSelectedPlayableClassTrial(ClassId.Mage);
                string healerModelPath = healerTrial != null
                    ? healerTrial.ModelAssetPath
                    : QuaterniusAssetValidator.HealerModelPath;
                string rogueModelPath = rogueTrial != null
                    ? rogueTrial.ModelAssetPath
                    : QuaterniusAssetValidator.RogueModelPath;
                string mageModelPath = mageTrial != null
                    ? mageTrial.ModelAssetPath
                    : QuaterniusAssetValidator.MageModelPath;
                string hunterModelPath = ResolveHunterModelPath();
                string hunterModelInstanceName = ResolveHunterModelInstanceName();
                float hunterVisualScale = ResolveHunterVisualScale();

                GameObject healerModel = InstantiateModel(
                    healerModelPath,
                    healerTrial != null
                        ? healerTrial.ModelRootName
                        : "PriestModel_Quaternius",
                    root.transform,
                    new Vector3(
                        0f,
                        healerTrial != null ? healerTrial.GroundOffset : 0.03f,
                        0f),
                    Quaternion.identity,
                    Vector3.one * (healerTrial != null
                        ? healerTrial.VisualScale
                        : HealerCharacterVisualScale));
                GameObject rogueModel = InstantiateModel(
                    rogueModelPath,
                    rogueTrial != null
                        ? rogueTrial.ModelRootName
                        : "RogueModel_Quaternius",
                    root.transform,
                    new Vector3(
                        0f,
                        rogueTrial != null ? rogueTrial.GroundOffset : 0.03f,
                        0f),
                    Quaternion.identity,
                    Vector3.one * (rogueTrial != null
                        ? rogueTrial.VisualScale
                        : RogueCharacterVisualScale));
                GameObject mageModel = InstantiateModel(
                    mageModelPath,
                    mageTrial != null
                        ? mageTrial.ModelRootName
                        : "MageModel_Quaternius",
                    root.transform,
                    new Vector3(
                        0f,
                        mageTrial != null ? mageTrial.GroundOffset : 0.03f,
                        0f),
                    Quaternion.identity,
                    Vector3.one * (mageTrial != null
                        ? mageTrial.VisualScale
                        : MageCharacterVisualScale));
                GameObject hunterModel = InstantiateModel(
                    hunterModelPath,
                    hunterModelInstanceName,
                    root.transform,
                    new Vector3(0f, 0.03f, 0f),
                    Quaternion.identity,
                    Vector3.one * hunterVisualScale);
                GameObject bossModel = InstantiateModel(
                    BossModelPath,
                    "BossModel_SkeletonWarrior",
                    root.transform,
                    Vector3.zero,
                    Quaternion.identity,
                    new Vector3(1.12f, 1.02f, 1.12f));

                Animator healerAnimator = ConfigureAnimator(
                    healerModel,
                    healerModelPath,
                    animationControllers.Healer,
                    true);
                Animator rogueAnimator = ConfigureAnimator(
                    rogueModel,
                    rogueModelPath,
                    animationControllers.Rogue,
                    true);
                Animator mageAnimator = ConfigureAnimator(
                    mageModel,
                    mageModelPath,
                    animationControllers.Mage,
                    true);
                Animator hunterAnimator = ConfigureAnimator(
                    hunterModel,
                    hunterModelPath,
                    animationControllers.Mage,
                    true);
                Animator bossAnimator = ConfigureAnimator(
                    bossModel,
                    BossModelPath,
                    animationControllers.Boss,
                    false);

                if (healerTrial != null)
                {
                    AuthorMixamoPlayableClassTrialCharacter(healerModel, healerTrial);
                }
                else
                {
                    AuthorQuaterniusCharacter(
                        healerModel,
                        healerAnimator,
                        QuaterniusAssetValidator.HealerOutfitPath,
                        FemaleHeadMeshPath,
                        "Priest",
                        "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Peasant_BaseColor.png",
                        "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Peasant_Normal.png",
                        new Color(1f, 0.94f, 0.74f, 1f));
                }

                if (rogueTrial != null)
                {
                    AuthorMixamoPlayableClassTrialCharacter(rogueModel, rogueTrial);
                }
                else
                {
                    AuthorQuaterniusCharacter(
                        rogueModel,
                        rogueAnimator,
                        QuaterniusAssetValidator.RogueOutfitPath,
                        MaleHeadMeshPath,
                        "Rogue",
                        "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Ranger_BaseColor.png",
                        "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Ranger_Normal.png",
                        new Color(0.82f, 0.90f, 0.78f, 1f));
                }

                if (mageTrial != null)
                {
                    AuthorMixamoPlayableClassTrialCharacter(mageModel, mageTrial);
                }
                else
                {
                    AuthorQuaterniusCharacter(
                        mageModel,
                        mageAnimator,
                        QuaterniusAssetValidator.MageOutfitPath,
                        FemaleHeadMeshPath,
                        "Mage",
                        "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Ranger_3_BaseColor.png",
                        "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Ranger_Normal.png",
                        new Color(0.68f, 0.82f, 1f, 1f));
                }
                if (HunterCharacterSourceSelection.UsesProductionV1)
                {
                    AuthorProductionHunter(hunterModel);
                }
                else if (HunterCharacterSourceSelection.UsesMixamoErikaTrial)
                {
                    AuthorMixamoErikaTrialCharacter(hunterModel, hunterAnimator);
                }
                else
                {
                    AuthorQuaterniusCharacter(
                        hunterModel,
                        hunterAnimator,
                        QuaterniusAssetValidator.RogueOutfitPath,
                        MaleHeadMeshPath,
                        "Hunter",
                        UseFirstPartyHunterOutfitAlbedo
                            ? HunterOutfitAlbedoValidator.SourceAssetPath
                            : HunterFallbackOutfitAlbedoPath,
                        "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Ranger_Normal.png",
                        // Preserve the source atlas's authored leather, hardware,
                        // and cloth breakup without a second whole-outfit color cast.
                        Color.white);
                }

                Material priestAccentMaterial = EnsureClassAccentMaterial(
                    CharacterArtFolder + "/Priest_ArmorAccent.mat",
                    new Color(1f, 0.72f, 0.20f, 1f),
                    new Color(0.28f, 0.13f, 0.02f, 1f),
                    0.58f,
                    0.62f);
                Material rogueAccentMaterial = EnsureClassAccentMaterial(
                    CharacterArtFolder + "/Rogue_ArmorAccent.mat",
                    new Color(0.58f, 0.13f, 0.74f, 1f),
                    new Color(0.12f, 0.02f, 0.18f, 1f),
                    0.48f,
                    0.46f);
                Material mageAccentMaterial = EnsureClassAccentMaterial(
                    CharacterArtFolder + "/Mage_ArmorAccent.mat",
                    new Color(0.08f, 0.62f, 1f, 1f),
                    new Color(0.02f, 0.20f, 0.42f, 1f),
                    0.34f,
                    0.68f);
                Material hunterAccentMaterial = EnsureClassAccentMaterial(
                    CharacterArtFolder + "/Hunter_ArmorAccent.mat",
                    new Color(0.28f, 0.52f, 0.10f, 1f),
                    Color.clear,
                    0.12f,
                    0.30f);
                Material hunterWeaponMaterial = EnsureClassAccentMaterial(
                    CharacterArtFolder + "/Hunter_WeaponBronze.mat",
                    new Color(0.96f, 0.68f, 0.18f, 1f),
                    Color.clear,
                    0.52f,
                    0.42f);
                Material hunterLeatherMaterial = EnsureClassAccentMaterial(
                    CharacterArtFolder + "/Hunter_Leather.mat",
                    new Color(0.16f, 0.065f, 0.024f, 1f),
                    Color.clear,
                    0.04f,
                    0.22f);
                Material hunterCompanionBodyMaterial = EnsureClassAccentMaterial(
                    CharacterArtFolder + "/HunterCompanion_Body.mat",
                    new Color(0.026f, 0.044f, 0.052f, 1f),
                    Color.clear,
                    0.08f,
                    0.24f);
                Material hunterCompanionAccentMaterial = EnsureClassAccentMaterial(
                    CharacterArtFolder + "/HunterCompanion_Accent.mat",
                    new Color(0.06f, 0.58f, 0.68f, 1f),
                    new Color(0.015f, 0.19f, 0.24f, 1f),
                    0.12f,
                    0.46f);

                if (healerTrial == null)
                {
                    AuthorPriestSilhouette(healerAnimator, priestAccentMaterial);
                }

                if (rogueTrial == null)
                {
                    AuthorRogueSilhouette(rogueAnimator, rogueAccentMaterial);
                }

                if (mageTrial == null)
                {
                    AuthorMageSilhouette(mageAnimator, mageAccentMaterial);
                }
                if (HunterCharacterSourceSelection.UsesQuaterniusPrototype)
                {
                    AuthorHunterSilhouette(
                        hunterAnimator,
                        hunterAccentMaterial,
                        hunterWeaponMaterial,
                        hunterLeatherMaterial);
                }
                AuthorBossSilhouette(bossModel, targetMaterial);

                healerModel.SetActive(true);
                rogueModel.SetActive(false);
                mageModel.SetActive(false);
                hunterModel.SetActive(false);
                bossModel.SetActive(false);

                CreatePrimitiveVisual(
                    "ContactShadow",
                    PrimitiveType.Cylinder,
                    root.transform,
                    new Vector3(0f, 0.014f, 0f),
                    Quaternion.identity,
                    new Vector3(0.92f, 0.012f, 0.68f),
                    contactShadowMaterial);

                Transform aimPoint = CreateTransform(
                    "AimPoint",
                    root.transform,
                    new Vector3(0f, 1.45f, 0f));

                LineRenderer teamRing = CreateRing(
                    "TeamRing",
                    root.transform,
                    0.56f,
                    0.045f,
                    0.025f,
                    teamRingMaterial);
                LineRenderer targetRingRenderer = CreateRing(
                    "TargetRing",
                    root.transform,
                    0.72f,
                    0.075f,
                    0.04f,
                    targetMaterial);
                GameObject targetRing = targetRingRenderer.gameObject;
                targetRing.SetActive(false);

                GameObject humanMarker = CreateHumanMarker(root.transform, humanMaterial);
                humanMarker.SetActive(false);
                CombatantRelationMarker relationMarker = CreateRelationMarker(
                    root.transform,
                    humanMaterial,
                    contactShadowMaterial);

                CreateWorldBars(
                    root.transform,
                    healthMaterial,
                    barrierMaterial,
                    castMaterial,
                    contactShadowMaterial,
                    out Transform healthFill,
                    out Transform barrierFill,
                    out Transform castFill);
                auraVisualPresenter = AuraVisualAuthoring.Build(root, auraEffectMaterial);
                HunterCompanionView companionView = CreateHunterCompanion(
                    root.transform,
                    hunterCompanionBodyMaterial,
                    hunterCompanionAccentMaterial,
                    healthMaterial,
                    contactShadowMaterial);

                ConfigureTargetCandidate(targetCandidate, aimPoint);
                ConfigureCombatantView(
                    combatantView,
                    healerModel,
                    rogueModel,
                    mageModel,
                    hunterModel,
                    bossModel,
                    teamRing,
                    targetRing,
                    humanMarker,
                    relationMarker,
                    healthFill,
                    barrierFill,
                    castFill,
                    targetCollider,
                    aimPoint,
                    animationPresenter,
                    auraVisualPresenter,
                    companionView);
                ConfigureAnimationPresenter(
                    animationPresenter,
                    healerAnimator,
                    rogueAnimator,
                    mageAnimator,
                    hunterAnimator,
                    bossAnimator);
                ConfigureAnimatorIkRelay(healerAnimator, animationPresenter);
                ConfigureAnimatorIkRelay(rogueAnimator, animationPresenter);
                ConfigureAnimatorIkRelay(mageAnimator, animationPresenter);
                ConfigureAnimatorIkRelay(hunterAnimator, animationPresenter);

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save the authored combatant prefab at '{PrefabPath}'.");
                }
            }
            finally
            {
                if (prefabExists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"The authored combatant prefab could not be loaded from '{PrefabPath}'.");
            }

            return prefab;
        }

        private static void ConfigureCombatantView(
            CombatantView combatantView,
            GameObject healerModel,
            GameObject rogueModel,
            GameObject mageModel,
            GameObject hunterModel,
            GameObject bossModel,
            Renderer teamRing,
            GameObject targetRing,
            GameObject humanMarker,
            CombatantRelationMarker relationMarker,
            Transform healthFill,
            Transform barrierFill,
            Transform castFill,
            Collider targetCollider,
            Transform aimPoint,
            CombatantAnimationPresenter animationPresenter,
            AuraVisualPresenter auraVisualPresenter,
            HunterCompanionView companionView)
        {
            SerializedObject serializedView = new SerializedObject(combatantView);
            serializedView.Update();
            SetObjectReference(serializedView, "_healerModel", healerModel);
            SetObjectReference(serializedView, "_rogueModel", rogueModel);
            SetObjectReference(serializedView, "_mageModel", mageModel);
            SetObjectReference(serializedView, "_hunterModel", hunterModel);
            SetObjectReference(serializedView, "_bossModel", bossModel);
            SetObjectReference(serializedView, "_teamRing", teamRing);
            SetObjectReference(serializedView, "_targetRing", targetRing);
            SetObjectReference(serializedView, "_humanMarker", humanMarker);
            SetObjectReference(serializedView, "_relationMarker", relationMarker);
            SetObjectReference(serializedView, "_healthFill", healthFill);
            SetObjectReference(serializedView, "_barrierFill", barrierFill);
            SetObjectReference(serializedView, "_castFill", castFill);
            SetObjectReference(serializedView, "_targetCollider", targetCollider);
            SetObjectReference(serializedView, "_aimPoint", aimPoint);
            SetObjectReference(serializedView, "_animationPresenter", animationPresenter);
            SetObjectReference(serializedView, "_auraVisualPresenter", auraVisualPresenter);
            SetObjectReference(serializedView, "_companionView", companionView);
            RequireProperty(serializedView, "_groundLayers").intValue = 1 << 10;
            RequireProperty(serializedView, "_positionSharpness").floatValue = 24f;
            RequireProperty(serializedView, "_rotationSharpness").floatValue = 18f;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
        }

        private static HunterCompanionView CreateHunterCompanion(
            Transform parent,
            Material bodyMaterial,
            Material accentMaterial,
            Material healthMaterial,
            Material shadowMaterial)
        {
            GameObject root = new GameObject("HunterCompanion");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-1.25f, 0.04f, -0.9f);
            HunterCompanionView view = root.AddComponent<HunterCompanionView>();

            GameObject visualRoot = new GameObject("SpectralSpider");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.localScale = Vector3.one * HunterCompanionVisualScale;
            var bodyRenderers = new List<Renderer>(19);
            var accentRenderers = new List<Renderer>(12);
            AddCompanionPart("Abdomen", PrimitiveType.Sphere, visualRoot.transform,
                new Vector3(0f, 0.42f, -0.28f), Quaternion.identity,
                new Vector3(0.44f, 0.34f, 0.58f), bodyMaterial, bodyRenderers);
            AddCompanionPart("Thorax", PrimitiveType.Sphere, visualRoot.transform,
                new Vector3(0f, 0.4f, 0.18f), Quaternion.identity,
                new Vector3(0.37f, 0.31f, 0.4f), bodyMaterial, bodyRenderers);
            AddCompanionPart("Head", PrimitiveType.Sphere, visualRoot.transform,
                new Vector3(0f, 0.42f, 0.5f), Quaternion.identity,
                new Vector3(0.26f, 0.22f, 0.27f), bodyMaterial, bodyRenderers);
            AddCompanionPart("FangLeft", PrimitiveType.Capsule, visualRoot.transform,
                new Vector3(-0.085f, 0.31f, 0.67f), Quaternion.Euler(18f, 0f, 12f),
                new Vector3(0.04f, 0.12f, 0.04f), accentMaterial, accentRenderers);
            AddCompanionPart("FangRight", PrimitiveType.Capsule, visualRoot.transform,
                new Vector3(0.085f, 0.31f, 0.67f), Quaternion.Euler(18f, 0f, -12f),
                new Vector3(0.04f, 0.12f, 0.04f), accentMaterial, accentRenderers);
            AddCompanionPart("AbdomenSigil", PrimitiveType.Sphere, visualRoot.transform,
                new Vector3(0f, 0.585f, -0.3f), Quaternion.identity,
                new Vector3(0.24f, 0.03f, 0.30f), accentMaterial, accentRenderers);
            AddCompanionPart("ThoraxSigil", PrimitiveType.Sphere, visualRoot.transform,
                new Vector3(0f, 0.55f, 0.17f), Quaternion.identity,
                new Vector3(0.19f, 0.03f, 0.21f), accentMaterial, accentRenderers);
            AddCompanionPart("OwnerBandLeft", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(-0.16f, 0.57f, -0.27f), Quaternion.Euler(0f, -24f, 0f),
                new Vector3(0.09f, 0.035f, 0.38f), accentMaterial, accentRenderers);
            AddCompanionPart("OwnerBandRight", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0.16f, 0.57f, -0.27f), Quaternion.Euler(0f, 24f, 0f),
                new Vector3(0.09f, 0.035f, 0.38f), accentMaterial, accentRenderers);
            for (int eye = 0; eye < 4; eye++)
            {
                float side = eye < 2 ? -1f : 1f;
                float inset = (eye & 1) == 0 ? 0.045f : 0.105f;
                AddCompanionPart(
                    "Eye_" + eye,
                    PrimitiveType.Sphere,
                    visualRoot.transform,
                    new Vector3(side * inset, 0.49f, 0.625f),
                    Quaternion.identity,
                    Vector3.one * 0.038f,
                    accentMaterial,
                    accentRenderers);
            }
            var gaitLimbs = new List<Transform>(16);
            float[] legRows = { -0.28f, -0.08f, 0.12f, 0.29f };
            float[] legAngles = { -34f, -12f, 12f, 34f };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int leg = 0; leg < legRows.Length; leg++)
                {
                    float yaw = legAngles[leg] * side;
                    Vector3 outward = Quaternion.Euler(0f, yaw, 0f) *
                        new Vector3(side, 0f, 0.12f);
                    outward.Normalize();
                    Vector3 knee = new Vector3(
                        side * 0.41f,
                        0.32f,
                        legRows[leg] + (outward.z * 0.16f));
                    Vector3 foot = new Vector3(
                        side * 0.68f,
                        0.08f,
                        legRows[leg] + (outward.z * 0.3f));
                    gaitLimbs.Add(AddCompanionLimb(
                        "LegUpper_" + side + "_" + leg,
                        visualRoot.transform,
                        new Vector3(side * 0.2f, 0.4f, legRows[leg]),
                        knee,
                        0.053f,
                        bodyMaterial,
                        bodyRenderers));
                    gaitLimbs.Add(AddCompanionLimb(
                        "LegLower_" + side + "_" + leg,
                        visualRoot.transform,
                        knee,
                        foot,
                        0.043f,
                        bodyMaterial,
                        bodyRenderers));
                }
            }

            CreatePrimitiveVisual(
                "ContactShadow",
                PrimitiveType.Cylinder,
                visualRoot.transform,
                new Vector3(0f, 0.01f, 0f),
                Quaternion.identity,
                new Vector3(0.68f, 0.008f, 0.84f),
                shadowMaterial);
            GameObject orderMarkerObject = CreatePrimitiveVisual(
                "OrderMarker",
                PrimitiveType.Cube,
                visualRoot.transform,
                new Vector3(0f, 1.08f, 0f),
                Quaternion.Euler(0f, 45f, 0f),
                new Vector3(0.13f, 0.035f, 0.13f),
                accentMaterial);
            var orderRenderers = new List<Renderer>(11);
            GameObject attackOrderRoot = CreateCompanionOrderRoot(
                "AttackOrder",
                visualRoot.transform,
                new Vector3(0f, 1.25f, 0f));
            AddCompanionOrderPart("ArrowShaft", attackOrderRoot.transform,
                new Vector3(0f, 0f, 0.08f), Quaternion.identity,
                new Vector3(0.045f, 0.022f, 0.28f), accentMaterial, orderRenderers);
            AddCompanionOrderPart("ArrowHeadLeft", attackOrderRoot.transform,
                new Vector3(-0.1f, 0f, 0.32f), Quaternion.Euler(0f, -42f, 0f),
                new Vector3(0.045f, 0.022f, 0.2f), accentMaterial, orderRenderers);
            AddCompanionOrderPart("ArrowHeadRight", attackOrderRoot.transform,
                new Vector3(0.1f, 0f, 0.32f), Quaternion.Euler(0f, 42f, 0f),
                new Vector3(0.045f, 0.022f, 0.2f), accentMaterial, orderRenderers);

            GameObject followOrderRoot = CreateCompanionOrderRoot(
                "FollowOrder",
                visualRoot.transform,
                new Vector3(0f, 1.25f, 0f));
            for (int chevron = 0; chevron < 2; chevron++)
            {
                float z = -0.16f + chevron * 0.28f;
                AddCompanionOrderPart("FollowLeft_" + chevron, followOrderRoot.transform,
                    new Vector3(-0.1f, 0f, z), Quaternion.Euler(0f, -42f, 0f),
                    new Vector3(0.042f, 0.022f, 0.2f), accentMaterial, orderRenderers);
                AddCompanionOrderPart("FollowRight_" + chevron, followOrderRoot.transform,
                    new Vector3(0.1f, 0f, z), Quaternion.Euler(0f, 42f, 0f),
                    new Vector3(0.042f, 0.022f, 0.2f), accentMaterial, orderRenderers);
            }

            GameObject stayOrderRoot = CreateCompanionOrderRoot(
                "StayOrder",
                visualRoot.transform,
                new Vector3(0f, 1.25f, 0f));
            AddCompanionOrderPart("StayTop", stayOrderRoot.transform,
                new Vector3(0f, 0f, 0.25f), Quaternion.identity,
                new Vector3(0.42f, 0.022f, 0.042f), accentMaterial, orderRenderers);
            AddCompanionOrderPart("StayBottom", stayOrderRoot.transform,
                new Vector3(0f, 0f, -0.25f), Quaternion.identity,
                new Vector3(0.42f, 0.022f, 0.042f), accentMaterial, orderRenderers);
            AddCompanionOrderPart("StayLeft", stayOrderRoot.transform,
                new Vector3(-0.25f, 0f, 0f), Quaternion.identity,
                new Vector3(0.042f, 0.022f, 0.42f), accentMaterial, orderRenderers);
            AddCompanionOrderPart("StayRight", stayOrderRoot.transform,
                new Vector3(0.25f, 0f, 0f), Quaternion.identity,
                new Vector3(0.042f, 0.022f, 0.42f), accentMaterial, orderRenderers);
            attackOrderRoot.SetActive(false);
            followOrderRoot.SetActive(false);
            stayOrderRoot.SetActive(false);

            GameObject healthRoot = new GameObject("PetHealth");
            healthRoot.transform.SetParent(visualRoot.transform, false);
            healthRoot.transform.localPosition = new Vector3(0f, 0.97f, 0f);
            CreatePrimitiveVisual(
                "Track", PrimitiveType.Cube, healthRoot.transform,
                Vector3.zero, Quaternion.identity,
                new Vector3(0.58f, 0.05f, 0.03f), shadowMaterial);
            GameObject healthFill = CreatePrimitiveVisual(
                "Fill", PrimitiveType.Cube, healthRoot.transform,
                new Vector3(0f, 0f, -0.025f), Quaternion.identity,
                new Vector3(0.54f, 0.028f, 0.025f), healthMaterial);

            SerializedObject serialized = new SerializedObject(view);
            serialized.Update();
            SetObjectReference(serialized, "_visualRoot", visualRoot);
            SerializedProperty bodyRendererProperty = RequireProperty(
                serialized,
                "_bodyRenderers");
            bodyRendererProperty.arraySize = bodyRenderers.Count;
            for (int index = 0; index < bodyRenderers.Count; index++)
            {
                bodyRendererProperty.GetArrayElementAtIndex(index).objectReferenceValue =
                    bodyRenderers[index];
            }
            SerializedProperty renderers = RequireProperty(serialized, "_accentRenderers");
            renderers.arraySize = accentRenderers.Count;
            for (int index = 0; index < accentRenderers.Count; index++)
            {
                renderers.GetArrayElementAtIndex(index).objectReferenceValue = accentRenderers[index];
            }
            SetObjectReference(serialized, "_healthRoot", healthRoot.transform);
            SetObjectReference(serialized, "_healthFill", healthFill.transform);
            SerializedProperty gaitLimbProperty = RequireProperty(serialized, "_gaitLimbs");
            gaitLimbProperty.arraySize = gaitLimbs.Count;
            for (int index = 0; index < gaitLimbs.Count; index++)
            {
                gaitLimbProperty.GetArrayElementAtIndex(index).objectReferenceValue =
                    gaitLimbs[index];
            }
            SetObjectReference(serialized, "_orderMarker", orderMarkerObject.GetComponent<Renderer>());
            SetObjectReference(serialized, "_attackOrderRoot", attackOrderRoot);
            SetObjectReference(serialized, "_followOrderRoot", followOrderRoot);
            SetObjectReference(serialized, "_stayOrderRoot", stayOrderRoot);
            SerializedProperty orderRendererProperty = RequireProperty(
                serialized,
                "_orderRenderers");
            orderRendererProperty.arraySize = orderRenderers.Count;
            for (int index = 0; index < orderRenderers.Count; index++)
            {
                orderRendererProperty.GetArrayElementAtIndex(index).objectReferenceValue =
                    orderRenderers[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static GameObject CreateCompanionOrderRoot(
            string name,
            Transform parent,
            Vector3 position)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            return root;
        }

        private static void AddCompanionOrderPart(
            string name,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material material,
            List<Renderer> renderers)
        {
            GameObject part = CreatePrimitiveVisual(
                name,
                PrimitiveType.Cube,
                parent,
                position,
                rotation,
                scale,
                material);
            renderers.Add(part.GetComponent<Renderer>());
        }

        private static void AddCompanionPart(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material material,
            List<Renderer> renderers)
        {
            GameObject part = CreatePrimitiveVisual(
                name,
                type,
                parent,
                position,
                rotation,
                scale,
                material);
            Renderer renderer = part.GetComponent<Renderer>();
            ConfigureCharacterRenderer(renderer);
            renderers.Add(renderer);
        }

        private static Transform AddCompanionLimb(
            string name,
            Transform parent,
            Vector3 start,
            Vector3 end,
            float thickness,
            Material material,
            List<Renderer> renderers)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            GameObject limb = CreatePrimitiveVisual(
                name,
                PrimitiveType.Capsule,
                parent,
                (start + end) * 0.5f,
                Quaternion.FromToRotation(Vector3.up, direction.normalized),
                new Vector3(thickness, length * 0.5f, thickness),
                material);
            Renderer renderer = limb.GetComponent<Renderer>();
            ConfigureCharacterRenderer(renderer);
            renderers.Add(renderer);
            return limb.transform;
        }

        private static void ConfigureAnimationPresenter(
            CombatantAnimationPresenter presenter,
            Animator healerAnimator,
            Animator rogueAnimator,
            Animator mageAnimator,
            Animator hunterAnimator,
            Animator bossAnimator)
        {
            SerializedObject serialized = new SerializedObject(presenter);
            serialized.Update();
            SetObjectReference(serialized, "_healerAnimator", healerAnimator);
            SetObjectReference(serialized, "_rogueAnimator", rogueAnimator);
            SetObjectReference(serialized, "_mageAnimator", mageAnimator);
            SetObjectReference(serialized, "_hunterAnimator", hunterAnimator);
            SetObjectReference(serialized, "_bossAnimator", bossAnimator);
            SetObjectReference(
                serialized,
                "_hunterStaticString",
                FindProductionHunterRenderer(
                    hunterAnimator.gameObject,
                    HunterProductionAssetValidator.StaticStringRendererName,
                    false)?.gameObject);
            SetObjectReference(
                serialized,
                "_hunterDrawString",
                FindProductionHunterRenderer(
                    hunterAnimator.gameObject,
                    HunterProductionAssetValidator.DrawStringRendererName,
                    false)?.gameObject);
            SetObjectReference(
                serialized,
                "_hunterNockedArrow",
                HunterCharacterSourceSelection.UsesMixamoErikaTrial
                    ? FindDescendantGameObject(
                        hunterAnimator.gameObject,
                        "HunterArrow_Hand",
                        true)
                    : FindProductionHunterRenderer(
                        hunterAnimator.gameObject,
                        HunterProductionAssetValidator.NockedArrowRendererName,
                        false)?.gameObject);
            RequireProperty(serialized, "_locomotionDamping").floatValue = 0.08f;
            RequireProperty(serialized, "_maximumGroundedLift").floatValue = 0.018f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAnimatorIkRelay(
            Animator animator,
            CombatantAnimationPresenter presenter)
        {
            CombatantAnimatorIkRelay relay =
                GetOrAddComponent<CombatantAnimatorIkRelay>(animator.gameObject);
            relay.Bind(presenter);
        }

        private static void ConfigureTargetCandidate(
            SoftTargetCandidate targetCandidate,
            Transform aimPoint)
        {
            SerializedObject serializedCandidate = new SerializedObject(targetCandidate);
            serializedCandidate.Update();
            RequireProperty(serializedCandidate, "_targetId").intValue = 0;
            RequireProperty(serializedCandidate, "_teamId").intValue = 0;
            SetObjectReference(serializedCandidate, "_aimPoint", aimPoint);
            RequireProperty(serializedCandidate, "_isTargetable").boolValue = true;
            serializedCandidate.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTargetCollider(CapsuleCollider collider)
        {
            collider.center = new Vector3(0f, 1.175f, 0f);
            collider.radius = 0.72f;
            collider.height = 2.35f;
            collider.direction = 1;
            collider.isTrigger = true;
            collider.enabled = true;
        }

        private static void AuthorPriestSilhouette(
            Animator animator,
            Material accentMaterial)
        {
            Transform leftHand = RequireHumanoidBone(animator, HumanBodyBones.LeftHand);
            Transform rightHand = RequireHumanoidBone(animator, HumanBodyBones.RightHand);
            Transform chest = RequireHumanoidBone(animator, HumanBodyBones.Chest);
            Transform head = RequireHumanoidBone(animator, HumanBodyBones.Head);

            AttachEquipment(
                StaffPath,
                "PriestStaff_Hand",
                rightHand,
                new Vector3(0.02f, 0.02f, 0f),
                Quaternion.Euler(0f, 90f, 90f),
                Vector3.one * 1.08f);
            AttachEquipment(
                SpellbookPath,
                "PriestScripture_Hand",
                leftHand,
                new Vector3(0.02f, 0.01f, 0f),
                Quaternion.Euler(0f, 90f, 80f),
                Vector3.one * 0.72f);

            CreateArmorPrimitive(
                "PriestMantle_Left",
                PrimitiveType.Sphere,
                chest,
                new Vector3(-0.34f, 0.12f, -0.01f),
                Quaternion.Euler(0f, 0f, -12f),
                new Vector3(0.33f, 0.17f, 0.35f),
                accentMaterial);
            CreateArmorPrimitive(
                "PriestMantle_Right",
                PrimitiveType.Sphere,
                chest,
                new Vector3(0.34f, 0.12f, -0.01f),
                Quaternion.Euler(0f, 0f, 12f),
                new Vector3(0.33f, 0.17f, 0.35f),
                accentMaterial);
            CreateArmorPrimitive(
                "PriestReliquary",
                PrimitiveType.Cube,
                chest,
                new Vector3(0f, -0.01f, 0.24f),
                Quaternion.Euler(18f, 0f, 45f),
                new Vector3(0.16f, 0.22f, 0.09f),
                accentMaterial);
            CreateArmorPrimitive(
                "PriestCrown",
                PrimitiveType.Cube,
                head,
                new Vector3(0f, 0.25f, -0.015f),
                Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.13f, 0.13f, 0.034f),
                accentMaterial);
        }

        private static void AuthorMageSilhouette(
            Animator animator,
            Material accentMaterial)
        {
            Transform leftHand = RequireHumanoidBone(animator, HumanBodyBones.LeftHand);
            Transform rightHand = RequireHumanoidBone(animator, HumanBodyBones.RightHand);
            Transform chest = RequireHumanoidBone(animator, HumanBodyBones.Chest);

            AttachEquipment(
                StaffPath,
                "MageStaff_Hand",
                rightHand,
                new Vector3(0.02f, 0.02f, 0f),
                Quaternion.Euler(0f, 90f, 90f),
                Vector3.one * 1.18f);
            AttachEquipment(
                WandPath,
                "MageWand_Offhand",
                leftHand,
                new Vector3(0.02f, 0.01f, 0f),
                Quaternion.Euler(0f, 90f, 90f),
                Vector3.one * 1.02f);

            CreateArmorPrimitive(
                "MageCrystal_Left",
                PrimitiveType.Cube,
                chest,
                new Vector3(-0.33f, 0.21f, 0f),
                Quaternion.Euler(12f, 20f, -43f),
                new Vector3(0.13f, 0.39f, 0.13f),
                accentMaterial);
            CreateArmorPrimitive(
                "MageCrystal_Right",
                PrimitiveType.Cube,
                chest,
                new Vector3(0.33f, 0.21f, 0f),
                Quaternion.Euler(-12f, -20f, 43f),
                new Vector3(0.13f, 0.39f, 0.13f),
                accentMaterial);
            CreateArmorPrimitive(
                "MageFocusCrystal",
                PrimitiveType.Cube,
                chest,
                new Vector3(0f, 0.025f, 0.23f),
                Quaternion.Euler(18f, 0f, 45f),
                new Vector3(0.17f, 0.24f, 0.12f),
                accentMaterial);
        }

        private static void AuthorRogueSilhouette(
            Animator animator,
            Material accentMaterial)
        {
            Transform leftHand = RequireHumanoidBone(animator, HumanBodyBones.LeftHand);
            Transform rightHand = RequireHumanoidBone(animator, HumanBodyBones.RightHand);
            Transform chest = RequireHumanoidBone(animator, HumanBodyBones.Chest);

            AttachEquipment(
                DaggerPath,
                "RogueDagger_LeftHand",
                leftHand,
                new Vector3(0.03f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 90f),
                Vector3.one * 0.82f);
            AttachEquipment(
                DaggerPath,
                "RogueDagger_RightHand",
                rightHand,
                new Vector3(0.03f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 90f),
                Vector3.one * 0.82f);

            CreateArmorPrimitive(
                "RoguePauldron_Left",
                PrimitiveType.Sphere,
                chest,
                new Vector3(-0.32f, 0.11f, -0.015f),
                Quaternion.Euler(0f, 0f, -12f),
                new Vector3(0.29f, 0.14f, 0.31f),
                accentMaterial);
            CreateArmorPrimitive(
                "RoguePauldron_Right",
                PrimitiveType.Sphere,
                chest,
                new Vector3(0.32f, 0.11f, -0.015f),
                Quaternion.Euler(0f, 0f, 12f),
                new Vector3(0.29f, 0.14f, 0.31f),
                accentMaterial);
            CreateArmorPrimitive(
                "RogueShoulderBlade_Left",
                PrimitiveType.Capsule,
                chest,
                new Vector3(-0.40f, 0.25f, -0.02f),
                Quaternion.Euler(6f, 0f, -54f),
                new Vector3(0.085f, 0.29f, 0.085f),
                accentMaterial);
            CreateArmorPrimitive(
                "RogueShoulderBlade_Right",
                PrimitiveType.Capsule,
                chest,
                new Vector3(0.40f, 0.25f, -0.02f),
                Quaternion.Euler(-6f, 0f, 54f),
                new Vector3(0.085f, 0.29f, 0.085f),
                accentMaterial);
        }

        private static void AuthorProductionHunter(GameObject model)
        {
            HunterProductionAssetValidator.ValidateSelectedSourceOrThrow();
            Material material = EnsureProductionHunterMaterial();
            Renderer lod0 = FindProductionHunterRenderer(
                model,
                HunterProductionAssetValidator.Lod0RendererName,
                true);
            Renderer lod1 = FindProductionHunterRenderer(
                model,
                HunterProductionAssetValidator.Lod1RendererName,
                true);
            Renderer lod2 = FindProductionHunterRenderer(
                model,
                HunterProductionAssetValidator.Lod2RendererName,
                true);
            Renderer staticString = FindProductionHunterRenderer(
                model,
                HunterProductionAssetValidator.StaticStringRendererName,
                true);
            Renderer drawString = FindProductionHunterRenderer(
                model,
                HunterProductionAssetValidator.DrawStringRendererName,
                true);
            Renderer nockedArrow = FindProductionHunterRenderer(
                model,
                HunterProductionAssetValidator.NockedArrowRendererName,
                true);
            Renderer[] renderers =
            {
                lod0,
                lod1,
                lod2,
                staticString,
                drawString,
                nockedArrow,
            };
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].sharedMaterial = material;
                ConfigureCharacterRenderer(renderers[index]);
                if (renderers[index] is SkinnedMeshRenderer skinnedRenderer)
                {
                    // The shipped Android quality assets currently permit a
                    // two-bone global default. Production deformation is authored
                    // and validated at four normalized influences, so lock the
                    // visual renderer instead of silently losing those weights.
                    skinnedRenderer.quality = SkinQuality.Bone4;
                }
            }

            LODGroup lodGroup = GetOrAddComponent<LODGroup>(model);
            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.animateCrossFading = false;
            lodGroup.SetLODs(new[]
            {
                // The locked strategy framing projects the accepted Hunter at
                // roughly 0.115 of 1080p height. Keep that normal gameplay read
                // on LOD0, then step down without crossfade double draws.
                new LOD(0.10f, new[] { lod0 }),
                new LOD(0.055f, new[] { lod1 }),
                new LOD(0.02f, new[] { lod2 }),
            });
            lodGroup.RecalculateBounds();

            HunterAuxiliaryLodCuller auxiliaryLodCuller =
                GetOrAddComponent<HunterAuxiliaryLodCuller>(model);
            SerializedObject serializedCuller = new SerializedObject(
                auxiliaryLodCuller);
            serializedCuller.Update();
            SetObjectReference(serializedCuller, "_lodGroup", lodGroup);
            SerializedProperty auxiliaryRenderers = RequireProperty(
                serializedCuller,
                "_auxiliaryRenderers");
            auxiliaryRenderers.arraySize = 3;
            auxiliaryRenderers.GetArrayElementAtIndex(0).objectReferenceValue =
                staticString;
            auxiliaryRenderers.GetArrayElementAtIndex(1).objectReferenceValue =
                drawString;
            auxiliaryRenderers.GetArrayElementAtIndex(2).objectReferenceValue =
                nockedArrow;
            RequireProperty(
                serializedCuller,
                "_cullScreenRelativeHeight").floatValue = 0.02f;
            serializedCuller.ApplyModifiedPropertiesWithoutUndo();

            lod0.gameObject.SetActive(true);
            lod1.gameObject.SetActive(true);
            lod2.gameObject.SetActive(true);
            staticString.gameObject.SetActive(true);
            drawString.gameObject.SetActive(false);
            nockedArrow.gameObject.SetActive(false);
        }

        private static Renderer FindProductionHunterRenderer(
            GameObject model,
            string rendererName,
            bool required)
        {
            if (!HunterCharacterSourceSelection.UsesProductionV1)
            {
                return null;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Renderer match = renderers.SingleOrDefault(value =>
                string.Equals(value.name, rendererName, StringComparison.Ordinal));
            if (match == null && required)
            {
                throw new InvalidOperationException(
                    "Production Hunter instance '" + model.name +
                    "' has no required renderer '" + rendererName + "'.");
            }

            return match;
        }

        private static GameObject FindDescendantGameObject(
            GameObject root,
            string objectName,
            bool required)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            Transform match = transforms.SingleOrDefault(value =>
                string.Equals(value.name, objectName, StringComparison.Ordinal));
            if (match == null && required)
            {
                throw new InvalidOperationException(
                    "Character instance '" + root.name +
                    "' has no required object '" + objectName + "'.");
            }

            return match != null ? match.gameObject : null;
        }

        private static void AuthorMixamoPlayableClassTrialCharacter(
            GameObject model,
            MixamoPlayableClassTrialSpec spec)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            EnsureCharacterArtFolder();
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (spec.Renderers.Count == 0 || spec.Materials.Count == 0)
            {
                AuthorMixamoPlayableClassTrialFromImportedBindings(
                    renderers,
                    spec);
                return;
            }

            if (spec.Renderers.Count != spec.ExpectedRendererCount)
            {
                throw new InvalidOperationException(
                    "Mixamo playable-class trial '" + spec.DisplayName +
                    "' contains " + spec.Renderers.Count +
                    " authored visible renderer contracts; expected exactly " +
                    spec.ExpectedRendererCount + ".");
            }

            var rendererByName = renderers.ToDictionary(
                value => value.name,
                StringComparer.Ordinal);
            for (int rendererIndex = 0;
                 rendererIndex < spec.Renderers.Count;
                 rendererIndex++)
            {
                MixamoTrialRendererSpec rendererSpec = spec.Renderers[rendererIndex];
                if (!rendererByName.TryGetValue(
                        rendererSpec.RendererName,
                        out Renderer renderer))
                {
                    throw new InvalidOperationException(
                        "Mixamo playable-class trial '" + spec.DisplayName +
                        "' is missing renderer '" + rendererSpec.RendererName + "'.");
                }

                Material[] sourceMaterials = renderer.sharedMaterials;
                if (sourceMaterials.Length != rendererSpec.MaterialKeysBySlot.Count)
                {
                    throw new InvalidOperationException(
                        "Mixamo playable-class trial renderer '" + renderer.name +
                        "' exposes " + sourceMaterials.Length +
                        " material slots; expected " +
                        rendererSpec.MaterialKeysBySlot.Count + ".");
                }

                Material[] authoredMaterials = new Material[sourceMaterials.Length];
                for (int materialIndex = 0;
                     materialIndex < sourceMaterials.Length;
                     materialIndex++)
                {
                    MixamoTrialMaterialSpec materialSpec = spec.GetMaterialOrThrow(
                        rendererSpec.MaterialKeysBySlot[materialIndex]);
                    Material sourceMaterial = sourceMaterials[materialIndex];
                    if (sourceMaterial == null || !string.Equals(
                            sourceMaterial.name,
                            materialSpec.SourceMaterialName,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Mixamo playable-class trial renderer '" + renderer.name +
                            "' slot " + materialIndex + " uses source material '" +
                            (sourceMaterial != null ? sourceMaterial.name : "<null>") +
                            "'; expected '" + materialSpec.SourceMaterialName + "'.");
                    }

                    authoredMaterials[materialIndex] =
                        EnsureMixamoPlayableClassTrialMaterial(materialSpec);
                }

                renderer.sharedMaterials = authoredMaterials;
                ConfigureCharacterRenderer(renderer);
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    skinnedRenderer.quality = SkinQuality.Bone4;
                }
            }

            var contractedRendererNames = new HashSet<string>(
                spec.Renderers.Select(value => value.RendererName),
                StringComparer.Ordinal);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (contractedRendererNames.Contains(renderer.name))
                {
                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (renderer is not MeshRenderer ||
                    filter == null ||
                    filter.sharedMesh == null ||
                    filter.sharedMesh.vertexCount != 0)
                {
                    throw new InvalidOperationException(
                        "Mixamo playable-class trial '" + spec.DisplayName +
                        "' contains unexpected renderer '" + renderer.name + "'.");
                }

                renderer.enabled = false;
            }
        }

        private static void AuthorMixamoPlayableClassTrialFromImportedBindings(
            IReadOnlyList<Renderer> renderers,
            MixamoPlayableClassTrialSpec spec)
        {
            if (renderers.Count == 0)
            {
                throw new InvalidOperationException(
                    "Mixamo playable-class trial '" + spec.DisplayName +
                    "' contains no imported renderers.");
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] sourceMaterials = renderer.sharedMaterials;
                if (sourceMaterials.Length == 0 || sourceMaterials.Any(value => value == null))
                {
                    throw new InvalidOperationException(
                        "Mixamo playable-class trial renderer '" + renderer.name +
                        "' has an incomplete imported material binding.");
                }

                Material[] authoredMaterials = new Material[sourceMaterials.Length];
                for (int slot = 0; slot < sourceMaterials.Length; slot++)
                {
                    string assetPath = CharacterArtFolder + "/" +
                        spec.ModelRootName + "_Inspection_" +
                        rendererIndex.ToString("00") + "_" +
                        slot.ToString("00") + ".mat";
                    authoredMaterials[slot] =
                        EnsureMixamoPlayableClassTrialFallbackMaterial(
                            assetPath,
                            sourceMaterials[slot]);
                }

                renderer.sharedMaterials = authoredMaterials;
                ConfigureCharacterRenderer(renderer);
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    skinnedRenderer.quality = SkinQuality.Bone4;
                }
            }
        }

        private static void AuthorMixamoErikaTrialCharacter(
            GameObject model,
            Animator animator)
        {
            MixamoErikaTrialAssetValidator.ValidateSelectedSourceOrThrow();
            EnsureCharacterArtFolder();
            Material clothesMaterial = EnsureMixamoErikaTrialMaterial(
                CharacterArtFolder + "/MixamoErikaTrial_Clothes.mat",
                MixamoErikaTrialAssetValidator.ClothesBaseColorAssetPath,
                MixamoErikaTrialAssetValidator.ClothesNormalAssetPath,
                Color.white,
                0.34f,
                false);
            Material bodyMaterial = EnsureMixamoErikaTrialMaterial(
                CharacterArtFolder + "/MixamoErikaTrial_Body.mat",
                MixamoErikaTrialAssetValidator.BodyBaseColorAssetPath,
                MixamoErikaTrialAssetValidator.BodyNormalAssetPath,
                Color.white,
                0.38f,
                false);
            Material eyesMaterial = EnsureMixamoErikaTrialMaterial(
                CharacterArtFolder + "/MixamoErikaTrial_Eyes.mat",
                null,
                null,
                new Color(0.035f, 0.022f, 0.018f, 1f),
                0.72f,
                false);
            Material eyelashesMaterial = EnsureMixamoErikaTrialMaterial(
                CharacterArtFolder + "/MixamoErikaTrial_Eyelashes.mat",
                MixamoErikaTrialAssetValidator.EyelashesBaseColorAssetPath,
                null,
                Color.white,
                0.18f,
                true);
            Material embeddedBowMaterial = EnsureMixamoErikaTrialMaterial(
                CharacterArtFolder + "/MixamoErikaTrial_EmbeddedBow.mat",
                MixamoErikaTrialAssetValidator.BowBaseColorAssetPath,
                MixamoErikaTrialAssetValidator.BowNormalAssetPath,
                Color.white,
                0.34f,
                false);
            Material embeddedArrowMaterial = EnsureMixamoErikaTrialMaterial(
                CharacterArtFolder + "/MixamoErikaTrial_EmbeddedArrow.mat",
                MixamoErikaTrialAssetValidator.ArrowBaseColorAssetPath,
                MixamoErikaTrialAssetValidator.ArrowNormalAssetPath,
                Color.white,
                0.28f,
                false);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != MixamoErikaTrialAssetValidator.ExpectedRendererCount)
            {
                throw new InvalidOperationException(
                    "Mixamo Erika trial instance has " + renderers.Length +
                    " renderers; expected exactly " +
                    MixamoErikaTrialAssetValidator.ExpectedRendererCount + ".");
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (string.Equals(
                        renderer.name,
                        MixamoErikaTrialAssetValidator.ClothesRendererName,
                        StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = clothesMaterial;
                }
                else if (string.Equals(
                    renderer.name,
                    MixamoErikaTrialAssetValidator.BodyRendererName,
                    StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = bodyMaterial;
                }
                else if (string.Equals(
                    renderer.name,
                    MixamoErikaTrialAssetValidator.EyesRendererName,
                    StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = eyesMaterial;
                }
                else if (string.Equals(
                    renderer.name,
                    MixamoErikaTrialAssetValidator.EyelashesRendererName,
                    StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = eyelashesMaterial;
                }
                else if (string.Equals(
                    renderer.name,
                    MixamoErikaTrialAssetValidator.EmbeddedBowRendererName,
                    StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = embeddedBowMaterial;
                }
                else if (string.Equals(
                    renderer.name,
                    MixamoErikaTrialAssetValidator.EmbeddedArrowRendererName,
                    StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = embeddedArrowMaterial;
                }

                ConfigureCharacterRenderer(renderer);
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    skinnedRenderer.quality = SkinQuality.Bone4;
                }

                bool rejectedEmbeddedWeapon =
                    string.Equals(
                        renderer.name,
                        MixamoErikaTrialAssetValidator.EmbeddedBowRendererName,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        renderer.name,
                        MixamoErikaTrialAssetValidator.EmbeddedArrowRendererName,
                        StringComparison.Ordinal);
                if (rejectedEmbeddedWeapon)
                {
                    renderer.gameObject.SetActive(false);
                }
            }

            AuthorMixamoErikaTrialWeapon(
                model,
                animator,
                embeddedBowMaterial,
                embeddedArrowMaterial);
        }

        private static void AuthorMixamoErikaTrialWeapon(
            GameObject model,
            Animator animator,
            Material bowMaterial,
            Material arrowMaterial)
        {
            Transform leftHand = RequireHumanoidBone(animator, HumanBodyBones.LeftHand);
            Transform rightHand = RequireHumanoidBone(animator, HumanBodyBones.RightHand);
            SkinnedMeshRenderer[] sourceRenderers = model
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer sourceBow = sourceRenderers.Single(value =>
                string.Equals(
                    value.name,
                    MixamoErikaTrialAssetValidator.EmbeddedBowRendererName,
                    StringComparison.Ordinal));
            SkinnedMeshRenderer sourceArrow = sourceRenderers.Single(value =>
                string.Equals(
                    value.name,
                    MixamoErikaTrialAssetValidator.EmbeddedArrowRendererName,
                    StringComparison.Ordinal));

            Vector3 bowGrip = sourceBow.transform.InverseTransformPoint(leftHand.position);
            Mesh bowMesh = EnsureMixamoErikaTrialRigidMesh(
                sourceBow,
                MixamoErikaTrialBowMeshPath,
                bowGrip);
            Mesh arrowMesh = EnsureMixamoErikaTrialRigidMesh(
                sourceArrow,
                MixamoErikaTrialArrowMeshPath,
                null);
            GameObject bow = CreateMixamoErikaTrialRigidProp(
                "HunterBow_Hand",
                leftHand,
                bowMesh,
                bowMaterial);
            GameObject arrow = CreateMixamoErikaTrialRigidProp(
                "HunterArrow_Hand",
                rightHand,
                arrowMesh,
                arrowMaterial);

            HunterTrialWeaponPose pose = GetOrAddComponent<HunterTrialWeaponPose>(model);
            pose.Bind(
                model.transform,
                leftHand,
                rightHand,
                bow.transform,
                arrow.transform);
            arrow.SetActive(false);
        }

        private static Mesh EnsureMixamoErikaTrialRigidMesh(
            SkinnedMeshRenderer source,
            string assetPath,
            Vector3? pivotOverride)
        {
            Mesh generated = new Mesh
            {
                name = System.IO.Path.GetFileNameWithoutExtension(assetPath),
            };
            source.BakeMesh(generated);
            Vector3 pivot = pivotOverride ?? generated.bounds.center;
            Vector3[] vertices = generated.vertices;
            for (int index = 0; index < vertices.Length; index++)
            {
                vertices[index] -= pivot;
            }

            generated.vertices = vertices;
            generated.RecalculateBounds();
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, asset);
            asset.name = generated.name;
            EditorUtility.SetDirty(asset);
            UnityEngine.Object.DestroyImmediate(generated);
            return asset;
        }

        private static GameObject CreateMixamoErikaTrialRigidProp(
            string name,
            Transform parent,
            Mesh mesh,
            Material material)
        {
            GameObject prop = new GameObject(name);
            prop.transform.SetParent(parent, false);
            MeshFilter filter = prop.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = prop.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureCharacterRenderer(renderer);
            return prop;
        }

        private static void AuthorHunterSilhouette(
            Animator animator,
            Material armorMaterial,
            Material weaponMaterial,
            Material leatherMaterial)
        {
            Transform chest = RequireHumanoidBone(animator, HumanBodyBones.Chest);
            AuthorHunterWeapon(animator, weaponMaterial, leatherMaterial);
            CreateArmorPrimitive(
                "HunterQuiver",
                PrimitiveType.Capsule,
                chest,
                new Vector3(0.35f, -0.14f, -0.29f),
                Quaternion.Euler(0f, 0f, -18f),
                new Vector3(0.14f, 0.43f, 0.14f),
                leatherMaterial);
            CreateArmorPrimitive(
                "HunterPauldron_Left",
                PrimitiveType.Sphere,
                chest,
                new Vector3(-0.34f, 0.12f, -0.015f),
                Quaternion.Euler(0f, 0f, -12f),
                new Vector3(0.31f, 0.15f, 0.33f),
                armorMaterial);
        }

        private static void AuthorHunterWeapon(
            Animator animator,
            Material weaponMaterial,
            Material leatherMaterial)
        {
            Transform leftHand = RequireHumanoidBone(animator, HumanBodyBones.LeftHand);
            Transform rightHand = RequireHumanoidBone(animator, HumanBodyBones.RightHand);

            GameObject bow = CreateHunterBow(
                leftHand,
                weaponMaterial,
                leatherMaterial);
            CreateArmorPrimitive(
                "HunterBowGrip",
                PrimitiveType.Cube,
                bow.transform,
                new Vector3(0.31f, 0f, 0f),
                Quaternion.identity,
                new Vector3(0.13f, 0.28f, 0.10f),
                leatherMaterial);
            CreateArmorPrimitive(
                "HunterBowTip_Lower",
                PrimitiveType.Cube,
                bow.transform,
                new Vector3(0f, -0.84f, 0f),
                Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.10f, 0.10f, 0.08f),
                weaponMaterial);
            CreateArmorPrimitive(
                "HunterBowTip_Upper",
                PrimitiveType.Cube,
                bow.transform,
                new Vector3(0f, 0.84f, 0f),
                Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.10f, 0.10f, 0.08f),
                weaponMaterial);
            GameObject arrow = AttachEquipment(
                WandPath,
                "HunterArrow_Hand",
                rightHand,
                new Vector3(0.02f, 0.01f, 0f),
                Quaternion.Euler(0f, 90f, 90f),
                Vector3.one * 0.94f);
            AssignSharedMaterial(arrow, weaponMaterial);
            if (HunterCharacterSourceSelection.UsesMixamoErikaTrial)
            {
                arrow.SetActive(false);
            }
        }

        private static GameObject CreateHunterBow(
            Transform parent,
            Material weaponMaterial,
            Material stringMaterial)
        {
            GameObject bow = new GameObject("HunterBow_Hand");
            bow.transform.SetParent(parent, false);
            bow.transform.localPosition = new Vector3(0.02f, 0.01f, 0.02f);
            bow.transform.localRotation = Quaternion.Euler(0f, 90f, 12f);

            Vector3[] limbPoints =
            {
                new Vector3(0f, -0.84f, 0f),
                new Vector3(0.16f, -0.62f, 0f),
                new Vector3(0.28f, -0.30f, 0f),
                new Vector3(0.31f, 0f, 0f),
                new Vector3(0.28f, 0.30f, 0f),
                new Vector3(0.16f, 0.62f, 0f),
                new Vector3(0f, 0.84f, 0f),
            };
            CreateCharacterLine(
                "HunterBowLimb",
                bow.transform,
                limbPoints,
                0.095f,
                weaponMaterial);
            CreateCharacterLine(
                "HunterBowString",
                bow.transform,
                new[]
                {
                    limbPoints[0],
                    new Vector3(-0.08f, 0f, 0f),
                    limbPoints[limbPoints.Length - 1],
                },
                0.013f,
                stringMaterial);
            return bow;
        }

        private static LineRenderer CreateCharacterLine(
            string name,
            Transform parent,
            IReadOnlyList<Vector3> points,
            float width,
            Material material)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.positionCount = points.Count;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int index = 0; index < points.Count; index++)
            {
                line.SetPosition(index, points[index]);
            }

            return line;
        }

        private static void AuthorQuaterniusCharacter(
            GameObject model,
            Animator animator,
            string outfitPath,
            string headMeshPath,
            string className,
            string outfitBaseMapPath,
            string outfitNormalMapPath,
            Color outfitTint)
        {
            EnsureCharacterArtFolder();
            Material outfitMaterial = EnsureQuaterniusMaterial(
                CharacterArtFolder + "/" + className + "_Outfit.mat",
                outfitBaseMapPath,
                outfitNormalMapPath,
                outfitTint,
                0.28f);
            bool male = animator.avatar != null &&
                string.Equals(animator.avatar.name, "Superhero_Male_FullBodyAvatar", StringComparison.Ordinal);
            Material headSkinMaterial = EnsureQuaterniusMaterial(
                CharacterArtFolder + "/" + (male ? "Male" : "Female") + "_Skin.mat",
                male
                    ? "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Superhero_Male_Dark.png"
                    : "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Superhero_Female_Dark_BaseColor.png",
                male
                    ? "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Superhero_Male_Normal.png"
                    : "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Superhero_Female_Normal.png",
                Color.white,
                0.36f);
            Material outfitSkinMaterial = EnsureQuaterniusMaterial(
                CharacterArtFolder + "/" + (male ? "Male" : "Female") + "_OutfitSkin.mat",
                male
                    ? "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Regular_Male_Dark_BaseColor.png"
                    : "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Regular_Female_Dark_BaseColor.png",
                male
                    ? "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Regular_Male_Normal.png"
                    : "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Regular_Female_Normal.png",
                Color.white,
                0.32f);
            Material eyeMaterial = EnsureQuaterniusMaterial(
                CharacterArtFolder + "/Quaternius_Eyes.mat",
                "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Eye_Brown.png",
                "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Eye_Normal.png",
                Color.white,
                0.48f);
            Material browMaterial = EnsureQuaterniusMaterial(
                CharacterArtFolder + "/" + (male ? "Male" : "Female") + "_Brows.mat",
                male
                    ? "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Hair_1_BaseColor.png"
                    : "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Hair_2_BaseColor.png",
                male
                    ? "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Hair_1_Normal.png"
                    : "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures/T_Hair_2_Normal.png",
                Color.white,
                0.24f);

            CreateHeadOnlyBody(model, animator, headMeshPath, headSkinMaterial);
            ConfigureFacialMaterials(model, eyeMaterial, browMaterial);
            AddRemappedOutfit(
                model,
                outfitPath,
                className + "Outfit",
                outfitMaterial,
                outfitSkinMaterial);

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                ConfigureCharacterRenderer(renderers[index]);
            }
        }

        private static void ConfigureFacialMaterials(
            GameObject model,
            Material eyeMaterial,
            Material browMaterial)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (string.Equals(renderer.name, "Eyes", StringComparison.OrdinalIgnoreCase))
                {
                    renderer.sharedMaterial = eyeMaterial;
                }
                else if (string.Equals(renderer.name, "Eyebrows", StringComparison.OrdinalIgnoreCase))
                {
                    renderer.sharedMaterial = browMaterial;
                }
            }
        }

        private static void CreateHeadOnlyBody(
            GameObject model,
            Animator animator,
            string headMeshPath,
            Material skinMaterial)
        {
            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer body = renderers
                .Where(value => value.sharedMesh != null)
                .OrderByDescending(value => value.sharedMesh.vertexCount)
                .FirstOrDefault();
            if (body == null)
            {
                throw new InvalidOperationException(
                    $"Quaternius base model '{model.name}' has no skinned body mesh.");
            }

            Transform neck = RequireHumanoidBone(animator, HumanBodyBones.Neck);
            float cutoff = body.transform.InverseTransformPoint(neck.position).y - 0.055f;
            Mesh headMesh = EnsureHeadMesh(body.sharedMesh, headMeshPath, cutoff);
            body.sharedMesh = headMesh;
            body.sharedMaterial = skinMaterial;
        }

        private static Mesh EnsureHeadMesh(Mesh source, string assetPath, float cutoff)
        {
            Mesh generated = UnityEngine.Object.Instantiate(source);
            generated.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            Vector3[] vertices = generated.vertices;
            bool hasBounds = false;
            Bounds visibleBounds = default;
            int keptTriangleCount = 0;

            for (int subMeshIndex = 0; subMeshIndex < generated.subMeshCount; subMeshIndex++)
            {
                int[] sourceTriangles = generated.GetTriangles(subMeshIndex);
                List<int> keptTriangles = new List<int>(sourceTriangles.Length / 5);
                for (int triangleIndex = 0; triangleIndex < sourceTriangles.Length; triangleIndex += 3)
                {
                    int first = sourceTriangles[triangleIndex];
                    int second = sourceTriangles[triangleIndex + 1];
                    int third = sourceTriangles[triangleIndex + 2];
                    float centroidY = (vertices[first].y + vertices[second].y + vertices[third].y) / 3f;
                    if (centroidY < cutoff)
                    {
                        continue;
                    }

                    keptTriangles.Add(first);
                    keptTriangles.Add(second);
                    keptTriangles.Add(third);
                    Encapsulate(ref visibleBounds, ref hasBounds, vertices[first]);
                    Encapsulate(ref visibleBounds, ref hasBounds, vertices[second]);
                    Encapsulate(ref visibleBounds, ref hasBounds, vertices[third]);
                    keptTriangleCount++;
                }

                generated.SetTriangles(keptTriangles, subMeshIndex, false);
            }

            if (!hasBounds || keptTriangleCount < 20)
            {
                UnityEngine.Object.DestroyImmediate(generated);
                throw new InvalidOperationException(
                    $"Head extraction from '{source.name}' kept only {keptTriangleCount} triangles.");
            }

            visibleBounds.Expand(0.14f);
            generated.bounds = visibleBounds;
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, asset);
            asset.name = generated.name;
            EditorUtility.SetDirty(asset);
            UnityEngine.Object.DestroyImmediate(generated);
            return asset;
        }

        private static void AddRemappedOutfit(
            GameObject model,
            string outfitPath,
            string instanceName,
            Material outfitMaterial,
            Material skinMaterial)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(outfitPath);
            if (source == null)
            {
                throw new InvalidOperationException($"Quaternius outfit is missing at '{outfitPath}'.");
            }

            GameObject outfit = PrefabUtility.InstantiatePrefab(source, model.transform) as GameObject;
            if (outfit == null)
            {
                throw new InvalidOperationException($"Unity could not instantiate Quaternius outfit '{outfitPath}'.");
            }

            outfit.name = instanceName;
            outfit.transform.localPosition = Vector3.zero;
            outfit.transform.localRotation = Quaternion.identity;
            outfit.transform.localScale = Vector3.one;
            PrefabUtility.UnpackPrefabInstance(
                outfit,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            Dictionary<string, Transform> targetBones = model
                .GetComponentsInChildren<Transform>(true)
                .Where(value => !value.IsChildOf(outfit.transform))
                .GroupBy(value => value.name, StringComparer.Ordinal)
                .ToDictionary(value => value.Key, value => value.First(), StringComparer.Ordinal);
            SkinnedMeshRenderer[] outfitRenderers = outfit
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (outfitRenderers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Quaternius outfit '{outfitPath}' contains no skinned renderers.");
            }

            for (int rendererIndex = 0; rendererIndex < outfitRenderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = outfitRenderers[rendererIndex];
                Transform[] remappedBones = new Transform[renderer.bones.Length];
                for (int boneIndex = 0; boneIndex < renderer.bones.Length; boneIndex++)
                {
                    Transform sourceBone = renderer.bones[boneIndex];
                    if (sourceBone == null || !targetBones.TryGetValue(sourceBone.name, out Transform targetBone))
                    {
                        throw new InvalidOperationException(
                            $"Outfit '{outfitPath}' bone '{sourceBone?.name ?? "<null>"}' has no base-character match.");
                    }

                    remappedBones[boneIndex] = targetBone;
                }

                if (renderer.rootBone == null ||
                    !targetBones.TryGetValue(renderer.rootBone.name, out Transform remappedRoot))
                {
                    throw new InvalidOperationException(
                        $"Outfit '{outfitPath}' root bone '{renderer.rootBone?.name ?? "<null>"}' " +
                        "has no base-character match.");
                }

                renderer.bones = remappedBones;
                renderer.rootBone = remappedRoot;
                renderer.sharedMesh = EnsureRemappedOutfitMesh(
                    renderer.sharedMesh,
                    CharacterArtFolder + "/" + instanceName + "_" + renderer.name + "_Remapped.asset",
                    remappedBones,
                    renderer.transform);

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    string materialName = materials[materialIndex] != null
                        ? materials[materialIndex].name
                        : string.Empty;
                    materials[materialIndex] = materialName.IndexOf(
                        "Regular",
                        StringComparison.OrdinalIgnoreCase) >= 0
                        ? skinMaterial
                        : outfitMaterial;
                }

                renderer.sharedMaterials = materials;
            }

            GameObject meshGroup = new GameObject(instanceName + "Meshes");
            meshGroup.transform.SetParent(model.transform, false);
            HashSet<Transform> rendererTransforms = new HashSet<Transform>(
                outfitRenderers.Select(value => value.transform));
            for (int rendererIndex = 0; rendererIndex < outfitRenderers.Length; rendererIndex++)
            {
                Transform rendererTransform = outfitRenderers[rendererIndex].transform;
                if (rendererTransform == outfit.transform)
                {
                    throw new InvalidOperationException(
                        $"Outfit '{outfitPath}' places a skinned renderer on its model root; " +
                        "the redundant rig cannot be removed safely.");
                }

                bool rendererHasRendererAncestor = rendererTransforms.Any(candidate =>
                    candidate != rendererTransform && rendererTransform.IsChildOf(candidate));
                if (!rendererHasRendererAncestor)
                {
                    rendererTransform.SetParent(meshGroup.transform, true);
                }
            }

            UnityEngine.Object.DestroyImmediate(outfit);
            ValidateRemappedOutfitBindings(model, meshGroup.transform, outfitRenderers, outfitPath);
        }

        private static void ValidateRemappedOutfitBindings(
            GameObject model,
            Transform meshGroup,
            IReadOnlyList<SkinnedMeshRenderer> renderers,
            string outfitPath)
        {
            for (int rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                if (renderer == null || !renderer.transform.IsChildOf(meshGroup))
                {
                    throw new InvalidOperationException(
                        $"Outfit '{outfitPath}' lost renderer {rendererIndex} while removing its redundant rig.");
                }

                if (renderer.rootBone == null || !renderer.rootBone.IsChildOf(model.transform))
                {
                    throw new InvalidOperationException(
                        $"Outfit renderer '{renderer.name}' is not rooted on the active base-character skeleton.");
                }

                Transform[] bones = renderer.bones;
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    if (bones[boneIndex] == null || !bones[boneIndex].IsChildOf(model.transform))
                    {
                        throw new InvalidOperationException(
                            $"Outfit renderer '{renderer.name}' bone {boneIndex} is not bound to " +
                            "the active base-character skeleton.");
                    }
                }
            }
        }

        private static Mesh EnsureRemappedOutfitMesh(
            Mesh source,
            string assetPath,
            IReadOnlyList<Transform> bones,
            Transform rendererTransform)
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Cannot remap outfit mesh for '{rendererTransform.name}' because its source mesh is missing.");
            }

            if (source.bindposes.Length != bones.Count)
            {
                throw new InvalidOperationException(
                    $"Outfit mesh '{source.name}' has {source.bindposes.Length} bind poses but " +
                    $"renderer '{rendererTransform.name}' has {bones.Count} bones.");
            }

            Mesh generated = UnityEngine.Object.Instantiate(source);
            generated.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            Matrix4x4[] bindPoses = new Matrix4x4[bones.Count];
            Matrix4x4 rendererToWorld = rendererTransform.localToWorldMatrix;
            for (int boneIndex = 0; boneIndex < bones.Count; boneIndex++)
            {
                bindPoses[boneIndex] = bones[boneIndex].worldToLocalMatrix * rendererToWorld;
            }

            generated.bindposes = bindPoses;
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, asset);
            asset.name = generated.name;
            EditorUtility.SetDirty(asset);
            UnityEngine.Object.DestroyImmediate(generated);
            return asset;
        }

        private static Material EnsureQuaterniusMaterial(
            string assetPath,
            string baseMapPath,
            string normalMapPath,
            Color tint,
            float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP/Lit shader is unavailable for Quaternius materials.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath),
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMapPath));
            material.SetColor("_BaseColor", tint);
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 0.85f);
            material.SetFloat("_Metallic", 0.02f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureMixamoPlayableClassTrialMaterial(
            MixamoTrialMaterialSpec materialSpec)
        {
            if (materialSpec == null)
            {
                throw new ArgumentNullException(nameof(materialSpec));
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP/Lit shader is unavailable for Mixamo playable-class trial materials.");
            }

            string assetPath = materialSpec.GeneratedMaterialAssetPath;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath),
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            Texture2D baseMap = LoadOptionalMixamoTrialTexture(
                materialSpec.BaseColorAssetPath,
                materialSpec.Key,
                "base color");
            Texture2D normalMap = LoadOptionalMixamoTrialTexture(
                materialSpec.NormalAssetPath,
                materialSpec.Key,
                "normal");
            Texture2D specularMap = LoadOptionalMixamoTrialTexture(
                materialSpec.SpecularAssetPath,
                materialSpec.Key,
                "specular");
            Texture2D emissionMap = LoadOptionalMixamoTrialTexture(
                materialSpec.EmissionAssetPath,
                materialSpec.Key,
                "emission");

            material.shader = shader;
            material.SetFloat("_WorkflowMode", specularMap != null ? 0f : 1f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat(
                "_Cull",
                materialSpec.DoubleSided
                    ? (float)CullMode.Off
                    : (float)CullMode.Back);
            material.doubleSidedGI = materialSpec.DoubleSided;
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", materialSpec.Tint);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 0.9f);
            material.SetFloat("_Metallic", Mathf.Clamp01(materialSpec.Metallic));
            material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_SpecGlossMap", specularMap);
            material.SetColor("_SpecColor", Color.white);
            material.SetFloat("_Smoothness", Mathf.Clamp01(materialSpec.Smoothness));
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (specularMap != null)
            {
                material.EnableKeyword("_SPECULAR_SETUP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_SPECGLOSSMAP");
            }
            else
            {
                material.DisableKeyword("_SPECULAR_SETUP");
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.DisableKeyword("_SPECGLOSSMAP");
            }

            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor(
                "_EmissionColor",
                emissionMap != null ? Color.white : Color.black);
            if (emissionMap != null)
            {
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            material.SetFloat("_AlphaClip", materialSpec.AlphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.32f);
            if (materialSpec.AlphaClip)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)RenderQueue.Geometry;
            }

            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureMixamoPlayableClassTrialFallbackMaterial(
            string assetPath,
            Material source)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP/Lit shader is unavailable for Mixamo playable-class inspection materials.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath),
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            Texture baseMap = FindImportedMaterialTexture(
                source,
                "_BaseMap",
                "_MainTex",
                "_BaseColorMap");
            Texture normalMap = FindImportedMaterialTexture(
                source,
                "_BumpMap",
                "_NormalMap");
            Texture emissionMap = FindImportedMaterialTexture(
                source,
                "_EmissionMap");
            Color tint = FindImportedMaterialColor(
                source,
                Color.white,
                "_BaseColor",
                "_Color");
            bool alphaClip = source.IsKeywordEnabled("_ALPHATEST_ON") ||
                source.name.IndexOf("transparent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (source.HasProperty("_Mode") && source.GetFloat("_Mode") > 0.5f);

            material.shader = shader;
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", tint);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 0.9f);
            material.SetFloat(
                "_Metallic",
                Mathf.Clamp01(FindImportedMaterialFloat(source, 0.02f, "_Metallic")));
            material.SetFloat(
                "_Smoothness",
                Mathf.Clamp01(FindImportedMaterialFloat(
                    source,
                    0.34f,
                    "_Smoothness",
                    "_Glossiness")));
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor(
                "_EmissionColor",
                emissionMap != null ? Color.white : Color.black);
            if (emissionMap != null)
            {
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            material.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.32f);
            if (alphaClip)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)RenderQueue.Geometry;
            }

            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture FindImportedMaterialTexture(
            Material material,
            params string[] propertyNames)
        {
            for (int index = 0; index < propertyNames.Length; index++)
            {
                string propertyName = propertyNames[index];
                if (material.HasProperty(propertyName))
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static Color FindImportedMaterialColor(
            Material material,
            Color fallback,
            params string[] propertyNames)
        {
            for (int index = 0; index < propertyNames.Length; index++)
            {
                string propertyName = propertyNames[index];
                if (material.HasProperty(propertyName))
                {
                    return material.GetColor(propertyName);
                }
            }

            return fallback;
        }

        private static float FindImportedMaterialFloat(
            Material material,
            float fallback,
            params string[] propertyNames)
        {
            for (int index = 0; index < propertyNames.Length; index++)
            {
                string propertyName = propertyNames[index];
                if (material.HasProperty(propertyName))
                {
                    return material.GetFloat(propertyName);
                }
            }

            return fallback;
        }

        private static Texture2D LoadOptionalMixamoTrialTexture(
            string assetPath,
            string materialKey,
            string textureUsage)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                throw new InvalidOperationException(
                    "Validated Mixamo playable-class material '" + materialKey +
                    "' has no loadable " + textureUsage + " texture at '" +
                    assetPath + "'.");
            }

            return texture;
        }

        private static Material EnsureMixamoErikaTrialMaterial(
            string assetPath,
            string baseMapPath,
            string normalMapPath,
            Color tint,
            float smoothness,
            bool alphaClip)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP/Lit shader is unavailable for the Mixamo Erika trial materials.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath),
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            Texture2D baseMap = string.IsNullOrEmpty(baseMapPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(baseMapPath);
            Texture2D normalMap = string.IsNullOrEmpty(normalMapPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
            if (!string.IsNullOrEmpty(baseMapPath) && baseMap == null)
            {
                throw new InvalidOperationException(
                    "Validated Mixamo Erika base texture is not loadable at '" +
                    baseMapPath + "'.");
            }

            if (!string.IsNullOrEmpty(normalMapPath) && normalMap == null)
            {
                throw new InvalidOperationException(
                    "Validated Mixamo Erika normal texture is not loadable at '" +
                    normalMapPath + "'.");
            }

            material.shader = shader;
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", tint);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 0.9f);
            material.SetFloat("_Metallic", 0.02f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            material.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.32f);
            if (alphaClip)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)RenderQueue.Geometry;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureProductionHunterMaterial()
        {
            EnsureCharacterArtFolder();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP/Lit shader is unavailable for the Production Hunter material.");
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(
                HunterProductionAssetValidator.BaseColorAssetPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                HunterProductionAssetValidator.NormalAssetPath);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(
                HunterProductionAssetValidator.MaskMapAssetPath);
            if (baseColor == null || normal == null || mask == null)
            {
                throw new InvalidOperationException(
                    "Validated Production Hunter textures could not be loaded for material authoring.");
            }

            string assetPath =
                HunterProductionAssetValidator.GeneratedMaterialAssetPath;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = HunterProductionAssetValidator.GeneratedMaterialName,
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = shader;
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetTexture("_BaseMap", baseColor);
            material.SetColor("_BaseColor", Color.white);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.SetTextureScale("_BumpMap", Vector2.one);
            material.SetTextureOffset("_BumpMap", Vector2.zero);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_SpecGlossMap", null);
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetTextureScale("_MetallicGlossMap", Vector2.one);
            material.SetTextureOffset("_MetallicGlossMap", Vector2.zero);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", mask);
            material.SetFloat("_OcclusionStrength", 1f);
            material.SetTextureScale("_OcclusionMap", Vector2.one);
            material.SetTextureOffset("_OcclusionMap", Vector2.zero);
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_SPECULAR_SETUP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureClassAccentMaterial(
            string assetPath,
            Color baseColor,
            Color emissionColor,
            float metallic,
            float smoothness)
        {
            EnsureCharacterArtFolder();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP/Lit shader is unavailable for class accent materials.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(assetPath),
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            material.SetColor("_EmissionColor", emissionColor);
            LocalKeyword emissionKeyword = new LocalKeyword(shader, "_EMISSION");
            if (!emissionKeyword.isValid)
            {
                throw new InvalidOperationException(
                    "URP/Lit does not expose the required _EMISSION local keyword.");
            }

            if (emissionColor.maxColorComponent > 0.001f)
            {
                material.EnableKeyword(emissionKeyword);
            }
            else
            {
                material.DisableKeyword(emissionKeyword);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool initialized, Vector3 point)
        {
            if (!initialized)
            {
                bounds = new Bounds(point, Vector3.zero);
                initialized = true;
                return;
            }

            bounds.Encapsulate(point);
        }

        private static Transform RequireHumanoidBone(Animator animator, HumanBodyBones bone)
        {
            Transform transform = animator.GetBoneTransform(bone);
            if (transform == null)
            {
                throw new InvalidOperationException(
                    $"Humanoid '{animator.name}' is missing required bone '{bone}'.");
            }

            return transform;
        }

        private static void AuthorBossSilhouette(GameObject model, Material accentMaterial)
        {
            Material boneMaterial = ResolvePrimaryMaterial(model, accentMaterial);
            Transform chest = RequireDescendant(model.transform, "chest");
            Transform leftHand = RequireDescendant(model.transform, "handslot.l");
            Transform rightHand = RequireDescendant(model.transform, "handslot.r");

            AttachEquipment(
                SkeletonAxePath,
                "BossAxe_Hand",
                rightHand,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 1.30f);
            AttachEquipment(
                SkeletonShieldPath,
                "BossShield_Hand",
                leftHand,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 1.25f);

            CreateArmorPrimitive(
                "BossPauldron_Left",
                PrimitiveType.Sphere,
                chest,
                new Vector3(-0.46f, 0.17f, 0f),
                Quaternion.Euler(0f, 0f, -12f),
                new Vector3(0.40f, 0.23f, 0.42f),
                boneMaterial);
            CreateArmorPrimitive(
                "BossPauldron_Right",
                PrimitiveType.Sphere,
                chest,
                new Vector3(0.46f, 0.17f, 0f),
                Quaternion.Euler(0f, 0f, 12f),
                new Vector3(0.40f, 0.23f, 0.42f),
                boneMaterial);
            CreateArmorPrimitive(
                "BossShoulderSpike_Left",
                PrimitiveType.Capsule,
                chest,
                new Vector3(-0.60f, 0.34f, 0f),
                Quaternion.Euler(0f, 0f, -48f),
                new Vector3(0.10f, 0.29f, 0.10f),
                accentMaterial);
            CreateArmorPrimitive(
                "BossShoulderSpike_Right",
                PrimitiveType.Capsule,
                chest,
                new Vector3(0.60f, 0.34f, 0f),
                Quaternion.Euler(0f, 0f, 48f),
                new Vector3(0.10f, 0.29f, 0.10f),
                accentMaterial);

            CreateArmorPrimitive(
                "BossBackStandardPole",
                PrimitiveType.Cylinder,
                chest,
                new Vector3(0f, 0.30f, -0.31f),
                Quaternion.identity,
                new Vector3(0.055f, 0.72f, 0.055f),
                boneMaterial);
            CreateArmorPrimitive(
                "BossBackStandard",
                PrimitiveType.Cube,
                chest,
                new Vector3(0f, 0.63f, -0.32f),
                Quaternion.identity,
                new Vector3(0.52f, 0.36f, 0.055f),
                accentMaterial);
            CreateArmorPrimitive(
                "BossChestPlate",
                PrimitiveType.Cube,
                chest,
                new Vector3(0f, -0.02f, 0.25f),
                Quaternion.identity,
                new Vector3(0.48f, 0.38f, 0.075f),
                boneMaterial);
        }

        private static GameObject CreateHumanMarker(Transform parent, Material material)
        {
            GameObject marker = new GameObject("HumanMarker_DiamondChevron");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, HumanMarkerHeight, 0f);

            CreatePrimitiveVisual(
                "Diamond",
                PrimitiveType.Cube,
                marker.transform,
                new Vector3(0f, 0.12f, 0f),
                Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.21f, 0.21f, 0.04f),
                material);
            CreatePrimitiveVisual(
                "ChevronLeft",
                PrimitiveType.Cube,
                marker.transform,
                new Vector3(-0.075f, -0.13f, 0f),
                Quaternion.Euler(0f, 0f, -35f),
                new Vector3(0.19f, 0.05f, 0.04f),
                material);
            CreatePrimitiveVisual(
                "ChevronRight",
                PrimitiveType.Cube,
                marker.transform,
                new Vector3(0.075f, -0.13f, 0f),
                Quaternion.Euler(0f, 0f, 35f),
                new Vector3(0.19f, 0.05f, 0.04f),
                material);
            return marker;
        }

        private static CombatantRelationMarker CreateRelationMarker(
            Transform parent,
            Material selectionMaterial,
            Material trackMaterial)
        {
            GameObject root = new GameObject("RelationMarker", typeof(CombatantRelationMarker));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, RelationMarkerHeight, 0f);

            GameObject labelObject = new GameObject("StateLabel");
            labelObject.transform.SetParent(root.transform, false);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.font = RequireProductFont();
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.045f;
            label.fontSize = 32;
            label.color = new Color(0.94f, 0.97f, 1f, 1f);
            label.text = "ENEMY";
            MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
            labelRenderer.sharedMaterial = label.font.material;
            labelRenderer.shadowCastingMode = ShadowCastingMode.Off;
            labelRenderer.receiveShadows = false;

            GameObject chevron = new GameObject("SelectedChevron");
            chevron.transform.SetParent(root.transform, false);
            chevron.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            CreatePrimitiveVisual(
                "ChevronLeft",
                PrimitiveType.Cube,
                chevron.transform,
                new Vector3(-0.08f, 0f, 0f),
                Quaternion.Euler(0f, 0f, -35f),
                new Vector3(0.2f, 0.045f, 0.035f),
                material: selectionMaterial);
            CreatePrimitiveVisual(
                "ChevronRight",
                PrimitiveType.Cube,
                chevron.transform,
                new Vector3(0.08f, 0f, 0f),
                Quaternion.Euler(0f, 0f, 35f),
                new Vector3(0.2f, 0.045f, 0.035f),
                material: selectionMaterial);
            chevron.SetActive(false);

            var identityRenderers = new List<Renderer>(13);
            GameObject allyIdentity = CreateRelationIdentityRoot(
                "AllyIdentity_Wings",
                root.transform);
            AddRelationIdentityPart(
                "WingLeft",
                allyIdentity.transform,
                new Vector3(-0.065f, 0.015f, 0f),
                Quaternion.Euler(0f, 0f, -18f),
                new Vector3(0.14f, 0.038f, 0.032f),
                selectionMaterial,
                identityRenderers);
            AddRelationIdentityPart(
                "WingRight",
                allyIdentity.transform,
                new Vector3(0.065f, 0.015f, 0f),
                Quaternion.Euler(0f, 0f, 18f),
                new Vector3(0.14f, 0.038f, 0.032f),
                selectionMaterial,
                identityRenderers);
            AddRelationIdentityPart(
                "ShieldStem",
                allyIdentity.transform,
                new Vector3(0f, -0.04f, 0f),
                Quaternion.identity,
                new Vector3(0.035f, 0.105f, 0.032f),
                selectionMaterial,
                identityRenderers);

            GameObject enemyIdentity = CreateRelationIdentityRoot(
                "EnemyIdentity_Fangs",
                root.transform);
            AddRelationIdentityPart(
                "FangLeft",
                enemyIdentity.transform,
                new Vector3(-0.055f, 0.01f, 0f),
                Quaternion.Euler(0f, 0f, 42f),
                new Vector3(0.15f, 0.04f, 0.032f),
                selectionMaterial,
                identityRenderers);
            AddRelationIdentityPart(
                "FangRight",
                enemyIdentity.transform,
                new Vector3(0.055f, 0.01f, 0f),
                Quaternion.Euler(0f, 0f, -42f),
                new Vector3(0.15f, 0.04f, 0.032f),
                selectionMaterial,
                identityRenderers);
            AddRelationIdentityPart(
                "FangPoint",
                enemyIdentity.transform,
                new Vector3(0f, -0.07f, 0f),
                Quaternion.identity,
                new Vector3(0.03f, 0.11f, 0.032f),
                selectionMaterial,
                identityRenderers);

            GameObject humanIdentity = CreateRelationIdentityRoot(
                "HumanIdentity_Diamond",
                root.transform);
            AddRelationIdentityPart(
                "Diamond",
                humanIdentity.transform,
                Vector3.zero,
                Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0.13f, 0.13f, 0.034f),
                selectionMaterial,
                identityRenderers);

            GameObject bossIdentity = CreateRelationIdentityRoot(
                "BossIdentity_Crown",
                root.transform);
            AddRelationIdentityPart(
                "CrownBase",
                bossIdentity.transform,
                new Vector3(0f, -0.045f, 0f),
                Quaternion.identity,
                new Vector3(0.24f, 0.04f, 0.034f),
                selectionMaterial,
                identityRenderers);
            for (int spike = -1; spike <= 1; spike++)
            {
                AddRelationIdentityPart(
                    "CrownSpike_" + spike,
                    bossIdentity.transform,
                    new Vector3(spike * 0.075f, 0.025f, 0f),
                    Quaternion.identity,
                    new Vector3(0.035f, spike == 0 ? 0.17f : 0.12f, 0.034f),
                    selectionMaterial,
                    identityRenderers);
            }

            allyIdentity.SetActive(false);
            enemyIdentity.SetActive(false);
            humanIdentity.SetActive(false);
            bossIdentity.SetActive(false);

            GameObject badgeRoot = new GameObject("ClassBadge");
            badgeRoot.transform.SetParent(root.transform, false);
            badgeRoot.transform.localPosition = new Vector3(0.73f, -0.25f, 0f);
            CreatePrimitiveVisual(
                "BadgeBack",
                PrimitiveType.Quad,
                badgeRoot.transform,
                new Vector3(0f, 0f, 0.012f),
                Quaternion.identity,
                new Vector3(0.25f, 0.25f, 1f),
                trackMaterial);
            GameObject badgeTextObject = new GameObject("ClassGlyph", typeof(TextMesh));
            badgeTextObject.transform.SetParent(badgeRoot.transform, false);
            badgeTextObject.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            TextMesh badgeLabel = badgeTextObject.GetComponent<TextMesh>();
            badgeLabel.font = RequireProductFont();
            badgeLabel.anchor = TextAnchor.MiddleCenter;
            badgeLabel.alignment = TextAlignment.Center;
            badgeLabel.characterSize = 0.034f;
            badgeLabel.fontSize = 32;
            badgeLabel.fontStyle = FontStyle.Bold;
            badgeLabel.color = Color.white;
            badgeLabel.text = "?";
            MeshRenderer badgeRenderer = badgeTextObject.GetComponent<MeshRenderer>();
            badgeRenderer.sharedMaterial = badgeLabel.font.material;
            badgeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            badgeRenderer.receiveShadows = false;

            CombatantRelationMarker marker = root.GetComponent<CombatantRelationMarker>();
            SerializedObject serialized = new SerializedObject(marker);
            serialized.Update();
            SetObjectReference(serialized, "_label", label);
            SetObjectReference(serialized, "_classBadgeLabel", badgeLabel);
            SetObjectReference(serialized, "_selectionChevron", chevron.transform);
            SetObjectReference(serialized, "_allyIdentityRoot", allyIdentity);
            SetObjectReference(serialized, "_enemyIdentityRoot", enemyIdentity);
            SetObjectReference(serialized, "_humanIdentityRoot", humanIdentity);
            SetObjectReference(serialized, "_bossIdentityRoot", bossIdentity);
            SerializedProperty identityRendererProperty = RequireProperty(
                serialized,
                "_identityRenderers");
            identityRendererProperty.arraySize = identityRenderers.Count;
            for (int index = 0; index < identityRenderers.Count; index++)
            {
                identityRendererProperty.GetArrayElementAtIndex(index).objectReferenceValue =
                    identityRenderers[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        private static GameObject CreateRelationIdentityRoot(string name, Transform parent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-0.73f, -0.25f, 0f);
            return root;
        }

        private static void AddRelationIdentityPart(
            string name,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material material,
            List<Renderer> renderers)
        {
            GameObject part = CreatePrimitiveVisual(
                name,
                PrimitiveType.Cube,
                parent,
                position,
                rotation,
                scale,
                material);
            renderers.Add(part.GetComponent<Renderer>());
        }

        private static void CreateWorldBars(
            Transform parent,
            Material healthMaterial,
            Material barrierMaterial,
            Material castMaterial,
            Material trackMaterial,
            out Transform healthFill,
            out Transform barrierFill,
            out Transform castFill)
        {
            GameObject bars = new GameObject("WorldStatusBars");
            bars.transform.SetParent(parent, false);
            bars.transform.localPosition = new Vector3(0f, WorldStatusHeight, 0f);

            CreatePrimitiveVisual(
                "HealthTrack",
                PrimitiveType.Quad,
                bars.transform,
                new Vector3(0f, 0.10f, 0.012f),
                Quaternion.identity,
                new Vector3(1.22f, 0.16f, 1f),
                trackMaterial);

            healthFill = CreateBarFill(
                "HealthFill",
                bars.transform,
                1.12f,
                0.10f,
                0.10f,
                healthMaterial);
            barrierFill = CreateBarFill(
                "BarrierFill",
                bars.transform,
                1.12f,
                0.055f,
                -0.015f,
                barrierMaterial);
            castFill = CreateBarFill(
                "CastFill",
                bars.transform,
                1.12f,
                0.06f,
                -0.11f,
                castMaterial);

            healthFill.gameObject.SetActive(true);
            barrierFill.gameObject.SetActive(false);
            castFill.gameObject.SetActive(false);
        }

        private static Transform CreateBarFill(
            string name,
            Transform parent,
            float width,
            float height,
            float verticalPosition,
            Material material)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = new Vector3(-width * 0.5f, verticalPosition, 0f);

            CreatePrimitiveVisual(
                "Visual",
                PrimitiveType.Quad,
                anchor.transform,
                new Vector3(width * 0.5f, 0f, 0f),
                Quaternion.identity,
                new Vector3(width, height, 1f),
                material);
            return anchor.transform;
        }

        private static LineRenderer CreateRing(
            string name,
            Transform parent,
            float radius,
            float width,
            float height,
            Material material)
        {
            GameObject ringObject = new GameObject(name);
            ringObject.transform.SetParent(parent, false);
            ringObject.transform.localPosition = new Vector3(0f, height, 0f);
            ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.alignment = LineAlignment.TransformZ;
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = RingPointCount;
            ring.widthMultiplier = width;
            ring.numCapVertices = 2;
            ring.numCornerVertices = 2;
            ring.textureMode = LineTextureMode.Stretch;
            ring.sharedMaterial = material;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.lightProbeUsage = LightProbeUsage.Off;
            ring.reflectionProbeUsage = ReflectionProbeUsage.Off;

            for (int index = 0; index < RingPointCount; index++)
            {
                float radians = (index / (float)RingPointCount) * Mathf.PI * 2f;
                ring.SetPosition(index, new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0f));
            }

            return ring;
        }

        private static void CreateVerticalRing(
            string name,
            Transform parent,
            Vector3 localPosition,
            float radius,
            float width,
            Material material)
        {
            GameObject ringObject = new GameObject(name);
            ringObject.transform.SetParent(parent, false);
            ringObject.transform.localPosition = localPosition;

            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.alignment = LineAlignment.TransformZ;
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = RingPointCount;
            ring.widthMultiplier = width;
            ring.numCapVertices = 2;
            ring.numCornerVertices = 2;
            ring.textureMode = LineTextureMode.Stretch;
            ring.sharedMaterial = material;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.lightProbeUsage = LightProbeUsage.Off;
            ring.reflectionProbeUsage = ReflectionProbeUsage.Off;

            for (int index = 0; index < RingPointCount; index++)
            {
                float radians = (index / (float)RingPointCount) * Mathf.PI * 2f;
                ring.SetPosition(index, new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0f));
            }
        }

        private static GameObject AttachEquipment(
            string assetPath,
            string instanceName,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Cannot author the heroic combatant silhouette because equipment is missing at '{assetPath}'.");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Unity could not instantiate heroic equipment at '{assetPath}'.");
            }

            instance.name = instanceName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                ConfigureCharacterRenderer(renderers[index]);
            }

            return instance;
        }

        private static void AssignSharedMaterial(GameObject root, Material material)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    materials[materialIndex] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static GameObject CreateArmorPrimitive(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;

            Collider generatedCollider = visual.GetComponent<Collider>();
            if (generatedCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(generatedCollider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            ConfigureCharacterRenderer(renderer);
            return visual;
        }

        private static void ConfigureCharacterRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }

        private static Material ResolvePrimaryMaterial(GameObject model, Material fallback)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (materials[materialIndex] != null)
                    {
                        return materials[materialIndex];
                    }
                }
            }

            return fallback;
        }

        private static Transform RequireDescendant(Transform root, string descendantName)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                string value = descendants[index].name;
                if (string.Equals(value, descendantName, StringComparison.OrdinalIgnoreCase) ||
                    value.EndsWith(":" + descendantName, StringComparison.OrdinalIgnoreCase))
                {
                    return descendants[index];
                }
            }

            throw new InvalidOperationException(
                $"KayKit model '{root.name}' has no required Generic-rig attachment '{descendantName}'.");
        }

        private static GameObject CreatePrimitiveVisual(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;

            Collider generatedCollider = visual.GetComponent<Collider>();
            if (generatedCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(generatedCollider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return visual;
        }

        private static GameObject InstantiateModel(
            string assetPath,
            string instanceName,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Cannot author Combatant.prefab because the model is missing at '{assetPath}'. " +
                    "Restore the verified free asset and run asset validation.");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Unity could not instantiate the model at '{assetPath}'.");
            }

            instance.name = instanceName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;
            return instance;
        }

        private static Animator ConfigureAnimator(
            GameObject model,
            string modelPath,
            RuntimeAnimatorController controller,
            bool requireHuman)
        {
            if (controller == null)
            {
                throw new InvalidOperationException(
                    "Cannot author animated combatant because a class AnimatorController is missing.");
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<Avatar>()
                .SingleOrDefault(value =>
                    value.isValid &&
                    (requireHuman ? value.isHuman : !value.isHuman));
            if (avatar == null)
            {
                throw new InvalidOperationException(
                    "Model at '" + modelPath + "' has no valid " +
                    (requireHuman ? "Humanoid" : "Generic") + " Avatar.");
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return animator;
        }

        private static Transform CreateTransform(string name, Transform parent, Vector3 position)
        {
            GameObject value = new GameObject(name);
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            return value.transform;
        }

        private static void SetObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            RequireProperty(serializedObject, propertyName).objectReferenceValue = value;
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Authoring contract changed: '{serializedObject.targetObject.GetType().Name}' " +
                    $"does not expose serialized field '{propertyName}'.");
            }

            return property;
        }

        private static T GetOrAddComponent<T>(GameObject value)
            where T : Component
        {
            T component = value.GetComponent<T>();
            return component != null ? component : value.AddComponent<T>();
        }

        private static void ResetRootTransform(GameObject root)
        {
            root.name = "Combatant";
            root.layer = 9;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.SetActive(true);
        }

        private static void ClearChildren(Transform root)
        {
            while (root.childCount > 0)
            {
                UnityEngine.Object.DestroyImmediate(root.GetChild(root.childCount - 1).gameObject);
            }
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                string guid = AssetDatabase.CreateFolder("Assets/SessionRpg", "Prefabs");
                if (string.IsNullOrEmpty(guid))
                {
                    throw new InvalidOperationException(
                        $"Unity failed to create the prefab folder '{PrefabFolder}'.");
                }
            }
        }

        private static void EnsureCharacterArtFolder()
        {
            if (AssetDatabase.IsValidFolder(CharacterArtFolder))
            {
                return;
            }

            string guid = AssetDatabase.CreateFolder(AuthoringAssets.ArtFolder, "Characters");
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException(
                    $"Could not create generated character-art folder '{CharacterArtFolder}'.");
            }
        }

        private static void RequireMaterial(Material material, string parameterName)
        {
            if (material == null)
            {
                throw new ArgumentNullException(
                    parameterName,
                    "Combatant prefab authoring requires every readability material.");
            }
        }

        private static Font RequireProductFont()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ProductFontPath);
            if (font == null)
            {
                throw new InvalidOperationException(
                    "Combatant readability authoring requires the tracked product font at '" +
                    ProductFontPath + "'.");
            }

            return font;
        }
    }
}
