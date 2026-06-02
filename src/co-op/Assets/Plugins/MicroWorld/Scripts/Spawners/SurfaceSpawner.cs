using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// Defines the material, color, and texture blending for the terrain surface
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.gtoczebnc0qy")]
    public class SurfaceSpawner : BaseSpawner, IExclusive
    {
        public string ExclusiveGroup => "SurfaceSpawner";
        [field: SerializeField]
        [field: Tooltip("Defines the chance of selecting this spawner among all SurfaceSpawners of MicroWorld instance.")]
        public float Chance { get; set; } = 1;
        public override int Order => 1300;

        [Header("Surface")]
        [Tooltip("Material that will be assigned to the terrain.")]
        [SerializeField] Material TerrainMaterial;
        [Tooltip("Defines slope angle where rock texture appears instead of grass.")]
        [SerializeField, InspectorName("Grass <-> Rock Surface Balance")] ParticleSystem.MinMaxCurve GrassRockBalance = 0.4f;
        [Tooltip("Tint color for rock texture.")]
        [SerializeField] public ParticleSystem.MinMaxGradient RocksTint = new Color(0.798f, 0.782f, 0.731f);
        [Tooltip("How much the sand texture will rise above the water level (meters).")]
        [SerializeField] public float SandTextureHeightAboveWater = 0;

        [Space(15)]
        [Tooltip("List of layers to generate splatmaps for terrain.")]
        [SerializeField] public SplatMapSettings[] SplatMaps;
        [Tooltip("Sets the noise radius when mixing splatmaps (meters). This property only works for cell type based splatmaps.")]
        [SerializeField] public float BlendingRadius = 3;
        [SerializeField] public SplatMapFeatures SplatmapsFeatures;

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            CheckMapSpawner();
            CheckTerrainSpawner();

            FixVariableParams();

            if (!TerrainMaterial)
                TerrainMaterial = Resources.Load<Material>("Terrain");

            // Material
            if (Terrain != null)
            {
                Terrain.materialTemplate = Instantiate(TerrainMaterial);
                Terrain.materialTemplate.SetFloat("_Slope", GrassRockBalance_fixed);
                Terrain.materialTemplate.SetColor("_SideTint", RocksTint_fixed);
                Terrain.materialTemplate.SetFloat("_WaterLevel", Builder.TerrainSpawner.WaterLevel + SandTextureHeightAboveWater);
            }
            Builder.CreatedMaterials.Add(TerrainMaterial, Terrain.materialTemplate);

            //splatmaps
            yield return BuildSplatMaps();
        }

        private IEnumerator BuildSplatMaps()
        {
            if (SplatMaps == null || SplatMaps.Length == 0)
                yield break;

            var terrainSpawner = Builder.TerrainSpawner;
            var slopes = terrainSpawner.Slopes;
            var waterLevel = Builder.TerrainSpawner.WaterLevel + SandTextureHeightAboveWater;

            // prepare terrain layers
            var terrainData = Terrain.terrainData;
            var defaultLayer = -1;

            var layers = new TerrainLayer[SplatMaps.Length];
            for (int i = 0; i < SplatMaps.Length; i++)
            {
                var splatmap = SplatMaps[i];
                layers[i] = splatmap.TerrainLayer;
                if (layers[i] == null)
                    layers[i] = i < terrainData.terrainLayers.Length ? terrainData.terrainLayers[i] : new TerrainLayer();

                if (splatmap.Source == SplatMapSource.Default && defaultLayer < 0)
                    defaultLayer = i;
            }
            terrainData.terrainLayers = layers;

            // calculate splatmap layer for each point
            var size = terrainData.alphamapWidth;
            var splatmapLayer = new byte[size, size];
            var kx = terrainData.size.x / size;
            var kz = terrainData.size.z / size;
            var rnd = rootRnd.GetBranch(342);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var worldX = x * kx;
                    var worldZ = y * kz;
                    var height = -1f;
                    Cell cell = null;
                    Cell cell2 = null;

                    var worldPos = new Vector3(worldX, 0, worldZ);
                    var worldPosBlended = worldPos + new Vector3(rnd.Gauss(0, BlendingRadius), 0, rnd.Gauss(0, BlendingRadius));
                    var worldPos2 = worldPos.ToVector2();
                    var found = false;

                    for (int i = 0; i < SplatMaps.Length; i++)
                    {
                        var splatMap = SplatMaps[i];
                        found = false;
                        switch (splatMap.Source)
                        {
                            case SplatMapSource.Water:
                                if (height < 0)
                                    height = Terrain.SampleHeight(worldPos);
                                if (height <= waterLevel)
                                    found = true;
                                break;

                            case SplatMapSource.Slope:
                                var terrainPos = terrainSpawner.WorldToTerrain(worldPos);
                                var slope = slopes.GetPixelBilinear(terrainPos.x, terrainPos.y).a;
                                if (slope >= splatMap.SlopeAngle)
                                    found = true;
                                break;

                            case SplatMapSource.Height:
                                if (height < 0)
                                    height = Terrain.SampleHeight(worldPos);
                                if (splatMap.Height.InRange(height - terrainSpawner.WaterLevel))
                                    found = true;
                                break;

                            case SplatMapSource.CellType:
                                if (cell == null)
                                    cell = Map[Builder.PosToHex(worldPosBlended)];
                                if (splatMap.CellType == cell.Type?.Name)
                                    found = true;
                                break;

                            case SplatMapSource.Road:
                                if (cell2 == null)
                                    cell2 = Map[Builder.PosToHex(worldPos)];
                                if (IsRoadSurfaceArea(cell2, worldPos2))
                                    found = rnd.Bool(splatMap.RoadDensity);
                                break;

                            case SplatMapSource.Noise:
                                if (splatMap.NoiseTexture.GetPixelBilinear(worldX * splatMap.NoiseFrequency, worldZ * splatMap.NoiseFrequency).g < splatMap.Amount)
                                    found = true;
                                break;
                        }

                        // if layer found => set layer index and break cycle
                        if (found)
                        {
                            splatmapLayer[y, x] = (byte)i;
                            break;
                        }
                    }

                    //if not found any layer => set default layer
                    if (!found && defaultLayer >= 0)
                        splatmapLayer[y, x] = (byte)defaultLayer;
                }

                if (y % 10 == 0)
                    yield return null;
            }

            // build three-dim splatmap data array
            yield return BuildSplatmapData(splatmapLayer);
        }

        private static bool IsRoadSurfaceArea(Cell cell, Vector2 worldPos)
        {
            if (cell == null || cell.TakenAreas == null)
                return false;

            return cell.TakenAreas.Any(area => IsRoadSurfaceArea(cell, area, worldPos));
        }

        private static bool IsRoadSurfaceArea(Cell cell, TakenArea area, Vector2 worldPos)
        {
            if (area == null)
                return false;

            bool isRoadArea = area.Type == TakenAreaType.Road && cell.Content.HasFlag(CellContent.IsRoad);
            bool isRailwayArea = area.Type == TakenAreaType.Railway;
            if (!isRoadArea && !isRailwayArea)
                return false;

            return area.MinDistanceSq(worldPos) < area.Radius * area.Radius;
        }

        private IEnumerator BuildSplatmapData(byte[,] splatmapLayer)
        {
            var terrainData = Terrain.terrainData;
            var size = terrainData.alphamapWidth;

            // build splatmapData
            var splatmapData = new float[size, size, terrainData.terrainLayers.Length];
            if (SplatmapsFeatures.HasFlag(SplatMapFeatures.Smoothed))
                yield return BuildSplatmapDataSmoothed();
            else
                yield return BuildSplatmapData();

            // flush data
            terrainData.SetAlphamaps(0, 0, splatmapData);

            IEnumerator BuildSplatmapData()
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                        splatmapData[y, x, splatmapLayer[y, x]] = 1;

                    if (y % 50 == 0)
                        yield return null;
                }
            }

            IEnumerator BuildSplatmapDataSmoothed()
            {
                for (int x = 0; x < size; x++)
                {
                    splatmapData[0, x, splatmapLayer[0, x]] = 1;
                    splatmapData[x, 0, splatmapLayer[x, 0]] = 1;
                    splatmapData[size - 1, x, splatmapLayer[size - 1, x]] = 1;
                    splatmapData[x, size - 1, splatmapLayer[x, size - 1]] = 1;
                }

                const float k = 1 / 5f;
                for (int y = 1; y < size - 1; y++)
                {
                    for (int x = 1; x < size - 1; x++)
                    {
                        splatmapData[y, x, splatmapLayer[y, x]] += k;
                        splatmapData[y, x, splatmapLayer[y - 1, x]] += k;
                        splatmapData[y, x, splatmapLayer[y, x - 1]] += k;
                        splatmapData[y, x, splatmapLayer[y + 1, x]] += k;
                        splatmapData[y, x, splatmapLayer[y, x + 1]] += k;
                    }

                    if (y % 50 == 0)
                        yield return null;
                }
            }
        }

        private void OnValidate()
        {
            GrassRockBalance = GrassRockBalance.Clamp(0, 1);

            if (SplatMaps != null)
            {
                if (SplatMaps.Length == 1 && SplatMaps[0].SlopeAngle == 0 && SplatMaps[0].Source == 0)
                    SplatMaps[0] = new SplatMapSettings();

                foreach (var splatMap in SplatMaps)
                {
                    switch (splatMap.Source)
                    {
                        case SplatMapSource.CellType:
                            splatMap.Name = $"Cell {splatMap.CellType}";
                            break;
                        case SplatMapSource.Height:
                            splatMap.Name = $"Height {splatMap.Height}";
                            break;
                        default:
                            splatMap.Name = splatMap.Source.ToString();
                            break;
                    }

                    if (splatMap.NoiseFrequency == 0 && splatMap.Amount == 0)
                    {
                        splatMap.NoiseFrequency = new SplatMapSettings().NoiseFrequency;
                        splatMap.Amount = new SplatMapSettings().Amount;
                    }

                    if (splatMap.NoiseTexture == null)
                        splatMap.NoiseTexture = Resources.Load<Texture2D>("mask2");

                    splatMap.Owner = this;
                }
            }
        }

        #region Fix variable params

        float GrassRockBalance_fixed;
        Color RocksTint_fixed;

        private void FixVariableParams()
        {
            var minMaxCurveRnd = rootRnd.GetBranch(487454);

            GrassRockBalance_fixed = GrassRockBalance.Value(minMaxCurveRnd);
            RocksTint_fixed = RocksTint.Evaluate(minMaxCurveRnd.Float(), minMaxCurveRnd.Float());
        }
        #endregion
    }

    [Serializable]
    public class SplatMapSettings
    {
        [SerializeField, HideInInspector] public string Name;
        [SerializeReference, HideInInspector] public SurfaceSpawner Owner;

        [Tooltip("Data source for splatmap.")]
        public SplatMapSource Source;

        [Tooltip("Terrain layer to spawn. It can be null.")]
        public TerrainLayer TerrainLayer;

        [Tooltip("Cell type where layer is spawned.")]
        [ShowIf(nameof(Source), SplatMapSource.CellType), Popup(nameof(ProposedCellTypes), true)]
        public string CellType;

        [Tooltip("Density of road splatmap.")]
        [ShowIf(nameof(Source), SplatMapSource.Road)]
        [Range(0, 1)]
        public float RoadDensity = 1;

        [Tooltip("Minimal slope angle where slope layer is spawned (degrees).")]
        [ShowIf(nameof(Source), SplatMapSource.Slope)]
        public float SlopeAngle = 45;

        [Tooltip("Height interval where layer is spawned (meters, above water level).")]
        [ShowIf(nameof(Source), SplatMapSource.Height)]
        public RangeFloat Height = new RangeFloat(50, 100);

        [Tooltip("Amount of noise.")]
        [ShowIf(nameof(Source), SplatMapSource.Noise)]
        [Range(0, 1)]
        public float Amount = 0.05f;

        [Tooltip("Noise frequency.")]
        [ShowIf(nameof(Source), SplatMapSource.Noise)]
        [Range(0, 0.05f)]
        public float NoiseFrequency = 0.015f;

        [Tooltip("Noise texture.")]
        [ShowIf(nameof(Source), SplatMapSource.Noise)]
        public Texture2D NoiseTexture;

        IEnumerable<string> ProposedCellTypes => Owner?.ProposedCellTypes()?? MicroWorldHelper.ProposedCellTypes(null);
    }

    [Serializable]
    public enum SplatMapSource
    {
        Default = 0,
        CellType = 1,
        Slope = 2,
        Water = 3,
        Height = 4,
        Noise = 5,
        Road = 6
    }

    [Serializable, Flags]
    public enum SplatMapFeatures
    {
        Smoothed = 0x1, 
        Reserved = 0x2,
    }
}
