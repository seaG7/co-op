using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// The Cell Modifier allows you to manually adjust the type and height of cells in the desired place of the terrain.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.p1r60uorlddd")]
    public class CellModifier : BaseSpawner
    {
        [Tooltip("Cell type that will be applied to the area. Leave this parameter empty if you do not want to change the cell's type.")]
        [Popup(nameof(ProposedCellTypes), true)]
        public string CellType;

        [Tooltip("Radius of modifier area. Units are specified in RadiusUnits.")]
        public float Radius = 2;

        public RadiusUnit RadiusUnits = RadiusUnit.CellSize;

        [Tooltip("Elevates the cells of area (meters). This value can be negative and positive.")]
        public float Elevation = 0;

        [Tooltip("Defines multiplier of micro noise amplitude for cells.")]
        public float MicroNoiseScale = 1f;

        public CellModifierFeatures Features;

        [Tooltip("Prefab that will be spawned in the center of CellModifier. Can be null.")]
        public GameObject SpawnPrefab;

        public override int Order => 250;

        public override IEnumerator Prepare(MicroWorld builder)
        {
            yield return base.Prepare(builder);

            if (Features.HasFlag(CellModifierFeatures.KeepInCenterOfTerrain))
                transform.position = builder.HexToPos(Map.Center);
        }

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            var cellType = Builder.MapSpawner.AllCellTypes.FirstOrDefault(c => c.Name == CellType);

            // enumerate my cells
            var myCells = GetHexes();
            foreach (var hex in myCells)
            {
                var cell = Map[hex];

                // assign type
                if (cellType != null)
                    cell.Type = cellType;

                // assign features
                if (Features.HasFlag(CellModifierFeatures.LiftUpToWaterLevel))
                    cell.LiftUpToWaterLevel = true;

                if (Features.HasFlag(CellModifierFeatures.FlattenArea))
                    cell.FlattenArea = true;

                cell.MicroNoiseScale *= MicroNoiseScale;
                cell.Elevation += Elevation;
            }

            // create border cells
            if (Features.HasFlag(CellModifierFeatures.BorderAround))
                CreateBorderCellsAround(myCells);
        }

        /// <summary> Returns cells affected by this CellModifier </summary>
        protected virtual IEnumerable<Vector2Int> GetHexes() =>
            Builder.GetCellsInRadius(transform.position, Radius, RadiusUnits);

        private void CreateBorderCellsAround(IEnumerable<Vector2Int> myCells)
        {
            var rnd = rootRnd.GetBranch(83476);
            var enterCreated = false;
            foreach (var n in Builder.GetCellsAround(myCells).OrderBy(_ => rnd.Float()))
            {
                if (!enterCreated && Map[n].Type.IsPassable)
                {
                    enterCreated = true;
                    continue;// this is enter cell -> skip
                }
                // assign border type
                Map[n].Type = Builder.MapSpawner.BorderCellType;
            }
        }

        public override void OnBuildCompleted()
        {
            base.OnBuildCompleted();

            if (this == null || gameObject == null)
                return;

            // adjust Y position
            transform.position = transform.position.withSetY(Terrain.SampleHeight(transform.position));

            // spawn prefab 
            if (SpawnPrefab)
            {
                var obj = Instantiate(SpawnPrefab, transform.position, transform.rotation, Terrain.transform);
                obj.GetOrAddComponent<SpawnedObjInfo>();
                SpawnedObjectMetadataUtility.Apply(obj, transform, gameObject.name, string.Empty, string.Empty);

                // build variants
                Variant.Build(obj, new Rnd(name, Builder.Seed));
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 1, 0, 0.4f);
            Gizmos.DrawSphere(transform.position, 5);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, CellType);
        }

        private void OnDrawGizmosSelected()
        {
            var mw = GetComponentInParent<MicroWorld>();
            if (mw == null)
                return;
            if (mw.Terrain != null)
            {
                var h = mw.Terrain.SampleHeight(transform.position);
                transform.position = transform.position.withSetY(h);
            }

            var map = mw.Map;
            if (map == null || map.Size == 0)
                return;

            Gizmos.color = Color.white * 2;

            foreach (var hex in mw.GetCellsInRadius(transform.position, Radius, RadiusUnits))
                map.Geometry.Draw(hex, map[hex].Height + 2);
        }
#endif
    }

    [Flags, Serializable]
    public enum CellModifierFeatures
    {
        None = 0x0,
        FlattenArea = 0x1,
        LiftUpToWaterLevel = 0x2,
        KeepInCenterOfTerrain = 0x4,
        BorderAround = 0x8,
    }

    public enum RadiusUnit : byte
    {
        CellSize = 0, Meters = 1
    }
}
