using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroWorldNS
{
    public static class List
    {
        public static T GetItemSafe<T>(this IList<T> list, int index)
        {
            if (list.Count == 0)
                return default;

            var count = list.Count;
            return list[(index + count) % count];
        }

        public static List<T> AddFirstToEnd<T>(this IEnumerable<T> list)
        {
            var res = new List<T>(list);
            if (res.Count > 0)
                res.Add(res[0]);
            return res;
        }

        public static void AddRange<T>(this Queue<T> queue, IEnumerable<T> enumerable)
        {
            foreach (var item in enumerable)
                queue.Enqueue(item);
        }

        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> enumerable)
        {
            foreach (var item in enumerable)
                hashSet.Add(item);
        }

        public static void AddRange<T>(this ICollection<T> list, IEnumerable<T> enumerable)
        {
            foreach (var item in enumerable)
                list.Add(item);
        }

        public static Val GetOrCreate<Key, Val>(this IDictionary<Key, Val> dict, Key key) where Val : new()
        {
            if (dict.TryGetValue(key, out var v))
                return v;
            return dict[key] = new Val();
        }

        public static List<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            var list = collection.ToList();
            foreach (var item in list)
            {
                action(item);
            }

            return list;
        }

        public static List<T> For<T>(this IEnumerable<T> collection, Action<int> action)
        {
            var list = collection.ToList();
            for (int i = 0; i < list.Count; i++)
                action(i);

            return list;
        }

        public static List<T> For<T>(this IEnumerable<T> collection, Action<int, T> action)
        {
            var list = collection.ToList();
            for (int i = 0; i < list.Count; i++)
                action(i, list[i]);

            return list;
        }

        public static T Next<T>(this IList<T> list, int i)
        {
            if (list.Count == 0) return default;
            i++;
            return i >= list.Count ? list[0] : list[i];
        }

        public static T Prev<T>(this IList<T> list, int i)
        {
            if (list.Count == 0) return default;
            i--;
            return i < 0 ? list[list.Count - 1] : list[i];
        }

        /// <summary> Allows enumerate list with add/remove elemenets inside foreach </summary>
        public static IEnumerable<T> SafeEnumerator<T>(this IList<T> collection)
        {
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                yield return collection[i];
            }
        }

        public static T MinItem<T>(this IEnumerable<T> items, Func<T, float> distance)
        {
            var min = float.MaxValue;
            var best = default(T);
            foreach (var item in items)
            {
                var dist = distance(item);
                if (dist < min)
                {
                    min = dist;
                    best = item;
                }
            }

            return best;
        }

        public static T MaxItem<T>(this IEnumerable<T> items, Func<T, float> distance)
        {
            var max = float.MinValue;
            var best = default(T);
            foreach (var item in items)
            {
                var dist = distance(item);
                if (dist > max)
                {
                    max = dist;
                    best = item;
                }
            }

            return best;
        }
    }
}