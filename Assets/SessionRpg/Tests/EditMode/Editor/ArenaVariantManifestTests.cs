using System;
using System.IO;
using NUnit.Framework;
using SessionRpg.Editor.Arenas;
using SessionRpg.Unity.Arenas;

namespace SessionRpg.Editor.Tests
{
    public sealed class ArenaVariantManifestTests
    {
        [Test]
        public void Parse_ValidManifestSortsByModeThenStableContentId()
        {
            ArenaVariantManifest manifest = ArenaVariantManifest.Parse(
                Manifest(
                    Entry(
                        "second_arena",
                        "Arena",
                        "Assets/SessionRpg/Content/Arenas/Second/Second.prefab",
                        false),
                    Entry(
                        "ember_vault",
                        "Boss",
                        "Assets/SessionRpg/Content/Arenas/EmberVault/EmberVault.prefab",
                        true),
                    Entry(
                        "first_arena",
                        "Arena",
                        "Assets/SessionRpg/Content/Arenas/First/First.prefab",
                        true),
                    Entry(
                        "ashen_stronghold",
                        "Siege",
                        "Assets/SessionRpg/Content/Arenas/AshenStronghold/AshenStronghold.prefab",
                        true)),
                "valid-manifest");

            Assert.That(manifest.SchemaVersion, Is.EqualTo(1));
            Assert.That(manifest.Count, Is.EqualTo(4));
            Assert.That(manifest.GetEntry(0).ContentId.Value, Is.EqualTo("first_arena"));
            Assert.That(manifest.GetEntry(1).ContentId.Value, Is.EqualTo("second_arena"));
            Assert.That(manifest.GetEntry(2).ContentId.Value, Is.EqualTo("ember_vault"));
            Assert.That(manifest.GetEntry(3).ContentId.Value, Is.EqualTo("ashen_stronghold"));
            Assert.That(
                manifest.TryGetDefault(
                    ArenaContentMode.Arena,
                    out ArenaVariantDefinition arenaDefault),
                Is.True);
            Assert.That(arenaDefault.ContentId.Value, Is.EqualTo("first_arena"));
            Assert.That(
                manifest.TryGetDefault(
                    ArenaContentMode.Siege,
                    out ArenaVariantDefinition siegeDefault),
                Is.True);
            Assert.That(siegeDefault.ContentId.Value, Is.EqualTo("ashen_stronghold"));
            Assert.That((int)ArenaContentMode.Arena, Is.EqualTo(1));
            Assert.That((int)ArenaContentMode.Boss, Is.EqualTo(2));
            Assert.That((int)ArenaContentMode.Siege, Is.EqualTo(3));
        }

        [Test]
        public void DefaultManifest_CatalogsAshenStrongholdAsSiegeDefault()
        {
            ArenaVariantManifest manifest = ArenaVariantManifest.LoadDefault();

            Assert.That(
                manifest.TryGetDefault(
                    ArenaContentMode.Siege,
                    out ArenaVariantDefinition siege),
                Is.True);
            Assert.That(siege.ContentId.Value, Is.EqualTo("ashen_stronghold"));
            Assert.That(siege.GameplayLayoutId, Is.EqualTo("siege_default_v1"));
            Assert.That(siege.GeneratorRecipe, Is.EqualTo("ashen_stronghold_v1"));
            Assert.That(
                siege.OutputPrefabPath,
                Is.EqualTo(
                    "Assets/SessionRpg/Content/Arenas/AshenStronghold/Prefabs/" +
                    "AshenStronghold.prefab"));
            Assert.That(siege.PresentationVersion, Is.EqualTo(1));
        }

        [Test]
        public void Parse_DoesNotOwnKnownLayoutOrRecipeRegistry()
        {
            string entry = Entry(
                "future_arena",
                "Arena",
                "Assets/SessionRpg/Content/Arenas/Future/Future.prefab",
                true,
                "future_layout_v9",
                "future_recipe_v4");

            ArenaVariantManifest manifest =
                ArenaVariantManifest.Parse(Manifest(entry), "future-contract");

            Assert.That(
                manifest.GetEntry(0).GameplayLayoutId,
                Is.EqualTo("future_layout_v9"));
            Assert.That(
                manifest.GetEntry(0).GeneratorRecipe,
                Is.EqualTo("future_recipe_v4"));
        }

        [Test]
        public void Parse_RejectsUnsupportedSchemaWithActionableSource()
        {
            string json = Manifest(Entry(
                "only_arena",
                "Arena",
                "Assets/SessionRpg/Content/Arenas/Only/Only.prefab",
                true)).Replace("\"schemaVersion\":1", "\"schemaVersion\":2");

            ArenaVariantManifestException exception = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(json, "schema-test"));

            StringAssert.Contains("schema-test", exception.Message);
            StringAssert.Contains("schemaVersion must be 1", exception.Message);
        }

        [Test]
        public void Parse_RejectsDuplicateContentId()
        {
            string json = Manifest(
                Entry(
                    "same_arena",
                    "Arena",
                    "Assets/SessionRpg/Content/Arenas/One/One.prefab",
                    true),
                Entry(
                    "same_arena",
                    "Arena",
                    "Assets/SessionRpg/Content/Arenas/Two/Two.prefab",
                    false));

            ArenaVariantManifestException exception = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(json, "duplicate-id"));

