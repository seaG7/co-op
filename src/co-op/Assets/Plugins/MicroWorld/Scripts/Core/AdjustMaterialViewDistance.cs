using UnityEngine;

namespace MicroWorldNS
{
    [RequireComponent(typeof(Terrain), typeof(MeshRenderer))]
    class AdjustMaterialViewDistance : MonoBehaviour
    {
        float adjustedFor = -1;
        Terrain terrain;
        MeshRenderer mr;

        private void Start()
        {
            terrain = GetComponent<Terrain>();
            mr = GetComponent<MeshRenderer>();
        }

        private void Update()
        {
            if (terrain == null || mr == null)
                return;
            if (terrain.detailObjectDistance != adjustedFor)
                Adjust(terrain.detailObjectDistance);
        }

        private void Adjust(float viewDist)
        {
            adjustedFor = viewDist;

            foreach (var mat in mr.sharedMaterials)
            {
                mat.SetFloat("_ViewDist", viewDist);
                mat.renderQueue = 2000;
            }
        }
    }
}
