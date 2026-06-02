using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Infrastructure.Factories.Objects
{
    public sealed class GameObjectFactory : IGameObjectFactory
    {
        private readonly DiContainer _globalContainer;
        private readonly List<GameObject> _tracked = new();

        public GameObjectFactory(DiContainer globalContainer)
        {
            _globalContainer = globalContainer;
        }

        public GameObject Instantiate(GameObject prefab, Vector3? position = null,
            Quaternion? rotation = null, Transform parent = null, DiContainer container = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[GameObjectFactory] Prefab is null.");
                return null;
            }

            var c = container ?? _globalContainer;
            var go = c.InstantiatePrefab(prefab, position ?? Vector3.zero,
                rotation ?? Quaternion.identity, parent);

            if (parent == null && go != null)
                SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());

            if (go != null) _tracked.Add(go);
            return go;
        }

        public T InstantiateAndGetComponent<T>(GameObject prefab, Vector3? position = null,
            Quaternion? rotation = null, Transform parent = null, DiContainer container = null) where T : Component
        {
            var go = Instantiate(prefab, position, rotation, parent, container);
            if (go == null) return null;
            var component = go.GetComponent<T>();
            if (component == null)
                Debug.LogError($"[GameObjectFactory] Component {typeof(T).Name} missing on {go.name}.");
            return component;
        }

        public void Destroy(GameObject gameObject)
        {
            if (gameObject == null) return;
            _tracked.Remove(gameObject);
            Object.Destroy(gameObject);
        }

        public void Cleanup()
        {
            foreach (var go in _tracked)
                if (go != null) Object.Destroy(go);
            _tracked.Clear();
        }
    }
}
