using System;
using System.Collections;
using UnityEngine;

namespace MicroWorldNS
{
    /// <summary>
    /// Base class for GateManagers. 
    /// This class controls transitions between locations and the building of locations in the background.
    /// </summary>
    public abstract class BaseGateManager : MonoBehaviour
    {
        public static event Action<GameObject, Vector3, Quaternion> OnTeleportNeeded;

        protected virtual void Awake()
        {
            Gate.OnGatePassed += OnGatePassed;
        }

        protected virtual void OnDestroy()
        {
            Gate.OnGatePassed -= OnGatePassed;
        }

        public static void TeleportPlayer(Gate targetGate, GameObject player)
        {
            targetGate.FreezeTriggerNextFrames();
            var gateTr = targetGate.transform;
            var newPos = gateTr.position + Vector3.up * 0.5f;
            var lookAt = -gateTr.forward;
            var newRot = Quaternion.LookRotation(lookAt, Vector3.up);

            if (OnTeleportNeeded != null)
                OnTeleportNeeded.Invoke(player, newPos, newRot);
        }

        protected abstract IEnumerator OnGatePassed(Gate gate, GameObject player);

        public virtual void OnGatePassed(Gate gate, int direction, GameObject player)
        {
            if (!gate.DirectionIndependent && direction <= 0)
                return;

            StartCoroutine(OnGatePassed(gate, player));
        }
    }
}
