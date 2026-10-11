using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SessionRpg.Unity.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Assets
{
    public enum HunterCharacterSource
    {
        QuaterniusPrototype = 0,
        ProductionV1 = 1,
        MixamoErikaLicensedGameBase = 2,
        MixamoErikaTrial = MixamoErikaLicensedGameBase,
    }

    public static class HunterCharacterSourceSelection
    {
        // The public checkout uses the redistributable CC0 source. Licensed or
        // production art requires its own verified intake and authored rebuild.
        public const HunterCharacterSource Selected =
            HunterCharacterSource.QuaterniusPrototype;

        public static bool UsesQuaterniusPrototype =>
            Selected == HunterCharacterSource.QuaterniusPrototype;

        public static bool UsesProductionV1 =>
            Selected == HunterCharacterSource.ProductionV1;

        public static bool UsesMixamoErikaTrial =>
            UsesMixamoErikaLicensedGameBase;

        public static bool UsesMixamoErikaLicensedGameBase =>
            Selected == HunterCharacterSource.MixamoErikaLicensedGameBase;
    }

    public static class HunterProductionAssetValidator
    {
        public const string SourceFolder =
            "Assets/SessionRpg/SourceArt/Characters/HunterProductionV1";

        public const string ModelAssetPath =
            SourceFolder + "/HunterProductionV1.fbx";

        public const string BaseColorAssetPath =
            SourceFolder + "/HunterProductionV1_BaseColor.png";

        public const string NormalAssetPath =
            SourceFolder + "/HunterProductionV1_Normal.png";

        public const string MaskMapAssetPath =
            SourceFolder + "/HunterProductionV1_Mask.png";

        public const string Lod0RendererName = "HunterProductionV3_LOD0";
        public const string Lod1RendererName = "HunterProductionV3_LOD1";
        public const string Lod2RendererName = "HunterProductionV3_LOD2";
        public const string StaticStringRendererName =
            "HunterProductionV3_StaticString";
        public const string DrawStringRendererName =
            "HunterProductionV3_DrawString";
        public const string NockedArrowRendererName =
            "HunterProductionV3_NockedArrow";

        public const string GeneratedMaterialName = "HunterProductionV1";

        public const string GeneratedMaterialAssetPath =
            "Assets/SessionRpg/Art/Characters/" + GeneratedMaterialName + ".mat";

        public const string ConfigureMenuPath =
            "Tools/Session RPG/Assets/Configure Production Hunter Import";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Assets/Validate Production Hunter Source Art";

        public const int ExpectedBoneCount = 65;
        public const int ExpectedRendererCount = 6;
        public const int MaximumRuntimeRendererCount = 3;
        public const int TextureSize = 1024;

        private const string ConfigureInstruction =
            "Select ProductionV1 in HunterCharacterSourceSelection, copy the approved DCC export into the tracked production folder, then run Tools > Session RPG > Assets > Configure Production Hunter Import once.";

        private const float TransformTolerance = 0.0001f;

        private static readonly RendererContract[] RendererContracts =
        {
            new RendererContract(Lod0RendererName, 15000, 18000, true),
            new RendererContract(Lod1RendererName, 7000, 9000, true),
            new RendererContract(Lod2RendererName, 3000, 4000, true),
            new RendererContract(StaticStringRendererName, 1, 700, true),
            new RendererContract(DrawStringRendererName, 1, 700, true),
            new RendererContract(NockedArrowRendererName, 1, 700, true),
        };

        private static readonly HumanBodyBones[] RequiredHumanoidBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
        };

        private static readonly string[] ExpectedRigBoneNames =
        {
            "root",
            "pelvis",
            "spine_01",
            "spine_02",
            "spine_03",
            "neck_01",
            "Head",
            "clavicle_l",
            "upperarm_l",
            "lowerarm_l",
            "hand_l",
            "index_01_l",
            "index_02_l",
            "index_03_l",
            "index_04_leaf_l",
            "middle_01_l",
            "middle_02_l",
            "middle_03_l",
            "middle_04_leaf_l",
            "pinky_01_l",
            "pinky_02_l",
            "pinky_03_l",
            "pinky_04_leaf_l",
            "ring_01_l",
            "ring_02_l",
            "ring_03_l",
            "ring_04_leaf_l",
            "thumb_01_l",
            "thumb_02_l",
            "thumb_03_l",
            "thumb_04_leaf_l",
            "clavicle_r",
            "upperarm_r",
            "lowerarm_r",
            "hand_r",
            "index_01_r",
            "index_02_r",
            "index_03_r",
            "index_04_leaf_r",
            "middle_01_r",
            "middle_02_r",
            "middle_03_r",
            "middle_04_leaf_r",
            "pinky_01_r",
            "pinky_02_r",
            "pinky_03_r",
            "pinky_04_leaf_r",
            "ring_01_r",
            "ring_02_r",
            "ring_03_r",
            "ring_04_leaf_r",
            "thumb_01_r",
            "thumb_02_r",
            "thumb_03_r",
            "thumb_04_leaf_r",
            "thigh_l",
            "calf_l",
            "foot_l",
            "ball_l",
            "ball_leaf_l",
            "thigh_r",
            "calf_r",
            "foot_r",
            "ball_r",
            "ball_leaf_r",
        };

        [MenuItem(ValidateMenuPath, priority = 2084)]
        public static void ValidateFromMenu()
        {
            try
            {
                if (!HunterCharacterSourceSelection.UsesProductionV1)
                {
                    string skipped =
                        "Production Hunter validation skipped: ProductionV1 is not selected (selected source: " +
                        HunterCharacterSourceSelection.Selected + ").";
                    Debug.Log(skipped);
                    ShowDialog("Production Hunter validation", skipped);
                    return;
                }

                ValidateSelectedSourceOrThrow();
                const string message =
                    "Production Hunter model, textures, rig, and mobile renderer contract are valid.";
                Debug.Log(message);
                ShowDialog("Production Hunter validation", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Production Hunter validation failed", exception.Message);
            }
        }

        [MenuItem(ConfigureMenuPath, priority = 2085)]
        public static void ConfigureFromMenu()
        {
            try
            {
                if (!HunterCharacterSourceSelection.UsesProductionV1)
                {
                    string skipped =
                        "Production Hunter import skipped: ProductionV1 is not selected (selected source: " +
                        HunterCharacterSourceSelection.Selected + ").";
                    Debug.Log(skipped);
                    ShowDialog("Production Hunter import", skipped);
                    return;
                }

                int changedCount = ConfigureSelectedSource();
                ValidateSelectedSourceOrThrow();
                string message = changedCount == 0
                    ? "Production Hunter import settings were already correct."
                    : "Configured and reimported " + changedCount +
                      " Production Hunter asset(s).";
                Debug.Log(message);
                ShowDialog("Production Hunter import", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Production Hunter import failed", exception.Message);
            }
        }

        public static void ValidateForCommandLine()
        {
            if (!HunterCharacterSourceSelection.UsesProductionV1)
            {
                Debug.Log(
                    "Production Hunter validation skipped: ProductionV1 is not selected (selected source: " +
                    HunterCharacterSourceSelection.Selected + ").");
                return;
            }

            ValidateSelectedSourceOrThrow();
            Debug.Log("Production Hunter command-line validation passed.");
        }

        public static void ConfigureForCommandLine()
        {
            if (!HunterCharacterSourceSelection.UsesProductionV1)
            {
                Debug.Log(
                    "Production Hunter import skipped: ProductionV1 is not selected (selected source: " +
                    HunterCharacterSourceSelection.Selected +
                    "); no assets were queried or changed.");
                return;
            }

            int changedCount = ConfigureSelectedSource();
            ValidateSelectedSourceOrThrow();
            Debug.Log(
                "Production Hunter import configuration passed; changed " +
                changedCount + " asset(s).");
        }

        public static void ValidateSelectedSourceOrThrow()
        {
            if (!HunterCharacterSourceSelection.UsesProductionV1)
            {
                return;
            }

            var errors = new List<string>();
            ValidateModel(errors);
            ValidateTexture(BaseColorAssetPath, TextureKind.BaseColor, errors);
            ValidateTexture(NormalAssetPath, TextureKind.Normal, errors);
            ValidateTexture(MaskMapAssetPath, TextureKind.MaskMap, errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }
        }

        public static GameObject LoadSelectedModelOrThrow()
        {
            if (!HunterCharacterSourceSelection.UsesProductionV1)
            {
                throw new InvalidOperationException(
                    "Production Hunter source was requested while " +
                    HunterCharacterSourceSelection.Selected + " is selected.");
            }

            ValidateSelectedSourceOrThrow();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAssetPath);
            if (model == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the validated Production Hunter at '" +
                    ModelAssetPath + "'.");
            }

            return model;
        }

        public static void ValidateGeneratedPrefabOrThrow(GameObject prefab)
        {
            var errors = new List<string>();
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Cannot validate the selected Hunter hierarchy because Combatant.prefab is missing.");
            }

            Transform prototypeRoot = prefab.transform.Find("HunterModel_Quaternius");
            Transform productionRoot = prefab.transform.Find("HunterModel_ProductionV1");
            Transform trialRoot = prefab.transform.Find(
                MixamoErikaTrialAssetValidator.ModelRootName);
            Transform expectedRoot;
            string expectedName;
            if (HunterCharacterSourceSelection.UsesProductionV1)
            {
                expectedRoot = productionRoot;
                expectedName = "HunterModel_ProductionV1";
            }
            else if (HunterCharacterSourceSelection.UsesMixamoErikaTrial)
            {
                expectedRoot = trialRoot;
                expectedName = MixamoErikaTrialAssetValidator.ModelRootName;
            }
            else
            {
                expectedRoot = prototypeRoot;
                expectedName = "HunterModel_Quaternius";
            }

            if (expectedRoot == null)
            {
                errors.Add(
                    "Combatant.prefab is stale for the selected Hunter source; missing direct child '" +
                    expectedName + "'. Run the authored rebuild once.");
            }

            int authoredSourceRootCount =
                (prototypeRoot != null ? 1 : 0) +
                (productionRoot != null ? 1 : 0) +
                (trialRoot != null ? 1 : 0);
            if (authoredSourceRootCount != 1)
            {
                errors.Add(
                    "Combatant.prefab contains " + authoredSourceRootCount +
                    " Hunter source roots; exactly one authored source is allowed.");
            }

            CombatantView view = prefab.GetComponent<CombatantView>();
            CombatantAnimationPresenter presenter =
                prefab.GetComponent<CombatantAnimationPresenter>();
            if (view == null)
            {
                errors.Add("Combatant.prefab has no CombatantView.");
            }
            else if (expectedRoot != null)
            {
                UnityEngine.Object selectedModel = GetSerializedReference(
                    view,
                    "_hunterModel",
                    errors);
                if (selectedModel != expectedRoot.gameObject)
                {
                    errors.Add(
                        "CombatantView._hunterModel does not reference the selected '" +
                        expectedName + "' root.");
                }
            }

            if (presenter == null)
            {
                errors.Add("Combatant.prefab has no CombatantAnimationPresenter.");
            }

            if (HunterCharacterSourceSelection.UsesQuaterniusPrototype)
            {
                if (productionRoot != null || trialRoot != null)
                {
                    errors.Add(
                        "Combatant.prefab retains a non-Quaternius Hunter root while QuaterniusPrototype is selected.");
                }

                ValidateDormantPresenterReferences(presenter, errors);
                ThrowGeneratedErrors(errors);
                return;
            }

            if (HunterCharacterSourceSelection.UsesMixamoErikaTrial)
            {
                if (prototypeRoot != null || productionRoot != null)
                {
                    errors.Add(
                        "Combatant.prefab retains a production/prototype Hunter root while MixamoErikaTrial is selected.");
                }

                ValidateTrialPresenterReferences(
                    presenter,
                    trialRoot != null ? trialRoot.gameObject : null,
                    errors);
                ThrowGeneratedErrors(errors);
                MixamoErikaTrialAssetValidator.ValidateGeneratedRootOrThrow(
                    trialRoot.gameObject);
                return;
            }

            if (prototypeRoot != null || trialRoot != null)
            {
                errors.Add(
                    "Combatant.prefab retains a prototype/trial Hunter root while ProductionV1 is selected.");
            }

            if (productionRoot != null)
            {
                ValidateGeneratedProductionRoot(
                    productionRoot.gameObject,
                    presenter,
                    errors);
            }

            ThrowGeneratedErrors(errors);
        }

        private static void ValidateDormantPresenterReferences(
            CombatantAnimationPresenter presenter,
            List<string> errors)
        {
            if (presenter == null)
            {
                return;
            }

            string[] propertyNames =
            {
                "_hunterStaticString",
                "_hunterDrawString",
                "_hunterNockedArrow",
            };
            for (int index = 0; index < propertyNames.Length; index++)
            {
                if (GetSerializedReference(
                        presenter,
                        propertyNames[index],
                        errors) != null)
                {
                    errors.Add(
                        "Dormant Production Hunter reference '" +
                        propertyNames[index] + "' must be null while ProductionV1 is not selected.");
                }
            }
        }

        private static void ValidateTrialPresenterReferences(
            CombatantAnimationPresenter presenter,
            GameObject trialRoot,
            List<string> errors)
        {
            if (presenter == null || trialRoot == null)
            {
                return;
            }

            UnityEngine.Object staticString = GetSerializedReference(
                presenter,
                "_hunterStaticString",
                errors);
            UnityEngine.Object drawString = GetSerializedReference(
                presenter,
                "_hunterDrawString",
                errors);
            UnityEngine.Object nockedArrow = GetSerializedReference(
                presenter,
                "_hunterNockedArrow",
                errors);
            Transform projectArrow = trialRoot
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value =>
                    string.Equals(
                        value.name,
                        "HunterArrow_Hand",
                        StringComparison.Ordinal));

            if (staticString != null || drawString != null)
            {
                errors.Add(
                    "MixamoErikaTrial must keep the ProductionV1 static/draw string references null; its baked trial bow owns its static string geometry.");
            }

            if (projectArrow == null)
            {
                errors.Add(
                    "MixamoErikaTrial is missing the authored project nocked arrow 'HunterArrow_Hand'.");
            }
            else if (nockedArrow != projectArrow.gameObject)
            {
                errors.Add(
                    "CombatantAnimationPresenter._hunterNockedArrow must reference the MixamoErikaTrial project 'HunterArrow_Hand'.");
            }
        }

        private static void ValidateGeneratedProductionRoot(
            GameObject productionRoot,
            CombatantAnimationPresenter presenter,
            List<string> errors)
        {
            Renderer[] renderers = productionRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != ExpectedRendererCount)
            {
                errors.Add(
                    "Generated Production Hunter has " + renderers.Length +
                    " renderers; expected exactly " + ExpectedRendererCount + ".");
                return;
            }

            var rendererByName = new Dictionary<string, Renderer>(StringComparer.Ordinal);
            Material sharedMaterial = null;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!rendererByName.TryAdd(renderer.name, renderer))
                {
                    errors.Add(
                        "Generated Production Hunter has duplicate renderer '" +
                        renderer.name + "'.");
                }

                if (renderer.sharedMaterials.Length != 1 ||
                    renderer.sharedMaterial == null)
                {
                    errors.Add(
                        "Generated Production Hunter renderer '" + renderer.name +
                        "' must use exactly one shared atlas material.");
                    continue;
                }

                if (sharedMaterial == null)
                {
                    sharedMaterial = renderer.sharedMaterial;
                }
                else if (renderer.sharedMaterial != sharedMaterial)
                {
                    errors.Add(
                        "Generated Production Hunter renderers do not share one material instance.");
                }
            }

            ValidateGeneratedMaterial(sharedMaterial, errors);
            Renderer lod0 = GetGeneratedRenderer(
                Lod0RendererName,
                rendererByName,
                errors);
            Renderer lod1 = GetGeneratedRenderer(
                Lod1RendererName,
                rendererByName,
                errors);
            Renderer lod2 = GetGeneratedRenderer(
                Lod2RendererName,
                rendererByName,
                errors);
            Renderer staticString = GetGeneratedRenderer(
                StaticStringRendererName,
                rendererByName,
                errors);
            Renderer drawString = GetGeneratedRenderer(
                DrawStringRendererName,
                rendererByName,
                errors);
            Renderer nockedArrow = GetGeneratedRenderer(
                NockedArrowRendererName,
                rendererByName,
                errors);

            LODGroup lodGroup = productionRoot.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                errors.Add("Generated Production Hunter has no LODGroup.");
            }
            else
            {
                LOD[] lods = lodGroup.GetLODs();
                Renderer[] expectedLods = { lod0, lod1, lod2 };
                float[] expectedTransitions = { 0.10f, 0.055f, 0.02f };
                if (lods.Length != expectedLods.Length)
                {
                    errors.Add(
                        "Generated Production Hunter LODGroup has " + lods.Length +
                        " levels; expected three.");
                }
                else
                {
                    for (int index = 0; index < lods.Length; index++)
                    {
                        if (lods[index].renderers.Length != 1 ||
                            lods[index].renderers[0] != expectedLods[index] ||
                            !Mathf.Approximately(
                                lods[index].screenRelativeTransitionHeight,
                                expectedTransitions[index]))
                        {
                            errors.Add(
                                "Generated Production Hunter LOD" + index +
                                " does not match its one-renderer transition contract.");
                        }
                    }
                }
            }

            Animator animator = productionRoot.GetComponent<Animator>();
            if (animator == null ||
                animator.cullingMode != AnimatorCullingMode.CullUpdateTransforms)
            {
                errors.Add(
                    "Generated Production Hunter Animator must use CullUpdateTransforms so the final body/weapon cull also suspends animation work.");
            }

            HunterAuxiliaryLodCuller[] cullers =
                productionRoot.GetComponents<HunterAuxiliaryLodCuller>();
            if (cullers.Length != 1)
            {
                errors.Add(
                    "Generated Production Hunter requires exactly one auxiliary LOD culler; found " +
                    cullers.Length + ".");
            }
            else
            {
                HunterAuxiliaryLodCuller culler = cullers[0];
                if (culler.TargetLodGroup != lodGroup ||
                    culler.AuxiliaryRendererCount != 3 ||
                    !Mathf.Approximately(culler.CullScreenRelativeHeight, 0.02f) ||
                    !culler.ContainsAuxiliaryRenderer(staticString) ||
                    !culler.ContainsAuxiliaryRenderer(drawString) ||
                    !culler.ContainsAuxiliaryRenderer(nockedArrow))
                {
                    errors.Add(
                        "Generated Production Hunter auxiliary culler does not match the three weapon renderers and final LOD threshold.");
                }
            }

            ValidateInitialWeaponState(
                staticString,
                drawString,
                nockedArrow,
                errors);
            ValidateGeneratedPresenterReferences(
                presenter,
                staticString,
                drawString,
                nockedArrow,
                errors);
        }

        private static void ValidateGeneratedMaterial(
            Material material,
            List<string> errors)
        {
            if (material == null)
            {
                return;
            }

            bool valid = material.shader != null &&
                material.shader.name == "Universal Render Pipeline/Lit" &&
                AssetDatabase.GetAssetPath(material) == GeneratedMaterialAssetPath &&
                AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) ==
                    BaseColorAssetPath &&
                AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")) ==
                    NormalAssetPath &&
                AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")) ==
                    MaskMapAssetPath &&
                AssetDatabase.GetAssetPath(material.GetTexture("_OcclusionMap")) ==
                    MaskMapAssetPath &&
                Mathf.Approximately(material.GetFloat("_WorkflowMode"), 1f) &&
                Mathf.Approximately(material.GetFloat("_SmoothnessTextureChannel"), 0f) &&
                Mathf.Approximately(material.GetFloat("_Metallic"), 1f) &&
                Mathf.Approximately(material.GetFloat("_Smoothness"), 1f) &&
                material.IsKeywordEnabled("_NORMALMAP") &&
                material.IsKeywordEnabled("_METALLICSPECGLOSSMAP") &&
                material.IsKeywordEnabled("_OCCLUSIONMAP") &&
                !material.IsKeywordEnabled("_SPECULAR_SETUP") &&
                !material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A") &&
                !material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") &&
                material.renderQueue == (int)RenderQueue.Geometry;
            if (!valid)
            {
                errors.Add(
                    "Generated Production Hunter material does not use the normalized opaque URP metallic workflow or direct R/G/A mask packing.");
            }
        }

        private static Renderer GetGeneratedRenderer(
            string name,
            IReadOnlyDictionary<string, Renderer> rendererByName,
            List<string> errors)
        {
            if (rendererByName.TryGetValue(name, out Renderer renderer))
            {
                return renderer;
            }

            errors.Add("Generated Production Hunter is missing renderer '" + name + "'.");
            return null;
        }

        private static void ValidateInitialWeaponState(
            Renderer staticString,
            Renderer drawString,
            Renderer nockedArrow,
            List<string> errors)
        {
            if (staticString == null || drawString == null || nockedArrow == null)
            {
                return;
            }

            if (!staticString.gameObject.activeSelf ||
                drawString.gameObject.activeSelf ||
                nockedArrow.gameObject.activeSelf ||
                staticString.forceRenderingOff ||
                drawString.forceRenderingOff ||
                nockedArrow.forceRenderingOff)
            {
                errors.Add(
                    "Generated Production Hunter must begin released: static string visible, draw string and nocked arrow inactive, and no stale auxiliary cull flags.");
            }
        }

        private static void ValidateGeneratedPresenterReferences(
            CombatantAnimationPresenter presenter,
            Renderer staticString,
            Renderer drawString,
            Renderer nockedArrow,
            List<string> errors)
        {
            if (presenter == null ||
                staticString == null ||
                drawString == null ||
                nockedArrow == null)
            {
                return;
            }

            if (GetSerializedReference(presenter, "_hunterStaticString", errors) !=
                    staticString.gameObject ||
                GetSerializedReference(presenter, "_hunterDrawString", errors) !=
                    drawString.gameObject ||
                GetSerializedReference(presenter, "_hunterNockedArrow", errors) !=
                    nockedArrow.gameObject)
            {
                errors.Add(
                    "CombatantAnimationPresenter does not reference the generated Production Hunter weapon-state renderers.");
            }
        }

        private static UnityEngine.Object GetSerializedReference(
            UnityEngine.Object target,
            string propertyName,
            List<string> errors)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                errors.Add(
                    target.GetType().Name + " has no serialized property '" +
                    propertyName + "'.");
                return null;
            }

            return property.objectReferenceValue;
        }

        private static void ThrowGeneratedErrors(IReadOnlyList<string> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }

            var builder = new StringBuilder(
                "Generated Hunter hierarchy validation failed:");
            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append("\n - ");
                builder.Append(errors[index]);
            }

            throw new InvalidOperationException(builder.ToString());
        }

        private static int ConfigureSelectedSource()
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(ModelAssetPath)
                as ModelImporter;
            TextureImporter baseColorImporter = AssetImporter.GetAtPath(
                BaseColorAssetPath) as TextureImporter;
            TextureImporter normalImporter = AssetImporter.GetAtPath(
                NormalAssetPath) as TextureImporter;
            TextureImporter maskImporter = AssetImporter.GetAtPath(
                MaskMapAssetPath) as TextureImporter;
            if (modelImporter == null ||
                baseColorImporter == null ||
                normalImporter == null ||
                maskImporter == null)
            {
                throw new InvalidOperationException(
                    "Production Hunter source files are incomplete under '" +
                    SourceFolder + "'. " + ConfigureInstruction);
            }

            int changedCount = 0;
            if (ConfigureModelImporter(modelImporter))
            {
                changedCount++;
            }

            if (ConfigureTextureImporter(
                    baseColorImporter,
                    TextureKind.BaseColor))
            {
                changedCount++;
            }

            if (ConfigureTextureImporter(normalImporter, TextureKind.Normal))
            {
                changedCount++;
            }

            if (ConfigureTextureImporter(maskImporter, TextureKind.MaskMap))
            {
                changedCount++;
            }

            AssetDatabase.SaveAssets();
            return changedCount;
        }

        private static bool ConfigureModelImporter(ModelImporter importer)
        {
            bool changed = false;
            changed |= SetValue(importer.globalScale, 1f, value => importer.globalScale = value);
            changed |= SetValue(importer.useFileScale, true, value => importer.useFileScale = value);
            changed |= SetValue(
                importer.bakeAxisConversion,
                true,
                value => importer.bakeAxisConversion = value);
            changed |= SetValue(
                importer.animationType,
                ModelImporterAnimationType.Human,
                value => importer.animationType = value);
            changed |= SetValue(
                importer.avatarSetup,
                ModelImporterAvatarSetup.CreateFromThisModel,
                value => importer.avatarSetup = value);
            changed |= SetValue(
                importer.importAnimation,
                false,
                value => importer.importAnimation = value);
            changed |= SetValue(
                importer.optimizeGameObjects,
                false,
                value => importer.optimizeGameObjects = value);
            changed |= SetValue(
                importer.importAnimatedCustomProperties,
                false,
                value => importer.importAnimatedCustomProperties = value);
            changed |= SetValue(
                importer.importBlendShapes,
                false,
                value => importer.importBlendShapes = value);
            changed |= SetValue(
                importer.importCameras,
                false,
                value => importer.importCameras = value);
            changed |= SetValue(
                importer.importLights,
                false,
                value => importer.importLights = value);
            changed |= SetValue(
                importer.importVisibility,
                false,
                value => importer.importVisibility = value);
            changed |= SetValue(
                importer.importConstraints,
                false,
                value => importer.importConstraints = value);
            changed |= SetValue(
                importer.materialImportMode,
                ModelImporterMaterialImportMode.None,
                value => importer.materialImportMode = value);
            changed |= SetValue(
                importer.isReadable,
                false,
                value => importer.isReadable = value);
            changed |= SetValue(
                importer.addCollider,
                false,
                value => importer.addCollider = value);
            changed |= SetValue(
                importer.keepQuads,
                false,
                value => importer.keepQuads = value);
            changed |= SetValue(
                importer.weldVertices,
                true,
                value => importer.weldVertices = value);
            changed |= SetValue(
                importer.optimizeMeshPolygons,
                true,
                value => importer.optimizeMeshPolygons = value);
            changed |= SetValue(
                importer.optimizeMeshVertices,
                true,
                value => importer.optimizeMeshVertices = value);
            changed |= SetValue(
                importer.meshCompression,
                ModelImporterMeshCompression.Off,
                value => importer.meshCompression = value);
            changed |= SetValue(
                importer.generateSecondaryUV,
                false,
                value => importer.generateSecondaryUV = value);
            changed |= SetValue(
                importer.importNormals,
                ModelImporterNormals.Import,
                value => importer.importNormals = value);
            changed |= SetValue(
                importer.importTangents,
                ModelImporterTangents.CalculateMikk,
                value => importer.importTangents = value);
            changed |= SetValue(
                importer.indexFormat,
                ModelImporterIndexFormat.Auto,
                value => importer.indexFormat = value);
            changed |= SetValue(
                importer.preserveHierarchy,
                true,
                value => importer.preserveHierarchy = value);
            changed |= SetValue(
                importer.sortHierarchyByName,
                true,
                value => importer.sortHierarchyByName = value);
            changed |= SetValue(
                importer.skinWeights,
                ModelImporterSkinWeights.Custom,
                value => importer.skinWeights = value);
            changed |= SetValue(
                importer.maxBonesPerVertex,
                4,
                value => importer.maxBonesPerVertex = value);

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static bool ConfigureTextureImporter(
            TextureImporter importer,
            TextureKind kind)
        {
            TextureImporterType expectedType = kind == TextureKind.Normal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            bool expectedSrgb = kind == TextureKind.BaseColor;
            TextureImporterAlphaSource expectedAlpha = kind == TextureKind.MaskMap
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;

            bool changed = false;
            changed |= SetValue(
                importer.textureType,
                expectedType,
                value => importer.textureType = value);
            changed |= SetValue(
                importer.sRGBTexture,
                expectedSrgb,
                value => importer.sRGBTexture = value);
            changed |= SetValue(
                importer.alphaSource,
                expectedAlpha,
                value => importer.alphaSource = value);
            changed |= SetValue(
                importer.mipmapEnabled,
                true,
                value => importer.mipmapEnabled = value);
            changed |= SetValue(
                importer.wrapMode,
                TextureWrapMode.Repeat,
                value => importer.wrapMode = value);
            changed |= SetValue(
                importer.filterMode,
                FilterMode.Bilinear,
                value => importer.filterMode = value);
            changed |= SetValue(importer.anisoLevel, 4, value => importer.anisoLevel = value);
            changed |= SetValue(
                importer.maxTextureSize,
                TextureSize,
                value => importer.maxTextureSize = value);
            changed |= SetValue(
                importer.npotScale,
                TextureImporterNPOTScale.ToNearest,
                value => importer.npotScale = value);
            changed |= SetValue(
                importer.textureCompression,
                TextureImporterCompression.CompressedHQ,
                value => importer.textureCompression = value);
            changed |= SetValue(
                importer.isReadable,
                false,
                value => importer.isReadable = value);

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            if (!android.overridden ||
                android.maxTextureSize != TextureSize ||
                android.format != TextureImporterFormat.Automatic ||
                android.textureCompression != TextureImporterCompression.Compressed ||
                android.compressionQuality != 65 ||
                android.crunchedCompression)
            {
                android.name = "Android";
                android.overridden = true;
                android.maxTextureSize = TextureSize;
                android.format = TextureImporterFormat.Automatic;
                android.textureCompression = TextureImporterCompression.Compressed;
                android.compressionQuality = 65;
                android.crunchedCompression = false;
                importer.SetPlatformTextureSettings(android);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static void ValidateModel(List<string> errors)
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelAssetPath)
                as ModelImporter;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAssetPath);
            if (importer == null || model == null)
            {
                errors.Add(
                    "Missing or invalid Production Hunter FBX at '" +
                    ModelAssetPath + "'. " + ConfigureInstruction);
                return;
            }

            ValidateModelImporter(importer, errors);
            if (model.transform.localPosition.sqrMagnitude > TransformTolerance ||
                Quaternion.Angle(model.transform.localRotation, Quaternion.identity) >
                TransformTolerance ||
                (model.transform.localScale - Vector3.one).sqrMagnitude >
                TransformTolerance)
            {
                errors.Add(
                    "Production Hunter FBX root transform must remain identity so the shared combatant root is authoritative.");
            }

            if (model.GetComponentsInChildren<Collider>(true).Length != 0 ||
                model.GetComponentsInChildren<Camera>(true).Length != 0 ||
                model.GetComponentsInChildren<Light>(true).Length != 0 ||
                model.GetComponentsInChildren<Animation>(true).Length != 0)
            {
                errors.Add(
                    "Production Hunter FBX may contain only its Humanoid rig and six visual renderers: no Collider, Camera, Light, or legacy Animation component.");
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != ExpectedRendererCount)
            {
                errors.Add(
                    "Production Hunter FBX contains " + renderers.Length +
                    " renderer(s); expected exactly " + ExpectedRendererCount + ".");
            }

            var rendererByName = new Dictionary<string, Renderer>(StringComparer.Ordinal);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!rendererByName.TryAdd(renderer.name, renderer))
                {
                    errors.Add(
                        "Production Hunter FBX contains duplicate renderer name '" +
                        renderer.name + "'.");
                }
            }

            for (int index = 0; index < RendererContracts.Length; index++)
            {
                ValidateRenderer(RendererContracts[index], rendererByName, errors);
            }

            ValidateRig(model, renderers, errors);
            UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(
                ModelAssetPath);
            if (importedAssets.Any(value => value is Material))
            {
                errors.Add(
                    "Production Hunter FBX imported embedded Material assets. " +
                    ConfigureInstruction);
            }
        }

        private static void ValidateModelImporter(
            ModelImporter importer,
            List<string> errors)
        {
            if (!Mathf.Approximately(importer.globalScale, 1f) ||
                !importer.useFileScale ||
                importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                importer.importAnimation ||
                importer.optimizeGameObjects ||
                !importer.bakeAxisConversion ||
                importer.importAnimatedCustomProperties ||
                importer.materialImportMode != ModelImporterMaterialImportMode.None ||
                importer.isReadable ||
                importer.addCollider ||
                importer.importCameras ||
                importer.importLights ||
                importer.importVisibility ||
                importer.importConstraints ||
                importer.importBlendShapes ||
                importer.keepQuads ||
                !importer.weldVertices ||
                !importer.optimizeMeshPolygons ||
                !importer.optimizeMeshVertices ||
                importer.meshCompression != ModelImporterMeshCompression.Off ||
                importer.generateSecondaryUV ||
                importer.importNormals != ModelImporterNormals.Import ||
                importer.importTangents != ModelImporterTangents.CalculateMikk ||
                importer.indexFormat != ModelImporterIndexFormat.Auto ||
                !importer.preserveHierarchy ||
                !importer.sortHierarchyByName ||
                importer.skinWeights != ModelImporterSkinWeights.Custom ||
                importer.maxBonesPerVertex != 4)
            {
                errors.Add(
                    "Production Hunter FBX is not using the unoptimized, animation-free, four-weight Humanoid mobile import contract. " +
                    ConfigureInstruction);
            }
        }

        private static void ValidateRenderer(
            RendererContract contract,
            IReadOnlyDictionary<string, Renderer> rendererByName,
            List<string> errors)
        {
            if (!rendererByName.TryGetValue(contract.Name, out Renderer renderer))
            {
                errors.Add(
                    "Production Hunter FBX is missing renderer '" +
                    contract.Name + "'.");
                return;
            }

            Mesh mesh = GetSharedMesh(renderer);
            if (mesh == null)
            {
                errors.Add(
                    "Production Hunter renderer '" + contract.Name +
                    "' has no mesh.");
                return;
            }

            if (contract.RequiresSkinning && renderer is not SkinnedMeshRenderer)
            {
                errors.Add(
                    "Production Hunter renderer '" + contract.Name +
                    "' must remain skinned to the shared 65-bone rig.");
            }

            if (mesh.subMeshCount != 1)
            {
                errors.Add(
                    "Production Hunter renderer '" + contract.Name +
                    "' has " + mesh.subMeshCount +
                    " submeshes; expected one shared atlas draw.");
            }

            if (!mesh.HasVertexAttribute(VertexAttribute.Normal) ||
                !mesh.HasVertexAttribute(VertexAttribute.Tangent) ||
                !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                errors.Add(
                    "Production Hunter renderer '" + contract.Name +
                    "' must retain normals, MikkTSpace tangents, and UV0.");
            }

            int triangleCount = CountTriangles(mesh);
            if (triangleCount < contract.MinimumTriangles ||
                triangleCount > contract.MaximumTriangles)
            {
                errors.Add(
                    "Production Hunter renderer '" + contract.Name +
                    "' has " + triangleCount + " triangles; expected " +
                    contract.MinimumTriangles + "-" + contract.MaximumTriangles + ".");
            }
        }

        private static void ValidateRig(
            GameObject model,
            Renderer[] renderers,
            List<string> errors)
        {
            Avatar[] validAvatars = AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath)
                .OfType<Avatar>()
                .Where(value => value.isValid && value.isHuman)
                .ToArray();
            if (validAvatars.Length != 1)
            {
                errors.Add(
                    "Production Hunter FBX exposes " + validAvatars.Length +
                    " valid Humanoid Avatar(s); expected exactly one. " +
                    ConfigureInstruction);
                return;
            }

            Avatar avatar = validAvatars[0];
            Animator[] modelAnimators = model.GetComponentsInChildren<Animator>(true);
            if (modelAnimators.Length != 1 ||
                modelAnimators[0].transform != model.transform)
            {
                errors.Add(
                    "Production Hunter FBX must expose exactly one Animator on the model root; found " +
                    modelAnimators.Length + ".");
            }
            else if (modelAnimators[0].avatar != avatar)
            {
                errors.Add(
                    "Production Hunter root Animator must reference the one valid Humanoid Avatar embedded in the FBX.");
            }

            var expectedBoneNames = new HashSet<string>(
                ExpectedRigBoneNames,
                StringComparer.Ordinal);
            var hierarchyBones = new Dictionary<string, Transform>(
                StringComparer.Ordinal);
            Transform[] hierarchy = model.GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0;
                 transformIndex < hierarchy.Length;
                 transformIndex++)
            {
                string cleanName = CleanBoneName(hierarchy[transformIndex].name);
                if (!expectedBoneNames.Contains(cleanName))
                {
                    continue;
                }

                if (!hierarchyBones.TryAdd(cleanName, hierarchy[transformIndex]))
                {
                    errors.Add(
                        "Production Hunter hierarchy contains duplicate UAL bone name '" +
                        cleanName + "'. Every renderer must share one canonical skeleton.");
                }
            }

            if (hierarchyBones.Count != ExpectedBoneCount)
            {
                string[] missingBones = ExpectedRigBoneNames
                    .Where(value => !hierarchyBones.ContainsKey(value))
                    .ToArray();
                errors.Add(
                    "Production Hunter hierarchy exposes " +
                    hierarchyBones.Count + " of the exact " + ExpectedBoneCount +
                    " UAL bones. Missing: " + string.Join(", ", missingBones) + ".");
            }

            hierarchyBones.TryGetValue("root", out Transform canonicalRootBone);

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                if (renderers[rendererIndex] is not SkinnedMeshRenderer skinned)
                {
                    continue;
                }

                if (skinned.sharedMesh == null ||
                    skinned.rootBone == null ||
                    skinned.bones == null ||
                    skinned.bones.Length == 0 ||
                    skinned.bones.Any(value => value == null))
                {
                    errors.Add(
                        "Production Hunter renderer '" + skinned.name +
                        "' has an incomplete skin binding.");
                    continue;
                }

                if (canonicalRootBone != null && skinned.rootBone != canonicalRootBone)
                {
                    errors.Add(
                        "Production Hunter renderer '" + skinned.name +
                        "' is rooted on '" + CleanBoneName(skinned.rootBone.name) +
                        "' instead of the canonical UAL root bone.");
                }

                var uniqueRendererBones = new HashSet<Transform>();
                for (int boneIndex = 0; boneIndex < skinned.bones.Length; boneIndex++)
                {
                    Transform rendererBone = skinned.bones[boneIndex];
                    string boneName = CleanBoneName(rendererBone.name);
                    if (!hierarchyBones.TryGetValue(
                            boneName,
                            out Transform canonicalBone) ||
                        canonicalBone != rendererBone)
                    {
                        errors.Add(
                            "Production Hunter renderer '" + skinned.name +
                            "' binds bone '" + boneName +
                            "' outside the canonical 65-bone skeleton.");
                    }

                    if (!uniqueRendererBones.Add(rendererBone))
                    {
                        errors.Add(
                            "Production Hunter renderer '" + skinned.name +
                            "' contains duplicate binding for bone '" + boneName + "'.");
                    }
                }
            }

            GameObject instance = UnityEngine.Object.Instantiate(model);
            try
            {
                Animator[] instanceAnimators = instance.GetComponentsInChildren<Animator>(true);
                if (instanceAnimators.Length != 1 ||
                    instanceAnimators[0].transform != instance.transform)
                {
                    return;
                }

                Animator animator = instanceAnimators[0];
                for (int index = 0; index < RequiredHumanoidBones.Length; index++)
                {
                    HumanBodyBones bone = RequiredHumanoidBones[index];
                    if (animator.GetBoneTransform(bone) == null)
                    {
                        errors.Add(
                            "Production Hunter Humanoid mapping is missing '" +
                            bone + "'.");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateTexture(
            string assetPath,
            TextureKind kind,
            List<string> errors)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath)
                as TextureImporter;
            if (texture == null || importer == null)
            {
                errors.Add(
                    "Missing Production Hunter " + kind + " texture at '" +
                    assetPath + "'.");
                return;
            }

            TextureImporterType expectedType = kind == TextureKind.Normal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            bool expectedSrgb = kind == TextureKind.BaseColor;
            TextureImporterAlphaSource expectedAlpha = kind == TextureKind.MaskMap
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            if (texture.width != TextureSize ||
                texture.height != TextureSize ||
                importer.textureType != expectedType ||
                importer.sRGBTexture != expectedSrgb ||
                importer.alphaSource != expectedAlpha ||
                !importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Repeat ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.anisoLevel != 4 ||
                importer.maxTextureSize != TextureSize ||
                importer.npotScale != TextureImporterNPOTScale.ToNearest ||
                importer.textureCompression != TextureImporterCompression.CompressedHQ ||
                importer.isReadable ||
                !android.overridden ||
                android.maxTextureSize != TextureSize ||
                android.format != TextureImporterFormat.Automatic ||
                android.textureCompression != TextureImporterCompression.Compressed ||
                android.compressionQuality != 65 ||
                android.crunchedCompression)
            {
                errors.Add(
                    "Production Hunter " + kind + " texture at '" +
                    assetPath + "' is not using the audited 1024 px Android import contract. " +
                    ConfigureInstruction);
            }
        }

        private static Mesh GetSharedMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static int CountTriangles(Mesh mesh)
        {
            long indexCount = 0;
            for (int index = 0; index < mesh.subMeshCount; index++)
            {
                indexCount += (long)mesh.GetIndexCount(index);
            }

            return indexCount > int.MaxValue
                ? int.MaxValue
                : (int)(indexCount / 3L);
        }

        private static string CleanBoneName(string value)
        {
            int separator = value.LastIndexOf(':');
            return separator >= 0 ? value.Substring(separator + 1) : value;
        }

        private static string BuildErrorMessage(IReadOnlyList<string> errors)
        {
            var builder = new StringBuilder(
                "Production Hunter source validation failed:");
            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append("\n - ");
                builder.Append(errors[index]);
            }

            return builder.ToString();
        }

        private static bool SetValue<T>(
            T current,
            T expected,
            Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, expected))
            {
                return false;
            }

            setter(expected);
            return true;
        }

        private static void ShowDialog(string title, string message)
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(title, message, "OK");
            }
        }

        private readonly struct RendererContract
        {
            public RendererContract(
                string name,
                int minimumTriangles,
                int maximumTriangles,
                bool requiresSkinning)
            {
                Name = name;
                MinimumTriangles = minimumTriangles;
                MaximumTriangles = maximumTriangles;
                RequiresSkinning = requiresSkinning;
            }

            public string Name { get; }

            public int MinimumTriangles { get; }

            public int MaximumTriangles { get; }

            public bool RequiresSkinning { get; }
        }

        private enum TextureKind
        {
            BaseColor,
            Normal,
            MaskMap,
        }
    }
}
