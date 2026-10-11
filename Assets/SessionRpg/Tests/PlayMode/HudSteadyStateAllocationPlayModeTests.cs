using System;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class HudSteadyStateAllocationPlayModeTests
    {
        private GameObject _hudObject;
        private GameObject _fighterObject;
        private PrototypeHud _hud;
        private CombatantStatusView _fighter;
        private Text _playerResourceValue;
        private Text _targetHealth;
        private Text _targetBarrier;
        private Text _firstAura;
        private Text _fighterHealth;

        [SetUp]
        public void SetUp()
        {
            _hudObject = new GameObject(
                "HudSteadyStateUnderTest",
                typeof(RectTransform),
                typeof(PrototypeHud));
            _hud = _hudObject.GetComponent<PrototypeHud>();

            GameObject playerResource = CreateRoot("PlayerResource", _hudObject.transform);
            Image playerResourceFill = CreateImage("Fill", playerResource.transform);
            Text playerResourceType = CreateText("Type", playerResource.transform);
            _playerResourceValue = CreateText("Value", playerResource.transform);
            SetField(_hud, "_playerResourceRoot", playerResource);
            SetField(_hud, "_playerResourceFill", playerResourceFill);
            SetField(_hud, "_playerResourceTypeLabel", playerResourceType);
            SetField(_hud, "_playerResourceValueLabel", _playerResourceValue);

            GameObject target = CreateRoot("Target", _hudObject.transform);
            SetField(_hud, "_showDetailedTargetPanel", true);
            SetField(_hud, "_targetRoot", target);
            SetField(_hud, "_targetParticipantLabel", CreateText("Participant", target.transform));
            SetField(_hud, "_targetRelationLabel", CreateText("Relation", target.transform));
            SetField(_hud, "_targetLabel", CreateText("Identity", target.transform));
            SetField(_hud, "_targetHealthFill", CreateImage("HealthFill", target.transform));
            SetField(_hud, "_targetBarrierFill", CreateImage("BarrierFill", target.transform));
            _targetHealth = CreateText("Health", target.transform);
            _targetBarrier = CreateText("Barrier", target.transform);
            SetField(_hud, "_targetHealthLabel", _targetHealth);
            SetField(_hud, "_targetBarrierLabel", _targetBarrier);
            SetField(_hud, "_targetStatusLabel", CreateText("Status", target.transform));

            GameObject targetResource = CreateRoot("TargetResource", target.transform);
            SetField(_hud, "_targetResourceRoot", targetResource);
            SetField(_hud, "_targetResourceFill", CreateImage("Fill", targetResource.transform));
            SetField(_hud, "_targetResourceTypeLabel", CreateText("Type", targetResource.transform));
            SetField(_hud, "_targetResourceValueLabel", CreateText("Value", targetResource.transform));

            var auraRoots = new GameObject[PrototypeHud.TargetAuraSlotCount];
            var auraSurfaces = new Graphic[PrototypeHud.TargetAuraSlotCount];
            var auraLabels = new Text[PrototypeHud.TargetAuraSlotCount];
            for (int slot = 0; slot < PrototypeHud.TargetAuraSlotCount; slot++)
            {
                auraRoots[slot] = CreateRoot("Aura" + slot, target.transform);
                auraSurfaces[slot] = CreateImage("Surface", auraRoots[slot].transform);
                auraLabels[slot] = CreateText("Label", auraRoots[slot].transform);
            }

            _firstAura = auraLabels[0];
            SetField(_hud, "_targetAuraRoots", auraRoots);
            SetField(_hud, "_targetAuraSurfaces", auraSurfaces);
            SetField(_hud, "_targetAuraLabels", auraLabels);

            _fighterObject = new GameObject(
                "FighterStatusUnderTest",
                typeof(RectTransform),
                typeof(CombatantStatusView));
            _fighter = _fighterObject.GetComponent<CombatantStatusView>();
            SetField(_fighter, "_healthFill", CreateImage("HealthFill", _fighterObject.transform));
            SetField(_fighter, "_barrierFill", CreateImage("BarrierFill", _fighterObject.transform));
            _fighterHealth = CreateText("Health", _fighterObject.transform);
            SetField(_fighter, "_healthLabel", _fighterHealth);
            SetField(_fighter, "_barrierLabel", CreateText("Barrier", _fighterObject.transform));
            SetField(_fighter, "_statusLabel", CreateText("Status", _fighterObject.transform));
            _fighter.Bind(1, ClassId.Hunter, "Player", "YOU", true, true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_fighterObject);
            UnityEngine.Object.DestroyImmediate(_hudObject);
        }

        [Test]
        public void StableVitalsResourcesAndAuras_AfterWarmupAllocateZeroManagedBytes()
        {
            RefreshStableState();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            RefreshStableState();
            GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 100; iteration++)
            {
                RefreshStableState();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(
                allocated,
                Is.Zero,
                "Stable HUD resource, focused-target, aura, and fighter updates must not allocate.");
        }

        [Test]
        public void CachedHudLabels_RefreshWhenDisplayedValuesChange()
        {
            RefreshStableState();

            _hud.SetPlayerResource(ResourceType.Mana, 36f, 100f);
            _hud.SetTarget(
                2,
                ClassId.Mage,
                "Opponent",
                "ENEMY",
                false,
                59f,
                100f,
                0f,
                string.Empty,
                0f,
                "SLOWED",
                true,
                true);
            _hud.SetTargetAura(
                0,
                new HudAuraIndicator("Poison", 4.2f, 3, HudAuraKind.Harmful));
            _fighter.Present(64f, 100f, 0f, string.Empty, 0f, "READY", true, false);

            Assert.That(_playerResourceValue.text, Is.EqualTo("36 / 100"));
            Assert.That(_targetHealth.text, Is.EqualTo("59 / 100  •  59%"));
            Assert.That(_targetBarrier.text, Is.Empty);
            Assert.That(_firstAura.text, Is.EqualTo("POISON x3  5s"));
            Assert.That(_fighterHealth.text, Is.EqualTo("64%"));
        }

        private void RefreshStableState()
        {
            _hud.SetPlayerResource(ResourceType.Mana, 37f, 100f);
            _hud.SetTargetResource(ResourceType.Energy, 82f, 100f);
            _hud.SetTarget(
                2,
                ClassId.Mage,
                "Opponent",
                "ENEMY",
                false,
                61f,
                100f,
                12f,
                string.Empty,
                0f,
                "SLOWED",
                true,
                true);
            _hud.SetTargetAuras(
                new HudAuraIndicator("Poison", 5.2f, 2, HudAuraKind.Harmful),
                new HudAuraIndicator("Fear", 2.2f, kind: HudAuraKind.Control));
            _fighter.Present(75f, 100f, 0f, string.Empty, 0f, "READY", true, false);
        }

        private static GameObject CreateRoot(string name, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            return value;
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            value.transform.SetParent(parent, false);
            return value.GetComponent<Image>();
        }

        private static Text CreateText(string name, Transform parent)
        {
            var value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            value.transform.SetParent(parent, false);
            return value.GetComponent<Text>();
        }

        private static void SetField(object owner, string fieldName, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(owner, value);
        }
    }
}
