using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.World.Items
{
    public static class PlayerItemPhysics
    {
        private static readonly HashSet<CharacterController> Players = new();
        private static readonly HashSet<Carryable> Items = new();
        private static readonly List<Collider> Buffer = new();

        public static void RegisterPlayer(CharacterController cc)
        {
            if (cc == null || !Players.Add(cc)) return;
            foreach (var item in Items) SetIgnored(item, cc);
        }

        public static void UnregisterPlayer(CharacterController cc)
        {
            if (cc != null) Players.Remove(cc);
        }

        public static void RegisterItem(Carryable item)
        {
            if (item == null || !Items.Add(item)) return;
            foreach (var cc in Players) SetIgnored(item, cc);
        }

        public static void UnregisterItem(Carryable item)
        {
            if (item != null) Items.Remove(item);
        }

        private static void SetIgnored(Carryable item, CharacterController cc)
        {
            if (item == null || cc == null) return;
            item.GetComponentsInChildren(true, Buffer);
            for (int i = 0; i < Buffer.Count; i++)
            {
                var col = Buffer[i];
                if (col != null && !col.isTrigger) Physics.IgnoreCollision(col, cc, true);
            }
        }
    }
}
