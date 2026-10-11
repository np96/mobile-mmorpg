using System;
using UnityEngine;

namespace SessionRpg.Unity.Arenas
{
    [Serializable]
    public sealed class ArenaCatalogEntry
    {
        [SerializeField] private ArenaContentId _contentId;
        [SerializeField] private ArenaContentMode _mode;
        [SerializeField] private bool _isDefaultForMode;
        [SerializeField] private ArenaPresentation _presentationPrefab;
        [SerializeField] private string _presentationScenePath = string.Empty;

        public ArenaContentId ContentId => _contentId;

        public ArenaContentMode Mode => _mode;

        public bool IsDefaultForMode => _isDefaultForMode;

        public ArenaPresentation PresentationPrefab => _presentationPrefab;

        public string PresentationScenePath => _presentationScenePath ?? string.Empty;

        public bool HasPrefab => _presentationPrefab != null;

        public bool HasScene => !string.IsNullOrWhiteSpace(PresentationScenePath);

    }

    [CreateAssetMenu(
        fileName = "ArenaCatalog",
        menuName = "Session RPG/Arenas/Arena Catalog")]
    public sealed class ArenaCatalogAsset : ScriptableObject
    {
        private static readonly ArenaCatalogEntry[] EmptyEntries =
            Array.Empty<ArenaCatalogEntry>();

        [SerializeField] private ArenaCatalogEntry[] _entries = EmptyEntries;

        public int Count => _entries != null ? _entries.Length : 0;

        public ArenaCatalogEntry GetEntry(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _entries[index];
        }

        public bool TryGet(ArenaContentId contentId, out ArenaCatalogEntry entry)
        {
            int count = Count;
            for (int index = 0; index < count; index++)
            {
                ArenaCatalogEntry candidate = _entries[index];
                if (candidate != null && candidate.ContentId == contentId)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public bool TryGetDefault(
            ArenaContentMode mode,
            out ArenaCatalogEntry entry)
        {
            int count = Count;
            for (int index = 0; index < count; index++)
            {
                ArenaCatalogEntry candidate = _entries[index];
                if (candidate != null &&
                    candidate.Mode == mode &&
                    candidate.IsDefaultForMode)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _entries ??= EmptyEntries;
        }
#endif
    }
}
