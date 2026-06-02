using System;
using System.Collections.Generic;
using UnityEngine;

namespace MicroWorldNS
{
    public interface ICellGeometry
    {
        float Radius { get; }// outer circle radius
        Vector3 Offset { get; }// offset of start of grid in world space
        float InnerRadius { get; }// inner circle radius
        float Width { get; }
        float Height { get; }
        float EdgeLength { get; }
        float VertSpacing { get; }
        float HorzSpacing { get; }
        float Area { get; }
        int CornersCount { get; }// 6 for Hex, 4 for rect

        /// <summary> Returns iCorner corner in local coordinates of Hex (relative to center of Hex) </summary>
        Vector3 Corner(int iCorner);
        /// <summary> Returns center of edge iEdge (in local coordinates) </summary>
        Vector3 EdgeCenter(int iEdge) => (Corner(iEdge) + Corner(iEdge + 1)) / 2f;
        Vector3 Center(Vector2Int hex);
        Vector3 EdgeNormal(int iEdge);
        Vector3 CornerNormal(int iCorner);
        Vector2Int Neighbor(Vector2Int hex, int iEdge);
        Vector2Int PointToHex(Vector3 p);
        float SignedDistance(Vector3 local);
        int PointToEdge(Vector3 local);
        int PointToCorner(Vector3 local);
        IEnumerable<Vector2Int> CellsAroundCorner(Vector2Int hex, int iCorner);

        /// <summary> Returns iCorner corner of Hex (in global coordinates) </summary>
        Vector3 Corner(Vector2Int hex, int iCorner) => Center(hex) + Corner(iCorner);
        /// <summary> Returns center of edge iEdge (in global coordinates) </summary>
        Vector3 EdgeCenter(Vector2Int hex, int iEdge) => Center(hex) + EdgeCenter(iEdge);

        float InnerAngle => (CornersCount - 2) * Mathf.PI / CornersCount;

        /// <summary> Returns corners of Hex (in global coordinates) </summary>
        IEnumerable<Vector3> Corners(Vector2Int hex)
        {
            var c = Center(hex);
            for (int i = 0; i < CornersCount; i++)
                yield return c + Corner(i);
        }


        /// <summary> Returns edges of Hex (in global coordinates) </summary>
        IEnumerable<(Vector3, Vector3)> Edges(Vector2Int hex)
        {
            var c = Center(hex);
            for (int i = 0; i < CornersCount; i++)
                yield return (c + Corner(i), c + Corner(i + 1));
        }

        /// <summary> Returns neighbors of Hex </summary>
        IEnumerable<Vector2Int> Neighbors(Vector2Int hex)
        {
            for (int i = 0; i < CornersCount; i++)
                yield return Neighbor(hex, i);
        }

        IEnumerable<Vector2Int> NeighborsEx(Vector2Int hex) => Neighbors(hex);

        /// <summary> Hex to Point (Hex center) </summary>
        Rect Bounds(Vector2Int p)
        {
            var c = Center(p);
            return new Rect(c.x - Width / 2f, c.z - Height / 2f, Width, Height);
        }

        /// <summary> Returns 0 on edge of Hex, InnerRadius - in center, negative - outside of Hex (distance to nearest edge) </summary>
        float SignedDistance(Vector2Int hex, Vector3 point) => SignedDistance(point - Center(hex));

        /// <summary> Returns index of nearest edge for point (in global coordinates) </summary>
        int PointToEdge(Vector2Int hex, Vector3 p) => PointToEdge(p - Center(hex));

        /// <summary> Returns index of nearest corner for point (in global coordinates) </summary>
        int PointToCorner(Vector2Int hex, Vector3 p) => PointToCorner(p - Center(hex));

        bool Contains(Vector2Int hex, Vector3 point) => PointToHex(point) == hex;
        bool Contains(Vector3 local) => PointToHex(local) == Vector2Int.zero;

        int OppositeCorner(int iCorner) => (iCorner + CornersCount / 2).Mod(CornersCount);

