using System;
using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    [Serializable]
    public struct AbilityIconEntry
    {
        [SerializeField] private AbilityId _abilityId;
        [SerializeField] private Sprite _icon;

        public AbilityId AbilityId => _abilityId;

        public Sprite Icon => _icon;
    }

    [CreateAssetMenu(
        fileName = "AbilityIconCatalog",
        menuName = "Session RPG/Ability Icon Catalog")]
    public sealed class AbilityIconCatalog : ScriptableObject
    {
        private const int LookupCapacity = AbilityCatalog.StorageCapacity;

        [SerializeField] private AbilityIconEntry[] _entries = Array.Empty<AbilityIconEntry>();

        private Sprite[] _lookup;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryGetIcon(AbilityId abilityId, out Sprite icon)
        {
            EnsureLookup();
            int index = (int)abilityId;
            if (index > 0 && index < _lookup.Length)
            {
                icon = _lookup[index];
                return icon != null;
            }

            icon = null;
            return false;
        }

        private void EnsureLookup()
        {
            if (_lookup == null || _lookup.Length != LookupCapacity)
            {
                RebuildLookup();
            }
        }

        private void RebuildLookup()
        {
            _lookup = new Sprite[LookupCapacity];
            int entryCount = _entries != null ? _entries.Length : 0;
            for (int index = 0; index < entryCount; index++)
            {
                AbilityIconEntry entry = _entries[index];
                int abilityIndex = (int)entry.AbilityId;
                if (abilityIndex > 0 && abilityIndex < _lookup.Length)
                {
                    _lookup[abilityIndex] = entry.Icon;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildLookup();
        }
#endif
    }
}
