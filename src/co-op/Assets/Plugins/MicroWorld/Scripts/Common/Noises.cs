using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public class Noises
    {
        private static Islands islands;
        private static IslandsNoIntersections smoothedIslands;

        public static Islands Islands { get => islands ??= new Islands(); }
        public static IslandsNoIntersections SmoothedIslands { get => smoothedIslands ??= new IslandsNoIntersections(); }
    }

    public class Islands
    {
        public const int Size = 1024;
        public int[][] rows;

        static Color[] colors = new[] { Color.magenta, Color.blue, Color.red, Color.green, Color.grey, Color.cyan, Color.yellow, Color.white, Color.gray * 0.8f, Color.magenta * 0.8f, Color.blue * 0.8f, Color.red * 0.8f, Color.green * 0.8f, Color.cyan * 0.8f, Color.yellow * 0.8f };

        public Islands()
        {
            rows = new int[Size][];
            for (int i = 0; i < Size; i++)
                rows[i] = new int[Size];

            Build();
        }

        public int this[Vector2Int p]
        {
            get => rows[p.y.Mod(Size)][p.x.Mod(Size)];
            set => rows[p.y.Mod(Size)][p.x.Mod(Size)] = value;
        }

        public int this[int x, int y]
        {
            get => rows[y.Mod(Size)][x.Mod(Size)];
            set => rows[y.Mod(Size)][x.Mod(Size)] = value;
        }

        protected virtual void Build()
        {
            var centers = new List<Vector2Int>();
            foreach (var center in Rnd.HaltonSequence2(17).Take(1200))
                centers.Add(Vector2Int.RoundToInt(center * Size * 3));

            var rnd = new Rnd(123);

            for (int i = 0; i < 100; i++)
            {
                for (int iClass = 0; iClass < centers.Count; iClass++)
                {
                    const int d = 25;
                    var p = centers[iClass] + Vector2Int.RoundToInt(new Vector2(rnd.Triangle(-d, d), rnd.Triangle(-d, d)));
                    this[p] = iClass + 1;
                }
            }

            Grow();
        }

        protected void Grow()
        {
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            for (int i = 0; i < Size; i++)
                for (int j = 0; j < Size; j++)
                {
                    var p = new Vector2Int(i, j);
                    if (this[p] > 0)
                        queue.Enqueue(p);
                }

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var n in p.Neighbors8())
                    if (this[n] == 0)
                    {
                        this[n] = this[p];
                        queue.Enqueue(n);
                    }
            }
        }

        public void MakeDemoTexture(string path = "d:\\temp.png")
        {
            var texture = new Texture2D(Size, Size, TextureFormat.ARGB32, true, true);
            texture.filterMode = FilterMode.Point;
            var pixels = texture.GetPixels();

            for (int i = Size * Size - 1; i >= 0; i--)
            {
                var val = rows[i / Size][i % Size];
                pixels[i] = colors[val % colors.Length];
            }

            texture.SetPixels(pixels);
            texture.Apply();
            var bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }
    }

    public class IslandsNoIntersections : Islands
    {
        protected override void Build()
        {
            var centers = new List<Vector2Int>();
            var classId = 1;
            foreach (var center in Rnd.HaltonSequence2(17).Take(1200))
                this[Vector2Int.RoundToInt(center * Size * 3)] = classId++;

            Grow();
        }
    }
}