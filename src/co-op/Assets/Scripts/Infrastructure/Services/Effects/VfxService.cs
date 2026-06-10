using Data.Effects;
using Infrastructure.Providers.Configs;
using UnityEngine;

namespace Infrastructure.Services.Effects
{
    public sealed class VfxService : IVfxService
    {
        private sealed class Loop : IVfxHandle
        {
            public GameObject Go;
            public void Stop() { if (Go != null) Object.Destroy(Go); }
        }

        private readonly IConfigDataProvider _configs;
        private Transform _root;

        public VfxService(IConfigDataProvider configs) { _configs = configs; }

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("[VFX Pool]").transform;
            Object.DontDestroyOnLoad(_root.gameObject);
        }

        public void Play(VfxId id, Vector3 position, Quaternion rotation = default, Transform parent = null)
        {
            var e = _configs?.Vfx?.Get(id);
            if (e == null) return;
            EnsureRoot();
            var rot = rotation == default ? Quaternion.identity : rotation;
            var go = Object.Instantiate(e.Prefab, position, rot, parent != null && e.ParentToTarget ? parent : _root);
            float life = e.LifetimeOverride > 0f ? e.LifetimeOverride : LongestDuration(go);
            Object.Destroy(go, life);
        }

        public IVfxHandle PlayLoop(VfxId id, Transform follow)
        {
            var e = _configs?.Vfx?.Get(id);
            if (e == null) return new Loop();
            EnsureRoot();
            var go = Object.Instantiate(e.Prefab, follow != null ? follow.position : Vector3.zero, Quaternion.identity, follow != null ? follow : _root);
            return new Loop { Go = go };
        }

        private static float LongestDuration(GameObject go)
        {
            float max = 2f;
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var m = systems[i].main;
                float d = m.duration + m.startLifetime.constantMax;
                if (d > max) max = d;
            }
            return max;
        }
    }
}
