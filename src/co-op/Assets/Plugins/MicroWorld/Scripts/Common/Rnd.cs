#define UNITY

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;

namespace MicroWorldNS
{
    public class Rnd
    {
        int seed;
        Random rnd;

        public static Rnd Instance { get; private set; } = new Rnd();

        public Rnd(int seed)
        {
            this.seed = seed;
            rnd = new Random(seed);
        }

        public Rnd(string strSeed, int intSeed) : this(CombineHashCodes(intSeed, strSeed))
        {
        }

        public Rnd() : this(Environment.TickCount)
        {
        }

        #region Base Random methods

        /// <summary>Returns random: An int [0..Int32.MaxValue)</summary>
        public int Int()
        {
            return rnd.Next();
        }

        /// <summary>Returns random int: from 0 to exclusiveTo (exclusive upper bound)</summary>
        public int Int(int exclusiveTo)
        {
            return rnd.Next(exclusiveTo);
        }

        /// <summary>Returns random int: from 0 to exclusiveTo (exclusive upper bound)</summary>
        public int Int(int inclusiveFrom, int exclusiveTo)
        {
            return rnd.Next(inclusiveFrom, exclusiveTo);
        }

        /// <summary>Return random float from 0 to 1</summary>
        public float Float()
        {
            return (float)rnd.NextDouble();
        }

        /// <summary>Return random float from 0 to 1</summary>
        public float Float(float to)
        {
            return Float(0f, to);
        }

        /// <summary>Return random float from 0 to 1</summary>
        public float Float(float from, float to)
        {
            if (to < from)
                return from;

            var d = rnd.NextDouble();
            return from + (float)(d * (to - from));
        }

        /// <summary>Return random float from 0 to 1</summary>
        public float Float(UnityEngine.Vector2 diapason) => Float(diapason.x, diapason.y);

        /// <summary> Return True with defined probability </summary>
        public bool Bool(float probability = 0.5f)
        {
            return Float() <= probability;
        }

        #endregion

        #region Different distributions

        /// <summary>
        /// Чем ближе точка value к центру между triangleFrom и triangleTo - тем больше вероятность возврата true.
        /// В центре - вероятность 1, на краях и за пределами triangleFrom и triangleTo - вероятность возврата true = 0.
        /// </summary>
        public bool InTriangle(float value, float triangleFrom, float triangleTo)
        {
            var d = (triangleTo - triangleFrom) / 2;
            var center = triangleFrom + d;
            var dist = Math.Abs(value - center);
            return Float(0f, d) > dist;
        }

        /// <summary>
        /// Random float value (Triangle distribution)
        /// </summary>
        public float Triangle(float from, float to)
        {
            var v = (Float() + Float()) / 2f;
            return from + v * (to - from);
        }

        /// <summary>
        /// Inverted Triangle distribution (max - on borders of diapason, min - in center)
        /// </summary>
        public float TriangleInv(float from, float to)
        {
            var v = (Float() + Float());
            if (v > 1) v = v - 2;
            v = (v + 1) / 2f;
            return from + v * (to - from);
        }

        /// <summary>
        /// Random float value (Simple Log distribution)
        /// </summary>
        public float Log(float from, float to)
        {
            var v = Math.Abs(Float() + Float() - 1f);
            return from + v * (to - from);
        }

        /// <summary>
        /// When factor = 1 - uniform distribution in range
        /// When factor = 2 - lowest values appear often
        /// When factor = 1/2 - highest values appear often
        /// </summary>
        public float Log(float from, float to, float factor)
        {
            var v = (float)Math.Pow(Float(), factor);
            return from + v * (to - from);
        }

        /// <summary>
        /// Random float value (Gauss distribution)
        /// </summary>
        public float Gauss(float center, float sigma)
        {
            return center + sigma * (Float() + Float() + Float() + Float() - 2f) / 2f;
        }