            StringAssert.Contains("entries[1].contentId 'same_arena' is duplicated", exception.Message);
        }

        [Test]
        public void Parse_RejectsCaseOnlyOutputCollision()
        {
            string json = Manifest(
                Entry(
                    "first_arena",
                    "Arena",
                    "Assets/SessionRpg/Content/Arenas/Shared/Arena.prefab",
                    true),
                Entry(
                    "second_arena",
                    "Arena",
                    "Assets/SessionRpg/Content/Arenas/Shared/arena.prefab",
                    false));

            ArenaVariantManifestException exception = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(json, "duplicate-output"));

            StringAssert.Contains("outputPrefabPath", exception.Message);
            StringAssert.Contains("already used by another entry", exception.Message);
        }

        [TestCase("Arena Name", "Arena")]
        [TestCase("arena_name", "arena")]
        [TestCase("arena_name", "Raid")]
        public void Parse_RejectsInvalidIdentityOrMode(string contentId, string mode)
        {
            string json = Manifest(Entry(
                contentId,
                mode,
                "Assets/SessionRpg/Content/Arenas/Only/Only.prefab",
                true));

            ArenaVariantManifestException exception = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(json, "identity-test"));

            StringAssert.Contains("entries[0]", exception.Message);
            StringAssert.Contains("identity-test", exception.Message);
        }

        [TestCase("../Outside.prefab")]
        [TestCase("Assets/SessionRpg/Content/Arenas/../Outside.prefab")]
        [TestCase("Assets\\SessionRpg\\Content\\Arenas\\Arena.prefab")]
        [TestCase("Assets/SessionRpg/Content/Arenas/Arena.asset")]
        public void Parse_RejectsInvalidOutputPrefabPath(string outputPath)
        {
            string json = Manifest(Entry(
                "only_arena",
                "Arena",
                outputPath,
                true));

            ArenaVariantManifestException exception = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(json, "invalid-output"));

            StringAssert.Contains("outputPrefabPath", exception.Message);
        }

        [Test]
        public void Parse_RequiresExactlyOneDefaultForEachRepresentedMode()
        {
            string noDefault = Manifest(Entry(
                "only_arena",
                "Arena",
                "Assets/SessionRpg/Content/Arenas/Only/Only.prefab",
                false));
            string twoDefaults = Manifest(
                Entry(
                    "first_arena",
                    "Arena",
                    "Assets/SessionRpg/Content/Arenas/First/First.prefab",
                    true),
                Entry(
                    "second_arena",
                    "Arena",
                    "Assets/SessionRpg/Content/Arenas/Second/Second.prefab",
                    true));

            ArenaVariantManifestException missing = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(noDefault, "missing-default"));
            ArenaVariantManifestException duplicate = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(twoDefaults, "duplicate-default"));

            StringAssert.Contains("exactly one defaultForMode", missing.Message);
            StringAssert.Contains("but has 0", missing.Message);
            StringAssert.Contains("but has 2", duplicate.Message);
        }

        [Test]
        public void Parse_RejectsMissingOrUnknownSchemaProperties()
        {
            string validEntry = Entry(
                "only_arena",
                "Arena",
                "Assets/SessionRpg/Content/Arenas/Only/Only.prefab",
                true);
            string missingBrief = Manifest(
                validEntry.Replace(",\"brief\":\"Authoring test variant.\"", string.Empty));
            string unknown = Manifest(
                validEntry.Replace(
                    "\"brief\":",
                    "\"unexpected\":true,\"brief\":"));

            ArenaVariantManifestException missing = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(missingBrief, "missing-property"));
            ArenaVariantManifestException extra = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(unknown, "unknown-property"));

            StringAssert.Contains("missing required property 'brief'", missing.Message);
            StringAssert.Contains("unknown property 'unexpected'", extra.Message);
        }

        [Test]
        public void Parse_RejectsPresentationVersionBelowOne()
        {
            string json = Manifest(Entry(
                "only_arena",
                "Arena",
                "Assets/SessionRpg/Content/Arenas/Only/Only.prefab",
                true).Replace("\"presentationVersion\":1", "\"presentationVersion\":0"));

            ArenaVariantManifestException exception = Assert.Throws<
                ArenaVariantManifestException>(
                () => ArenaVariantManifest.Parse(json, "version-test"));

            StringAssert.Contains("presentationVersion must be at least 1", exception.Message);
        }

        [Test]
        public void DefaultPath_ResolvesFromProjectRootAndRejectsTraversal()
        {
            string resolved = ArenaVariantManifest.ResolveProjectRelativePath(
                ArenaVariantManifest.DefaultProjectRelativePath);

            StringAssert.EndsWith(
                Path.Combine("Tools", "Agent", "arena-variants.json"),
                resolved);
            Assert.Throws<ArenaVariantManifestException>(
                () => ArenaVariantManifest.ResolveProjectRelativePath(
                    "../arena-variants.json"));
        }

        private static string Manifest(params string[] entries)
        {
            return "{\"schemaVersion\":1,\"entries\":[" +
                string.Join(",", entries) + "]}";
        }

        private static string Entry(
            string contentId,
            string mode,
            string outputPrefabPath,
            bool isDefault,
            string gameplayLayoutId = "layout_v1",
            string generatorRecipe = "recipe_v1")
        {
            return "{" +
                "\"contentId\":\"" + Escape(contentId) + "\"," +
                "\"mode\":\"" + Escape(mode) + "\"," +
                "\"gameplayLayoutId\":\"" + Escape(gameplayLayoutId) + "\"," +
                "\"generatorRecipe\":\"" + Escape(generatorRecipe) + "\"," +
                "\"outputPrefabPath\":\"" + Escape(outputPrefabPath) + "\"," +
                "\"presentationVersion\":1," +
                "\"defaultForMode\":" + (isDefault ? "true" : "false") + "," +
                "\"artStatus\":\"prototype\"," +
                "\"brief\":\"Authoring test variant.\"" +
                "}";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
