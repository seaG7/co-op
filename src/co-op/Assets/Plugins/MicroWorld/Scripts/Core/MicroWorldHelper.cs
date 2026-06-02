using MicroWorldNS.Spawners;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public static class MicroWorldHelper
    {
        public static Quaternion Rotate(this GameObject spawned, Vector3 surfaceNormal, Vector3 faceDir, float verticality, RotationType rotationType, Rnd rnd, bool lookToFaceDir = false, bool keepInitRotation = false)
        {
            Space space = Space.World;
            if (!keepInitRotation)
            {
                var normalXZ = Mathf.Abs(surfaceNormal.y) > 0.99f || lookToFaceDir ? faceDir : surfaceNormal.XZ().normalized;
                spawned.transform.LookAt(spawned.transform.position + normalXZ);
                var angle = Vector3.Angle(normalXZ, surfaceNormal) * Mathf.Sign(surfaceNormal.y);
                var angle2 = Mathf.Abs(90 - angle) * (1 - verticality);
                spawned.transform.RotateAround(spawned.transform.position, spawned.transform.right, angle2);
                space = Space.Self;
            }

            switch (rotationType)
            {
                case RotationType.Any:
                    spawned.transform.rotation = Quaternion.Euler(rnd.Float(360), rnd.Float(360), rnd.Float(360));
                    break;
                case RotationType.AroundY:
                    spawned.transform.Rotate(Vector3.up, rnd.Float(360), space);
                    break;
                case RotationType.AroundYZ180:
                    spawned.transform.Rotate(Vector3.forward, rnd.Bool(0.5f) ? 0 : 180, space);
                    spawned.transform.Rotate(Vector3.up, rnd.Float(360), space);
                    break;
            }

            return spawned.transform.rotation;
        }

        public static bool CheckCellType(this IList<string> CellTypes, string type)
        {
            if (CellTypes == null || CellTypes.Count == 0)
                return true;

            var hasPositive = false;

            foreach (var s in CellTypes)
            {
                if (s.Length > 0 && s.StartsWith('-'))
                {
                    if (type == s.Substring(1))
                        return false;
                } else
                    hasPositive = true;
            }

            if (hasPositive)
                return CellTypes.Contains(type);

            return true;
        }

        internal static Vector2 GetScaleBounds(float scale, float scaleVariance)
        {
            return new Vector2(scale * (1 - scaleVariance), scale * (1 + scaleVariance));
        }

        internal static float GetScale(float scale, float scaleVariance, Rnd rnd)
        {
            return rnd.Float(GetScaleBounds(scale, scaleVariance));
        }

        //public static bool IsAllowedTags(MicroWorld world, IEnumerable<string> tags)
        //{
        //    if (tags == null || !tags.Any()) return true;
        //    foreach (var tag in tags)
        //    {
        //        if (world.ForbiddenTags.Contains(tag))
        //            return false;
        //    }

        //    return true;
        //}

        public static void SelectOneOfExclusiveGroup(this List<BaseSpawner> spawners, int startIndex,  Rnd rnd)
        {
            var group = (spawners[startIndex] as IExclusive).ExclusiveGroup;
            if (group.IsNullOrEmpty())
                return;
            var list = spawners.OfType<IExclusive>().Where(s => s.ExclusiveGroup == group).ToArray();
            var probs = list.Select(s => s.Chance).ToList();
            var hash = list.ToHashSet();
            var selected = rnd.GetBranch(group, 282656).GetRnd(list, probs);

            // remove unselected spawners
            for (int i = startIndex; i < spawners.Count; i++)
            {
                if (spawners[i] is IExclusive ex)
                    if (ex != selected && hash.Contains(ex))
                    {
                        spawners.RemoveAt(i);
                        i--;
                    }
            }
        }

        public static System.Type[] GetDefaultSpawners()
        {
            var types = new System.Type[] { typeof(MapSpawner), typeof(TerrainSpawner), typeof(SurfaceSpawner), typeof(GateSpawner), typeof(BorderColliderSpawner) };
            return types;
        }

        public static IEnumerator MakeBedOnTerrain(MicroWorld Builder, float BedPadding, float RoadRadius, float BedOffsetY, float BedHorizontality, TakenAreaType type, bool smoothEnds)
        {
            var heights = Builder.TerrainSpawner.HeightMap;
            var terrainSize = Builder.Terrain.terrainData.size;
            var CellGeometry = Builder.CellGeometry;
            var Map = Builder.Map;
            var hRes = heights.GetLength(0);
            var kx = (float)terrainSize.x / hRes;
            var ky = (float)terrainSize.z / hRes;
            var kH = 1 / (float)terrainSize.y;
            var hollowPaddingSq = BedPadding * BedPadding + 0.00001f;

            var origHeights = new float[heights.GetLength(0), heights.GetLength(1)];
            Array.Copy(heights, origHeights, heights.Length);

            yield return null;

            var smoothedHeight = new Dictionary<TakenArea, (float, float)>();

            for (int i = 1; i < hRes - 1; i++)
            {
                for (int j = 1; j < hRes - 1; j++)
                {
                    var p = new Vector3(i * kx, 0, j * ky);
                    var hex = CellGeometry.PointToHex(p);
                    var cell = Map[hex];

                    // get distance to taken areas
                    var res = cell.TakenAreas.Where(t => t.Type == type).NearestPoint(p.ToVector2(), RoadRadius);

                    // if no taken area => find taken area in neighbor cell
                    if (res.distSq > hollowPaddingSq && smoothEnds)
                    {
                        // get neighbor cell
                        var iEdge = CellGeometry.PointToEdge(hex, p);
                        var n = CellGeometry.Neighbor(hex, iEdge);
                        res = Map[n].TakenAreas.Where(t => t.Type == type).NearestPoint(p.ToVector2(), RoadRadius);
                    }

                    if (res.distSq > hollowPaddingSq) continue;

                    var ci = Mathf.RoundToInt(res.pos.x / kx);
                    var cj = Mathf.RoundToInt(res.pos.y / ky);
                    var centerH = origHeights[cj, ci];// GetSmoothedHeight(ci, cj);
                    var myH = heights[j, i];
                    //if (myH < centerH) myH = centerH;//?????
                    var h = Mathf.Lerp(myH, centerH, BedHorizontality);

                    heights[j, i] = Mathf.Lerp(h + BedOffsetY * kH, myH, res.distSq / hollowPaddingSq);
                }

                if (i % 10 == 0)
                    yield return null;
            }
        }

        public static IEnumerable<Vector2Int> GetCellsInRadius(this MicroWorld mw, Vector3 center, float radius, RadiusUnit radiusUnits)
        {
            var map = mw.Map;
            var r2 = radius;
            if (radiusUnits == RadiusUnit.CellSize)
                r2 *= map.Geometry.Radius;
            r2 *= r2;

            var myPosXZ = center.XZ();

            foreach (var hex in map.AllInsideHex())
            {
                var cellPos = mw.HexToPos(hex);
                if (cellPos.XZ().distSqr(myPosXZ) < r2)
                    yield return hex;
            }
        }

        public static IEnumerable<Vector2Int> GetCellsAround(this MicroWorld mw, IEnumerable<Vector2Int> area)
        {
            var geom = mw.CellGeometry;
            var processed = new HashSet<Vector2Int>(area);
            foreach (var hex in area)
            foreach (var n in geom.Neighbors(hex))
            if (processed.Add(n))
                yield return n;
        }

        static (Vector2Int hex, Cell cell, float weight)[] temp3 = new (Vector2Int hex, Cell cell, float weight)[3];
        static (Vector2Int hex, Cell cell, float weight)[] temp4 = new (Vector2Int hex, Cell cell, float weight)[4];
        static Vector2Int[] offsets = new Vector2Int[4] { new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) };

        /// <summary> Returns list of cells around the point, weighted </summary>
        public static (Vector2Int hex, Cell cell, float weight)[] PosToCells(this MicroWorld world, Vector3 worldPos)
        {
            var cellGeometry = world.CellGeometry;
            var map = world.Map;

            var hex = cellGeometry.PointToHex(worldPos);
            var iCorner = cellGeometry.PointToCorner(hex, worldPos);

            if (cellGeometry.CornersCount == 6)//hex grid?
            {
                var n1 = cellGeometry.Neighbor(hex, iCorner - 1);
                var n2 = cellGeometry.Neighbor(hex, iCorner);

                var p0 = cellGeometry.Center(hex); var cell0 = map[hex]; var w0 = cell0.Type.HeightPower;
                var p1 = cellGeometry.Center(n1); var cell1 = map[n1]; var w1 = cell1.Type.HeightPower;
                var p2 = cellGeometry.Center(n2); var cell2 = map[n2]; var w2 = cell2.Type.HeightPower;

                // get Barycentric coordinates of point
                var bar = Helper.Barycentric(worldPos.XZ(), p0, p1, p2);
                bar = new Vector3(Mathf.Abs(bar.x), Mathf.Abs(bar.y), Mathf.Abs(bar.z));

                bar.x *= w0;
                bar.y *= w1;
                bar.z *= w2;

                // normalize Barycentric
                bar /= bar.x + bar.y + bar.z;

                // return result
                temp3[0] = (hex, cell0, bar.x);
                temp3[1] = (n1, cell1, bar.y);
                temp3[2] = (n2, cell2, bar.z);
                return temp3;
            }
            else// rect grid
            {
                var one = offsets[iCorner];
                var n1 = hex + new Vector2Int(one.x, 0);
                var n2 = hex + one;
                var n3 = hex + new Vector2Int(0, one.y);

                var p0 = cellGeometry.Center(hex); var cell0 = map[hex]; var w0 = cell0.Type.HeightPower;
                var p1 = cellGeometry.Center(n1); var cell1 = map[n1]; var w1 = cell1.Type.HeightPower;
                var p2 = cellGeometry.Center(n2); var cell2 = map[n2]; var w2 = cell2.Type.HeightPower;
                var p3 = cellGeometry.Center(n3); var cell3 = map[n3]; var w3 = cell3.Type.HeightPower;

                var bar = Helper.BarycentricRect(worldPos.XZ(), p0, p1, p2, p3);
                bar = new Vector4(Mathf.Abs(bar.x), Mathf.Abs(bar.y), Mathf.Abs(bar.z), Mathf.Abs(bar.w));

                bar.x *= w0;
                bar.y *= w1;
                bar.z *= w2;
                bar.w *= w3;

                // normalize Barycentric
                bar /= bar.x + bar.y + bar.z + bar.w;

                // return result
                temp4[0] = (hex, cell0, bar.x);
                temp4[1] = (n1, cell1, bar.y);
                temp4[2] = (n2, cell2, bar.z);
                temp4[3] = (n3, cell3, bar.w);
                return temp4;
            }
        }

        static string[] BuiltInCellTypes = new string[] { "Border", "Gate", "Water", "Field", "Forest", "Ruins" };

        public static IEnumerable<string> ProposedCellTypes(this Component spawner)
        {
            var result = (IEnumerable<string>)BuiltInCellTypes;

            if (spawner)
            {
                var mw = spawner.GetComponentInParent<MicroWorld>(true);
                if (mw)
                {
                    var mapSpawners = mw.GetComponentsInChildren<MapSpawner>();
                    result = result.Union(mapSpawners.SelectMany(mp => mp.CellTypes).Select(t => t.Name)).Distinct();
                }
            }

            return result.OrderBy(s=>s);
        }
    }
}
