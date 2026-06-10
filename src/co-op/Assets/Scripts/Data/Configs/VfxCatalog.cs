using System;
using System.Collections.Generic;
using Data.Effects;
using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/VFX Catalog", fileName = "VfxCatalog")]
    public sealed class VfxCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public VfxId Id;
            public GameObject Prefab;
            [Tooltip("0 = auto (use the prefab's longest ParticleSystem duration).")]
            public float LifetimeOverride;
            public bool ParentToTarget;
        }

        public Entry[] Entries = Array.Empty<Entry>();

        private Dictionary<VfxId, Entry> _map;

        public Entry Get(VfxId id)
        {
            if (_map == null)
            {
                _map = new Dictionary<VfxId, Entry>();
                for (int i = 0; i < Entries.Length; i++)
                    if (Entries[i] != null) _map[Entries[i].Id] = Entries[i];
            }
            return _map.TryGetValue(id, out var e) && e.Prefab != null ? e : null;
        }

        private void OnEnable() => _map = null;
    }
}