        /// <summary>
        /// Random float value (Gauss distribution by BoxMuller formula)
        /// </summary>
        public float GaussBoxMuller(float center = 0, float sigma = 1)
        {
            //якщо невикористаного значення немає - генерується наступна пара
            float x = 0f, y = 0f, R = 2f;
            while (R > 1f || R <= float.Epsilon)
            {
                //генеруються випадкові x та y, поки не буде виконана умова R <= 1
                x = Float() * 2 - 1;
                y = Float() * 2 - 1;
                R = x * x + y * y;
            }
            double t = Math.Sqrt(-2 * Math.Log(R) / R);
            var res = (float)(x * t);
            res = res * sigma + center;
            return res;
        }

        /// <summary>
        /// Int value from Puasson distribution
        /// </summary>
        public int Poisson(float avg)
        {
            // Пороговое значение, при котором переходим на нормальное распределение
            const double threshold = 10.0;

            // Используем Пуассоновское распределение для малых значений
            if (avg < threshold)
                return GetPoisson(avg);

            // Используем нормальное распределение для больших значений
            var res = GaussBoxMuller(avg, MathF.Sqrt(avg));
            return Math.Max(0, (int)MathF.Round(res));

            // Метод для генерации случайного числа объектов с использованием Пуассоновского распределения
            int GetPoisson(float avg)
            {
                double L = Math.Exp(-avg);
                int k = 0;
                double p = 1.0;

                do
                {
                    k++;
                    p *= Float();
                }
                while (p > L);

                return k - 1;
            }
        }

        /// <summary>
        /// Random float value (Log distribution)
        /// </summary>
        public float Log(float avg)
        {
            return -avg * (float)Math.Log(Float());
        }

        /// <summary>
        /// Random float value (Log distribution)
        /// </summary>
        public float LogClamped(float avg, float min, float max)
        {
            var res = -avg * (float)Math.Log(Float());
            if (res > max) return max;
            if (res < min) return min;
            return res;
        }

        #endregion

        #region List operations

        /// <summary>
        /// Returns shuffled enumeration
        /// </summary>
        public IEnumerable<T> Shuffle<T>(IEnumerable<T> list)
        {
            return list.OrderBy(_ => Int());
        }

        /// <summary> Shuffle list </summary>
        public void ShuffleFisherYates<T>(IList<T> list)
        {
            var n = list.Count - 1;
            while (n > 0)
            {
                int k = Int(n + 1);
                T temp = list[n];
                list[n] = list[k];
                list[k] = temp;
                n--;
            }
        }

        /// <summary>Returns random element of enumeration</summary>
        public T GetRnd<T>(IEnumerable<T> list)
        {
            using (var po = ListPool<T>.Get(out var arr))
            {
                arr.AddRange(list);
                return GetRnd(arr);
            }
        }

        /// <summary>Returns random element of list</summary>
        public T GetRnd<T>(IList<T> list)
        {
            if (list == null || list.Count == 0)
                return default(T);

            var f = Int(list.Count);
            return list[f];
        }

        /// <summary>
        /// When factor = 1 - uniform distribution in range
        /// When factor = 2 - lowest values appear often
        /// When factor = 1/2 - highest values appear often
        /// </summary>
        public T GetRnd<T>(IList<T> list, float fallFactor)
        {
            if (list == null || list.Count == 0)
                return default(T);

            var index = (int)Log(0, list.Count - 0.001f, fallFactor);
            return list[index];
        }

        public List<int> Ints(int exclusiveUpperBound, int count)
        {
            if (count > exclusiveUpperBound) count = exclusiveUpperBound;

            var res = new HashSet<int>();

            while (res.Count < count)
                res.Add(Int(exclusiveUpperBound));

            return res.ToList();
        }

        public List<T> GetRnds<T>(IList<T> list, int count)
        {
            var res = new List<T>();

            if (list == null || list.Count == 0 || count < 1)
                return res;

            foreach (var index in Ints(list.Count, count))
                res.Add(list[index]);

            return res;
        }

        /// <summary>
        /// Возвращает индекс согласно вероятностям
        /// </summary>
        public int GetRndIndex(IList<float> probabilities, float sumOfProb = 1f)
        {
            if (probabilities == null || probabilities.Count == 0)
                return -1;

            if (sumOfProb <= float.Epsilon)
                //return Int(probabilities.Count);
                return -1;

            var v = Float(0, sumOfProb);

            var sum = 0f;
            for (int i = 0; i < probabilities.Count; i++)
            {
                sum += probabilities[i];
                if (sum >= v) return i;
            }

            return -1;
        }

