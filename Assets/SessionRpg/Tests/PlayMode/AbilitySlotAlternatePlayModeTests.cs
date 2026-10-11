using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class AbilitySlotAlternatePlayModeTests
    {
        private const float UnifiedControlDiameter = 232f;
        private const float PrimaryMedallionDiameter = 84f;
        private const float DirectionRootDiameter = 60f;
        private const float DirectionArtDiameter = 48f;
        private const float DirectionOffset = 78f;
        private static readonly Vector2 AuthoredDownCaptionPosition =
            new Vector2(0f, -92f);
        private static readonly Vector2 AuthoredLeftCaptionPosition =
            new Vector2(-78f, -14f);
        private static readonly Vector2 AuthoredRightCaptionPosition =
            new Vector2(78f, -14f);

        private GameObject _eventSystemObject;
        private GameObject _slotObject;
        private RectTransform _primaryVisualRoot;
        private EventSystem _eventSystem;
        private AbilityButton _button;
        private AbilitySlotView _slot;
        private GameObject _alternateRoot;
        private Text _alternateLabel;
        private Text _actionLabel;
        private AbilityGlyphGraphic _alternateGlyph;
        private AbilityRadialGuideGraphic _radialGuide;
        private GameObject _alternateDownRoot;
        private Text _alternateDownHint;
        private AbilityGlyphGraphic _alternateDownGlyph;
        private GameObject _alternateLeftRoot;
        private Text _alternateLeftHint;
        private AbilityGlyphGraphic _alternateLeftGlyph;
        private GameObject _alternateRightRoot;
        private Text _alternateRightHint;
        private AbilityGlyphGraphic _alternateRightGlyph;
        private Image _outerFrame;
        private Image _frameHighlight;
        private Image _innerFrame;
        private Image _interactionHalo;
        private Image _rim;
        private Image _iconGlow;
        private Image _stateSurface;
        private Image _cooldownFill;
        private Image _globalCooldownFill;
        private Text _cooldownText;
        private Text _globalCooldownText;
        private GameObject _blockedMark;

        [SetUp]
        public void SetUp()
        {
            _eventSystemObject = new GameObject("AlternateSlotTestEventSystem");
            _eventSystem = _eventSystemObject.AddComponent<EventSystem>();

            _slotObject = new GameObject(
                "AlternateSlotUnderTest",
                typeof(RectTransform),
                typeof(AbilityButton),
                typeof(AbilitySlotView));
            _slotObject.SetActive(false);
            _button = _slotObject.GetComponent<AbilityButton>();
            _slot = _slotObject.GetComponent<AbilitySlotView>();
            RectTransform controlRoot = _slotObject.GetComponent<RectTransform>();
            controlRoot.sizeDelta = Vector2.one * UnifiedControlDiameter;

            GameObject primaryVisualObject = new GameObject(
                "PrimaryMedallion",
                typeof(RectTransform));
            primaryVisualObject.transform.SetParent(_slotObject.transform, false);
            _primaryVisualRoot =
                primaryVisualObject.GetComponent<RectTransform>();
            _primaryVisualRoot.sizeDelta =
                Vector2.one * PrimaryMedallionDiameter;

            GameObject radialGuideObject = new GameObject(
                "DirectionalRadialGuide",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(AbilityRadialGuideGraphic));
            radialGuideObject.transform.SetParent(_slotObject.transform, false);
            _radialGuide = radialGuideObject.GetComponent<AbilityRadialGuideGraphic>();
            _radialGuide.rectTransform.sizeDelta =
                Vector2.one * UnifiedControlDiameter;
            _radialGuide.raycastTarget = false;

            _alternateRoot = CreateDirectionalVisual(
                "AlternateUp",
                new Vector2(0f, DirectionOffset),
                out _alternateGlyph);
            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));
            labelObject.transform.SetParent(_alternateRoot.transform, false);
            _alternateLabel = labelObject.GetComponent<Text>();
            _alternateLabel.raycastTarget = false;
            GameObject actionObject = new GameObject(
                "Action",
                typeof(RectTransform),
                typeof(Text));
            actionObject.transform.SetParent(_primaryVisualRoot, false);
            _actionLabel = actionObject.GetComponent<Text>();

            _alternateDownRoot = CreateDirectionalVisual(
                "AlternateDownTag",
                new Vector2(0f, -DirectionOffset),
                out _alternateDownGlyph);
            _alternateDownHint = CreateDirectionalHint(
                "AlternateDownHint",
                AuthoredDownCaptionPosition);
            _alternateLeftRoot = CreateDirectionalVisual(
                "AlternateLeftTag",
                new Vector2(-DirectionOffset, 0f),
                out _alternateLeftGlyph);
            _alternateLeftHint = CreateDirectionalHint(
                "AlternateLeftHint",
                AuthoredLeftCaptionPosition);
            _alternateRightRoot = CreateDirectionalVisual(
                "AlternateRightTag",
                new Vector2(DirectionOffset, 0f),
                out _alternateRightGlyph);
            _alternateRightHint = CreateDirectionalHint(
                "AlternateRightHint",
                AuthoredRightCaptionPosition);

            _outerFrame = CreateImageChild("OuterFrame");
            _frameHighlight = CreateImageChild("FrameHighlight");
            _innerFrame = CreateImageChild("InnerFrame");
            _interactionHalo = CreateImageChild("InteractionHalo");
            _rim = CreateImageChild("Rim");
            _iconGlow = CreateImageChild("IconGlow");
            _stateSurface = CreateImageChild("StateSurface");
            _cooldownFill = CreateImageChild("CooldownFill");
            _globalCooldownFill = CreateImageChild("GlobalCooldownFill");
            _cooldownText = CreateTextChild("CooldownText");
            _globalCooldownText = CreateTextChild("GlobalCooldownText");
            _blockedMark = new GameObject("BlockedMark", typeof(RectTransform));
            _blockedMark.transform.SetParent(_primaryVisualRoot, false);

            SetField("_inputButton", _button);
            SetField("_actionLabel", _actionLabel);
            SetField("_outerFrame", _outerFrame);
            SetField("_frameHighlight", _frameHighlight);
            SetField("_innerFrame", _innerFrame);
            SetField("_interactionHalo", _interactionHalo);
            SetField("_rim", _rim);
            SetField("_iconGlow", _iconGlow);
            SetField("_stateSurface", _stateSurface);
            SetField("_cooldownFill", _cooldownFill);
            SetField("_cooldownText", _cooldownText);
            SetField("_globalCooldownFill", _globalCooldownFill);
            SetField("_globalCooldownLockRoot", _globalCooldownText.gameObject);
            SetField("_blockedMark", _blockedMark);
            SetField("_visualRoot", _primaryVisualRoot);
            SetField("_directionalGuide", _radialGuide);
            SetField("_alternateUpRoot", _alternateRoot);
            SetField("_alternateUpSurface", null);
            SetField("_alternateUpLabel", _alternateLabel);
            SetField("_alternateUpGlyph", _alternateGlyph);
            SetField("_alternateDownHint", _alternateDownHint);
            SetField("_alternateDownHintRoot", _alternateDownRoot);
            SetField("_alternateDownHintSurface", null);
            SetField("_alternateDownGlyph", _alternateDownGlyph);
            SetField("_alternateLeftHint", _alternateLeftHint);
            SetField("_alternateLeftHintRoot", _alternateLeftRoot);
            SetField("_alternateLeftHintSurface", null);
            SetField("_alternateLeftGlyph", _alternateLeftGlyph);
            SetField("_alternateRightHint", _alternateRightHint);
            SetField("_alternateRightHintRoot", _alternateRightRoot);
            SetField("_alternateRightHintSurface", null);
            SetField("_alternateRightGlyph", _alternateRightGlyph);
            _slotObject.SetActive(true);
            _button.ConfigureFlickThreshold(40f, 40f, false);
            _button.ConfigureMaximumFlickDuration(1f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_slotObject);
            Object.DestroyImmediate(_eventSystemObject);
        }

        [Test]
        public void PlayablePrimaryCaptions_ArePersistentSpecificAndUniquePerKit()
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

            for (int specIndex = 0; specIndex < specializations.Length; specIndex++)
            {
                SpecializationId specialization = specializations[specIndex];
                AbilityKit kit = SpecializationCatalog.GetKit(specialization);
                var captions = new HashSet<string>();
                for (int slot = 0; slot < AbilityKit.Capacity; slot++)
                {
                    AbilityId ability = kit.GetAbility(slot);
                    string caption = PrototypeHud.ResolveActionCaption(ability);
                    Assert.That(
                        string.IsNullOrWhiteSpace(caption),
                        Is.False,
                        specialization + " slot " + slot + " has no caption");
                    Assert.That(
                        captions.Add(caption),
                        Is.True,
                        specialization + " repeats caption " + caption);
                }
            }
        }

        [TestCase(AbilityId.BladeTempest, "BLADE FAN")]
        [TestCase(AbilityId.DawnNova, "DAWN NOVA")]
        [TestCase(AbilityId.UmbralCollapse, "COLLAPSE")]
        [TestCase(AbilityId.FlameSurge, "FLAME SURGE")]
        [TestCase(AbilityId.Blizzard, "BLIZZARD")]
        [TestCase(AbilityId.ArrowRain, "ARROW RAIN")]
        public void HeroSiegeAreaCaptions_AreSpecific(
            AbilityId ability,
            string expectedCaption)
        {
            Assert.That(
                PrototypeHud.ResolveActionCaption(ability),
                Is.EqualTo(expectedCaption));
        }

        [Test]
        public void AlternateUp_WedgeAppearsAndHighlightsWithoutResizing()
        {
            _slot.Bind(AbilityId.FlashHeal, null, "Flash Heal");
            _slot.BindAlternateUp(AbilityId.Renew, null, "Renew", "Healing over time");

            Assert.That(_slot.HasAlternateUp, Is.True);
            Assert.That(_slot.IsAlternateUpVisible, Is.False);
            Assert.That(_button.GetDirectionalAbility(AbilityFlickDirection.Up),
                Is.EqualTo(AbilityId.Renew));
            Assert.That(_alternateLabel.text, Is.EqualTo("↑ RENEW"));
            Assert.That(_alternateRoot.activeSelf, Is.True);
            Assert.That(
                _alternateRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(_alternateLabel.gameObject.activeInHierarchy, Is.False);
            Assert.That(_radialGuide.IsExpanded, Is.False);
            Assert.That(_radialGuide.BoundDirectionCount, Is.EqualTo(1));
            Assert.That(_radialGuide.HasDirection(AbilityFlickDirection.Up), Is.True);
            Assert.That(
                PopulateGuideAndGetVertexCount(),
                Is.GreaterThan(0),
                "The unified directional guide must remain rendered at rest.");
            Assert.That(
                ((RectTransform)_alternateRoot.transform).sizeDelta.x,
                Is.EqualTo(DirectionRootDiameter));
            Assert.That(
                ((RectTransform)_alternateRoot.transform).sizeDelta.y,
                Is.EqualTo(DirectionRootDiameter));
            Assert.That(
                ((RectTransform)_alternateRoot.transform).anchoredPosition,
                Is.EqualTo(new Vector2(0f, DirectionOffset)));
            Assert.That(
                _alternateGlyph.rectTransform.anchorMin,
                Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(
                _alternateGlyph.rectTransform.anchorMax,
                Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(
                _alternateGlyph.rectTransform.anchoredPosition,
                Is.EqualTo(Vector2.zero));
            Assert.That(
                _alternateGlyph.rectTransform.sizeDelta,
                Is.EqualTo(Vector2.one * DirectionArtDiameter));

            PointerEventData pointer = Pointer(91, Vector2.zero);
            _button.OnPointerDown(pointer);
            Assert.That(_slot.IsAlternateUpVisible, Is.True);
            Assert.That(_radialGuide.IsExpanded, Is.True);
            Assert.That(_alternateLabel.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                _alternateRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(
                ((RectTransform)_alternateRoot.transform).sizeDelta.x,
                Is.EqualTo(DirectionRootDiameter));
            Assert.That(
                _alternateGlyph.rectTransform.anchorMin,
                Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(
                _alternateGlyph.rectTransform.anchoredPosition,
                Is.EqualTo(Vector2.zero));
            pointer.position = new Vector2(0f, 42f);
            _button.OnDrag(pointer);

            Assert.That(_button.IsSelectionLocked, Is.True);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.Renew));
            Assert.That(_slot.IsAlternateUpSelected, Is.True);
            Assert.That(
                _radialGuide.SelectedDirection,
                Is.EqualTo(AbilityFlickDirection.Up));
            Assert.That(_alternateLabel.text, Is.EqualTo("✓ RENEW"));
            Assert.That(_actionLabel.text, Is.EqualTo("RENEW"));
            Assert.That(
                _alternateRoot.transform.localScale,
                Is.EqualTo(Vector3.one));

            _button.OnPointerUp(pointer);
            Assert.That(_actionLabel.text, Is.EqualTo("HEAL"));
            Assert.That(_slot.IsAlternateUpVisible, Is.False);
            Assert.That(_radialGuide.IsExpanded, Is.False);
            Assert.That(_alternateLabel.gameObject.activeInHierarchy, Is.False);
            Assert.That(
                _alternateRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(
                ((RectTransform)_alternateRoot.transform).sizeDelta.x,
                Is.EqualTo(DirectionRootDiameter));
            Assert.That(
                _alternateGlyph.rectTransform.anchorMin,
                Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(
                _alternateGlyph.rectTransform.anchoredPosition,
                Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AlternateUp_UsesCompactNameForMultiWordAbility()
        {
            _slot.Bind(AbilityId.KidneyShot, null, "Kidney Shot");
            _slot.BindAlternateUp(AbilityId.CheapShot, null, "Cheap Shot");

            Assert.That(_alternateLabel.text, Is.EqualTo("↑ CHEAP"));
        }

        [Test]
        public void RadialGuide_RendersOnlyBoundDirectionalSectors()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            Assert.That(_radialGuide.BoundDirectionCount, Is.Zero);
            Assert.That(PopulateGuideAndGetVertexCount(), Is.Zero);

            _slot.BindAlternateUp(
                AbilityId.SerpentSting,
                null,
                "Serpent Sting");
            int oneSectorVertexCount = PopulateGuideAndGetVertexCount();
            Assert.That(_radialGuide.BoundDirectionCount, Is.EqualTo(1));
            Assert.That(
                oneSectorVertexCount,
                Is.EqualTo(160),
                "One bound wedge is four gradient bands × ten segments × four vertices.");

            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Down,
                AbilityId.ArcaneShot);
            Assert.That(_radialGuide.BoundDirectionCount, Is.EqualTo(2));
            Assert.That(
                PopulateGuideAndGetVertexCount(),
                Is.EqualTo(oneSectorVertexCount * 2),
                "An unbound left or right sector must not contribute mesh geometry.");

            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Left,
                AbilityId.KillShot);
            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Right,
                AbilityId.ScorpidSting);
            Assert.That(_radialGuide.BoundDirectionCount, Is.EqualTo(4));
            Assert.That(
                PopulateGuideAndGetVertexCount(),
                Is.EqualTo(oneSectorVertexCount * 4));

            PointerEventData pointer = Pointer(94, Vector2.zero);
            _button.OnPointerDown(pointer);
            pointer.position = Vector2.down * 42f;
            _button.OnDrag(pointer);
            Assert.That(_radialGuide.IsExpanded, Is.True);
            Assert.That(
                PopulateGuideAndGetVertexCount(),
                Is.EqualTo(oneSectorVertexCount * 4),
                "Hold and selection may recolor bound sectors but not create empty ones.");
            _button.OnPointerUp(pointer);
        }

        [Test]
        public void RadialGuide_LayersDepthAndBevelWhileStateOnlyChangesColor()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            _slot.BindAlternateUp(
                AbilityId.SerpentSting,
                null,
                "Serpent Sting");

            GuideMeshSnapshot resting = PopulateGuideMesh();
            const int VerticesPerBand = 40;
            Assert.That(resting.Positions.Length, Is.EqualTo(VerticesPerBand * 4));
            Assert.That(
                MinimumRadius(resting.Positions),
                Is.GreaterThan(PrimaryMedallionDiameter * 0.5f),
                "The shared wheel must not paint over the 72px center action.");
            Assert.That(
                MaximumRadius(resting.Positions),
                Is.LessThanOrEqualTo(UnifiedControlDiameter * 0.5f + 0.01f),
                "Layered depth must remain inside the unified gesture disc.");

            Color32 depthOuter = resting.Colors[1];
            Color32 faceInner = resting.Colors[VerticesPerBand];
            Color32 faceOuter = resting.Colors[VerticesPerBand + 1];
            Color32 outerBevelInner = resting.Colors[VerticesPerBand * 2];
            Color32 outerBevelEdge = resting.Colors[VerticesPerBand * 2 + 1];
            Assert.That(
                Luminance(depthOuter),
                Is.LessThan(Luminance(faceOuter)),
                "The offset foundation must read darker than the wedge face.");
            Assert.That(
                ColorDistance(faceInner, faceOuter),
                Is.GreaterThan(0.08f),
                "The face needs a visible inner-to-outer radial gradient.");
            Assert.That(
                Luminance(outerBevelEdge),
                Is.GreaterThan(Luminance(outerBevelInner)),
                "The outside edge must carry a restrained bevel highlight.");
            Assert.That(
                faceOuter.a,
                Is.GreaterThanOrEqualTo(240),
                "The main face must stay legible over bright lava.");

            PointerEventData pointer = Pointer(98, Vector2.zero);
            _button.OnPointerDown(pointer);
            GuideMeshSnapshot held = PopulateGuideMesh();
            AssertSamePositions(resting.Positions, held.Positions, "held");
            Color32 heldFaceOuter = held.Colors[VerticesPerBand + 1];
            Assert.That(heldFaceOuter.b, Is.GreaterThan(heldFaceOuter.r));
            Assert.That(
                ColorDistance(faceOuter, heldFaceOuter),
                Is.GreaterThan(0.08f),
                "Holding the disc must brighten its cool directional identity.");

            pointer.position = Vector2.up * 42f;
            _button.OnDrag(pointer);
            GuideMeshSnapshot selected = PopulateGuideMesh();
            AssertSamePositions(resting.Positions, selected.Positions, "selected");
            Color32 selectedFaceOuter = selected.Colors[VerticesPerBand + 1];
            Assert.That(selectedFaceOuter.r, Is.GreaterThan(selectedFaceOuter.g));
            Assert.That(selectedFaceOuter.g, Is.GreaterThan(selectedFaceOuter.b));
            Assert.That(
                ColorDistance(heldFaceOuter, selectedFaceOuter),
                Is.GreaterThan(0.2f),
                "The locked wedge needs an unmistakable gold treatment.");

            _button.OnPointerUp(pointer);
        }

        [Test]
        public void NonUpDirection_UsesPersistentWedgeAndLockedCentralFeedback()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Down,
                AbilityId.ArcaneShot);

            Assert.That(_alternateDownRoot.activeSelf, Is.True);
            Assert.That(
                _alternateDownRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(_alternateDownHint.gameObject.activeInHierarchy, Is.False);
            Assert.That(_actionLabel.text, Is.EqualTo("CHIMERA"));

            PointerEventData pointer = Pointer(95, Vector2.zero);
            _button.OnPointerDown(pointer);

            Assert.That(
                _alternateDownRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(_alternateDownHint.gameObject.activeInHierarchy, Is.True);
            Assert.That(_alternateDownHint.text, Is.EqualTo("↓ ARCANE"));

            pointer.position = new Vector2(0f, -42f);
            _button.OnDrag(pointer);

            Assert.That(_button.IsSelectionLocked, Is.True);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.ArcaneShot));
            Assert.That(_actionLabel.text, Is.EqualTo("↓ ARCANE"));
            Assert.That(
                _alternateDownRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Color expectedRim = Color.Lerp(
                AbilityPresentationCatalog.Get(AbilityId.ArcaneShot).Color,
                new Color32(232, 181, 94, 255),
                0.36f);
            expectedRim.a = 0.96f;
            AssertColorNear(_rim.color, expectedRim, "Selected alternate rim");

            _button.OnPointerUp(pointer);

            Assert.That(_actionLabel.text, Is.EqualTo("CHIMERA"));
            Assert.That(_alternateDownHint.gameObject.activeInHierarchy, Is.False);
            Assert.That(
                _alternateDownRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
        }

        [Test]
        public void UnifiedGestureCircle_KeepsAllFiveActionsFixedThroughInteraction()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            _slot.BindAlternateUp(
                AbilityId.SerpentSting,
                null,
                "Serpent Sting");
            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Down,
                AbilityId.ArcaneShot);
            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Left,
                AbilityId.KillShot);
            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Right,
                AbilityId.ScorpidSting);

            Assert.That(
                _slotObject.GetComponentsInChildren<AbilityButton>(true).Length,
                Is.EqualTo(1),
                "Center tap and all four flicks must share one input control.");
            Assert.That(_radialGuide.BoundDirectionCount, Is.EqualTo(4));
            Assert.That(_radialGuide.IsExpanded, Is.False);
            AssertUnifiedGestureGeometry("rest");
            AssertDirectionalCaptionsVisible(false, "rest");

            PointerEventData pointer = Pointer(97, Vector2.zero);
            _button.OnPointerDown(pointer);
            Assert.That(_radialGuide.IsExpanded, Is.True);
            Assert.That(
                _radialGuide.SelectedDirection,
                Is.EqualTo(AbilityFlickDirection.None));
            AssertUnifiedGestureGeometry("hold");
            AssertDirectionalCaptionsVisible(true, "hold");

            pointer.position = new Vector2(42f, 0f);
            _button.OnDrag(pointer);
            Assert.That(_button.IsSelectionLocked, Is.True);
            Assert.That(
                _button.SelectedAbilityId,
                Is.EqualTo(AbilityId.ScorpidSting));
            Assert.That(
                _radialGuide.SelectedDirection,
                Is.EqualTo(AbilityFlickDirection.Right));
            AssertUnifiedGestureGeometry("locked selection");
            AssertDirectionalCaptionsVisible(true, "locked selection");

            _button.OnPointerUp(pointer);
            Assert.That(_radialGuide.IsExpanded, Is.False);
            Assert.That(
                _radialGuide.SelectedDirection,
                Is.EqualTo(AbilityFlickDirection.None));
            AssertUnifiedGestureGeometry("release");
            AssertDirectionalCaptionsVisible(false, "release");
        }

        [Test]
        public void RebindingPrimary_ClearsStaleDirectionalAbilitiesAndWedge()
        {
            _slot.Bind(AbilityId.FlashHeal, null, "Flash Heal");
            _slot.BindAlternateUp(AbilityId.Renew, null, "Renew");

            _slot.Bind(AbilityId.Fireball, null, "Fireball");

            Assert.That(_slot.HasAlternateUp, Is.False);
            Assert.That(_alternateRoot.activeSelf, Is.False);
            Assert.That(_button.GetDirectionalAbility(AbilityFlickDirection.Up),
                Is.EqualTo(AbilityId.None));
        }

        [Test]
        public void AlternateCooldownAndReadinessRouteToSharedSlot()
        {
            _slot.Bind(AbilityId.FlashHeal, null, "Flash Heal");
            _slot.BindAlternateUp(AbilityId.Renew, null, "Renew");

            Assert.That(_slot.SetAbilityCooldown(AbilityId.Renew, 7f, 12f, false),
                Is.True);
            Assert.That(_slot.AlternateUpReadinessState,
                Is.EqualTo(AbilityReadinessState.Cooldown));
            Assert.That(_slot.ReadinessState, Is.EqualTo(AbilityReadinessState.Ready));

            _slot.SetAbilityCooldown(AbilityId.Renew, 0f, 12f, true);
            Assert.That(_slot.SetAbilityReadiness(
                    AbilityId.Renew,
                    AbilityReadinessState.OutOfRange,
                    "RANGE"),
                Is.True);
            Assert.That(_slot.AlternateUpReadinessState,
                Is.EqualTo(AbilityReadinessState.OutOfRange));
        }

        [Test]
        public void PrimarySlot_UsesLayeredFantasyMedallionAcrossHoverPressFlickLockAndCooldown()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Up,
                AbilityId.SerpentSting);
            AssertStableMedallionChrome(0.58f, 0.78f);
            AssertJewelInteriorAndDisabledRadials(0.11f);
            Assert.That(
                _slotObject.GetComponentsInChildren<Mask>(true),
                Is.Empty,
                "True-alpha ability art must not be cropped by a UI Mask.");

            PointerEventData pointer = Pointer(93, Vector2.zero);
            Color restingLabel = _actionLabel.color;
            _slot.OnPointerEnter(pointer);
            Assert.That(_slot.IsHovered, Is.True);
            Assert.That(_slot.IsPresentationSelected, Is.False);
            AssertStableMedallionChrome(0.72f, 0.9f);
            Assert.That(_interactionHalo.color.a, Is.EqualTo(0.26f).Within(0.001f));
            AssertJewelInteriorAndDisabledRadials(0.16f);
            Assert.That(_actionLabel.color, Is.Not.EqualTo(restingLabel),
                "Hover should emphasize both the forged ring and its caption.");

            Color hoveredLabel = _actionLabel.color;
            _button.OnPointerDown(pointer);
            AssertStableMedallionChrome(0.82f, 0.9f);
            Assert.That(_interactionHalo.color.a, Is.EqualTo(0.46f).Within(0.001f));
            AssertJewelInteriorAndDisabledRadials(0.2f);
            Assert.That(_actionLabel.color, Is.Not.EqualTo(hoveredLabel),
                "Press feedback should reinforce the medallion with caption emphasis and motion.");

            pointer.position = new Vector2(0f, 44f);
            _button.OnDrag(pointer);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.SerpentSting));
            Assert.That(_slot.IsPresentationSelected, Is.True);
            AssertStableMedallionChrome(0.96f, 0.96f);
            Assert.That(_interactionHalo.color.a, Is.EqualTo(0.68f).Within(0.001f));
            AssertJewelInteriorAndDisabledRadials(0.24f);

            _button.OnPointerUp(pointer);
            Assert.That(_slot.IsPresentationSelected, Is.False);
            Assert.That(_slot.IsHovered, Is.True);
            AssertStableMedallionChrome(0.72f, 0.9f);
            Assert.That(_interactionHalo.color.a, Is.EqualTo(0.26f).Within(0.001f));
            AssertJewelInteriorAndDisabledRadials(0.16f);
            _slot.OnPointerExit(pointer);
            Assert.That(_slot.IsHovered, Is.False);
            AssertStableMedallionChrome(0.58f, 0.78f);
            Assert.That(_interactionHalo.color.a, Is.Zero.Within(0.001f));
            AssertJewelInteriorAndDisabledRadials(0.11f);

            _slot.SetCooldown(4.2f, 8f, false);
            Assert.That(_slot.ReadinessState, Is.EqualTo(AbilityReadinessState.Cooldown));
            Assert.That(_cooldownFill.enabled, Is.False,
                "Cooldown must use icon tint and seconds, not a circular radial fill.");
            Assert.That(_cooldownFill.fillAmount, Is.EqualTo(0.525f).Within(0.001f));
            Assert.That(_cooldownText.text, Is.EqualTo("5"));
            Assert.That(_cooldownText.color.r, Is.GreaterThan(0.95f));
            Assert.That(_cooldownText.color.g, Is.InRange(0.7f, 0.75f));
            AssertStableMedallionChrome(0.58f, 0.92f);
            AssertColorNear(
                _rim.color,
                new Color32(255, 184, 69, 235),
                "Cooldown rim");
            Assert.That(_interactionHalo.color.a, Is.Zero.Within(0.001f));
            AssertJewelInteriorAndDisabledRadials(0.11f);

            _slot.SetCooldown(0f, 8f, true);
            _slot.SetGlobalCooldown(0.9f, 1.5f, true);
            Assert.That(
                _slot.ReadinessState,
                Is.EqualTo(AbilityReadinessState.GlobalCooldown));
            Assert.That(_globalCooldownFill.enabled, Is.False);
            Assert.That(_globalCooldownFill.color.a, Is.Zero.Within(0.001f));
            AssertStableMedallionChrome(0.58f, 0.92f);
            AssertColorNear(
                _rim.color,
                new Color32(122, 229, 255, 235),
                "Global-cooldown rim");
            Assert.That(_interactionHalo.color.a, Is.Zero.Within(0.001f));
            AssertJewelInteriorAndDisabledRadials(0.11f);
            _slot.SetGlobalCooldown(0f, 1.5f, false);
            _slot.SetReadiness(AbilityReadinessState.Dead, "DEAD");
            Assert.That(_blockedMark.activeSelf, Is.True);
            AssertStableMedallionChrome(0.58f, 0.88f);
            AssertColorNear(
                _rim.color,
                new Color(1f, 0.4f, 0.32f, 0.88f),
                "Readiness rim");
            Assert.That(_interactionHalo.color.a, Is.Zero.Within(0.001f));
            AssertJewelInteriorAndDisabledRadials(0.11f);
        }

        [Test]
        public void MissingTarget_KeepsAbilityIdentityOnThePrimaryAccentRing()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            _slot.SetReadiness(
                AbilityReadinessState.NoTarget,
                "SELECT TARGET");

            Color expectedRim = AbilityPresentationCatalog
                .Get(AbilityId.ChimeraShot)
                .Color;
            expectedRim.a = 0.88f;
            AssertColorNear(
                _rim.color,
                expectedRim,
                "No-target ability-identity rim");
            Assert.That(
                Vector3.Distance(
                    new Vector3(_innerFrame.color.r, _innerFrame.color.g, _innerFrame.color.b),
                    new Vector3(_rim.color.r, _rim.color.g, _rim.color.b)),
                Is.GreaterThan(0.05f),
                "The inner state lip may explain the blocker without flooding every rim red.");
        }

        [Test]
        public void MissingTarget_KeepsDirectionalWedgeCaptionInAbilityPalette()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Down,
                AbilityId.ArcaneShot);
            _slot.SetAbilityReadiness(
                AbilityId.ArcaneShot,
                AbilityReadinessState.NoTarget,
                "SELECT TARGET");

            PointerEventData pointer = Pointer(95, Vector2.zero);
            _button.OnPointerDown(pointer);

            Color abilityColor = AbilityPresentationCatalog
                .Get(AbilityId.ArcaneShot)
                .Color;
            Color expectedLabel = Color.Lerp(
                abilityColor,
                Color.white,
                0.5f);

            Assert.That(_alternateDownHint.text, Is.EqualTo("↓ ARCANE"));
            Assert.That(
                _alternateDownRoot.GetComponent<Graphic>(),
                Is.Null,
                "Directional art belongs inside the one radial control, not on a second medallion surface.");
            AssertColorNear(
                _alternateDownGlyph.color,
                Color.white,
                "No-target alternate glyph");
            AssertColorNear(
                _alternateDownHint.color,
                expectedLabel,
                "No-target alternate tag label");

            _button.OnPointerUp(pointer);
        }

        [Test]
        public void RepeatedHoverAndPressStyling_AfterWarmupAllocatesZeroManagedBytes()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            PointerEventData pointer = Pointer(94, Vector2.zero);

            RefreshInteractionStyling(pointer);
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            RefreshInteractionStyling(pointer);
            System.GC.GetAllocatedBytesForCurrentThread();

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 100; iteration++)
            {
                RefreshInteractionStyling(pointer);
            }

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(
                allocated,
                Is.Zero,
                "Warmed hover/pressed presentation transitions must not allocate.");
        }

        [Test]
        public void RepeatedAlternateStateRefresh_AfterWarmupAllocatesZeroManagedBytes()
        {
            _slot.Bind(AbilityId.ChimeraShot, null, "Chimera Shot");
            _slot.BindAlternateUp(
                AbilityId.SerpentSting,
                null,
                "Serpent Sting");
            _slot.BindDirectionalAbility(
                AbilitySlotDirection.Down,
                AbilityId.ArcaneShot);

            Assert.That(_alternateDownRoot.activeSelf, Is.True);
            Assert.That(
                _alternateDownRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(_alternateDownHint.gameObject.activeInHierarchy, Is.False);

            PointerEventData pointer = Pointer(92, Vector2.zero);
            _button.OnPointerDown(pointer);
            Assert.That(_alternateDownRoot.activeSelf, Is.True);
            Assert.That(
                _alternateDownRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(_alternateDownHint.gameObject.activeInHierarchy, Is.True);
            Assert.That(_alternateDownHint.text, Is.EqualTo("↓ ARCANE"));

            RefreshStableAlternateState();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            RefreshStableAlternateState();
            System.GC.GetAllocatedBytesForCurrentThread();

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 100; iteration++)
            {
                RefreshStableAlternateState();
            }

            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(
                allocated,
                Is.Zero,
                "Stable alternate cooldown/readiness/GCD refresh must not allocate.");
            _button.OnPointerUp(pointer);
            Assert.That(_alternateDownRoot.activeSelf, Is.True);
            Assert.That(
                _alternateDownRoot.transform.localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(_alternateDownHint.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator ParentDeactivation_DefersPaletteSiblingRestorationUntilActive()
        {
            var parent = new GameObject("AbilityPaletteParent", typeof(RectTransform));
            var before = new GameObject("Before", typeof(RectTransform));
            var after = new GameObject("After", typeof(RectTransform));
            before.transform.SetParent(parent.transform, false);
            _slotObject.transform.SetParent(parent.transform, false);
            after.transform.SetParent(parent.transform, false);
            Assert.That(_slotObject.transform.GetSiblingIndex(), Is.EqualTo(1));

            _slot.Bind(AbilityId.FlashHeal, null, "Flash Heal");
            _slot.BindAlternateUp(AbilityId.Renew, null, "Renew");
            PointerEventData pointer = Pointer(96, Vector2.zero);
            _button.OnPointerDown(pointer);
            Assert.That(_slotObject.transform.GetSiblingIndex(), Is.EqualTo(2));

            parent.SetActive(false);
            parent.SetActive(true);
            yield return null;

            Assert.That(
                _slotObject.transform.GetSiblingIndex(),
                Is.EqualTo(1),
                "The raised gesture slot must return to its authored layer after reactivation.");

            _slotObject.transform.SetParent(null, false);
            Object.DestroyImmediate(parent);
        }

        private void RefreshStableAlternateState()
        {
            _slot.SetAbilityCooldown(
                AbilityId.SerpentSting,
                6.2f,
                15f,
                false);
            _slot.SetAbilityReadiness(
                AbilityId.SerpentSting,
                AbilityReadinessState.Cooldown,
                "COOLDOWN");
            _slot.SetAbilityCooldown(
                AbilityId.ArcaneShot,
                6.2f,
                15f,
                false);
            _slot.SetAbilityReadiness(
                AbilityId.ArcaneShot,
                AbilityReadinessState.Cooldown,
                "COOLDOWN");
            _slot.SetGlobalCooldown(0.9f, 1.5f, true);
        }

        private void AssertUnifiedGestureGeometry(string phase)
        {
            Assert.That(
                _slotObject.GetComponent<RectTransform>().sizeDelta,
                Is.EqualTo(Vector2.one * UnifiedControlDiameter),
                phase + " unified control size");
            Assert.That(
                _slotObject.transform.localScale,
                Is.EqualTo(Vector3.one),
                phase + " unified control scale");
            Assert.That(
                _primaryVisualRoot.sizeDelta,
                Is.EqualTo(Vector2.one * PrimaryMedallionDiameter),
                phase + " center medallion size");
            Assert.That(
                _primaryVisualRoot.localScale,
                Is.EqualTo(Vector3.one),
                phase + " center medallion scale");
            Assert.That(
                _radialGuide.rectTransform.sizeDelta,
                Is.EqualTo(Vector2.one * UnifiedControlDiameter),
                phase + " radial guide size");

            AssertDirectionalWedgeGeometry(
                phase,
                "up",
                _alternateRoot,
                _alternateGlyph,
                new Vector2(0f, DirectionOffset));
            AssertDirectionalWedgeGeometry(
                phase,
                "down",
                _alternateDownRoot,
                _alternateDownGlyph,
                new Vector2(0f, -DirectionOffset));
            AssertDirectionalWedgeGeometry(
                phase,
                "left",
                _alternateLeftRoot,
                _alternateLeftGlyph,
                new Vector2(-DirectionOffset, 0f));
            AssertDirectionalWedgeGeometry(
                phase,
                "right",
                _alternateRightRoot,
                _alternateRightGlyph,
                new Vector2(DirectionOffset, 0f));
        }

        private static void AssertDirectionalWedgeGeometry(
            string phase,
            string direction,
            GameObject root,
            AbilityGlyphGraphic glyph,
            Vector2 expectedPosition)
        {
            RectTransform rootRect = (RectTransform)root.transform;
            Assert.That(
                root.transform.localScale,
                Is.EqualTo(Vector3.one),
                phase + " " + direction + " wedge scale");
            Assert.That(
                rootRect.sizeDelta,
                Is.EqualTo(Vector2.one * DirectionRootDiameter),
                phase + " " + direction + " wedge root size");
            Assert.That(
                rootRect.anchoredPosition,
                Is.EqualTo(expectedPosition),
                phase + " " + direction + " wedge position");
            Assert.That(
                glyph.rectTransform.sizeDelta,
                Is.EqualTo(Vector2.one * DirectionArtDiameter),
                phase + " " + direction + " art size");
            Assert.That(
                glyph.rectTransform.anchoredPosition,
                Is.EqualTo(Vector2.zero),
                phase + " " + direction + " art position");
        }

        private void AssertDirectionalCaptionsVisible(bool expected, string phase)
        {
            Assert.That(
                _alternateLabel.gameObject.activeInHierarchy,
                Is.EqualTo(expected),
                phase + " up caption");
            Assert.That(
                _alternateDownHint.gameObject.activeInHierarchy,
                Is.EqualTo(expected),
                phase + " down caption");
            Assert.That(
                _alternateLeftHint.gameObject.activeInHierarchy,
                Is.EqualTo(expected),
                phase + " left caption");
            Assert.That(
                _alternateRightHint.gameObject.activeInHierarchy,
                Is.EqualTo(expected),
                phase + " right caption");
            Assert.That(
                _alternateDownHint.rectTransform.anchoredPosition,
                Is.EqualTo(AuthoredDownCaptionPosition),
                phase + " down caption must stay below its icon");
            Assert.That(
                _alternateLeftHint.rectTransform.anchoredPosition,
                Is.EqualTo(AuthoredLeftCaptionPosition),
                phase + " left caption must stay below its icon");
            Assert.That(
                _alternateRightHint.rectTransform.anchoredPosition,
                Is.EqualTo(AuthoredRightCaptionPosition),
                phase + " right caption must stay below its icon");
        }

        private int PopulateGuideAndGetVertexCount()
        {
            using (var helper = new VertexHelper())
            {
                PopulateGuide(helper);
                return helper.currentVertCount;
            }
        }

        private GuideMeshSnapshot PopulateGuideMesh()
        {
            using (var helper = new VertexHelper())
            {
                PopulateGuide(helper);
                int vertexCount = helper.currentVertCount;
                var positions = new Vector3[vertexCount];
                var colors = new Color32[vertexCount];
                UIVertex vertex = default;
                for (int index = 0; index < vertexCount; index++)
                {
                    helper.PopulateUIVertex(ref vertex, index);
                    positions[index] = vertex.position;
                    colors[index] = vertex.color;
                }

                return new GuideMeshSnapshot(positions, colors);
            }
        }

        private void PopulateGuide(VertexHelper helper)
        {
            MethodInfo populate = typeof(AbilityRadialGuideGraphic).GetMethod(
                "OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(VertexHelper) },
                null);
            Assert.That(populate, Is.Not.Null);
            populate.Invoke(_radialGuide, new object[] { helper });
        }

        private static float MinimumRadius(Vector3[] positions)
        {
            float minimum = float.PositiveInfinity;
            for (int index = 0; index < positions.Length; index++)
            {
                minimum = Mathf.Min(minimum, positions[index].magnitude);
            }

            return minimum;
        }

        private static float MaximumRadius(Vector3[] positions)
        {
            float maximum = 0f;
            for (int index = 0; index < positions.Length; index++)
            {
                maximum = Mathf.Max(maximum, positions[index].magnitude);
            }

            return maximum;
        }

        private static float Luminance(Color32 color)
        {
            return (0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b) /
                255f;
        }

        private static float ColorDistance(Color32 first, Color32 second)
        {
            return (Mathf.Abs(first.r - second.r) +
                    Mathf.Abs(first.g - second.g) +
                    Mathf.Abs(first.b - second.b)) /
                (255f * 3f);
        }

        private static void AssertSamePositions(
            Vector3[] expected,
            Vector3[] actual,
            string phase)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), phase);
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(
                    actual[index],
                    Is.EqualTo(expected[index]),
                    phase + " vertex " + index);
            }
        }

        private void RefreshInteractionStyling(PointerEventData pointer)
        {
            _slot.OnPointerEnter(pointer);
            _slot.SetPressed(true);
            _slot.SetPressed(false);
            _slot.OnPointerExit(pointer);
        }

        private void AssertStableMedallionChrome(
            float expectedHighlightAlpha,
            float expectedRimAlpha)
        {
            Assert.That(_outerFrame.color.a, Is.EqualTo(0.96f).Within(0.001f));
            Assert.That(
                _frameHighlight.color.a,
                Is.EqualTo(expectedHighlightAlpha).Within(0.001f));
            Assert.That(_innerFrame.color.a, Is.EqualTo(0.92f).Within(0.001f));
            Assert.That(_rim.color.a, Is.EqualTo(expectedRimAlpha).Within(0.001f));
        }

        private void AssertJewelInteriorAndDisabledRadials(float expectedGlowAlpha)
        {
            Assert.That(
                _stateSurface.color.a,
                Is.EqualTo(0.94f).Within(0.001f),
                "The medallion needs an opaque-enough jewel well behind true-alpha art.");
            Assert.That(
                _iconGlow.color.a,
                Is.EqualTo(expectedGlowAlpha).Within(0.001f),
                "The jewel bloom must stay restrained and state-responsive.");
            Assert.That(_cooldownFill.enabled, Is.False);
            Assert.That(_cooldownFill.color.a, Is.Zero.Within(0.001f));
            Assert.That(_globalCooldownFill.enabled, Is.False);
            Assert.That(_globalCooldownFill.color.a, Is.Zero.Within(0.001f));
        }

        private static void AssertColorNear(
            Color actual,
            Color expected,
            string label)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.005f), label + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.005f), label + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.005f), label + " blue");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.005f), label + " alpha");
        }

        private PointerEventData Pointer(int pointerId, Vector2 position)
        {
            return new PointerEventData(_eventSystem)
            {
                pointerId = pointerId,
                button = PointerEventData.InputButton.Left,
                position = position,
            };
        }

        private void SetField(string name, object value)
        {
            FieldInfo field = typeof(AbilitySlotView).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(_slot, value);
        }

        private Image CreateImageChild(string name)
        {
            GameObject value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            value.transform.SetParent(_primaryVisualRoot, false);
            return value.GetComponent<Image>();
        }

        private Text CreateTextChild(string name)
        {
            GameObject value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            value.transform.SetParent(_primaryVisualRoot, false);
            return value.GetComponent<Text>();
        }

        private GameObject CreateDirectionalVisual(
            string name,
            Vector2 anchoredPosition,
            out AbilityGlyphGraphic glyph)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(_slotObject.transform, false);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = Vector2.one * DirectionRootDiameter;

            GameObject glyphObject = new GameObject(
                "Glyph",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(AbilityGlyphGraphic));
            glyphObject.transform.SetParent(root.transform, false);
            glyph = glyphObject.GetComponent<AbilityGlyphGraphic>();
            glyph.raycastTarget = false;
            RectTransform glyphRect = glyph.rectTransform;
            glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
            glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
            glyphRect.pivot = new Vector2(0.5f, 0.5f);
            glyphRect.anchoredPosition = Vector2.zero;
            glyphRect.sizeDelta = Vector2.one * DirectionArtDiameter;
            return root;
        }

        private Text CreateDirectionalHint(string name, Vector2 anchoredPosition)
        {
            GameObject hintObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            hintObject.transform.SetParent(_slotObject.transform, false);
            Text hint = hintObject.GetComponent<Text>();
            hint.raycastTarget = false;
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 0.5f);
            hintRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.anchoredPosition = anchoredPosition;
            return hint;
        }

        private readonly struct GuideMeshSnapshot
        {
            public GuideMeshSnapshot(Vector3[] positions, Color32[] colors)
            {
                Positions = positions;
                Colors = colors;
            }

            public Vector3[] Positions { get; }

            public Color32[] Colors { get; }
        }

    }
}
