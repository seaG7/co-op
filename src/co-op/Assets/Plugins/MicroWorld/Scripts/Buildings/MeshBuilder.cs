using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MicroWorldNS.MeshBuilderNS
{
    class MeshBuilder
    {
        public List<(List<Vector3>, List<int>, Material)> SubMeshes = new List<(List<Vector3>, List<int>, Material)>();
        public List<Element> Elements = new List<Element>();
        public List<GameObject> GameObjects = new List<GameObject>();
        public Material DefaultMaterial;
        static Dictionary<ElementType, (Vector3[], int[])> data = new Dictionary<ElementType, (Vector3[], int[])>();

        static MeshBuilder()
        {
            var contour = new List<Vector3>
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, -1),
                new Vector2(0, 0)
            };
            data[ElementType.Prism] = Extrude(contour);

            contour = new List<Vector3>
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 0),
                new Vector2(0, 0)
            };
            data[ElementType.Ramp] = Extrude(contour);
        }

        public GameObject Build(MicroWorld builder)
        {
            //
            var verticies = new List<Vector3>();
            var normals = new List<Vector3>();

            Dictionary<Material, List<int>> matToTriangles = new Dictionary<Material, List<int>>();

            BuildSubmeshes(verticies, matToTriangles);

            BuildSimpleElements(verticies, normals, matToTriangles);

            var mesh = new Mesh();
            if (verticies.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(verticies);
            mesh.SetUVs(0, verticies.Select(v => new Vector2(1, 1)).ToArray());

            mesh.subMeshCount = matToTriangles.Count;
            var mats = matToTriangles.Keys.ToArray();
            for (int i = 0; i < mats.Length; i++)
                mesh.SetTriangles(matToTriangles[mats[i]], i);

            if (normals.Count > 0)
                mesh.SetNormals(normals);
            else
                mesh.RecalculateNormals();

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            //
            var obj = new GameObject("Building", typeof(MeshRenderer), typeof(MeshFilter), typeof(MeshCollider));
            obj.transform.SetParent(builder?.Terrain?.transform);
            var mr = obj.GetComponent<MeshRenderer>();
            var mf = obj.GetComponent<MeshFilter>();
            var mc = obj.GetComponent<MeshCollider>();
            mf.sharedMesh = mesh;
            mc.sharedMesh = mesh;
            mr.sharedMaterials = mats;

            foreach (var go in GameObjects)
                go.transform.SetParent(obj.transform, true);

            return obj;
        }

        private void BuildSubmeshes(List<Vector3> verticies, Dictionary<Material, List<int>> matToTriangles)
        {
            foreach (var el in SubMeshes)
            {
                var mat = el.Item3;
                if (mat == null)
                    mat = DefaultMaterial;

                if (!matToTriangles.TryGetValue(mat, out var tris))
                    matToTriangles[mat] = tris = new List<int>();

                var startVert = verticies.Count;
                verticies.AddRange(el.Item1);

                var elemTris = el.Item2;
                for (int i = 0; i < elemTris.Count; i++)
                    tris.Add(elemTris[i] + startVert);
            }
        }

        private void BuildSimpleElements(List<Vector3> verticies, List<Vector3> normals, Dictionary<Material, List<int>> matToTriangles)
        {
            Vector3[] elemVert = default;
            int[] elemTris = default;
            Vector3[] elemNormals = default;

            foreach (var el in Elements)
            {
                var mat = el.Material == null ? DefaultMaterial : el.Material;

                if (!matToTriangles.TryGetValue(mat, out var tris))
                    matToTriangles[mat] = tris = new List<int>();

                switch (el.Type)
                {
                    case ElementType.Box:
                        elemVert = cubeVert;
                        elemTris = cubeTriangles;
                        break;

                    case ElementType.SmoothedBox:
                        elemVert = smoothedCubeVert;
                        elemTris = smoothedCubeTriangles;
                        break;
                    case ElementType.Hex:
                        elemVert = hexVertices;
                        elemTris = hexTris;
                        break;
                    default:
                        if (data.TryGetValue(el.Type, out var pair))
                        {
                            elemVert = pair.Item1;
                            elemTris = pair.Item2;
                        }
                        break;
                }

                if (el.SkewZ != 1f || el.SkewX != 1f)
                {
                    var newElemVert = new Vector3[elemVert.Length];
                    var skewZ = el.SkewZ;
                    var skewX = el.SkewX;

                    if (skewZ >= 0f)
                        SkewTop();
                    else
                    {
                        skewZ = Mathf.Abs(skewZ);
                        skewX = Mathf.Abs(skewX);
                        SkewBottom();
                    }

                    elemVert = newElemVert;

                    void SkewTop()
                    {
                        for (int i = 0; i < elemVert.Length; i++)
                        {
                            var v = elemVert[i];
                            if (v.y > 0.1f)
                            {
                                v.z *= skewZ;
                                v.x *= skewX;
                            }
                            newElemVert[i] = v;
                        }
                    }

                    void SkewBottom()
                    {
                        for (int i = 0; i < elemVert.Length; i++)
                        {
                            var v = elemVert[i];
                            if (v.y < 0.9f)
                            {
                                v.z *= skewZ;
                                v.x *= skewX;
                            }
                            newElemVert[i] = v;
                        }
                    }
                }

                var startVert = verticies.Count;
                for (int i = 0; i < elemVert.Length; i++)
                    verticies.Add(el.Transform.MultiplyPoint3x4(elemVert[i]));

                if (elemNormals != null)
                    for (int i = 0; i < elemNormals.Length; i++)
                        normals.Add(el.Transform.MultiplyVector(elemNormals[i]));

                for (int i = 0; i < elemTris.Length; i++)
                    tris.Add(elemTris[i] + startVert);
            }
        }

        static (Vector3[], int[]) Extrude(List<Vector3> contour)
        {
            List<Vector3> verticies = new List<Vector3>();
            List<int> tris = new List<int>();
            var fwd = Vector3.forward / 2;

            // front face
            verticies.AddRange(contour.Select(v => v - fwd));

            for (int i = 1; i < verticies.Count - 1; i++)
            {
                tris.Add(0); tris.Add(i); tris.Add(i + 1);
            }

            // back face
            var start = verticies.Count;
            verticies.AddRange(contour.Select(v => v + fwd));

            for (int i = start + 1; i < verticies.Count - 1; i++)
            {
                tris.Add(start); tris.Add(i + 1); tris.Add(i);
            }

            // side face
            start = verticies.Count;
            var stride = contour.Count;
            verticies.AddRange(contour.Select(v => v - fwd));
            verticies.AddRange(contour.Select(v => v + fwd));

            for (int i = 1; i < contour.Count - 2; i++)
            {
                var ii = start + i;
                tris.Add(ii); tris.Add(ii + stride); tris.Add(ii + stride + 1);
                tris.Add(ii); tris.Add(ii + stride + 1); tris.Add(ii + 1);
            }

            return (verticies.ToArray(), tris.ToArray());
        }

        public struct Element
        {
            public Matrix4x4 Transform;
            public Material Material;
            public ElementType Type;
            public float SkewZ;
            public float SkewX;

            public Element(Vector3 pos, Vector3 forward, Vector3 scale, Material mat, ElementType type, float skewZ = 1f, float skewX = 1f)
            {
                Transform = Matrix4x4.TRS(pos, Quaternion.LookRotation(forward, Vector3.up), scale);
                Material = mat;
                Type = type;
                SkewZ = skewZ;
                SkewX = skewX;
            }
        }

        public enum ElementType : byte
        {
            Box, SmoothedBox, Cylinder, Hex, Prism, Ramp
        }

        #region Data

        // smoothed Box
        static readonly Vector3[] smoothedCubeVert = new Vector3[]
        {
            // Bottom face
            new Vector3(-0.5f, 0f,  0.5f), // 0
            new Vector3( 0.5f, 0f,  0.5f), // 1
            new Vector3( 0.5f, 0f, -0.5f), // 2
            new Vector3(-0.5f, 0f, -0.5f), // 3

            // Top face
            new Vector3(-0.5f, 1f,  0.5f), // 4
            new Vector3( 0.5f, 1f,  0.5f), // 5
            new Vector3( 0.5f, 1f, -0.5f), // 6
            new Vector3(-0.5f, 1f, -0.5f)  // 7
        };

        // Define triangles (each face of the cube consists of two triangles)
        static readonly int[] smoothedCubeTriangles = new int[]
        {
            // Bottom face
            0, 2, 1,
            0, 3, 2,

            // Top face
            4, 5, 6,
            4, 6, 7,

            // Front face
            0, 1, 5,
            0, 5, 4,

            // Back face
            2, 3, 7,
            2, 7, 6,

            // Left face
            0, 4, 7,
            0, 7, 3,

            // Right face
            1, 2, 6,
            1, 6, 5
        };

        //Box
        public static Vector3[] cubeVert = new Vector3[]
        {
            // Bottom face
            new Vector3(-0.5f, 0f,  0.5f), // 0
            new Vector3( 0.5f, 0f,  0.5f), // 1
            new Vector3( 0.5f, 0f, -0.5f), // 2
            new Vector3(-0.5f, 0f, -0.5f), // 3

            // Top face
            new Vector3(-0.5f, 1f,  0.5f), // 4
            new Vector3( 0.5f, 1f,  0.5f), // 5
            new Vector3( 0.5f, 1f, -0.5f), // 6
            new Vector3(-0.5f, 1f, -0.5f), // 7

            // Front face
            new Vector3(-0.5f, 0f,  0.5f), // 8
            new Vector3( 0.5f, 0f,  0.5f), // 9
            new Vector3( 0.5f, 1f,  0.5f), // 10
            new Vector3(-0.5f, 1f,  0.5f), // 11

            // Back face
            new Vector3( 0.5f, 0f, -0.5f), // 12
            new Vector3(-0.5f, 0f, -0.5f), // 13
            new Vector3(-0.5f, 1f, -0.5f), // 14
            new Vector3( 0.5f, 1f, -0.5f), // 15

            // Left face
            new Vector3(-0.5f, 0f, -0.5f), // 16
            new Vector3(-0.5f, 0f,  0.5f), // 17
            new Vector3(-0.5f, 1f,  0.5f), // 18
            new Vector3(-0.5f, 1f, -0.5f), // 19

            // Right face
            new Vector3( 0.5f, 0f,  0.5f), // 20
            new Vector3( 0.5f, 0f, -0.5f), // 21
            new Vector3( 0.5f, 1f, -0.5f), // 22
            new Vector3( 0.5f, 1f,  0.5f), // 23
        };

        // Define triangles (each face of the cube consists of two triangles)
        public static int[] cubeTriangles = new int[]
        {
            // Bottom face
            0, 2, 1,
            0, 3, 2,

            // Top face
            4, 5, 6,
            4, 6, 7,

            // Front face
            8, 9, 10,
            8, 10, 11,

            // Back face
            12, 13, 14,
            12, 14, 15,

            // Left face
            16, 17, 18,
            16, 18, 19,

            // Right face
            20, 21, 22,
            20, 22, 23
        };

        //Hex
        // Define the vertices of a hexagon with pivot at the bottom center
        const float hexRadius = 1.154700538f / 2f;  // Radius of the hexagon
        const float hexHeight = 1.0f;  // Height of the prism

        static Vector3[] hexVertices = new Vector3[]
        {
            // Bottom face (each vertex unique for flat shading)
            GetHexVertex(0, hexRadius, 0), // 0
            GetHexVertex(1, hexRadius, 0), // 1
            GetHexVertex(2, hexRadius, 0), // 2
            GetHexVertex(3, hexRadius, 0), // 3
            GetHexVertex(4, hexRadius, 0), // 4
            GetHexVertex(5, hexRadius, 0), // 5

            // Top face (each vertex unique for flat shading, offset by height)
            GetHexVertex(0, hexRadius, hexHeight), // 6
            GetHexVertex(1, hexRadius, hexHeight), // 7
            GetHexVertex(2, hexRadius, hexHeight), // 8
            GetHexVertex(3, hexRadius, hexHeight), // 9
            GetHexVertex(4, hexRadius, hexHeight), // 10
            GetHexVertex(5, hexRadius, hexHeight), // 11

            // Side faces (each face has its own set of 4 vertices to avoid smoothing)
            GetHexVertex(0, hexRadius, 0), GetHexVertex(1, hexRadius, 0), GetHexVertex(1, hexRadius, hexHeight), GetHexVertex(0, hexRadius, hexHeight), // Side 1
            GetHexVertex(1, hexRadius, 0), GetHexVertex(2, hexRadius, 0), GetHexVertex(2, hexRadius, hexHeight), GetHexVertex(1, hexRadius, hexHeight), // Side 2
            GetHexVertex(2, hexRadius, 0), GetHexVertex(3, hexRadius, 0), GetHexVertex(3, hexRadius, hexHeight), GetHexVertex(2, hexRadius, hexHeight), // Side 3
            GetHexVertex(3, hexRadius, 0), GetHexVertex(4, hexRadius, 0), GetHexVertex(4, hexRadius, hexHeight), GetHexVertex(3, hexRadius, hexHeight), // Side 4
            GetHexVertex(4, hexRadius, 0), GetHexVertex(5, hexRadius, 0), GetHexVertex(5, hexRadius, hexHeight), GetHexVertex(4, hexRadius, hexHeight), // Side 5
            GetHexVertex(5, hexRadius, 0), GetHexVertex(0, hexRadius, 0), GetHexVertex(0, hexRadius, hexHeight), GetHexVertex(5, hexRadius, hexHeight)  // Side 6
        };

        // Helper function to calculate the position of a vertex in a hexagon
        static Vector3 GetHexVertex(int i, float radius, float yOffset)
        {
            float angleDeg = -60 * i;  // Each angle between hexagon vertices is 60 degrees
            float angleRad = Mathf.Deg2Rad * angleDeg;
            return new Vector3(radius * Mathf.Cos(angleRad), yOffset, radius * Mathf.Sin(angleRad));
        }

        // Define triangles (each face of the hexagonal prism consists of two triangles)
        int[] hexTris = new int[]
        {
            // Bottom face (clockwise)
            0, 2, 1,
            0, 3, 2,
            0, 4, 3,
            0, 5, 4,

            // Top face (counterclockwise)
            6, 7, 8,
            6, 8, 9,
            6, 9, 10,
            6, 10, 11,

            // Side faces (each face uses its own set of vertices for flat shading)
            12, 14, 15, 12, 13, 14, // Side 1
            16, 18, 19, 16, 17, 18, // Side 2
            20, 22, 23, 20, 21, 22, // Side 3
            24, 26, 27, 24, 25, 26, // Side 4
            28, 30, 31, 28, 29, 30, // Side 5
            32, 34, 35, 32, 33, 34  // Side 6
        };

        #endregion
    }
}
