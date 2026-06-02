using System.Collections.Generic;
using UnityEngine;

namespace MicroWorldNS
{
    /// <summary> 
    /// Source of prefabs
    /// </summary>
    public interface IPrefabGenerator
    {
        List<GameObject> GetPrefabs(int seed, int amountOfSet, Transform holder);
    }
}
