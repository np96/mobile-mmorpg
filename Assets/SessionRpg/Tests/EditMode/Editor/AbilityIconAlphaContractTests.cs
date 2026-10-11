using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Unity.Presentation;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class AbilityIconAlphaContractTests
    {
        private const string IconFolder =
            "Assets/SessionRpg/Art/UI/AbilityIcons";

        private const string CatalogPath =
            "Assets/SessionRpg/Settings/AbilityIconCatalog.asset";

        private const string TransparentSuffix = "_Transparent";
        private const int ExpectedIconCount = 108;
        private const int TransparentPerimeterWidth = 2;
        private const float MinimumTransparentCoverage = 0.10f;
        // Narrow bow/flare silhouettes and solid ice masses intentionally occupy
        // less antialiased fringe than the broader original paintings. These
        // floors still reject binary cutouts and rectangular matte leakage.
        private const float MinimumSoftCoverage = 0.065f;
        private const float MinimumLowAlphaCoverage = 0.03f;
        private const float MinimumMidAlphaCoverage = 0.015f;

        [Test]
        public void TransparentDerivatives_PreserveFullSilhouettesAndSmoothEdges()
        {
            string[] allPngs = Directory.GetFiles(
                IconFolder,
                "*.png",
                SearchOption.TopDirectoryOnly);
            string[] sources = allPngs
                .Where(path => !Path.GetFileNameWithoutExtension(path)
                    .EndsWith(TransparentSuffix, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            string[] derivatives = allPngs
                .Where(path => Path.GetFileNameWithoutExtension(path)
                    .EndsWith(TransparentSuffix, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(sources, Has.Length.EqualTo(ExpectedIconCount));
            Assert.That(derivatives, Has.Length.EqualTo(ExpectedIconCount));

            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                string source = sources[sourceIndex];
                string stem = Path.GetFileNameWithoutExtension(source);
                string derivative = Path.Combine(
                        IconFolder,
                        stem + TransparentSuffix + ".png")
                    .Replace('\\', '/');
                Assert.That(File.Exists(derivative), Is.True, derivative);

                byte[] sourceBytes = File.ReadAllBytes(source);
                byte[] derivativeBytes = File.ReadAllBytes(derivative);
                Assert.That(sourceBytes[25], Is.EqualTo(2),
                    source + " must remain an immutable RGB source PNG.");
                Assert.That(derivativeBytes[25], Is.EqualTo(6),
                    derivative + " must encode actual RGBA pixels, not a baked matte.");

                Texture2D decoded = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false);
                try
                {
                    Assert.That(
                        ImageConversion.LoadImage(decoded, derivativeBytes, false),
                        Is.True,
                        derivative);
                    Assert.That(decoded.width, Is.EqualTo(256));
                    Assert.That(decoded.height, Is.EqualTo(256));
                    Color32[] pixels = decoded.GetPixels32();
                    bool hasClearPixel = false;
                    bool hasSoftPixel = false;
                    bool hasLowAlphaEdgePixel = false;
                    bool hasMidAlphaEdgePixel = false;
                    bool hasReadableCorePixel = false;
                    int clearPixelCount = 0;
                    int softPixelCount = 0;
                    int lowAlphaEdgePixelCount = 0;
                    int midAlphaEdgePixelCount = 0;
                    for (int y = 0; y < decoded.height; y++)
                    {
                        for (int x = 0; x < decoded.width; x++)
                        {
                            byte alpha = pixels[y * decoded.width + x].a;
                            hasClearPixel |= alpha == 0;
                            hasSoftPixel |= alpha > 0 && alpha < 255;
                            hasLowAlphaEdgePixel |= alpha > 0 && alpha < 64;
                            hasMidAlphaEdgePixel |= alpha >= 64 && alpha < 230;
                            hasReadableCorePixel |= alpha >= 230;
                            if (alpha == 0)
                            {
                                clearPixelCount++;
                            }
                            if (alpha > 0 && alpha < 255)
                            {
                                softPixelCount++;
                            }
                            if (alpha > 0 && alpha < 64)
                            {
                                lowAlphaEdgePixelCount++;
                            }
                            else if (alpha >= 64 && alpha < 230)
                            {
                                midAlphaEdgePixelCount++;
                            }
                            if (x < TransparentPerimeterWidth ||
                                x >= decoded.width - TransparentPerimeterWidth ||
                                y < TransparentPerimeterWidth ||
                                y >= decoded.height - TransparentPerimeterWidth)
                            {
                                Assert.That(
                                    alpha,
                                    Is.Zero,
                                    derivative + " retained an encoded rectangular edge.");
                            }
                        }
                    }

                    Assert.That(hasClearPixel, Is.True, derivative);
                    Assert.That(hasSoftPixel, Is.True,
                        derivative + " needs antialiased/glow alpha transitions.");
                    Assert.That(hasLowAlphaEdgePixel, Is.True,
                        derivative + " needs a low-alpha silhouette fringe.");
                    Assert.That(hasMidAlphaEdgePixel, Is.True,
                        derivative + " needs a continuous mid-alpha edge transition.");
                    Assert.That(hasReadableCorePixel, Is.True,
                        derivative + " needs a fully readable foreground core.");
                    Assert.That(
                        clearPixelCount / (float)pixels.Length,
                        Is.GreaterThanOrEqualTo(MinimumTransparentCoverage),
                        derivative +
                        " retained an opaque source matte rather than transparency.");
                    Assert.That(
                        softPixelCount / (float)pixels.Length,
                        Is.GreaterThanOrEqualTo(MinimumSoftCoverage),
                        derivative +
                        " needs enough soft coverage for antialiased edges.");
                    Assert.That(
                        lowAlphaEdgePixelCount / (float)pixels.Length,
                        Is.GreaterThanOrEqualTo(MinimumLowAlphaCoverage),
                        derivative +
                        " has a clipped low-alpha silhouette edge.");
                    Assert.That(
                        midAlphaEdgePixelCount / (float)pixels.Length,
                        Is.GreaterThanOrEqualTo(MinimumMidAlphaCoverage),
                        derivative +
                        " has a binary/cut-out alpha transition.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }
            }
        }

        [Test]
        public void Catalog_UsesOnlyConfiguredTransparentDerivatives()
        {
            AbilityIconCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AbilityIconCatalog>(CatalogPath);
            Assert.That(
                catalog,
                Is.Not.Null,
                "Rebuild the playable foundation after extracting ability-icon alpha.");

            string[] derivatives = Directory.GetFiles(
                    IconFolder,
                    "*" + TransparentSuffix + ".png",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(derivatives, Has.Length.EqualTo(ExpectedIconCount));
            for (int index = 0; index < derivatives.Length; index++)
            {
                string derivative = derivatives[index].Replace('\\', '/');
                string stem = Path.GetFileNameWithoutExtension(derivative);
                string abilityName = stem.Substring(
                    0,
                    stem.Length - TransparentSuffix.Length);
                Assert.That(
                    Enum.TryParse(abilityName, out AbilityId abilityId),
                    Is.True,
                    derivative);

                TextureImporter importer =
                    AssetImporter.GetAtPath(derivative) as TextureImporter;
                Assert.That(importer, Is.Not.Null, derivative);
                Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True, derivative);
                Assert.That(
                    importer.alphaSource,
                    Is.EqualTo(TextureImporterAlphaSource.FromInput),
                    derivative);
                Assert.That(importer.alphaIsTransparency, Is.True, derivative);
                Assert.That(importer.sRGBTexture, Is.True, derivative);
                Assert.That(importer.isReadable, Is.False, derivative);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                TextureImporterSettings textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                Assert.That(
                    textureSettings.spriteMeshType,
                    Is.EqualTo(SpriteMeshType.FullRect));
                Assert.That(importer.mipmapEnabled, Is.True);
                Assert.That(
                    importer.mipmapFilter,
                    Is.EqualTo(TextureImporterMipFilter.KaiserFilter));
                Assert.That(importer.borderMipmap, Is.True);
                Assert.That(
                    importer.mipMapsPreserveCoverage,
                    Is.False,
                    "Alpha coverage scaling is for alpha-tested cutouts, not blended HUD art.");
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(importer.maxTextureSize, Is.EqualTo(256));
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed));

                Assert.That(catalog.TryGetIcon(abilityId, out Sprite sprite), Is.True);
                Assert.That(sprite, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(sprite),
                    Is.EqualTo(derivative),
                    abilityId + " must not reference its opaque source PNG.");
            }
        }

        [Test]
        public void Catalog_CoversEveryLevelingAbilityWithPaintedArt()
        {
            AbilityIconCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AbilityIconCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);

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
                for (int slot = 0;
                     slot < LevelingAbilityUnlockCatalog.TotalAbilityCount;
                     slot++)
                {
                    AbilityId abilityId =
                        LevelingAbilityUnlockCatalog.GetAbility(
                            specialization,
                            slot);
                    Assert.That(
                        catalog.TryGetIcon(abilityId, out Sprite icon) &&
                        icon != null,
                        Is.True,
                        specialization + " leveling slot " + slot +
                        " must use painted art for " + abilityId + ".");
                }
            }
        }

        [Test]
        public void Catalog_CoversEveryPlayerArenaBindingWithPaintedArt()
        {
            AbilityIconCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AbilityIconCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);

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
                SpecializationId specialization =
                    specializations[specializationIndex];
                AbilityKit kit = SpecializationCatalog.GetKit(specialization);
                for (int slot = 0; slot < AbilityKit.Capacity; slot++)
                {
                    AbilitySlotBinding binding = kit.GetSlotBinding(slot);
                    for (int directionIndex = 0;
                         directionIndex < directions.Length;
                         directionIndex++)
                    {
                        AbilitySlotDirection direction = directions[directionIndex];
                        AbilityId abilityId = binding.GetAbility(direction);
                        if (abilityId == AbilityId.None)
                        {
                            continue;
                        }

                        Assert.That(
                            catalog.TryGetIcon(abilityId, out Sprite icon) &&
                            icon != null,
                            Is.True,
                            specialization + " slot " + slot + " " + direction +
                            " must use painted art for " + abilityId + ".");
                    }
                }
            }
        }

        [Test]
        public void Catalog_CoversEveryHeroSiegeBindingWithPaintedArt()
        {
            AbilityIconCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AbilityIconCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);

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
            AbilityId[] expectedAreaAbilities =
            {
                AbilityId.DawnNova,
                AbilityId.UmbralCollapse,
                AbilityId.FlameSurge,
                AbilityId.Blizzard,
                AbilityId.BladeTempest,
                AbilityId.BladeTempest,
                AbilityId.ArrowRain,
            };
            for (int index = 0; index < specializations.Length; index++)
            {
                AbilityKit kit = ModeAbilityKitCatalog.GetKit(
                    PlayableModeId.HeroSiege,
                    specializations[index]);
                AbilityId ability = kit.GetAbility(
                    ModeAbilityKitCatalog.SiegeAreaSlot,
                    ModeAbilityKitCatalog.SiegeAreaDirection);
                Assert.That(ability, Is.EqualTo(expectedAreaAbilities[index]));
                Assert.That(
                    catalog.TryGetIcon(ability, out Sprite icon) && icon != null,
                    Is.True,
                    specializations[index] +
                    " Hero Siege AoE must use painted art.");
            }
        }
    }
}
