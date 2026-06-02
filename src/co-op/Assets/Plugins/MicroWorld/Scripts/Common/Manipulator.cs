using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroWorldNS
{
    static class Manipulator
    {
        static Transform manipulator;

        public static Transform Get(Transform holder)
        {
            if (manipulator == null)
            {
                manipulator = new GameObject("Manipulator").transform;
                manipulator.hideFlags = HideFlags.HideAndDontSave;
                manipulator.SetParent(holder);
            }
            else
            {
                if (manipulator.parent != holder)
                    manipulator.SetParent(holder);
            }

            manipulator.DestroyAllChildren();
            manipulator.localScale = Vector3.one;
            manipulator.localRotation = Quaternion.identity;
            manipulator.localPosition = Vector3.zero;
            return manipulator;
        }

        public static void Destroy()
        {
            if (manipulator && manipulator.gameObject)
                GameObject.Destroy(manipulator.gameObject);
        }
    }
}
