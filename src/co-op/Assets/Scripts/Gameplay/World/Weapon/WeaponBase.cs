using UnityEngine;

namespace Gameplay.World.Weapon
{
    [DisallowMultipleComponent]
    public sealed class WeaponBase : MonoBehaviour
    {
        [Tooltip("Networked weapon prefab — server spawns one at this base's transform at level-ready.")]
        public GameObject WeaponPrefab;

        public Vector3 SpawnWorldPos => transform.position;
        public Quaternion SpawnWorldRot => transform.rotation;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.5f, 1f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(2f, 0.5f, 1.5f));
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = new Color(1f, 0.6f, 1f, 1f);
            Vector3 fwdEnd = transform.position + transform.forward * 1.8f;
            Gizmos.DrawLine(transform.position, fwdEnd);
            Vector3 right = transform.right;
            Gizmos.DrawLine(fwdEnd, fwdEnd - transform.forward * 0.3f + right * 0.15f);
            Gizmos.DrawLine(fwdEnd, fwdEnd - transform.forward * 0.3f - right * 0.15f);

            if (WeaponPrefab == null) return;
            var mfs = WeaponPrefab.GetComponentsInChildren<MeshFilter>(true);
            if (mfs == null || mfs.Length == 0) return;
            var prefabRoot = WeaponPrefab.transform;
            var prevMatrix = Gizmos.matrix;
            Gizmos.color = new Color(0.9f, 0.5f, 1f, 0.4f);
            foreach (var mf in mfs)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                Matrix4x4 localToPrefab = prefabRoot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Matrix4x4 worldOfGhost = Matrix4x4.TRS(transform.position, transform.rotation, prefabRoot.localScale);
                Gizmos.matrix = worldOfGhost * localToPrefab;
                Gizmos.DrawMesh(mf.sharedMesh, 0);
            }
            Gizmos.matrix = prevMatrix;
        }
#endif
    }
}
