using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class FourClassHudPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Home_AuthorsFourClassSelectorsAndShadowHunterSpecializations()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.Screen, Is.EqualTo(PrototypeHudScreen.Home));

            Button priestButton = GetPrivateField<Button>(hud, "_healerButton");
            Button rogueButton = GetPrivateField<Button>(hud, "_rogueButton");
            Button mageButton = GetPrivateField<Button>(hud, "_mageButton");
            Button hunterButton = GetPrivateField<Button>(hud, "_hunterButton");
            Button shadowButton = GetPrivateField<Button>(hud, "_shadowButton");
            Button marksmanshipButton = GetPrivateField<Button>(
                hud,
                "_marksmanshipButton");
            GameObject priestSpecializations = GetPrivateField<GameObject>(
                hud,
                "_disciplineSpecializationRoot");
            GameObject hunterSpecializations = GetPrivateField<GameObject>(
                hud,
                "_hunterSpecializationRoot");

            AssertAuthoredButton(priestButton, "Healer");
            AssertAuthoredButton(rogueButton, "Rogue");
            AssertAuthoredButton(mageButton, "Mage");
            AssertAuthoredButton(hunterButton, "Hunter");

            priestButton.onClick.Invoke();
            Assert.That(hud.SelectedClass, Is.EqualTo(ClassId.Priest));
            Assert.That(priestSpecializations.activeInHierarchy, Is.True);
            Assert.That(shadowButton.gameObject.activeInHierarchy, Is.True);

            shadowButton.onClick.Invoke();
            Assert.That(hud.SelectedSpecialization, Is.EqualTo(SpecializationId.Shadow));

            rogueButton.onClick.Invoke();
            Assert.That(hud.SelectedClass, Is.EqualTo(ClassId.Rogue));
            mageButton.onClick.Invoke();
            Assert.That(hud.SelectedClass, Is.EqualTo(ClassId.Mage));
            hunterButton.onClick.Invoke();
            Assert.That(hud.SelectedClass, Is.EqualTo(ClassId.Hunter));
            Assert.That(
                hud.SelectedSpecialization,
                Is.EqualTo(SpecializationId.Marksmanship));
            Assert.That(hunterSpecializations.activeInHierarchy, Is.True);
            Assert.That(priestSpecializations.activeInHierarchy, Is.False);
            Assert.That(marksmanshipButton.gameObject.activeInHierarchy, Is.True);

            marksmanshipButton.onClick.Invoke();
            Assert.That(
                hud.SelectedSpecialization,
                Is.EqualTo(SpecializationId.Marksmanship));

            priestButton.onClick.Invoke();
            Assert.That(hud.SelectedClass, Is.EqualTo(ClassId.Priest));
            Assert.That(
                hud.SelectedSpecialization,
                Is.EqualTo(SpecializationId.Shadow),
                "Returning to Priest must preserve the explicitly selected Shadow spec.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HunterRangedFamily_CommitsEveryCardinalFlickAndStillTapsPrimary()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.BindSpecializationKit(ClassId.Hunter, SpecializationId.Marksmanship);
            hud.ShowGameplay();
            yield return null;

            AbilitySlotView slot = hud.GetAbilitySlot(0);
            Assert.That(slot, Is.Not.Null);
            Assert.That(slot.AbilityId, Is.EqualTo(AbilityId.SerpentSting));
            AbilityButton button = slot.GetComponent<AbilityButton>();
            Assert.That(button, Is.Not.Null);

            AssertDirectionalBinding(
                slot,
                button,
                AbilitySlotDirection.Up,
                AbilityFlickDirection.Up,
                AbilityId.KillShot);
            AssertDirectionalBinding(
                slot,
                button,
                AbilitySlotDirection.Down,
                AbilityFlickDirection.Down,
                AbilityId.ScorpidSting);
            AssertDirectionalBinding(
                slot,
                button,
                AbilitySlotDirection.Left,
                AbilityFlickDirection.Left,
                AbilityId.TranquilizingShot);
            AssertDirectionalBinding(
                slot,
                button,
                AbilitySlotDirection.Right,
                AbilityFlickDirection.Right,
                AbilityId.ConcussiveShot);

            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null);
            AbilityGesture committedGesture = default;
            int committedCount = 0;
            button.GestureChanged += gesture =>
            {
                if (gesture.Phase == AbilityGesturePhase.Tapped ||
                    gesture.Phase == AbilityGesturePhase.Flicked)
                {
                    committedGesture = gesture;
                    committedCount++;
                }
            };

            CommitTap(button, eventSystem, 401);
            Assert.That(committedCount, Is.EqualTo(1));
            AssertGesture(
                committedGesture,
                AbilityGesturePhase.Tapped,
                AbilityId.SerpentSting,
                401);

            CommitFlick(button, eventSystem, 402, Vector2.up * 160f);
            AssertGesture(
                committedGesture,
                AbilityGesturePhase.Flicked,
                AbilityId.KillShot,
                402);
            CommitFlick(button, eventSystem, 403, Vector2.down * 160f);
            AssertGesture(
                committedGesture,
                AbilityGesturePhase.Flicked,
                AbilityId.ScorpidSting,
                403);
            CommitFlick(button, eventSystem, 404, Vector2.left * 160f);
            AssertGesture(
                committedGesture,
                AbilityGesturePhase.Flicked,
                AbilityId.TranquilizingShot,
                404);
            CommitFlick(button, eventSystem, 405, Vector2.right * 160f);
            AssertGesture(
                committedGesture,
                AbilityGesturePhase.Flicked,
                AbilityId.ConcussiveShot,
                405);
            Assert.That(committedCount, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator BothGestureFamilies_KeepUnifiedDiscGeometryAcrossInteractionStates()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.BindSpecializationKit(ClassId.Hunter, SpecializationId.Marksmanship);
            hud.ShowGameplay();
            yield return null;

            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null);
            for (int slotIndex = 0;
                 slotIndex < AbilityKit.GestureSlotCount;
                 slotIndex++)
            {
                AbilitySlotView slot = hud.GetAbilitySlot(slotIndex);
                AbilityButton button = slot.GetComponent<AbilityButton>();
                Assert.That(button, Is.Not.Null);
                AssertUnifiedGestureDiscGeometry(slot, false, "idle");

                hud.SetAbilityCooldown(slot.AbilityId, 2.4f, 8f, true);
                hud.SetGlobalCooldown(0.8f, 1.5f, true);
                AssertUnifiedGestureDiscGeometry(slot, false, "cooldown");

                PointerEventData pointer = new PointerEventData(eventSystem)
                {
                    pointerId = 460 + slotIndex,
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(
                        null,
                        slot.transform.position),
                };
                button.OnPointerDown(pointer);
                AssertUnifiedGestureDiscGeometry(slot, true, "pressed");

                Vector2 displacement = Vector2.right * 160f;
                pointer.delta = displacement;
                pointer.position += displacement;
                button.OnDrag(pointer);
                AssertUnifiedGestureDiscGeometry(slot, true, "flick-locked");

                button.OnPointerUp(pointer);
                AssertUnifiedGestureDiscGeometry(slot, false, "released");
                yield return null;
                AssertUnifiedGestureDiscGeometry(slot, false, "settled");
            }
        }

        [UnityTest]
        public IEnumerator EveryPlayableAbility_HasSpriteOrDedicatedVectorGlyph()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            AbilityIconCatalog catalog = GetPrivateField<AbilityIconCatalog>(
                hud,
                "_abilityIconCatalog");
            Assert.That(catalog, Is.Not.Null);

            SpecializationId[] specializations =
            {
                SpecializationId.Discipline,
                SpecializationId.Shadow,
                SpecializationId.ShadowDance,
                SpecializationId.Mutilate,
                SpecializationId.Fire,
                SpecializationId.Frost,
                SpecializationId.Marksmanship,
            };
            AbilitySlotDirection[] directions =
            {
                AbilitySlotDirection.None,
                AbilitySlotDirection.Up,
                AbilitySlotDirection.Down,
                AbilitySlotDirection.Left,
                AbilitySlotDirection.Right,
            };

            for (int specializationIndex = 0;
                 specializationIndex < specializations.Length;
                 specializationIndex++)
            {
                AbilityKit kit = SpecializationCatalog.GetKit(
                    specializations[specializationIndex]);
                for (int slot = 0; slot < AbilityKit.Capacity; slot++)
                {
                    for (int directionIndex = 0;
                         directionIndex < directions.Length;
                         directionIndex++)
                    {
                        AbilityId ability = kit.GetAbility(
                            slot,
                            directions[directionIndex]);
                        if (ability == AbilityId.None)
                        {
                            continue;
                        }

                        bool hasSprite = catalog.TryGetIcon(
                            ability,
                            out Sprite sprite) && sprite != null;
                        Assert.That(
                            hasSprite || AbilityGlyphGraphic.HasExplicitGlyph(ability),
                            Is.True,
                            ability + " has no authored button identity.");
                    }
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator EverySpecialization_ReusesTheSharedFantasyMedallionFrame()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.That(hud, Is.Not.Null);
            hud.ShowGameplay();

            SpecializationId[] specializations =
            {
                SpecializationId.Discipline,
                SpecializationId.Shadow,
                SpecializationId.ShadowDance,
                SpecializationId.Mutilate,
                SpecializationId.Fire,
                SpecializationId.Frost,
                SpecializationId.Marksmanship,
            };
            Transform[] frameInstances = new Transform[AbilityCatalog.AbilitySlotCount];
            Sprite sharedFrameSprite = null;
            int[] authoredChildCounts = new int[AbilityCatalog.AbilitySlotCount];

            for (int specializationIndex = 0;
                 specializationIndex < specializations.Length;
                 specializationIndex++)
            {
                SpecializationId specialization = specializations[specializationIndex];
                AbilityKit kit = SpecializationCatalog.GetKit(specialization);
                hud.BindSpecializationKit(kit.OwnerClass, specialization);
                yield return null;

                for (int slotIndex = 0;
                     slotIndex < AbilityCatalog.AbilitySlotCount;
                     slotIndex++)
                {
                    AbilitySlotView slot = hud.GetAbilitySlot(slotIndex);
                    Assert.That(slot, Is.Not.Null);
                    Assert.That(slot.AbilityId, Is.EqualTo(kit.GetAbility(slotIndex)));

                    Transform root = slot.transform;
                    Transform medallionRoot = slotIndex < AbilityKit.GestureSlotCount
                        ? root.Find("PrimaryMedallion")
                        : root;
                    Assert.That(medallionRoot, Is.Not.Null);
                    Transform outerBezel = medallionRoot.Find("OuterBezel");
                    Transform medallionFrame = medallionRoot.Find("MedallionFrameArt");
                    Transform metalShadow = medallionRoot.Find("MedallionShadow");
                    Transform backplate = medallionRoot.Find("Backplate");
                    Transform iconGlow = medallionRoot.Find("IconGlow");
                    Transform captionPlate = medallionRoot.Find("CaptionPlate");
                    Assert.That(outerBezel, Is.Not.Null);
                    Assert.That(medallionFrame, Is.Not.Null);
                    Assert.That(metalShadow, Is.Not.Null);
                    Assert.That(backplate, Is.Not.Null);
                    Assert.That(iconGlow, Is.Not.Null);
                    Assert.That(captionPlate, Is.Not.Null);

                    if (specializationIndex == 0)
                    {
                        frameInstances[slotIndex] = medallionFrame;
                        authoredChildCounts[slotIndex] = root.childCount;
                    }
                    else
                    {
                        Assert.That(
                            medallionFrame,
                            Is.SameAs(frameInstances[slotIndex]),
                            "Class rebinding must reuse the shared medallion frame.");
                        Assert.That(
                            root.childCount,
                            Is.EqualTo(authoredChildCounts[slotIndex]),
                            "Class rebinding must not add a class-specific shell.");
                    }

                    Image inputSurface = medallionRoot.Find("RootSurface").GetComponent<Image>();
                    Image frameArt = medallionFrame.GetComponent<Image>();
                    Image jewelWell = backplate.GetComponent<Image>();
                    Image jewelBloom = iconGlow.GetComponent<Image>();
                    Assert.That(inputSurface.raycastTarget, Is.True);
                    Assert.That(inputSurface.color.a, Is.GreaterThanOrEqualTo(0.95f));
                    Assert.That(frameArt.sprite, Is.Not.Null);
                    Assert.That(frameArt.type, Is.EqualTo(Image.Type.Simple));
                    Assert.That(frameArt.preserveAspect, Is.True);
                    Assert.That(frameArt.raycastTarget, Is.False);
                    Assert.That(
                        medallionFrame.GetSiblingIndex(),
                        Is.GreaterThan(medallionRoot.Find("Cooldown").GetSiblingIndex()));
                    Assert.That(
                        medallionFrame.GetSiblingIndex(),
                        Is.GreaterThan(medallionRoot.Find("GlobalCooldown").GetSiblingIndex()));
                    if (sharedFrameSprite == null)
                    {
                        sharedFrameSprite = frameArt.sprite;
                    }
                    else
                    {
                        Assert.That(frameArt.sprite, Is.SameAs(sharedFrameSprite));
                    }
                    Assert.That(jewelWell.raycastTarget, Is.False);
                    Assert.That(jewelWell.color.a, Is.InRange(0.9f, 0.97f));
                    Assert.That(jewelBloom.raycastTarget, Is.False);
                    Assert.That(jewelBloom.color.a, Is.InRange(0.08f, 0.25f));
                    Assert.That(captionPlate.GetComponent<Outline>(), Is.Not.Null);
                    Assert.That(captionPlate.GetComponent<Shadow>(), Is.Not.Null);

                    int ornamentCount = 0;
                    Image[] images = root.GetComponentsInChildren<Image>(true);
                    for (int imageIndex = 0; imageIndex < images.Length; imageIndex++)
                    {
                        Image image = images[imageIndex];
                        if (image.transform.parent == root &&
                            image.name.StartsWith("Stud_"))
                        {
                            ornamentCount++;
                            Assert.That(image.raycastTarget, Is.False);
                        }
                    }

                    Assert.That(ornamentCount, Is.Zero);
                    Assert.That(root.GetComponentsInChildren<Mask>(true), Is.Empty);
                    Assert.That(root.GetComponentsInChildren<RectMask2D>(true), Is.Empty);
                }
            }
        }

        [Test]
        public void AbilityGlyphs_StayInsidePrimaryAndAlternateButtonBounds()
        {
            GameObject glyphObject = new GameObject(
                "AbilityGlyphBoundsProbe",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(AbilityGlyphGraphic));
            RectTransform glyphRect = glyphObject.GetComponent<RectTransform>();
            AbilityGlyphGraphic glyph = glyphObject.GetComponent<AbilityGlyphGraphic>();
            MethodInfo populateMesh = typeof(AbilityGlyphGraphic).GetMethod(
                "OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(VertexHelper) },
                null);
            Assert.That(populateMesh, Is.Not.Null);

            float[] authoredSizes = { 128f, 30f };
            try
            {
                for (int sizeIndex = 0; sizeIndex < authoredSizes.Length; sizeIndex++)
                {
                    float authoredSize = authoredSizes[sizeIndex];
                    glyphRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        authoredSize);
                    glyphRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Vertical,
                        authoredSize);
                    Rect bounds = glyphRect.rect;

                    for (int value = (int)AbilityId.None;
                         value <= (int)AbilityId.ArrowRain;
                         value++)
                    {
                        AbilityId abilityId = (AbilityId)value;
                        glyph.Bind(abilityId, Color.cyan);
                        using (VertexHelper vertices = new VertexHelper())
                        {
                            populateMesh.Invoke(glyph, new object[] { vertices });
                            Assert.That(
                                vertices.currentVertCount,
                                Is.GreaterThan(0),
                                abilityId + " produced an empty glyph.");

                            UIVertex vertex = default;
                            for (int vertexIndex = 0;
                                 vertexIndex < vertices.currentVertCount;
                                 vertexIndex++)
                            {
                                vertices.PopulateUIVertex(ref vertex, vertexIndex);
                                Assert.That(
                                    vertex.position.x,
                                    Is.InRange(bounds.xMin - 0.01f, bounds.xMax + 0.01f),
                                    abilityId + " escaped the " + authoredSize + " px button horizontally.");
                                Assert.That(
                                    vertex.position.y,
                                    Is.InRange(bounds.yMin - 0.01f, bounds.yMax + 0.01f),
                                    abilityId + " escaped the " + authoredSize + " px button vertically.");
                            }
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(glyphObject);
            }
        }

        [UnityTest]
        public IEnumerator HunterDirectionalHints_ExposeCooldownReadinessAndGcdStates()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.BindSpecializationKit(ClassId.Hunter, SpecializationId.Marksmanship);
            hud.ShowGameplay();
            yield return null;

            AbilitySlotView controlSlot = hud.GetAbilitySlot(1);
            Assert.That(controlSlot.AbilityId, Is.EqualTo(AbilityId.ScatterShot));
            Assert.That(
                controlSlot.GetDirectionalAbility(AbilitySlotDirection.Up),
                Is.EqualTo(AbilityId.FreezingArrow));
            Assert.That(
                controlSlot.GetDirectionalAbility(AbilitySlotDirection.Down),
                Is.EqualTo(AbilityId.FrostTrap));
            Assert.That(
                controlSlot.GetDirectionalAbility(AbilitySlotDirection.Left),
                Is.EqualTo(AbilityId.SilencingShot));
            Assert.That(
                controlSlot.GetDirectionalAbility(AbilitySlotDirection.Right),
                Is.EqualTo(AbilityId.Disengage));
            Text downHint = RequireChildText(controlSlot.transform, "AlternateDownHint");
            Text leftHint = RequireChildText(controlSlot.transform, "AlternateLeftHint");
            Text rightHint = RequireChildText(controlSlot.transform, "AlternateRightHint");
            AssertHiddenHint(downHint);
            AssertHiddenHint(leftHint);
            AssertHiddenHint(rightHint);
            AssertDirectionTag(controlSlot.transform, "AlternateDownTag", 1f);
            AssertDirectionTag(controlSlot.transform, "AlternateLeftTag", 1f);
            AssertDirectionTag(controlSlot.transform, "AlternateRightTag", 1f);
            AssertDirectionTagPaintedIcon(
                controlSlot.transform,
                "AlternateDownTag");
            AssertDirectionTagPaintedIcon(
                controlSlot.transform,
                "AlternateLeftTag");
            AssertDirectionTagPaintedIcon(
                controlSlot.transform,
                "AlternateRightTag");

            AbilitySlotView stingSlot = hud.GetAbilitySlot(0);
            RectTransform restingMedallion =
                (RectTransform)stingSlot.transform.Find("AlternateUp");
            Assert.That(restingMedallion, Is.Not.Null);
            Assert.That(restingMedallion.rect.size, Is.EqualTo(new Vector2(60f, 60f)));
            Assert.That(
                restingMedallion.gameObject.activeInHierarchy,
                Is.True,
                "A bound direction needs a subtle resting marker.");
            Assert.That(
                restingMedallion.localScale,
                Is.EqualTo(Vector3.one));
            AbilityGlyphGraphic restingGlyph =
                restingMedallion.Find("Glyph").GetComponent<AbilityGlyphGraphic>();
            Image restingIcon =
                restingMedallion.Find("Icon").GetComponent<Image>();
            Assert.That(restingIcon.enabled, Is.True);
            Assert.That(restingIcon.sprite, Is.Not.Null);
            Assert.That(
                restingIcon.rectTransform.rect.size,
                Is.EqualTo(new Vector2(48f, 48f)));
            Assert.That(
                restingIcon.color.a,
                Is.GreaterThanOrEqualTo(0.85f),
                "A resting swipe marker should preview its painted ability art.");
            Assert.That(restingGlyph.enabled, Is.False);
            Assert.That(
                restingGlyph.rectTransform.anchorMin,
                Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(
                restingGlyph.rectTransform.anchorMax,
                Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(restingGlyph.rectTransform.rect.width, Is.GreaterThanOrEqualTo(30f));

            hud.SetAbilityCooldown(AbilityId.SilencingShot, 6.2f, 40f, true);
            AssertHiddenHint(leftHint);

            hud.SetAbilityReadiness(
                AbilityId.FrostTrap,
                AbilityReadinessState.Unavailable,
                "UNAVAILABLE");
            AssertHiddenHint(downHint);

            hud.SetGlobalCooldown(0.9f, 1.5f, true);

            AbilityButton controlButton = controlSlot.GetComponent<AbilityButton>();
            PointerEventData pointer = new PointerEventData(
                Object.FindAnyObjectByType<EventSystem>())
            {
                pointerId = 442,
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    controlSlot.transform.position),
            };
            controlButton.OnPointerDown(pointer);
            Assert.That(downHint.gameObject.activeInHierarchy, Is.True);
            Assert.That(leftHint.gameObject.activeInHierarchy, Is.True);
            Assert.That(rightHint.gameObject.activeInHierarchy, Is.True);
            Assert.That(
                downHint.rectTransform.anchoredPosition,
                Is.EqualTo(new Vector2(0f, -92f)),
                "The held Down caption must remain below its wedge icon.");
            Assert.That(
                leftHint.rectTransform.anchoredPosition,
                Is.EqualTo(new Vector2(-78f, -14f)),
                "The held Left caption must remain below its wedge icon.");
            Assert.That(
                rightHint.rectTransform.anchoredPosition,
                Is.EqualTo(new Vector2(78f, -14f)),
                "The held Right caption must remain below its wedge icon.");
            AssertDirectionTag(controlSlot.transform, "AlternateDownTag", 1f);
            AssertDirectionTag(controlSlot.transform, "AlternateLeftTag", 1f);
            AssertDirectionTag(controlSlot.transform, "AlternateRightTag", 1f);
            AbilityDefinition disengage = AbilityCatalog.Get(AbilityId.Disengage);
            Assert.That(disengage.GlobalCooldownSeconds, Is.Zero);
            Assert.That(
                (disengage.Requirements &
                 AbilityRequirementFlags.IgnoresGlobalCooldown) != 0,
                Is.True);
            Assert.That(
                rightHint.text,
                Is.EqualTo("→ DISENGA"),
                "Disengage ignores the active GCD, so its ready action identity must remain visible.");
            Assert.That(
                leftHint.text,
                Is.EqualTo("← 7"),
                "An ability cooldown must remain more specific than the GCD.");
            Assert.That(
                downHint.text,
                Is.EqualTo("↓ GCD"),
                "An active GCD is the immediate Frost Trap blocker.");
            hud.SetGlobalCooldown(0f, 0f, false);
            Assert.That(
                downHint.text,
                Is.EqualTo("↓ LOCKED"),
                "Frost Trap must reveal its explicit readiness blocker after the GCD.");
            controlButton.OnPointerUp(pointer);
            AssertHiddenHint(leftHint);
            AssertHiddenHint(downHint);
            AssertHiddenHint(rightHint);
            AssertDirectionTag(controlSlot.transform, "AlternateDownTag", 1f);
            AssertDirectionTag(controlSlot.transform, "AlternateLeftTag", 1f);
            AssertDirectionTag(controlSlot.transform, "AlternateRightTag", 1f);

            hud.SetGlobalCooldown(0.9f, 1.5f, true);
            Text stingUpState = RequireChildText(
                stingSlot.transform,
                "AlternateUp/CooldownText");
            Assert.That(stingUpState.enabled, Is.False);
            AbilityButton stingButton = stingSlot.GetComponent<AbilityButton>();
            pointer.pointerId = 443;
            pointer.position = RectTransformUtility.WorldToScreenPoint(
                null,
                stingSlot.transform.position);
            stingButton.OnPointerDown(pointer);
            Assert.That(stingUpState.enabled, Is.True);
            Assert.That(stingUpState.text, Is.EqualTo("GCD"));
            Assert.That(stingUpState.gameObject.activeInHierarchy, Is.True);
            stingButton.OnPointerUp(pointer);
            Assert.That(stingUpState.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator EveryArenaKitBinding_UsesMatchingPaintedSpriteAndClearsStaleArt()
        {
            PrototypeHud hud = Object.FindAnyObjectByType<PrototypeHud>();
            AbilityIconCatalog catalog = GetPrivateField<AbilityIconCatalog>(
                hud,
                "_abilityIconCatalog");
            Assert.That(catalog, Is.Not.Null);
            hud.ShowGameplay();

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
                SpecializationId specialization =
                    specializations[specializationIndex];
                ClassId classId =
                    SpecializationCatalog.GetDefinition(specialization).OwnerClass;
                hud.BindSpecializationKit(classId, specialization);
                yield return null;

                AbilityKit kit = SpecializationCatalog.GetKit(specialization);
                for (int slot = 0; slot < AbilityKit.Capacity; slot++)
                {
                    AbilitySlotView slotView = hud.GetAbilitySlot(slot);
                    Assert.That(slotView, Is.Not.Null);
                    AbilitySlotBinding binding = kit.GetSlotBinding(slot);
                    Transform primaryRoot = slot < AbilityKit.GestureSlotCount
                        ? slotView.transform.Find("PrimaryMedallion")
                        : slotView.transform;
                    Assert.That(primaryRoot, Is.Not.Null);
                    AssertPaintedBinding(
                        primaryRoot,
                        "Icon",
                        "Glyph",
                        binding.Primary,
                        catalog,
                        specialization + " slot " + slot + " primary");
                    AssertDirectionalPaintedBinding(
                        slotView.transform,
                        "AlternateUp",
                        binding.Up,
                        catalog,
                        specialization + " slot " + slot + " up");
                    AssertDirectionalPaintedBinding(
                        slotView.transform,
                        "AlternateDownTag",
                        binding.Down,
                        catalog,
                        specialization + " slot " + slot + " down");
                    AssertDirectionalPaintedBinding(
                        slotView.transform,
                        "AlternateLeftTag",
                        binding.Left,
                        catalog,
                        specialization + " slot " + slot + " left");
                    AssertDirectionalPaintedBinding(
                        slotView.transform,
                        "AlternateRightTag",
                        binding.Right,
                        catalog,
                        specialization + " slot " + slot + " right");
                }
            }
        }

        [UnityTest]
        public IEnumerator HunterSession_HasCompleteAuthoredCompanionVisualReferences()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(
                GameModeId.Arena,
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            yield return new WaitForFixedUpdate();
            yield return null;

            CombatantView[] combatants = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(combatants, Has.Length.EqualTo(4));

            HunterCompanionView humanCompanion = null;
            for (int index = 0; index < combatants.Length; index++)
            {
                CombatantView combatant = combatants[index];
                Assert.That(
                    combatant.CompanionVisuals,
                    Is.Not.Null,
                    combatant.name + " is missing the prefab's companion reference.");
                if (combatant.IsHuman)
                {
                    humanCompanion = combatant.CompanionVisuals;
                }
            }

            Assert.That(humanCompanion, Is.Not.Null);
            Assert.That(humanCompanion.HasAuthoredOrderLanguage, Is.True);
            Assert.That(
                GetPrivateField<GameObject>(humanCompanion, "_visualRoot"),
                Is.Not.Null);
            Assert.That(
                GetPrivateField<Transform>(humanCompanion, "_healthFill"),
                Is.Not.Null);
            Assert.That(
                GetPrivateField<Renderer>(humanCompanion, "_orderMarker"),
                Is.Not.Null);
            Assert.That(
                GetPrivateField<GameObject>(humanCompanion, "_attackOrderRoot"),
                Is.Not.Null);
            Assert.That(
                GetPrivateField<GameObject>(humanCompanion, "_followOrderRoot"),
                Is.Not.Null);
            Assert.That(
                GetPrivateField<GameObject>(humanCompanion, "_stayOrderRoot"),
                Is.Not.Null);

            Renderer[] accentRenderers = GetPrivateField<Renderer[]>(
                humanCompanion,
                "_accentRenderers");
            Transform[] gaitLimbs = GetPrivateField<Transform[]>(
                humanCompanion,
                "_gaitLimbs");
            Renderer[] orderRenderers = GetPrivateField<Renderer[]>(
                humanCompanion,
                "_orderRenderers");
            Assert.That(accentRenderers, Is.Not.Empty);
            AssertNoNullElements(accentRenderers, "companion accent renderer");
            Assert.That(gaitLimbs, Has.Length.EqualTo(16));
            AssertNoNullElements(gaitLimbs, "companion gait limb");
            Assert.That(
                orderRenderers,
                Has.Length.EqualTo(HunterCompanionView.AuthoredOrderRendererCount));
            AssertNoNullElements(orderRenderers, "companion order renderer");
        }

        private static void AssertAuthoredButton(Button button, string expectedName)
        {
            Assert.That(button, Is.Not.Null, expectedName + " selector is not wired.");
            Assert.That(button.name, Is.EqualTo(expectedName));
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            Assert.That(button.interactable, Is.True);
        }

        private static void AssertDirectionalBinding(
            AbilitySlotView slot,
            AbilityButton button,
            AbilitySlotDirection slotDirection,
            AbilityFlickDirection inputDirection,
            AbilityId abilityId)
        {
            Assert.That(slot.GetDirectionalAbility(slotDirection), Is.EqualTo(abilityId));
            Assert.That(button.GetDirectionalAbility(inputDirection), Is.EqualTo(abilityId));
            Assert.That(slot.ContainsAbility(abilityId), Is.True);
        }

        private static void AssertGesture(
            AbilityGesture gesture,
            AbilityGesturePhase phase,
            AbilityId abilityId,
            int pointerId)
        {
            Assert.That(gesture.Phase, Is.EqualTo(phase));
            Assert.That(gesture.AbilityId, Is.EqualTo(abilityId));
            Assert.That(gesture.PointerId, Is.EqualTo(pointerId));
        }

        private static void AssertHiddenHint(Text hint)
        {
            Assert.That(hint.gameObject.activeInHierarchy, Is.False);
        }

        private static void AssertUnifiedGestureDiscGeometry(
            AbilitySlotView slot,
            bool expectsExpandedGuide,
            string state)
        {
            string context = slot.name + " " + state;
            RectTransform root = (RectTransform)slot.transform;
            Assert.That(
                root.localScale,
                Is.EqualTo(new Vector3(1.3f, 1.3f, 1f)),
                context + " must retain the 30% enlargement through interaction and rebinding");
            Assert.That(
                root.rect.size,
                Is.EqualTo(new Vector2(232f, 232f)),
                context + " unified disc");

            RectTransform touchSurface = (RectTransform)root.Find(
                "GestureTouchSurface");
            Assert.That(touchSurface, Is.Not.Null, context + " gesture touch surface");
            Assert.That(
                touchSurface.rect.size,
                Is.EqualTo(new Vector2(232f, 232f)),
                context + " gesture touch surface size");
            Image touchImage = touchSurface.GetComponent<Image>();
            Assert.That(touchImage, Is.Not.Null, context + " gesture touch image");
            Assert.That(touchImage.raycastTarget, Is.True, context + " hit target");

            RectTransform clusterShadow = (RectTransform)root.Find("ClusterShadow");
            RectTransform clusterBacking = (RectTransform)root.Find("ClusterBacking");
            RectTransform clusterOuterBezel = (RectTransform)root.Find(
                "ClusterOuterBezel");
            RectTransform clusterOuterHighlight = (RectTransform)root.Find(
                "ClusterOuterHighlight");
            RectTransform clusterAccentRing = (RectTransform)root.Find(
                "ClusterAccentRing");
            Assert.That(clusterShadow, Is.Not.Null, context + " cluster shadow");
            Assert.That(clusterBacking, Is.Not.Null, context + " cluster backing");
            Assert.That(clusterOuterBezel, Is.Not.Null, context + " cluster bezel");
            Assert.That(
                clusterOuterHighlight,
                Is.Not.Null,
                context + " cluster highlight");
            Assert.That(
                clusterAccentRing,
                Is.Not.Null,
                context + " cluster accent");
            Assert.That(
                clusterShadow.rect.size,
                Is.EqualTo(new Vector2(242f, 242f)),
                context + " cluster shadow size");
            Assert.That(
                clusterBacking.rect.size,
                Is.EqualTo(new Vector2(224f, 224f)),
                context + " cluster backing size");
            Assert.That(
                clusterOuterBezel.rect.size,
                Is.EqualTo(new Vector2(230f, 230f)),
                context + " cluster bezel size");
            Assert.That(
                clusterOuterHighlight.rect.size,
                Is.EqualTo(new Vector2(222f, 222f)),
                context + " cluster highlight size");
            Assert.That(
                clusterAccentRing.rect.size,
                Is.EqualTo(new Vector2(214f, 214f)),
                context + " cluster accent size");
            Assert.That(
                clusterBacking.GetComponent<Image>().raycastTarget,
                Is.False,
                context + " cluster backing input");
            Assert.That(
                clusterOuterBezel.GetComponent<AbilityRingGraphic>().raycastTarget,
                Is.False,
                context + " cluster bezel input");
            Assert.That(
                clusterOuterHighlight.GetComponent<AbilityRingGraphic>().raycastTarget,
                Is.False,
                context + " cluster highlight input");
            Assert.That(
                clusterAccentRing.GetComponent<AbilityRingGraphic>().raycastTarget,
                Is.False,
                context + " cluster accent input");

            string[] ornamentNames =
            {
                "ClusterCardinalTickUp",
                "ClusterCardinalTickDown",
                "ClusterCardinalTickLeft",
                "ClusterCardinalTickRight",
            };
            Vector2[] ornamentPositions =
            {
                new Vector2(0f, 105f),
                new Vector2(0f, -105f),
                new Vector2(-105f, 0f),
                new Vector2(105f, 0f),
            };
            for (int ornamentIndex = 0;
                 ornamentIndex < ornamentNames.Length;
                 ornamentIndex++)
            {
                RectTransform ornament = (RectTransform)root.Find(
                    ornamentNames[ornamentIndex]);
                Assert.That(ornament, Is.Not.Null, context + " cardinal ornament");
                Assert.That(
                    ornament.anchoredPosition,
                    Is.EqualTo(ornamentPositions[ornamentIndex]),
                    context + " cardinal ornament position");
                Vector2 expectedOrnamentSize = ornamentIndex < 2
                    ? new Vector2(16f, 4f)
                    : new Vector2(4f, 16f);
                Assert.That(
                    ornament.rect.size,
                    Is.EqualTo(expectedOrnamentSize),
                    context + " cardinal ornament size");
                Assert.That(
                    ornament.localScale,
                    Is.EqualTo(Vector3.one),
                    context + " cardinal ornament scale");
                Assert.That(
                    ornament.GetComponent<Image>().raycastTarget,
                    Is.False,
                    context + " cardinal ornament input");
            }

            RectTransform primary = (RectTransform)root.Find("PrimaryMedallion");
            Assert.That(primary, Is.Not.Null, context + " primary medallion");
            Assert.That(
                primary.rect.size,
                Is.EqualTo(new Vector2(84f, 84f)),
                context + " primary size");
            Assert.That(primary.anchoredPosition, Is.EqualTo(Vector2.zero), context);
            Assert.That(primary.localScale, Is.EqualTo(Vector3.one), context);
            Assert.That(
                ((RectTransform)primary.Find("Icon")).rect.size,
                Is.EqualTo(new Vector2(48f, 48f)),
                context + " primary icon");
            Assert.That(
                ((RectTransform)primary.Find("Glyph")).rect.size,
                Is.EqualTo(new Vector2(48f, 48f)),
                context + " primary glyph");

            AbilityRadialGuideGraphic guide = root
                .Find("DirectionalRadialGuide")
                .GetComponent<AbilityRadialGuideGraphic>();
            Assert.That(guide, Is.Not.Null, context + " radial guide");
            Assert.That(
                ((RectTransform)guide.transform).rect.size,
                Is.EqualTo(new Vector2(232f, 232f)),
                context + " radial guide size");
            Assert.That(guide.gameObject.activeInHierarchy, Is.True, context);
            Assert.That(guide.enabled, Is.True, context);
            Assert.That(guide.BoundDirectionCount, Is.EqualTo(4), context);
            Assert.That(guide.IsExpanded, Is.EqualTo(expectsExpandedGuide), context);

            string[] names =
            {
                "AlternateUp",
                "AlternateDownTag",
                "AlternateLeftTag",
                "AlternateRightTag",
            };
            Vector2[] positions =
            {
                new Vector2(0f, 78f),
                new Vector2(0f, -78f),
                new Vector2(-78f, 0f),
                new Vector2(78f, 0f),
            };
            for (int directionIndex = 0;
                 directionIndex < names.Length;
                 directionIndex++)
            {
                RectTransform direction = (RectTransform)root.Find(
                    names[directionIndex]);
                string directionContext = context + " " + names[directionIndex];
                Assert.That(direction, Is.Not.Null, directionContext);
                Assert.That(
                    direction.gameObject.activeInHierarchy,
                    Is.True,
                    directionContext + " visibility");
                Assert.That(
                    direction.rect.size,
                    Is.EqualTo(new Vector2(60f, 60f)),
                    directionContext + " size");
                Assert.That(
                    direction.anchoredPosition,
                    Is.EqualTo(positions[directionIndex]),
                    directionContext + " position");
                Assert.That(
                    direction.localScale,
                    Is.EqualTo(Vector3.one),
                    directionContext + " scale");
                Assert.That(
                    direction.GetComponent<Image>(),
                    Is.Null,
                    directionContext + " root must remain a transparent icon region");
                Assert.That(direction.Find("Surface"), Is.Null, directionContext);
                Assert.That(direction.Find("OuterBezel"), Is.Null, directionContext);
                Assert.That(direction.Find("DirectionAccent"), Is.Null, directionContext);
                Assert.That(direction.Find("MedallionFrameArt"), Is.Null, directionContext);
                Assert.That(
                    ((RectTransform)direction.Find("Icon")).rect.size,
                    Is.EqualTo(new Vector2(48f, 48f)),
                    directionContext + " icon");
                Assert.That(
                    ((RectTransform)direction.Find("Glyph")).rect.size,
                    Is.EqualTo(new Vector2(48f, 48f)),
                    directionContext + " glyph");
                string[] expectedChildren = directionIndex == 0
                    ? new[] { "Icon", "Glyph", "Label", "Cooldown", "CooldownText" }
                    : new[] { "Icon", "Glyph" };
                Assert.That(
                    direction.Cast<Transform>().Select(child => child.name),
                    Is.EquivalentTo(expectedChildren),
                    directionContext + " may not add per-direction chrome");
            }
        }

        private static void AssertDirectionTag(
            Transform root,
            string name,
            float expectedScale)
        {
            RectTransform tag = (RectTransform)root.Find(name);
            Assert.That(tag, Is.Not.Null);
            Assert.That(tag.gameObject.activeInHierarchy, Is.True);
            Assert.That(tag.rect.size, Is.EqualTo(new Vector2(60f, 60f)));
            Assert.That(
                tag.localScale,
                Is.EqualTo(new Vector3(expectedScale, expectedScale, 1f)));
            Assert.That(
                tag.GetComponent<Image>(),
                Is.Null,
                "Directional icons must not restore the old rectangular chip surface.");
        }

        private static void CommitTap(
            AbilityButton button,
            EventSystem eventSystem,
            int pointerId)
        {
            PointerEventData pointer = Pointer(eventSystem, pointerId);
            button.OnPointerDown(pointer);
            button.OnPointerUp(pointer);
        }

        private static void CommitFlick(
            AbilityButton button,
            EventSystem eventSystem,
            int pointerId,
            Vector2 displacement)
        {
            PointerEventData pointer = Pointer(eventSystem, pointerId);
            button.OnPointerDown(pointer);
            pointer.delta = displacement;
            pointer.position += displacement;
            button.OnDrag(pointer);
            button.OnPointerUp(pointer);
        }

        private static PointerEventData Pointer(EventSystem eventSystem, int pointerId)
        {
            return new PointerEventData(eventSystem)
            {
                pointerId = pointerId,
                button = PointerEventData.InputButton.Left,
                position = new Vector2(900f, 420f),
            };
        }

        private static Text RequireChildText(Transform root, string path)
        {
            Transform child = root.Find(path);
            Assert.That(child, Is.Not.Null, "Missing authored HUD child " + path + ".");
            Text text = child.GetComponent<Text>();
            Assert.That(text, Is.Not.Null, path + " must be a Text hint.");
            return text;
        }

        private static void AssertDirectionTagPaintedIcon(
            Transform slot,
            string tagName)
        {
            Transform tag = slot.Find(tagName);
            Assert.That(tag, Is.Not.Null, "Missing authored HUD child " + tagName + ".");
            Transform iconTransform = tag.Find("Icon");
            Assert.That(iconTransform, Is.Not.Null, tagName + " needs an icon child.");
            Image icon = iconTransform.GetComponent<Image>();
            Assert.That(icon, Is.Not.Null, tagName + " icon must be a uGUI Image.");
            Assert.That(
                ((RectTransform)iconTransform).rect.size,
                Is.EqualTo(new Vector2(48f, 48f)),
                tagName + " must use the unified-disc directional icon size.");
            Assert.That(icon.enabled, Is.True, tagName + " must display painted art.");
            Assert.That(icon.sprite, Is.Not.Null, tagName + " has no painted sprite.");

            Transform glyphTransform = tag.Find("Glyph");
            Assert.That(glyphTransform, Is.Not.Null, tagName + " needs a fallback glyph.");
            Assert.That(
                glyphTransform.GetComponent<AbilityGlyphGraphic>().enabled,
                Is.False,
                tagName + " should not show vector fallback when painted art exists.");
        }

        private static void AssertDirectionalPaintedBinding(
            Transform slot,
            string rootName,
            AbilityId abilityId,
            AbilityIconCatalog catalog,
            string context)
        {
            Transform directionalRoot = slot.Find(rootName);
            Assert.That(
                directionalRoot,
                Is.Not.Null,
                context + " is missing authored root " + rootName + ".");
            bool hasBinding = abilityId != AbilityId.None;
            Assert.That(
                directionalRoot.gameObject.activeSelf,
                Is.EqualTo(hasBinding),
                context + " retained stale directional visibility.");
            AssertPaintedBinding(
                directionalRoot,
                "Icon",
                "Glyph",
                abilityId,
                catalog,
                context);
        }

        private static void AssertPaintedBinding(
            Transform root,
            string iconPath,
            string glyphPath,
            AbilityId abilityId,
            AbilityIconCatalog catalog,
            string context)
        {
            Transform iconTransform = root.Find(iconPath);
            Transform glyphTransform = root.Find(glyphPath);
            Assert.That(iconTransform, Is.Not.Null, context + " has no icon child.");
            Assert.That(glyphTransform, Is.Not.Null, context + " has no glyph child.");
            Image icon = iconTransform.GetComponent<Image>();
            AbilityGlyphGraphic glyph =
                glyphTransform.GetComponent<AbilityGlyphGraphic>();
            Assert.That(icon, Is.Not.Null, context + " icon is not a uGUI Image.");
            Assert.That(glyph, Is.Not.Null, context + " has no vector fallback.");

            if (abilityId == AbilityId.None)
            {
                Assert.That(icon.enabled, Is.False, context + " retained stale icon art.");
                Assert.That(glyph.enabled, Is.False, context + " retained stale glyph art.");
                return;
            }

            Assert.That(
                catalog.TryGetIcon(abilityId, out Sprite expectedSprite),
                Is.True,
                context + " has no catalog sprite for " + abilityId + ".");
            Assert.That(icon.enabled, Is.True, context + " hides painted art.");
            Assert.That(
                icon.sprite,
                Is.SameAs(expectedSprite),
                context + " displays the wrong ability sprite.");
            Assert.That(
                icon.color.a,
                Is.GreaterThan(0.01f),
                context + " painted art is fully transparent.");
            Assert.That(
                glyph.enabled,
                Is.False,
                context + " uses a vector fallback despite painted art.");
        }

        private static void AssertNoNullElements<T>(T[] values, string description)
            where T : Object
        {
            for (int index = 0; index < values.Length; index++)
            {
                Assert.That(
                    values[index],
                    Is.Not.Null,
                    description + " " + index + " is not wired.");
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
    }
}
