using MicroWorldNS.Spawners;
using System;
using System.Collections;
using UnityEngine;

namespace MicroWorldNS
{
    [ExecuteAlways]
    [RequireComponent(typeof(Terrain))]
    class AdjustObjectsPosition : MonoBehaviour
    {
        public float DelayTime = 0.3f;

        float lastChangedTime = 0;
        Terrain terrain;

        private void Update()
        {
            if (lastChangedTime > 0 && lastChangedTime + DelayTime < Time.time)
            {
                lastChangedTime = 0;
                if (Application.isPlaying)
                {
                    StartCoroutine(UpdatePositions());
                }
                else
                {
                    var en = UpdatePositions();
                    while (en.MoveNext()) ;
                }
            }
        }

        void OnTerrainChanged(TerrainChangedFlags flags)
        {
            if ((flags & (TerrainChangedFlags.Heightmap | TerrainChangedFlags.DelayedHeightmapUpdate)) != 0)
                lastChangedTime = Time.time;
        }

        private IEnumerator UpdatePositions()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            terrain = GetComponent<Terrain>();
            if (terrain == null)
                yield break;

            for (int i = 0; i < transform.childCount; i++)
            {
                var info = transform.GetChild(i).GetComponent<SpawnedObjInfo>();
                if (info)
                    UpdatePosition(info);

                if (sw.ElapsedMilliseconds > Preferences.Instance.MaxBuildDutyPerFrameInMs)
                {
                    yield return null;
                    sw.Restart();
                }
            }
        }

        private void UpdatePosition(SpawnedObjInfo info)
        {
            var pos = info.transform.position;
            pos.y = terrain.SampleHeight(pos) + info.OffsetY;
            info.transform.position = pos;
        }
    }
}
