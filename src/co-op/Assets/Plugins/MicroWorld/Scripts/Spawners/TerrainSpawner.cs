using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.Serialization;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// TerrainSpawner is designed to create a height map and terrain
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.ca6qg51dymb2")]
    public class TerrainSpawner : BaseSpawner, IExclusive
    {
        public string ExclusiveGroup => "TerrainSpawner";
        [field: SerializeField]
        [field: Tooltip("Defines the chance of selecting this spawner among all TerrainSpawners of MicroWorld instance.")] 
        public float Chance { get; set; } = 1;
        public override int Order => 500;

        [Tooltip("Terrain prefab used to create new terrain. Can be null.")]
        [SerializeField] Terrain TerrainPrefab;
        [Tooltip("Water prefab used to create a new water plane. Can be null.")]
        [SerializeField] GameObject WaterPrefab;

        [Header("Landscape")]
        [Tooltip("Steepness of slopes between cells. Typical value - from 1.5 (smoothed borders) to 6 (sharp borders).")]
        [SerializeField] public ParticleSystem.MinMaxCurve SlopeSteepness = 3;
        [Tooltip("Steepness of slopes between connected cells. Typical value - from 1.5 (smoothed) to 6 (sharp).")]
        [SerializeField, UnityEngine.Min(1)] public float SlopeSteepnessInPassages = 2;
        [Tooltip("The probability that two adjacent cells will have the same height.")]
        [SerializeField] ParticleSystem.MinMaxCurve FlatChance = 0f;
        [Tooltip("Smooths cell heights. Typical value - from 0 (no smoothing) to 7 (cell heights are significantly smoothed). Use this parameter if you want to reduce the variation in heights across the cells.")]
        [SerializeField] ParticleSystem.MinMaxCurve Smoothing = 0;
        [Tooltip("Maximum height of wall between cells on passage sector (meters).")]
        [SerializeField] float MaxStepForPassage = 2;
        [Tooltip("Alternative method to calculate heights.")]
        [SerializeField] bool ClampHeight = false;
        [Tooltip("Do not connect cells.")]
        [SerializeField] bool NoPaths;

        [Header("Elevation")]
        [Tooltip("Elevates the central part of the terrain (meters). This value can be negative and positive.")]
        [SerializeField] ParticleSystem.MinMaxCurve CenterElevation = 0f;
        [Tooltip("Lowers or raises the peripheral parts of the terrain.")]
        [SerializeField] float PeripheryElevation = 0;
        [Tooltip("Periphery size for PeripheryElevation (cells).")]
        [SerializeField, UnityEngine.Min(0f)] float PeripherySize = 2;

        [Header("Main Noise")]
        [Tooltip("Noise frequency of Main Noise.")]
        [SerializeField] ParticleSystem.MinMaxCurve MainNoiseFrequency = 0.3f;
        [Tooltip("Noise amplitude of Main Noise (meters).")]
        [SerializeField] ParticleSystem.MinMaxCurve MainNoiseAmpl = 30f;

        [Header("Mid Noise")]
        [Tooltip("Amplitude of Mid Noise (meters).")]
        [SerializeField] ParticleSystem.MinMaxCurve MidNoiseAmpl = 15f;

        [Header("Micro Noise")]
        [Tooltip("Noise frequency of Micro Noise.")]
        [SerializeField] ParticleSystem.MinMaxCurve MicroNoiseFrequency = 5f;
        [Tooltip("Noise amplitude of Main Noise (meters).")]
        [SerializeField] ParticleSystem.MinMaxCurve MicroNoiseAmpl = 1f;
        [Tooltip("Normalized offset of noise by Y. Typical values ​​are 0 or 0.5.")]
        [SerializeField] float MicroNoiseOffsetY = 0f;

        [Header("Slope Roughness")]
        [Tooltip("Normalized amplitude of noise on slopes, from 0 to 2.")]
        [SerializeField, Range(0, 2f)] float SlopeRoughness = 0.8f;
        [Tooltip("Normalized noise amplitude at small elevation differences between cells, from 0 to 2.")]
        [SerializeField, Range(0, 2)] float SmallSlopeRoughness = 1f;
        [Tooltip("Noise frequency.")]
        [SerializeField, Range(2f, 25f)] float SlopeRoughnessFrequency = 15f;

        [Header("Water")]
        [Tooltip("Degree of coverage of the terrain area with water, from 0 (no water) to 1 (whole terrain covered by water).")]
        [SerializeField] ParticleSystem.MinMaxCurve WaterCovering = 0.10f;
        [Tooltip("Maximum depth of water body (meters).")]
        [SerializeField] public float MaxDepthUnderWater = 10;

        [Header("Coast")]
        [Tooltip("Width of the coast area.")]
        [SerializeField, Range(0, 20)] float CoastWidth = 2;
        [Tooltip("Flatness of coast area. A greater value makes the shore lower and smoother.")]
        [SerializeField, Range(0, 5)] float CoastFlatness = 2f;
        [Tooltip("Additional elevation of the coastline above the water level (meters).")]
        [SerializeField, Range(-1, 1)] float CoastElevation = 0.1f;

        [Header("Border Cells")]
        [Tooltip("Y offset of border cell relative to adjacent inside cells (meters).")]
        [SerializeField] public float BorderElevation = 5;
        [SerializeField] public BorderFeatures BorderFeatures = BorderFeatures.LiftUpGatesToWaterLevel | BorderFeatures.Reserved0;

        [Header("Valley/River")]
        [Tooltip("Width of valley (cells).")]
        [SerializeField] ParticleSystem.MinMaxCurve ValleyWidth = 0;
        [Tooltip("Valley elevation (meters). Positive value creates ridge, negative - valley or river.")]
        [SerializeField, FormerlySerializedAs("ValleyOffsetY")] ParticleSystem.MinMaxCurve ValleyElevation = -5;

        private void OnValidate()
        {
            SlopeSteepness = SlopeSteepness.Clamp(1.5f, 50);
            FlatChance = FlatChance.Clamp(0, 1);
            Smoothing = Smoothing.ClampInt(0, 7);
            ValleyWidth = ValleyWidth.ClampInt(0, 4);
            MainNoiseFrequency = MainNoiseFrequency.Clamp(0, 5);
            MainNoiseAmpl = MainNoiseAmpl.Clamp(0, 40);
            MidNoiseAmpl = MidNoiseAmpl.Clamp(0, 30);
            MicroNoiseFrequency = MicroNoiseFrequency.Clamp(1, 7);
            MicroNoiseAmpl = MicroNoiseAmpl.Clamp(0, 3);
            WaterCovering = WaterCovering.Clamp(0, 1);
        }

        public float WaterLevel { get; private set; }

        float minCellH;
        float maxCellH;
        float StepY => 2 + SlopeSteepness_fixed;
        int seed => Builder.Seed;
        Rnd rnd;
        internal Texture2D Slopes;
        GameObject waterObj;
        public Transform TempHolder { get; private set; }
        MapSpawner mapSpawner => Builder.MapSpawner;
        Dictionary<Vector2Int, int> flattenAreas;
        Vector3 terrainSize;
        [NonSerialized] public float[,] HeightMap;

        public override IEnumerator Build(MicroWorld builder)
        {
            if (Preferences.Instance.LogFeatures.HasFlag(LogFeatures.LogSeed))
                Debug.Log("Terrain Spawner: " + name + " Seed: " + builder.Seed);

            yield return base.Build(builder);

            CheckMapSpawner();

            // here map is completely created, call phase CellTypesCreated
            yield return Builder.OnPhaseCompleted(BuildPhase.MapCreated);

            builder.TerrainSpawner = this;

            rnd = rootRnd.GetBranch(943872);
            // calculate variable params
            FixVariableParams();

            // prepare
            if (!TerrainPrefab)
                TerrainPrefab = Resources.Load<Terrain>("Terrain");

            if (!WaterPrefab)
                WaterPrefab = Resources.Load<GameObject>("Water");

            // execute spawners after cell types are created but before terrain spawner start
            yield return Builder.OnPhaseCompleted(BuildPhase.StartTerrainSpawner);

            // find Flatten Areas
            FindFlattenAreas();

            // build cells height
            float buildCellHeightsStartedAt = Time.realtimeSinceStartup;
            BuildCellHeights();
            Debug.Log($"[TerrainSpawner] BuildCellHeights done in {Time.realtimeSinceStartup - buildCellHeightsStartedAt:0.00}s");

            yield return null;

            // set water level
            SetWaterLevel();

            // under water cell type -> Water Cell type
            ChangeUnderWaterCelltype();

            // calc border cells height
            CalcBorderCellsHeight();

            // lift up cells to water level
            LiftUpCellsToWaterLevel();

            // apply coast height changes to formal cell height
            MakeCoast();

            // execute spawners after cell heights are created
            yield return Builder.OnPhaseCompleted(BuildPhase.CellHeightsCreated);

            // calc wall heights and angle on cell edges
            CalcFormalSlopeAngles();

            // execute spawners after finally heights are created, but before terrain created
            yield return Builder.OnPhaseCompleted(BuildPhase.BeforeTerrainBuilding);

            // <--- here cell height are final

            // create terrain
            float buildTerrainStartedAt = Time.realtimeSinceStartup;
            yield return BuildTerrain();
            Debug.Log($"[TerrainSpawner] BuildTerrain done in {Time.realtimeSinceStartup - buildTerrainStartedAt:0.00}s");

            // set water position
            if (WaterPrefab && WaterCovering_fixed > float.Epsilon)
            {
                waterObj = Instantiate(WaterPrefab, Terrain.transform).gameObject;
                waterObj.transform.position = (Terrain.terrainData.size / 2).withSetY(WaterLevel);
            }

            // execute spawners after cell types are created
            yield return Builder.OnPhaseCompleted(BuildPhase.TerrainCreated);
            Debug.Log("[TerrainSpawner] TerrainCreated phase completed");
        }

        private void CalcFormalSlopeAngles()
        {
            var edgesCount = CellGeometry.CornersCount;

            // clear isProcessed flag
            foreach (var hex in Map.AllHex())
            {
                var cell = Map[hex];
                for (int iEdge = 0; iEdge < edgesCount; iEdge++)
                    cell.Edges[iEdge].isProcessed = false;
            }

            foreach (var hex in Map.AllHex())
            {
                var cell = Map[hex];

                for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
                {
                    var edge = cell.Edges[iEdge];
                    if (edge.isProcessed)
                        continue;

                    var n = CellGeometry.Neighbor(hex, iEdge);
                    var nCell = Map[n];

                    // calc wall height
                    edge.WallHeight = nCell.Height - cell.Height;
                    edge.IsCellTypeBorder = nCell.Type != cell.Type;
                    edge.IsPassage = cell.Parent == n || nCell.Parent == hex;

                    // go along central line and calc max angle
                    var count = Mathf.CeilToInt(CellGeometry.InnerRadius);
                    count += count % 2;

                    var prevH = 0f;
                    var maxAbsDeltaH = -1f;
                    var maxDeltaH = 0f;
                    for (int i = 0; i < count; i++)
                    {
                        var t = i / (count - 1f);
                        var h = GetFormalHeightBetweenCells(hex, iEdge, t);
                        if (i > 0)
                        {
                            var dH = h - prevH;
                            var absDeltaH = Mathf.Abs(dH);
                            if (absDeltaH > maxAbsDeltaH)
                            {
                                maxAbsDeltaH = absDeltaH;
                                maxDeltaH = dH;
                            }
                        }

                        prevH = h;
                    }

                    // clac slope angle
                    var dx = CellGeometry.InnerRadius * 2 / (count - 1);
                    edge.WallAngle = Mathf.Atan2(maxDeltaH, dx) * Mathf.Rad2Deg;

                    //
                    edge.isProcessed = true;
                    cell.Edges[iEdge] = edge;

                    // copy to opposite cell edge
                    var opEdge = CellGeometry.OppositeCorner(iEdge);
                    var nEdge = nCell.Edges[opEdge];
                    nEdge.WallHeight = -edge.WallHeight;
                    nEdge.IsCellTypeBorder = edge.IsCellTypeBorder;
                    nEdge.WallAngle = -edge.WallAngle;
                    nEdge.IsPassage = edge.IsPassage;
                    nEdge.isProcessed = true;
                    
                    nCell.Edges[opEdge] = nEdge;
                }
            }
        }

        private void ChangeUnderWaterCelltype()
        {
            // under water cell type -> Water Cell type
            foreach (var hex in Map.AllInsideHex())
                if (Map[hex].Height <= Builder.WaterLevel)
                    Map[hex].Type = mapSpawner.WaterCellType;
        }

        private void SetWaterLevel()
        {
            // set water level  (calc percentile)
            var sortedCellsByHeight = Map.AllInsideHex().OrderBy(p => Map[p].Height).ToList();
            var index = (int)(WaterCovering_fixed * sortedCellsByHeight.Count);
            index = Mathf.Clamp(index, 0, sortedCellsByHeight.Count - 1);
            WaterLevel = Map[sortedCellsByHeight[index]].Height - 0.1f;
            if (WaterCovering_fixed <= float.Epsilon)
                WaterLevel = minCellH - 3;
            Builder.WaterLevel = WaterLevel;
        }

        private void FindFlattenAreas()
        {
            flattenAreas = new Dictionary<Vector2Int, int>();

            // build flatten areas from cell type
            var processed = new HashSet<Vector2Int>();
            foreach (var hex in Map.AllInsideHex())
            {
                if (!processed.Add(hex))
                    continue;
                if (!Map[hex].FlattenArea)
                    continue;

                var queue = new Queue<Vector2Int>();
                queue.Enqueue(hex);
                var type = Map[hex].Type;
                var id = flattenAreas.Count;
                flattenAreas[hex] = id;

                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    foreach (var n in CellGeometry.Neighbors(p).Where(n => !Map.IsBorderOrOutside(n) && Map[n].Type == type))
                    if (processed.Add(n))
                    {
                        queue.Enqueue(n);
                        flattenAreas[n] = id;
                    }
                }
            }
        }

        #region Fix variable params

        float SlopeSteepness_fixed;
        float FlatChance_fixed;
        float MainNoiseFrequency_fixed;
        float MainNoiseAmpl_fixed;
        float MidNoiseAmpl_fixed;
        float MicroNoiseFrequency_fixed;
        float MicroNoiseAmpl_fixed;
        int ValleyWidth_fixed;
        float CenterElevation_fixed;
        float ValleyOffsetY_fixed;
        int Smoothing_fixed;
        float WaterCovering_fixed;

        private void FixVariableParams()
        {
            var minMaxCurveRnd = rootRnd.GetBranch(487454);

            SlopeSteepness_fixed = SlopeSteepness.Value(minMaxCurveRnd);
            FlatChance_fixed = FlatChance.Value(minMaxCurveRnd);
            MainNoiseFrequency_fixed = MainNoiseFrequency.Value(minMaxCurveRnd);
            MainNoiseAmpl_fixed = MainNoiseAmpl.Value(minMaxCurveRnd) * Builder.LandscapeHeightScale;
            MidNoiseAmpl_fixed = MidNoiseAmpl.Value(minMaxCurveRnd) * Builder.LandscapeHeightScale;
            MicroNoiseFrequency_fixed = MicroNoiseFrequency.Value(minMaxCurveRnd);
            MicroNoiseAmpl_fixed = MicroNoiseAmpl.Value(minMaxCurveRnd);
            ValleyWidth_fixed = ValleyWidth.IntValue(minMaxCurveRnd);
            CenterElevation_fixed = CenterElevation.Value(minMaxCurveRnd) * Builder.LandscapeHeightScale;
            ValleyOffsetY_fixed = ValleyElevation.Value(minMaxCurveRnd) * Builder.LandscapeHeightScale;
            Smoothing_fixed = Smoothing.IntValue(minMaxCurveRnd);
            WaterCovering_fixed = WaterCovering.Value(minMaxCurveRnd);
        }
        #endregion

        private void LiftUpCellsToWaterLevel()
        {
            var depth = WaterLevel - minCellH;
            var tragetMinH = depth > MaxDepthUnderWater ? WaterLevel - MaxDepthUnderWater : minCellH;

            foreach (var hex in Map.AllHex())
            {
                var cell = Map[hex];
                if (cell.Type == mapSpawner.GateCellType && BorderFeatures.HasFlag(BorderFeatures.LiftUpGatesToWaterLevel))
                {
                    LiftUpCellToWaterLevel(cell);
                    continue;
                }

                if (cell.Type == mapSpawner.BorderCellType && BorderFeatures.HasFlag(BorderFeatures.LiftUpBordersToWaterLevel))
                {
                    if (cell.Height <= WaterLevel && BorderElevation >= 0 && !BorderFeatures.HasFlag(BorderFeatures.AbsoluteHeight))
                        cell.Height = WaterLevel + BorderElevation + DefaultHeightAboveWater;
                    continue;
                }

                if (cell.LiftUpToWaterLevel)
                {
                    LiftUpCellToWaterLevel(cell);
                    continue;
                }

                if (cell.Height < WaterLevel)
                    cell.Height = Helper.Remap(cell.Height, minCellH, WaterLevel, tragetMinH, WaterLevel);
                else
                    cell.Height += DefaultHeightAboveWater;
            }
        }

        private void MakeCoast()
        {
            // make coast formal cell height
            const float steepness = 1.15f;
            foreach (var hex in Map.AllInsideHex())
            {
                var h = Map[hex].Height;
                Map[hex].Height = Coast(h, CoastWidth, steepness);
            }
        }

        const float DefaultHeightAboveWater = 0.5f;

        public void LiftUpCellToWaterLevel(Cell cell, float heightAboveWater = DefaultHeightAboveWater)
        {
            cell.Height = Mathf.Max(cell.Height, WaterLevel + heightAboveWater);
        }

        private void CreateGateCells()
        {
            if (Builder.Gates.Count == 0)
                return;
            var candidates = GetGateCandidates().ToList();
            var rnd = rootRnd.GetBranch(37872);
            //if (BorderFeatures.HasFlag(BorderFeatures.Reserved0))
                //candidates.Sort((a, b) => -Map[a.Item1].Height.CompareTo(Map[b.Item1].Height));
            //else
                rnd.ShuffleFisherYates(candidates);

            foreach (var gate in Builder.Gates)
            {
                var hex = default(Vector2Int);
                if (gate.WorldSide == WorldSide.Custom)
                {
                    hex = gate.Cell;
                }
                else
                {
                    var cand = candidates.FirstOrDefault(p => p.Item2 == gate.WorldSide);
                    candidates.Remove(cand);
                    hex = gate.Cell = cand.Item1;
                }

                Map[hex].Type = mapSpawner.GateCellType;

                var maxH = CellGeometry.Neighbors(hex).Where(h => !Map.IsBorderOrOutside(h)).Select(h => Map[h].Height).DefaultIfEmpty(0).Max();
                Map[hex].Height = maxH + rnd.Float(0.001f);
                var parent = CellGeometry.Neighbors(hex).Where(h => !Map.IsBorderOrOutside(h)).FirstOrDefault(h => Map[h].Height == maxH);
                Map[hex].Parent = parent;
            }
        }

        public IEnumerable<(Vector2Int, WorldSide)> GetGateCandidates()
        {
            var delta = 1;
            switch (Map.Size)
            {
                case > 7: delta = 3; break;
                case > 5: delta = 2; break;
            }

            var from = Map.LeftBorder;
            var to = Map.RightBorder;
            var From = from + delta;
            var To = to - delta;
            for (int x = From; x <= To; x++)
            {
                yield return (new Vector2Int(x, from), WorldSide.South);
                yield return (new Vector2Int(x, to), WorldSide.North);
            }

            for (int y = From; y <= To; y++)
            {
                yield return (new Vector2Int(from, y), WorldSide.West);
                yield return (new Vector2Int(to, y), WorldSide.East);
            }
        }

        const float noiseFreqDivider = 100f;

        protected virtual void BuildCellHeights()
        {
            const float corridor = 0f;
            var terrainSize = CellGeometry.VertSpacing * Map.Size;
            var center = CellGeometry.Center(Map.Center);
            var blur = this.Smoothing_fixed;

            var centerElevate = this.CenterElevation_fixed * (ClampHeight ? -1 : 1);

            // set rnd heights
            var minH = float.MaxValue;
            var maxH = float.MinValue;

            foreach (var hex in Map.AllHex())
            {
                var cell = Map[hex];
                var pp = CellGeometry.Center(hex).ToVector2();
                // mid noise
                var h = rnd.Float(-MidNoiseAmpl_fixed, MidNoiseAmpl_fixed);
                // main noise
                h += SimplexNoise.ComputeNotClamped(pp.x, pp.y + seed * 13.13f, 2, 0, MainNoiseAmpl_fixed, MainNoiseFrequency_fixed / noiseFreqDivider);
                // elevate center
                h += -(center - CellGeometry.Center(hex)).magnitude * 2 / terrainSize * centerElevate;
                // elevate
                h += cell.Elevation;
                // assign formal height
                h = 500 + h;
                cell.Height = h;

                if (h > maxH) maxH = h;
                if (h < minH) minH = h;
            }

            // make river/valley/ridge
            if (ValleyWidth_fixed > 0)
                MakeValley(rnd.GetBranch(3522), ref blur, ValleyOffsetY_fixed < 0 ? minH : maxH);

            // elevate periphery
            FallPeriphery(minH);

            // smooth border cells
            foreach (var hex in Map.AllHex().Where(Map.IsBorderOrOutside))
            {
                var sum = Map.SumNeighborsSafe(hex, out var count);
                if (count > 0)
                    Map[hex].Height = sum / count;
                else
                    Map[hex].Height = 0;
            }

            // blur heights
            Map.Blur(blur);

            // build flatten areas
            BuildFlattenAreas();

            if (NoPaths)
                return;

            // make passages
            Map.AllHex().ForEach(p => Map[p].InternalData = null);
            var visited = new LinkedList<Vector2Int>();
            var avgH = (maxH + minH) / 2f;
            var start = FindStartCell(avgH, rnd.GetBranch(83261));
            Map[start].InternalData = this;
            visited.AddLast(start);
            var cellToNextNodes = new Dictionary<Vector2Int, List<Vector2Int>>();
            cellToNextNodes[start] = new List<Vector2Int>();

            // make paths
            while (true)
            {
                // find neighbor with minimal delta height
                var minDY = float.MaxValue;
                var foundNeighbor = Vector2Int.zero;
                var foundParent = Vector2Int.zero;
                foreach (var hex in visited)
                {
                    var h = Map[hex].Height;
                    for (int j = 0; j < CellGeometry.CornersCount; j++)
                    {
                        var n = CellGeometry.Neighbor(hex, j);
                        if ((!Map.IsBorderOrOutside(n) || Map[n].Type == mapSpawner.GateCellType) && Map[n].Type.IsPassable && Map[n].InternalData == null)
                        {
                            var dY = Mathf.Abs(Map[n].Height - h);
                            if (dY < minDY)
                            {
                                minDY = dY;
                                foundNeighbor = n;
                                foundParent = hex;
                            }
                        }
                    }

                    if (minDY < float.MaxValue)
                        if (rnd.Bool(corridor))
                            break;
                }

                if (minDY == float.MaxValue)
                    break;

                Map[foundNeighbor].InternalData = this;
                Map[foundNeighbor].Parent = foundParent;
                visited.AddFirst(foundNeighbor);
                cellToNextNodes[foundParent].Add(foundNeighbor);
                cellToNextNodes[foundNeighbor] = new List<Vector2Int>();
            }

            // SmoothPaths
            var maxStepForPassage = Mathf.Min(MaxStepForPassage, Preferences.Instance.MaxPassageHeight);
            var maxStepInt = Mathf.Floor(maxStepForPassage);
            SmoothPaths(start);

            // normalize cells heights
            // find min and max height
            minCellH = float.MaxValue;
            maxCellH = float.MinValue;
            foreach (var h in Map.AllHex().Where(h => !Map.IsBorderOrOutside(h)).Select(h => Map[h].Height))
            {
                if (h > maxCellH) maxCellH = h;
                if (h < minCellH) minCellH = h;
            }

            const float minHeightOfCell = 3;
            var heightRnd = rnd.GetBranch(89362);
            if (ClampHeight)
            {
                var mid0 = Mathf.Lerp(minCellH, maxCellH, 1f / 3);
                var mid1 = Mathf.Lerp(minCellH, maxCellH, 2f / 3);
                foreach (var c in Map.AllHex())
                {
                    var h = Map[c].Height;
                    if (h < mid0) h = mid0 + Mathf.Abs(h - mid0);
                    if (h > mid1) h = mid1 - Mathf.Abs(h - mid1);
                    Map[c].Height = (h - mid0) + minHeightOfCell + heightRnd.Float(0.001f);
                }
            }
            else
            {
                foreach (var c in Map.AllHex())
                    Map[c].Height = Map[c].Height - minCellH + minHeightOfCell + heightRnd.Float(0.001f);
            }

            maxCellH = maxCellH - minCellH + minHeightOfCell;
            minCellH = minHeightOfCell;

            void SmoothPaths(Vector2Int start)
            {
                var startCell = Map[start];
                var startH = startCell.Height;
                foreach (var n in cellToNextNodes[start])
                {
                    var newH = startH;
                    var cell = Map[n];
                    var flatten = flattenAreas.ContainsKey(n);
                    var diff = cell.Height - startH;

                    // make flat
                    var flatChance = FlatChance_fixed;
                    if (rnd.Bool(flatChance) || flatten)
                    {
                        newH = startH;
                        goto next;
                    }

                    // make slope
                    if (Mathf.Abs(diff) > maxStepForPassage / 2)
                    {
                        var sign = Mathf.Sign(diff);
                        newH = startH + Mathf.Clamp(sign * rnd.Float(0, StepY), -maxStepForPassage, maxStepForPassage);
                    }
                    else
                    {
                        newH = cell.Height;
                    }

                    // make standard step (0, 1, 2, 3 meters)
                    {
                        diff = Mathf.RoundToInt(newH - startH);
                        diff = Mathf.Clamp(diff, -maxStepInt, maxStepInt);
                        newH = startH + diff;
                        goto next;
                    }

                    next:
                    cell.Height = newH;

                    SmoothPaths(n);
                }
            }
        }

        private void BuildFlattenAreas()
        {
            var processed = new HashSet<int>();
            foreach (var pair in flattenAreas)
            {
                var areaId = pair.Value;
                if (!processed.Add(areaId))
                    continue;

                var h = Map[pair.Key].Height;
                foreach (var p in flattenAreas)
                if (p.Value == pair.Value)
                        Map[p.Key].Height = h;
            }
        }

        private Vector2Int FindStartCell(float avgH, Rnd rnd)
        {
            // try to start from ruins
            var ruinCells = Map.AllInsideHex().Where(p => Map[p].Type.Name == "Ruins");
            if (ruinCells.Any())
            {
                var start = ruinCells.Where(p => Map[p].Height > avgH).MinItem(p => Map[p].Height);
                if (start == Vector2Int.zero) start = rnd.GetRnd(ruinCells);
                return start;
            }
            else
            {
                var start = Map.AllInsideHex().Where(p => Map[p].Height > avgH && Map[p].Type.IsPassable).MinItem(p => Map[p].Height);
                if (start == Vector2Int.zero) start = Map.Center;
                return start;
            }
        }

        private void FallPeriphery(float minH)
        {
            if (PeripheryElevation.IsZeroApprox() || PeripherySize.IsZeroApprox())
                return;

            foreach (var hex in Map.AllInsideHex())
            {
                var d = Map.SignedDistanceToBorder(hex);
                var k = Mathf.Exp(-d / (float)PeripherySize);
                var targetH = (PeripheryElevation < 0 ? minH : Map[hex].Height) + PeripheryElevation;
                Map[hex].Height = Mathf.Lerp(Map[hex].Height, targetH, k);
            }                
        }

        protected void MakeValley(Rnd rnd, ref int blur, float minOrMaxH)
        {
            var startX = Map.Size / 2;// Mathf.RoundToInt(rnd.Triangle(Map.Size / 3f, 2f / 3f * Map.Size));
            var startY = 0;
            while (startY < Map.Size)
            {
                for (int i = -ValleyWidth_fixed; i <= ValleyWidth_fixed; i++)
                {
                    var x = startX + i;
                    var pos = new Vector2Int(x, startY);
                    if (Map.InRange(pos))
                        Map[pos].Height = minOrMaxH + ValleyOffsetY_fixed;
                }

                if (rnd.Bool(0.8f))
                    startY++;
                startX += rnd.Int(-1, 2);
            }

            if (blur < 1) blur = 1;
        }

        private void MakeValley_old(ref int blur, float minOrMaxH)
        {
            var riverX = new Vector2Int(Map.Center.x - ValleyWidth_fixed, Map.Center.x + ValleyWidth_fixed);
            foreach (var p in Map.AllHex())
            {
                // river / valley
                if (p.x > riverX.x && p.x < riverX.y)
                    Map[p].Height = minOrMaxH + ValleyOffsetY_fixed;
            }

            if (blur < 1) blur = 1;
        }

        private void LiftUpCells()
        {
            var maxLiftUpY = 2;
            var found = true;
            while (found)
            {
                found = false;
                foreach (var hex in Map.AllHex())
                {
                    var cell = Map[hex];
                    var minH = GetMinNeighborHeight(hex);
                    if (minH == float.MaxValue || minH <= cell.Height + maxLiftUpY)
                        continue;
                    cell.Height = minH - maxLiftUpY;
                    found = true;
                }
            }
        }

        float GetMinNeighborHeight(Vector2Int hex)
        {
            var cell = Map[hex];
            var min = float.MaxValue;

            if (!Map.IsBorderOrOutside(hex))
            for (int i = 0; i < CellGeometry.CornersCount; i++)
            {
                var n = CellGeometry.Neighbor(hex, i);
                if (Map.IsBorderOrOutside(n)) continue;
                var h = Map[n].Height;
                if (h > cell.Height)
                {
                    if (h < min)
                        min = h;
                }
            }

            return min;
        }

        private void CalcBorderCellsHeight()
        {
            var borderIsZero = BorderFeatures.HasFlag(BorderFeatures.AbsoluteHeight);
            foreach (var hex in Map.AllHex())
            if (Map[hex].Type == mapSpawner.BorderCellType)
            {
                var maxH = CellGeometry.NeighborsEx(hex).Where(h => Map[h].Type.IsPassable).Select(h => Map[h].Height).DefaultIfEmpty(0).Max();
                Map[hex].Height = (borderIsZero ? 0 : maxH) + BorderElevation;
            }

            if ((borderIsZero || BorderElevation < 0) && mapSpawner.BorderCellType.HeightPower > 0.1)
                //BorderCellType.HeightPower = 0.1f;
                Debug.LogWarning("BorderOffsetY is less than zero. Reduce BorderCellType.HeightPower to 0.1 or less!");
        }

        private void OnDestroy()
        {
            if (Slopes)
            {
                Destroy(Slopes);
                Slopes = null;
            }
        }

        public float GetSlopeAngle(Vector3 pos)
        {
            var pos2d = WorldToTerrain(pos);
            return Slopes.GetPixelBilinear(pos2d.x, pos2d.y).a;
        }

        public Vector3 GetNormal(Vector3 pos)
        {
            var pos2d = WorldToTerrain(pos);
            var n = Slopes.GetPixelBilinear(pos2d.x, pos2d.y);
            return new Vector3(n.r, n.g, n.b).normalized;
        }

        public Vector2 WorldToTerrain(Vector3 pos)
        {
            return new Vector2(pos.x / terrainSize.x, pos.z / terrainSize.z);
        }

        private IEnumerator BuildTerrain()
        {
            Builder.Terrain = Instantiate(TerrainPrefab, Builder.transform);
            Builder.Terrain.gameObject.SetActive(false);
            Builder.Terrain.name = "Terrain";
            Builder.Terrain.gameObject.GetOrAddComponent<AdjustMaterialViewDistance>();
            Builder.Terrain.gameObject.GetOrAddComponent<AdjustObjectsPosition>();
            TempHolder = new GameObject("TempHolder").transform;
            TempHolder.SetParent(Terrain.transform);
            TempHolder.gameObject.SetActive(false);
            var data = Builder.Terrain.terrainData = Instantiate(Builder.Terrain.terrainData);
            terrainSize = data.size = CalcTerrainSize(data);
            data.SetDetailResolution((int)(900f + 200f * terrainSize.z / 277f), data.detailResolutionPerPatch);
            Builder.DetailResolutionScale = data.detailResolution / (900f + 200f);
            //data.SetDetailResolution(1024, data.detailResolutionPerPatch);
            #if UNITY_2022_2_OR_NEWER
            QualitySettings.useLegacyDetailDistribution = false;
            #endif
            var hRes = data.heightmapResolution;

            // BuildTerrainHeights
            HeightMap = new float[hRes, hRes];

            if (Application.isPlaying)
                yield return Dispatcher.ExecuteInSecondaryThread(() => BuildTerrainHeights(hRes, HeightMap));
            else
                BuildTerrainHeights(hRes, HeightMap);

            yield return null;

            // Coast filter
            if (!CoastWidth.IsZeroApprox())
            {
                if (Application.isPlaying)
                    yield return Dispatcher.ExecuteInSecondaryThread(() => MakeCoast(HeightMap));
                else
                    MakeCoast(HeightMap);
            }

            yield return null;

            // Median filter
            if (Application.isPlaying)
                yield return Dispatcher.ExecuteInSecondaryThread(() => MedianFilter(HeightMap));
            else
                MedianFilter(HeightMap);

            yield return null;

            yield return Builder.OnPhaseCompleted(BuildPhase.TerrainHeightMapCreated);

            //build slope map
            var slopes = new Color[hRes * hRes];
            if (Application.isPlaying)
                yield return Dispatcher.ExecuteInSecondaryThread(() => CalcTerrainSlopes(HeightMap, slopes));
            else
                CalcTerrainSlopes(HeightMap, slopes);

            this.Slopes = new Texture2D(hRes, hRes, TextureFormat.RGBAFloat, false, true);
            this.Slopes.SetPixels(slopes);
            this.Slopes.Apply();

            // sweep border points to zero level
            SetBorderToZero(HeightMap);

            // flush heights
            data.SetHeights(0, 0, HeightMap);

            yield return null;

            Builder.TerrainLayersBuilder = new TerrainLayersBuilder(Builder, Terrain);

            yield return null;
            Builder.Terrain.GetComponent<TerrainCollider>().terrainData = data;
        }

        private void SetBorderToZero(float[,] heightMap)
        {
            var hRes = heightMap.GetLength(0);
            var hResMinusOne = hRes - 1;

            for (int i = 0; i < hRes; i++)
                heightMap[i, 0] = 
                heightMap[i, hResMinusOne] = 
                heightMap[0, i] = 
                heightMap[hResMinusOne, i] = 0;
        }

        private Vector3 CalcTerrainSize(TerrainData data)
        {
            Vector3 size;
            switch (CellGeometry.CornersCount)
            {
                case 6:
                    var offset = CellGeometry.Center(Vector2Int.zero) - Builder.transform.position;
                    size = new Vector3((Map.Width) * CellGeometry.HorzSpacing + offset.x - (CellGeometry.HorzSpacing - CellGeometry.Radius), data.size.y, (Map.Height) * CellGeometry.VertSpacing + offset.z);
                    break;
                default:
                    size = new Vector3((Map.Width) * CellGeometry.HorzSpacing, data.size.y, (Map.Height) * CellGeometry.VertSpacing);
                    break;
            }

            return size;
        }

        private void CalcTerrainSlopes(float[,] heights, Color[] slopes)
        {
            var hRes = heights.GetLength(0);
            var ky = terrainSize.y;
            var kx = terrainSize.x / hRes;
            var kz = terrainSize.z / hRes;

            for (int i = 1; i < hRes - 1; i++)
            {
                for (int j = 1; j < hRes - 1; j++)
                {
                    var p0 = Convert(new Vector3(i - 1, heights[j, i - 1], j));
                    var p1 = Convert(new Vector3(i + 1, heights[j + 1, i + 1], j + 1));
                    var p2 = Convert(new Vector3(i + 1, heights[j - 1, i + 1], j - 1));
                    var n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                    var slope = Mathf.Acos(n.y) * Mathf.Rad2Deg;
                    // RGB - normal, A - angle of slope (degrees)
                    slopes[i + j * hRes] = new Color(n.x, n.y, n.z, slope);
                }
            }

            Vector3 Convert(Vector3 p)
            {
                p.x *= kx;
                p.y *= ky;
                p.z *= kz;
                return p;
            }
        }       

        private void MakeCoast(float[,] heights)
        {
            const float steepness = 1.3f;//1.5
            var hRes = heights.GetLength(0);
            var ky = terrainSize.y;

            for (int i = 1; i < hRes - 1; i++)
            {
                for (int j = 1; j < hRes - 1; j++)
                {
                    // to world height 
                    var h = heights[j, i] * ky;
                    // Logistic curve
                    h = Coast(h, CoastFlatness, steepness, CoastElevation);
                    // to terrain normalized height
                    heights[j, i] = h / ky;
                }
            }
        }

        private float Coast(float height, float coastPower, float steepness, float elevation = 0f)
        {
            var waterLevel = WaterLevel;
            height -= waterLevel;

            var k = CoastCoeff(height, coastPower, steepness);
            height *= k;

            if (height > 0)
                height += (1 - k) * elevation;
            else
                height -= (1 - k) * elevation;

            height += waterLevel;
            return height;
        }

        private float CoastCoeff(float heightAboveWater, float coastPower, float steepness)
        {
            if (heightAboveWater < 0)
                //return 1;
                heightAboveWater *= -1;

            var k = 1 / (coastPower + 0.001f);
            var res = 2 / (1 + Mathf.Exp(-k * Mathf.Pow(heightAboveWater, steepness))) - 1;
            return res;
        }

        private void MedianFilter(float[,] heights)
        {
            var hRes = heights.GetLength(0);

            for (int i = 1; i < hRes - 1; i++)
            {
                for (int j = 1; j < hRes - 1; j++)
                {
                    var h0 = heights[j, i - 1];
                    var h1 = heights[j, i + 1];
                    var h2 = heights[j - 1, i];
                    var h3 = heights[j + 1, i];

                    heights[j, i] = (h0 + h1 + h2 + h3) / 4;
                }
            }
        }

        private void BuildTerrainHeights(int hRes, float[,] heights)
        {
            var isHexGeometry = CellGeometry.CornersCount == 6;
            //Set terrain height
            var kx = (float)terrainSize.x / hRes;
            var ky = (float)terrainSize.z / hRes;
            for (int i = 0; i < hRes; i++)
            {
                for (int j = 0; j < hRes; j++)
                {
                    var p = new Vector3(i * kx, 0, j * ky);
                    if (isHexGeometry)
                        heights[j, i] = GetHeightHex(p) / terrainSize.y;
                    else
                        heights[j, i] = GetHeightRect(p) / terrainSize.y;
                }
            }
        }

        public float GetFormalHeightBetweenCells(Vector2Int hex, int iEdge, float distance01)
        {
            var n1 = CellGeometry.Neighbor(hex, iEdge);

            var cell0 = Map[hex]; var h0 = cell0.Height; var w0 = cell0.Type.HeightPower;
            var cell1 = Map[n1]; var h1 = cell1.Height; var w1 = cell1.Type.HeightPower;

            var k = cell0.Parent == n1 || cell1.Parent == hex ? SlopeSteepnessInPassages : SlopeSteepness_fixed;

            var x = Mathf.Pow(1 - distance01, k) * w0 * (distance01 <= 0.5f ? cell0.Type.HeightSharpness : 1);
            var y = Mathf.Pow(distance01, k) * w1 * (distance01 > 0.5f ? cell1.Type.HeightSharpness : 1);

            var resH = (h0 * x + h1 * y) / (x + y);
            return resH;
        }

        private float GetHeightHex(Vector3 worldPos)
        {
            var hex = CellGeometry.PointToHex(worldPos);
            var iCorner = CellGeometry.PointToCorner(hex, worldPos);
            var n1 = CellGeometry.Neighbor(hex, iCorner - 1);
            var n2 = CellGeometry.Neighbor(hex, iCorner);

            var p0 = CellGeometry.Center(hex); var cell0 = Map[hex]; var h0 = cell0.Height; var w0 = cell0.Type.HeightPower; var noise0 = cell0.MicroNoiseScale;
            var p1 = CellGeometry.Center(n1); var cell1 = Map[n1]; var h1 = cell1.Height; var w1 = cell1.Type.HeightPower; var noise1 = cell1.MicroNoiseScale;
            var p2 = CellGeometry.Center(n2); var cell2 = Map[n2]; var h2 = cell2.Height; var w2 = cell2.Type.HeightPower; var noise2 = cell2.MicroNoiseScale;

            // get Barycentric coordinates of point
            var bar = Helper.Barycentric(worldPos.XZ(), p0, p1, p2);
            bar = new Vector3(Mathf.Abs(bar.x), Mathf.Abs(bar.y), Mathf.Abs(bar.z));

            // steepness
            var k1 = cell0.Parent == n1 || cell1.Parent == hex ? SlopeSteepnessInPassages : SlopeSteepness_fixed;
            var k2 = cell0.Parent == n2 || cell2.Parent == hex ? SlopeSteepnessInPassages : SlopeSteepness_fixed;
            var k = Mathf.Min(k1, k2);

            var heightSharpness = cell0.Type.HeightSharpness;

            bar.x = Mathf.Pow(bar.x, k) * w0 * heightSharpness;
            bar.y = Mathf.Pow(bar.y, k1) * w1;
            bar.z = Mathf.Pow(bar.z, k2) * w2; 

            // normalize Barycentric
            var sum = bar.x + bar.y + bar.z;
            bar /= sum;

            // interpolate height
            var res = h0 * bar.x + h1 * bar.y + h2 * bar.z;

            // add rocks on slopes
            var rocks = SimplexNoise.ComputeNotClamped(worldPos.x + seed * 2 + 112, worldPos.z, 4, 0.3f, 0.5f, SlopeRoughnessFrequency / noiseFreqDivider, 2, 0.7f);

            rocks *= noise0 * bar.x + noise1 * bar.y + noise2 * bar.z;

            var minDH = Mathf.Min(Mathf.Abs(res - h0), Mathf.Abs(res - h1), Mathf.Abs(res - h2));
            minDH += (SmallSlopeRoughness - 1) * Mathf.Exp((1 - minDH / 4) * 3) * minDH / 10f;
            res += Mathf.Clamp(rocks, -1, 1) * Mathf.Clamp(minDH, 0, 5) * SlopeRoughness;

            // add noise
            var noise = SimplexNoise.ComputeNotClamped(worldPos.x, worldPos.z + seed * 3 + 145, 5, MicroNoiseOffsetY, 1, MicroNoiseFrequency_fixed / noiseFreqDivider, 2, 0.5f) * MicroNoiseAmpl_fixed;

            noise *= noise0 * bar.x + noise1 * bar.y + noise2 * bar.z;
            res += noise;

            return res;
        }

        static Vector2Int[] offsets = new Vector2Int[4] { new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) };

        private float GetHeightRect(Vector3 worldPos)
        {
            var hex = CellGeometry.PointToHex(worldPos);
            var iCorner = CellGeometry.PointToCorner(hex, worldPos);
            var one = offsets[iCorner];
            var n1 = hex + new Vector2Int(one.x, 0);
            var n2 = hex + one;
            var n3 = hex + new Vector2Int(0, one.y);

            var p0 = CellGeometry.Center(hex); var cell0 = Map[hex]; var h0 = cell0.Height; var w0 = cell0.Type.HeightPower; var noise0 = cell0.Type.MicroNoiseScale;
            var p1 = CellGeometry.Center(n1); var cell1 = Map[n1]; var h1 = cell1.Height; var w1 = cell1.Type.HeightPower; var noise1 = cell1.Type.MicroNoiseScale;
            var p2 = CellGeometry.Center(n2); var cell2 = Map[n2]; var h2 = cell2.Height; var w2 = cell2.Type.HeightPower; var noise2 = cell2.Type.MicroNoiseScale;
            var p3 = CellGeometry.Center(n3); var cell3 = Map[n3]; var h3 = cell3.Height; var w3 = cell3.Type.HeightPower; var noise3 = cell3.Type.MicroNoiseScale;

            var bar = Helper.BarycentricRect(worldPos.XZ(), p0, p1, p2, p3);
            bar = new Vector4(Mathf.Abs(bar.x), Mathf.Abs(bar.y), Mathf.Abs(bar.z), Mathf.Abs(bar.w));

            // steepness
            var k1 = cell0.Parent == n1 || cell1.Parent == hex ? SlopeSteepnessInPassages : SlopeSteepness_fixed;
            var k2 = cell0.Parent == n2 || cell2.Parent == hex ? SlopeSteepnessInPassages : SlopeSteepness_fixed;
            var k3 = cell0.Parent == n3 || cell3.Parent == hex ? SlopeSteepnessInPassages : SlopeSteepness_fixed;
            var k = Mathf.Min(k1, k2, k3);

            bar.x = Mathf.Pow(bar.x, k) * w0 * cell0.Type.HeightSharpness;
            bar.y = Mathf.Pow(bar.y, k1) * w1;
            bar.z = Mathf.Pow(bar.z, k2) * w2;
            bar.w = Mathf.Pow(bar.w, k3) * w3;

            // normalize Barycentric
            var sum = bar.x + bar.y + bar.z + bar.w;
            bar /= sum;

            var res = h0 * bar.x + h1 * bar.y + h2 * bar.z + h3 * bar.w;

            // add rocks on slopes
            var rocks = SimplexNoise.ComputeNotClamped(worldPos.x + seed * 2 + 112, worldPos.z, 4, 0.3f, 0.5f, SlopeRoughnessFrequency / noiseFreqDivider, 2, 0.7f);

            rocks *= noise0 * bar.x + noise1 * bar.y + noise2 * bar.z + noise3 * bar.w;

            var minDH = Mathf.Min(Mathf.Abs(res - h0), Mathf.Abs(res - h1), Mathf.Abs(res - h2), Mathf.Abs(res - h3));
            minDH += (SmallSlopeRoughness - 1) * Mathf.Exp((1 - minDH / 4) * 3) * minDH / 10f;
            res += Mathf.Clamp(rocks, -1, 1) * Mathf.Clamp(minDH, 0, 5) * SlopeRoughness;

            // add noise
            var noise = SimplexNoise.ComputeNotClamped(worldPos.x, worldPos.z + seed * 3 + 145, 5, MicroNoiseOffsetY, 1, MicroNoiseFrequency_fixed / noiseFreqDivider, 2, 0.5f) * MicroNoiseAmpl_fixed;

            noise *= noise0 * bar.x + noise1 * bar.y + noise2 * bar.z + noise3 * bar.w;
            res += noise;

            return res;
        }

        //private void Draw(int rockLayerIndex, Vector2Int pos, float brushSize, float power)
        //{
        //    var size = Mathf.CeilToInt(brushSize);
        //    for (var dx = -size + 1; dx < size; dx++)
        //    for (var dy = -size + 1; dy < size; dy++)
        //    {
        //        var xx = pos.x + dx;
        //        var yy = pos.y + dy;
        //        var intens = 1f - (dx * dx + dy * dy) / (brushSize * brushSize);
        //        if (xx < 0 || yy < 0 || xx >= aw || yy >= ah || intens <= 0)
        //            continue;
        //        intens *= power;
        //        intens = Mathf.Max(splatmapData[xx, yy, rockLayerIndex], intens);
        //        splatmapData[xx, yy, rockLayerIndex] = intens;
        //        //splatmapData[xx, yy, defaultLayerIndex] = 1 - intens;
        //    }
        //}

        private void CreateMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            foreach (var hex in Map.AllHex())
            {
                var cell = Map[hex];
                var center = CellGeometry.Center(hex) + Vector3.up * cell.Height;
                var startVert = verts.Count;
                verts.Add(center);
                for (int i = 0; i < CellGeometry.CornersCount; i++)
                {
                    var p = center + CellGeometry.Corner(i);
                    verts.Add(p);
                }

                // make tris
                for (int i = 0; i < CellGeometry.CornersCount; i++)
                {
                    tris.Add(startVert);
                    tris.Add(startVert + i + 1);
                    tris.Add(i == CellGeometry.CornersCount - 1 ? startVert + 1 : startVert + i + 2);
                }
            }

            var mesh = new Mesh();
            if (verts.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            //var mf = Instantiate(terrainPrefab, transform);
            //mf.transform.position = Vector3.zero;
            //mf.sharedMesh = mesh;
        }
    }

    [Flags]
    public enum BorderFeatures
    {
        None = 0,
        AbsoluteHeight = 0x1,
        LiftUpBordersToWaterLevel = 0x2,
        LiftUpGatesToWaterLevel = 0x4,
        Reserved0 = 0x8,
    }

    static class MinMaxCurveHelper
    {
        public static float Value(this ParticleSystem.MinMaxCurve val, Rnd rnd)
        {
            var time = rnd.Float();
            var lerp = rnd.Float();
            var res = val.Evaluate(time, lerp);

            if (val.mode == ParticleSystemCurveMode.Curve || val.mode == ParticleSystemCurveMode.TwoCurves)
                res *= val.constant;

            return res;
        }

        public static int IntValue(this ParticleSystem.MinMaxCurve val, Rnd rnd)
        {
            return Mathf.RoundToInt(val.Value(rnd));
        }

        public static ParticleSystem.MinMaxCurve Clamp(this ParticleSystem.MinMaxCurve val, float min, float max)
        {
            val.constant = Mathf.Clamp(val.constant, min, max);
            val.constantMin = Mathf.Clamp(val.constantMin, min, max);
            val.constantMax = Mathf.Clamp(val.constantMax, min, max);
            return val;
        }

        public static ParticleSystem.MinMaxCurve ClampInt(this ParticleSystem.MinMaxCurve val, int min, int max)
        {
            val.constant = RoundInt(Mathf.Clamp(val.constant, min, max));
            val.constantMin = RoundInt(Mathf.Clamp(val.constantMin, min, max));
            val.constantMax = RoundInt(Mathf.Clamp(val.constantMax, min, max));
            return val;
        }

        static int RoundInt(float val)
        {
            var i = Mathf.RoundToInt(val);
            var d = val - i;
            if (d < 0.001f)
            {
                if (d >= 0f)
                    return i;
                else
                    return i - 1;
            }else
                return i + 1;
        }
    }
}