        /// <summary>
        /// Возвращает значение согласно вероятностям
        /// </summary>
        public T GetRnd<T>(IList<T> list, IList<float> probabilities)
        {
            var sumOfProb = probabilities.Count == 0 ? 1 : probabilities.Sum();
            return GetRnd<T>(list, probabilities, sumOfProb);
        }

        /// <summary>
        /// Возвращает значение согласно вероятностям
        /// </summary>
        public T GetRnd<T>(IList<T> list, IList<float> probabilities, float sumOfProb)
        {
            var index = GetRndIndex(probabilities, sumOfProb);
            if (index < 0)
                return default;
            return list[index];
        }

        #endregion

        #region Get random for string idenifier

        /// <summary>Returns random int from 0 to 2^31-1 (inclusive both bounds) for identifier</summary>
        public int Int(string id)
        {
            return CombineHashCodes(seed, id);
        }

        /// <summary>Returns random int: from 0 to exclusiveTo (exclusive upper bound)</summary>
        public int Int(string id, int exclusiveTo)
        {
            if (exclusiveTo < 2)
                return 0;

            return Int(id) % exclusiveTo;
        }

        /// <summary>Return random float from 0 to 1</summary>
        public float Float(string id)
        {
            var d = (double)Int(id) / (double)0x7fffffff;
            return (float)d;
        }

        /// <summary>Return random float from 0 to 1</summary>
        public float Float(string id, float from, float to)
        {
            if (to < from)
                return from;

            var d = (double)Int(id) / (double)0x7fffffff;
            return from + (float)(d * (to - from));
        }

        /// <summary> Return True with defined probability </summary>
        public bool Bool(string id, float probability = 0.5f)
        {
            return Float(id) <= probability;
        }

        public T GetRnd<T>(string id, IList<T> list)
        {
            if (list == null || list.Count == 0)
                return default(T);

            return list[Int(id, list.Count)];
        }

        #endregion

        #region Uniform low dispersion distributions (static methods)

#if UNITY
        /// <summary>
        /// Uniform distribution with low dispersion (0..1)
        /// </summary>
        /// <param name="index">Index of sequence</param>
        /// <returns>Pseudo-random vector</returns>
        /// <remarks>
        /// http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
        /// https://observablehq.com/@jrus/plastic-sequence
        /// </remarks>
        public static UnityEngine.Vector3 PlasticSequence3(int index)
        {
            const double p1 = 0.8191725133961644; // inverse of plastic number
            const double p2 = 0.6710436067037892;
            const double p3 = 0.5497004779019702;

            return new UnityEngine.Vector3((float)((p1 * index) % 1), (float)((p2 * index) % 1), (float)((p3 * index) % 1));
        }

        /// <summary>
        /// Uniform distribution with low dispersion (0..1)
        /// </summary>
        /// <param name="index">Index of sequence</param>
        /// <returns>Pseudo-random vector</returns>
        /// <remarks>
        /// http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
        /// https://observablehq.com/@jrus/plastic-sequence
        /// </remarks>
        public static UnityEngine.Vector2 PlasticSequence2(int index)
        {
            const double p1 = 0.7548776662466927; // inverse of plastic number
            const double p2 = 0.5698402909980532;

            return new UnityEngine.Vector2((float)((p1 * index) % 1), (float)((p2 * index) % 1));
        }
#endif

