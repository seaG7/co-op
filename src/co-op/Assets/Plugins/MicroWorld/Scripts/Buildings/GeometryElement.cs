using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using MicroWorldNS.Buildings;
using MicroWorldNS.MeshBuilderNS;

namespace MicroWorldNS.Spawners
{
    [Serializable]
    class GeometryElement
    {
        [Tooltip("Name of the element.")]
        public string Name;
        [Tooltip("Allows the system to spawn this element.")]
        public bool Enabled = true;
        [Tooltip("Defines type of element and where it will be spawned.")]
        public GeometryPlace Place = GeometryPlace.Wall;
        [Tooltip("Defines order of segments where elements can spawn.")]
        public SegmentOrderType SegmentOrder = SegmentOrderType.Any;
        [Tooltip("Defines cell edge type where element can spawn.")]
        public BuildingEdgeType EdgeType = BuildingEdgeType.InnerFlat | BuildingEdgeType.InnerOverlap | BuildingEdgeType.Outer | BuildingEdgeType.Interior;
        [Tooltip("Vector3 property that defines offset of element relative to spawn position. In meters.")]
        public Vector3 Offset;
        [Header("Options")]
        [Tooltip("Chance of element spawning.")]
        public float Chance = 1f;
        [Tooltip("SegmentExclusiveGroup allows you to spawn only one random item from a set of items.")]
        public string SegmentExclusiveGroup = "";
        //public GeometryElementFeatures Features;
        public Shapes Shapes = new Shapes();

        internal int ExclusiveSegmentGroupId { get; set; }

        public void OnValidate()
        {
            const char NNBSP = ' ';//NNBSP U+202F
            if (Name == null)
                Name = "";

            var parts = Name.Split(NNBSP);
            var sb = new StringBuilder();
            if (!Enabled)
                sb.Append("[Disabled] ");
            sb.Append(Place.ToString());
            sb.Append(NNBSP);

            if (parts.Length > 1)
                sb.Append(parts[1].Trim());
            else
                sb.Append(parts[0].Trim());

            Name = sb.ToString();

            if (Shapes == null || Shapes.Items == null || Shapes.Items.Count == 0)
                Shapes = new Shapes();
        }

        List<(List<Vector3> verts, List<int> tris, Bounds bounds, Material mat)> preparedGeometry = new List<(List<Vector3> verts, List<int> tris, Bounds bounds, Material mat)>();

        public virtual void Prepare(Material[] mats)
        {
            // prepare geometry
            preparedGeometry.Clear();
            foreach (var shape in Shapes.Items)
            {
                var pair = shape.GetVerticiesNormalized(Shapes, mats);
                //TODO: check empty tris!!!!
                preparedGeometry.Add(pair);
            }
        }

