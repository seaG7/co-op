using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// Spawns Gate prefab in cells of Gate type
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.c3tllnhhbrb9")]
    public class GateSpawner : BaseSpawner
    {
        [Tooltip("Gate prefab. It can be empty.")]
        [SerializeField] GameObject defaultGatePrefab;
        [Tooltip("Moves gate inward from the edge so it stays reachable before the world border.")]
        [SerializeField, Min(0f)] float inwardOffset = 1.5f;
        [Tooltip("Snap gate vertically to terrain after inward offset is applied.")]
        [SerializeField] bool snapToTerrain = true;

        public override IEnumerator Build(MicroWorld builder)
        {
            yield return base.Build(builder);

            CheckMapSpawner();

            if (defaultGatePrefab == null)
                defaultGatePrefab = Resources.Load<GameObject>("Gate");

            foreach (var hex in Map.AllHex())
                if (Map[hex].Type == Builder.MapSpawner.GateCellType)
                    BuildGate(hex);
        }

        private void BuildGate(Vector2Int hex)
        {
            var cell = Map[hex];
            var gateInfo = Builder.Gates.FirstOrDefault(g => g.Cell == hex);
            if (gateInfo == null)
                return;

            int edgeIndex = -1;
            for (int iEdge = 0; iEdge < CellGeometry.CornersCount; iEdge ++)
            {
                if (CellGeometry.Neighbor(hex, iEdge) == cell.Parent)
                {
                    edgeIndex = iEdge;
                    break;
                }
            }

            Vector3 cellCenter = CellGeometry.Center(hex);
            Vector3 position;
            Vector3 direction;

            if (edgeIndex >= 0)
            {
                Vector3 p0 = CellGeometry.Corner(hex, edgeIndex);
                Vector3 p1 = CellGeometry.Corner(hex, edgeIndex + 1);
                position = (p0 + p1) / 2f;
                direction = (cellCenter - position).normalized;
                position += direction * inwardOffset;
            }
            else
            {
                Vector3 worldCenter = CellGeometry.Center(Map.Center);
                direction = (worldCenter - cellCenter).normalized;
                if (direction.sqrMagnitude <= 0.0001f)
                    direction = Vector3.forward;

                position = cellCenter + direction * inwardOffset;
            }

            position.y = cell.Height;

            if (snapToTerrain && Builder.Terrain != null)
                position.y = Builder.Terrain.SampleHeight(position) + Builder.Terrain.transform.position.y;

            var prefab = gateInfo.GatePrefab == null ? defaultGatePrefab : gateInfo.GatePrefab;
            var obj = Instantiate(prefab, position, Quaternion.LookRotation(direction, Vector3.up), Builder.Terrain.transform);
            var gate = obj.GetComponentInChildren<Gate>(true);
            gateInfo.Gate = gate;
            gate.GateInfo = gateInfo;
            gate.World = Builder;
        }
    }
}