        void Draw(Vector2Int hex, float y = 0, Vector3 offset = default)
        {
            var liftUp = Vector3.up * y;
            for (int i = 0; i < CornersCount; i++)
                Gizmos.DrawLine(Corner(hex, i) + liftUp + offset, Corner(hex, i + 1) + liftUp + offset);
        }
    }

    [Serializable]
    public class HexCellGeometry : ICellGeometry
    {
        [field: SerializeField]
        public float Radius { get; private set; } = 1;// outer circle radius
        [field: SerializeField]
        public Vector3 Offset { get; private set; }// offset of start of grid in world space
        [field: SerializeField]
        public float InnerRadius { get; private set; }// inner circle radius
        [field: SerializeField]
        public float Width { get; private set; }
        [field: SerializeField]
        public float Height { get; private set; }
        [field: SerializeField]
        public float EdgeLength { get; private set; }
        [field: SerializeField]
        public float VertSpacing { get; private set; }
        [field: SerializeField]
        public float HorzSpacing { get; private set; }
        [field: SerializeField]
        public float Area { get; private set; }
        [field: SerializeField]
        public int CornersCount { get; private set; }// 6 for Hex, 4 for rect

        static readonly float SqrtThree = Mathf.Sqrt(3);
        [SerializeField]
        float M0, M2;
        [SerializeField]
        float N0, N1, N2, N3;
        [SerializeField]
        Vector3[] corners;
        [SerializeField]
        Vector3[] edgeNormals;
        Vector3[] cornerNormals;
        [SerializeField]
        float startAngle;
        static Vector2Int[] neighborOffsets = new Vector2Int[6] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0) };
        static Vector2Int[] neighborOffsetsOdd = new Vector2Int[6] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1) };
        [SerializeField]
        float edgeAngle;

        public HexCellGeometry(float radius, Vector3 offset)
        {
            Radius = radius;
            Offset = offset;
            edgeAngle = 60f;
            CornersCount = 6;
            corners = new Vector3[6];
            edgeNormals = new Vector3[6];
            cornerNormals = new Vector3[6];
            EdgeLength = Radius;

            for (int i = 0; i < 6; i++)
            {
                var angle = 60.0f * i - 120;
                corners[i] = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(-angle * Mathf.Deg2Rad)) * radius;
            }

            for (int i = 0; i < 6; i++)
            {
                edgeNormals[i] = (corners[i] + corners[(i + 1) % 6]).normalized;
            }

            for (int i = 0; i < 6; i++)
            {
                cornerNormals[i] = corners[i].normalized;
            }

            Width = radius * 2;
            Height = SqrtThree * radius;
            VertSpacing = Height;
            HorzSpacing = Width * 3.0f / 4.0f;

            M0 = 3 / 2f * radius;
            M2 = SqrtThree * radius;

            N0 = 2f / 3f / radius;
            N1 = 0 / radius;
            N2 = -1 / 3f / radius;
            N3 = SqrtThree / 3f / radius;

            startAngle = 90 + 30 + 360;

            Area = radius * radius * SqrtThree / 4f * 6f;
            InnerRadius = SqrtThree * radius / 2.0f;
        }

        /// <summary> Returns i corner in local coordinates of Hex (relative to center of Hex) </summary>
        public Vector3 Corner(int iCorner)
        {
            return corners[Helper.Mod(iCorner, CornersCount)];
        }

        /// <summary> Direction from center to edge mid point </summary>
        public Vector3 EdgeNormal(int iEdge)
        {
            return edgeNormals[iEdge.Mod(CornersCount)];
        }

        public Vector3 CornerNormal(int iCorner)
        {
            return cornerNormals[iCorner.Mod(CornersCount)];
        }

        /// <summary> Returns neighbor of Hex </summary>
        public Vector2Int Neighbor(Vector2Int hex, int iEdge)
        {
            var offsets = hex.x % 2 == 0 ? neighborOffsets : neighborOffsetsOdd;
            return hex + offsets[iEdge.Mod(CornersCount)];
        }

        public Vector2Int PointToHex(Vector3 p)
        {
            p -= Offset;

            var q = N0 * p.x + N1 * p.z;
            var r = N2 * p.x + N3 * p.z;
            return AxialToOffsetFlat(AxialRound(q, r));

            static Vector2Int AxialRound(float x, float y)
            {
                var xgrid = Mathf.RoundToInt(x);
                var ygrid = Mathf.RoundToInt(y);
                x -= xgrid; y -= ygrid; //remainder
                if (Mathf.Abs(x) >= Mathf.Abs(y))
                    return new Vector2Int(xgrid + Mathf.RoundToInt(x + 0.5f * y), ygrid);
                else
                    return new Vector2Int(xgrid, ygrid + Mathf.RoundToInt(y + 0.5f * x));
            }

            static Vector2Int AxialToOffsetFlat(Vector2Int hex)
            {
                var col = hex.x;
                var row = hex.y + (hex.x - (hex.x & 1)) / 2;
                return new Vector2Int(col, row);
            }
        }

        /// <summary> Hex to Point (Hex center) </summary>
        public Vector3 Center(Vector2Int hex)
        {
            var x = hex.x * M0;
            var y = M2 * (hex.y + (hex.x & 1) / 2f);
            return new Vector3(x, 0, y) + Offset;
        }

        /// <summary> Returns 0 on edge of Hex, InnerRadius - in center, negative - outside of Hex (distance to nearest edge) </summary>
        public float SignedDistance(Vector3 local)
        {
            var edge = PointToEdge(local);
            var p0 = corners[edge];
            var p1 = corners[(edge + 1).Mod(CornersCount)];
            var d = ((p1.z - p0.z) * local.x - (p1.x - p0.x) * local.z + p1.x * p0.z - p1.z * p0.x) / EdgeLength;
            return d;
        }

        /// <summary> Returns index of nearest edge for point (in local coordinates) </summary>
        public int PointToEdge(Vector3 local)
        {
            var a = -Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg + startAngle;
            return (int)(a / edgeAngle) % CornersCount;
        }

        /// <summary> Returns index of nearest edge for point (in local coordinates) </summary>
        public int PointToCorner(Vector3 local)
        {
            var a = -Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg + startAngle + edgeAngle / 2f;
            return (int)(a / edgeAngle) % CornersCount;
        }

        public IEnumerable<Vector2Int> CellsAroundCorner(Vector2Int hex, int iCorner)
        {
            yield return Neighbor(hex, iCorner - 1);
            yield return Neighbor(hex, iCorner);
            yield return hex;
        }
    }

    [Serializable]
    public class RectCellGeometry : ICellGeometry
    {
        [field: SerializeField]
        public float Radius { get; private set; }// outer circle radius
        [field: SerializeField]
        public Vector3 Offset { get; private set; }// offset of start of grid in world space
        [field: SerializeField]
        public float InnerRadius { get; private set; }// inner circle radius
        [field: SerializeField]
        public float Width { get; private set; }
        [field: SerializeField]
        public float Height { get; private set; }
        [field: SerializeField]
        public float EdgeLength { get; private set; }
        [field: SerializeField]
        public float VertSpacing { get; private set; }
        [field: SerializeField]
        public float HorzSpacing { get; private set; }
        [field: SerializeField]
        public float Area { get; private set; }
        [field: SerializeField]
        public int CornersCount { get; private set; }// 6 for Hex, 4 for rect

        static readonly float SqrtTwo = Mathf.Sqrt(2);
        [SerializeField]
        Vector3[] corners;
        [SerializeField]
        Vector3[] edgeNormals;
        Vector3[] cornerNormals;
        static Vector2Int[] neighborOffsets = new Vector2Int[4] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };
        static Vector2Int[] neighborOffsetsEx = new Vector2Int[8] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1) };

        public RectCellGeometry(float innerRadius, Vector3 offset)
        {
            Radius = innerRadius * SqrtTwo;
            Offset = offset;
            CornersCount = 4;
            corners = new Vector3[4];
            edgeNormals = new Vector3[4];
            cornerNormals = new Vector3[4];
            EdgeLength = innerRadius * 2;
            HorzSpacing = VertSpacing = Height = Width = innerRadius * 2;
            Area = innerRadius * innerRadius * 4;
            InnerRadius = innerRadius;

            for (int i = 0; i < 4; i++)
            {
                var angle = 90.0f * i - 135;
                corners[i] = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(-angle * Mathf.Deg2Rad)) * Radius;
            }

            for (int i = 0; i < 4; i++)
            {
                edgeNormals[i] = (corners[i] + corners[(i + 1) % 4]).normalized;
            }

            for (int i = 0; i < 4; i++)
            {
                cornerNormals[i] = corners[i].normalized;
            }
        }

        /// <summary> Returns i corner in local coordinates of Hex (relative to center of Hex) </summary>
        public Vector3 Corner(int iCorner)
        {
            return corners[Helper.Mod(iCorner, CornersCount)];
        }

        /// <summary> Direction from center to edge mid point </summary>
        public Vector3 EdgeNormal(int iEdge)
        {
            return edgeNormals[iEdge.Mod(CornersCount)];
        }

        public Vector3 CornerNormal(int iCorner)
        {
            return cornerNormals[iCorner.Mod(CornersCount)];
        }

        /// <summary> Returns neighbor of Hex </summary>
        public Vector2Int Neighbor(Vector2Int hex, int iEdge)
        {
            return hex + neighborOffsets[iEdge.Mod(CornersCount)];
        }

        public IEnumerable<Vector2Int> NeighborsEx(Vector2Int hex)
        {
            for (int i = 0; i < neighborOffsetsEx.Length; i++)
                yield return hex + neighborOffsetsEx[i];
        }

        public Vector2Int PointToHex(Vector3 p)
        {
            p -= Offset;

            var q = Mathf.RoundToInt(p.x / Width);
            var r = Mathf.RoundToInt(p.z / Height);
            return new Vector2Int(q, r);
        }

        /// <summary> Returns 0 on edge of Hex, InnerRadius - in center, negative - outside of Hex (distance to nearest edge) </summary>
        public float SignedDistance(Vector3 local)
        {
            var ax = Mathf.Abs(local.x);
            var az = Mathf.Abs(local.z);
            if (ax > az)
                return InnerRadius - ax;
            else
                return InnerRadius - az;
        }

        public Vector3 Center(Vector2Int hex)
        {
            var x = hex.x * Width;
            var y = hex.y * Height;
            return new Vector3(x, 0, y) + Offset;
        }

        /// <summary> Returns index of nearest edge for point (in local coordinates) </summary>
        public int PointToEdge(Vector3 local)
        {
            var ax = Mathf.Abs(local.x);
            var az = Mathf.Abs(local.z);
            if (ax > az)
                return local.x > 0 ? 1 : 3;
            else
                return local.z > 0 ? 0 : 2;
        }

        /// <summary> Returns index of nearest edge for point (in local coordinates) </summary>
        public int PointToCorner(Vector3 local)
        {
            var sx = Mathf.Sign(local.x);
            var sz = Mathf.Sign(local.z);
            if (sx == -1)
                return sz == 1 ? 0 : 3;
            else
                return sz == 1 ? 1 : 2;
        }

        public IEnumerable<Vector2Int> CellsAroundCorner(Vector2Int hex, int iCorner)
        {
            yield return Neighbor(hex, iCorner - 1);
            switch (iCorner.Mod(4))
            {
                case 0: yield return hex + new Vector2Int(-1, 1); break;
                case 1: yield return hex + new Vector2Int(1, 1); break;
                case 2: yield return hex + new Vector2Int(1, -1); break;
                case 3: yield return hex + new Vector2Int(-1, -1); break;
            }
            yield return Neighbor(hex, iCorner);
            yield return hex;
        }
    }
}