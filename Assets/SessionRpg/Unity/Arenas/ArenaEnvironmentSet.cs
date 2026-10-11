using System;
using UnityEngine;

namespace SessionRpg.Unity.Arenas
{
    /// <summary>
    /// Bridges catalog identities to the two environments currently embedded in
    /// the bootstrap scene. Future scene or prefab loading can replace this seam
    /// without changing callers that select arenas by stable content ID.
    /// </summary>
    [Serializable]
    public sealed class ArenaEnvironmentSet
    {
        [SerializeField] private ArenaCatalogAsset _catalog;
        [SerializeField] private GameObject _arenaEnvironment;
        [SerializeField] private GameObject _bossEnvironment;

        public ArenaEnvironmentSet()
        {
        }

        public ArenaEnvironmentSet(
            ArenaCatalogAsset catalog,
            GameObject arenaEnvironment,
            GameObject bossEnvironment)
        {
            _catalog = catalog;
            _arenaEnvironment = arenaEnvironment;
            _bossEnvironment = bossEnvironment;
        }

        public ArenaCatalogAsset Catalog => _catalog;

        public GameObject ArenaEnvironment => _arenaEnvironment;

        public GameObject BossEnvironment => _bossEnvironment;

        public bool HasCatalog => _catalog != null;

        public bool HasArenaEnvironment => _arenaEnvironment != null;

        public bool HasBossEnvironment => _bossEnvironment != null;

        public bool IsConfigured =>
            HasCatalog && HasArenaEnvironment && HasBossEnvironment;

        public int EmbeddedEnvironmentCount =>
            (HasArenaEnvironment ? 1 : 0) + (HasBossEnvironment ? 1 : 0);

        public bool TryGetEmbedded(
            ArenaContentId contentId,
            out GameObject environment)
        {
            environment = null;
            if (_catalog == null ||
                !_catalog.TryGet(contentId, out ArenaCatalogEntry entry) ||
                !TryGetDefault(
                    entry.Mode,
                    out ArenaContentId defaultContentId,
                    out GameObject defaultEnvironment) ||
                defaultContentId != contentId)
            {
                return false;
            }

            environment = defaultEnvironment;
            return true;
        }

        public bool TryGetDefault(
            ArenaContentMode mode,
            out ArenaContentId contentId,
            out GameObject environment)
        {
            contentId = ArenaContentId.None;
            environment = null;
            if (_catalog == null ||
                !_catalog.TryGetDefault(mode, out ArenaCatalogEntry entry) ||
                !TryGetEnvironment(mode, out GameObject embeddedEnvironment))
            {
                return false;
            }

            contentId = entry.ContentId;
            environment = embeddedEnvironment;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            ArenaCatalogAsset catalog,
            GameObject arenaEnvironment,
            GameObject bossEnvironment)
        {
            _catalog = catalog;
            _arenaEnvironment = arenaEnvironment;
            _bossEnvironment = bossEnvironment;
        }
#endif

        private bool TryGetEnvironment(
            ArenaContentMode mode,
            out GameObject environment)
        {
            switch (mode)
            {
                case ArenaContentMode.Arena:
                    environment = _arenaEnvironment;
                    break;
                case ArenaContentMode.Boss:
                    environment = _bossEnvironment;
                    break;
                default:
                    environment = null;
                    return false;
            }

            return environment != null;
        }
    }
}
