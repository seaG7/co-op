using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// CellSpawner is designed to create objects inside cells: vegetation, bushes, trees, grass, stones, and so on. 
    /// This is the basic spawner type for creating nature.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.6pq85faxze3i")]
    public class CellSpawner : BaseSpawner, IExclusive
    {
        public override int Order => 1500;
        [Tooltip("An arbitrary comment for the spawner.")]
        [SerializeField] string Comment;
        [Tooltip("Spawner semantics. It affects the scaling of objects in the global settings.")]
        [SerializeField] PrefabSemantic Semantic = PrefabSemantic.Undefined;

        [Header("Spawn Conditions")]
        [Tooltip("Allowed distance diapason from center of cell to spawned object. Normalized value from 0 to 1.")]
        [SerializeField, RangeMode(Min = 0, Max = 1)] public RangeFloat DistanceFromCenter = new RangeFloat(0, 1);// normalized from 0 to 1
        [Tooltip("Diapason of surface slope where the spawner can spawn (degrees).")]
        [SerializeField, RangeMode(Min = 0, Max = 90)] public RangeFloat Slope = new RangeFloat(0, 30);
        [Tooltip("List of cell types where spawner is allowed. Use prefix '-' for negative types.")]
        [Popup(nameof(ProposedCellTypes), true)]
        public string[] CellTypes = new string[0];
        [Tooltip("List of allowed neighbor cell types. Use prefix '-' for negative types.")]
        [Popup(nameof(ProposedCellTypes), true)]
        public string[] OppositeCellTypes = new string[0];
        [Tooltip("Allowable formal slope angle between adjacent cells (degrees).")]
        [SerializeField, RangeMode(CanBeOutside = true)] public RangeFloat WallAngle = new RangeFloat(-90, 90);
        [Tooltip("A set of flags that allow a sector/edge to spawn.")]
        public EdgeConditions EdgeConditions;
        [Tooltip("If a spawner with an exclusive group is triggered, then another spawner with the same group will not be able to trigger.")]
        [SerializeField] protected CellSpawnerExclusiveGroups ExclusiveGroups;

        [Header("Spawn")]
        [Tooltip("Number of objects spawned per surface area (on average). Value 1 means one instance per one cell of standard size.")]
        public ParticleSystem.MinMaxCurve Density = 1;// per cell of standard size
        [Tooltip("")]
        [SerializeField] protected AdvancedSpawnSettings AdvancedSpawn = new AdvancedSpawnSettings();

        string IExclusive.ExclusiveGroup => ExclusiveGroups.TerrainExclusiveGroup;
        float IExclusive.Chance => ExclusiveGroups.Chance;

        [Tooltip("Drag and drop here prefab to add to prefab list.")]
        [SerializeField] UnityEngine.Object DragHerePrefabToAddToList;
        [Space]
        public List<Prefab> Prefabs;

        protected float CellHeight;
        protected Vector3 CellCenter;
        protected Cell Cell;
        protected float WaterLevel;
        protected MicroWorld.CellBuildInfo CellBuildInfo;
        protected Vector2Int CellHex;
        protected int EdgeIndex;
        protected List<float> PrefabsProbability;
        protected float Density_fixed;
        protected float scaleBySemantic = 1f;
        protected List<(Prefab, Vector2)> newExclusiveAreas = new List<(Prefab, Vector2)>();
        protected bool hasExclEdge;
        protected bool hasExclGroup;

        public override IEnumerator Prepare(MicroWorld builder)
        {
            yield return base.Prepare(builder);

            // calculate variable params
            FixVariableParams();

            // calc global scale
            switch (Semantic)
            {
                case PrefabSemantic.Plant: scaleBySemantic = Preferences.Instance.ScalePlants; break;
                case PrefabSemantic.Bush: scaleBySemantic = Preferences.Instance.ScaleBushes; break;
                case PrefabSemantic.Rock: scaleBySemantic = Preferences.Instance.ScaleRocks; break;
                case PrefabSemantic.Tree: scaleBySemantic = Preferences.Instance.ScaleTrees; break;
                case PrefabSemantic.Stick: scaleBySemantic = Preferences.Instance.ScaleSticks; break;
                default:
                    scaleBySemantic = 1f;
                    break;
            }
        }

        private void FixVariableParams()
        {
            var minMaxCurveRnd = rootRnd.GetBranch(8336);

            Density_fixed = Density.Value(minMaxCurveRnd);
        }

        [Serializable]
        public class AdvancedSpawnSettings
        {
            [Tooltip("Probability of spawning for a cell.")]
            [Range(0f, 1f)]
            public float ChanceInCell = 1;

            [Tooltip("Probability of spawning for a sector of cell.")]
            [Range(0f, 1f)]
            public float ChanceInSector = 1;

            [Tooltip("How many objects can be spawned per cell - min and max.")]
            [RangeMode(Min = 0, Max = 1000)]
            public RangeInt SpawnLimitsPerCell = new RangeInt(0, 1000);

            //public float ChancePower { get; set; } = 1;

            [Tooltip("A set of flags for fine-tuning spawning.")]
            public SpawnFeatures Features;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (DragHerePrefabToAddToList != null)
                AddPrefab(DragHerePrefabToAddToList);
            DragHerePrefabToAddToList = null;

            if (Prefabs != null && Prefabs.Count == 1)
            {
                var p = Prefabs[0];
                if (p.Enabled == false && p.Chance == 0 && p.Scale == 0f && p.prefab == null)
                    Prefabs[0] = new Prefab();
            }

            if (Prefabs != null)
            foreach (var prefab in Prefabs)
                prefab.OnValidate(this);

            Density = Density.Clamp(0, 10000000);

            //EditorUtility.SetDirty(this);
        }
#endif

        public IEnumerator Prepare(MicroWorld builder, Rnd rnd)
        {
            yield return Prefabs.Prepare(builder, rnd);
            PrefabsProbability = Prefabs.Select(p => p.ProbabilityInSet).ToList();
        }

        public IEnumerator BuildCell(MicroWorld builder, MicroWorld.CellBuildInfo info)
        {
            yield return null;

            CheckMapSpawner();
            CheckTerrainSpawner();

            CellBuildInfo = info;

            CellHex = info.Hex;
            Cell = Map[info.Hex];
            var cellRnd = rootRnd.GetBranch(info.Hex.GetHashCode());
            CellHeight = Cell.Height;
            CellCenter = CellGeometry.Center(info.Hex).withSetY(CellHeight);
            WaterLevel = builder.TerrainSpawner.WaterLevel;

            BuildCell(cellRnd);
        }

        private void BuildCell(Rnd rnd)
        {
            hasExclEdge = !string.IsNullOrWhiteSpace(ExclusiveGroups.SectorExclusiveGroup);
            hasExclGroup = !string.IsNullOrWhiteSpace(ExclusiveGroups.CellExclusiveGroup);

            if (!CheckCellConditions(rnd))
                return;

            var haltonCache = HaltonSequence.Instance.HaltonCache;
            var haltonCacheCounter = rnd.Int(200);

            // calc allowed edges
            var allowedEdges = new bool[CellGeometry.CornersCount];
            var allowedEdgesCount = 0;
            for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
            {
                if (!rnd.Bool(AdvancedSpawn.ChanceInSector))
                    continue;

                // checkn wall angle
                if (!WallAngle.InRange(Cell.Edges[iEdge].WallAngle))
                    continue;

                // check edge excl group
                if (hasExclEdge && CellBuildInfo.TakenExclusiveSectors.Contains((iEdge, ExclusiveGroups.SectorExclusiveGroup)))
                    continue;

                // check edge conditions
                var opposite = CellGeometry.Neighbor(CellHex, iEdge);
                if (!OppositeCellTypes.CheckCellType(Map[opposite].Type.Name))
                    continue;

                if (!CheckEdgeConditions(rnd.GetBranch(iEdge), iEdge))
                    continue;

                allowedEdges[iEdge] = true;
                allowedEdgesCount++;
            }

            if (allowedEdgesCount == 0)
                return;

            var spawnedPerCell = 0;
            var edgeIndex = new int[CellGeometry.CornersCount];
            for (int i = 0; i < edgeIndex.Length; i++) edgeIndex[i] = i;
            rnd.ShuffleFisherYates(edgeIndex);
            var startHalton = rnd.Int(100);
            Prefab prevPrefab = null;
            var spawnedEdges = new bool[CellGeometry.CornersCount];

            foreach (var iEdge in edgeIndex)
            {
                EdgeIndex = iEdge;

                if (!allowedEdges[iEdge])
                    continue;

                var E0 = CellGeometry.Corner(iEdge);
                var E1 = CellGeometry.Corner(iEdge + 1);

                // calc spawan count per sector
                var avgCountPerSector = Density_fixed * Builder.DensityScale / CellGeometry.CornersCount;// / allowedEdgesCount
                var countPerSector = rnd.Poisson(avgCountPerSector);

                if (countPerSector < 1 && AdvancedSpawn.SpawnLimitsPerCell.Min < 1) 
                    continue; 

                Prefab prefab = default;
                Vector3 lastPos = Vector3.zero;
                if (AdvancedSpawn.SpawnLimitsPerCell.Min > 0)
                    countPerSector = Mathf.Max(countPerSector, Mathf.CeilToInt(AdvancedSpawn.SpawnLimitsPerCell.Min / 6f));

                var counter = Mathf.Clamp(countPerSector * 3, 10, 100);

                var uniformDistr = !AdvancedSpawn.Features.HasFlag(SpawnFeatures.RandomDistribution);
                var isTooSmallDistanceInterval = Mathf.Abs(DistanceFromCenter.Max - DistanceFromCenter.Min) < 0.05f;
                if (isTooSmallDistanceInterval)
                    uniformDistr = false;
                if (AdvancedSpawn.Features.HasFlag(SpawnFeatures.SpawnOnCentralSectorLine))
                    uniformDistr = false;

                // spawn in edge
                var randomPoints = uniformDistr ? HaltonSequence.Instance.HaltonSequenceByEdges(CellGeometry.CornersCount)[iEdge].Skip(startHalton) : GetRandomPointsInSector(rnd.GetBranch(37747), iEdge);
                foreach (var pp in randomPoints)
                {
                    var p = pp;
                    p.Pos *= CellGeometry.Radius;// from normalized to actual cell size

                    if (countPerSector <= 0)
                        break;
                    if (counter-- < 0)
                        break;

                    if (spawnedPerCell >= AdvancedSpawn.SpawnLimitsPerCell.Max)
                        break;

                    if (CellBuildInfo.TakenPoints.Contains(p.Pos))
                        continue;

                    if (isTooSmallDistanceInterval)
                    {
                        var e = Vector3.Lerp(E0, E1, rnd.Float());
                        var dist = (DistanceFromCenter.Min + DistanceFromCenter.Max) / 2;
                        p.DistanceToEdge = 1 - dist;
                        p.Pos = Vector3.Lerp(Vector3.zero, e, dist).ToVector2();
                    }

                    if (AdvancedSpawn.Features.HasFlag(SpawnFeatures.SpawnOnCentralSectorLine))
                    {
                        var dist = rnd.Float(DistanceFromCenter.Min, DistanceFromCenter.Max);
                        p.DistanceToEdge = 1 - dist;
                        var dir = CellGeometry.EdgeNormal(iEdge).ToVector2();
                        p.Pos = dir * dist * CellGeometry.InnerRadius;
                    }

                    if (AdvancedSpawn.Features.HasFlag(SpawnFeatures.SpawnOnRoad))
                    {
                        //select rnd segment
                        var t = haltonCache[(haltonCacheCounter++) % haltonCache.Count];
                        var segment = rnd.GetRnd(CellBuildInfo.RoadSegments);
                        var a = segment.A - CellCenter.ToVector2();
                        var b = segment.B - CellCenter.ToVector2();
                        var dist = t.x.Remap(0, 1, DistanceFromCenter.Min, DistanceFromCenter.Max);
                        p.DistanceToEdge = 1 - dist;
                        var normal = (b - a).Rotate(90 * (rnd.Bool(0.5f) ? 1 : -1)).normalized;
                        p.Pos = Vector2.LerpUnclamped(a, b, t.y) + dist * normal * segment.Radius;
                    }

                    if (!DistanceFromCenter.InRange(1 - p.DistanceToEdge))
                        continue;

                    // get rnd prefab to spawn
                    if (prevPrefab == null || !rnd.Bool(prevPrefab.RepeatChance))// if no repeat prev prefab
                    {
                        // get rnd prefab
                        prefab = rnd.GetRnd(Prefabs, PrefabsProbability);
                        if (prefab == null)
                            continue;
                        prefab.SelectGO(rnd);
                    }
                    else
                    {
                        prefab = prevPrefab;
                        if (prefab.Features.HasFlag(PrefabSpawnFeatures.GenRepeatDifference))
                            prefab.SelectGO(rnd);
                    }

                    var pos = p.Pos.ToVector3() + CellCenter;
                    pos.y = SampleHeight(pos);

                    // check water condition
                    if (!prefab.HeightAboveWater.InRange(pos.y - WaterLevel))
                        continue;

                    if (prefab.Features.HasFlag(PrefabSpawnFeatures.SpawnAtCellLevel))
                        pos.y = CellHeight;
                    else
                    if (prefab.Features.HasFlag(PrefabSpawnFeatures.LiftToWaterLevel))
                        pos.y = Mathf.Max(pos.y, WaterLevel);
                        

                    // check slope condition
                    var slope = Builder.TerrainSpawner.GetSlopeAngle(pos);
                    if (!Slope.InRange(slope))
                        continue;

                    // check taken areas
                    if (!CheckExclusiveArea(prefab, pos.ToVector2()))
                        continue;

                    // check exclusive groups
                    if (prefab.ExclusiveGroups.CellExclusiveGroup.NotNullOrEmpty())
                    if (CellBuildInfo.TakenExclusiveGroups.Contains(prefab.ExclusiveGroups.CellExclusiveGroup))
                        continue;

                    if (prefab.ExclusiveGroups.SectorExclusiveGroup.NotNullOrEmpty())
                    if (CellBuildInfo.TakenExclusiveSectors.Contains((iEdge, prefab.ExclusiveGroups.SectorExclusiveGroup)))
                        continue;

                    if (prefab.ExclusiveGroups.TerrainExclusiveGroup.NotNullOrEmpty())
                    if (Builder.TakenTerrainExclusiveGroups.Contains(prefab.ExclusiveGroups.TerrainExclusiveGroup))
                        continue;

                    // spawn
                    Spawn(prefab, pos, rnd.GetBranch(countPerSector * 3 + iEdge * 5, 2836671));

                    // take point
                    CellBuildInfo.TakenPoints.Add(p.Pos);
                    prevPrefab = prefab;

                    //
                    spawnedEdges[iEdge] = true;
                    countPerSector--;
                    spawnedPerCell++;
                }
            }

            if (hasExclGroup)
                CellBuildInfo.TakenExclusiveGroups.Add(ExclusiveGroups.CellExclusiveGroup);

            if (hasExclEdge)
            for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
            if (spawnedEdges[iEdge])
                CellBuildInfo.TakenExclusiveSectors.Add((iEdge, ExclusiveGroups.SectorExclusiveGroup));
        }

        IEnumerable<PointInCell> GetRandomPointsInSector(Rnd rnd, int iEdge)
        {
            while (true)
            {
                var x = rnd.Float(-1, 1);
                var y = rnd.Float(-1, 1);
                var pos = new Vector2(x, y);

                var pos3d = (pos * CellGeometry.Radius).ToVector3();
                var dist = CellGeometry.SignedDistance(pos3d);
                if (dist >= 0)
                {
                    if (CellGeometry.PointToEdge(pos3d) == iEdge)
                        yield return new PointInCell { Pos = pos, DistanceToEdge = dist / CellGeometry.InnerRadius };
                }
            }
        }

        protected virtual bool CheckEdgeConditions(Rnd rnd, int iEdge)
        {
            if (EdgeConditions == 0)
                 return true;

            var edge = Cell.Edges[iEdge];
            var n = CellGeometry.Neighbor(CellBuildInfo.Hex, iEdge);
            var nCell = Map[n];

            if (EdgeConditions.HasFlag(EdgeConditions.NotWalkable) && edge.IsWalkable)
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.Walkable) && !edge.IsWalkable)
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.NoRoad) && edge.IsRoad)
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.Road) && !edge.IsRoad)
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.NoPassage) && (nCell.Parent == CellHex || Cell.Parent == n))
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.Passage) && (nCell.Parent != CellHex && Cell.Parent != n))
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.Entrance) && Cell.Parent != n)
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.Exit) && nCell.Parent != CellHex)
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.OtherCellType) && nCell.Type == Cell.Type)
                return false;

            if (EdgeConditions.HasFlag(EdgeConditions.SameCellType) && nCell.Type != Cell.Type)
                return false;

            return true;
        }

        protected virtual bool CheckCellConditions(Rnd rnd)
        {
            // check chance
            if (!rnd.Bool(AdvancedSpawn.ChanceInCell))
                return false;

            // check cell type
            if (!CellTypes.CheckCellType(Cell.Type.Name))
                return false;

            // check border cells
            if (Map.IsBorderOrOutside(CellHex))
            if (!AdvancedSpawn.Features.HasFlag(SpawnFeatures.AllowSpawnOnBorderCells))
                return false;

            // check cell excl group
            if (hasExclGroup && CellBuildInfo.TakenExclusiveGroups.Contains(ExclusiveGroups.CellExclusiveGroup))
                return false;

            // spawn on roads?
            if (CellBuildInfo.RoadSegments.Count == 0 && AdvancedSpawn.Features.HasFlag(SpawnFeatures.SpawnOnRoad))
                return false;

            return true;
        }

        /// <summary> Returns True is prefab has no intersections in the position </summary>
        private bool CheckExclusiveArea(Prefab prefab, Vector2 pos2d)
        {
            // SpawnOnRoad means that it is road surface and we do not need to check any intersections
            if (AdvancedSpawn.Features.HasFlag(SpawnFeatures.SpawnOnRoad))
                return true;

            if (prefab.NeedToCheckIntersections)
            {
                // check intersection with any type of collider
                return !CellBuildInfo.TakenAreas.HasIntersection(pos2d, prefab.ExclusiveRadius);
            }else
            {
                // check intersection with road or river only
                return !CellBuildInfo.TakenAreas.Where(t => t.Type != TakenAreaType.Object).HasIntersection(pos2d, prefab.ExclusiveRadius);
            }
        }

        private void Spawn(Prefab prefab, Vector3 initPos, Rnd rnd)
        {
            // calc count of Bunching
            var count = prefab.Bunch == 1f ? 1 : rnd.Poisson(prefab.Bunch);
            if (count < 1) count = 1;
            // spawn needed count
            var startHalton = rnd.Int(1283);
            var wasSpawned = false;
            var assignActualY = !(prefab.Features.HasFlag(PrefabSpawnFeatures.SpawnAtCellLevel) || prefab.Features.HasFlag(PrefabSpawnFeatures.LiftToWaterLevel));
            newExclusiveAreas.Clear();

            // spawn bunch
            foreach (var dp in HaltonSequence.Instance.GetHaltonInCircle(startHalton).Take(count))
            {
                var pos = initPos;
                var distToCenter = dp.magnitude;
                var addScale = scaleBySemantic;

                // bunching?
                if (count > 1)
                {
                    if (prefab.BunchGravity == 1f)
                        pos += dp.ToVector3() * prefab.BunchRadius;
                    else
                    {
                        // make non linear distribution
                        var newDistToCenter = Mathf.Pow(distToCenter, prefab.BunchGravity);
                        var newDP = dp / distToCenter * newDistToCenter;
                        distToCenter = newDistToCenter;
                        pos += newDP.ToVector3() * prefab.BunchRadius;
                    }

                    // check taken areas
                    if (!CheckExclusiveArea(prefab, pos.ToVector2()))
                        continue;

                    // bunch scale
                    addScale *= 1 - distToCenter * (1 - prefab.BunchPeripheryScale);
                }

                if (assignActualY)
                    pos.y = SampleHeight(pos);

                var slope = Builder.TerrainSpawner.GetSlopeAngle(pos);
                if (!Slope.InRange(slope))
                    continue;

                prefab.Spawn(Builder, pos, -CellGeometry.EdgeNormal(EdgeIndex), addScale, rnd);
                ApplySpawnMetadata(prefab);

                SetExclusiveGroups(prefab);
                newExclusiveAreas.Add((prefab, pos.ToVector2()));// remember exclusive area
                if (prefab.ExclusiveRadius > float.Epsilon)
                    CellBuildInfo.TakenAreas.Add(new TakenArea(pos.ToVector2(), prefab.ExclusiveRadius));
                wasSpawned = true;
            }

            // add exclusive areas (after whole bunch is spawned)
            if (prefab.ExclusiveRadius > float.Epsilon)
            foreach (var pair in newExclusiveAreas)
                CellBuildInfo.TakenAreas.Add(new TakenArea(pair.Item2, prefab.ExclusiveRadius));

            // spawn add prefab
            if (wasSpawned)
            if (prefab.Features.HasFlag(PrefabSpawnFeatures.OneAddPrefabPerBunch))
            {
                var pos = initPos;
                if (assignActualY)
                    pos.y = SampleHeight(pos);
                prefab.SpawnAddGameObject(Builder, pos, -CellGeometry.EdgeNormal(EdgeIndex), rnd);
                ApplySpawnMetadata(prefab);
            }
        }

        private void ApplySpawnMetadata(Prefab prefab)
        {
            ApplySpawnMetadata(prefab.SpawnedGO);
            ApplySpawnMetadata(prefab.SpawnedAddGO);
        }

        private void ApplySpawnMetadata(GameObject spawnedObject)
        {
            SpawnedObjectMetadataUtility.Apply(
                spawnedObject,
                transform,
                gameObject.name,
                ExclusiveGroups?.TerrainExclusiveGroup ?? string.Empty,
                Semantic.ToString());
        }

        private void SetExclusiveGroups(Prefab prefab)
        {
            // set exclusive groups
            if (prefab.ExclusiveGroups.CellExclusiveGroup.NotNullOrEmpty())
                CellBuildInfo.TakenExclusiveGroups.Add(prefab.ExclusiveGroups.CellExclusiveGroup);

            if (prefab.ExclusiveGroups.SectorExclusiveGroup.NotNullOrEmpty())
                CellBuildInfo.TakenExclusiveSectors.Add((EdgeIndex, prefab.ExclusiveGroups.SectorExclusiveGroup));

            if (prefab.ExclusiveGroups.TerrainExclusiveGroup.NotNullOrEmpty())
                Builder.TakenTerrainExclusiveGroups.Add(prefab.ExclusiveGroups.TerrainExclusiveGroup);
        }

        protected void AddPrefab(UnityEngine.Object prefab)
        {
            var orig = Prefabs.LastOrDefault();
            if (orig == null)
                orig = new Prefab();
            else
                //orig = orig.CloneViaFakeSerialization();
                orig = orig.Clone();

            orig.prefab = prefab;

            Prefabs.Add(orig);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [Serializable]
    enum PrefabSemantic : byte
    {
        Undefined = 0, Grass = 1, Plant = 2, Bush = 3, Stick = 4, Tree = 6, Rock = 10, Road = 11
    }

    [Serializable]
    public class CellSpawnerExclusiveGroups
    {
        [Tooltip("Exclusive group for the whole terrain.")]
        public string TerrainExclusiveGroup = "";
        [ShowIf(nameof(ShowChance))]
        public float Chance = 1;
        [Tooltip("Exclusive group for the cell.")]
        public string CellExclusiveGroup = "";
        [Tooltip("Exclusive group for the sector.")]
        public string SectorExclusiveGroup = "";

        bool ShowChance => !string.IsNullOrEmpty(TerrainExclusiveGroup);
    }

    [Flags]
    public enum EdgeConditions
    {
        None = 0,
        OtherCellType = 0x1,
        SameCellType = 0x2,
        NoPassage = 0x4,
        Passage = 0x8,
        Entrance = 0x10,
        Exit = 0x20,
        NotWalkable = 0x40,
        Walkable = 0x80,
        Road = 0x100,
        NoRoad = 0x200,
    }

    [Flags]
    public enum SpawnFeatures
    {
        None = 0,
        SpawnOnCentralSectorLine = 0x1,
        AllowSpawnOnBorderCells = 0x2,
        RandomDistribution = 0x4,
        SpawnOnRoad = 0x8,
    }
}
