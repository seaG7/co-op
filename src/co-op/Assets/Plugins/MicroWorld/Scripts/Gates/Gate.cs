using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    /// <summary>
    /// Gate component. It waiting while player passed through gate collider and fire static event OnGatePassed
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Gate : MonoBehaviour
    {
        /// <summary> My MicroWorld </summary>
        [HideInInspector] public MicroWorld World;
        /// <summary> My GateInfo in builder </summary>
        [HideInInspector] public GateInfo GateInfo;

        /// <summary> This event is called when player pass through gate </summary>
        public static event GatePassed OnGatePassed;
        public delegate void GatePassed(Gate gate, int direction, GameObject player);

        public int Index => World?.Gates.IndexOf(GateInfo) ?? -1;

        [SerializeField] string PlayerTag = "Player";
        [SerializeField] LayerMask CheckLayer = -1;
        [SerializeField] public bool DirectionIndependent = true;
        //PostProcessVolume ppVolume;
        int freezeTriggerNextFrames = 0;

        private void OnEnable()
        {
            //ppVolume = GetComponentInChildren<PostProcessVolume>();
            //if (ppVolume)
            //    ppVolume.weight = 1f;

            FreezeTriggerNextFrames();
        }

        public void FreezeTriggerNextFrames()
        {
            freezeTriggerNextFrames = 3;
        }

        private void FixedUpdate()
        {
            //if (ppVolume && ppVolume.weight > 0)
            //    ppVolume.weight = Mathf.MoveTowards(ppVolume.weight, 0, Time.deltaTime);

            if (freezeTriggerNextFrames > 0)
                freezeTriggerNextFrames--;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CheckLayer.Contains(other.gameObject.layer))
                return;

            var player = other.transform.GetAllParents(true).LastOrDefault(p => p.CompareTag(PlayerTag));
            if (!player)
                return;

            if (World == null || GateInfo == null)
                return;

            if (freezeTriggerNextFrames > 0)
                return;

            // dir > 0 => player leaves terrain
            // dir < 0 => player enter terrain
            var dir = -(int)Mathf.Sign(transform.InverseTransformPoint(other.transform.position).z);

            Debug.Log("Player passed gate: " + dir);

            Dispatcher.Enqueue(() => OnGatePassed?.Invoke(this, dir, player.gameObject));
        }
    }
}
