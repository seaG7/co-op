using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// Info about spawned object
    /// This script will added to AdditionalPrefab
    /// </summary>
    public class SpawnedPrefabInfo : MonoBehaviour
    {
        public RenderType RenderType;
        public GameObject SpawnedObj;
        public DetailPrototype DetailLayer;
        public TreePrototype TreeLayer;
        public int TreeIndex;// tree index in TreeLayer
    }
}
