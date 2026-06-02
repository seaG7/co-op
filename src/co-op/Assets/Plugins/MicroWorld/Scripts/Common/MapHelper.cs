using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MicroWorldNS
{
    public static class MapHelper
    {
        public static IEnumerable<(int x, int y)> GetSpiralFromCenter(int width, int height)
        {
            return GetSpiralFromCenter(0, 0, width, height);
        }

        public static IEnumerable<(int x, int y)> GetSpiralFromCenter(int startX, int startY, int width, int height)
        {
            var radius = Math.Max(width, height) / 2;
            var centerX = startX + width / 2;
            var centerY = startY + height / 2;

            var boundX = startX + width;
            var boundY = startY + height;

            if (Check(centerX, centerY, out var p)) yield return p;

            for (var r = 1; r <= radius; r++)
            {
                var fromX = centerX - r;
                var fromY = centerY - r;
                var toX = centerX + r;
                var toY = centerY + r;

                for (int x = 0; x <= r; x++)
                {
                    if (Check(centerX + x, fromY, out p)) yield return p;
                    if (Check(centerX + x, toY, out p)) yield return p;
                    if (x > 0)
                    {
                        if (Check(centerX - x, fromY, out p)) yield return p;
                        if (Check(centerX - x, toY, out p)) yield return p;
                    }
                }

                for (int y = 0; y < r; y++)
                {
                    if (Check(fromX, centerY + y, out p)) yield return p;
                    if (Check(toX, centerY + y, out p)) yield return p;
                    if (y > 0)
                    {
                        if (Check(fromX, centerY - y, out p)) yield return p;
                        if (Check(toX, centerY - y, out p)) yield return p;
                    }
                }
            }

            bool Check(int xx, int yy, out (int, int) point)
            {
                point = (xx, yy);
                return xx >= startX && xx < boundX && yy >= startY && yy < boundY;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ManhattenLength(this Vector2Int v)
        {
            return Mathf.Abs(v.x) + Mathf.Abs(v.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int Rotate(this Vector2Int offset, int rot90)
        {
            //rotate offset
            for (int i = 0; i < rot90; i++)
                offset = offset.Rotate90CW();
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int Neighbor(this Vector2Int p, int dir03)
        {
            switch (dir03)
            {
                case 0: return p + Vector2Int.up;
                case 1: return p + Vector2Int.right;
                case 2: return p + Vector2Int.down;
                case 3: return p + Vector2Int.left;
            }
            throw new System.Exception("Direction must be only 0-3");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2Int Neighbor8(this Vector2Int p, int dir07)
        {
            switch (dir07)
            {
                case 0: return new Vector2Int(p.x + 0, p.y + 1);//
                case 1: return new Vector2Int(p.x + 1, p.y + 1);//
                case 2: return new Vector2Int(p.x + 1, p.y + 0);//
                case 3: return new Vector2Int(p.x + 1, p.y - 1);
                case 4: return new Vector2Int(p.x + 0, p.y - 1);//
                case 5: return new Vector2Int(p.x - 1, p.y - 1);//
                case 6: return new Vector2Int(p.x - 1, p.y + 0);//
                case 7: return new Vector2Int(p.x - 1, p.y + 1);
            }
            throw new System.Exception("Direction must be only 0-7");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> Neighbors(this Vector2Int p, int radius, bool includeMe = false)
        {
            for (int xx = p.x - radius; xx <= p.x + radius; xx++)
                for (int yy = p.y - radius; yy <= p.y + radius; yy++)
                    if (includeMe || xx != p.x || yy != p.y)
                        yield return new Vector2Int(xx, yy);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> Neighbors(this Vector2Int p)
        {
            yield return p + Vector2Int.up;
            yield return p + Vector2Int.right;
            yield return p + Vector2Int.down;
            yield return p + Vector2Int.left;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> NeighborsAndMe(this Vector2Int p)
        {
            yield return p;
            yield return p + Vector2Int.up;
            yield return p + Vector2Int.right;
            yield return p + Vector2Int.down;
            yield return p + Vector2Int.left;
        }

        public static RectInt GetBounds(this IEnumerable<Vector2Int> cells)
        {
            if (!cells.Any()) return default;
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            var res = new RectInt();
            res.min = new Vector2Int(minX, minY);
            res.max = new Vector2Int(maxX + 1, maxY + 1);

            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> Neighbors8(this Vector2Int p)
        {
            yield return new Vector2Int(p.x + 0, p.y + 1);
            yield return new Vector2Int(p.x + 1, p.y + 1);
            yield return new Vector2Int(p.x + 1, p.y + 0);
            yield return new Vector2Int(p.x + 1, p.y - 1);
            yield return new Vector2Int(p.x + 0, p.y - 1);
            yield return new Vector2Int(p.x - 1, p.y - 1);
            yield return new Vector2Int(p.x - 1, p.y + 0);
            yield return new Vector2Int(p.x - 1, p.y + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> Neighbors48(this Vector2Int p)
        {
            yield return new Vector2Int(p.x + 0, p.y + 1);
            yield return new Vector2Int(p.x + 1, p.y + 0);
            yield return new Vector2Int(p.x + 0, p.y - 1);
            yield return new Vector2Int(p.x - 1, p.y + 0);
            yield return new Vector2Int(p.x + 1, p.y + 1);
            yield return new Vector2Int(p.x + 1, p.y - 1);
            yield return new Vector2Int(p.x - 1, p.y - 1);
            yield return new Vector2Int(p.x - 1, p.y + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> Neighbors8AndMe(this Vector2Int p)
        {
            yield return p;
            yield return new Vector2Int(p.x + 0, p.y + 1);
            yield return new Vector2Int(p.x + 1, p.y + 1);
            yield return new Vector2Int(p.x + 1, p.y + 0);
            yield return new Vector2Int(p.x + 1, p.y - 1);
            yield return new Vector2Int(p.x + 0, p.y - 1);
            yield return new Vector2Int(p.x - 1, p.y - 1);
            yield return new Vector2Int(p.x - 1, p.y + 0);
            yield return new Vector2Int(p.x - 1, p.y + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> NeighborsSafe<T>(this Vector2Int p, T[,] map)
        {
            if (p.x < 1 || p.y < 1 || p.x >= map.GetLength(0) - 1 || p.y >= map.GetLength(1) - 1)
            {
                var q = p + Vector2Int.up;
                if (map.InBounds(q)) yield return q;

                q = p + Vector2Int.right;
                if (map.InBounds(q)) yield return q;

                q = p + Vector2Int.down;
                if (map.InBounds(q)) yield return q;

                q = p + Vector2Int.left;
                if (map.InBounds(q)) yield return q;
            }
            else
            {
                yield return p + Vector2Int.up;
                yield return p + Vector2Int.right;
                yield return p + Vector2Int.down;
                yield return p + Vector2Int.left;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> NeighborsSafe8<T>(this Vector2Int p, T[,] map)
        {
            if (p.x < 1 || p.y < 1 || p.x >= map.GetLength(0) - 1 || p.y >= map.GetLength(1) - 1)
            {
                var q = new Vector2Int(p.x + 0, p.y + 1); if (map.InBounds(q)) yield return q;
                q = new Vector2Int(p.x + 1, p.y + 1); if (map.InBounds(q)) yield return q;
                q = new Vector2Int(p.x + 1, p.y + 0); if (map.InBounds(q)) yield return q;
                q = new Vector2Int(p.x + 1, p.y - 1); if (map.InBounds(q)) yield return q;
                q = new Vector2Int(p.x + 0, p.y - 1); if (map.InBounds(q)) yield return q;
                q = new Vector2Int(p.x - 1, p.y - 1); if (map.InBounds(q)) yield return q;
                q = new Vector2Int(p.x - 1, p.y + 0); if (map.InBounds(q)) yield return q;
                q = new Vector2Int(p.x - 1, p.y + 1); if (map.InBounds(q)) yield return q;

            }
            else
            {
                yield return new Vector2Int(p.x + 0, p.y + 1);
                yield return new Vector2Int(p.x + 1, p.y + 1);
                yield return new Vector2Int(p.x + 1, p.y + 0);
                yield return new Vector2Int(p.x + 1, p.y - 1);
                yield return new Vector2Int(p.x + 0, p.y - 1);
                yield return new Vector2Int(p.x - 1, p.y - 1);
                yield return new Vector2Int(p.x - 1, p.y + 0);
                yield return new Vector2Int(p.x - 1, p.y + 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<Vector2Int> NeighborsSafe<T>(this Vector2Int p, T[,] map, Func<T, bool> condition)
        {
            if (p.x < 1 || p.y < 1 || p.x >= map.GetLength(0) - 1 || p.y >= map.GetLength(1) - 1)
            {
                var q = p + Vector2Int.up;
                if (map.InBounds(q) && condition(map[q.x, q.y])) yield return q;

                q = p + Vector2Int.right;
                if (map.InBounds(q) && condition(map[q.x, q.y])) yield return q;

                q = p + Vector2Int.down;
                if (map.InBounds(q) && condition(map[q.x, q.y])) yield return q;

                q = p + Vector2Int.left;
                if (map.InBounds(q) && condition(map[q.x, q.y])) yield return q;
            }
            else
            {
                if (condition(map[p.x, p.y + 1])) yield return new Vector2Int(p.x, p.y + 1);
                if (condition(map[p.x + 1, p.y])) yield return new Vector2Int(p.x + 1, p.y);
                if (condition(map[p.x, p.y - 1])) yield return new Vector2Int(p.x, p.y - 1);
                if (condition(map[p.x - 1, p.y])) yield return new Vector2Int(p.x - 1, p.y);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Neighbor<T>(this T[,] map, Vector2Int p, int dir03)
        {
            switch (dir03)
            {
                case 0: return map[p.x, p.y + 1];
                case 1: return map[p.x + 1, p.y];
                case 2: return map[p.x, p.y - 1];
                case 3: return map[p.x - 1, p.y];
            }
            throw new System.Exception("Direction must be only 0-3");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Neighbor8<T>(this T[,] map, Vector2Int p, int dir07)
        {
            switch (dir07)
            {
                case 0: return map[p.x + 0, p.y + 1];
                case 1: return map[p.x + 1, p.y + 1];
                case 2: return map[p.x + 1, p.y + 0];
                case 3: return map[p.x + 1, p.y - 1];
                case 4: return map[p.x + 0, p.y - 1];
                case 5: return map[p.x - 1, p.y - 1];
                case 6: return map[p.x - 1, p.y + 0];
                case 7: return map[p.x - 1, p.y + 1];
            }
            throw new System.Exception("Direction must be only 0-7");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NeighborSafe<T>(this T[,] map, Vector2Int p, int dir03)
        {
            switch (dir03)
            {
                case 0: return GetSafe(map, p + Vector2Int.up);
                case 1: return GetSafe(map, p + Vector2Int.right);
                case 2: return GetSafe(map, p + Vector2Int.down);
                case 3: return GetSafe(map, p + Vector2Int.left);
            }
            throw new System.Exception("Direction must be only 0-3");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> Neighbors<T>(this T[,] map, Vector2Int p)
        {
            yield return map[p.x, p.y + 1];
            yield return map[p.x + 1, p.y];
            yield return map[p.x, p.y - 1];
            yield return map[p.x - 1, p.y];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> NeighborsSafe<T>(this T[,] map, Vector2Int p)
        {
            if (p.x < 1 || p.y < 1 || p.x >= map.GetLength(0) - 1 || p.y >= map.GetLength(1) - 1)
            {
                var q = p + Vector2Int.up;
                if (map.InBounds(q)) yield return map[q.x, q.y];

                q = p + Vector2Int.right;
                if (map.InBounds(q)) yield return map[q.x, q.y];

                q = p + Vector2Int.down;
                if (map.InBounds(q)) yield return map[q.x, q.y];

                q = p + Vector2Int.left;
                if (map.InBounds(q)) yield return map[q.x, q.y];
            }
            else
            {
                yield return map[p.x, p.y + 1];
                yield return map[p.x + 1, p.y];
                yield return map[p.x, p.y - 1];
                yield return map[p.x - 1, p.y];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> NeighborsSafe<T>(this T[,] map, Vector2Int p, Func<T, bool> condition)
        {
            if (p.x < 1 || p.y < 1 || p.x >= map.GetLength(0) - 1 || p.y >= map.GetLength(1) - 1)
            {
                var q = p + Vector2Int.up;
                if (map.InBounds(q) && condition(map[q.x, q.y])) yield return map[q.x, q.y];

                q = p + Vector2Int.right;
                if (map.InBounds(q) && condition(map[q.x, q.y])) yield return map[q.x, q.y];

                q = p + Vector2Int.down;
                if (map.InBounds(q) && condition(map[q.x, q.y])) yield return map[q.x, q.y];

                q = p + Vector2Int.left;
                if (map.InBounds(q) && condition(map[q.x, q.y])) yield return map[q.x, q.y];
            }
            else
            {
                if (condition(map[p.x, p.y + 1])) yield return map[p.x, p.y + 1];
                if (condition(map[p.x + 1, p.y])) yield return map[p.x + 1, p.y];
                if (condition(map[p.x, p.y - 1])) yield return map[p.x, p.y - 1];
                if (condition(map[p.x - 1, p.y])) yield return map[p.x - 1, p.y];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>(this T[,] map, Vector2Int p)
        {
            return map[p.x, p.y];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Set<T>(this T[,] map, Vector2Int p, T val)
        {
            return map[p.x, p.y] = val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetSafe<T>(this T[,] map, Vector2Int p)
        {
            if (p.x < 0 || p.y < 0 || p.x >= map.GetLength(0) || p.y >= map.GetLength(1))
                return default(T);

            return map[p.x, p.y];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T SetSafe<T>(this T[,] map, Vector2Int p, T val)
        {
            if (p.x < 0 || p.y < 0 || p.x >= map.GetLength(0) || p.y >= map.GetLength(1))
                return default(T);

            return map[p.x, p.y] = val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InBounds<T>(this T[,] map, Vector2Int p)
        {
            if (p.x < 0 || p.y < 0 || p.x >= map.GetLength(0) || p.y >= map.GetLength(1))
                return false;

            return true;
        }

        public static void Fill<T>(this T[,] map) where T : class, new()
        {
            var w = map.GetLength(0) + map.GetLowerBound(0) * 2;
            var h = map.GetLength(1) + map.GetLowerBound(1) * 2;

            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    map[i, j] = new T();
        }

        public static void Fill<T>(this T[,] map, Func<Vector2Int, T> creator)
        {
            var w = map.GetLength(0) + map.GetLowerBound(0) * 2;
            var h = map.GetLength(1) + map.GetLowerBound(1) * 2;

            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    map[i, j] = creator(new Vector2Int(i, j));
        }

        public static void Fill<T>(this T[,] map, RectInt rect, Func<Vector2Int, T> creator)
        {
            var w = map.GetLength(0) + map.GetLowerBound(0) * 2;
            var h = map.GetLength(1) + map.GetLowerBound(1) * 2;
            rect.ClampToBounds(new RectInt(0, 0, w, h));

            foreach (var p in rect.allPositionsWithin)
                map[p.x, p.y] = creator(p);
        }

        public static RectInt Expand(this RectInt rect, int dx, int dy)
        {
            return new RectInt(rect.x - dx, rect.y - dy, rect.width + 2 * dx, rect.height + 2 * dy);
        }

        public static RectInt Expand(this RectInt rect, int dxLeft, int dxRight, int dyBottom, int dyTop)
        {
            return new RectInt(rect.x - dxLeft, rect.y - dyBottom, rect.width + dxLeft + dxRight, rect.height + dyBottom + dyTop);
        }

        public static RectInt Padding(this RectInt rect, int padX, int padY)
        {
            return new RectInt(rect.x + padX, rect.y + padY, rect.width - 2 * padX, rect.height - 2 * padY);
        }

        public static RectInt Padding(this RectInt rect, int padLeft, int padRight, int padBottom, int padTop)
        {
            return new RectInt(rect.x + padLeft, rect.y + padBottom, rect.width - padLeft - padRight, rect.height - padBottom - padTop);
        }

        public static RectInt Intersection(this RectInt rect, RectInt other)
        {
            var minX = Mathf.Max(rect.xMin, other.xMin);
            var minY = Mathf.Max(rect.yMin, other.yMin);

            var maxX = Mathf.Min(rect.xMax, other.xMax);
            var maxY = Mathf.Min(rect.yMax, other.yMax);

            return new RectInt(minX, minY, maxX - minX, maxY - minY);
        }

        public static int GetDistanceToBorder(this RectInt rect, Vector2Int p)
        {
            var dx = Mathf.Min(Mathf.Abs(p.x - rect.xMin), Mathf.Abs(p.x - rect.xMax));
            var dy = Mathf.Min(Mathf.Abs(p.y - rect.yMin), Mathf.Abs(p.y - rect.yMax));
            return Mathf.Min(dx, dy);
        }

        public static IEnumerable<Vector2Int> GetBorder(this RectInt rect, int padding = 0)
        {
            var fromX = rect.x + padding;
            var fromY = rect.y + padding;
            var toX = rect.xMax - padding;
            var toY = rect.yMax - padding;
            var w = rect.width - 2 * padding;
            var h = rect.height - 2 * padding;

            for (int x = fromX; x <= toX; x++)
                yield return new Vector2Int(x, fromY);

            if (h > 1)
                for (int y = fromY + 1; y <= toY - 1; y++)
                    yield return new Vector2Int(toX, y);

            if (h > 0)
                for (int x = toX; x >= fromX; x--)
                    yield return new Vector2Int(x, toY);

            if (w > 0 && h > 1)
                for (int y = toY - 1; y >= fromY + 1; y--)
                    yield return new Vector2Int(fromX, y);
        }

        public static IEnumerable<Vector2Int> GetBorder2(this RectInt rect, int padding = 0)
        {
            var fromX = rect.x + padding;
            var fromY = rect.y + padding;
            var toX = rect.xMax - padding - 1;
            var toY = rect.yMax - padding - 1;
            var w = rect.width - 2 * padding;
            var h = rect.height - 2 * padding;

            for (int x = fromX; x <= toX; x++)
                yield return new Vector2Int(x, fromY);

            if (h > 1)
                for (int y = fromY + 1; y <= toY - 1; y++)
                    yield return new Vector2Int(toX, y);

            if (h > 0)
                for (int x = toX; x >= fromX; x--)
                    yield return new Vector2Int(x, toY);

            if (w > 0 && h > 1)
                for (int y = toY - 1; y >= fromY + 1; y--)
                    yield return new Vector2Int(fromX, y);
        }

        public static IEnumerable<Vector2Int> AllPoints<T>(this T[,] map)
        {
            var w = map.GetLength(0) + map.GetLowerBound(0) * 2;
            var h = map.GetLength(1) + map.GetLowerBound(1) * 2;

            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    yield return new Vector2Int(i, j);
        }

        public static void GetSize<T>(this T[,] map, out int w, out int h)
        {
            w = map.GetLength(0) + map.GetLowerBound(0) * 2;
            h = map.GetLength(1) + map.GetLowerBound(1) * 2;
        }

        public static RectInt GetRect<T>(this T[,] map)
        {
            var w = map.GetLength(0) + map.GetLowerBound(0) * 2;
            var h = map.GetLength(1) + map.GetLowerBound(1) * 2;

            return new RectInt(0, 0, w, h);
        }

        public static T[,] Clone<T>(this T[,] map)
        {
            var w = map.GetLength(0);
            var h = map.GetLength(1);
            var fromX = map.GetLowerBound(0);
            var fromY = map.GetLowerBound(1);

            var res = new T[w, h];

            for (int i = fromX; i < w; i++)
                for (int j = fromY; j < h; j++)
                    res[i, j] = map[i, j];

            return res;
        }

        public static T[,] DeepClone<T>(this T[,] map) where T : ICloneable
        {
            var w = map.GetLength(0);
            var h = map.GetLength(1);
            var fromX = map.GetLowerBound(0);
            var fromY = map.GetLowerBound(1);

            var res = new T[w, h];

            for (int i = fromX; i < w; i++)
                for (int j = fromY; j < h; j++)
                    res[i, j] = (T)map[i, j].Clone();

            return res;
        }

        public static T[,] CreateSafeBorderMap<T>(int w, int h, T defaultOutOfBorderElement)
        {
            var map = (T[,])Array.CreateInstance(typeof(T), new int[] { w + 2, h + 2 }, new int[] { -1, -1 });

            foreach (var p in new RectInt(-1, -1, w + 2, h + 2).GetBorder())
                map[p.x, p.y] = defaultOutOfBorderElement;

            return map;
        }

        public static T[,] CreateSafeBorderMap<T>(int w, int h)
        {
            var map = (T[,])Array.CreateInstance(typeof(T), new int[] { w + 2, h + 2 }, new int[] { -1, -1 });

            return map;
        }
    }
}