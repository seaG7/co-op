using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Wave Set Config", fileName = "WaveSetConfig")]
    public sealed class WaveSetConfig : ScriptableObject
    {
        [Serializable]
        public struct Wave
        {
            [Min(1)] public int Count;
            [Tooltip("Seconds between each enemy spawn within this wave.")]
            public float SpawnInterval;
        }

        [Tooltip("Networked enemy prefab to spawn (must be a FishNet spawnable prefab with an Enemy component).")]
        public GameObject EnemyPrefab;

        [Tooltip("Seconds after the source starts before the first wave.")]
        public float GraceBeforeFirstWave = 5f;

        [Tooltip("Seconds of pause between waves.")]
        public float PauseBetweenWaves = 8f;

        [Tooltip("Source hit points; destroying it ends the round in victory.")]
        public float SourceMaxHealth = 100f;

        public List<Wave> Waves = new();
    }
}
