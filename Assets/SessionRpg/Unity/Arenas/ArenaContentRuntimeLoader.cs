using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Unity.Arenas
{
    /// <summary>
    /// Runtime catalog boundary for built-in arena-shaped content. Stable IDs,
    /// not Bootstrap branches or object names, choose an explicitly authored
    /// prefab/scene. Only one presentation is resident at a time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaContentRuntimeLoader : MonoBehaviour
    {
        [SerializeField] private ArenaCatalogAsset _catalog;
        [SerializeField] private ArenaPresentation _embeddedArena;
        [SerializeField] private ArenaPresentation _embeddedBoss;
        [SerializeField] private Transform _instanceRoot;

        private ArenaPresentation _current;
        private GameObject _instantiatedRoot;
        private Scene _loadedScene;

        public ArenaCatalogAsset Catalog => _catalog;

        public ArenaContentId CurrentContentId => _current != null
            ? _current.ContentId
            : ArenaContentId.None;

        public ArenaPresentation CurrentPresentation => _current;

        public bool TryLoadDefault(ArenaContentMode mode, out string error)
        {
            if (_catalog == null || !_catalog.TryGetDefault(mode, out ArenaCatalogEntry entry))
            {
                error = "No default presentation is cataloged for " + mode + ".";
                return false;
            }

            return TryLoad(entry.ContentId, out error);
        }

        public bool TryLoad(ArenaContentId contentId, out string error)
        {
            if (!contentId.IsValid || _catalog == null ||
                !_catalog.TryGet(contentId, out ArenaCatalogEntry entry))
            {
                error = "Unknown arena content ID '" + contentId.Value + "'.";
                return false;
            }

            if (_current != null && _current.ContentId == contentId)
            {
                error = string.Empty;
                return true;
            }

            Unload();
            ArenaPresentation embedded = ResolveEmbedded(entry.Mode);
            if (embedded != null && embedded.ContentId == contentId)
            {
                embedded.gameObject.SetActive(true);
                _current = embedded;
                error = string.Empty;
                return true;
            }

            if (entry.HasPrefab)
            {
                Transform parent = _instanceRoot != null ? _instanceRoot : transform;
                _instantiatedRoot = Instantiate(
                    entry.PresentationPrefab.gameObject,
                    parent,
                    false);
                _instantiatedRoot.name = "LoadedArena_" + contentId.Value;
                _current = _instantiatedRoot.GetComponent<ArenaPresentation>();
            }
            else if (entry.HasScene)
            {
                SceneManager.LoadScene(entry.PresentationScenePath, LoadSceneMode.Additive);
                _loadedScene = SceneManager.GetSceneByPath(entry.PresentationScenePath);
                _current = FindPresentation(_loadedScene, contentId);
            }

            if (_current == null || _current.ContentId != contentId)
            {
                Unload();
                error = "Arena content '" + contentId.Value +
                    "' did not produce its declared presentation root.";
                return false;
            }

            _current.gameObject.SetActive(true);
            error = string.Empty;
            return true;
        }

        public void Unload()
        {
            SetEmbeddedActive(_embeddedArena, false);
            SetEmbeddedActive(_embeddedBoss, false);
            _current = null;

            if (_instantiatedRoot != null)
            {
                _instantiatedRoot.SetActive(false);
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(_instantiatedRoot);
                }
                else
                {
                    DestroyImmediate(_instantiatedRoot);
                }

                _instantiatedRoot = null;
            }

            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(_loadedScene);
            }

            _loadedScene = default;
        }

        private void OnDestroy()
        {
            Unload();
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            ArenaCatalogAsset catalog,
            ArenaPresentation embeddedArena,
            ArenaPresentation embeddedBoss,
            Transform instanceRoot)
        {
            _catalog = catalog;
            _embeddedArena = embeddedArena;
            _embeddedBoss = embeddedBoss;
            _instanceRoot = instanceRoot;
        }
#endif

        private ArenaPresentation ResolveEmbedded(ArenaContentMode mode)
        {
            switch (mode)
            {
                case ArenaContentMode.Arena:
                    return _embeddedArena;
                case ArenaContentMode.Boss:
                    return _embeddedBoss;
                default:
                    return null;
            }
        }

        private static void SetEmbeddedActive(ArenaPresentation value, bool active)
        {
            if (value != null && value.gameObject.activeSelf != active)
            {
                value.gameObject.SetActive(active);
            }
        }

        private static ArenaPresentation FindPresentation(
            Scene scene,
            ArenaContentId contentId)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                ArenaPresentation[] presentations = roots[rootIndex]
                    .GetComponentsInChildren<ArenaPresentation>(true);
                for (int index = 0; index < presentations.Length; index++)
                {
                    if (presentations[index].ContentId == contentId)
                    {
                        return presentations[index];
                    }
                }
            }

            return null;
        }
    }
}
