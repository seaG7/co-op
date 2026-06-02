using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public class Variant : MonoBehaviour
    {
        public bool Exclusive = true;

        public static void Build(GameObject holder, Rnd rnd)
        {
            Build(holder.GetComponentsInChildren<Variant>(), rnd);
        }

        public static void Build(IEnumerable<Variant> variants, Rnd rnd)
        {
            var variantsByParent = new Dictionary<Transform, List<Variant>>();
            foreach (var v in variants)
            {
                if (!variantsByParent.TryGetValue(v.transform.parent, out var list))
                    variantsByParent[v.transform.parent] = list = new List<Variant>();
                list.Add(v);
            }

            foreach (var pair in variantsByParent)
            {
                var parent = pair.Key;
                if (!parent) continue;
                var exclusives = pair.Value.Where(v=>v != null && v.Exclusive).ToArray();
                if (exclusives.Length == 0) continue;
                var selected = rnd.Int(exclusives.Length);
                for (int i = 0; i < exclusives.Length; i++)
                {
                    // destroy unselected object
                    if (i != selected)
                    {
                        var go = exclusives[i].gameObject;
                        go.SetActive(false);
                        Helper.DestroySafe(go);
                        continue;
                    }
                    // destroy variant script
                    exclusives[i].enabled = false;
                    Helper.DestroySafe(exclusives[i]);
                }
            }
        }
    }
}
