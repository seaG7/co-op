using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Buildings
{
    [Serializable]
    class Shapes
    {
        const int goodDividend = 48;
        const int goodDividendDepth = 12;
        public const int GraphWidth = (goodDividend + 4) * GridStep;
        public const int GraphHeight = (goodDividend + 24) * GridStep;
        public const int GraphDepth = (goodDividendDepth + 16) * GridStep;
        public const int GridStep = 5; // Grid step
        public static Vector3Int FrameSizeInGridUnits = new Vector3Int(goodDividend, goodDividend, goodDividendDepth); // Frame size
        public Vector3 FrameSize => FrameSizeInGridUnits * GridStep; // Frame size
        public List<Shape> Items = new List<Shape>();
        public int SelectedShapeIndex = -1;
        public float Zoom = 1;
        public static readonly Vector3 TypicalSize = new Vector3(4, 5, 1);

        public Shapes()
        {
            var shape = new Shape();
            shape.CreateCubePoints(FrameSize);
            Items.Add(shape);
        }

        public IEnumerable<Shape> GetActiveShapes()
        {
            for(int i = 0; i < Items.Count; i++)
            {
                if (SelectedShapeIndex == i || SelectedShapeIndex < 0)
                    yield return Items[i];
            }
        }

        public bool IsShapeSelected(Shape s)
        {
            var i = Items.IndexOf(s);
            return SelectedShapeIndex == i || SelectedShapeIndex == -1;
        }

        public Vector3 GetNormalized(Vector3 p)
        {
            return new Vector3(p.x / FrameSize.x, -p.y / FrameSize.y + 0.5f, p.z / FrameSize.z);
        }
    }

    [Serializable]
    class Shape
    {
        /// <summary> Points in Grid coordinates </summary>
        public List<ShapePoint> Points = new List<ShapePoint>();
        public bool[] VisibleFaces;
        public ShapeType Type;
        public ShapeFeatures Features;
        public int MaterialIndex;
        public Material Material;
        public UnityEngine.Object Prefab;
        public float MeshScale = 1;

        public void CreateCubePoints(Vector3 frameSize)
        {
            var x0 = -frameSize.x / 2f;
            var x1 = frameSize.x / 2f;
            var y0 = -frameSize.y / 2f;
            var y1 = frameSize.y / 2f;
            var z0 = -frameSize.z / 2f;
            var z1 = frameSize.z / 2f;
            Points = new List<ShapePoint>();
            VisibleFaces = new bool[] { true, true, true, true, true, true };

            // top face
            Points.Add(new ShapePoint(x0, y1, z0));//0
            Points.Add(new ShapePoint(x0, y1, z1));//1
            Points.Add(new ShapePoint(x1, y1, z1));//2
            Points.Add(new ShapePoint(x1, y1, z0));//3

            // bottom face
            Points.Add(new ShapePoint(x0, y0, z0));//4
            Points.Add(new ShapePoint(x0, y0, z1));//5
            Points.Add(new ShapePoint(x1, y0, z1));//6
            Points.Add(new ShapePoint(x1, y0, z0));//7
        }

        static int[] CubeEdgePoints = new int[]
          { 0, 1, 2, 3,// face 0
            7, 6, 5, 4,// face 1
            0, 4, 5, 1,// face 2
            1, 5, 6, 2,// face 3
            3, 2, 6, 7,// face 4
            0, 3, 7, 4,// face 5
            };

        public static int[] FrontalFaces = new int[] { 0, 1, 3, 5 };
        public static int[] SideFaces = new int[] { 0, 1, 2, 4 };
        public static int[] AllFaces = new int[] { 0, 1, 2, 3, 4, 5 };

        public IEnumerable<ShapePoint> GetCubeFace(int iFace)
        {
            for (int i = 0; i < 4; i++)
                yield return Points[CubeEdgePoints[i + iFace * 4]];
        }

        public IEnumerable<(ShapePoint, ShapePoint)> GetEdges(bool frontal, bool side)
        {
            if (frontal)
            {
                // top face
                yield return (Points[3], Points[0]);
                yield return (Points[1], Points[2]);

                // bottom face
                yield return (Points[6], Points[5]);
                yield return (Points[4], Points[7]);


                // vertical
                yield return (Points[4], Points[0]);
                yield return (Points[3], Points[7]);
                yield return (Points[6], Points[2]);
                yield return (Points[1], Points[5]);
            }

            if (side)
            {
                yield return (Points[0], Points[1]);
                yield return (Points[2], Points[3]);
                yield return (Points[5], Points[4]);
                yield return (Points[7], Points[6]);
            }
        }

        public (List<Vector3> verts, List<int> tris, Bounds bounds, Material mat) GetVerticiesNormalized(Shapes shapes, Material[] mats)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var mat = Material;

            if (mat == null && mats != null && MaterialIndex >= 0 && MaterialIndex < mats.Length)
                mat = mats[MaterialIndex];
            if (mat == null && mats != null && mats.Length > 0)
                mat = mats[0];

            // calc bounds
            Bounds bounds = new Bounds(shapes.GetNormalized(Points[0].Point), Vector3.zero);
            foreach (var sp in Points)
            {
                var v = shapes.GetNormalized(sp.Point);
                bounds.Encapsulate(v);
            }

            // calc verts and triangles
            switch (Type)
            {
                case ShapeType.Box:
                    foreach (var iFace in AllFaces)
                    if (VisibleFaces[iFace])
                        Triangulate(GetCubeFace(iFace).Select(f=>f.Point).ToArray());
                    break;

                case ShapeType.Cylinder4:
                    // top face
                    if (VisibleFaces[0])
                        Triangulate(GetCubeFace(0).Select(f => f.Point).ToArray());

                    // bottom face
                    if (VisibleFaces[1])
                        Triangulate(GetCubeFace(1).Select(f => f.Point).ToArray());

                    // side faces
                    var hash = new Dictionary<Vector3, int>();
                    for (int i = 2; i < 6; i++)
                    if (VisibleFaces[i])
                        TriangulateSmoothed(GetCubeFace(i).Select(f => f.Point).ToArray(), hash);

                    break;

                case ShapeType.Cylinder8:
                {
                    // top and bottom faces
                    var top = GenerateOctagon(GetCubeFace(0).Select(s => s.Point).ToArray());
                    var bottom = GenerateOctagon(GetCubeFace(1).Select(s => s.Point).ToArray());

                    // top face
                    if (VisibleFaces[0])
                        Triangulate(top);

                    // bottom face
                    if (VisibleFaces[1])
                        Triangulate(bottom);

                    bottom = GenerateOctagon(GetCubeFace(1).Reverse().Select(s => s.Point).ToArray());

                    // side faces
                    var hash2 = new Dictionary<Vector3, int>();
                    for (int iSide = 0; iSide < 8; iSide++)
                        TriangulateSmoothed(new Vector3[] { top[iSide], top[(iSide + 1) % top.Length], bottom[(iSide + 1) % bottom.Length], bottom[iSide] }, hash2, true);

                    break;
                }

                case ShapeType.Hex:
                {
                    // top and bottom faces
                    var top = GenerateHexagon(GetCubeFace(0).Select(s => s.Point).ToArray());
                    var bottom = GenerateHexagon(GetCubeFace(1).Select(s => s.Point).ToArray());

                    // top face
                    if (VisibleFaces[0])
                        Triangulate(top);

                    // bottom face
                    if (VisibleFaces[1])
                        Triangulate(bottom);

                    bottom = GenerateHexagon(GetCubeFace(1).Reverse().Select(s => s.Point).ToArray());

                    // side faces
                    for (int iSide = 0; iSide < 6; iSide++)
                        Triangulate(new Vector3[] { top[iSide], top[(iSide + 1) % top.Length], bottom[(iSide + 1) % bottom.Length], bottom[iSide] }, true);

                    break;
                }

                case ShapeType.Mesh:
                {
                    var mesh = Prefab as Mesh;
                    if (mesh == null)
                        break;
                    verts.AddRange(mesh.vertices.Select(v => v * MeshScale));
                    tris.AddRange(mesh.triangles);
                    break;
                }
                case ShapeType.ScaledMesh:
                {
                    var mesh = Prefab as Mesh;
                    if (mesh == null)
                        break;
                    // normalize mesh to 1
                    //mesh.RecalculateBounds();
                    var c = mesh.bounds.center;
                    var s = bounds.size.div(mesh.bounds.size);
                    //if (Features.HasFlag(ShapeFeatures.KeepAspectXZ))
                        //s = Vector3.one;
                    verts.AddRange(mesh.vertices.Select(v=>((v - c).mul(s) + bounds.center)));
                    tris.AddRange(mesh.triangles);
                    break;
                }
            }

            return (verts, tris, bounds, mat);

            void Triangulate(IList<Vector3> points, bool reverse = false)
            {
                var start = verts.Count;
                foreach (var p in points)
                    verts.Add(shapes.GetNormalized(p));

                var dx1 = reverse ? 1 : 0;
                var dx2 = reverse ? 0 : 1;
                for (int i = 2; i < points.Count; i++)
                {
                    tris.Add(start); tris.Add(start + i - dx1); tris.Add(start + i - dx2);
                }
            }

            void TriangulateSmoothed(IList<Vector3> points, Dictionary<Vector3, int> hash, bool reverse = false)
            {
                var start = verts.Count;
                var indicies = new int[points.Count];

                for (int i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    if (hash.TryGetValue(p, out var ind))
                    {
                        indicies[i] = ind;
                        continue;
                    }

                    indicies[i] = hash[p] = verts.Count;
                    verts.Add(shapes.GetNormalized(p));
                }

                var dx1 = reverse ? 1 : 0;
                var dx2 = reverse ? 0 : 1;
                for (int i = 2; i < points.Count; i++)
                {
                    tris.Add(indicies[0]); tris.Add(indicies[i - dx1]); tris.Add(indicies[i - dx2]);
                }
            }
        }

        public static Vector3[] GenerateHexagon(Vector3[] quad, float inset = 0.25f)
        {
            // Linear interpolation between two points
            Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + t * (b - a);

            var size = (quad[1] - quad[2]).magnitude / 2f;
            var d = (quad[1] - quad[0]).normalized * size * (2 - Mathf.Sqrt(3)) * 0;

            // Calculate the vertices of the hexagon
            Vector3[] hexagon = new Vector3[6];
            hexagon[0] = Lerp(quad[0], quad[1], 0.5f);  // Edge from p0 to p1
            hexagon[1] = Lerp(quad[1], quad[2], inset) - d;  // Edge from p1 to p2
            hexagon[2] = Lerp(quad[2], quad[1], inset) - d;  // Edge from p2 to p3
            hexagon[3] = Lerp(quad[2], quad[3], 0.5f);  // Edge from p3 to p2
            hexagon[4] = Lerp(quad[3], quad[0], inset) + d;  // Edge from p3 to p0
            hexagon[5] = Lerp(quad[0], quad[3], inset) + d;  // Edge from p0 to p3

            return hexagon;
        }

        public static Vector3[] GenerateOctagon(Vector3[] quad, float inset = 0.2929f)
        {
            // Linear interpolation between two points
            Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + t * (b - a);

            // Calculate the vertices of the octagon
            Vector3[] octagon = new Vector3[8];
            octagon[0] = Lerp(quad[0], quad[1], inset);  // Edge from p0 to p1
            octagon[1] = Lerp(quad[1], quad[0], inset);  // Edge from p1 to p0
            octagon[2] = Lerp(quad[1], quad[2], inset);  // Edge from p1 to p2
            octagon[3] = Lerp(quad[2], quad[1], inset);  // Edge from p2 to p1
            octagon[4] = Lerp(quad[2], quad[3], inset);  // Edge from p2 to p3
            octagon[5] = Lerp(quad[3], quad[2], inset);  // Edge from p3 to p2
            octagon[6] = Lerp(quad[3], quad[0], inset);  // Edge from p3 to p0
            octagon[7] = Lerp(quad[0], quad[3], inset);  // Edge from p0 to p3

            return octagon;
        }
    }

    [Flags]
    enum ShapeFeatures
    {
        None = 0x0,
        KeepAspectXZ = 0x1,
        //Smoothed = 0x2,
        Reserved = 0x4,
    }

    [Serializable]
    enum ShapeType : byte
    {
        Box = 0,
        Hex = 1,
        Cylinder4 = 5,
        Cylinder8 = 6,
        Mesh = 10,
        ScaledMesh = 11,
        GameObject = 20,
        DoNotSpawn = 255
    }

    [Serializable]
    class ShapePoint
    {
        public Vector3 Point;
        public Vector2Int draggedAxes;

        public ShapePoint(float x, float y, float z)
        {
            Point = new Vector3(x, y, z);
        }

        public ShapePoint(Vector3 point)
        {
            Point = point;
        }
    }
}