using UnityEngine;
using Zenject;

namespace Infrastructure.Factories.Objects
{
    public interface IGameObjectFactory
    {
        GameObject Instantiate(GameObject prefab, Vector3? position = null,
            Quaternion? rotation = null, Transform parent = null, DiContainer container = null);

        T InstantiateAndGetComponent<T>(GameObject prefab, Vector3? position = null,
            Quaternion? rotation = null, Transform parent = null, DiContainer container = null) where T : Component;

        void Destroy(GameObject gameObject);
        void Cleanup();
    }
}
