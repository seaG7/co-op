using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Vitals Config", fileName = "VitalsConfig")]
    public sealed class VitalsConfig : ScriptableObject
    {
        [Tooltip("Seconds a downed player survives before dying if not revived.")]
        public float DownReviveSeconds = 15f;

        [Tooltip("Seconds a teammate must stay in range to fully revive a downed player.")]
        public float ReviveHoldSeconds = 3f;

        [Tooltip("How close (m) an alive teammate must be to revive a downed player.")]
        public float ReviveRange = 2.5f;

        [Tooltip("How fast revive progress drains (multiplier of real time) when no reviver is in range.")]
        public float ReviveDecayMultiplier = 2f;

        [Tooltip("Spectator camera offset relative to the followed teammate's facing (z = behind, y = up) for a third-person watch view, e.g. (0, 2, -3.5).")]
        public Vector3 SpectateCameraOffset = new Vector3(0f, 2f, -3.5f);
    }
}
