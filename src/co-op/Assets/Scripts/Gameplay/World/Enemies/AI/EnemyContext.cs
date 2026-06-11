using UnityEngine;
using Data.Configs;
using Gameplay.Player.Vitals;
using Infrastructure.Services.Enemies;

namespace Gameplay.World.Enemies.AI
{
    public sealed class EnemyContext
    {
        public Transform Body;
        public EnemyConfig Config;
        public SurfaceCrawler Crawler;
        public IEnemyTargetingService Targeting;

        public Vector3 Up = Vector3.up;
        public Vector3 Forward = Vector3.forward;
        public bool Airborne;

        public EnemyTarget Target;
        public LatchInfo Latch;

        public float PounceCooldownLeft;
        public float StuckTimer;
        public float LastTargetDistance = float.MaxValue;
        public float DetourTimer;
        public float DetourSign = 1f;
        public float WanderTime;
        public float WanderSeed;

        public PlayerVitals PendingKnockdown;
        public bool DeadRequested;
        public EnemyEffectKind PendingEffect;
        public bool PendingLatchOnPlayer;
    }
}
