using UnityEngine;

namespace Gameplay.Spawn
{
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Color _color = new(0.2f, 1f, 0.4f, 1f);

        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        private void Reset()
        {
            if (string.IsNullOrEmpty(gameObject.name) || gameObject.name == "GameObject")
                gameObject.name = "SpawnPoint";
            _color = Color.HSVToRGB(Random.value, 0.7f, 1f);
        }

        private void OnDrawGizmos()
        {
            var c = _color; c.a = 0.35f;
            Gizmos.color = c;
            Gizmos.DrawSphere(transform.position, 0.45f);
            c.a = 0.9f;
            Gizmos.color = c;
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
            Gizmos.DrawWireSphere(transform.position, 0.45f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, gameObject.name);
        }
#endif
    }
}