        public virtual void Build(MeshBuilder builder, Vector3 pos, Vector3 right, Vector3 up, Vector3 fwd, Vector3 scale, float coeffElongation, bool stretchY, Material[] mats)
        {
            var sy = scale.y;
            if (!stretchY)
                sy = Shapes.TypicalSize.y;

            for (int iShape = 0; iShape < Shapes.Items.Count; iShape++)
            {
                List<Vector3> Verts = new List<Vector3>();

                var shape = Shapes.Items[iShape];
                var geometry = preparedGeometry[iShape];
                var keepAspectXZ = shape.Features.HasFlag(ShapeFeatures.KeepAspectXZ);
                var bounds = geometry.bounds;

                switch (shape.Type)
                {
                    case ShapeType.Mesh:
                        SpawnAsMesh();
                        break;
                    case ShapeType.GameObject:
                        SpawnAsGameObject();
                        break;  
                    default:
                        SpawnAsCube();
                        break;
                }   

                var pair = (Verts, geometry.tris, geometry.mat);
                builder.SubMeshes.Add(pair);

                void SpawnAsGameObject()
                {
                    var prefab = shape.Prefab as GameObject;
                    if (!prefab)
                        return;

                    var v = bounds.center;

                    // calc offset by Y
                    var offset = Offset;
                    if (!stretchY)
                    {
                        if (v.y > 0.5f)
                            offset.y += scale.y - Shapes.TypicalSize.y;
                    }

                    var p = pos + (v.x * scale.x + offset.x) * right + (v.y * sy + offset.y) * up + (v.z * scale.z + offset.z) * fwd;
                    var go = GameObject.Instantiate(prefab, p, Quaternion.LookRotation(fwd, Vector3.up));
                    go.transform.localScale = go.transform.localScale * shape.MeshScale;
                    builder.GameObjects.Add(go);
                }

                void SpawnAsCube()
                {
                    foreach (var vert in geometry.Item1)
                    {
                        var v = vert;
                        // calc Elongation
                        var sx = scale.x;
                        var carryOverZ = (v.z * scale.z + Offset.z) / 2f;
                        sx -= carryOverZ * coeffElongation * 4;

                        // calc offset by Y
                        var offset = Offset;
                        if (!stretchY)
                        {
                            if (v.y > 0.5f)
                                offset.y += scale.y - Shapes.TypicalSize.y;
                        }

                        // keep aspect XZ
                        var sz = scale.z;
                        if (keepAspectXZ)
                        {
                            v.x = (v.x - bounds.center.x) / bounds.size.x * bounds.size.z + bounds.center.x * sx;
                            sx = sz;
                        }

                        v = pos + (v.x * sx + offset.x) * right + (v.y * sy + offset.y) * up + (v.z * sz + offset.z) * fwd;

                        Verts.Add(v);
                    }
                }

                void SpawnAsMesh()
                {
                    var pivot = bounds.center.withSetY(bounds.min.y);
                    var sx = scale.x;
                    var sz = scale.z;

                    // calc offset by Y
                    var offset = Offset;
                    if (!stretchY)
                    {
                        if (pivot.y > 0.5f)
                            offset.y += scale.y - Shapes.TypicalSize.y;
                    }

                    foreach (var vert in geometry.Item1)
                    {
                        var v = vert;
                        v = pos + (v.x + pivot.x * sx + offset.x) * right + (v.y + pivot.y * sy + offset.y) * up + (v.z + pivot.z * sz + offset.z) * fwd;
                        Verts.Add(v);
                    }
                }

                //void SpawnAsScaledMesh()
                //{
                //    var pivot = bounds.center.withSetY(bounds.min.y);
                //    var sx = scale.x;
                //    var sz = scale.z;
                //    var meshScale = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * Vector3.one;

                //    // calc offset by Y
                //    var offset = Offset;
                //    if (!stretchY)
                //    {
                //        if (pivot.y > 0.5f)
                //            offset.y += scale.y - Shapes.TypicalSize.y;
                //    }

                //    foreach (var vert in geometry.verts)
                //    {
                //        var v = vert;
                //        //v = v + bounds.center
                //        v.Scale(meshScale);
                //        v = pos + (v.x + pivot.x * sx + offset.x) * right + (v.y + pivot.y * sy + offset.y) * up + (v.z + pivot.z * sz + offset.z) * fwd;
                //        Verts.Add(v);
                //    }
                //}
            }

        }
    }

    [Serializable]
    enum GeometryElementFeatures
    {
        None = 0,
        MakeExclusiveArea = 0x1,
        MakeSpawnPoint = 0x2,
        UseLOD = 0x80,
    }

    [Serializable]
    enum SegmentOrderType : byte
    {
        Any = 0, Left = 2, Mid = 3, Right = 4, Central = 5
    }


    [Serializable, Flags]
    enum BuildingEdgeType : byte
    {
        Outer = 0x1, InnerOverlap = 0x2, InnerFlat = 0x4, Interior = 0x8
    }

    [Serializable]
    class SimpleGeometry
    {
        [Range(-1, 1)] public int PivotX = 0;
        [Range(-1, 1)] public int PivotY = -1;
        [Range(-1, 1)] public int PivotZ = 0;
        [Range(-1, 2)] public float Skew = 0;
        public SimpleGeometryFeatures Features = SimpleGeometryFeatures.RemoveFaces0;

