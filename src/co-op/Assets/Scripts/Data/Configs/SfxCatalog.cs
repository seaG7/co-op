using System;
using System.Collections.Generic;
using Data.Effects;
using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/SFX Catalog", fileName = "SfxCatalog")]
    public sealed class SfxCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SfxId Id;
            public AudioClip[] Clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float Volume = 1f;
            public Vector2 PitchRange = Vector2.one;
            [Range(0f, 1f)] public float SpatialBlend = 1f;
            public bool Loop;
            [Tooltip("Minimum seconds between plays of this id (0 = no throttle).")]
            public float MinIntervalSec;

            public bool HasClip => Clips != null && Clips.Length > 0;
        }

        public Entry[] Entries = Array.Empty<Entry>();

        private Dictionary<SfxId, Entry> _map;

        public Entry Get(SfxId id)
        {
            if (_map == null)
            {
                _map = new Dictionary<SfxId, Entry>();
                for (int i = 0; i < Entries.Length; i++)
                    if (Entries[i] != null) _map[Entries[i].Id] = Entries[i];
            }
            return _map.TryGetValue(id, out var e) && e.HasClip ? e : null;
        }

        public static AudioClip PickClip(Entry e, int roll)
        {
            if (e == null || !e.HasClip) return null;
            return e.Clips[((roll % e.Clips.Length) + e.Clips.Length) % e.Clips.Length];
        }

        private void OnEnable() => _map = null;
    }
}
