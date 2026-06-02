// ===== Core/TakenArea.cs =====
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MicroWorldNS
{
    [Serializable]
    public class TakenArea
    {
        public Vector2 A;
        public Vector2 B;
        public float Radius;
        ColliderType colliderType;
        public TakenAreaType Type;

        public TakenArea(Vector2 pos, float radius, TakenAreaType type = 0)
        {
            B = A = pos;
            Radius = radius;
            colliderType = ColliderType.Point;
            Type = type;
        }

        public TakenArea(Vector2 a, Vector2 b, float radius, TakenAreaType type = 0)
        {
            A = a;
            B = b;
            Radius = radius;
            colliderType = ColliderType.Line;
            Type = type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasIntersection(Vector2 pos, float radius)
        {
            var distSqr = Radius + radius;
            distSqr *= distSqr;

            return MinDistanceSq(pos) <= distSqr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float MinDistanceSq(Vector2 pos)
        {
            switch (colliderType)
            {
                case ColliderType.Point:
                    return (pos - A).sqrMagnitude;

                case ColliderType.Line:
                    return Helper.DistanceSqToSegment(A, B, pos);
            }

            throw new Exception("Unknown type of area: " + colliderType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 NearestPoint(Vector2 pos)
        {
            switch (colliderType)
            {
                case ColliderType.Point:
                    return A;

                case ColliderType.Line:
                    return Helper.NearestPointOnSegment(A, B, pos);
            }

            throw new Exception("Unknown type of area: " + colliderType);
        }

        enum ColliderType : byte
        {
            Point = 0, Line = 1
        }
    }

    public enum TakenAreaType : byte
    {
        Object = 0, 
        Road = 1, 
        River = 2,
        Railway = 3 // <--- ДОБАВЬТЕ ЭТУ СТРОКУ
    }

    public static class TakenAreaHelper
    {
        public static bool HasIntersection(this IEnumerable<TakenArea> TakenAreas, Vector2 pos, float radius)
        {
            foreach (var item in TakenAreas)
                if (item.HasIntersection(pos, radius))
                    return true;

            return false;
        }

        public static float MinDistanceSq(this IEnumerable<TakenArea> TakenAreas, Vector2 pos)
        {
            var res = float.MaxValue;
            foreach (var item in TakenAreas)
                res = Mathf.Min(res, item.MinDistanceSq(pos));

            return res;
        }

        public static float MinDistanceToEdgeSq(this IEnumerable<TakenArea> TakenAreas, Vector2 pos)
        {
            var res = float.MaxValue;
            foreach (var item in TakenAreas)
            {
                var itemR2 = item.Radius * item.Radius;
                var dist = Mathf.Max(0, item.MinDistanceSq(pos) - itemR2);
                res = Mathf.Min(res, dist);
            }

            return res;
        }

        public static (TakenArea area, Vector2 pos, float distSq) NearestPoint(this IEnumerable<TakenArea> TakenAreas, Vector2 pos)
        {
            var res = float.MaxValue;
            var minDist = float.MaxValue;
            var point = Vector2.zero;
            var area = default(TakenArea);
            foreach (var item in TakenAreas)
            {
                var itemR2 = item.Radius * item.Radius;
                var nearestPoint = item.NearestPoint(pos);
                var distSq = (nearestPoint - pos).sqrMagnitude;
                var dist = Mathf.Max(0, distSq - itemR2);
                if (distSq < minDist)
                {
                    minDist = distSq;
                    res = dist;
                    point = nearestPoint;
                    area = item;
                }
            }

            return (area, point, res);
        }

        public static (TakenArea area, Vector2 pos, float distSq) NearestPoint(this IEnumerable<TakenArea> TakenAreas, Vector2 pos, float radius)
        {
            var res = float.MaxValue;
            var minDist = float.MaxValue;
            var point = Vector2.zero;
            radius = Mathf.Max(0, radius);
            var area = default(TakenArea);
            var itemR2 = radius * radius;
            foreach (var item in TakenAreas)
            {
                var nearestPoint = item.NearestPoint(pos);
                var distSq = (nearestPoint - pos).sqrMagnitude;
                var dist = Mathf.Max(0, distSq - itemR2);
                if (distSq < minDist)
                {
                    minDist = distSq;
                    res = dist;
                    point = nearestPoint;
                    area = item;
                }
            }

            return (area, point, res);
        }
    }
}