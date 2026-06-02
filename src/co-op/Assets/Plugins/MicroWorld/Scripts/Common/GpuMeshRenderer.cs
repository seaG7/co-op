using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MicroWorldNS
{
    [ExecuteAlways]
    public class GpuMeshRenderer : MonoBehaviour
    {
        public Mesh Mesh;
        public Material[] Materials = new Material[0];
        public ShadowCastingMode CastShadows = ShadowCastingMode.On;
        public bool ReceiveShadows = true;
        public bool MatricesIsInLocalPositions = false;

        [HideInInspector]
        public List<Matrix4x4> Matrices = new List<Matrix4x4>();
        public MaterialPropertyBlock MaterialPropertyBlock { get; set; }
        public bool DoNotSpawnWhileEnabled { get; set; }

        void Update()
        {
            if (DoNotSpawnWhileEnabled)
                return;
#if UNITY_EDITOR
            // to debug 
            if (Matrices.Count == 0 && Materials != null)
            {
                for (int iSub = 0; iSub < Materials.Length; iSub++)
                    Graphics.DrawMeshInstanced(Mesh, iSub, Materials[iSub], new Matrix4x4[] { transform.localToWorldMatrix }, 1, MaterialPropertyBlock, CastShadows, ReceiveShadows, gameObject.layer);
                return;
            }
#endif

            for (int iSub = 0; iSub < Materials.Length; iSub++)
                Graphics.DrawMeshInstanced(Mesh, iSub, Materials[iSub], Matrices, MaterialPropertyBlock, CastShadows, ReceiveShadows, gameObject.layer);
        }

        private void OnEnable()
        {
            DoNotSpawnWhileEnabled = false;
        }
    }
}