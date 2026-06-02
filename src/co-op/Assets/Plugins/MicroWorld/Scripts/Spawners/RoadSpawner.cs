using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// Builds roads/paths between cells of specified types.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.mur423cgb1qy")]
    public class RoadSpawner : BaseSpawner, IExclusive, IBuildPhaseHandler
    {
        public string ExclusiveGroup => "RoadSpawner";
        [field: SerializeField]
        [field: Tooltip("Defines the chance of selecting this spawner among all RoadSpawners of MicroWorld instance.")]
        public float Chance { get; set; } = 1;

        public override int Order => 650;
        [Tooltip("Specifies the types of cells between which roads are built.")]
        [Popup(nameof(ProposedCellTypes), true)]
        public string[] CellTypes = new string[] { "Ruins", "Gate" };
        [Tooltip("Specifies the radius of the road (meters).")]
        public float RoadRadius = 0.7f;
        [Tooltip("Additional radius of the taken area (meters). Сan be positive and negative. In fact, it defines an additional radius in which vegetation will not spawn.")]
        public float TakenAreaPadding = 0f;
        [Tooltip("Defines multiplier of micro noise amplitude for cells where road is spawned.")]
        public float MicroNoiseScale = 0.1f;
        public RoadSpawnerFeatures Features = RoadSpawnerFeatures.LiftUpRoadsToWaterLevel | RoadSpawnerFeatures.MakeMesh | RoadSpawnerFeatures.MakeCollider;

        [ShowIf(nameof(IsLiftUp))]
        [Tooltip("Cell height above water if LiftUpRoadsToWaterLevel or LiftUpCrossToWaterLevel is turned on (meters).")]
        public float HeightAboveWater = 1f;

        [Header("Road Bed")]
        [SerializeField][Tooltip("Elevation of road bed over terrain surface (meters). As a rule it is negative.")]
        float BedOffsetY = -0.3f;
        [SerializeField][Tooltip("Side padding of bed relative to road radius (meters).")]
        float BedPadding = 1.5f;
        const float BedHorizontality = 1f;

        [Header("Road Mesh")]
        [Tooltip("Prefab of road mesh that contains MeshRenderer with settings of rendering and  MeshCollider with settings of collider. Layer of the prefab will be the layer of collider.")]
        public MeshRenderer RoadPrefab;
        [Tooltip("Material of road surface.")]
        public Material Material;
        [Tooltip("Elevation of road mesh surface over bed bottom (meters). It should be positive.")]
        public float OffsetY = 0.01f;
        [Tooltip("Side padding of road mesh surface relative to road radius (meters).")]
        public float MeshPadding = 0.5f;
        [Range(0, 1)] public float SideIncline = 0.3f;
        public float UVSegmentLength = 5f;

        protected HashSet<Vector2Int> isRoadCell = new HashSet<Vector2Int>();
        protected Dictionary<Vector2Int, Vector2Int> cellToEnterCell = new Dictionary<Vector2Int, Vector2Int>();
        protected HashSet<string> myCellTypes;

        bool IsLiftUp => (Features & (RoadSpawnerFeatures.LiftUpCrossToWaterLevel | RoadSpawnerFeatures.LiftUpRoadsToWaterLevel)) != 0;
        bool IsCollider => Features.HasFlag(RoadSpawnerFeatures.MakeCollider);
        bool IsMesh => Features.HasFlag(RoadSpawnerFeatures.MakeMesh);
        bool IsMeshOrCollider => IsMesh || IsCollider;

        public IEnumerator OnPhaseCompleted(BuildPhase phase)
        {
            switch (phase)
            {
                case BuildPhase.CellHeightsCreated:
                    yield return BuildRoadNetwork();
                    break;
                case BuildPhase.TerrainHeightMapCreated:
                    yield return MakeBedOnTerrain();
                    break;
            }
        }

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            // build mesh
            var mesh = LineMeshHelper.BuildMesh(TakenAreaType.Road, Builder, UVSegmentLength, RoadRadius + MeshPadding, SideIncline, OffsetY);

            if (IsMeshOrCollider)
            {
                if (!RoadPrefab)
                    RoadPrefab = Resources.Load<MeshRenderer>("Road");

                var go = Instantiate(RoadPrefab, Terrain.transform);
                var mf = go.GetOrAddComponent<MeshFilter>();
                mf.sharedMesh = IsMesh ? mesh : null;
                if (Material)
                    go.sharedMaterial = Material;

                var coll = go.GetOrAddComponent<MeshCollider>();
                coll.sharedMesh = IsCollider ? mesh : null;
            }
        }

        private IEnumerator BuildRoadNetwork()
        {
            CheckMapSpawner();
            CheckTerrainSpawner();

            myCellTypes = CellTypes.ToHashSet();
            cellToEnterCell = new Dictionary<Vector2Int, Vector2Int>();
            var myCells = Map.AllHex().Where(p => !Map.IsBorderOrOutside(p) || myCellTypes.Contains(Map[p].Type.Name));
            var ruinsCells = myCells.Where(p => myCellTypes.Contains(Map[p].Type.Name)).ToHashSet();
            var RoadParent = new Vector2Int[Map.Size, Map.Size];

            foreach (var ruin in ruinsCells)
                cellToEnterCell[ruin] = ruin;

            // find crosses
            var isRoadNode = new HashSet<Vector2Int>();
            foreach (var ruin in ruinsCells)
            {
                var prev = ruin;
                var next = Map[ruin].Parent;
                while (next != Vector2Int.zero)
                {
                    if (prev == next)
                        break;

                    if (cellToEnterCell.TryGetValue(next, out var exists))
                    {
                        if (exists != prev)
                            isRoadNode.Add(next);
                        break;
                    }
                    cellToEnterCell[next] = prev;

                    //
                    prev = next;
                    next = Map[next].Parent;
                }
            }

            // find roads
            isRoadCell = new HashSet<Vector2Int>();
            isRoadCell.AddRange(isRoadNode);
            var queue = new Queue<Vector2Int>();
            foreach (var ruin in ruinsCells)
            {
                queue.Clear();
                queue.Enqueue(ruin);
                var next = Map[ruin].Parent;

                while (next != Vector2Int.zero)
                {
                    if (isRoadNode.Contains(next))
                        FlushQueue();
                    queue.Enqueue(next);
                    next = Map[next].Parent;
                }

                void FlushQueue()
                {
                    while (queue.Count > 0)
                        isRoadCell.Add(queue.Dequeue());
                }
            }

            // set flag "Road" in edges
            foreach (var hex in isRoadCell)
            {
                if (ruinsCells.Contains(hex) && !Features.HasFlag(RoadSpawnerFeatures.SpawnRoadsInTargetCells))
                    continue;

                var cell = Map[hex];
                var edges = cell.Edges;
                for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
                {
                    var n = CellGeometry.Neighbor(hex, iEdge);
                    if (isRoadCell.Contains(n) || ruinsCells.Contains(n))
                        if (Map[n].Parent == hex || cell.Parent == n)
                            edges[iEdge].IsRoad = true;
                }
            }

            // set flag "IsRoad" in cells
            foreach (var n in isRoadCell)
            {
                var count = Map[n].Edges.Count(e => e.IsRoad);
                Map[n].SetContent(CellContent.IsRoad, count >= 1);

                if (Map[n].HasContent(CellContent.IsRoad))
                {
                    // lift up above water
                    if (Features.HasFlag(RoadSpawnerFeatures.LiftUpRoadsToWaterLevel))
                        Builder.TerrainSpawner.LiftUpCellToWaterLevel(Map[n], HeightAboveWater);
                    Map[n].MicroNoiseScale *= MicroNoiseScale;
                }
            }

            foreach (var hex in isRoadNode)
            {
                var count = Map[hex].Edges.Count(e => e.IsRoad);
                Map[hex].SetContent(CellContent.IsRoadCross, count >= 3);

                if (Map[hex].HasContent(CellContent.IsRoadCross) && Features.HasFlag(RoadSpawnerFeatures.LiftUpCrossToWaterLevel))
                    Builder.TerrainSpawner.LiftUpCellToWaterLevel(Map[hex], HeightAboveWater);
            }

            yield return null;

            BuildPathsSegments();
        }

        private void BuildPathsSegments()
        {
            var neighbors = new List<(Vector2Int n, int iEdge, float height)>();

            foreach (var hex in Map.AllInsideHex())
            {
                var cell  = Map[hex];
                if (!cell.Content.HasFlag(CellContent.IsRoad)) continue;

                var center = Builder.HexToPos(hex).withSetY(cell.Height);

                // find neighbors by roads
                neighbors.Clear();
                for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
                {
                    var n = CellGeometry.Neighbor(hex, iEdge);
                    if (cell.Edges[iEdge].IsRoad)
                    {
                        var nCell = Map[n];
                        var h = (cell.Height * cell.Type.HeightPower + nCell.Height * nCell.Type.HeightPower) / (cell.Type.HeightPower + nCell.Type.HeightPower);
                        neighbors.Add((n, iEdge, h));
                    }
                }

                var takenRadius = RoadRadius + TakenAreaPadding;

                if (cell.Content.HasFlag(CellContent.IsRoadCross))
                {
                    foreach (var pair in neighbors)
                    {
                        var p = CellGeometry.EdgeCenter(hex, pair.iEdge);
                        cell.TakenAreas.Add(new TakenArea(p.ToVector2(), center.ToVector2(), takenRadius, TakenAreaType.Road));
                    }
                }else
                if (neighbors.Count == 2)
                {
                    var pair0 = neighbors[0];
                    var pair1 = neighbors[1];
                    var p0 = CellGeometry.EdgeCenter(hex, pair0.iEdge).withSetY(pair0.height);
                    var p1 = CellGeometry.EdgeCenter(hex, pair1.iEdge).withSetY(pair1.height);
                    var pp0 = ((p0 + center) / 2).withSetY(center.y);
                    var pp1 = ((p1 + center) / 2).withSetY(center.y);
                    cell.TakenAreas.Add(new TakenArea(p0.ToVector2(), pp0.ToVector2(), takenRadius, TakenAreaType.Road));
                    cell.TakenAreas.Add(new TakenArea(pp0.ToVector2(), pp1.ToVector2(), takenRadius, TakenAreaType.Road));
                    cell.TakenAreas.Add(new TakenArea(pp1.ToVector2(), p1.ToVector2(), takenRadius, TakenAreaType.Road));
                }
            }
        }

        protected virtual IEnumerator MakeBedOnTerrain() => MicroWorldHelper.MakeBedOnTerrain(Builder, BedPadding, RoadRadius, BedOffsetY, BedHorizontality, TakenAreaType.Road, Features.HasFlag(RoadSpawnerFeatures.SmoothEnds));

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (UnityEditor.Selection.activeGameObject != gameObject)
                return;

            var builder = GetComponentInParent<MicroWorld>();
            var map = builder?.Map;
            if (builder == null || map == null)
                return;

            foreach (var hex in map.AllInsideHex())
            {
                Gizmos.color = Color.green;
                var cell = map[hex];
                var center = builder.CellGeometry.Center(hex).withSetY(cell.Height + 1);

                for (var i = 0; i < builder.CellGeometry.CornersCount; i++)
                {
                    if (!cell.Edges[i].IsRoad)
                        continue;
                    var n = builder.CellGeometry.Neighbor(hex, i);
                    var nCell = map[n];
                    var nCenter = builder.CellGeometry.Center(n).withSetY(nCell.Height + 1);
                    Gizmos.DrawLine(center, (center + nCenter) / 2);
                }
            }

            Gizmos.color = Color.magenta;

            foreach (var hex in map.AllHex())
                if (map[hex].Content.HasFlag(CellContent.IsRoadCross))
                    Gizmos.DrawSphere(builder.HexToPos(hex) + Vector3.up * 1, 1);

            Gizmos.color = Color.green;

            foreach (var hex in map.AllHex())
                if (map[hex].Content.HasFlag(CellContent.IsRoad) && !map[hex].Content.HasFlag(CellContent.IsRoadCross))
                    Gizmos.DrawSphere(builder.HexToPos(hex) + Vector3.up * 1, 1);
        }
#endif
    }

    [Flags, Serializable]
    public enum RoadSpawnerFeatures
    {
        None = 0x0,
        LiftUpRoadsToWaterLevel = 0x1,
        LiftUpCrossToWaterLevel = 0x2,
        SpawnRoadsInTargetCells = 0x4,
        MakeMesh = 0x40,
        MakeCollider = 0x80,
        SmoothEnds = 0x100,
    }
}
