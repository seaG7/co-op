using System.Collections.Generic;
using Data.Configs;
using Data.Effects;
using Infrastructure.Providers.Configs;
using UnityEngine;

namespace Infrastructure.Services.Effects
{
    public sealed class SfxService : ISfxService
    {
        private sealed class Handle : ISfxHandle
        {
            public AudioSource Source;
            public Transform Root;
            public void Stop()
            {
                if (Source == null) return;
                Source.Stop();
                Source.clip = null;
                Source.loop = false;
                if (Root != null) Source.transform.SetParent(Root);
                Source.gameObject.SetActive(false);
            }
        }

        private const int PoolSize = 16;
        private readonly IConfigDataProvider _configs;
        private readonly Dictionary<SfxId, float> _lastPlay = new();
        private Transform _root;
        private AudioSource[] _pool;
        private AudioSource _2d;

        public SfxService(IConfigDataProvider configs) { _configs = configs; }

        public static bool PassesThrottle(Dictionary<SfxId, float> last, SfxId id, float minInterval, float now)
        {
            if (minInterval <= 0f) return true;
            if (last.TryGetValue(id, out var t) && now - t < minInterval) return false;
            last[id] = now;
            return true;
        }

        public void Play(SfxId id, Vector3 position)
        {
            var e = _configs?.Sfx?.Get(id);
            if (e == null) return;
            if (!PassesThrottle(_lastPlay, id, e.MinIntervalSec, Time.unscaledTime)) return;
            EnsurePool();
            var src = FreeSource();
            if (src == null) return;
            Configure(src, e, position, false);
            src.gameObject.SetActive(true);
            src.Play();
        }

        public void Play2D(SfxId id)
        {
            var e = _configs?.Sfx?.Get(id);
            if (e == null) return;
            if (!PassesThrottle(_lastPlay, id, e.MinIntervalSec, Time.unscaledTime)) return;
            EnsurePool();
            _2d.gameObject.SetActive(true);
            _2d.spatialBlend = 0f;
            _2d.pitch = Random.Range(e.PitchRange.x, e.PitchRange.y);
            _2d.PlayOneShot(SfxCatalog.PickClip(e, Random.Range(0, 10000)), e.Volume);
        }

        public ISfxHandle PlayLoop(SfxId id, Transform follow)
        {
            var e = _configs?.Sfx?.Get(id);
            if (e == null) return new Handle();
            EnsurePool();
            var src = FreeSource();
            if (src == null) return new Handle();
            if (follow != null) src.transform.SetParent(follow, false);
            Configure(src, e, follow != null ? follow.position : Vector3.zero, true);
            src.gameObject.SetActive(true);
            src.Play();
            return new Handle { Source = src, Root = _root };
        }

        private void EnsurePool()
        {
            if (_pool != null) return;
            _root = new GameObject("[SFX Pool]").transform;
            Object.DontDestroyOnLoad(_root.gameObject);
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++) _pool[i] = MakeSource($"sfx_{i}");
            _2d = MakeSource("sfx_2d");
            _2d.gameObject.SetActive(true);
            _2d.spatialBlend = 0f;
        }

        private AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root);
            go.SetActive(false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            return src;
        }

        private void Configure(AudioSource src, SfxCatalog.Entry e, Vector3 pos, bool loop)
        {
            src.transform.position = pos;
            src.clip = SfxCatalog.PickClip(e, Random.Range(0, 10000));
            src.volume = e.Volume;
            src.pitch = Random.Range(e.PitchRange.x, e.PitchRange.y);
            src.spatialBlend = e.SpatialBlend;
            src.loop = loop;
        }

        private AudioSource FreeSource()
        {
            for (int i = 0; i < _pool.Length; i++)
                if (!_pool[i].isPlaying && !_pool[i].gameObject.activeSelf) return _pool[i];
            for (int i = 0; i < _pool.Length; i++)
                if (!_pool[i].isPlaying) { _pool[i].transform.SetParent(_root); return _pool[i]; }
            return null;
        }
    }
}
