using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Enemy Config", fileName = "EnemyConfig")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [Header("Health")]
        public float MaxHealth = 30f;

        [Header("Crawl")]
        public float CrawlSpeed = 3.5f;
        public float HoverHeight = 0.15f;
        public float AlignRate = 8f;
        public LayerMask SurfaceMask = ~0;

        [Header("Surface probes")]
        public float StickProbeUp = 0.5f;
        public float MaxStepDown = 1.2f;
        public float StepHeight = 0.4f;
        public float LookAhead = 0.7f;
        public int FanRayCount = 5;
        public float FanHalfAngle = 35f;
        public float WallAngleThreshold = 45f;
        [Range(0f, 1f)] public float ClimbCoverage = 0.5f;
        public int SweepSteps = 6;
        public float SweepMaxAngle = 110f;
        public float FallSpeed = 6f;

        [Header("Targeting")]
        public float PlayerAggroRadius = 6f;
        public float BlockingAngleDeg = 45f;
        [Tooltip("Any alive player within this distance is targeted/pounced regardless of angle (the 'don't stand next to a mob' rule), even if not blocking the path to the cannon.")]
        public float PlayerThreatRadius = 3.5f;

        [Header("Pounce / latch")]
        public float PounceRange = 4f;
        public float PounceCooldown = 2f;
        public float PounceSpeed = 9f;
        public float PounceArcHeight = 1.5f;
        public float PounceTimeout = 1.5f;
        public float LatchDistance = 0.9f;

        [Tooltip("Vertical offset (world up) of the enemy ROOT when it latches a cannon module. Negative pulls the body down onto the part (offsets the Mimic visual's height). Tune so the spider sits ON the module.")]
        public float LatchHover = -0.5f;

        [Header("Anti-stuck")]
        public float StuckTime = 2.5f;
        public float ProgressEpsilon = 0.25f;
    }
}
