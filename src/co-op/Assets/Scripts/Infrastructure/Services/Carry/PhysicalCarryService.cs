using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using Gameplay.Player.Look;
using Gameplay.World.Items;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Network;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Carry
{
    public sealed class PhysicalCarryService : IPhysicalCarryService, IInitializable, IDisposable
    {
        private sealed class Holder
        {
            public NetworkConnection Conn;
            public PlayerLookController Look;
            public int AnchorIndex;
        }

        private sealed class HeldItem
        {
            public Carryable Item;
            public readonly List<Holder> Holders = new();
        }

        private readonly INetworkService _network;
        private readonly IConfigDataProvider _configs;
        private readonly Dictionary<Carryable, HeldItem> _held = new();
        private readonly List<Carryable> _toRelease = new();
        private readonly CancellationTokenSource _cts = new();

        public PhysicalCarryService(INetworkService network, IConfigDataProvider configs)
        {
            _network = network;
            _configs = configs;
        }

        public void Initialize() => DriveLoopAsync(_cts.Token).Forget();

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _held.Clear();
        }

        public bool TryGrab(Carryable item, NetworkConnection conn, Vector3 holderEye, Vector3 holderAim)
        {
            if (_network == null || !_network.IsServer || item == null || conn == null) return false;

            var look = FindLook(conn);
            if (look == null) return false;

            if (!_held.TryGetValue(item, out var h))
            {
                h = new HeldItem { Item = item };
                _held[item] = h;
            }
            if (h.Holders.Exists(x => x.Conn == conn)) return true;

            int required = Mathf.Max(1, item.HoldersRequired);
            if (h.Holders.Count >= required) return false;

            h.Holders.Add(new Holder { Conn = conn, Look = look, AnchorIndex = h.Holders.Count });

            if (h.Holders.Count >= required) Lift(item, conn);
            return true;
        }

        public bool IsHeldBy(Carryable item, NetworkConnection conn)
            => item != null && _held.TryGetValue(item, out var h) && h.Holders.Exists(x => x.Conn == conn);

        public int HolderCount(Carryable item)
            => item != null && _held.TryGetValue(item, out var h) ? h.Holders.Count : 0;

        public void Release(Carryable item, NetworkConnection conn, Vector3 throwAim, Vector3? explicitVelocity = null)
        {
            if (_network == null || !_network.IsServer || item == null) return;
            if (!_held.TryGetValue(item, out var h)) return;

            for (int i = h.Holders.Count - 1; i >= 0; i--)
            {
                if (h.Holders[i].Conn == conn || h.Holders[i].Conn == null)
                    h.Holders.RemoveAt(i);
            }
            if (h.Holders.Count > 0) return;

            var baseVel = explicitVelocity ?? Vector3.zero;
            _held.Remove(item);
            bool wasLifted = item.HolderClientId.Value != -1;
            item.HolderClientId.Value = -1;
            item.ApplyPhysicsState();

            if (!wasLifted) return;

            var carry = _configs?.Carry;
            var rb = item.Body;
            if (rb != null && !rb.isKinematic && carry != null)
            {
                Vector3 v = baseVel * carry.ThrowVelocityScaleV2 + throwAim.normalized * carry.ThrowAimImpulse;
                rb.linearVelocity = Vector3.ClampMagnitude(v, carry.MaxThrowSpeed);
            }
        }

        private void Lift(Carryable item, NetworkConnection firstHolder)
        {
            item.HolderClientId.Value = firstHolder.ClientId;
            item.HasBeenGrabbedOnce.Value = true;
            if (item.IsSnapped.Value) item.IsSnapped.Value = false;
            item.ApplyPhysicsState();
        }

        private async UniTaskVoid DriveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
                    if (_network == null || !_network.IsServer || _held.Count == 0) continue;
                    Drive(Time.deltaTime);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void Drive(float dt)
        {
            var carry = _configs?.Carry;
            if (carry == null || dt <= 0f) return;

            foreach (var kv in _held)
            {
                var h = kv.Value;
                var item = h.Item;
                if (item == null || item.Body == null) { _toRelease.Add(kv.Key); continue; }

                for (int i = h.Holders.Count - 1; i >= 0; i--)
                {
                    var hl = h.Holders[i];
                    if (hl.Conn == null || !hl.Conn.IsActive || hl.Look == null)
                        h.Holders.RemoveAt(i);
                }

                int required = Mathf.Max(1, item.HoldersRequired);
                bool lifted = item.HolderClientId.Value != -1;

                if (h.Holders.Count == 0) { _toRelease.Add(item); continue; }
                if (lifted && h.Holders.Count < required) { _toRelease.Add(item); continue; }
                if (h.Holders.Count < required) continue;

                if (h.Holders.Count >= 2)
                {
                    float sep = Vector3.Distance(h.Holders[0].Look.EyePosition, h.Holders[1].Look.EyePosition);
                    if (sep > carry.TwoHandMaxSeparation) { _toRelease.Add(item); continue; }
                }

                // Item transform while held is set client-side (rigid to the holder's CarryAnchor).
                // The server only does holder bookkeeping here; on release it re-enables the
                // NetworkTransform and applies throw velocity.
            }

            for (int i = 0; i < _toRelease.Count; i++)
            {
                var item = _toRelease[i];
                if (item == null) continue;
                if (_held.TryGetValue(item, out var hi) && hi.Holders.Count > 0)
                {
                    var conns = new List<NetworkConnection>(hi.Holders.Count);
                    foreach (var holder in hi.Holders) conns.Add(holder.Conn);
                    foreach (var c in conns) Release(item, c, Vector3.zero);
                }
                else
                {
                    _held.Remove(item);
                    if (item.HolderClientId.Value != -1) item.HolderClientId.Value = -1;
                    item.ApplyPhysicsState();
                }
            }
            _toRelease.Clear();
        }

        private static PlayerLookController FindLook(NetworkConnection conn)
        {
            foreach (var nob in conn.Objects)
            {
                var look = nob.GetComponent<PlayerLookController>();
                if (look != null) return look;
            }
            return null;
        }
    }
}
