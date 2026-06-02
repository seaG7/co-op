using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// Creates collider around terrain to avoid player to leave level
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.1dnvp2vq4i9q")]
    public class BorderColliderSpawner : BaseSpawner
    {
        [Tooltip("Height of the collider over neighboring inside cells (meters).")]
        [SerializeField] float ColliderHeight = 10;
        [Tooltip("Layer of the collider.")]
        [SerializeField, Layer] int ColliderLayer = 0;
        [Tooltip("Type of collider shape. BorderCell makes collider between inside cells and border cells. SimpleBox makes box collider around terrain.")]
        [SerializeField] ColliderType Type = ColliderType.BorderCell;
        [Tooltip("Inset of SimpleBox collider (meters).")]
        [SerializeField, ShowIf(nameof(Type), ColliderType.SimpleBox)] float Inset = 0;

        enum ColliderType
        {
            BorderCell = 1, SimpleBox = 2
        }

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);


            switch (Type)
            {
                case ColliderType.SimpleBox:
                    CheckTerrainSpawner();
                    CreateSimpleBoxCollider();
                    break;

                case ColliderType.BorderCell:
                    CheckMapSpawner();
                    CreateBorderCellCollider();
                    break;
            }

            yield return null;
        }

        private void CreateSimpleBoxCollider()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var waterLevel = Builder.WaterLevel;
            var size = Terrain.terrainData.size;
            var maxH = Map.AllHex().Select(h => Map[h].Height).Max();

            var fwd = Vector3.forward;
            var right = Vector3.right;

            verts.Add(Terrain.transform.position + fwd * Inset + right * Inset);
            verts.Add(Terrain.transform.position + (size.z - Inset) * fwd + right * Inset);
            verts.Add(Terrain.transform.position + (size.z - Inset) * fwd + (size.x - Inset) * right);
            verts.Add(Terrain.transform.position + fwd * Inset + (size.x - Inset) * right);

            for (int i = 0; i < 4; i++)
                verts.Add(verts[i] + Vector3.up * (maxH + ColliderHeight));
            
            for (int i = 0; i < 4; i++)
            {
                var i0 = i;
                var i1 = i + 4;
                var i2 = (i + 1) % 4 + 4;
                var i3 = (i + 1) % 4;
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                tris.Add(i0); tris.Add(i2); tris.Add(i3);
            }

            // bottom
            {
                tris.Add(0); tris.Add(1); tris.Add(2);
                tris.Add(0); tris.Add(2); tris.Add(3);
            }

            CreateMesh(verts, tris);
        }

        private void CreateBorderCellCollider()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var waterLevel = Builder.WaterLevel;

            foreach (var hex in Map.AllHex())
            {
                var type = Map[hex].Type;
                var isGate = type == Builder.MapSpawner.GateCellType;
                var isBorder = type == Builder.MapSpawner.BorderCellType;
                if (!isBorder && !isGate)
                    continue;

                for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge++)
                {
                    var opposite = CellGeometry.Neighbor(hex, iEdge);
                    var oppositeIsBorderOrOutside = Map.IsBorderOrOutside(opposite);
                    if (!oppositeIsBorderOrOutside && isGate) continue;
                    if (oppositeIsBorderOrOutside && isBorder) continue;

                    // calc min/max height of 
                    if (!GetMinMaxNeighborHeight(hex, out var minH, out var maxH)) continue;

                    minH = Mathf.Min(minH, Map[hex].Height);

                    var y0 = minH;
                    var y1 = maxH;
                    if (y1 < waterLevel)
                        y1 += (waterLevel - y1);

                    y0 -= 3;
                    y1 += ColliderHeight;

                    var p0 = CellGeometry.Corner(hex, iEdge + 1).withSetY(y0);
                    var p1 = p0.withSetY(y1);
                    var p3 = CellGeometry.Corner(hex, iEdge).withSetY(y0);
                    var p2 = p3.withSetY(y1);

                    if (oppositeIsBorderOrOutside)
                        (p0, p1, p2, p3) = (p3, p2, p1, p0);// swap face of collider

                    var i = verts.Count;
                    verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
                }
            }

            CreateMesh(verts, tris);
        }

        private void CreateMesh(List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh();
            if (verts.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            var go = new GameObject("BorderCollider", typeof(MeshCollider));
            go.transform.SetParent(Terrain.transform);
            go.layer = ColliderLayer;
            var mc = go.GetComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }

        bool GetMinMaxNeighborHeight(Vector2Int hex, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;

            foreach(var n in CellGeometry.NeighborsEx(hex))
            {
                if (Map.IsBorderOrOutside(n)) continue;
                var h = Map[n].Height;
                if (h < min) min = h;
                if (h > max) max = h;
            }

            return min != float.MaxValue;
        }
    }
}
