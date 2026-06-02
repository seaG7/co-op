using System;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    /// <summary> 
    /// Global Preferences of MicroWorld
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.iw3z3bz3z1pe")]
    [Serializable]
    [CreateAssetMenu()]
    public class Preferences : ScriptableObject
    {
        public float MaxPassageHeight = 2f;
        [Range(0, 90)] public float InclineWalkableAngle = 45;
        [Range(-90, 0)] public float DeclineWalkableAngle = -55;
        public float StepHeight = 0.3f;

        [Header("Scale Density and Noise Proportionally To Cell Size:")]
        [Range(0, 1)] public float ScaleLandscapeNoiseAmplProportionallyToCellSize = 0.5f;
        [Range(0, 1)] public float ScaleSpawnCountProportionallyToCellSize = 1f;

        [Header("Background build:")]
        public float MaxBuildDutyPerFrameInMs = 0.5f;// ms

        [Header("Debug:")]
        public LogFeatures LogFeatures = LogFeatures.LogBuildTime | LogFeatures.LogSeed;

        [Header("Scale prefabs:")]
        [Range(0, 2)] public float ScaleGrassWidth = 0.8f;
        [Range(0, 2)] public float ScaleGrassHeight = 0.8f;
        [Range(0, 2)] public float ScaleRocks = 1;
        [Range(0, 2)] public float ScaleBushes = 1;
        [Range(0, 2)] public float ScalePlants = 1;
        [Range(0, 2)] public float ScaleTrees = 1;
        [Range(0, 2)] public float ScaleSticks = 1;

        private static Preferences instance;

        public static Preferences Instance 
        {
            get
            {
                if (instance == null)
                    instance = Resources.LoadAll<Preferences>("").FirstOrDefault();
                return instance;
            }
        }

        public static void Reset()
        {
            instance = new Preferences();
        }
    }

    [Flags, Serializable]
    public enum LogFeatures
    {
        LogBuildTime = 0x1,
        LogSeed = 0x2,
    }
}
