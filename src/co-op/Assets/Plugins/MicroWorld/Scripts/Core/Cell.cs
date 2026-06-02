using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MicroWorldNS
{
    /// <summary> Cell of the terrain </summary>
    [Serializable]
    public class Cell
    {
        /// <summary> Refernce to cell type </summary>
        public CellType Type { get => type; set => SetType(value); }

        /// <summary> Formal height of cell (meters) </summary>
        public float Height;
        /// <summary> Cell index where from you can pass into this cell. </summary>
        public Vector2Int Parent;
        /// <summary> Info about cell edges </summary>
        public Edge[] Edges;
        [NonSerialized]
        public List<SpawnPoint> GoodSpawnPoints;
        [NonSerialized]
        /// <summary> List of taken areas for the cell </summary>
        public List<TakenArea> TakenAreas = new List<TakenArea>(0);
        /// <summary> Is any road/river in this cell? </summary>
        public CellContent Content;
        /// <summary> Cumulative micronoise scale for the cell </summary>
        public float MicroNoiseScale = 1f;
        /// <summary> Should cell be lifted above water? </summary>
        public bool LiftUpToWaterLevel = false;
        /// <summary> Cell additional elevation (meters) </summary>
        public float Elevation = 0;
        /// <summary> The cell is aligned with the height of neighboring cells </summary>
        public bool FlattenArea = false;

        [NonSerialized]
        public object InternalData;

        [SerializeReference, FormerlySerializedAs("Type")]
        private CellType type;

        public Cell(int edgesCount)
        {
            Edges = new Edge[edgesCount];
        }

        public void AssignTo(Cell cell)
        {
            cell.Type = Type;
            cell.Height = Height;
            cell.Content = Content;
        }

        public void SetContent(CellContent flag, bool value)
        {
            if (value)
                Content |= flag;
            else
                Content &= ~flag;
        }

        public bool HasContent(CellContent flag)
        {
            return Content.HasFlag(flag);
        }

        private void SetType(CellType type)
        {
            // assign type
            this.type = type;
            if (type == null)
                return;

            // assign parameters from type
            MicroNoiseScale = type.MicroNoiseScale;
            LiftUpToWaterLevel = type.Features.HasFlag(CellTypeFeatures.LiftUpToWaterLevel);
            Elevation = type.Elevation;
            FlattenArea = type.Features.HasFlag(CellTypeFeatures.FlattenArea);
        }
    }

    [Flags]
    public enum CellContent : UInt16
    {
        IsRoad = 1 << 0,
        IsRoadCross = 1 << 1,
        IsRiver = 1 << 2,
    }


    [Serializable]
    public class CellType
    {
        [Tooltip("Cell type identifier.")]
        public string Name;
        [Tooltip("Probability of appearing on map, relative to other types.")]
        public float Chance = 1;
        //
        [Tooltip("Defines multiplier of micro noise amplitude for given cell type.")]
        public float MicroNoiseScale = 1;
        [Tooltip("The Height Power parameter allows you to set the weight of the height of cells of a given type during interpolation.")]
        public float HeightPower = 1;
        [Tooltip("Affects the steepness of the slope for cells of this type.")]
        public float HeightSharpness = 1;
        //
        [Tooltip("Elevates the cells of this type (meters).")]
        public float Elevation = 0;
        public CellTypeFeatures Features;

        public bool IsFlatCellType => MicroNoiseScale <= 0.001f;
        public bool IsPassable => !Features.HasFlag(CellTypeFeatures.NoPassage);
    }

    [Flags, Serializable]
    public enum CellTypeFeatures
    {
        None = 0x0,
        LiftUpToWaterLevel = 0x1,
        OneCellPerTerrain = 0x2,
        FlattenArea = 0x4,
        NoPassage = 0x8,// impassable cell
    }

    /// <summary> Edge of cell </summary>
    [Serializable]
    public struct Edge
    {
        /// <summary> Height difference with neighboring cell. In meters. </summary>
        public float WallHeight;
        /// <summary> Max formal slope angle between cells. Can be positive and negative. In degrees. </summary>
        public float WallAngle;
        /// <summary> Is this edge border between cells of different types? </summary>
        public bool IsCellTypeBorder;
        /// <summary> Edge is passage between cells </summary>
        public bool IsPassage;
        /// <summary> Road crosses this edge </summary>
        public bool IsRoad;
        /// <summary> River crosses this edge </summary>
        public bool IsRiver;
        /// <summary> Can the character pass over the edge? </summary>
        public readonly bool IsWalkable => WallAngle.InRange(Preferences.Instance.DeclineWalkableAngle, Preferences.Instance.InclineWalkableAngle);
        /// <summary> For internal purposes </summary>
        internal bool isProcessed;
    }


}