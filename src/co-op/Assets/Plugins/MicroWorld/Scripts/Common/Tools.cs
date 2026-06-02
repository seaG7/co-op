using MicroWorldNS.Spawners;
using System;
using UnityEngine;

namespace MicroWorldNS
{
    public class Tools : MonoBehaviour
    {
        [SerializeField] RemoveObjects RemoveObjects = new RemoveObjects();

        private void OnValidate()
        {
            var mw = GetComponentInParent<MicroWorld>();
            RemoveObjects.MicroWorld = mw;
        }
    }

    [Serializable]
    class RemoveObjects
    {
        [SerializeField] LayerMask Layer;
        [SerializeField] InspectorButton _ClearObjectsOverLayer;
        [SerializeField, HideInInspector] internal MicroWorld MicroWorld;

        void ClearObjectsOverLayer()
        {
            var terrain = MicroWorld?.Terrain;
            if (!terrain)
                return;

            foreach (var obj in terrain.GetComponentsInChildren<SpawnedObjInfo>(true))
            {
                var collided = Physics.Raycast(obj.transform.position + Vector3.up * 100, Vector3.down, 120, Layer, QueryTriggerInteraction.Ignore);
                obj.gameObject.SetActive(!collided);
            }
        }
    }

}