        /// <summary>
        /// Uniform distribution with low dispersion (0..1)
        /// </summary>
        /// <param name="index">Index of sequence</param>
        /// <returns>Pseudo-random value</returns>
        /// <remarks>
        /// http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
        /// https://observablehq.com/@jrus/plastic-sequence
        /// </remarks>
        public static float PlasticSequence1(int index)
        {
            const double p = 0.618033988749894848; // inverse of golden ratio

            return (float)((p * index) % 1);
        }

#if UNITY
        /// <summary>
        /// Halton uniform distribution with low dispersion (0..1) 
        /// </summary>
        /// <param name="index">Index of sequence</param>
        /// <returns>Pseudo-random Vector3</returns>
        /// <remarks>
        /// https://en.wikipedia.org/wiki/Halton_sequence
        /// </remarks>
        public static UnityEngine.Vector3 Halton3(int index)
        {
            return new UnityEngine.Vector3(Halton(index, 2), Halton(index, 3), Halton(index, 5));
        }

        /// <summary>
        /// Halton uniform distribution with low dispersion (0..1) 
        /// </summary>
        /// <param name="index">Index of sequence</param>
        /// <returns>Pseudo-random Vector2</returns>
        /// <remarks>
        /// https://en.wikipedia.org/wiki/Halton_sequence
        /// </remarks>
        public static UnityEngine.Vector2 Halton2(int index)
        {
            return new UnityEngine.Vector2(Halton(index, 2), Halton(index, 3));
        }

        /// <summary>
        /// Halton uniform distribution with low dispersion (0..1) 
        /// </summary>
        /// <param name="index">Index of sequence</param>
        /// <param name="basePrime">Any prime number (2, 3, 5, 7, 11, etc)</param>
        /// <returns>Pseudo-random value</returns>
        /// <remarks>
        /// https://en.wikipedia.org/wiki/Halton_sequence
        /// </remarks>
        public static float Halton(int index, int basePrime = 2)
        {
            var fraction = 1.0;
            var result = 0.0;
            while (index > 0)
            {
                fraction /= basePrime;
                result += fraction * (index % basePrime);
                index = UnityEngine.Mathf.FloorToInt(index / basePrime); // floor division
            }
            return (float)result;
        }

        public static IEnumerable<float> HaltonSequence(int startIndex, int basePrime = 2)
        {
            for (int i = 0; ; i++)
                yield return Halton(startIndex + i, basePrime);
        }

        public static IEnumerable<UnityEngine.Vector2> HaltonSequence2(int startIndex)
        {
            for (int i = 0; ; i++)
                yield return Halton2(startIndex + i);
        }

        public static IEnumerable<UnityEngine.Vector3> HaltonSequence3(int startIndex)
        {
            for (int i = 0; ; i++)
                yield return Halton3(startIndex + i);
        }
#endif

        #endregion

        #region Spatial random hash (static methods)

        /// <summary>Spatial random from 0 to 1</summary>
        public static float Hash12(int x, int y)
        {
            var p3_x = Fract(x * 0.1031f);
            var p3_y = Fract(y * 0.1031f);
            var p3_z = p3_y;
            var dot = p3_x * (p3_y + 33.33f) + p3_y * (p3_z + 33.33f) + p3_z * (p3_x + 33.33f);
            p3_x += dot;
            p3_y += dot;
            p3_z += dot;
            return Fract((p3_x + p3_y) * p3_z);
        }

        /// <summary>Spatial random from 0 to 1</summary>
        public static float Hash13(int x, int y, int z)
        {
            var p3_x = Fract(x * 0.1031f);
            var p3_y = Fract(y * 0.1031f);
            var p3_z = Fract(z * 0.1031f);
            var dot = p3_x * (p3_z + 31.32f) + p3_y * (p3_y + 31.32f) + p3_z * (p3_x + 31.32f);
            p3_x += dot;
            p3_y += dot;
            p3_z += dot;
            return Fract((p3_x + p3_y) * p3_z);
        }

        /// <summary>Spatial random from 0 to int.MaxValue</summary>
        public static int Hash12Int(int x, int y)
        {
            var p3_x = Fract(x * 0.1031d);
            var p3_y = Fract(y * 0.1031d);
            var p3_z = p3_y;
            var dot = p3_x * (p3_y + 33.33d) + p3_y * (p3_z + 33.33d) + p3_z * (p3_x + 33.33d);
            p3_x += dot;
            p3_y += dot;
            p3_z += dot;
            var f = Fract((p3_x + p3_y) * p3_z);
            return (int)(f * int.MaxValue);
        }

