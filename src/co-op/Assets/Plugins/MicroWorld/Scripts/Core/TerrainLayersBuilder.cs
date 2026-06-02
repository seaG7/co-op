using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public class TerrainLayersBuilder
    {
        List<DetailPrototype> detailPrototypes = new List<DetailPrototype>();
        List<List<Vector3>> detailLayerToSpawnPositions = new List<List<Vector3>>();
        Dictionary<UnityEngine.Object, int> meshToDetailLayerIndex = new Dictionary<UnityEngine.Object, int>();
        Dictionary<MeshRenderer, GpuMeshRenderer> meshRenderersToGpuMeshRenderer = new Dictionary<MeshRenderer, GpuMeshRenderer>();

        List<TreePrototype> treePrototypes = new List<TreePrototype>();
        List<TreeInstance> treeInstances = new List<TreeInstance>();
        Dictionary<GameObject, int> meshToTreeLayerIndex = new Dictionary<GameObject, int>();

        TerrainData data;
        MicroWorld builder;
        float scaleX;
        float scaleY;
        int detailWidthMinusOne;
        int detailHeightMinusOne;
        internal Dictionary<int, int[,]> CustomLayers = new Dictionary<int, int[,]>();

        public TerrainLayersBuilder(MicroWorld builder, Terrain terrain)
        {
            this.data = terrain.terrainData;
            this.builder = builder;
            scaleX = data.detailWidth / data.size.x;
            scaleY = data.detailHeight / data.size.z;
            detailWidthMinusOne = data.detailWidth - 1;
            detailHeightMinusOne = data.detailHeight - 1;

            // needed adjustments of terrain
#if UNITY_2022_2_OR_NEWER
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
#endif
            terrain.drawInstanced = false;

            // Since terrain details work differently when adding a mesh detail, we immediately create an empty layer with a mesh.
            // create dumb layer
            GetOrCreateDetailPrototype(Resources.Load<GameObject>("Dumb"), out var _, out var _);
        }

        public IEnumerator FlushTerrainLayers()
        {
            // flush details
            data.detailPrototypes = detailPrototypes.ToArray();

            var map = new int[data.detailHeight, data.detailWidth];
            for (int iLayer = 0; iLayer < detailLayerToSpawnPositions.Count; iLayer++)
            {
                if (CustomLayers.TryGetValue(iLayer, out var custom))
                {
                    data.SetDetailLayer(0, 0, iLayer, custom);
                    yield return null;
                    continue;
                }

                var list = detailLayerToSpawnPositions[iLayer];
                for (int i = 0; i < list.Count; i++)
                {
                    var pos = list[i];
                    var mapX = (int)(pos.x * scaleX);
                    var mapY = (int)(pos.z * scaleY);
                    mapX = Mathf.Clamp(mapX, 0, detailWidthMinusOne);
                    mapY = Mathf.Clamp(mapY, 0, detailHeightMinusOne);
                    map[mapY, mapX] += 1;

                    if ((i + 1) % 3000 == 0)
                        yield return null;
                }
                yield return null;
                data.SetDetailLayer(0, 0, iLayer, map);
                Array.Clear(map, 0, data.detailHeight * data.detailWidth);
            }

            // flush trees
            data.treePrototypes = treePrototypes.ToArray();
            data.SetTreeInstances(treeInstances.ToArray(), false);
        }

        public DetailPrototype SpawnDetail(GameObject mesh, Vector3 pos, int count = 1)
        {
            GetOrCreateDetailPrototype(mesh, out var prot, out var index);
            var list = detailLayerToSpawnPositions[index];

            for (int i = 0; i < count; i++)
                list.Add(pos);
            return prot;
        }

        public TreePrototype SpawnTree(GameObject mesh, TreeInstance sample)
        {
            GetOrCreateTreePrototype(mesh, out var prot, out var index);
            sample.position = sample.position.mul(1f / data.size.x, 1f / data.size.y, 1f / data.size.z);
            
            sample.prototypeIndex = index;
            treeInstances.Add(sample);
            return prot;
        }

        public DetailPrototype SpawnGrass(Texture2D grassTexture, Vector3 pos, int count = 1)
        {
            GetOrCreateDetailPrototype(grassTexture, out var prot, out var index);
            var list = detailLayerToSpawnPositions[index];

            for (int i = 0; i < count; i++)
                list.Add(pos);
            return prot;
        }

        internal void GetOrCreateDetailPrototype(UnityEngine.Object gameObjectOrTexture, out DetailPrototype prot, out int index)
        {
            if (meshToDetailLayerIndex.TryGetValue(gameObjectOrTexture, out index))
            {
                prot = detailPrototypes[index];
                return;
            }

            var isMesh  = gameObjectOrTexture is GameObject;

            prot = new DetailPrototype();
            prot.usePrototypeMesh = isMesh;
            prot.renderMode = isMesh ? DetailRenderMode.VertexLit : DetailRenderMode.GrassBillboard;
            prot.useInstancing = isMesh;
            prot.prototype = gameObjectOrTexture as GameObject;
            prot.prototypeTexture = gameObjectOrTexture as Texture2D;

#if UNITY_2022_2_OR_NEWER
            prot.useDensityScaling = true;
#endif
            if (isMesh)
                prot.dryColor = prot.healthyColor = Color.white;
            detailPrototypes.Add(prot);
            detailLayerToSpawnPositions.Add(new List<Vector3>());
            index = detailPrototypes.Count - 1;
            meshToDetailLayerIndex.Add(gameObjectOrTexture, index);
        }

        internal void GetOrCreateTreePrototype(GameObject prefab, out TreePrototype prot, out int index)
        {
            if (meshToTreeLayerIndex.TryGetValue(prefab, out index))
            {
                prot = treePrototypes[index];
                return;
            }

            prot = new TreePrototype();
            prot.prefab = prefab;
            treePrototypes.Add(prot);
            index = treePrototypes.Count - 1;
            meshToTreeLayerIndex.Add(prefab, index);
        }

        struct GpuInstItem
        {
            public Mesh Mesh;
            public Material[] Materials;
            public int Layer;
        }

        public GpuMeshRenderer SpawnGpuInstanced(MeshRenderer mr, Matrix4x4 localToWorldMatrix)
        {
            //TODO: spawn colliders
            //TODO: burst (RenderMeshInstanced)
            //TODO: 1024 insances max
            if (!meshRenderersToGpuMeshRenderer.TryGetValue(mr, out var gpuMr))
            {
                var obj = new GameObject(mr.name, typeof(GpuMeshRenderer));
                obj.transform.SetParent(builder.Terrain.transform);
                gpuMr = obj.GetComponent<GpuMeshRenderer>();
                meshRenderersToGpuMeshRenderer[mr] = gpuMr;

                obj.layer = mr.gameObject.layer;
                gpuMr.Materials = mr.sharedMaterials.ToArray();
                gpuMr.Mesh = mr.GetComponent<MeshFilter>()?.sharedMesh;
                gpuMr.CastShadows = mr.shadowCastingMode;
                gpuMr.ReceiveShadows = mr.receiveShadows;
                gpuMr.DoNotSpawnWhileEnabled = true;
            }

            gpuMr.Matrices.Add(localToWorldMatrix);
            return gpuMr;
        }
    }
}
