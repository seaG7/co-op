using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Gameplay.World.Items;
using UnityEngine;

namespace Gameplay.World.Weapon
{
    public sealed class WeaponSnapPoint : NetworkBehaviour
    {
        [Header("Distances")]
        [Tooltip("From a carried item to this point — within this range the glow particles light up.")]
        [Min(0.05f)] public float HighlightDistance = 2f;

        [Tooltip("On release within this distance, the carried item snaps to this socket.")]
        [Min(0.05f)] public float SnapDistance = 0.5f;

        [Header("Visual")]
        [Tooltip("Particle system playing while a carried item is in range. Auto-found among children if null.")]
        [SerializeField] private ParticleSystem _glowParticles;

        public readonly SyncVar<bool> IsOccupied = new(false);

        [System.NonSerialized] public Carryable AttachedCarryable;

        public bool IsFree => !IsOccupied.Value;

        private static readonly List<WeaponSnapPoint> _all = new();
        public static IReadOnlyList<WeaponSnapPoint> All => _all;

        private bool _highlightedLocally;

        private void Awake()
        {
            if (_glowParticles == null) _glowParticles = GetComponentInChildren<ParticleSystem>(true);
            if (_glowParticles != null)
            {
                _glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var em = _glowParticles.emission;
                em.enabled = false;
            }
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public void SetHighlight(bool on)
        {
            if (_highlightedLocally == on) return;
            _highlightedLocally = on;
            if (_glowParticles == null) return;
            var em = _glowParticles.emission;
            em.enabled = on;
            if (on && !_glowParticles.isPlaying) _glowParticles.Play();
            else if (!on && _glowParticles.isPlaying) _glowParticles.Stop();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 1f, 0.8f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, HighlightDistance);
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, SnapDistance);
            Gizmos.color = new Color(1f, 1f, 0.4f, 1f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }
#endif
    }
}
