using System.Collections;
using System.Linq;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class CombatantAnimationPresenterPlayModeTests
    {
        private const float RequiredHunterHandTravelMetres = 0.30f;

        private const float RequiredHunterFullDrawSeparationMetres = 0.45f;

        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MageHardCast_GathersThenLaunchesBothHandsWithoutMovingRoot()
        {
            CombatantAnimationPresenter presenter = StartHuman(
                ClassId.Mage,
                SpecializationId.Frost,
                out PrototypeGameController controller);
            yield return null;
            controller.enabled = false;
            presenter.GetComponent<CombatantView>().enabled = false;

            Assert.That(presenter.HasHumanoidHandRig, Is.True);
            CombatantAnimatorIkRelay relay =
                presenter.ActiveAnimator.GetComponent<CombatantAnimatorIkRelay>();
            Assert.That(relay, Is.Not.Null);
            Assert.That(relay.Presenter, Is.SameAs(presenter));

            Vector3 rootPosition = presenter.transform.position;
            Vector3 rootForward = presenter.transform.forward;
            presenter.PlayCast(AbilityId.Frostbolt);
            presenter.SetCastState(AbilityId.Frostbolt, 0.55f, false);
            yield return null;
            yield return null;

            Assert.That(presenter.CurrentGesture, Is.EqualTo(CombatantGesture.MageGather));
            float gatherReach = presenter.IntendedHandReach;

            presenter.PlayCastRelease(AbilityId.Frostbolt);
            yield return new WaitForSeconds(0.1f);

            Assert.That(presenter.CurrentGesture, Is.EqualTo(CombatantGesture.MageLaunch));
            Assert.That(
                presenter.IntendedHandReach,
                Is.GreaterThan(gatherReach + 0.12f),
                "Frostbolt release must read as a two-handed launch from the gameplay camera.");
            Assert.That(presenter.transform.position, Is.EqualTo(rootPosition));
            Assert.That(
                Vector3.Dot(presenter.transform.forward, rootForward),
                Is.GreaterThan(0.999f),
                "Presentation gestures cannot reverse or steer the simulation root.");

            presenter.PlayCast(AbilityId.FlameSurge);
            Assert.That(presenter.CurrentGesture, Is.EqualTo(CombatantGesture.MageNova));
            presenter.PlayCast(AbilityId.Blizzard);
            Assert.That(presenter.CurrentGesture, Is.EqualTo(CombatantGesture.MageNova));
            Assert.That(presenter.transform.position, Is.EqualTo(rootPosition));
        }

        [UnityTest]
        public IEnumerator ClassAbilities_SelectDistinctWeaponAndSpellChoreography()
        {
            CombatantAnimationPresenter rogue = StartHuman(
                ClassId.Rogue,
                SpecializationId.Mutilate,
                out PrototypeGameController rogueController);
            yield return null;
            rogueController.enabled = false;
            rogue.GetComponent<CombatantView>().enabled = false;

            Vector3 rogueRoot = rogue.transform.position;
            rogue.PlayCast(AbilityId.Mutilate);
            yield return new WaitForSeconds(0.08f);
            Assert.That(rogue.CurrentGesture, Is.EqualTo(CombatantGesture.RogueDoubleSlash));
            Assert.That(rogue.CurrentPhase, Is.EqualTo(CombatantAnimationPhase.MeleeContact));
            Assert.That(rogue.transform.position, Is.EqualTo(rogueRoot));

            rogue.PlayCast(AbilityId.BladeTempest);
            yield return new WaitForSeconds(0.1f);
            Assert.That(
                rogue.CurrentGesture,
                Is.EqualTo(CombatantGesture.RogueWhirlwind),
                "Blade Tempest needs a dedicated two-arm circular sweep.");
            Assert.That(
                rogue.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.MeleeContact));
            Assert.That(
                rogue.IntendedHandSpan,
                Is.GreaterThan(0.8f),
                "Blade Tempest must keep both weapons outside the torso silhouette.");
            Assert.That(
                rogue.transform.position,
                Is.EqualTo(rogueRoot),
                "Whirlwind choreography cannot move the deterministic root.");

            rogue.PlayCast(AbilityId.Envenom);
            yield return null;
            Assert.That(rogue.CurrentGesture, Is.EqualTo(CombatantGesture.RogueFinisher));

            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
            CombatantAnimationPresenter priest = StartHuman(
                ClassId.Healer,
                SpecializationId.Discipline,
                out PrototypeGameController priestController);
            yield return null;
            priestController.enabled = false;
            priest.GetComponent<CombatantView>().enabled = false;

            Vector3 priestRoot = priest.transform.position;
            priest.PlayCast(AbilityId.Penance);
            priest.SetCastState(AbilityId.Penance, 0.5f, true);
            yield return null;
            Assert.That(priest.CurrentGesture, Is.EqualTo(CombatantGesture.PriestChannel));
            Assert.That(priest.CurrentPhase, Is.EqualTo(CombatantAnimationPhase.ChannelHold));
            Assert.That(priest.transform.position, Is.EqualTo(priestRoot));

            priest.PlayCastRelease(AbilityId.Penance);
            priest.PlayCast(AbilityId.PowerWordShield);
            yield return null;
            Assert.That(priest.CurrentGesture, Is.EqualTo(CombatantGesture.PriestWard));
            priest.PlayCast(AbilityId.DawnNova);
            Assert.That(priest.CurrentGesture, Is.EqualTo(CombatantGesture.PriestWard));
            priest.PlayCast(AbilityId.UmbralCollapse);
            Assert.That(priest.CurrentGesture, Is.EqualTo(CombatantGesture.PriestShadow));
            Assert.That(priest.transform.position, Is.EqualTo(priestRoot));
        }

        [UnityTest]
        public IEnumerator HunterAbilities_UseBowTrapCommandAndSurvivalChoreography()
        {
            CombatantAnimationPresenter hunter = StartHuman(
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                out PrototypeGameController controller);
            yield return null;
            controller.enabled = false;
            hunter.GetComponent<CombatantView>().enabled = false;

            Vector3 rootPosition = hunter.transform.position;
            Quaternion rootRotation = hunter.transform.rotation;
            Transform visualRoot = hunter.ActiveVisualRoot;
            Quaternion groundedVisualRotation = visualRoot.localRotation;
            float groundedVisualHeight = visualRoot.localPosition.y;
            bool productionHunter = visualRoot.name == "HunterModel_ProductionV1";
            bool mixamoErikaTrial = visualRoot.name == "HunterModel_MixamoErikaTrial";
            bool hasStatefulHunterWeapon = productionHunter || mixamoErikaTrial;
            if (hasStatefulHunterWeapon)
            {
                AssertSelectedHunterWeaponState(visualRoot, false);
            }

            hunter.PlayCast(AbilityId.SteadyShot);
            hunter.SetCastState(AbilityId.SteadyShot, 0.62f, false);
            yield return new WaitForSeconds(0.08f);
            Assert.That(hunter.CurrentGesture, Is.EqualTo(CombatantGesture.HunterDraw));
            Assert.That(hunter.CurrentPhase, Is.EqualTo(CombatantAnimationPhase.CastHold));
            Assert.That(
                hunter.IntendedHandSpan,
                Is.GreaterThan(0.9f),
                "The bow arm and drawing hand must separate enough to read above the torso.");
            AssertGroundedHunterRangedPose(
                hunter,
                groundedVisualRotation,
                groundedVisualHeight,
                "Steady Shot draw");
            if (hasStatefulHunterWeapon)
            {
                AssertSelectedHunterWeaponState(visualRoot, true);

                hunter.ClearCastState();
                AssertSelectedHunterWeaponState(visualRoot, false);

                hunter.PlayCast(AbilityId.SteadyShot);
                hunter.SetCastState(AbilityId.SteadyShot, 0.45f, false);
                AssertSelectedHunterWeaponState(visualRoot, true);
                hunter.PlayCastCanceled();
                AssertSelectedHunterWeaponState(visualRoot, false);

                hunter.PlayCast(AbilityId.SteadyShot);
                hunter.SetCastState(AbilityId.SteadyShot, 0.45f, false);
                AssertSelectedHunterWeaponState(visualRoot, true);
                hunter.Present(0f, Vector2.zero, false);
                AssertSelectedHunterWeaponState(visualRoot, false);

                hunter.Initialize(ClassId.Hunter, visualRoot);
                hunter.Present(0f, Vector2.zero, true);
                AssertSelectedHunterWeaponState(visualRoot, false);
                hunter.PlayCast(AbilityId.SteadyShot);
                hunter.SetCastState(AbilityId.SteadyShot, 0.45f, false);
                AssertSelectedHunterWeaponState(visualRoot, true);
                hunter.enabled = false;
                AssertSelectedHunterWeaponState(visualRoot, false);
                hunter.enabled = true;
                hunter.Initialize(ClassId.Hunter, visualRoot);

                CombatantView hunterView = hunter.GetComponent<CombatantView>();
                Renderer[] hunterRenderers = visualRoot
                    .GetComponentsInChildren<Renderer>(true);
                hunterView.Present(
                    CreateHunterSnapshot(hunterView, true),
                    1,
                    30,
                    6f,
                    hunterView.Team.Value);
                Assert.That(hunterView.IsModelVisible, Is.False);
                Assert.That(
                    hunterRenderers.All(value => !value.enabled),
                    Is.True,
                    "Stealth must hide every body and weapon-state renderer.");
                hunterView.Present(
                    CreateHunterSnapshot(hunterView, false),
                    2,
                    30,
                    6f,
                    hunterView.Team.Value);
                Assert.That(hunterView.IsModelVisible, Is.True);
                Assert.That(
                    hunterRenderers.All(value => value.enabled),
                    Is.True,
                    "Leaving stealth must restore every renderer component without changing weapon state.");
                AssertSelectedHunterWeaponState(visualRoot, false);
            }

            hunter.PlayCastRelease(AbilityId.SteadyShot);
            yield return new WaitForSeconds(0.08f);
            Assert.That(hunter.CurrentGesture, Is.EqualTo(CombatantGesture.HunterLoose));
            AssertGroundedHunterRangedPose(
                hunter,
                groundedVisualRotation,
                groundedVisualHeight,
                "Steady Shot release");
            if (hasStatefulHunterWeapon)
            {
                AssertSelectedHunterWeaponState(visualRoot, false);
            }

            AbilityId[] abilities =
            {
                AbilityId.ArrowRain,
                AbilityId.FrostTrap,
                AbilityId.PetAttack,
                AbilityId.Readiness,
                AbilityId.Deterrence,
                AbilityId.WingClip,
                AbilityId.Disengage,
                AbilityId.FeignDeath,
            };
            CombatantGesture[] expectedGestures =
            {
                CombatantGesture.HunterLoose,
                CombatantGesture.HunterTrap,
                CombatantGesture.HunterCommand,
                CombatantGesture.HunterReady,
                CombatantGesture.HunterBrace,
                CombatantGesture.HunterMelee,
                CombatantGesture.HunterVault,
                CombatantGesture.HunterFeign,
            };
            for (int index = 0; index < abilities.Length; index++)
            {
                hunter.PlayCast(abilities[index]);
                Assert.That(
                    hunter.CurrentGesture,
                    Is.EqualTo(expectedGestures[index]),
                    abilities[index] + " should retain a Hunter silhouette.");
                Assert.That(
                    hunter.CurrentGesture.ToString(),
                    Does.Not.StartWith("Mage"),
                    abilities[index] + " must not reuse Mage choreography.");
            }

            yield return new WaitForSeconds(0.1f);
            Assert.That(hunter.transform.position, Is.EqualTo(rootPosition));
            Assert.That(hunter.transform.rotation, Is.EqualTo(rootRotation));
            if (hasStatefulHunterWeapon)
            {
                AssertSelectedHunterWeaponState(visualRoot, false);
            }
        }

        [UnityTest]
        public IEnumerator HunterProceduralHandMotion_PreservesDrawAndReleaseTravelGates()
        {
            CombatantAnimationPresenter hunter = StartHuman(
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                out PrototypeGameController controller);
            yield return null;
            controller.enabled = false;
            hunter.GetComponent<CombatantView>().enabled = false;

            Animator animator = hunter.ActiveAnimator;
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.isHuman, Is.True);
            // Batch PlayMode does not guarantee a rendered camera before each
            // coroutine sample. Force evaluation for this bone-motion proof;
            // normal gameplay keeps CullUpdateTransforms and evaluates whenever
            // the visible combatant is inside the strategy camera.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.That(leftHand, Is.Not.Null);
            Assert.That(rightHand, Is.Not.Null);

            Vector3 rootPosition = hunter.transform.position;
            hunter.PlayCast(AbilityId.SteadyShot);
            hunter.SetCastState(AbilityId.SteadyShot, 0f, false);
            yield return new WaitForSeconds(0.1f);
            Vector3 readyRightHand = rightHand.position;

            hunter.SetCastState(AbilityId.SteadyShot, 1f, false);
            yield return new WaitForSeconds(0.1f);
            Vector3 fullDrawRightHand = rightHand.position;
            float drawTravel = Vector3.Distance(readyRightHand, fullDrawRightHand);
            float fullDrawSeparation = Vector3.Distance(leftHand.position, rightHand.position);
            Assert.That(
                drawTravel,
                Is.GreaterThanOrEqualTo(RequiredHunterHandTravelMetres),
                "The actual Humanoid drawing hand weakened the 0.30 m draw gate. " +
                "Ready=" + readyRightHand.ToString("F3") +
                ", fullDraw=" + fullDrawRightHand.ToString("F3") + ".");
            Assert.That(
                fullDrawSeparation,
                Is.GreaterThanOrEqualTo(RequiredHunterFullDrawSeparationMetres),
                "The actual Humanoid hands weakened the 0.45 m full-draw separation gate.");

            hunter.PlayCastRelease(AbilityId.SteadyShot);
            float maximumReleaseTravel = 0f;
            bool sampledLoose = false;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
                if (hunter.CurrentGesture != CombatantGesture.HunterLoose)
                {
                    if (sampledLoose)
                    {
                        break;
                    }

                    continue;
                }

                sampledLoose = true;
                maximumReleaseTravel = Mathf.Max(
                    maximumReleaseTravel,
                    Vector3.Distance(fullDrawRightHand, rightHand.position));
            }

            Assert.That(sampledLoose, Is.True, "Steady Shot never entered its release accent.");
            Assert.That(
                maximumReleaseTravel,
                Is.GreaterThanOrEqualTo(RequiredHunterHandTravelMetres),
                "The actual Humanoid drawing hand weakened the 0.30 m release gate.");
            Assert.That(
                hunter.transform.position,
                Is.EqualTo(rootPosition),
                "Procedural bow motion cannot move the deterministic combatant root.");
        }

        private static void AssertSelectedHunterWeaponState(
            Transform root,
            bool drawn)
        {
            if (root.name == "HunterModel_ProductionV1")
            {
                AssertProductionHunterWeaponState(root, drawn);
                return;
            }

            Assert.That(root.name, Is.EqualTo("HunterModel_MixamoErikaTrial"));
            Transform nockedArrow = FindDescendant(root, "HunterArrow_Hand");
            Assert.That(nockedArrow, Is.Not.Null);
            Assert.That(
                nockedArrow.gameObject.activeSelf,
                Is.EqualTo(drawn),
                "The model-only trial must drive its sparse project nocked-arrow reference.");
        }

        private static void AssertProductionHunterWeaponState(
            Transform root,
            bool drawn)
        {
            Transform staticString = FindDescendant(
                root,
                "HunterProductionV3_StaticString");
            Transform drawString = FindDescendant(
                root,
                "HunterProductionV3_DrawString");
            Transform nockedArrow = FindDescendant(
                root,
                "HunterProductionV3_NockedArrow");
            Assert.That(staticString, Is.Not.Null);
            Assert.That(drawString, Is.Not.Null);
            Assert.That(nockedArrow, Is.Not.Null);
            Assert.That(staticString.gameObject.activeSelf, Is.EqualTo(!drawn));
            Assert.That(drawString.gameObject.activeSelf, Is.EqualTo(drawn));
            Assert.That(nockedArrow.gameObject.activeSelf, Is.EqualTo(drawn));
        }

        private static CombatantSnapshot CreateHunterSnapshot(
            CombatantView view,
            bool stealthed)
        {
            Vector3 position = view.transform.position;
            Vector3 facing = view.transform.forward;
            return new CombatantSnapshot(
                view.Id,
                view.Team,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                new Float3(position.x, 0f, position.z),
                new Float3(facing.x, 0f, facing.z),
                110f,
                110f,
                0f,
                AbilityId.None,
                0,
                0,
                AbilityActivationType.Instant,
                SpellSchoolMask.None,
                ResourceType.Mana,
                100f,
                100f,
                0,
                default,
                0,
                SpellSchoolMask.None,
                true,
                false,
                stealthed ? StatusFlags.Stealthed : StatusFlags.None,
                stealthed
                    ? AuraTag.Stealth | AuraTag.StealthAccess
                    : AuraTag.None);
        }

        private static void AssertGroundedHunterRangedPose(
            CombatantAnimationPresenter presenter,
            Quaternion groundedVisualRotation,
            float groundedVisualHeight,
            string context)
        {
            Transform visualRoot = presenter.ActiveVisualRoot;
            Vector3 groundedUp = groundedVisualRotation * Vector3.up;
            Vector3 posedUp = visualRoot.localRotation * Vector3.up;
            Assert.That(
                Vector3.Angle(groundedUp, posedUp),
                Is.LessThanOrEqualTo(15.1f),
                context + " tilted the complete character into a falling pose.");
            Assert.That(
                visualRoot.localPosition.y,
                Is.InRange(groundedVisualHeight - 0.145f, groundedVisualHeight + 0.003f),
                context + " moved the visual root away from its planted stance.");
            Assert.That(
                presenter.ActiveAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                Is.EqualTo(Animator.StringToHash("Locomotion")),
                context + " must keep the lower body in grounded locomotion while IK poses the bow.");

            Transform head = presenter.ActiveAnimator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = presenter.ActiveAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = presenter.ActiveAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            Assert.That(head, Is.Not.Null);
            Assert.That(leftFoot, Is.Not.Null);
            Assert.That(rightFoot, Is.Not.Null);
            float lowestFoot = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            Assert.That(
                head.position.y - lowestFoot,
                Is.GreaterThan(0.85f),
                context + " collapsed the Humanoid skeleton into a horizontal silhouette.");
            Assert.That(
                lowestFoot - presenter.transform.position.y,
                Is.InRange(-0.1f, 0.45f),
                context + " lifted both feet away from the authored ground.");
        }

        [UnityTest]
        public IEnumerator MageRelease_KeepsEverySkinnedPartOnOneCohesiveSilhouette()
        {
            CombatantAnimationPresenter presenter = StartHuman(
                ClassId.Mage,
                SpecializationId.Fire,
                out PrototypeGameController controller);
            yield return null;
            controller.enabled = false;
            presenter.GetComponent<CombatantView>().enabled = false;

            presenter.PlayCastRelease(AbilityId.Fireball);
            yield return new WaitForSeconds(0.18f);

            SkinnedMeshRenderer[] renderers = presenter.ActiveAnimator
                .GetComponentsInChildren<SkinnedMeshRenderer>(false);
            Assert.That(
                renderers.Length,
                Is.GreaterThanOrEqualTo(1),
                "A coherent trial model may author the complete Mage in one skinned renderer.");
            Bounds combined = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                combined.Encapsulate(renderers[index].bounds);
            }

            float visualScale = presenter.ActiveAnimator.transform.lossyScale.x;
            float maximumWidth =
                presenter.ActiveAnimator.name == "MageModel_MixamoDemonTrial"
                    ? 2.25f
                    : 2.1f;
            Assert.That(
                combined.size.x / visualScale,
                Is.LessThan(maximumWidth),
                "Mage head, body, and outfit separated into multiple silhouettes during release.");
            Assert.That(
                combined.size.z / visualScale,
                Is.LessThan(1.2f),
                "Mage head, body, and outfit separated into multiple silhouettes during release.");
        }

        [UnityTest]
        public IEnumerator AbilityAnticipations_KeepTheVisualRootGrounded()
        {
            CombatantAnimationPresenter presenter = StartHuman(
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                out PrototypeGameController controller);
            yield return null;
            controller.enabled = false;
            presenter.GetComponent<CombatantView>().enabled = false;

            Transform visualRoot = presenter.ActiveVisualRoot;
            Vector3 simulationRootPosition = presenter.transform.position;
            float groundedHeight = visualRoot.localPosition.y;
            float highestVisualRoot = groundedHeight;

            presenter.PlayCast(AbilityId.Disengage);
            for (int frame = 0; frame < 18; frame++)
            {
                yield return null;
                highestVisualRoot = Mathf.Max(
                    highestVisualRoot,
                    visualRoot.localPosition.y);
            }

            Assert.That(
                highestVisualRoot,
                Is.LessThanOrEqualTo(
                    groundedHeight + presenter.MaximumGroundedLift + 0.003f),
                "A visual anticipation lifted the character far enough to read as flying.");
            Assert.That(
                presenter.transform.position,
                Is.EqualTo(simulationRootPosition),
                "Presentation-only grounding cannot move the simulation root.");
        }

        [UnityTest]
        public IEnumerator HitReaction_SnapsOnTheFirstRenderedFrameThenSettles()
        {
            CombatantAnimationPresenter presenter = StartHuman(
                ClassId.Mage,
                SpecializationId.Fire,
                out PrototypeGameController controller);
            yield return null;
            controller.enabled = false;
            presenter.GetComponent<CombatantView>().enabled = false;

            Transform visualRoot = presenter.ActiveVisualRoot;
            Quaternion restingRotation = visualRoot.localRotation;
            Vector3 simulationRootPosition = presenter.transform.position;
            presenter.PlayHit(presenter.transform.position - presenter.transform.forward);
            Assert.That(
                presenter.CurrentPhase,
                Is.EqualTo(CombatantAnimationPhase.HitReaction));

            yield return null;
            Assert.That(
                Quaternion.Angle(restingRotation, visualRoot.localRotation),
                Is.GreaterThan(0.5f),
                "Damage feedback should recoil on its first rendered frame.");

            yield return new WaitForSeconds(0.24f);
            Assert.That(presenter.CurrentPhase, Is.EqualTo(CombatantAnimationPhase.Idle));
            Assert.That(presenter.transform.position, Is.EqualTo(simulationRootPosition));
        }

        [UnityTest]
        public IEnumerator Reinitialize_ReturnsAReactionPoseToGroundedIdle()
        {
            CombatantAnimationPresenter presenter = StartHuman(
                ClassId.Mage,
                SpecializationId.Fire,
                out PrototypeGameController controller);
            yield return null;
            controller.enabled = false;
            presenter.GetComponent<CombatantView>().enabled = false;

            Quaternion idleRotation = presenter.ActiveVisualRoot.localRotation;
            presenter.PlayKnockback(presenter.transform.right);
            yield return new WaitForSeconds(0.1f);
            Assert.That(
                Quaternion.Angle(idleRotation, presenter.ActiveVisualRoot.localRotation),
                Is.GreaterThan(0.5f));

            presenter.Initialize(ClassId.Mage);
            presenter.Present(0f, Vector2.zero, true);
            yield return null;

            Assert.That(presenter.CurrentPhase, Is.EqualTo(CombatantAnimationPhase.Idle));
            Assert.That(
                Quaternion.Angle(idleRotation, presenter.ActiveVisualRoot.localRotation),
                Is.LessThan(1f),
                "Static review must never preserve a falling or knockback pose.");
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

        private static CombatantAnimationPresenter StartHuman(
            ClassId classId,
            SpecializationId specialization,
            out PrototypeGameController controller)
        {
            controller = Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller, Is.Not.Null);
            controller.StartLocalSession(GameModeId.Arena, classId, specialization);
            CombatantView human = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude)
                .Single(value => value.IsHuman);
            CombatantAnimationPresenter presenter =
                human.GetComponent<CombatantAnimationPresenter>();
            Assert.That(presenter, Is.Not.Null);
            return presenter;
        }

    }
}
