using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    /// <summary>
    /// Contains info about level map - cells, cell types, heights, geometry, roads 
    /// </summary>
    [Serializable]
    public class Map
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public int Size => Mathf.Max(Width, Height); 

        [SerializeReference]
        public ICellGeometry Geometry;
        public Cell[] Cells;
        
        public Vector2Int Center => new Vector2Int(Mathf.RoundToInt(Width / 2f), Mathf.RoundToInt(Height / 2f));
        
        public int LeftBorder; // left border index
        
        public int RightBorderX; 
        public int RightBorderY;
        
        public int RightBorder => Mathf.Min(RightBorderX, RightBorderY);

        [SerializeField] Cell defaultCell;

        public Map Clone()
        {
            var clone = new Map(Width, Height, 0, Geometry);
            clone.LeftBorder = LeftBorder;
            clone.RightBorderX = RightBorderX;
            clone.RightBorderY = RightBorderY;

            for (int i = 0; i < Cells.Length; i++)
                Cells[i].AssignTo(clone.Cells[i]);

            return clone;
        }

        public IEnumerable<Vector2Int> AllHex()
        {
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                yield return new Vector2Int(x, y);
        }

        public IEnumerable<Vector2Int> AllInsideHex()
        {
            for (int x = LeftBorder + 1; x < RightBorderX; x++)
            for (int y = LeftBorder + 1; y < RightBorderY; y++)
                yield return new Vector2Int(x, y);
        }
        
        public Map(int width, int height, int padding, ICellGeometry geometry)
        {
            Width = width;
            Height = height;
            LeftBorder = padding - 1;
            RightBorderX = Width - padding;
            RightBorderY = Height - padding;
            
            Geometry = geometry;
            Cells = new Cell[Width * Height];
            for (int i = 0; i < Cells.Length; i++)
                Cells[i] = new Cell(geometry.CornersCount);

            defaultCell = new Cell(geometry.CornersCount) { Type = new CellType() };
        }

        public Cell this[Vector2Int p]
        {
            set
            {
                var x = p.x;
                var y = p.y;
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    return;
                Cells[y * Width + x] = value;
            }
            get
            {
                var x = p.x;
                var y = p.y;
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    return defaultCell;
                return Cells[y * Width + x];
            }
        }

        public bool IsBorderOrOutside(Vector2Int hex)
        {
            var x = hex.x;
            var y = hex.y;
            if (x <= LeftBorder || x >= RightBorderX) return true;
            if (y <= LeftBorder || y >= RightBorderY) return true;
            return false;
        }

        public bool IsOutside(Vector2Int hex)
        {
            var x = hex.x;
            var y = hex.y;
            if (x < LeftBorder || x > RightBorderX) return true;
            if (y < LeftBorder || y > RightBorderY) return true;
            return false;
        }

        public bool InRange(Vector2Int hex)
        {
            return !IsOutside(hex);
        }

        public int SignedDistanceToBorder(Vector2Int hex)
        {
            var center = Center;
            var x = hex.x;
            var y = hex.y;
            var dx = x < center.x ? x - LeftBorder : RightBorderX - x;
            var dy = y < center.y ? y - LeftBorder : RightBorderY - y;
            return Mathf.Min(dx, dy);
        }

        #region
        public float SumNeighbors(Vector2Int p) =>
            Geometry.Neighbors(p).Sum(pp => this[pp].Height);

        public float SumNeighborsSafe(Vector2Int p, out int count)
        {
            var sum = 0f;
            count = 0;
            foreach (var n in Geometry.Neighbors(p))
            {
                if (IsOutside(n)) continue;
                sum += this[n].Height;
                count++;
            }
            return sum;
        }

        public void Blur()
        {
            var temp = Clone();
            foreach (var p in AllHex())
            {
                var w = 1f;
                var sum = temp.SumNeighborsSafe(p, out var count);
                this[p].Height = (sum + temp[p].Height * w) / (count + w);
            }
        }

        public void Blur(int count)
        {
            for (int i = 0; i < count; i++)
                Blur();
        }
        #endregion
    }
    
    [Serializable]
    public struct SpawnPoint
    {
        public SpawnPointType Type;
        public Vector3 Pos;
        public Vector3 Normal;
        public Vector2Int OwnerCellHex;

        public SpawnPoint(SpawnPointType type, Vector3 pos, Vector3 normal, Vector2Int ownerCellHex)
        {
            Type = type;
            Pos = pos;
            Normal = normal;
            OwnerCellHex = ownerCellHex;
        }
    }

    [Serializable, Flags]
    public enum SpawnPointType : byte
    {
        None = 0,
        Interior = 0x1, InteriorWall = 0x2, InteriorWindow = 0x3, InteriorBalcony = 0x8, 
        //ExteriorWall = 0x8, ExteriorWindow = 0x10, Exterior = 0x20
    }
}