        public virtual void Prepare()
        {
            var sides = Features.HasFlag(SimpleGeometryFeatures.Cylinder6) ? 6 : 4;
            if (Features.HasFlag(SimpleGeometryFeatures.Cylinder12))
                sides = 12;

            (verticies, tris) = Features.HasFlag(SimpleGeometryFeatures.Smoothed) ? CreatePolygonMeshSmoothed(sides, 1) : CreatePolygonMesh(sides, 1);

            var pivot = new Vector3(-PivotX / 2f, - PivotY / 2f, -PivotZ / 2f);

            // pivot and skewing
            int alongZ = Features.HasFlag(SimpleGeometryFeatures.SkewAlongZ)? 0 : 1;

            for (int i = 0; i < verticies.Count; i++)
            {
                var v = verticies[i];

                v += pivot;
                
                v.x += v.x * Mathf.Abs(v.y) * Skew * Mathf.Abs(PivotX) * Mathf.Abs(PivotY);
                v.z += v.z * Mathf.Abs(v.y) * Skew * Mathf.Abs(PivotZ) * Mathf.Abs(PivotY) * alongZ;

                v.y += v.y * Mathf.Abs(v.z) * Skew * Mathf.Abs(PivotY) * Mathf.Abs(PivotZ);
                v.x += v.x * Mathf.Abs(v.z) * Skew * Mathf.Abs(PivotX) * Mathf.Abs(PivotZ);

                v.y += v.y * Mathf.Abs(v.x) * Skew * Mathf.Abs(PivotY) * Mathf.Abs(PivotX);
                v.z += v.z * Mathf.Abs(v.x) * Skew * Mathf.Abs(PivotZ) * Mathf.Abs(PivotX) * alongZ;

                if (PivotX == 0 && PivotZ == 0)
                {
                    v.x += v.x * Mathf.Abs(v.y) * Skew * Mathf.Abs(PivotY);
                    v.z += v.z * Mathf.Abs(v.y) * Skew * Mathf.Abs(PivotY) * alongZ;
                }

                if (PivotY == 0 && PivotX == 0)
                {
                    v.y += v.y * Mathf.Abs(v.z) * Skew * Mathf.Abs(PivotZ);
                    v.x += v.x * Mathf.Abs(v.z) * Skew * Mathf.Abs(PivotZ);
                }

                if (PivotY == 0 && PivotZ == 0)
                {
                    v.y += v.y * Mathf.Abs(v.x) * Skew * Mathf.Abs(PivotX);
                    v.z += v.z * Mathf.Abs(v.x) * Skew * Mathf.Abs(PivotX) * alongZ;
                }

                verticies[i] = v;
            }

            // remove invisible faces
            var origTris = tris;
            tris = new List<int>();
            for (int i = 0; i < origTris.Count; i += 3)
            {
                var p0 = verticies[origTris[i]];
                var p1 = verticies[origTris[i + 1]];
                var p2 = verticies[origTris[i + 2]];
                // is zero area?
                var v0 = p1 - p0;
                var v1 = p2 - p0;
                if (Vector3.Cross(v0, v1).sqrMagnitude < 0.0001f)
                    continue;

                // all points have zero coord ?
                if (Features.HasFlag(SimpleGeometryFeatures.RemoveFaces0))
                {
                    if (p0.x.IsZeroApprox() && p1.x.IsZeroApprox() && p2.x.IsZeroApprox()) continue;
                    if (p0.y.IsZeroApprox() && p1.y.IsZeroApprox() && p2.y.IsZeroApprox()) continue;
                    if (p0.z.IsZeroApprox() && p1.z.IsZeroApprox() && p2.z.IsZeroApprox()) continue;
                }

                if (Features.HasFlag(SimpleGeometryFeatures.RemoveFaces1))
                {
                    if (p0.x.IsOneApprox() && p1.x.IsOneApprox() && p2.x.IsOneApprox()) continue;
                    if (p0.y.IsOneApprox() && p1.y.IsOneApprox() && p2.y.IsOneApprox()) continue;
                    if (p0.z.IsOneApprox() && p1.z.IsOneApprox() && p2.z.IsOneApprox()) continue;
                }

                //
                tris.Add(origTris[i]);
                tris.Add(origTris[i + 1]);
                tris.Add(origTris[i + 2]);
            }
        }

        /// <summary>
        /// Создает вершины и треугольники для полигона.
        /// </summary>
        private (List<Vector3> vertices, List<int> triangles) CreatePolygonMeshSmoothed(int sides, float height)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            float angleStep = 2 * Mathf.PI / sides;
            float initialAngle = sides == 4 ? Mathf.PI / 4 : 0;  // Начальный угол 45 градусов
            float r = sides == 4 ? Mathf.Sqrt(2) / 2 : 0.5f;

