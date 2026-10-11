using System;
using NUnit.Framework;
using SessionRpg.Editor.Arenas;
using SessionRpg.Unity.Arenas;
using UnityEngine;

namespace SessionRpg.Tests.Performance
{
    public sealed class ArenaEnvironmentSetTests
    {
        private const int WarmupLookups = 256;
        private const int MeasuredLookups = 2048;

        private static readonly ArenaContentId ArenaId =
            new ArenaContentId("rift_crucible");
        private static readonly ArenaContentId BossId =
            new ArenaContentId("ember_vault");
        private static readonly ArenaContentId AlternateArenaId =
            new ArenaContentId("alternate_arena");

        [Test]
        public void Configuration_ExposesReferencesForValidation()
        {
            EnvironmentFixture fixture = EnvironmentFixture.Create();
            try
            {
                ArenaEnvironmentSet environments = fixture.Environments;

                Assert.That(environments.Catalog, Is.SameAs(fixture.Catalog));
                Assert.That(
                    environments.ArenaEnvironment,
                    Is.SameAs(fixture.ArenaEnvironment));
                Assert.That(
                    environments.BossEnvironment,
                    Is.SameAs(fixture.BossEnvironment));
                Assert.That(environments.HasCatalog, Is.True);
                Assert.That(environments.HasArenaEnvironment, Is.True);
                Assert.That(environments.HasBossEnvironment, Is.True);
                Assert.That(environments.IsConfigured, Is.True);
                Assert.That(environments.EmbeddedEnvironmentCount, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Lookup_MapsOnlyCurrentCatalogDefaultsToEmbeddedRoots()
        {
            EnvironmentFixture fixture = EnvironmentFixture.Create();
            try
            {
                ArenaEnvironmentSet environments = fixture.Environments;

                Assert.That(
                    environments.TryGetDefault(
                        ArenaContentMode.Arena,
                        out ArenaContentId arenaId,
                        out GameObject arenaEnvironment),
                    Is.True);
                Assert.That(arenaId, Is.EqualTo(ArenaId));
                Assert.That(arenaEnvironment, Is.SameAs(fixture.ArenaEnvironment));

                Assert.That(
                    environments.TryGetDefault(
                        ArenaContentMode.Boss,
                        out ArenaContentId bossId,
                        out GameObject bossEnvironment),
                    Is.True);
                Assert.That(bossId, Is.EqualTo(BossId));
                Assert.That(bossEnvironment, Is.SameAs(fixture.BossEnvironment));

                Assert.That(
                    environments.TryGetEmbedded(ArenaId, out arenaEnvironment),
                    Is.True);
                Assert.That(arenaEnvironment, Is.SameAs(fixture.ArenaEnvironment));
                Assert.That(
                    environments.TryGetEmbedded(BossId, out bossEnvironment),
                    Is.True);
                Assert.That(bossEnvironment, Is.SameAs(fixture.BossEnvironment));

                Assert.That(
                    environments.TryGetEmbedded(
                        AlternateArenaId,
                        out GameObject nonDefaultEnvironment),
                    Is.False);
                Assert.That(nonDefaultEnvironment, Is.Null);
                Assert.That(
                    environments.TryGetDefault(
                        ArenaContentMode.None,
                        out ArenaContentId missingId,
                        out GameObject missingEnvironment),
                    Is.False);
                Assert.That(missingId, Is.EqualTo(ArenaContentId.None));
                Assert.That(missingEnvironment, Is.Null);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Lookup_AfterWarmupAllocatesZeroManagedBytes()
        {
            EnvironmentFixture fixture = EnvironmentFixture.Create();
            try
            {
                ArenaEnvironmentSet environments = fixture.Environments;
                bool allFound = true;
                for (int index = 0; index < WarmupLookups; index++)
                {
                    allFound &= environments.TryGetEmbedded(
                        ArenaId,
                        out GameObject arenaEnvironment);
                    allFound &= arenaEnvironment == fixture.ArenaEnvironment;
                    allFound &= environments.TryGetDefault(
                        ArenaContentMode.Boss,
                        out ArenaContentId bossId,
                        out GameObject bossEnvironment);
                    allFound &= bossId == BossId;
                    allFound &= bossEnvironment == fixture.BossEnvironment;
                }

                ForceCollectionAndPrimeCounter();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < MeasuredLookups; index++)
                {
                    allFound &= environments.TryGetEmbedded(
                        ArenaId,
                        out GameObject arenaEnvironment);
                    allFound &= arenaEnvironment == fixture.ArenaEnvironment;
                    allFound &= environments.TryGetDefault(
                        ArenaContentMode.Boss,
                        out ArenaContentId bossId,
                        out GameObject bossEnvironment);
                    allFound &= bossId == BossId;
                    allFound &= bossEnvironment == fixture.BossEnvironment;
                }

                long allocatedBytes =
                    GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allFound, Is.True);
                Assert.That(
                    allocatedBytes,
                    Is.Zero,
                    "Embedded arena selection must remain allocation-free after warm-up.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void ForceCollectionAndPrimeCounter()
        {
            GC.GetAllocatedBytesForCurrentThread();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.GetAllocatedBytesForCurrentThread();
        }

        private sealed class EnvironmentFixture : IDisposable
        {
            private EnvironmentFixture()
            {
                Catalog = ScriptableObject.CreateInstance<ArenaCatalogAsset>();
                ArenaEnvironment = new GameObject("Embedded Arena");
                BossEnvironment = new GameObject("Embedded Boss");

                ArenaContentAuthoring.ReplaceCatalogEntries(
                    Catalog,
                    ArenaCatalogEntryAuthoring.ForScene(
                        ArenaId,
                        ArenaContentMode.Arena,
                        "Assets/SessionRpg/Scenes/RiftCrucible.unity",
                        true),
                    ArenaCatalogEntryAuthoring.ForScene(
                        BossId,
                        ArenaContentMode.Boss,
                        "Assets/SessionRpg/Scenes/EmberVault.unity",
                        true),
                    ArenaCatalogEntryAuthoring.ForScene(
                        AlternateArenaId,
                        ArenaContentMode.Arena,
                        "Assets/SessionRpg/Scenes/AlternateArena.unity"));

                Environments = new ArenaEnvironmentSet(
                    Catalog,
                    ArenaEnvironment,
                    BossEnvironment);
            }

            public ArenaCatalogAsset Catalog { get; }

            public GameObject ArenaEnvironment { get; }

            public GameObject BossEnvironment { get; }

            public ArenaEnvironmentSet Environments { get; }

            public static EnvironmentFixture Create()
            {
                return new EnvironmentFixture();
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(ArenaEnvironment);
                UnityEngine.Object.DestroyImmediate(BossEnvironment);
                UnityEngine.Object.DestroyImmediate(Catalog);
            }
        }
    }
}
