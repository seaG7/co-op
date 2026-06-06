using System;
using System.Collections.Generic;
using FishNet.Connection;
using Gameplay.Player.Carry;
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
            public Vector3 LastPos;
            public Vector3 TrackedVelocity;
        }

        private readonly INetworkService _network;
        private readonly IConfigDataProvider _configs;
        private readonly Dictionary<Carryable, HeldItem> _held = new();
        private readonly List<Carryable> _toRelease = new();
        private bool _subscribed;

        public PhysicalCarryService(INetworkService network, IConfigDataProvider configs)
        {
            _network = network;
            _configs = configs;
        }

        public void Initialize()
        {
            var tm = _network?.NetworkManager?.TimeManager;
            if (tm == null)
            {
                Debug.LogWarning("[PhysicalCarryService] No TimeManager available; carry follow disabled.");
                return;
            }
            tm.OnTick += OnTick;
            _subscribed = true;
        }

        public void Dispose()
        {
            if (_subscribed && _network?.NetworkManager?.TimeManager != null)
                _network.NetworkManager.TimeManager.OnTick -= OnTick;
            _subscribed = false;
            _held.Clear();
        }

        public bool TryGrab(Carryable item, NetworkConnection conn, Vector3 holderEye, Vector3 holderAim)
        {
            if (_network == null || !_network.IsServer || item == null || conn == null) return false;

            var look = FindLook(conn);
            if (look == null) return false;

            if (!_held.TryGetValue(item, out var h))
            {
                h = new HeldItem { Item = item, LastPos = item.transform.position };
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

        public void Release(Carryable item, NetworkConnection conn, Vector3 throwAim)
        {
            if (_network == null || !_network.IsServer || item == null) return;
            if (!_held.TryGetValue(item, out var h)) return;

            for (int i = h.Holders.Count - 1; i >= 0; i--)
            {
                if (h.Holders[i].Conn == conn || h.Holders[i].Conn == null)
                    h.Holders.RemoveAt(i);
            }
            if (h.Holders.Count > 0) return;

            var tracked = h.TrackedVelocity;
            _held.Remove(item);
            bool wasLifted = item.HolderClientId.Value != -1;
            item.HolderClientId.Value = -1;
            item.ApplyPhysicsState();

            if (!wasLifted) return;

            var carry = _configs?.Carry;
            var rb = item.Body;
            if (rb != null && !rb.isKinematic && carry != null)
            {
                Vector3 v = tracked * carry.ThrowVelocityScaleV2 + throwAim.normalized * carry.ThrowAimImpulse;
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

        private void OnTick()
        {
            if (_network == null || !_network.IsServer || _held.Count == 0) return;
            var carry = _configs?.Carry;
            if (carry == null) return;

            float dt = (float)_network.NetworkManager.TimeManager.TickDelta;
            if (dt <= 0f) dt = Time.fixedDeltaTime;

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

                float holdDist = item.HoldDistance > 0f ? item.HoldDistance : carry.DefaultHoldDistance;
                int n = h.Holders.Count;
                var grips = new HolderGrip[n];
                var anchors = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    var look = h.Holders[i].Look;
                    Vector3 aim = look.AimDirection.normalized;
                    grips[i] = new HolderGrip(look.EyePosition + aim * holdDist + Vector3.down * 0.1f, aim, Vector3.up);
                    anchors[i] = item.AnchorLocalPosition(h.Holders[i].AnchorIndex);
                }

                var target = CarrySolver.SolveTarget(grips, anchors, Vector3.up);
                float massMult = carry.SpeedMultiplierForMass(item.Mass);
                var rb = item.Body;
                rb.linearVelocity = CarrySolver.FollowVelocity(rb.position, target.Position, dt,
                    carry.FollowMaxSpeed * massMult, carry.FollowResponsiveness * massMult);
                rb.angularVelocity = CarrySolver.AngularVelocity(rb.rotation, target.Rotation, dt, carry.FollowMaxAngularSpeed);

                h.TrackedVelocity = Vector3.Lerp(h.TrackedVelocity,
                    (item.transform.position - h.LastPos) / Mathf.Max(dt, 1e-4f), 0.5f);
                h.LastPos = item.transform.position;
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
