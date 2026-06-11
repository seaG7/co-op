using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.World.Items
{
    public sealed class Corpse : MonoBehaviour
    {
        private static readonly List<Corpse> _all = new();
        public static IReadOnlyList<Corpse> All => _all;

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() { _all.Remove(this); }
    }
}