            // 1. Вершины для нижней крышки
            for (int i = 0; i < sides; i++)
            {
                float angle = initialAngle + i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, -height / 2, z));  // Уникальные вершины для нижней крышки
            }

            // 2. Вершины для верхней крышки
            for (int i = 0; i < sides; i++)
            {
                float angle = initialAngle + i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, height / 2, z));  // Уникальные вершины для верхней крышки
            }

            // 3. Вершины для боковых граней (по две на каждую сторону)
            for (int i = 0; i < sides; i++)
            {
                float angle = initialAngle + i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, -height / 2, z));  // Нижняя боковая вершина
                vertices.Add(new Vector3(x, height / 2, z));   // Верхняя боковая вершина
            }

            // 4. Треугольники для нижней крышки
            for (int i = 1; i < sides - 1; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            // 5. Треугольники для верхней крышки
            int upperOffset = sides;
            for (int i = 1; i < sides - 1; i++)
            {
                triangles.Add(upperOffset);
                triangles.Add(upperOffset + i + 1);
                triangles.Add(upperOffset + i);
            }

            // 6. Треугольники для боковых граней
            int sideOffset = sides * 2;
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                // Первый треугольник боковой грани
                triangles.Add(sideOffset + i * 2);
                triangles.Add(sideOffset + i * 2 + 1);
                triangles.Add(sideOffset + next * 2);

                // Второй треугольник боковой грани
                triangles.Add(sideOffset + next * 2);
                triangles.Add(sideOffset + i * 2 + 1);
                triangles.Add(sideOffset + next * 2 + 1);
            }

            return (vertices, triangles);
        }

        /// <summary>
        /// Создает вершины и треугольники для полигона.
        /// </summary>
        private (List<Vector3> vertices, List<int> triangles) CreatePolygonMesh(int sides, float height)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            float angleStep = 2 * Mathf.PI / sides;
            float initialAngle = sides == 4 ? Mathf.PI / 4 : 0;  // Начальный угол 45 градусов
            float r = sides == 4 ? Mathf.Sqrt(2) / 2 : 0.5f;

            // 1. Вершины для нижней крышки
            for (int i = 0; i < sides; i++)
            {
                float angle = initialAngle + i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, -height / 2, z));  // Вершины нижней крышки
            }

            // 2. Вершины для верхней крышки
            int upperOffset = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = initialAngle + i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, height / 2, z));  // Вершины верхней крышки
            }

            // 3. Вершины для боковых граней (4 вершины на грань)
            int sideStartIndex = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = initialAngle + i * angleStep;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;

                // Нижние и верхние вершины для боковой грани
                vertices.Add(new Vector3(x, -height / 2, z)); // Нижняя левая
                vertices.Add(new Vector3(x, height / 2, z));  // Верхняя левая
                vertices.Add(new Vector3(x, -height / 2, z)); // Нижняя правая
                vertices.Add(new Vector3(x, height / 2, z));  // Верхняя правая
            }

            // 4. Треугольники для нижней крышки
            for (int i = 1; i < sides - 1; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            // 5. Треугольники для верхней крышки
            for (int i = 1; i < sides - 1; i++)
            {
                triangles.Add(upperOffset);
                triangles.Add(upperOffset + i + 1);
                triangles.Add(upperOffset + i);
            }

            // 6. Треугольники для боковых граней (по 4 вершины на грань)
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                // Индексы вершин для текущей боковой грани
                int v0 = sideStartIndex + i * 4;     // Нижняя левая
                int v1 = sideStartIndex + i * 4 + 1; // Верхняя левая
                int v2 = sideStartIndex + next * 4 + 2; // Нижняя правая следующей грани
                int v3 = sideStartIndex + next * 4 + 3; // Верхняя правая следующей грани

                // Первый треугольник боковой грани
                triangles.Add(v0);
                triangles.Add(v1);
                triangles.Add(v2);

                // Второй треугольник боковой грани
                triangles.Add(v1);
                triangles.Add(v3);
                triangles.Add(v2);
            }

            return (vertices, triangles);
        }

        [NonSerialized]
        public List<Vector3> verticies = new List<Vector3>();
        [NonSerialized]
        public List<int> tris = new List<int>();
    }

    [Serializable, Flags]
    enum SimpleGeometryFeatures : UInt16
    {
        None = 0,
        KeepAspectXZ = 0x1, 
        RemoveFaces0 = 0x2, 
        RemoveFaces1 = 0x4, 
        Smoothed = 0x8, 
        //NotUsed2 = 0x10,
        SkewAlongZ = 0x20,
        //SkewAlongY = 0x40, 
        //SkewAlongX = 0x80,
        Cylinder6 = 0x100,
        Cylinder12 = 0x200,
    }

    [Flags]
    enum GeometryPlace : int
    {
        None = 0x00,
        Wall = 0x01,
        Window = 0x02,
        Door = 0x04,
        Balcony = 0x08,
        Fence = 0x10,
        Cornice = 0x200,
        Edge = 0x20,
        Corner = 0x40,
        Cell = 0x80,
        WallPadding = 0x100,
    }
}