using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Network Config", fileName = "NetworkConfig")]
    public class NetworkConfig : ScriptableObject
    {
        [Header("Defaults")]
        public ushort DefaultPort = 7777;
        public string DefaultAddress = "127.0.0.1";
        public string LocalhostAddress = "127.0.0.1";

        [Header("Timeouts (seconds)")]
        public float ConnectTimeoutSec = 10f;

        [Header("Dedicated server")]
        public bool UseDedicatedServer = false;
    }
}
