using System;
using System.Collections;
using UnityEngine;

namespace MicroWorldNS
{
    public class OnActivatedLauncher : MonoBehaviour
    {
        public event Action OnActivated;

        private void Start()
        {
            OnActivated?.Invoke();
            Destroy(this);
        }
    }

}
