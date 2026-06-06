using System.Collections.Generic;
using Data.Configs;
using FishNet.Object;
using Gameplay.Net;
using Gameplay.Player.Vitals;
using Gameplay.World.Sources;
using Infrastructure.Providers.Configs;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Weapon
{
    public sealed class Weapon : InjectableNetworkBehaviour
    {
        [SerializeField, HideInInspector] private List<WeaponSnapPoint> _snapPoints = new();

        [Header("Firing")]
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform[] _operatorStations;

        [Inject] private IConfigDataProvider _configs;
        [Inject] private SignalBus _signalBus;

        private float _fireTimer;

        public IReadOnlyList<WeaponSnapPoint> SnapPoints => _snapPoints;

        public bool IsAssembled
        {
            get
            {
                if (_snapPoints.Count == 0) return false;
                for (int i = 0; i < _snapPoints.Count; i++)
                    if (_snapPoints[i] == null || _snapPoints[i].IsFree) return false;
                return true;
            }
        }

        private void Awake()
        {
            _snapPoints.Clear();
            _snapPoints.AddRange(GetComponentsInChildren<WeaponSnapPoint>(includeInactive: true));
        }

        private void Update()
        {
            if (!IsServerInitialized) return;
            var cfg = _configs?.Weapon;
            if (cfg == null || !IsAssembled) return;

            if (_fireTimer > 0f) _fireTimer -= Time.deltaTime;

            int required = cfg.RequiresBothOperators ? 2 : 1;
            if (CountOperators(cfg) < required) return;

            var target = FindVulnerableSource();
            if (target == null) return;

            Vector3 origin = _muzzle != null ? _muzzle.position : transform.position;
            if ((target.transform.position - origin).magnitude > cfg.MuzzleRange) return;

            if (_fireTimer > 0f) return;
            _fireTimer = Mathf.Max(0.05f, cfg.FireInterval);

            target.ServerApplyDamage(cfg.ShotDamage);
            RpcFired(origin, target.transform.position, true);
        }

        private int CountOperators(WeaponConfig cfg)
        {
            float rSq = cfg.OperatorRange * cfg.OperatorRange;
            var players = PlayerVitals.All;

            if (_operatorStations == null || _operatorStations.Length == 0)
            {
                int near = 0;
                Vector3 c = transform.position;
                for (int i = 0; i < players.Count; i++)
                {
                    var v = players[i];
                    if (v != null && v.IsAlive && (v.transform.position - c).sqrMagnitude <= rSq) near++;
                }
                return near;
            }

            int manned = 0;
            for (int s = 0; s < _operatorStations.Length; s++)
            {
                var station = _operatorStations[s];
                if (station == null) continue;
                Vector3 p = station.position;
                for (int i = 0; i < players.Count; i++)
                {
                    var v = players[i];
                    if (v != null && v.IsAlive && (v.transform.position - p).sqrMagnitude <= rSq) { manned++; break; }
                }
            }
            return manned;
        }

        private static Source FindVulnerableSource()
        {
            var all = Source.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s != null && s.IsVulnerable.Value && !s.Destroyed.Value) return s;
            }
            return null;
        }

        [ObserversRpc]
        private void RpcFired(Vector3 origin, Vector3 hitPoint, bool hit)
        {
            _signalBus?.Fire(new WeaponFiredSignal(origin, hitPoint, hit));
        }
    }
}
