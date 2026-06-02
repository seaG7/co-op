using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public class TeleportPlayerToGate : MonoBehaviour
    {
        [SerializeField] MicroWorld World;
        [SerializeField] GameObject Player;

        private void Awake()
        {
            // subscribe to teleport to new terrain
            World.OnBuildCompleted += TeleportPlayer;
        }

        private void Start()
        {
            // teleport to existing terrain
            if (World.Terrain != null && World.Terrain.gameObject.activeInHierarchy)
                TeleportPlayer(World);
        }

        private void TeleportPlayer(MicroWorld world)
        {
            var gate = world.Gates.FirstOrDefault();
            if (gate?.Gate)
                BaseGateManager.TeleportPlayer(gate?.Gate, Player);
        }
    }
}
