using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public class HaltonSequence : ScriptableObject
    {
        [SerializeField] int ItemsCount = 30000;

        [HideInInspector] public List<Vector2> HaltonCache;
        [HideInInspector] public List<Vector2> HaltonInCircleCache;

        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesHex0;
        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesHex1;
        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesHex2;
        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesHex3;
        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesHex4;
        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesHex5;

        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesSquare0;
        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesSquare1;
        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesSquare2;
        [SerializeField, HideInInspector] List<PointInCell> HaltonSequenceByEdgesSquare3;

        List<PointInCell>[] HaltonSequenceByEdgesHex;
        List<PointInCell>[] HaltonSequenceByEdgesSquare;

        public List<PointInCell>[] HaltonSequenceByEdges(int cornersCount) => cornersCount == 6 ? HaltonSequenceByEdgesHex : HaltonSequenceByEdgesSquare;

        static HaltonSequence instance;

        public static HaltonSequence Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<HaltonSequence>(nameof(HaltonSequence));

                return instance;
            }
        }

        private void OnValidate()
        {
            if (HaltonCache == null || HaltonCache.Count != ItemsCount)
            {
                Init();
                instance = null;
            }
        }

        private void OnEnable()
        {
            FillArrays();
        }

        private void FillArrays()
        {
            HaltonSequenceByEdgesHex = new List<PointInCell>[] { HaltonSequenceByEdgesHex0, HaltonSequenceByEdgesHex1, HaltonSequenceByEdgesHex2, HaltonSequenceByEdgesHex3, HaltonSequenceByEdgesHex4, HaltonSequenceByEdgesHex5 };
            HaltonSequenceByEdgesSquare = new List<PointInCell>[] { HaltonSequenceByEdgesSquare0, HaltonSequenceByEdgesSquare1, HaltonSequenceByEdgesSquare2, HaltonSequenceByEdgesSquare3 };
        }

        void Init()
        {
            HaltonCache = new List<Vector2>(ItemsCount);
            HaltonInCircleCache = new List<Vector2>(ItemsCount);

            foreach (var p in Rnd.HaltonSequence2(2).Take(ItemsCount))
            {
                HaltonCache.Add(p);

                var pp = (p * 2 - Vector2.one);
                if (pp.sqrMagnitude < 1f)
                    HaltonInCircleCache.Add(pp);
            }

            HaltonSequenceByEdgesHex0 = new List<PointInCell>();
            HaltonSequenceByEdgesHex1 = new List<PointInCell>();
            HaltonSequenceByEdgesHex2 = new List<PointInCell>();
            HaltonSequenceByEdgesHex3 = new List<PointInCell>();
            HaltonSequenceByEdgesHex4 = new List<PointInCell>();
            HaltonSequenceByEdgesHex5 = new List<PointInCell>();

            HaltonSequenceByEdgesSquare0 = new List<PointInCell>();
            HaltonSequenceByEdgesSquare1 = new List<PointInCell>();
            HaltonSequenceByEdgesSquare2 = new List<PointInCell>();
            HaltonSequenceByEdgesSquare3 = new List<PointInCell>();

            FillArrays();

            ICellGeometry geometry = new HexCellGeometry(1, Vector3.zero);
            {
                foreach (var pp in HaltonInCircleCache)
                {
                    var pos = pp * geometry.Radius;
                    var pos3d = pos.ToVector3();
                    var dist = geometry.SignedDistance(pos3d);
                    if (dist >= 0)
                    {
                        var iEdge = geometry.PointToEdge(pos3d);
                        HaltonSequenceByEdgesHex[iEdge].Add(new PointInCell { Pos = pp, DistanceToEdge = dist / geometry.InnerRadius });
                    }
                }
            }

            geometry = new RectCellGeometry(1, Vector3.zero);
            {
                var k = Mathf.Sqrt(2);

                foreach (var p in HaltonCache)
                {
                    var pp = (p * 2 - Vector2.one);
                    var pos3d = pp.ToVector3();
                    var iEdge = geometry.PointToEdge(pos3d);
                    var dist01 = geometry.SignedDistance(pos3d);
                    
                    HaltonSequenceByEdgesSquare[iEdge].Add(new PointInCell { Pos = pp / k, DistanceToEdge = dist01 });
                }
            }
        }

        public IEnumerable<Vector2> GetHaltonInCircle(int start)
        {
            while (true)
            {
                yield return HaltonInCircleCache[start % HaltonInCircleCache.Count];
                start++;
            }
        }
    }

    [Serializable]
    public struct PointInCell
    {
        public Vector2 Pos;//local coord (normalized, for hex with Raius = 1)
        public float DistanceToEdge;// normalized 0-1
    }
}
