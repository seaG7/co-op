using System;
using Cysharp.Threading.Tasks;
using FishNet.Object;
using Gameplay.World.Items;
using UnityEngine;

namespace Gameplay.World.Weapon
{
    public sealed class CorpseIntake : MonoBehaviour
    {
        [SerializeField] private Weapon _weapon;

        [Header("Load zone")]
        [Tooltip("A carried/dropped corpse within this radius of the tube mouth is sucked in and charges the cannon.")]
        [SerializeField] private float _radius = 2f;

        [Header("Suck tube (corpse travels start -> end while shrinking to nothing, then counts)")]
        [Tooltip("Mouth of the tube — the corpse snaps here and starts shrinking.")]
        [SerializeField] private Transform _tubeStart;
        [Tooltip("Throat of the tube — the corpse finishes here at zero scale, then despawns.")]
        [SerializeField] private Transform _tubeEnd;
        [SerializeField] private float _suckDuration = 0.6f;

        private void Awake()
        {
            if (_weapon == null) _weapon = GetComponentInParent<Weapon>();
            if (_weapon == null) Debug.LogError($"[{nameof(CorpseIntake)}] No Weapon found in parents; corpses will not load.", this);
        }

        private void Update()
        {
            if (_weapon == null || !_weapon.IsServerInitialized || _tubeStart == null) return;

            Vector3 mouth = _tubeStart.position;
            Vector3 end = _tubeEnd != null ? _tubeEnd.position : mouth;
            float r2 = _radius * _radius;

            var corpses = Corpse.All;
            for (int i = 0; i < corpses.Count; i++)
            {
                var corpse = corpses[i];
                if (corpse == null) continue;
                if ((corpse.transform.position - mouth).sqrMagnitude > r2) continue;
                if (!corpse.TryGetComponent<Carryable>(out var c) || c.Consuming.Value) continue;
                var no = c.NetworkObject;
                if (no == null || !no.IsSpawned) continue;

                c.ServerBeginConsume(mouth, end, _suckDuration);
                ConsumeAndCount(no, _suckDuration).Forget();
            }
        }

        private async UniTaskVoid ConsumeAndCount(NetworkObject no, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());
            if (_weapon == null || !_weapon.IsServerInitialized) return;
            _weapon.AddCorpse();
            if (no != null && no.IsSpawned) no.Despawn();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            if (_tubeStart != null) Gizmos.DrawWireSphere(_tubeStart.position, _radius);
            if (_tubeStart != null && _tubeEnd != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_tubeStart.position, _tubeEnd.position);
            }
        }
#endif
    }
}
