using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace Gameplay.World.Weapon
{
    public sealed class Weapon : NetworkBehaviour
    {
        [SerializeField, HideInInspector] private List<WeaponSnapPoint> _snapPoints = new();

        public IReadOnlyList<WeaponSnapPoint> SnapPoints => _snapPoints;

        private void Awake()
        {
            _snapPoints.Clear();
            _snapPoints.AddRange(GetComponentsInChildren<WeaponSnapPoint>(includeInactive: true));
        }
    }
}
