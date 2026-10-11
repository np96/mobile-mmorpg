using NUnit.Framework;
using SessionRpg.Editor.Arenas;
using SessionRpg.Unity.Arenas;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class ArenaContentRuntimeLoaderTests
    {
        private static readonly ArenaContentId ArenaId =
            new ArenaContentId("runtime_arena");
        private static readonly ArenaContentId BossId =
            new ArenaContentId("runtime_boss");
        private static readonly ArenaContentId SiegeId =
            new ArenaContentId("runtime_siege");
        private static readonly ArenaContentId AlternateId =
            new ArenaContentId("runtime_alternate");
        private static readonly ArenaContentId MismatchId =
            new ArenaContentId("runtime_mismatch");

        private ArenaCatalogAsset _catalog;
        private GameObject _loaderRoot;
        private Transform _instanceRoot;
        private ArenaContentRuntimeLoader _loader;
        private ArenaPresentation _embeddedArena;
        private ArenaPresentation _embeddedBoss;
        private ArenaPresentation _siegeSource;
        private ArenaPresentation _alternateSource;
        private ArenaPresentation _mismatchedSource;

        [SetUp]
        public void SetUp()
        {
            _embeddedArena = CreatePresentation(
                "EmbeddedArena",
                ArenaId,
                ArenaContentMode.Arena);
            _embeddedBoss = CreatePresentation(
                "EmbeddedBoss",
                BossId,
                ArenaContentMode.Boss);
            _siegeSource = CreatePresentation(
                "SiegeSource",
                SiegeId,
                ArenaContentMode.Siege);
            _alternateSource = CreatePresentation(
                "AlternateSource",
                AlternateId,
                ArenaContentMode.Arena);
            _mismatchedSource = CreatePresentation(
                "MismatchedSource",
                new ArenaContentId("runtime_actual"),
                ArenaContentMode.Arena);

            _catalog = ScriptableObject.CreateInstance<ArenaCatalogAsset>();
            ArenaContentAuthoring.ReplaceCatalogEntries(
                _catalog,
                ArenaCatalogEntryAuthoring.ForPrefab(
                    ArenaId,
                    ArenaContentMode.Arena,
                    _embeddedArena,
                    true),
                ArenaCatalogEntryAuthoring.ForPrefab(
                    BossId,
                    ArenaContentMode.Boss,
                    _embeddedBoss,
                    true),
                ArenaCatalogEntryAuthoring.ForPrefab(
                    SiegeId,
                    ArenaContentMode.Siege,
                    _siegeSource,
                    true),
                ArenaCatalogEntryAuthoring.ForPrefab(
                    AlternateId,
                    ArenaContentMode.Arena,
                    _alternateSource),
                ArenaCatalogEntryAuthoring.ForPrefab(
                    MismatchId,
                    ArenaContentMode.Arena,
                    _mismatchedSource));

            _loaderRoot = new GameObject(
                "ArenaLoader",
                typeof(ArenaContentRuntimeLoader));
            _instanceRoot = new GameObject("Instances").transform;
            _instanceRoot.SetParent(_loaderRoot.transform, false);
            _loader = _loaderRoot.GetComponent<ArenaContentRuntimeLoader>();
            _loader.ConfigureForAuthoring(
                _catalog,
                _embeddedArena,
                _embeddedBoss,
                _instanceRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (_loader != null)
            {
                _loader.Unload();
            }

            Destroy(_loaderRoot);
            Destroy(_embeddedArena != null
                ? _embeddedArena.gameObject
                : null);
            Destroy(_embeddedBoss != null
                ? _embeddedBoss.gameObject
                : null);
            Destroy(_siegeSource != null
                ? _siegeSource.gameObject
                : null);
            Destroy(_alternateSource != null
                ? _alternateSource.gameObject
                : null);
            Destroy(_mismatchedSource != null
                ? _mismatchedSource.gameObject
                : null);
            Destroy(_catalog);
        }

        [Test]
        public void CatalogDefault_UsesStableIdentityAndPreservesCurrentOnUnknown()
        {
            Assert.That(_loader.Catalog, Is.SameAs(_catalog));
            Assert.That(_loader.CurrentContentId, Is.EqualTo(ArenaContentId.None));
            Assert.That(_loader.CurrentPresentation, Is.Null);

            Assert.That(
                _loader.TryLoadDefault(ArenaContentMode.Arena, out string error),
                Is.True,
                error);
            Assert.That(error, Is.Empty);
            Assert.That(_loader.CurrentContentId, Is.EqualTo(ArenaId));
            Assert.That(_loader.CurrentPresentation, Is.SameAs(_embeddedArena));
            Assert.That(_embeddedArena.gameObject.activeSelf, Is.True);
            Assert.That(_embeddedBoss.gameObject.activeSelf, Is.False);

            Assert.That(
                _loader.TryLoad(
                    new ArenaContentId("uncataloged_arena"),
                    out error),
                Is.False);
            StringAssert.Contains("Unknown arena content ID", error);
            Assert.That(_loader.CurrentPresentation, Is.SameAs(_embeddedArena));
            Assert.That(_embeddedArena.gameObject.activeSelf, Is.True);

            Assert.That(
                _loader.TryLoadDefault(ArenaContentMode.None, out error),
                Is.False);
            StringAssert.Contains("No default presentation", error);
            Assert.That(_loader.CurrentPresentation, Is.SameAs(_embeddedArena));
        }

        [Test]
        public void PrefabLoad_IsIdempotentAndUnloadResetsAllResidency()
        {
            Assert.That(
                _loader.TryLoad(AlternateId, out string error),
                Is.True,
                error);
            ArenaPresentation loaded = _loader.CurrentPresentation;
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded, Is.Not.SameAs(_alternateSource));
            Assert.That(loaded.ContentId, Is.EqualTo(AlternateId));
            Assert.That(loaded.transform.parent, Is.SameAs(_instanceRoot));
            Assert.That(loaded.gameObject.name, Is.EqualTo("LoadedArena_" + AlternateId.Value));
            Assert.That(_instanceRoot.childCount, Is.EqualTo(1));
            Assert.That(_embeddedArena.gameObject.activeSelf, Is.False);

            Assert.That(_loader.TryLoad(AlternateId, out error), Is.True, error);
            Assert.That(_loader.CurrentPresentation, Is.SameAs(loaded));
            Assert.That(_instanceRoot.childCount, Is.EqualTo(1));

            Assert.That(
                _loader.TryLoadDefault(ArenaContentMode.Boss, out error),
                Is.True,
                error);
            Assert.That(_loader.CurrentPresentation, Is.SameAs(_embeddedBoss));
            Assert.That(_loader.CurrentContentId, Is.EqualTo(BossId));
            Assert.That(_embeddedBoss.gameObject.activeSelf, Is.True);
            Assert.That(_embeddedArena.gameObject.activeSelf, Is.False);
            Assert.That(_instanceRoot.childCount, Is.Zero);

            _loader.Unload();
            _loader.Unload();

            Assert.That(_loader.CurrentPresentation, Is.Null);
            Assert.That(_loader.CurrentContentId, Is.EqualTo(ArenaContentId.None));
            Assert.That(_embeddedArena.gameObject.activeSelf, Is.False);
            Assert.That(_embeddedBoss.gameObject.activeSelf, Is.False);
            Assert.That(_instanceRoot.childCount, Is.Zero);
        }

        [Test]
        public void SiegeDefault_LoadsCatalogPrefabWithoutAnEmbeddedBootstrapCopy()
        {
            Assert.That(
                _loader.TryLoadDefault(ArenaContentMode.Siege, out string error),
                Is.True,
                error);

            ArenaPresentation loaded = _loader.CurrentPresentation;
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded, Is.Not.SameAs(_siegeSource));
            Assert.That(loaded.ContentId, Is.EqualTo(SiegeId));
            Assert.That(loaded.Mode, Is.EqualTo(ArenaContentMode.Siege));
            Assert.That(loaded.transform.parent, Is.SameAs(_instanceRoot));
            Assert.That(_instanceRoot.childCount, Is.EqualTo(1));
            Assert.That(_embeddedArena.gameObject.activeSelf, Is.False);
            Assert.That(_embeddedBoss.gameObject.activeSelf, Is.False);

            _loader.Unload();

            Assert.That(_loader.CurrentPresentation, Is.Null);
            Assert.That(_instanceRoot.childCount, Is.Zero);
            Assert.That(_siegeSource.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void CatalogPrefabIdentityMismatch_FailsClosedAndResetsResidency()
        {
            Assert.That(
                _loader.TryLoadDefault(ArenaContentMode.Arena, out string error),
                Is.True,
                error);
            Assert.That(_embeddedArena.gameObject.activeSelf, Is.True);

            Assert.That(_loader.TryLoad(MismatchId, out error), Is.False);
            StringAssert.Contains("did not produce its declared presentation root", error);
            Assert.That(_loader.CurrentPresentation, Is.Null);
            Assert.That(_loader.CurrentContentId, Is.EqualTo(ArenaContentId.None));
            Assert.That(_embeddedArena.gameObject.activeSelf, Is.False);
            Assert.That(_embeddedBoss.gameObject.activeSelf, Is.False);
            Assert.That(_instanceRoot.childCount, Is.Zero);
            Assert.That(_mismatchedSource.gameObject.activeSelf, Is.False);
        }

        private static ArenaPresentation CreatePresentation(
            string name,
            ArenaContentId contentId,
            ArenaContentMode mode)
        {
            var root = new GameObject(name);
            ArenaPresentation presentation =
                ArenaContentAuthoring.ConfigurePresentation(
                    root,
                    contentId,
                    mode,
                    ArenaPresentationCapabilities.FlatCombatGround,
                    null,
                    null,
                    null,
                    0f,
                    1,
                    null);
            root.SetActive(false);
            return presentation;
        }

        private static void Destroy(Object value)
        {
            if (value != null)
            {
                Object.DestroyImmediate(value);
            }
        }
    }
}
