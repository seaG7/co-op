using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    public class RiverSpawner : BaseSpawner, IExclusive, IBuildPhaseHandler
    {
        public string ExclusiveGroup => "RiverSpawner";
        [field: SerializeField]
        [field: Tooltip("Defines the chance of selecting this spawner among all RiverSpawners of MicroWorld instance.")]
        public float Chance { get; set; } = 1;

        public override int Order => 670;
        [Tooltip("Defines the types of cells through which a river can flow.")]
        [Popup(nameof(ProposedCellTypes), true)]
        public string[] CellTypes = new string[] { "-Ruins" };
        [Tooltip("Specifies the radius of the river (meters).")]
        public float RiverRadius = 0.7f;
        [Tooltip("Additional radius of the taken area (meters). Сan be positive and negative. In fact, it defines an additional radius in which vegetation will not spawn.")]
        public float TakenAreaPadding = 0f;
        [Tooltip("Defines multiplier of micro noise amplitude for cells where river is spawned.")]
        public float MicroNoiseScale = 0.1f;
        public int MinRiverLength = 3;
        public RiverSpawnerFeatures Features = RiverSpawnerFeatures.MakeMesh;

        [Header("River Bed")]
        [SerializeField]
        [Tooltip("Elevation of river bed over terrain surface (meters). As a rule it is negative.")]
        float BedOffsetY = -0.3f;
        [SerializeField]
        [Tooltip("Side padding of bed relative to river radius (meters).")]
        float BedPadding = 1.5f;
        const float BedHorizontality = 1f;

        [Header("River Mesh")]
        [Tooltip("Prefab of river mesh that contains MeshRenderer with settings of rendering and  MeshCollider with settings of collider. Layer of the prefab will be the layer of collider.")]
        public MeshRenderer RiverPrefab;
        [Tooltip("Material of river surface.")]
        public Material Material;
        [Tooltip("Elevation of river mesh surface over bed bottom (meters). It should be positive.")]
        public float OffsetY = 0.01f;
        [Tooltip("Side padding of river mesh surface relative to river radius (meters).")]
        public float MeshPadding = 0.5f;
        [Range(0, 1)] public float SideIncline = 0.3f;
        public float UVSegmentLength = 5f;

        protected HashSet<Vector2Int> isRoadCell = new HashSet<Vector2Int>();
        protected Dictionary<Vector2Int, Vector2Int> cellToEnterCell = new Dictionary<Vector2Int, Vector2Int>();
        protected HashSet<string> myCellTypes;

        bool IsCollider => Features.HasFlag(RiverSpawnerFeatures.MakeCollider);
        bool IsMesh => Features.HasFlag(RiverSpawnerFeatures.MakeMesh);
        bool IsMeshOrCollider => IsMesh || IsCollider;

        public IEnumerator OnPhaseCompleted(BuildPhase phase)
        {
            switch (phase)
            {
                case BuildPhase.CellHeightsCreated:
                    yield return BuildRiverNetwork();
                    break;
                case BuildPhase.TerrainHeightMapCreated:
                    yield return MakeBedOnTerrain;
                    break;
            }
        }

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            // build mesh
            var mesh = LineMeshHelper.BuildMesh(TakenAreaType.River, Builder, UVSegmentLength, RiverRadius + MeshPadding, SideIncline, OffsetY);

            if (IsMeshOrCollider)
            {
                if (!RiverPrefab)
                    RiverPrefab = Resources.Load<MeshRenderer>("River");

                var go = Instantiate(RiverPrefab, Terrain.transform);
                var mf = go.GetOrAddComponent<MeshFilter>();
                mf.sharedMesh = IsMesh ? mesh : null;
                if (Material)
                    go.sharedMaterial = Material;

                var coll = go.GetOrAddComponent<MeshCollider>();
                coll.sharedMesh = IsCollider ? mesh : null;
            }
        }

        protected virtual IEnumerator MakeBedOnTerrain => MicroWorldHelper.MakeBedOnTerrain(Builder, BedPadding, RiverRadius, BedOffsetY, BedHorizontality, TakenAreaType.River, Features.HasFlag(RiverSpawnerFeatures.SmoothEnds));

        class River
        {
            public List<Vector2Int> Cells;
        }

        private IEnumerator BuildRiverNetwork()
        {
            CheckMapSpawner();
            CheckTerrainSpawner();
            var rnd = rootRnd.GetBranch(923);

            // get cells where river allowed
            var allowedCells = new HashSet<Vector2Int>();
            foreach (var hex in Map.AllInsideHex())
            {
                var cell = Map[hex];
                if (!CellTypes.CheckCellType(cell.Type.Name))
                    continue;
                if (cell.Height <= Builder.WaterLevel)
                    continue;
                if (cell.Content != 0)
                    continue;
                allowedCells.Add(hex);
            }

            // get start water cells
            var startHexes = Map.AllInsideHex().Where(p => Map[p].Height < Builder.WaterLevel).ToList();
            rnd.ShuffleFisherYates(startHexes);

            yield return null;

            // create rivers
            var taken = new HashSet<Vector2Int>();
            var rivers = new List<River>();

            foreach (var startHex in startHexes)
                CreateRiver(startHex, taken);

            // set flag IsRiver in cells
            foreach (var river in rivers)
            foreach (var hex in river.Cells.Skip(0))
                Map[hex].SetContent(CellContent.IsRiver, true);

            // set flag IsRiver in edges
            foreach (var river in rivers)
            {
                for (int i = 1; i < river.Cells.Count; i++)
                {
                    var hex = river.Cells[i];
                    var prevHex = river.Cells[i - 1];
                    var iEdge = CellGeometry.PointToEdge(hex, CellGeometry.Center(prevHex));
                    Map[hex].Edges[iEdge].IsRiver = true;
                    Map[prevHex].Edges[CellGeometry.OppositeCorner(iEdge)].IsRiver = true;
                }
            }

            yield return null;

            BuildPathsSegments();

            void CreateRiver(Vector2Int hex, HashSet<Vector2Int> taken)
            {
                var cells = new List<Vector2Int>() {  hex };

                while (true)
                {
                    var curH = Map[hex].Height;
                    var bestNext = CellGeometry
                        .Neighbors(hex)
                        .Where(n => allowedCells.Contains(n) && !taken.Contains(n) && Map[n].Height > curH && CheckHeight(n))
                        .MinItem(n => Map[n].Height - curH);

                    if (bestNext == default)
                        break;

                    //if (cells.Count == 1 && Map[bestNext].Height > 6)
                        //break;

                    cells.Add(bestNext);
                    hex = bestNext;

                    bool CheckHeight(Vector2Int p)//????
                    {
                        var h = Map[p].Height;
                        foreach (var n in CellGeometry.Neighbors(p))
                        if (n != hex && Map[n].Height < h - 5)
                            return false;

                        return true;
                    }
                }

                if (cells.Count - 1 < MinRiverLength)
                    return;

                var river = new River();
                river.Cells = cells;
                rivers.Add(river);
                taken.AddRange(cells);
            }
        }

        private void BuildPathsSegments()
        {
            var neighbors = new List<(Vector2Int n, int iEdge, float height)>();

            foreach (var hex in Map.AllInsideHex())
            {
                var cell = Map[hex];
                if (!cell.Content.HasFlag(CellContent.IsRiver)) continue;

                var center = Builder.HexToPos(hex).withSetY(cell.Height);

                // find neighbors by rivers
                neighbors.Clear();
                for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
                {
                    var n = CellGeometry.Neighbor(hex, iEdge);
                    if (cell.Edges[iEdge].IsRiver)
                    {
                        var nCell = Map[n];
                        var h = (cell.Height * cell.Type.HeightPower + nCell.Height * nCell.Type.HeightPower) / (cell.Type.HeightPower + nCell.Type.HeightPower);
                        neighbors.Add((n, iEdge, h));
                    }
                }

                var r = RiverRadius + TakenAreaPadding;

                if (neighbors.Count == 1)
                {
                    var pair0 = neighbors[0];
                    var p0 = CellGeometry.EdgeCenter(hex, pair0.iEdge).withSetY(pair0.height);
                    var p1 = CellGeometry.Center(hex).withSetY(cell.Height);
                    cell.TakenAreas.Add(new TakenArea(p0.ToVector2(), p1.ToVector2(), r, TakenAreaType.River));
                }

                if (neighbors.Count == 2)
                {
                    var pair0 = neighbors[0];
                    var pair1 = neighbors[1];
                    var p0 = CellGeometry.EdgeCenter(hex, pair0.iEdge).withSetY(pair0.height);
                    var p1 = CellGeometry.EdgeCenter(hex, pair1.iEdge).withSetY(pair1.height);
                    var pp0 = ((p0 + center) / 2).withSetY(center.y);
                    var pp1 = ((p1 + center) / 2).withSetY(center.y);
                    cell.TakenAreas.Add(new TakenArea(p0.ToVector2(), pp0.ToVector2(), r, TakenAreaType.River));
                    cell.TakenAreas.Add(new TakenArea(pp0.ToVector2(), pp1.ToVector2(), r, TakenAreaType.River));
                    cell.TakenAreas.Add(new TakenArea(pp1.ToVector2(), p1.ToVector2(), r, TakenAreaType.River));
                }
            }
        }

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
                    if (!cell.Edges[i].IsRiver)
                        continue;
                    var n = builder.CellGeometry.Neighbor(hex, i);
                    var nCell = map[n];
                    var nCenter = builder.CellGeometry.Center(n).withSetY(nCell.Height + 1);
                    Gizmos.DrawLine(center, (center + nCenter) / 2);
                }
            }

            Gizmos.color = Color.green;

            foreach (var hex in map.AllHex())
                if (map[hex].HasContent(CellContent.IsRiver))
                    Gizmos.DrawSphere(builder.HexToPos(hex) + Vector3.up * 1, 1);
        }
#endif
    }

    [Flags, Serializable]
    public enum RiverSpawnerFeatures
    {
        None = 0x0,
        MakeMesh = 0x40,
        MakeCollider = 0x80,
        SmoothEnds = 0x100
    }
}
