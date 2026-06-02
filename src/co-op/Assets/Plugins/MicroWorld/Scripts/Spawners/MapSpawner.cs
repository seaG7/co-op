using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// This spawner defines map size, cell size, cell geometry, cell types
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.c14p1l2sm1ni")]
    public class MapSpawner : BaseSpawner, IExclusive
    {
        public string ExclusiveGroup => "MapSpawner";
        [field: SerializeField]
        [field: Tooltip("Defines the chance of selecting this spawner among all MapSpawners of MicroWorld instance.")] 
        public float Chance { get; set; } = 1;
        public override int Order => 200;

        [Header("Map")]
        [Tooltip("Map size (width and same height) measured in cells. This value does not include border cells.")]
        [SerializeField] public ParticleSystem.MinMaxCurve MapSize = 15;
        [Tooltip("Cell size defines the size of a cell (meters).")]
        [SerializeField] public ParticleSystem.MinMaxCurve CellSize = 10;
        [Tooltip("Specifies the thickness of the border, measured in cells.")]
        [SerializeField, Range(1, 3)] int BorderPadding = 1;
        [Tooltip("Specifies shape of cells.")]
        [SerializeField] CellShape Geometry = CellShape.Hex;
        public int BorderPaddingSize => BorderPadding;

        [Header("Cell Types")]
        [Tooltip("Defines the frequency of cell types noise. The higher the value, the smaller the type islands.")]
        [SerializeField] ParticleSystem.MinMaxCurve CellTypeFrequency = 1;
        [Tooltip("This flag makes the distribution of cell types more uniform, avoiding mixing of types.")]
        [SerializeField] public bool CellTypeUniformity = false;
        [Tooltip("List of cell types defined by user.")]
        [SerializeField] public CellType[] CellTypes;

        [Header("Predefined Cell Types")]
        [SerializeField] public CellType BorderCellType = new CellType { Name = "Border", HeightSharpness = 3, HeightPower = 1f };
        [SerializeField] public CellType GateCellType = new CellType { Name = "Gate", MicroNoiseScale = 0, HeightPower = 5, HeightSharpness = 10 };
        [SerializeField] public CellType WaterCellType = new CellType { Name = "Water" };
        [SerializeField, HideInInspector] public CellType FallbackCellType = new CellType { Name = "Fallback" };

        public IEnumerable<CellType> AllCellTypes => CellTypes.Union(new CellType[] { BorderCellType, GateCellType, WaterCellType, FallbackCellType });

        RndMapper rndMapper;
        
        private int? _overrideWidth;
        private int? _overrideHeight;

        public void SetMapSizeOverride(int width, int height)
        {
            _overrideWidth = width;
            _overrideHeight = height;
        }

        public override IEnumerator Prepare(MicroWorld builder)
        {
            yield return base.Prepare(builder);
            builder.MapSpawner = this;

            FixVariableParams();
            
            int finalWidth = _overrideWidth.HasValue ? _overrideWidth.Value : MapSize_fixed;
            int finalHeight = _overrideHeight.HasValue ? _overrideHeight.Value : MapSize_fixed;

            switch (Geometry)
            {
                case CellShape.Square:
                    CellGeometry = new RectCellGeometry(CellSize_fixed, Builder.transform.position + new Vector3(CellSize_fixed, 0, CellSize_fixed));
                    break;
                case CellShape.Hex:
                    CellGeometry = new HexCellGeometry(CellSize_fixed, Builder.transform.position + new Vector3(CellSize_fixed, 0, Mathf.Sqrt(3) * CellSize_fixed / 2));
                    break;
            }
            
            builder.Map = new Map(finalWidth + BorderPadding * 2, finalHeight + BorderPadding * 2, BorderPadding, CellGeometry);

            if (CellTypes == null || CellTypes.Length == 0)
            {
                CellTypes = new CellType[]
                {
                    new CellType{ Name = "Forest" },
                    new CellType{ Name = "Field" }
                };
            }

            BorderCellType.Features |= CellTypeFeatures.NoPassage;
        }

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            rndMapper = new RndMapper(CellTypes.Select(t => t.Chance).ToArray());
            CalcOnePerTerrainCellTypes(rootRnd.GetBranch(122));
            CalcCellTypes();
        }

        private void CalcCellTypes()
        {
            foreach (var hex in Map.AllInsideHex())
                if (Map[hex].Type == null)
                    Map[hex].Type = CalcCellType(hex);

            CreateGateCells();

            foreach (var hex in Map.AllHex().Where(Map.IsBorderOrOutside))
                if (Map[hex].Type == null)
                    Map[hex].Type = BorderCellType;
        }

        private void CalcOnePerTerrainCellTypes(Rnd rnd)
        {
            Vector2Int[] shuffledInsideHexes = null;
            var oneCellTypes = CellTypes.Where(c => c.Chance > 0 && c.Features.HasFlag(CellTypeFeatures.OneCellPerTerrain));

            foreach (var type in oneCellTypes)
            {
                if (!rnd.Bool(type.Chance))
                    continue;

                if (shuffledInsideHexes == null)
                    shuffledInsideHexes = rnd.Shuffle(Map.AllInsideHex()).ToArray();

                foreach (var p in shuffledInsideHexes)
                {
                    var cell = Map[p];
                    if (cell.Type == null)
                    {
                        cell.Type = type;
                        cell.LiftUpToWaterLevel = true;
                        break;
                    }
                }
            };
        }

        private CellType CalcCellType(Vector2Int hex)
        {
            var p = Vector2Int.RoundToInt(CellGeometry.Center(hex).ToVector2() * CellTypeFrequency_fixed);
            p += islandRndOffset;
            var classId = CellTypeUniformity ? Noises.SmoothedIslands[p] : Noises.Islands[p];
            var cellType = CellTypes[rndMapper.Generate(classId)];
            return cellType;
        }

        private void CreateGateCells()
        {
            if (Builder.Gates.Count == 0)
                return;
            var candidates = GetGateCandidates().Where(pair => Map[pair.Item1].Type == null).ToList();
            var rnd = rootRnd.GetBranch(37872);
            rnd.ShuffleFisherYates(candidates);

            foreach (var gate in Builder.Gates)
            {
                if (!TryResolveGateHex(gate, candidates, out Vector2Int hex))
                {
                    Debug.LogWarning($"[MapSpawner] Failed to resolve gate cell for '{gate.Name}'.");
                    continue;
                }

                Map[hex].Type = GateCellType;

                float maxH = CellGeometry.Neighbors(hex)
                    .Where(h => !Map.IsBorderOrOutside(h))
                    .Select(h => Map[h].Height)
                    .DefaultIfEmpty(0)
                    .Max();
                Map[hex].Height = maxH + rnd.Float(0.001f);

                Vector2Int parent = CellGeometry.Neighbors(hex)
                    .Where(h => !Map.IsBorderOrOutside(h))
                    .OrderByDescending(h => Map.SignedDistanceToBorder(h))
                    .ThenByDescending(h => Map[h].Height)
                    .FirstOrDefault();
                Map[hex].Parent = parent;
            }
        }

        private bool TryResolveGateHex(
            GateInfo gate,
            List<(Vector2Int cell, WorldSide side)> candidates,
            out Vector2Int hex)
        {
            if (gate.WorldSide == WorldSide.Custom && IsCustomGateHexUsable(gate, gate.Cell))
            {
                hex = gate.Cell;
                return true;
            }

            if (gate.WorldSide != WorldSide.Custom)
            {
                int sideIndex = candidates.FindIndex(candidate =>
                    candidate.side == gate.WorldSide &&
                    IsGateHexUsable(candidate.cell));

                if (sideIndex >= 0)
                {
                    hex = gate.Cell = candidates[sideIndex].cell;
                    candidates.RemoveAt(sideIndex);
                    return true;
                }
            }

            int fallbackIndex = candidates.FindIndex(candidate => IsGateHexUsable(candidate.cell));
            if (fallbackIndex >= 0)
            {
                hex = gate.Cell = candidates[fallbackIndex].cell;
                candidates.RemoveAt(fallbackIndex);
                Debug.LogWarning($"[MapSpawner] Fallback gate cell selected for '{gate.Name}' at {hex}.");
                return true;
            }

            hex = default;
            return false;
        }

        private bool IsCustomGateHexUsable(GateInfo currentGate, Vector2Int hex)
        {
            if (!Map.InRange(hex) || Map.IsBorderOrOutside(hex))
                return false;

            return Builder.Gates
                .Where(g => g != null && g != currentGate)
                .All(g => g.Cell != hex);
        }

        private bool IsGateHexUsable(Vector2Int hex) =>
            Map.InRange(hex) && Map[hex].Type == null;
        
        public IEnumerable<(Vector2Int, WorldSide)> GetGateCandidates()
        {
            var delta = 1;
            if (Map.Width > 7 || Map.Height > 7) delta = 3;
            else if (Map.Width > 5 || Map.Height > 5) delta = 2;

            var from = Map.LeftBorder;
            var toX = Map.RightBorderX;
            var toY = Map.RightBorderY;
            
            var From = from + delta;
            
            // X
            var ToX = toX - delta;
            for (int x = From; x <= ToX; x++)
            {
                yield return (new Vector2Int(x, from), WorldSide.South);
                yield return (new Vector2Int(x, toY), WorldSide.North);
            }

            // Y
            var ToY = toY - delta;
            for (int y = From; y <= ToY; y++)
            {
                yield return (new Vector2Int(from, y), WorldSide.West);
                yield return (new Vector2Int(toX, y), WorldSide.East);
            }
        }

        #region Fix variable params

        int MapSize_fixed;
        int CellSize_fixed;
        internal float CellTypeFrequency_fixed { get; private set; }
        Vector2Int islandRndOffset;

        private void FixVariableParams()
        {
            var minMaxCurveRnd = rootRnd.GetBranch(487454);

            MapSize_fixed = MapSize.IntValue(minMaxCurveRnd);
            CellSize_fixed = CellSize.IntValue(minMaxCurveRnd);
            CellTypeFrequency_fixed = CellTypeFrequency.Value(minMaxCurveRnd);
            islandRndOffset = new Vector2Int(minMaxCurveRnd.Int(200), minMaxCurveRnd.Int(200));
        }
        #endregion

        private void OnValidate()
        {
            MapSize = MapSize.ClampInt(2, 500);
            CellSize = CellSize.ClampInt(1, 300);
            CellTypeFrequency = CellTypeFrequency.Clamp(0, 10);

            if (CellTypes != null)
            for (int i = 0; i < CellTypes.Length; i++)
            {
                var c = CellTypes[i];
                if (c.Name.NotNullOrEmpty() || c.Chance != 0f)
                    continue;
                CellTypes[i] = new CellType();
            }
        }
    }

    public enum CellShape
    {
        Hex = 0, Square = 1
    }
    
    class RndMapper
    {
        private float[] cumulativeProbabilities;

        public RndMapper(float[] chances)
        {
            cumulativeProbabilities = new float[chances.Length];
            var sum = chances.Sum();
            if (sum <= float.Epsilon)
                return;

            var cumulativeSum = 0f;
            for (int i = 0; i < chances.Length; i++)
            {
                cumulativeSum += chances[i] / sum;
                cumulativeProbabilities[i] = cumulativeSum;
            }
        }

        public int Generate(int randomInput)
        {
            var rand = new System.Random(randomInput);
            var randomValue = (float)rand.NextDouble();

            int left = 0;
            int right = cumulativeProbabilities.Length - 1;

            while (left < right)
            {
                int mid = left + (right - left) / 2;
                if (randomValue <= cumulativeProbabilities[mid])
                {
                    right = mid;
                }
                else
                {
                    left = mid + 1;
                }
            }

            return left;
        }
    }
}
