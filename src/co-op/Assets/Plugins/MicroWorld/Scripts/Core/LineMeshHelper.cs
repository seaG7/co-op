using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public static class LineMeshHelper
    {
        public static Mesh BuildMesh(TakenAreaType type, MicroWorld builder, float UVSegmentLength, float radius, float SideIncline, float OffsetY)
        {
            var Map = builder.Map;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            var vertsPerWidth = 2;
            var segments = new List<SegInfo>();

            foreach (var hex in Map.AllHex())
            {
                var cell = Map[hex];
                segments.Clear();
                segments.AddRange(cell.TakenAreas.Where(s => s.Type == type).Select(s => new SegInfo { A = s.A, B = s.B, EndV = 1f }));

                if (segments.Count == 0)
                    continue;

                var totalLength = 0f;
                var isRoadCross = cell.Content.HasFlag(CellContent.IsRoadCross);
                if (!isRoadCross && segments.Count == 3)
                {
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var s = segments[i];
                        s.Dir = s.B - s.A;
                        s.Length = s.Dir.magnitude;
                        s.Dir /= s.Length;
                        totalLength += s.Length;
                        segments[i] = s;
                    }

                    var uvSegmentCount = Mathf.Max(1, Mathf.RoundToInt(totalLength / UVSegmentLength));
                    var uvSegmentKoeff = uvSegmentCount / totalLength;
                    var startV = 0f;

                    for (int i = 1; i < segments.Count; i++)
                    {
                        var sPrev = segments[i - 1];
                        var s = segments[i];

                        startV += segments[i - 1].Length * uvSegmentCount / totalLength;
                        sPrev.AngleOut = s.AngleIn = Vector2.SignedAngle(s.Dir, sPrev.Dir);
                        sPrev.EndV = s.StartV = startV;
                        s.EndV = uvSegmentCount;

                        segments[i - 1] = sPrev;
                        segments[i] = s;
                    }


                    for (int i = 0; i < segments.Count; i++)
                        segments[i].BuildSegment(verts, tris, uvs, radius, vertsPerWidth, builder, SideIncline, OffsetY);
                }

                if (isRoadCross || segments.Count == 1)
                {
                    var C = 0;// RoadRadius;
                    for (int i = 0; i < segments.Count; i++)
                    {
                        var s = segments[i];
                        s.Dir = s.B - s.A;
                        s.Length = s.Dir.magnitude;
                        s.Dir /= s.Length;
                        s.B -= C * s.Dir;
                        s.Length -= C;
                        totalLength += s.Length;
                        segments[i] = s;
                    }

                    var uvSegmentCount = Mathf.Max(1, Mathf.RoundToInt(segments[0].Length / UVSegmentLength));
                    var uvSegmentKoeff = uvSegmentCount / totalLength;

                    for (int i = 1; i < segments.Count; i++)
                    {
                        var s = segments[i];

                        s.StartV = 0;
                        s.EndV = uvSegmentCount;

                        segments[i] = s;
                    }

                    for (int i = 0; i < segments.Count; i++)
                        segments[i].BuildSegment(verts, tris, uvs, radius, vertsPerWidth, builder, SideIncline, OffsetY);
                }
            }

            //

            var mesh = new Mesh();
            if (verts.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        static void BuildSegment(this SegInfo seg, List<Vector3> verts, List<int> tris, List<Vector2> uvs, float radius, int vertsPerWidth, MicroWorld builder, float sideIncline, float offsetY)
        {
            var start = verts.Count;
            var segVerts = GetSegmentVerticies(seg, radius).ToList();
            for (var i = 0; i < segVerts.Count; i += vertsPerWidth)
            {
                var center = (segVerts[i].pos + segVerts[i + vertsPerWidth - 1].pos) / 2;
                var centerH = builder.Terrain.SampleHeight(center);
                //if (centerH < builder.TerrainSpawner.WaterLevel - 1f)
                //    continue;

                for (int j = i; j < i + vertsPerWidth; j++)
                {
                    var pair = segVerts[j];
                    var myH = builder.Terrain.SampleHeight(pair.pos);
                    myH = Math.Max(centerH, Mathf.Lerp(centerH, myH, sideIncline));
                    uvs.Add(pair.uv);
                    verts.Add(pair.pos.withSetY(myH + offsetY));
                }
            }

            for (int i = 0; i < segVerts.Count - vertsPerWidth; i += vertsPerWidth)
            {
                for (int j = 0; j < vertsPerWidth - 1; j++)
                {
                    var i0 = start + i + j;
                    var i1 = i0 + vertsPerWidth;
                    var i2 = i1 + 1;
                    var i3 = i0 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i0); tris.Add(i3); tris.Add(i2);
                }
            }
        }

        static IEnumerable<(Vector3 pos, Vector2 uv)> GetSegmentVerticies(SegInfo seg, float radius)//RiverRadius + MeshPadding
        {
            var n = seg.Dir.Rotate(90);
            var d0 = radius * Mathf.Tan(seg.AngleIn * Mathf.Deg2Rad / 2);
            var d1 = radius * Mathf.Tan(seg.AngleOut * Mathf.Deg2Rad / 2);
            var a0 = seg.A - n * radius + d0 * seg.Dir;
            var a1 = seg.A + n * radius - d0 * seg.Dir;
            var b0 = seg.B - n * radius - d1 * seg.Dir;
            var b1 = seg.B + n * radius + d1 * seg.Dir;

            var segmentsCount = 10;
            for (int i = 0; i < segmentsCount; i++)
            {
                var t = i / (segmentsCount - 1f);
                var p0 = Vector2.Lerp(a0, b0, t);
                var p1 = Vector2.Lerp(a1, b1, t);
                var v = Mathf.Lerp(seg.StartV, seg.EndV, t);
                yield return (p0.ToVector3(), new Vector2(0, v));
                yield return (p1.ToVector3(), new Vector2(1, v));
            }
        }
    }

    public struct SegInfo
    {
        public Vector2 A;
        public Vector2 B;
        public Vector2 Dir;
        public float Length;
        public float AngleIn;
        public float AngleOut;
        public float StartV;
        public float EndV;
    }
}
