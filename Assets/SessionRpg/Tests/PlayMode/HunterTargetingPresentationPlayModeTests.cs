using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class HunterTargetingPresentationPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FreezingArrow_UsesTheExplicitFocusedEnemyAsItsAreaTarget()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return null;

            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            CombatantView enemy = FindCombatant(team: 2, human: false);
            Assert.That(enemy, Is.Not.Null);
            Assert.That(hud.SelectFighter(enemy.Id), Is.True);

            Assert.That(
                controller.TryEvaluateFocusedAbility(
                    AbilityId.FreezingArrow,
                    out ParticipantId resolvedTarget,
                    out _,
                    out _),
                Is.True);
            Assert.That(
                resolvedTarget,
                Is.EqualTo(enemy.Id),
                "A selected-hostile area cast must retain the focused unit so its " +
                "resolved point can be centered and clamped deterministically.");
        }

        [UnityTest]
        public IEnumerator CompanionPresentation_DoesNotDisableItsVisualEverySnapshot()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return null;

            CombatantView human = FindCombatant(team: 1, human: true);
            HunterCompanionView companion = human.CompanionVisuals;
            Assert.That(companion, Is.Not.Null);
            Assert.That(companion.IsVisible, Is.True);
            Assert.That(companion.EffectTarget, Is.Not.Null);

            CompanionActivationProbe probe =
                companion.EffectTarget.gameObject.AddComponent<CompanionActivationProbe>();
            InvokePrivate(controller, "PresentSnapshots");
            InvokePrivate(controller, "PresentSnapshots");

            Assert.That(
                probe.DisableCount,
                Is.Zero,
                "A present companion must not be hidden and re-enabled at simulation frequency.");
        }

        [UnityTest]
        public IEnumerator StrategyReadability_UsesCompactScreenAlignedBarsAndIdentityCues()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return null;
            controller.enabled = false;
            yield return null;

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            CombatantView[] combatants = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(combatants, Has.Length.EqualTo(4));

            for (int index = 0; index < combatants.Length; index++)
            {
                CombatantView combatant = combatants[index];
                Transform bars = combatant.transform.Find("WorldStatusBars");
                Assert.That(bars, Is.Not.Null, combatant.name + " has no world bars.");
                if (combatant.IsHiddenFromViewer)
                {
                    Assert.That(
                        bars.gameObject.activeInHierarchy,
                        Is.False,
                        combatant.name + " must not leak hidden world status UI.");
                    continue;
                }

                AssertScreenAligned(bars, camera, combatant.name + " world bars");

                Transform track = bars.Find("HealthTrack");
                Assert.That(track, Is.Not.Null);
                Assert.That(track.localScale.x, Is.InRange(1f, 1.25f));
                Assert.That(track.localScale.y, Is.InRange(0.1f, 0.17f));
                AssertProjectedHorizontal(track, camera, combatant.name + " health track");

                CombatantRelationMarker marker = combatant
                    .GetComponentInChildren<CombatantRelationMarker>(true);
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.HasAuthoredIdentityLanguage, Is.True);
                Assert.That(marker.ClassBadgeText, Is.EqualTo(ResolveClassBadge(combatant.Class)));
                AssertScreenAligned(marker.transform, camera, combatant.name + " identity marker");
                AssertSingleRelationCue(marker.transform, combatant);

                Transform humanMarker = combatant.transform.Find(
                    "HumanMarker_DiamondChevron");
                Assert.That(humanMarker, Is.Not.Null);
                Assert.That(humanMarker.gameObject.activeSelf, Is.EqualTo(combatant.IsHuman));
                if (combatant.IsHuman)
                {
                    AssertScreenAligned(humanMarker, camera, "human marker");
                }
            }
        }

        [UnityTest]
        public IEnumerator CompanionSilhouette_HasDarkBodyAccentsAndScreenAlignedHealth()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return null;
            controller.enabled = false;
            yield return null;

            CombatantView human = FindCombatant(team: 1, human: true);
            HunterCompanionView companion = human.CompanionVisuals;
            Assert.That(companion, Is.Not.Null);
            Assert.That(companion.IsVisible, Is.True);
            Assert.That(companion.HasAuthoredSpiderSilhouette, Is.True);

            Renderer[] bodyRenderers = GetPrivateField<Renderer[]>(
                companion,
                "_bodyRenderers");
            Renderer[] accentRenderers = GetPrivateField<Renderer[]>(
                companion,
                "_accentRenderers");
            Assert.That(bodyRenderers, Has.Length.GreaterThanOrEqualTo(19));
            Assert.That(accentRenderers, Has.Length.GreaterThanOrEqualTo(6));
            Assert.That(bodyRenderers[0].sharedMaterial, Is.Not.Null);
            Assert.That(accentRenderers[0].sharedMaterial, Is.Not.Null);
            Assert.That(
                bodyRenderers[0].sharedMaterial,
                Is.Not.SameAs(accentRenderers[0].sharedMaterial),
                "The opaque body and restrained spectral accents must not collapse " +
                "into one emissive cyan mass.");

            Transform visualRoot = GetPrivateField<GameObject>(
                companion,
                "_visualRoot").transform;
            Assert.That(visualRoot.localScale.x, Is.InRange(1.24f, 1.32f));
            Assert.That(visualRoot.localScale.y, Is.EqualTo(visualRoot.localScale.x));
            Assert.That(visualRoot.localScale.z, Is.EqualTo(visualRoot.localScale.x));
            Assert.That(visualRoot.Find("OwnerBandLeft"), Is.Not.Null);
            Assert.That(visualRoot.Find("OwnerBandRight"), Is.Not.Null);
            Assert.That(
                companion.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "Scaling the presentation hierarchy must not create companion " +
                "targeting or collision authority.");

            Material bodyMaterial = bodyRenderers[0].sharedMaterial;
            Material accentMaterial = accentRenderers[0].sharedMaterial;
            Assert.That(bodyMaterial.HasProperty("_BaseColor"), Is.True);
            Assert.That(accentMaterial.HasProperty("_BaseColor"), Is.True);
            Color bodyColor = bodyMaterial.GetColor("_BaseColor");
            Color accentColor = accentMaterial.GetColor("_BaseColor");
            Assert.That(MaxRgb(bodyColor), Is.LessThan(0.16f));
            Assert.That(
                MaxRgb(accentColor),
                Is.GreaterThan(MaxRgb(bodyColor) + 0.35f));
            for (int index = 0; index < bodyRenderers.Length; index++)
            {
                Assert.That(
                    bodyRenderers[index].shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On));
                Assert.That(bodyRenderers[index].receiveShadows, Is.True);
            }

            Bounds bodyBounds = CombineBounds(bodyRenderers);
            CapsuleCollider combatantCollider = human.GetComponent<CapsuleCollider>();
            Assert.That(combatantCollider, Is.Not.Null);
            Assert.That(
                bodyBounds.size.y,
                Is.GreaterThan(combatantCollider.height * 0.25f),
                "The companion must keep enough projected mass to read as a creature.");
            Assert.That(
                bodyBounds.size.y,
                Is.LessThan(combatantCollider.height * 0.55f),
                "The companion must remain visually subordinate to its owner.");

            Transform healthRoot = GetPrivateField<Transform>(companion, "_healthRoot");
            Camera camera = Camera.main;
            Assert.That(healthRoot, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            AssertScreenAligned(healthRoot, camera, "pet health bar");
            AssertProjectedHorizontal(
                healthRoot.Find("Track"),
                camera,
                "pet health track");
        }

        [UnityTest]
        public IEnumerator MendPet_ResolvesItsHealPresentationToTheCompanion()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return null;

            CombatantView human = FindCombatant(team: 1, human: true);
            CombatEvent mend = CombatEvent.Create(
                1,
                CombatEventKind.Heal,
                human.Id,
                human.Id,
                AbilityId.MendPet,
                amount: 12f);
            MethodInfo resolver = typeof(PrototypeGameController).GetMethod(
                "ResolveHealPresentationTarget",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(resolver, Is.Not.Null);

            Transform resolved = (Transform)resolver.Invoke(
                null,
                new object[] { mend, human });
            Assert.That(resolved, Is.EqualTo(human.CompanionVisuals.EffectTarget));
            Assert.That(resolved, Is.Not.EqualTo(human.transform));
        }

        [UnityTest]
        public IEnumerator PetAttack_UsesTheCompanionAsItsVisibleDamageSource()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return null;
            controller.enabled = false;

            CombatantView human = FindCombatant(team: 1, human: true);
            CombatantView enemy = FindCombatant(team: 2, human: false);
            HunterCompanionView companion = human.CompanionVisuals;
            Assert.That(companion, Is.Not.Null);
            Assert.That(companion.IsVisible, Is.True);

            MethodInfo sourceResolver = typeof(PrototypeGameController).GetMethod(
                "ResolveDamagePresentationSource",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(sourceResolver, Is.Not.Null);
            CombatEvent petDamage = CombatEvent.Create(
                1,
                CombatEventKind.Damage,
                human.Id,
                enemy.Id,
                AbilityId.PetAttack,
                amount: 4f);
            Transform resolved = (Transform)sourceResolver.Invoke(
                null,
                new object[] { petDamage, human });
            Assert.That(resolved, Is.EqualTo(companion.EffectTarget));
            Assert.That(resolved, Is.Not.EqualTo(human.transform));

            MethodInfo deferredResolver = typeof(PrototypeGameController).GetMethod(
                "IsDeferredCompanionCommand",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(deferredResolver, Is.Not.Null);
            Assert.That(
                deferredResolver.Invoke(null, new object[] { AbilityId.PetAttack }),
                Is.True,
                "A successful chase order must not be presented as MISS before its first melee swing.");
            Assert.That(
                deferredResolver.Invoke(null, new object[] { AbilityId.ArcaneShot }),
                Is.False);

            int attacksBefore = companion.PresentedAttackCount;
            Transform visualRoot = companion.EffectTarget;
            Vector3 restingPosition = visualRoot.localPosition;
            companion.PlayAttack(enemy.transform.position);
            yield return null;

            Assert.That(companion.PresentedAttackCount, Is.EqualTo(attacksBefore + 1));
            Assert.That(
                visualRoot.localPosition,
                Is.Not.EqualTo(restingPosition),
                "Resolved companion damage needs a visible pet-origin lunge, not only a hunter animation.");
        }

        [TestCase(StatusId.Horrified, "HORRIFIED")]
        [TestCase(StatusId.Disarmed, "DISARMED")]
        [TestCase(StatusId.Pacified, "PACIFIED")]
        public void NewControlStatuses_HaveExactControllerAndEffectPresentation(
            StatusId status,
            string expectedLabel)
        {
            StatusFlags flag = ResolveFlag(status);
            var snapshot = new CombatantSnapshot(
                new ParticipantId(2),
                new TeamId(2),
                ClassId.Hunter,
                default,
                new Float3(0f, 0f, 1f),
                100f,
                100f,
                0f,
                AbilityId.None,
                0,
                true,
                false,
                flag);

            MethodInfo describeStatus = typeof(PrototypeGameController).GetMethod(
                "DescribeStatus",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(describeStatus, Is.Not.Null);
            Assert.That(
                describeStatus.Invoke(null, new object[] { snapshot }),
                Is.EqualTo(expectedLabel));

            MethodInfo resolveControl = typeof(PrototypeGameController).GetMethod(
                "GetControlVisualState",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(resolveControl, Is.Not.Null);
            CombatantControlVisualState visualState =
                (CombatantControlVisualState)resolveControl.Invoke(
                    null,
                    new object[] { snapshot });
            Assert.That(visualState, Is.Not.EqualTo(CombatantControlVisualState.None));

            MethodInfo isControl = typeof(CombatEffectsPresenter).GetMethod(
                "IsControlStatus",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(isControl, Is.Not.Null);
            Assert.That(isControl.Invoke(null, new object[] { status }), Is.True);

            CombatEffectsPresenter effects =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            Assert.That(effects, Is.Not.Null);
            var target = new GameObject("StatusPresentationTarget");
            effects.PresentStatus(status, target.transform);
            Assert.That(HasActiveCombatText(expectedLabel), Is.True);
            Object.DestroyImmediate(target);
        }

        private static CombatantView FindCombatant(int team, bool human)
        {
            CombatantView[] combatants = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude);
            for (int index = 0; index < combatants.Length; index++)
            {
                CombatantView combatant = combatants[index];
                if (combatant.Team.Value == team &&
                    combatant.IsHuman == human &&
                    (team != 2 || !combatant.IsHiddenFromViewer))
                {
                    return combatant;
                }
            }

            return null;
        }

        private static void AssertScreenAligned(
            Transform value,
            Camera camera,
            string context)
        {
            Assert.That(
                Quaternion.Angle(value.rotation, camera.transform.rotation),
                Is.LessThan(0.1f),
                context + " must match the strategy camera axes.");
        }

        private static void AssertProjectedHorizontal(
            Transform value,
            Camera camera,
            string context)
        {
            Assert.That(value, Is.Not.Null, context + " is missing.");
            Vector3 left = camera.WorldToScreenPoint(
                value.TransformPoint(Vector3.left * 0.5f));
            Vector3 right = camera.WorldToScreenPoint(
                value.TransformPoint(Vector3.right * 0.5f));
            Assert.That(left.z, Is.GreaterThan(0f));
            Assert.That(right.z, Is.GreaterThan(0f));
            Assert.That(
                Mathf.Abs(left.y - right.y),
                Is.LessThan(0.5f),
                context + " is visibly tilted in screen space.");
        }

        private static void AssertSingleRelationCue(
            Transform marker,
            CombatantView combatant)
        {
            Transform ally = marker.Find("AllyIdentity_Wings");
            Transform enemy = marker.Find("EnemyIdentity_Fangs");
            Transform human = marker.Find("HumanIdentity_Diamond");
            Transform boss = marker.Find("BossIdentity_Crown");
            Assert.That(ally, Is.Not.Null);
            Assert.That(enemy, Is.Not.Null);
            Assert.That(human, Is.Not.Null);
            Assert.That(boss, Is.Not.Null);

            Assert.That(ally.gameObject.activeSelf, Is.EqualTo(
                !combatant.IsHuman && combatant.Team.Value == 1 && combatant.Class != ClassId.Boss));
            Assert.That(enemy.gameObject.activeSelf, Is.EqualTo(
                !combatant.IsHuman && combatant.Team.Value != 1 && combatant.Class != ClassId.Boss));
            Assert.That(human.gameObject.activeSelf, Is.EqualTo(combatant.IsHuman));
            Assert.That(boss.gameObject.activeSelf, Is.EqualTo(combatant.Class == ClassId.Boss));
        }

        private static string ResolveClassBadge(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Healer:
                    return "P";
                case ClassId.Rogue:
                    return "R";
                case ClassId.Mage:
                    return "M";
                case ClassId.Hunter:
                    return "H";
                case ClassId.Boss:
                    return "B";
                default:
                    return "?";
            }
        }

        private static T GetPrivateField<T>(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + fieldName);
            return (T)field.GetValue(owner);
        }

        private static Bounds CombineBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static float MaxRgb(Color color)
        {
            return Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private static StatusFlags ResolveFlag(StatusId status)
        {
            switch (status)
            {
                case StatusId.Horrified:
                    return StatusFlags.Horrified;
                case StatusId.Disarmed:
                    return StatusFlags.Disarmed;
                case StatusId.Pacified:
                    return StatusFlags.Pacified;
                default:
                    return StatusFlags.None;
            }
        }

        private static bool HasActiveCombatText(string expected)
        {
            CombatTextView[] textViews = Object.FindObjectsByType<CombatTextView>(
                FindObjectsInactive.Exclude);
            for (int index = 0; index < textViews.Length; index++)
            {
                TextMesh text = textViews[index].GetComponentInChildren<TextMesh>(true);
                if (text != null && text.text == expected)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class CompanionActivationProbe : MonoBehaviour
    {
        public int DisableCount { get; private set; }

        private void OnDisable()
        {
            DisableCount++;
        }
    }
}
