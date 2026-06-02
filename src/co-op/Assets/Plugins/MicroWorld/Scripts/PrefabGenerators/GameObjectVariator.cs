using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    /// <summary> 
    /// Prefab generator, takes prefabs from children
    /// </summary>
    class GameObjectVariator : MonoBehaviour, IPrefabGenerator
    {
        [SerializeField] int Seed = 0;

        public List<GameObject> GetPrefabs(int microWorldSeed, int amountOfSet, Transform holder)
        {
            var prefabs = transform.OfType<Transform>().Select(t => t.gameObject).Where(go => go.activeSelf).ToArray();
            amountOfSet = Mathf.Min(amountOfSet, prefabs.Length);
            var rnd = new Rnd(name, Rnd.CombineHashCodes(this.Seed, microWorldSeed));

            return rnd.GetRnds(prefabs, amountOfSet).Select(p => GameObject.Instantiate(p, holder)).ToList();
        }
    }
}


