using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class HudMedallionFrameAlphaContractTests
    {
        private const string SpritePath =
            "Assets/SessionRpg/Art/UI/Hud/AbilityMedallionFrame.png";

        [Test]
        public void SharedMedallionFrame_HasTransparentCenterGutterAndStableImporter()
        {
            string path = SpritePath;
            Assert.That(File.Exists(path), Is.True, path);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(
                    ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false),
                    Is.True);
                Assert.That(texture.width, Is.EqualTo(texture.height));
                Assert.That(texture.width, Is.GreaterThanOrEqualTo(512));

                Color32[] pixels = texture.GetPixels32();
                int clear = 0;
                int soft = 0;
                int opaque = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    byte alpha = pixels[index].a;
                    if (alpha == 0)
                    {
                        clear++;
                    }
                    else if (alpha == 255)
                    {
                        opaque++;
                    }
                    else
                    {
                        soft++;
                    }
                }

                Assert.That(clear, Is.GreaterThan(pixels.Length * 0.65f));
                Assert.That(soft, Is.GreaterThan(pixels.Length * 0.002f));
                Assert.That(opaque, Is.GreaterThan(pixels.Length * 0.12f));

                int center = texture.width / 2;
                int clearRadius = Mathf.FloorToInt(texture.width * 0.3f);
                for (int y = center - clearRadius; y <= center + clearRadius; y++)
                {
                    for (int x = center - clearRadius; x <= center + clearRadius; x++)
                    {
                        int offsetX = x - center;
                        int offsetY = y - center;
                        if ((offsetX * offsetX) + (offsetY * offsetY) <=
                            clearRadius * clearRadius)
                        {
                            Assert.That(
                                pixels[(y * texture.width) + x].a,
                                Is.Zero,
                                "The icon opening must remain transparent.");
                        }
                    }
                }

                const int gutter = 4;
                for (int y = 0; y < texture.height; y++)
                {
                    for (int x = 0; x < texture.width; x++)
                    {
                        if (Mathf.Min(
                                Mathf.Min(x, y),
                                Mathf.Min(
                                    texture.width - 1 - x,
                                    texture.height - 1 - y)) < gutter)
                        {
                            Assert.That(pixels[(y * texture.width) + x].a, Is.Zero);
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(
                importer.alphaSource,
                Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(
                importer.mipmapFilter,
                Is.EqualTo(TextureImporterMipFilter.KaiserFilter));
            Assert.That(importer.borderMipmap, Is.True);
            Assert.That(importer.mipMapsPreserveCoverage, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
            Assert.That(importer.maxTextureSize, Is.EqualTo(512));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.isReadable, Is.False);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.width, Is.EqualTo(sprite.rect.height));
            Assert.That(sprite.rect.width, Is.EqualTo(sprite.texture.width));
            Assert.That(sprite.rect.height, Is.EqualTo(sprite.texture.height));
        }
    }
}