        /// <summary>Spatial random from 0 to int.MaxValue</summary>
        public static int Hash13Int(int x, int y, int z)
        {
            var p3_x = Fract(x * 0.1031d);
            var p3_y = Fract(y * 0.1031d);
            var p3_z = Fract(z * 0.1031d);
            var dot = p3_x * (p3_z + 31.32d) + p3_y * (p3_y + 31.32d) + p3_z * (p3_x + 31.32d);
            p3_x += dot;
            p3_y += dot;
            p3_z += dot;
            var f = Fract((p3_x + p3_y) * p3_z);
            return (int)(f * int.MaxValue);
        }

        public static float Fract(float x) => x - (float)Math.Floor(x);
        public static double Fract(double x) => x - Math.Floor(x);

        #endregion

        #region Random branches

        public Rnd GetBranch(int seed)
        {
            return new Rnd(CombineHashCodes(this.seed, seed));
        }

        public Rnd GetBranch(string seed)
        {
            return new Rnd(CombineHashCodes(this.seed, seed));
        }

        public Rnd GetBranch(string strSeed, int intSeed)
        {
            return new Rnd(CombineHashCodes(this.seed, CombineHashCodes(intSeed, strSeed)));
        }

        public Rnd GetBranch(int seed1, int seed2)
        {
            return new Rnd(CombineHashCodes(this.seed, CombineHashCodes(seed1, seed2)));
        }

        public Rnd GetBranch(string seed1, string seed2)
        {
            return new Rnd(CombineHashCodes(this.seed, CombineHashCodes(seed1, seed2)));
        }

        public Rnd GetRndBranch()
        {
            return new Rnd(Int());
        }

        #endregion

        #region Private utils

        public static int CombineHashCodes(int seed, string name)
        {
            var hashCode = 1686112577;
            hashCode = hashCode * -1521134295 + seed;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name);
            hashCode &= 0x7fffffff;
            return hashCode;
        }

        public static int CombineHashCodes(string name1, string name2)
        {
            var hashCode = 1686112577;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name1);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name2);
            hashCode &= 0x7fffffff;
            return hashCode;
        }

        public static int CombineHashCodes(int seed1, int seed2)
        {
            var hashCode = -1110210311;
            hashCode = hashCode * -1521134295 + seed1.GetHashCode();
            hashCode = hashCode * -1521134295 + seed2.GetHashCode();
            hashCode &= 0x7fffffff;
            return hashCode;
        }
        #endregion

        #region Self test
#if UNITY
        public void TestSameValuesForDifferentSeeds()
        {
            var less = 0;
            var great = 0;
            var equal = 0;

            for (int i = 0; i < 100000000; i++)
            {
                var rnd1 = new Rnd(i);
                var rnd2 = new Rnd(i + 1);
                var v1 = rnd1.Int();
                var v2 = rnd2.Int();
                if (v1 == v2)
                    equal++;
                else
                if (v1 > v2)
                    less++;
                else
                    great++;
            }

            UnityEngine.Debug.Log("Equal: " + equal);
            UnityEngine.Debug.Log("Less: " + less);
            UnityEngine.Debug.Log("Great: " + great);
        }

        public void TestHistogram()
        {
            var count = new int[10];
            var rnd = new Rnd();

            for (int i = 0; i < 100000000; i++)
            {
                count[rnd.Int(10)]++;
            }

            for (int i = 0; i < 10; i++)
                UnityEngine.Debug.Log(i + ": " + count[i]);
        }

        public void TestValueRepeating()
        {
            var less = 0;
            var great = 0;
            var equal = 0;

            var rnd = new Rnd();
            var v1 = rnd.Int();

            for (int i = 0; i < 100000000; i++)
            {
                var v2 = rnd.Int();
                if (v1 == v2)
                    equal++;
                else
                if (v1 > v2)
                    less++;
                else
                    great++;
            }

            UnityEngine.Debug.Log("Equal: " + equal);
            UnityEngine.Debug.Log("Less: " + less);
            UnityEngine.Debug.Log("Great: " + great);
        }
#endif
        #endregion
    }
}