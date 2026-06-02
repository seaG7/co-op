using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// This spawner is designed to spawn grass covering across the whole terrain.
    /// Grass spawner allows to spawn several grass prefabs that are distributed by “islands”
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.opcheq4g9mac")]
    public class GrassSpawner : BaseSpawner
    {
        [Tooltip("Height above water where spawning is allowed (meters).")]
        [SerializeField] RangeFloat HeightAboveWater = new RangeFloat(0, 200);
        [Tooltip("List of cell types where spawning is allowed, If list is empty - spawning allowed in any cell type.")]
        [SerializeField, Popup(nameof(ProposedCellTypes), true)] string[] CellTypes = new string[0];
        [Tooltip("Maximum slope angle where grass can be spawned (degrees).")]
        [SerializeField, Range(0, 90)] float MaxSlope = 40;

        public override int Order => 1400;

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);
            if (Prefabs == null || Prefabs.Count == 0)
                yield break;

            CheckTerrainSpawner();

            var data = Terrain.terrainData;
            var h = data.detailHeight;
            var w = data.detailWidth;
            var scaleX = data.size.x / data.detailWidth;
            var scaleY = data.size.z / data.detailHeight;
            var hRes = data.heightmapResolution;
            var waterLevel = builder.TerrainSpawner.WaterLevel;

            var slopes = Builder.TerrainSpawner.Slopes.GetPixels();
            var islands = Noises.Islands;
            var islCount = Prefabs.Count;
            var iIsland = 0;

            // prepare
            Cell[,] mapToCell = new Cell[h + 4, w + 4];

            for (int i = 0; i < w; i += 4)
            {
                for (int j = 0; j < h; j += 4)
                {
                    var pos = new Vector3(i * scaleX, 0, j * scaleY);
                    var hex = CellGeometry.PointToHex(pos);
                    var cell = Map[hex];
                    for (int a = 0; a < 4; a++)
                        for (int b = 0; b < 4; b++)
                            mapToCell[j + a, i + b] = cell;
                }

                if (i % 16 == 0)
                    yield return null;
            }

            // spawn
            foreach (var prefab in Prefabs)
            {
                TerrainLayersBuilder.GetOrCreateDetailPrototype(prefab.Texture, out var layer, out int iLayer);
                layer.minWidth = prefab.ScaleXZ.Min * Preferences.Instance.ScaleGrassWidth;
                layer.maxWidth = prefab.ScaleXZ.Max * Preferences.Instance.ScaleGrassWidth;
                layer.minHeight = prefab.ScaleY.Min * Preferences.Instance.ScaleGrassHeight;
                layer.maxHeight = prefab.ScaleY.Max * Preferences.Instance.ScaleGrassHeight;
                layer.renderMode = prefab.RenderMode;
                layer.healthyColor = prefab.HealthyColor.withAlpha(0.1f);
                layer.dryColor = prefab.DryColor.withAlpha(0.1f);
                layer.noiseSpread = 0.2f;

#if UNITY_2022_2_OR_NEWER
                layer.alignToGround = 1 - prefab.Verticality;
                layer.positionJitter = 0.5f;
                layer.density = prefab.Density * 100;
#endif

                var densityScale = Mathf.Max(Terrain.terrainData.size.x, Terrain.terrainData.size.z) / 277f;
                densityScale /= Builder.DetailResolutionScale;
                densityScale *= densityScale;

                var density = prefab.Density * densityScale;
                var rnd = rootRnd.GetBranch(iLayer * 13, 927633);
                //var bunch = Mathf.CeilToInt(prefab.Bunch * 2 * Mathf.Sqrt(densityScale)) + 1;
                var bunch = Mathf.CeilToInt(prefab.Bunch * 3) + 1;
                var Map = base.Map;

                var map = new int[h, w];
                for (int i = 0; i < w; i ++)
                {
                    var hx = i * hRes / w;

                    for (int j = 0; j < h; j ++)
                    {
                        if (!rnd.Bool(density))
                            continue;

                        if ((islands[i, j] % islCount) != iIsland)
                            continue;

                        var hy = j * hRes / h;
                        var iSlope = hx + hy * hRes;
                        if (iSlope >= 0 && iSlope < hRes * hRes && slopes[iSlope].a > MaxSlope)
                            continue;

                        var H = data.GetHeight(hx, hy) - waterLevel;
                        if (!HeightAboveWater.InRange(H))
                            continue;

                        // check cell type
                        var cell = mapToCell[j, i];

                        if (CellTypes.Length > 0)
                        {
                            if (!CellTypes.CheckCellType(cell.Type.Name))//TODO: make hash
                                continue;
                        }

                        // check taken area
                        if (cell.TakenAreas.Count > 0)
                        if (cell.TakenAreas.HasIntersection(new Vector2(i * scaleX, j * scaleY), rnd.Float(-0.5f, 0)))
                            continue;

                        //var d = 1 + islands[i + 13, j] % 3;
                        //map[j, i] = Mathf.CeilToInt(d * bunch);
                        map[j, i] = rnd.Int(1, bunch);
                    }
                    yield return null;
                }

                iIsland++;
                TerrainLayersBuilder.CustomLayers[iLayer] = map;

                yield return null;
            }
        }


#if UNITY_EDITOR
        public void OnValidate()
        {
            if (DragHereTextureToAddToList != null)
                Prefabs.Add(new GrassPrefab() { Texture = DragHereTextureToAddToList });
            DragHereTextureToAddToList = null;
        }
#endif

        #region Prefab definitions
        [SerializeField] Texture2D DragHereTextureToAddToList;

        [Space]
        public List<GrassPrefab> Prefabs;

        [Serializable]
        public class GrassPrefab
        {
            [Tooltip("Grass texture.")]
            public Texture2D Texture;
            [Tooltip("Density of grass covering. It is a normalized value from 0 to 1.")]
            [Range(0, 1)] public float Density = 0.5f;
            [Tooltip("How many instances of grass billboards will be spawned in one place.")]
            public float Bunch = 1;
            [Tooltip("Tint color for healthy grass.")]
            public Color HealthyColor = new Color(0.3034024f, 0.5280203f, 0.1587332f, 0.1019608f);
            [Tooltip("Tint color for dry grass.")]
            public Color DryColor = new Color(0.491376f, 0.5212617f, 0.09680995f, 0.1019608f);
            [Tooltip("How grass will be aligned relative to surface normal. From 0 (fully aligned to surface normal) to 1 (strictly vertical).")]
            [Range(0, 1)] public float Verticality = 0.4f;
            [Tooltip("Size of grass on the XZ plane (meters).")]
            public RangeFloat ScaleXZ = new RangeFloat(0.6f, 0.7f);
            [Tooltip("Size of grass by the Y axis (meters).")]
            public RangeFloat ScaleY = new RangeFloat(0.5f, 0.6f);
            [Tooltip("Render mode of grass.")]
            public DetailRenderMode RenderMode = DetailRenderMode.Grass;
        }

        #endregion
    }
}


