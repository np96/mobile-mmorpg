using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class CombatantCharacterHierarchyPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClassChanges_KeepExactlyOneActiveCharacterModelHierarchyPerCombatant()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller, Is.Not.Null);

            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Mage,
                SpecializationId.Fire);
            yield return null;
            AssertSingleCharacterHierarchyForEveryCombatant();

            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Rogue,
                SpecializationId.Mutilate);
            yield return null;
            AssertSingleCharacterHierarchyForEveryCombatant();

            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return null;
            AssertSingleCharacterHierarchyForEveryCombatant();

            controller.StartLocalSession(
                GameModeId.Boss,
                ClassId.Healer,
                SpecializationId.Discipline);
            yield return null;
            AssertSingleCharacterHierarchyForEveryCombatant();
        }

        [UnityTest]
        public IEnumerator PlayableClasses_HaveReadableLitSilhouetteAttachments()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller, Is.Not.Null);

            ClassId[] classes =
            {
                ClassId.Healer,
                ClassId.Rogue,
                ClassId.Mage,
                ClassId.Hunter,
            };
            SpecializationId[] specializations =
            {
                SpecializationId.Discipline,
                SpecializationId.Mutilate,
                SpecializationId.Fire,
                SpecializationId.Marksmanship,
            };
            float[] authoredVisualScales =
            {
                1.48f,
                1.46f,
                1.48f,
                1.46f,
            };
            string[][] requiredGearNames =
            {
                new[] { "PriestMantle_Left", "PriestReliquary" },
                new[] { "RoguePauldron_Left", "RogueShoulderBlade_Right" },
                new[] { "MageCrystal_Left", "MageFocusCrystal" },
                new[]
                {
                    "HunterBowGrip",
                    "HunterBowTip_Upper",
                    "HunterQuiver",
                    "HunterPauldron_Left",
                },
            };
            string[][] requiredGearMaterialNames =
            {
                new[] { "Priest_ArmorAccent", "Priest_ArmorAccent" },
                new[] { "Rogue_ArmorAccent", "Rogue_ArmorAccent" },
                new[] { "Mage_ArmorAccent", "Mage_ArmorAccent" },
                new[]
                {
                    "Hunter_Leather",
                    "Hunter_WeaponBronze",
                    "Hunter_Leather",
                    "Hunter_ArmorAccent",
                },
            };
            string[][] silhouetteMaterialNames =
            {
                new[] { "Priest_ArmorAccent" },
                new[] { "Rogue_ArmorAccent" },
                new[] { "Mage_ArmorAccent" },
                new[]
                {
                    "Hunter_ArmorAccent",
                    "Hunter_WeaponBronze",
                    "Hunter_Leather",
                },
            };

            for (int classIndex = 0; classIndex < classes.Length; classIndex++)
            {
                controller.StartLocalSession(
                    GameModeId.Arena,
                    classes[classIndex],
                    specializations[classIndex]);
                yield return null;

                CombatantView human = Object
                    .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                    .Single(value => value.IsHuman);
                Transform characterRoot = human.ActiveCharacterModelRoot;
                Assert.That(characterRoot, Is.Not.Null);
                CombatantAnimationPresenter animationPresenter =
                    human.GetComponent<CombatantAnimationPresenter>();
                Assert.That(animationPresenter, Is.Not.Null);
                bool mixamoPlayableClassTrial =
                    IsMixamoPlayableClassTrialRoot(
                        classes[classIndex],
                        characterRoot.name);
                float authoredScale;
                if (mixamoPlayableClassTrial)
                {
                    authoredScale = classes[classIndex] switch
                    {
                        ClassId.Priest => 0.881121f,
                        ClassId.Rogue => 0.855019f,
                        ClassId.Mage => 0.811476f,
                        _ => throw new AssertionException(
                            "Unexpected Mixamo playable-class trial " + classes[classIndex] + "."),
                    };
                }
                else
                {
                    authoredScale =
                        classes[classIndex] == ClassId.Hunter &&
                        characterRoot.name == "HunterModel_MixamoErikaTrial"
                            ? 1.0f
                            : authoredVisualScales[classIndex];
                }
                Assert.That(
                    animationPresenter.AuthoredVisualScale.x,
                    Is.EqualTo(authoredScale).Within(0.001f),
                    classes[classIndex] +
                    " is too small relative to the arena wardstones at strategy-camera distance.");
                AssertUniformScale(
                    animationPresenter.AuthoredVisualScale,
                    authoredScale,
                    classes[classIndex] + " authored visual scale");
                AssertUniformScale(
                    animationPresenter.ConfiguredVisualScale,
                    authoredScale * 1.08f,
                    classes[classIndex] + " human visual scale");

                // Relation readability is a visual-only multiplier. Exercise the
                // three playable relations directly so this contract cannot drift
                // into the combatant root or selection capsule during later art
                // work.
                animationPresenter.ConfigureRelation(false, true, false);
                AssertUniformScale(
                    animationPresenter.ConfiguredVisualScale,
                    authoredScale,
                    classes[classIndex] + " ally visual scale");
                animationPresenter.ConfigureRelation(false, false, false);
                AssertVectorScale(
                    animationPresenter.ConfiguredVisualScale,
                    new Vector3(
                        authoredScale * 1.04f,
                        authoredScale * 1.07f,
                        authoredScale * 1.04f),
                    classes[classIndex] + " enemy visual scale");
                animationPresenter.ConfigureRelation(true, true, false);

                AssertPlayableRootAndReadabilitySeparation(human, animationPresenter);

                Transform contactShadow = human.transform.Find("ContactShadow");
                Assert.That(contactShadow, Is.Not.Null);
                Assert.That(
                    contactShadow.localScale.x,
                    Is.GreaterThanOrEqualTo(0.9f),
                    "The readable character scale requires a matching planted contact shadow.");

                bool productionHunter = classes[classIndex] == ClassId.Hunter &&
                    characterRoot.name == "HunterModel_ProductionV1";
                bool trialHunter = classes[classIndex] == ClassId.Hunter &&
                    characterRoot.name == "HunterModel_MixamoErikaTrial";
                if (productionHunter)
                {
                    AssertProductionHunterContract(characterRoot);
                }
                else if (trialHunter)
                {
                    AssertMixamoErikaTrialContract(characterRoot);
                }
                else if (mixamoPlayableClassTrial)
                {
                    AssertMixamoPlayableClassTrialContract(
                        classes[classIndex],
                        characterRoot);
                }
                else
                {
                    for (int armorIndex = 0;
                         armorIndex < requiredGearNames[classIndex].Length;
                         armorIndex++)
                    {
                        string armorName = requiredGearNames[classIndex][armorIndex];
                        string expectedMaterialName =
                            requiredGearMaterialNames[classIndex][armorIndex];
                        Transform armor = FindDescendant(characterRoot, armorName);
                        Assert.That(
                            armor,
                            Is.Not.Null,
                            classes[classIndex] +
                            " is missing silhouette piece " + armorName + ".");
                        Renderer renderer = armor.GetComponent<Renderer>();
                        Assert.That(renderer, Is.Not.Null, armorName + " has no renderer.");
                        Material[] sharedMaterials = renderer.sharedMaterials;
                        Assert.That(
                            sharedMaterials.All(value =>
                                value != null &&
                                value.shader.name == "Universal Render Pipeline/Lit"),
                            Is.True,
                            armorName + " must use shared URP/Lit materials.");
                        Assert.That(
                            sharedMaterials.Any(value =>
                                value.name == expectedMaterialName),
                            Is.True,
                            armorName + " is missing material " +
                            expectedMaterialName + ".");
                        Assert.That(
                            renderer.shadowCastingMode,
                            Is.EqualTo(ShadowCastingMode.On));
                        Assert.That(renderer.receiveShadows, Is.True);
                    }

                    Renderer[] accentRenderers = characterRoot
                        .GetComponentsInChildren<Renderer>(true)
                        .Where(value =>
                            value is MeshRenderer &&
                            value.sharedMaterials.Any(material =>
                                material != null &&
                                silhouetteMaterialNames[classIndex].Contains(material.name)))
                        .ToArray();
                    Assert.That(
                        accentRenderers.Length,
                        classes[classIndex] == ClassId.Hunter
                            ? Is.InRange(5, 7)
                            : Is.InRange(3, 6),
                        classes[classIndex] +
                        " silhouette accents must remain legible but mobile-budget bounded.");

                    if (classes[classIndex] == ClassId.Hunter)
                    {
                        AssertHunterMaterialAndWeaponRead(characterRoot);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator Boss_KeepsItsAuthoredVisualAndSelectionGeometry()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller, Is.Not.Null);

            controller.StartLocalSession(
                GameModeId.Boss,
                ClassId.Healer,
                SpecializationId.Discipline);
            yield return null;

            CombatantView boss = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.Class == ClassId.Boss);
            CombatantAnimationPresenter presenter =
                boss.GetComponent<CombatantAnimationPresenter>();
            Assert.That(presenter, Is.Not.Null);
            AssertVectorScale(
                presenter.AuthoredVisualScale,
                new Vector3(1.12f, 1.02f, 1.12f),
                "boss authored visual scale");
            AssertVectorScale(
                presenter.ConfiguredVisualScale,
                new Vector3(1.12f, 1.02f, 1.12f),
                "boss configured visual scale");
            Assert.That(boss.transform.localScale, Is.EqualTo(Vector3.one));

            CapsuleCollider targetCollider = boss.GetComponent<CapsuleCollider>();
            Assert.That(targetCollider, Is.Not.Null);
            Assert.That(targetCollider.center, Is.EqualTo(new Vector3(0f, 1.5f, 0f)));
            Assert.That(targetCollider.radius, Is.EqualTo(1.05f).Within(0.001f));
            Assert.That(targetCollider.height, Is.EqualTo(3f).Within(0.001f));

            Assert.That(
                boss.transform.Find("WorldStatusBars").localPosition,
                Is.EqualTo(new Vector3(0f, 2.30f, 0f)));
            Assert.That(
                boss.transform.Find("RelationMarker").localPosition,
                Is.EqualTo(new Vector3(0f, 2.68f, 0f)));
            Assert.That(
                boss.transform.Find("HumanMarker_DiamondChevron").localPosition,
                Is.EqualTo(new Vector3(0f, 2.70f, 0f)));
        }

        private static void AssertHunterMaterialAndWeaponRead(Transform characterRoot)
        {
            Transform bowLimb = FindDescendant(characterRoot, "HunterBowLimb");
            Transform bowString = FindDescendant(characterRoot, "HunterBowString");
            Assert.That(bowLimb, Is.Not.Null);
            Assert.That(bowString, Is.Not.Null);
            Assert.That(
                bowLimb.GetComponent<LineRenderer>().widthMultiplier,
                Is.EqualTo(0.095f).Within(0.001f),
                "The Hunter weapon must retain enough projected width to frame the body.");
            Assert.That(
                bowString.GetComponent<LineRenderer>().widthMultiplier,
                Is.EqualTo(0.013f).Within(0.001f),
                "The bow string must stay subordinate to the bronze limb.");

            Material[] authoredMaterials = characterRoot
                .GetComponentsInChildren<Renderer>(true)
                .SelectMany(value => value.sharedMaterials)
                .Where(value => value != null)
                .ToArray();
            Material outfit = authoredMaterials.FirstOrDefault(
                value => value.name == "Hunter_Outfit");
            Assert.That(outfit, Is.Not.Null);
            AssertMaterialColor(
                outfit,
                "_BaseColor",
                Color.white,
                "Hunter outfit tint");

            Material armor = authoredMaterials.FirstOrDefault(
                value => value.name == "Hunter_ArmorAccent");
            Assert.That(armor, Is.Not.Null);
            AssertMaterialColor(
                armor,
                "_BaseColor",
                new Color(0.28f, 0.52f, 0.10f, 1f),
                "Hunter armor accent");

            Material weapon = authoredMaterials.FirstOrDefault(
                value => value.name == "Hunter_WeaponBronze");
            Assert.That(weapon, Is.Not.Null);
            AssertMaterialColor(
                weapon,
                "_BaseColor",
                new Color(0.96f, 0.68f, 0.18f, 1f),
                "Hunter weapon bronze");
            AssertMaterialColor(
                weapon,
                "_EmissionColor",
                Color.clear,
                "Hunter weapon non-emissive mobile material");
            LocalKeyword emissionKeyword = new LocalKeyword(
                weapon.shader,
                "_EMISSION");
            Assert.That(emissionKeyword.isValid, Is.True);
            Assert.That(weapon.IsKeywordEnabled(emissionKeyword), Is.False);
        }

        private static void AssertMixamoErikaTrialContract(Transform characterRoot)
        {
            Animator animator = characterRoot.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);

            // The imported FBX's renderer names do not describe their semantic
            // contents. These audited mappings prevent a future cleanup from
            // hiding the clothing/body instead of the rejected embedded props.
            string[] activeSourceRendererNames =
            {
                "Erika_Archer_Arrow_Mesh",       // clothes
                "Erika_Archer_Clothes_Mesh",     // eyelashes
                "Erika_Archer_Eyelashes_Mesh",   // body
                "Erika_Archer_Bow_Mesh",         // eyes
            };
            string[] inactiveSourceRendererNames =
            {
                "Erika_Archer_Body_Mesh",        // embedded bow
                "Erika_Archer_Eyes_Mesh",        // embedded arrow
            };

            for (int index = 0; index < activeSourceRendererNames.Length; index++)
            {
                Transform sourcePart = FindDescendant(
                    characterRoot,
                    activeSourceRendererNames[index]);
                Assert.That(sourcePart, Is.Not.Null, activeSourceRendererNames[index]);
                Assert.That(sourcePart.gameObject.activeSelf, Is.True);
                SkinnedMeshRenderer renderer = sourcePart.GetComponent<SkinnedMeshRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.quality, Is.EqualTo(SkinQuality.Bone4));
                Assert.That(
                    renderer.sharedMaterials.All(material =>
                        material != null &&
                        material.shader != null &&
                        material.shader.name == "Universal Render Pipeline/Lit"),
                    Is.True);
            }

            for (int index = 0; index < inactiveSourceRendererNames.Length; index++)
            {
                Transform sourcePart = FindDescendant(
                    characterRoot,
                    inactiveSourceRendererNames[index]);
                if (sourcePart != null)
                {
                    Assert.That(
                        sourcePart.gameObject.activeInHierarchy,
                        Is.False,
                        inactiveSourceRendererNames[index] +
                        " is an embedded trial prop and must remain hidden.");
                }
            }

            Transform projectBow = FindDescendant(characterRoot, "HunterBow_Hand");
            Transform projectArrow = FindDescendant(characterRoot, "HunterArrow_Hand");
            Assert.That(projectBow, Is.Not.Null);
            Assert.That(projectArrow, Is.Not.Null);
            Assert.That(projectBow.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                projectArrow.gameObject.activeSelf,
                Is.False,
                "The project arrow is nocked only during an active Hunter draw.");
            Assert.That(
                projectBow.IsChildOf(animator.GetBoneTransform(HumanBodyBones.LeftHand)),
                Is.True,
                "The trial must use the authored project bow socket.");
            Assert.That(
                projectArrow.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightHand)),
                Is.True,
                "The trial must use the authored project nocked-arrow socket.");
            MeshFilter bowFilter = projectBow.GetComponent<MeshFilter>();
            MeshFilter arrowFilter = projectArrow.GetComponent<MeshFilter>();
            MeshRenderer bowRenderer = projectBow.GetComponent<MeshRenderer>();
            MeshRenderer arrowRenderer = projectArrow.GetComponent<MeshRenderer>();
            Assert.That(bowFilter, Is.Not.Null);
            Assert.That(arrowFilter, Is.Not.Null);
            Assert.That(bowFilter.sharedMesh, Is.Not.Null);
            Assert.That(arrowFilter.sharedMesh, Is.Not.Null);
            Assert.That(
                bowFilter.sharedMesh.bounds.size.z,
                Is.InRange(1.30f, 1.42f),
                "The trial must retain Erika's full authored bow silhouette.");
            Assert.That(
                arrowFilter.sharedMesh.bounds.size.z,
                Is.InRange(0.72f, 0.80f),
                "The nocked arrow must retain the authored shaft length.");
            Assert.That(bowRenderer.sharedMaterial.name, Is.EqualTo("MixamoErikaTrial_EmbeddedBow"));
            Assert.That(arrowRenderer.sharedMaterial.name, Is.EqualTo("MixamoErikaTrial_EmbeddedArrow"));
            Assert.That(projectBow.GetComponent<LineRenderer>(), Is.Null);
            HunterTrialWeaponPose weaponPose =
                characterRoot.GetComponent<HunterTrialWeaponPose>();
            Assert.That(weaponPose, Is.Not.Null);
            Assert.That(weaponPose.OrientationFrame, Is.EqualTo(characterRoot));
            Assert.That(weaponPose.LeftHand, Is.EqualTo(animator.GetBoneTransform(HumanBodyBones.LeftHand)));
            Assert.That(weaponPose.RightHand, Is.EqualTo(animator.GetBoneTransform(HumanBodyBones.RightHand)));
            Assert.That(weaponPose.Bow, Is.EqualTo(projectBow));
            Assert.That(weaponPose.NockedArrow, Is.EqualTo(projectArrow));
            Assert.That(
                Vector3.Dot(projectBow.forward, characterRoot.up),
                Is.GreaterThan(0.98f),
                "The rigid trial bow's authored long axis must remain vertical.");
            Assert.That(
                Vector3.Dot(projectBow.up, characterRoot.forward),
                Is.GreaterThan(0.90f),
                "The resting trial bow must present its silhouette to the strategy camera.");
            Assert.That(FindDescendant(characterRoot, "HunterQuiver"), Is.Null);
            Assert.That(FindDescendant(characterRoot, "HunterPauldron_Left"), Is.Null);
        }

        private static void AssertProductionHunterContract(Transform characterRoot)
        {
            string[] rendererNames =
            {
                "HunterProductionV3_LOD0",
                "HunterProductionV3_LOD1",
                "HunterProductionV3_LOD2",
                "HunterProductionV3_StaticString",
                "HunterProductionV3_DrawString",
                "HunterProductionV3_NockedArrow",
            };
            Renderer[] renderers = characterRoot.GetComponentsInChildren<Renderer>(true);
            Assert.That(
                renderers,
                Has.Length.EqualTo(rendererNames.Length),
                "Production Hunter must retain its six-renderer source contract.");
            for (int index = 0; index < rendererNames.Length; index++)
            {
                Renderer renderer = renderers.SingleOrDefault(value =>
                    value.name == rendererNames[index]);
                Assert.That(renderer, Is.Not.Null, rendererNames[index]);
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.name, Is.EqualTo("HunterProductionV1"));
                Assert.That(
                    renderer.sharedMaterial.shader.name,
                    Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
                Assert.That(renderer.receiveShadows, Is.True);
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    Assert.That(
                        skinnedRenderer.quality,
                        Is.EqualTo(SkinQuality.Bone4),
                        renderer.name +
                        " must retain its authored four-weight deformation on every quality tier.");
                }
            }

            Assert.That(
                renderers.Select(value => value.sharedMaterial).Distinct().Count(),
                Is.EqualTo(1),
                "All Production Hunter renderers must share one atlas material.");
            Material productionMaterial = renderers[0].sharedMaterial;
            Assert.That(productionMaterial.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(productionMaterial.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(productionMaterial.GetTexture("_MetallicGlossMap"), Is.Not.Null);
            Assert.That(
                productionMaterial.GetTexture("_OcclusionMap"),
                Is.SameAs(productionMaterial.GetTexture("_MetallicGlossMap")),
                "The direct URP mask packs metallic in R, occlusion in G, and smoothness in A.");
            Assert.That(productionMaterial.GetFloat("_WorkflowMode"), Is.EqualTo(1f));
            Assert.That(
                productionMaterial.GetFloat("_SmoothnessTextureChannel"),
                Is.EqualTo(0f));
            Assert.That(productionMaterial.IsKeywordEnabled("_SPECULAR_SETUP"), Is.False);
            Assert.That(
                productionMaterial.IsKeywordEnabled(
                    "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"),
                Is.False);
            LODGroup lodGroup = characterRoot.GetComponent<LODGroup>();
            Assert.That(lodGroup, Is.Not.Null);
            Assert.That(lodGroup.fadeMode, Is.EqualTo(LODFadeMode.None));
            Assert.That(
                characterRoot.GetComponent<Animator>().cullingMode,
                Is.EqualTo(AnimatorCullingMode.CullUpdateTransforms),
                "Once body and auxiliary renderers cull, Hunter animation work must cull too.");
            LOD[] lods = lodGroup.GetLODs();
            Assert.That(lods, Has.Length.EqualTo(3));
            float[] expectedTransitions = { 0.10f, 0.055f, 0.02f };
            for (int index = 0; index < lods.Length; index++)
            {
                Assert.That(lods[index].renderers, Has.Length.EqualTo(1));
                Assert.That(
                    lods[index].renderers[0].name,
                    Is.EqualTo("HunterProductionV3_LOD" + index));
                Assert.That(
                    lods[index].screenRelativeTransitionHeight,
                    Is.EqualTo(expectedTransitions[index]).Within(0.001f));
            }

            Camera gameplayCamera = Object.FindAnyObjectByType<Camera>();
            Assert.That(gameplayCamera, Is.Not.Null);
            Bounds lod0Bounds = lods[0].renderers[0].bounds;
            Vector3 screenTop = gameplayCamera.WorldToScreenPoint(
                lod0Bounds.center + (Vector3.up * lod0Bounds.extents.y));
            Vector3 screenBottom = gameplayCamera.WorldToScreenPoint(
                lod0Bounds.center - (Vector3.up * lod0Bounds.extents.y));
            float projectedHeight = Mathf.Abs(screenTop.y - screenBottom.y) /
                Mathf.Max(1f, gameplayCamera.pixelHeight);
            Assert.That(
                projectedHeight,
                Is.GreaterThanOrEqualTo(lods[0].screenRelativeTransitionHeight),
                "The locked strategy framing must keep the human Production Hunter on LOD0.");

            HunterAuxiliaryLodCuller auxiliaryCuller =
                characterRoot.GetComponent<HunterAuxiliaryLodCuller>();
            Assert.That(auxiliaryCuller, Is.Not.Null);
            Assert.That(auxiliaryCuller.TargetLodGroup, Is.SameAs(lodGroup));
            Assert.That(auxiliaryCuller.AuxiliaryRendererCount, Is.EqualTo(3));
            Assert.That(
                auxiliaryCuller.CullScreenRelativeHeight,
                Is.EqualTo(0.02f).Within(0.0001f));
            AssertProductionHunterAuxiliaryCulling(
                auxiliaryCuller,
                gameplayCamera,
                lodGroup,
                lods);

            Assert.That(
                FindDescendant(characterRoot, "HunterProductionV3_StaticString")
                    .gameObject.activeSelf,
                Is.True);
            Assert.That(
                FindDescendant(characterRoot, "HunterProductionV3_DrawString")
                    .gameObject.activeSelf,
                Is.False);
            Assert.That(
                FindDescendant(characterRoot, "HunterProductionV3_NockedArrow")
                    .gameObject.activeSelf,
                Is.False);
        }

        private static bool IsMixamoPlayableClassTrialRoot(
            ClassId classId,
            string rootName)
        {
            switch (classId)
            {
                case ClassId.Healer:
                    return rootName == "PriestModel_MixamoUrielTrial";
                case ClassId.Rogue:
                    return rootName == "RogueModel_MixamoVampireTrial";
                case ClassId.Mage:
                    return rootName == "MageModel_MixamoDemonTrial";
                default:
                    return false;
            }
        }

        private static void AssertMixamoPlayableClassTrialContract(
            ClassId classId,
            Transform characterRoot)
        {
            string expectedRootName;
            string expectedControllerName;
            string[] rejectedPrototypeAttachments;
            switch (classId)
            {
                case ClassId.Healer:
                    expectedRootName = "PriestModel_MixamoUrielTrial";
                    expectedControllerName = "Healer";
                    rejectedPrototypeAttachments = new[]
                    {
                        "PriestStaff_Hand",
                        "PriestScripture_Hand",
                        "PriestMantle_Left",
                        "PriestMantle_Right",
                        "PriestReliquary",
                        "PriestCrown",
                    };
                    break;
                case ClassId.Rogue:
                    expectedRootName = "RogueModel_MixamoVampireTrial";
                    expectedControllerName = "Rogue";
                    rejectedPrototypeAttachments = new[]
                    {
                        "RogueDagger_LeftHand",
                        "RogueDagger_RightHand",
                        "RoguePauldron_Left",
                        "RoguePauldron_Right",
                        "RogueShoulderBlade_Left",
                        "RogueShoulderBlade_Right",
                    };
                    break;
                case ClassId.Mage:
                    expectedRootName = "MageModel_MixamoDemonTrial";
                    expectedControllerName = "Mage";
                    rejectedPrototypeAttachments = new[]
                    {
                        "MageStaff_Hand",
                        "MageWand_Offhand",
                        "MageCrystal_Left",
                        "MageCrystal_Right",
                        "MageFocusCrystal",
                    };
                    break;
                default:
                    Assert.Fail(classId + " has no non-Hunter Mixamo trial contract.");
                    return;
            }

            Assert.That(characterRoot.name, Is.EqualTo(expectedRootName));
            Assert.That(
                characterRoot.parent.Find(
                    classId == ClassId.Healer
                        ? "PriestModel_Quaternius"
                        : classId + "Model_Quaternius"),
                Is.Null,
                expectedRootName +
                " must replace, not coexist with, its generated Quaternius root.");
            Animator[] animators = characterRoot
                .GetComponentsInChildren<Animator>(true);
            Assert.That(animators, Has.Length.EqualTo(1));
            Animator animator = animators[0];
            Assert.That(animator.transform, Is.SameAs(characterRoot));
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(
                animator.runtimeAnimatorController.name,
                Is.EqualTo(expectedControllerName));

            HumanBodyBones[] requiredBones =
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
            for (int index = 0; index < requiredBones.Length; index++)
            {
                Assert.That(
                    animator.GetBoneTransform(requiredBones[index]),
                    Is.Not.Null,
                    expectedRootName + " is missing Humanoid bone " +
                    requiredBones[index] + ".");
            }

            SkinnedMeshRenderer[] renderers = characterRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(
                renderers.Length,
                Is.GreaterThanOrEqualTo(1),
                expectedRootName + " has no skinned visual renderer.");
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                Assert.That(renderer.sharedMesh, Is.Not.Null, renderer.name);
                if (classId == ClassId.Rogue)
                {
                    Assert.That(
                        renderer.enabled,
                        Is.False,
                        renderer.name +
                        " should honor the local Rogue's real starting-stealth presentation.");
                }
                else
                {
                    Assert.That(renderer.enabled, Is.True, renderer.name);
                }
                Assert.That(renderer.gameObject.activeInHierarchy, Is.True, renderer.name);
                Assert.That(renderer.quality, Is.EqualTo(SkinQuality.Bone4), renderer.name);
                Assert.That(
                    renderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.On),
                    renderer.name);
                Assert.That(renderer.receiveShadows, Is.True, renderer.name);
                Assert.That(renderer.sharedMaterials, Is.Not.Empty, renderer.name);
                Assert.That(
                    renderer.sharedMaterials.All(value =>
                        value != null &&
                        value.shader != null &&
                        value.shader.name == "Universal Render Pipeline/Lit"),
                    Is.True,
                    renderer.name + " must use complete generated URP/Lit materials.");
            }

            Assert.That(
                characterRoot.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                expectedRootName + " must not add presentation collision.");
            for (int index = 0;
                 index < rejectedPrototypeAttachments.Length;
                 index++)
            {
                Assert.That(
                    FindDescendant(
                        characterRoot,
                        rejectedPrototypeAttachments[index]),
                    Is.Null,
                    expectedRootName + " retained rejected prototype attachment " +
                    rejectedPrototypeAttachments[index] + ".");
            }
        }

        private static void AssertProductionHunterAuxiliaryCulling(
            HunterAuxiliaryLodCuller culler,
            Camera camera,
            LODGroup lodGroup,
            IReadOnlyList<LOD> lods)
        {
            Vector3 originalPosition = camera.transform.position;
            Quaternion originalRotation = camera.transform.rotation;
            Vector3 referencePoint = lodGroup.transform.TransformPoint(
                lodGroup.localReferencePoint);
            Vector3 cameraDirection = originalPosition - referencePoint;
            if (cameraDirection.sqrMagnitude <= 0.0001f)
            {
                cameraDirection = -camera.transform.forward;
            }

            cameraDirection.Normalize();
            try
            {
                for (int lodIndex = 0; lodIndex < lods.Count; lodIndex++)
                {
                    float transition = lods[lodIndex].screenRelativeTransitionHeight;
                    SetCameraForScreenRelativeHeight(
                        camera,
                        lodGroup,
                        referencePoint,
                        cameraDirection,
                        transition + 0.001f);
                    culler.RefreshForCamera(camera);
                    Assert.That(
                        culler.AreAuxiliaryRenderersCulled,
                        Is.False,
                        "Auxiliary weapon renderers culled above LOD" +
                        lodIndex + " transition " + transition + ".");

                    SetCameraForScreenRelativeHeight(
                        camera,
                        lodGroup,
                        referencePoint,
                        cameraDirection,
                        transition);
                    culler.RefreshForCamera(camera);
                    Assert.That(
                        culler.AreAuxiliaryRenderersCulled,
                        Is.False,
                        "Auxiliary culling must match Unity's exclusive final " +
                        "LOD threshold at LOD" + lodIndex + ".");

                    SetCameraForScreenRelativeHeight(
                        camera,
                        lodGroup,
                        referencePoint,
                        cameraDirection,
                        transition - 0.001f);
                    culler.RefreshForCamera(camera);
                    Assert.That(
                        culler.AreAuxiliaryRenderersCulled,
                        Is.EqualTo(lodIndex == lods.Count - 1),
                        "Only the final LOD threshold may cull auxiliary weapon renderers.");
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(
                    originalPosition,
                    originalRotation);
                culler.RefreshForCamera(camera);
            }
        }

        private static void SetCameraForScreenRelativeHeight(
            Camera camera,
            LODGroup lodGroup,
            Vector3 referencePoint,
            Vector3 cameraDirection,
            float screenRelativeHeight)
        {
            Vector3 scale = lodGroup.transform.lossyScale;
            float largestAxis = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            float worldSpaceSize = lodGroup.size * largestAxis;
            float lodBias = Mathf.Max(QualitySettings.lodBias, 0.0001f);
            float halfAngle = Mathf.Tan(
                camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = worldSpaceSize * lodBias /
                (2f * screenRelativeHeight * halfAngle);
            camera.transform.position = referencePoint + (cameraDirection * distance);
            Assert.That(
                lodGroup.gameObject
                    .GetComponent<HunterAuxiliaryLodCuller>()
                    .CalculateScreenRelativeHeight(camera),
                Is.EqualTo(screenRelativeHeight).Within(0.0002f));
        }

        private static void AssertPlayableRootAndReadabilitySeparation(
            CombatantView combatant,
            CombatantAnimationPresenter presenter)
        {
            Assert.That(combatant.transform.localScale, Is.EqualTo(Vector3.one));

            Transform characterRoot = combatant.ActiveCharacterModelRoot;
            Assert.That(characterRoot.parent, Is.SameAs(combatant.transform));

            CapsuleCollider targetCollider = combatant.GetComponent<CapsuleCollider>();
            Assert.That(targetCollider, Is.Not.Null);
            Assert.That(targetCollider.transform, Is.SameAs(combatant.transform));
            Assert.That(targetCollider.center, Is.EqualTo(new Vector3(0f, 1.175f, 0f)));
            Assert.That(targetCollider.radius, Is.EqualTo(0.72f).Within(0.001f));
            Assert.That(targetCollider.height, Is.EqualTo(2.35f).Within(0.001f));
            Assert.That(targetCollider.direction, Is.EqualTo(1));
            Assert.That(targetCollider.isTrigger, Is.True);

            Transform aimPoint = combatant.transform.Find("AimPoint");
            Transform teamRing = combatant.transform.Find("TeamRing");
            Transform targetRing = combatant.transform.Find("TargetRing");
            Transform statusBars = combatant.transform.Find("WorldStatusBars");
            Transform relationMarker = combatant.transform.Find("RelationMarker");
            Transform humanMarker = combatant.transform.Find(
                "HumanMarker_DiamondChevron");
            Assert.That(aimPoint, Is.Not.Null);
            Assert.That(teamRing, Is.Not.Null);
            Assert.That(targetRing, Is.Not.Null);
            Assert.That(statusBars, Is.Not.Null);
            Assert.That(relationMarker, Is.Not.Null);
            Assert.That(humanMarker, Is.Not.Null);
            Assert.That(
                relationMarker.GetComponent<TextMesh>(),
                Is.Null,
                "The relation root must remain a stable world anchor; its bobbing " +
                "state label belongs on a child transform.");
            Assert.That(
                relationMarker.Find("StateLabel")?.GetComponent<TextMesh>(),
                Is.Not.Null);
            Assert.That(aimPoint.parent, Is.SameAs(combatant.transform));
            Assert.That(teamRing.parent, Is.SameAs(combatant.transform));
            Assert.That(targetRing.parent, Is.SameAs(combatant.transform));
            Assert.That(statusBars.parent, Is.SameAs(combatant.transform));
            Assert.That(relationMarker.parent, Is.SameAs(combatant.transform));
            Assert.That(humanMarker.parent, Is.SameAs(combatant.transform));
            Assert.That(aimPoint.localPosition, Is.EqualTo(new Vector3(0f, 1.45f, 0f)));
            Assert.That(statusBars.localPosition, Is.EqualTo(new Vector3(0f, 2.98f, 0f)));
            Assert.That(
                relationMarker.localPosition,
                Is.EqualTo(new Vector3(0f, 3.36f, 0f)));
            Assert.That(
                humanMarker.localPosition,
                Is.EqualTo(new Vector3(0f, 3.38f, 0f)));

            Transform head = presenter.ActiveAnimator.GetBoneTransform(HumanBodyBones.Head);
            Assert.That(head, Is.Not.Null);
            Assert.That(
                statusBars.position.y - head.position.y,
                Is.GreaterThanOrEqualTo(0.22f),
                combatant.Class +
                " health presentation must clear the scaled head without moving the aim point.");
        }

        private static void AssertUniformScale(
            Vector3 actual,
            float expected,
            string message)
        {
            AssertVectorScale(actual, Vector3.one * expected, message);
        }

        private static void AssertMaterialColor(
            Material material,
            string propertyName,
            Color expected,
            string message)
        {
            Color actual = material.GetColor(propertyName);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f), message + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f), message + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f), message + " blue");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f), message + " alpha");
        }

        private static void AssertVectorScale(
            Vector3 actual,
            Vector3 expected,
            string message)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f), message + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f), message + " y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f), message + " z");
        }

        private static void AssertSingleCharacterHierarchyForEveryCombatant()
        {
            CombatantView[] combatants = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(combatants, Has.Length.EqualTo(4));

            for (int combatantIndex = 0; combatantIndex < combatants.Length; combatantIndex++)
            {
                CombatantView combatant = combatants[combatantIndex];
                Animator[] authoredModelRoots = combatant
                    .GetComponentsInChildren<Animator>(true)
                    .Where(value => value.transform.parent == combatant.transform)
                    .ToArray();
                Assert.That(
                    authoredModelRoots,
                    Has.Length.EqualTo(5),
                    combatant.name +
                    " must retain four playable-class roots and one boss root.");

                Animator[] activeModelRoots = authoredModelRoots
                    .Where(value => value.gameObject.activeInHierarchy)
                    .ToArray();
                Assert.That(
                    activeModelRoots,
                    Has.Length.EqualTo(1),
                    combatant.name + " rendered more than one character model hierarchy.");

                CombatantAnimationPresenter presenter =
                    combatant.GetComponent<CombatantAnimationPresenter>();
                Assert.That(presenter, Is.Not.Null);
                Assert.That(
                    combatant.ActiveCharacterModelRoot,
                    Is.SameAs(activeModelRoots[0].transform));
                Assert.That(presenter.ActiveVisualRoot, Is.SameAs(activeModelRoots[0].transform));
                Assert.That(presenter.ActiveAnimator, Is.SameAs(activeModelRoots[0]));
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name == name)
                {
                    return descendants[index];
                }
            }

            return null;
        }

    }
}
