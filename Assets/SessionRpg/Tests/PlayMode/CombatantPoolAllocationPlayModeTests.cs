using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class CombatantPoolAllocationPlayModeTests
    {
        private CombatantView _view;

        [UnitySetUp]
        public IEnumerator LoadAuthoredPool()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
            CombatantView[] views = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Include);
            Assert.That(views, Is.Not.Empty);
            _view = views[0];
            _view.ReleaseToPool();
        }

        [Test]
        public void SameClassRespawn_AfterWarmupAllocatesNoManagedBytes()
        {
            for (int index = 0; index < 8; index++)
            {
                CycleArchetype(LevelingMobArchetypeId.AshMelee, index);
            }

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 64; index++)
            {
                CycleArchetype(LevelingMobArchetypeId.AshMelee, index);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero,
                "A warmed pooled respawn must not recreate model renderer arrays.");
        }

        [Test]
        public void AlternatingAllMobArchetypes_AfterWarmupAllocatesNoManagedBytes()
        {
            for (int index = 0; index < 32; index++)
            {
                CycleArchetype((LevelingMobArchetypeId)(index % 8 + 1), index);
            }

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 64; index++)
            {
                CycleArchetype((LevelingMobArchetypeId)(index % 8 + 1), index);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero,
                "Changing enemy classes must select prewarmed model caches.");
        }

        [Test]
        public void RendererDefaults_SurviveClassChangesAndPoolReturn()
        {
            GameObject root = new GameObject("ColdRendererCacheProbe");
            root.SetActive(false);
            try
            {
                CombatantView view = root.AddComponent<CombatantView>();
                GameObject mageModel = new GameObject("MageModel");
                mageModel.transform.SetParent(root.transform, false);
                MeshRenderer hiddenRenderer = mageModel.AddComponent<MeshRenderer>();
                hiddenRenderer.enabled = false;
                GameObject rogueModel = new GameObject("RogueModel");
                rogueModel.transform.SetParent(root.transform, false);
                MeshRenderer visibleRenderer = rogueModel.AddComponent<MeshRenderer>();
                visibleRenderer.enabled = true;
                SetField(view, "_mageModel", mageModel);
                SetField(view, "_rogueModel", rogueModel);

                view.Initialize(new ParticipantId(1), new TeamId(2), ClassId.Mage,
                    false, Vector3.zero);
                Assert.That(hiddenRenderer.enabled, Is.False);
                hiddenRenderer.enabled = true;
                view.ReleaseToPool();
                view.Initialize(new ParticipantId(2), new TeamId(2), ClassId.Rogue,
                    false, Vector3.zero);
                Assert.That(visibleRenderer.enabled, Is.True);
                view.ReleaseToPool();
                view.Initialize(new ParticipantId(3), new TeamId(2), ClassId.Mage,
                    false, Vector3.zero);
                Assert.That(hiddenRenderer.enabled, Is.False,
                    "Pooling must restore authored disabled renderers, not enable everything.");
                view.ReleaseToPool();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private void CycleArchetype(LevelingMobArchetypeId archetype, int sequence)
        {
            LevelingMobArchetypeDefinition definition =
                LevelingMobArchetypeCatalog.Get(archetype);
            _view.Initialize(new ParticipantId(sequence + 100), new TeamId(2),
                definition.Class, false, Vector3.zero);
            _view.ConfigureLevelingArchetype(archetype);
            _view.ReleaseToPool();
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
