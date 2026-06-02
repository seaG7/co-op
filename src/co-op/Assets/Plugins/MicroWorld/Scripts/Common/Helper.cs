using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroWorldNS
{
    public static partial class Helper
    {
        #region Angle helpers

        public static float ClampAngle0360(float angle)
        {
            float result = angle - Mathf.CeilToInt(angle / 360f) * 360f;
            if (result < 0)
            {
                result += 360f;
            }
            return result;
        }

        public static float ClampAngle(float angle, float from, float to)
        {
            var mid = (from + to) / 2;
            if (Mathf.Abs(mid - angle) > 180)
            {
                if (mid > angle)
                    angle += 360;
                else
                    angle -= 360;
            }

            angle = Mathf.Clamp(angle, from, to);
            if (angle > 360) angle -= 360;
            if (angle < 0) angle += 360;

            return angle;
        }

        /// <summary>
        /// Угол по горизонтали и по вертикали между двумя векторами
        /// </summary>
        public static Vector2 SignedAngles(Vector3 forward, Vector3 vector)
        {
            var v1 = new Vector3(forward.x, 0, forward.z);
            var v2 = new Vector3(vector.x, 0, vector.z);
            var horizAngle = Vector3.SignedAngle(v1, v2, Vector3.up);
            var vertAngle = Mathf.Atan2(vector.y, v2.magnitude) * Mathf.Rad2Deg;

            return new Vector2(horizAngle, vertAngle);
        }

        public static float ToSignedAngle(float angleFrom0to360)
        {
            if (angleFrom0to360 > 180)
                angleFrom0to360 = angleFrom0to360 - 360;
            return angleFrom0to360;
        }
        #endregion

        #region Destroy all

        public static void DestroySafe(this GameObject obj)
        {
            if (!obj)
                return;
            if (Application.isPlaying)
                GameObject.Destroy(obj);
            else
                GameObject.DestroyImmediate(obj);
        }

        public static void DestroySafe(this UnityEngine.Object obj)
        {
            if (!obj)
                return;
            if (Application.isPlaying)
                GameObject.Destroy(obj);
            else
                GameObject.DestroyImmediate(obj);
        }

        public static void DestroyComponentSafe(this Component obj)
        {
            if (Application.isPlaying)
                GameObject.Destroy(obj);
            else
                GameObject.DestroyImmediate(obj);
        }

        public static void DestroyAllChildren(this GameObject obj)
        {
            DestroyAllChildren(obj?.transform);
        }

        public static void DestroyAllChildrenImmediate(this GameObject obj)
        {
            var c = obj.transform.childCount;
            for (int i = c - 1; i >= 0; i--)
            {
                GameObject.DestroyImmediate(obj.transform.GetChild(i).gameObject);
            }
        }

        public static void DestroyAllChildren(this Transform tr)
        {
            if (!Application.isPlaying)
                while (tr.childCount != 0)
                {
                    GameObject.DestroyImmediate(tr.GetChild(0).gameObject);
                }

            if (Application.isPlaying)
                for (int i = tr.childCount - 1; i >= 0; i--)
                {
                    GameObject.Destroy(tr.GetChild(i).gameObject);
                }
        }

        public static void DestroyAllChildrenImmediate(this Transform tr)
        {
            while (tr.childCount != 0)
            {
                GameObject.DestroyImmediate(tr.GetChild(0).gameObject);
            }
        }

        #endregion

        #region Find object
        /// <summary>
        /// Finds active and inactive objects by name
        /// </summary>
        public static GameObject FindObject(this GameObject parent, string name)
        {
            var trs = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in trs)
            {
                if (t.name == name)
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds active and inactive objects by name
        /// </summary>
        public static T FindObject<T>(this GameObject parent, string name)
        {
            var trs = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in trs)
            {
                if (t.name == name)
                {
                    return t.gameObject.GetComponent<T>();
                }
            }
            return default(T);
        }

        static List<GameObject> rootObjectsInScene = new List<GameObject>();

        public static IReadOnlyCollection<GameObject> GetRootObjects()
        {
            var scene = SceneManager.GetActiveScene();
            scene.GetRootGameObjects(rootObjectsInScene);
            return rootObjectsInScene;
        }

        public static IEnumerable<T> GetAllComponentsOnScene<T>(bool includeInactive = false)
        {
            foreach (var root in GetRootObjects())
                foreach (var c in root.GetComponentsInChildren<T>(includeInactive))
                    yield return c;
        }

        #endregion

        #region Get Bounds

        public static Bounds GetTotalBounds(IEnumerable<GameObject> objects, Func<Renderer, bool> allow)
        {
            var first = objects.FirstOrDefault();
            if (first == null)
                return new Bounds();
            var res = GetTotalBounds(first, allow);
            foreach (var obj in objects)
                res.Encapsulate(GetTotalBounds(obj, allow));

            return res;
        }

        public static Bounds GetTotalBounds(GameObject obj, Func<Renderer, bool> allow = null)
        {
            var rends = obj.GetComponentsInChildren<Renderer>().Where(b => allow == null || allow(b));
            if (!rends.Any())
                return new Bounds(obj.transform.position, new Vector3(1, 1, 1));
            else
            {
                var b = rends.First().bounds;

                foreach (var rend in rends.Skip(1))
                {
                    b.Encapsulate(rend.bounds);
                }
                return b;
            }
        }

        public static Bounds GetTotalBoundsAccurate(GameObject obj, Func<MeshFilter, bool> allow = null)
        {
            var meshes = obj.GetComponentsInChildren<MeshFilter>().Where(b => allow == null || allow(b)).ToArray();

            if (meshes.Length == 0)
                return new Bounds(obj.transform.position, new Vector3(1, 1, 1));

            Bounds bounds = default;
            for (int i = 0; i < meshes.Length; i++)
            {
                var tr = meshes[i].transform;
                var vertices = meshes[i].sharedMesh.vertices;

                if (i == 0)
                    bounds = new Bounds(tr.TransformPoint(vertices[0]), Vector3.zero);

                for (int j = 0; j < vertices.Length; j++)
                    bounds.Encapsulate(tr.TransformPoint(vertices[j]));
            }

            return bounds;
        }

        #endregion

        #region Coroutine helpers

        public static Coroutine ExecuteInNextFrame(this MonoBehaviour mn, Action action, int frames = 1)
        {
            return mn.StartCoroutine(executeInNextFrame(frames, action));
        }

        private static IEnumerator executeInNextFrame(int frames, Action onFinished)
        {
            for (int i = 0; i < frames; i++)
                yield return null;

            onFinished?.Invoke();
        }

        public static Coroutine StartCoroutine(this MonoBehaviour mn, IEnumerator func, Action onFinished)
        {
            return mn.StartCoroutine(executeCoroutine(func, onFinished));
        }

        public static Coroutine StartCoroutine(this MonoBehaviour mn, Action onStep)
        {
            return mn.StartCoroutine(executeCoroutine(onStep, null));
        }

        public static Coroutine StartCoroutine(this MonoBehaviour mn, Func<bool> onStep)
        {
            return mn.StartCoroutine(executeCoroutine(onStep, null));
        }

        public static Coroutine StartCoroutineInFixedUpdate(this MonoBehaviour mn, Action onStep)
        {
            return mn.StartCoroutine(executeCoroutine(onStep, new WaitForFixedUpdate()));
        }

        private static IEnumerator executeCoroutine(Action func, YieldInstruction waiter)
        {
            while (true)
            {
                try
                {
                    func?.Invoke();
                }
                catch
                {
                    break;
                }
                yield return waiter;
            }
        }

        private static IEnumerator executeCoroutine(Func<bool> func, YieldInstruction waiter)
        {
            if (func == null)
                yield break;

            while (func.Invoke())
                yield return waiter;
        }

        private static IEnumerator executeCoroutine(IEnumerator func, Action onFinished)
        {
            yield return func;
            onFinished();
        }

        public static void StartAnimate(float duration, Action<float> onStep)
        {
            Dispatcher.StartCoroutine(Animate(duration, onStep));
        }

        public static void StartAnimateInFixedUpdate(float duration, Action<float> onStep)
        {
            Dispatcher.StartCoroutine(AnimateInFixedUpdate(duration, onStep));
        }

        public static IEnumerator Animate(float duration, Action<float> onStep)
        {
            var steps = Mathf.RoundToInt(duration / Time.deltaTime);
            if (steps < 2) steps = 2;

            for (int i = 0; i < steps; i++)
            {
                var t01 = i / (steps - 1f);
                try
                {
                    onStep?.Invoke(t01);
                }
                catch
                {
                    yield break;
                }

                if (i < steps - 1)
                    yield return null;
            }
        }

        public static IEnumerator AnimateInFixedUpdate(float duration, Action<float> onStep)
        {
            var waiter = new WaitForFixedUpdate();
            var steps = Mathf.RoundToInt(duration / Time.deltaTime);
            if (steps < 2) steps = 2;

            for (int i = 0; i < steps; i++)
            {
                var t01 = i / (steps - 1f);
                try
                {
                    onStep?.Invoke(t01);
                }
                catch
                {
                    yield break;
                }

                if (i < steps - 1)
                    yield return waiter;
            }
        }

        /// <summary> Allows run coroutine with Exception handling </summary>
        public static IEnumerator CoroutineWithExceptions(this IEnumerator enumerator, Action<Exception> done)
        {
            // The enumerator might yield return enumerators, in which case 
            // we need to enumerate those here rather than yield-returning 
            // them. Otherwise, any exceptions thrown by those "inner enumerators"
            // would actually escape to an outer level of iteration, outside this 
            // code here, and not be passed to the done callback.
            // So, this stack holds any inner enumerators.
            var stack = new Stack<IEnumerator>();
            stack.Push(enumerator);

            while (stack.Count > 0)
            {
                // any inner enumerator will be at the top of the stack
                // otherwise the original one
                var currentEnumerator = stack.Peek();
                // this is what get "yield returned" in the work enumerator
                object currentYieldedObject;
                // the contents of this try block run the work enumerator until
                // it gets to a yield return statement
                try
                {
                    if (currentEnumerator.MoveNext() == false)
                    {
                        // in this case, the enumerator has finished
                        stack.Pop();
                        // if the stack is empty, then everything has finished,
                        // and the while (stack.Count &gt; 0) will pick it up
                        continue;
                    }
                    currentYieldedObject = currentEnumerator.Current;
                }
                catch (Exception ex)
                {
                    // this part is the whole point of this method!
                    done(ex);
                    yield break;
                }
                // in unity you can yield return whatever the hell you want,
                // so this will pick up whether it's something to enumerate 
                // here, or pass through by yield returning it
                var currentYieldedEnumerator = currentYieldedObject as IEnumerator;
                if (currentYieldedEnumerator != null)
                {
                    stack.Push(currentYieldedEnumerator);
                }
                else
                {
                    yield return currentYieldedObject;
                }
            }
            done(null);
        }

        /// <example>
        /// var e = builder.BuildInternal();
        /// while (e.Enumerate()) { ... }
        /// </example>
        public static bool Enumerate(this IEnumerator e)
        {
            var nested = e.Current as IEnumerator;
            if (nested != null && Enumerate(nested))
                return true;

            return e.MoveNext();
        }

        static int[] emptyList = new int[0];

        public static IEnumerator EmptyEnumerator => emptyList.GetEnumerator();

        #endregion

        #region Texture and sprite helpers

        public static Sprite ToSprite(this Texture2D texture, float pixelsPerUnit = 100f)
        {
            var res = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            return res;
        }

        public static Texture2D LoadTextureFromPersistent(string fileName)
        {
            Texture2D texture;
            var path = Path.Combine(Application.persistentDataPath, fileName);
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, true);
            var data = File.ReadAllBytes(path);
            texture.LoadImage(data, true);
            return texture;
        }

        public static void SaveTextureToPersistent(this Texture2D texture, string fileName)
        {
            var path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }

        #endregion

        #region Vector3 and Transform helpers

        public static void LookAtUp(this Transform tr, Vector3 up)
        {
            tr.LookAt(tr.position + up);
            tr.RotateAround(tr.position, tr.right, 90);
        }

        public static Vector3 Avg(this IEnumerable<Vector3> points)
        {
            var count = 0;
            var sum = default(Vector3);
            foreach (var p in points)
            {
                sum += p;
                count++;
            }

            if (count == 0)
                return default(Vector3);

            return sum / count;
        }

        // Compute barycentric coordinates (u, v, w) for
        // point p with respect to triangle (a, b, c)
        public static Vector3 Barycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            //float &u, float &v, float &w
            Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            var v = (d11 * d20 - d01 * d21) / denom;
            var w = (d00 * d21 - d01 * d20) / denom;
            var u = 1.0f - v - w;

            return new Vector3(u, v, w);
        }

        // Compute barycentric coordinates for
        // point p with respect to rectangle (a, b, c, d) in XZ plane
        public static Vector4 BarycentricRect(Vector3 P, Vector3 A, Vector3 B, Vector3 C, Vector3 D)
        {
            float u = (P.x - A.x) / (B.x - A.x);
            float v = (P.z - A.z) / (D.z - A.z);

            float lambda1 = (1 - u) * (1 - v);
            float lambda2 = u * (1 - v);
            float lambda3 = u * v;
            float lambda4 = (1 - u) * v;

            return new Vector4(lambda1, lambda2, lambda3, lambda4);
        }

        public static float Perlin(float x, float y, float frequency, int octaves = 1)
        {
            var res = 0f;
            var ampl = 1f;
            for (int i = 0; i < octaves; i++)
            {
                res += Mathf.PerlinNoise(x * frequency + octaves * 5, y * frequency + octaves * 3) * ampl;
                frequency *= 2f;
                ampl /= 2f;
            }

            return res;
        }

        public static float InvertedSquare(float t01)
        {
            var t = 1 - t01;
            return 1 - t * t;
        }

        /// <summary>
        /// Nearest point on segment
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 NearestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 x;
            if (Vector3.Dot(a - b, p - a) > 0) x = a;
            else if (Vector3.Dot(b - a, p - b) > 0) x = b;
            else x = a + Vector3.Project(p - a, b - a);
            return x;
        }

        /// <summary>
        /// Sqaure distance from point to segment
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceSqToSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            return (NearestPointOnSegment(a, b, p) - p).sqrMagnitude;
        }

        /// <summary>
        /// Determines if the given point is inside the polygon
        /// </summary>
        /// <param name="polygon">the vertices of polygon</param>
        /// <param name="testPoint">the given point</param>
        /// <returns>true if the point is inside the polygon; otherwise, false</returns>
        public static bool IsPointInsidePolygon(IList<Vector2> polygon, Vector2 testPoint)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                if (polygon[i].y < testPoint.y && polygon[j].y >= testPoint.y ||
                    polygon[j].y < testPoint.y && polygon[i].y >= testPoint.y)
                {
                    if (polygon[i].x + (testPoint.y - polygon[i].y) /
                       (polygon[j].y - polygon[i].y) *
                       (polygon[j].x - polygon[i].x) < testPoint.x)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        /// Gets the coordinates of the intersection point of two lines.
        /// A point on the first line.
        /// Another point on the first line.
        /// A point on the second line.
        /// Another point on the second line.
        /// Is set to false of there are no solution. true otherwise.
        /// The intersection point coordinates. Returns Vector2.zero if there is no solution.
        public static bool LinesIntersection(Vector2 A1, Vector2 A2, Vector2 B1, Vector2 B2, out Vector2 res)
        {
            float tmp = (B2.x - B1.x) * (A2.y - A1.y) - (B2.y - B1.y) * (A2.x - A1.x);

            if (tmp == 0)
            {
                // No solution!
                res = Vector2.zero;
                return false;
            }

            float mu = ((A1.x - B1.x) * (A2.y - A1.y) - (A1.y - B1.y) * (A2.x - A1.x)) / tmp;

            res = new Vector2(
                B1.x + (B2.x - B1.x) * mu,
                B1.y + (B2.y - B1.y) * mu
            );

            return true;
        }

        public static Vector3 ToVector3(this Vector2 vector2)
        {
            return new Vector3(vector2.x, 0, vector2.y);
        }

        public static Vector2 ToVector2(this Vector3 vector3)
        {
            return new Vector2(vector3.x, vector3.z);
        }

        public static Vector3 GetPerpendicular(this Vector3 p)
        {
            if (Mathf.Abs(p.z) <= float.Epsilon)
                return Vector3.forward;
            else
                return new Vector3(1, 1, -(p.x + p.y) / p.z);
        }

        public static Vector2Int Rotate90CW(this Vector2Int v)
        {
            return new Vector2Int(v.y, -v.x);
        }

        public static Vector2Int Rotate90CCW(this Vector2Int v)
        {
            return new Vector2Int(-v.y, v.x);
        }

        public static Vector2Int Normalized(this Vector2Int v)
        {
            v.x = Math.Sign(v.x);
            v.y = Math.Sign(v.y);
            return v;
        }

        public static Vector2Int Lerp(Vector2Int from, Vector2Int to, float k)
        {
            return Vector2Int.RoundToInt((Vector2)from * (1 - k) + (Vector2)to * k);
        }

        public static (float, float, float) ToKey(this Vector3 v)
        {
            return (v.x, v.y, v.z);
        }

        public static Vector3 XZ(this Vector3 v)
        {
            return new Vector3(v.x, 0, v.z);
        }

        public static Vector3 XY(this Vector3 v)
        {
            return new Vector3(v.x, v.y, 0);
        }

        public static bool Approximately(this Vector3 me, Vector3 other, float allowedDifference)
        {
            if (Mathf.Abs(me.x - other.x) > allowedDifference) return false;
            if (Mathf.Abs(me.z - other.z) > allowedDifference) return false;
            return Mathf.Abs(me.y - other.y) <= allowedDifference;
        }

        public static bool ApproximatelyByXZ(this Vector3 me, Vector3 other, float allowedDifference)
        {
            if (Mathf.Abs(me.x - other.x) > allowedDifference) return false;
            return Mathf.Abs(me.z - other.z) <= allowedDifference;
        }

        public static bool IsZeroApproxByXZ(this Vector3 me, float allowedDifference)
        {
            if (Mathf.Abs(me.x) > allowedDifference) return false;
            return Mathf.Abs(me.z) <= allowedDifference;
        }

        public static bool IsZeroApprox(this Vector3 me, float allowedDifference)
        {
            if (Mathf.Abs(me.x) > allowedDifference) return false;
            if (Mathf.Abs(me.z) > allowedDifference) return false;
            return Mathf.Abs(me.y) <= allowedDifference;
        }

        public static bool IsZeroApprox(this float me, float allowedDifference = 0.00001f)
        {
            if (Mathf.Abs(me) > allowedDifference) return false;
            return true;
        }

        public static bool IsOneApprox(this float me, float allowedDifference = 0.00001f)
        {
            if (Mathf.Abs(me - 1f) > allowedDifference) return false;
            return true;
        }

        public static Quaternion SmoothDamp(this Quaternion rot, Quaternion target, ref Quaternion deriv, float time)
        {
            if (Time.deltaTime < Mathf.Epsilon) return rot;
            // account for double-cover
            var Dot = Quaternion.Dot(rot, target);
            var Multi = Dot > 0f ? 1f : -1f;
            target.x *= Multi;
            target.y *= Multi;
            target.z *= Multi;
            target.w *= Multi;
            // smooth damp (nlerp approx)
            var Result = new Vector4(
                Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time),
                Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time),
                Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time),
                Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time)
            ).normalized;

            // ensure deriv is tangent
            var derivError = Vector4.Project(new Vector4(deriv.x, deriv.y, deriv.z, deriv.w), Result);
            deriv.x -= derivError.x;
            deriv.y -= derivError.y;
            deriv.z -= derivError.z;
            deriv.w -= derivError.w;

            return new Quaternion(Result.x, Result.y, Result.z, Result.w);
        }

        #endregion

        #region Other

        public static bool NotNullOrEmpty(this string s) => !string.IsNullOrEmpty(s);
        public static bool IsNullOrEmpty(this string s) => string.IsNullOrEmpty(s);

#if UNITY_EDITOR
        public static void MakeTextureReadable(Texture tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        public static void MakeTextureSRGB(Texture tex, bool sRGB)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.sRGBTexture != sRGB)
            {
                importer.sRGBTexture = sRGB;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        public static T GetNextAsset<T>(T obj, int dir = 1) where T : UnityEngine.Object
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(obj);
            var folder = Path.GetDirectoryName(path);
            var files = Directory.GetFiles(folder, "*" + Path.GetExtension(path));
            var index = Array.IndexOf(files.Select(p => Path.GetFileName(p)).ToArray(), Path.GetFileName(path));
            if (index == -1)
                return obj;
            index = (index + dir + files.Length) % files.Length;
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(files[index]);
        }

        public static GameObject SaveAsPrefab(this GameObject gameObject, string folder = "Prefabs")
        {
            if (!Directory.Exists(Path.Combine("Assets", folder)))
                UnityEditor.AssetDatabase.CreateFolder("Assets", folder);
            string localPath = Path.Combine("Assets", folder, gameObject.name + ".prefab");

            // Make sure the file name is unique, in case an existing Prefab has the same name.
            localPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(localPath);

            // Create the new Prefab and log whether Prefab was saved successfully.
            return UnityEditor.PrefabUtility.SaveAsPrefabAsset(gameObject, localPath, out var prefabSuccess);
        }

        public static void SaveAsAsset(this UnityEngine.Object obj, string folder = "")
        {
            if (!Directory.Exists(Path.Combine("Assets", folder)))
                UnityEditor.AssetDatabase.CreateFolder("Assets", folder);
            string localPath = Path.Combine("Assets", folder, obj.name + ".asset");

            // Make sure the file name is unique, in case an existing Prefab has the same name.
            localPath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(localPath);

            // Create the scriptable object asset
            UnityEditor.AssetDatabase.CreateAsset(obj, localPath);
            UnityEditor.AssetDatabase.SaveAssets();
        }

        public static void SaveMeshAsAsset(Transform holder, Mesh mesh, bool callSaveAssets = true)
        {
            var dir = "Assets/Autogenerated";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var name = GetUniqueName(holder);
            var path = Path.Combine(dir, name + ".asset");
            //if (File.Exists(path))
            //File.Delete(path);

            mesh.name = name;

            if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(mesh)))
            {
                UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                if (callSaveAssets)
                    UnityEditor.AssetDatabase.SaveAssets();
                else
                    UnityEditor.EditorUtility.SetDirty(mesh);
            }
            else
            {
                UnityEditor.EditorUtility.SetDirty(mesh);
            }

            Debug.Log("Save BaseSplineComponent mesh: " + name);
        }

#endif

        public static int[] LodsH = new int[4] { 50, 25, 10, 3 };

        public static GameObject BuildLODGroup(Material material, Func<int, Mesh> getMesh, int lodOfCollider = 1, int[] LodsH = null)
        {
            if (LodsH == null)
                LodsH = Helper.LodsH;
            var lodsCount = LodsH.Length;

            var holder = new GameObject("Prefab", typeof(LODGroup));
            var lg = holder.GetOrAddComponent<LODGroup>();
            var lods = new LOD[lodsCount];
            holder.isStatic = true;

            for (int i = 0; i < lodsCount; i++)
            {
                var go = new GameObject("LOD" + i, typeof(MeshRenderer), typeof(MeshFilter));
                go.isStatic = true;
                go.transform.SetParent(holder.transform);
                go.transform.localPosition = Vector3.zero;
                var iterations = lodsCount - i - 1;
                var mesh = getMesh(iterations);
                if (i == lodOfCollider)
                    holder.GetOrAddComponent<MeshCollider>().sharedMesh = mesh;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                lods[i].renderers = new Renderer[1] { mr };

                lods[i].screenRelativeTransitionHeight = LodsH[i] / 100f;
            }

            lg.SetLODs(lods.ToArray());

            return holder;
        }

        public static string GetUniqueName(this Transform tr)
        {
            var name = string.Join(",", tr.GetAllParents(true).Select(t => t.name).Union(new string[] { tr.gameObject.scene.name }));
            return name;
        }

        /// <summary> Angle of AnimtioanCurve (radians) </summary>
        public static float EvaluateAngle(this AnimationCurve curve, float t)
        {
            const float dx = 0.01f;
            var y0 = curve.Evaluate(t - dx);
            var y1 = curve.Evaluate(t + dx);
            return Mathf.Atan2(y1 - y0, 2 * dx);
        }

        public static void HeightsToTerrain(Terrain terrain, Func<float, float, float> xzToHeight)
        {
            float[,] heights = new float[terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution];
            var data = terrain.terrainData;
            var terrainPos = terrain.GetPosition();
            var kx = data.size.x / data.heightmapResolution;
            var kz = data.size.z / data.heightmapResolution;
            var hmRes = data.heightmapResolution;
            var terrainSizeY = data.size.y;

            Parallel.For(0, hmRes, i =>
            //for (int i = 0; i < hmRes; i++)
            {
                for (int k = 0; k < hmRes; k++)
                {
                    var x = i * kx + terrainPos.x;
                    var y = k * kz + terrainPos.z;
                    var h = (xzToHeight(x, y) - terrainPos.y) / terrainSizeY;
                    heights[k, i] = Mathf.Clamp01(h);
                }
            });

            terrain.terrainData.SetHeights(0, 0, heights);
        }

        /// <summary> Returns direct childrens (does not include child of children, does not include itself) </summary>
        public static IEnumerable<T> GetComponentsInDirectChildren<T>(this Component parent, bool includeInactive = false) where T : Component
        {
            var children = parent.GetComponentsInChildren<T>(includeInactive);
            foreach (var child in children)
                if (child.gameObject != parent.gameObject)//skip itself
                {
                    var childParent = child.transform.parent.GetComponentInParent<T>(includeInactive);
                    if (childParent == parent || childParent == null)//direct child?
                        yield return child;
                }
        }

        /// <summary>
        /// Gets or add a component. Usage example:
        /// BoxCollider boxCollider = transform.GetOrAddComponent<BoxCollider>();
        /// </summary>
        static public T GetOrAddComponent<T>(this Component child) where T : Component
        {
            T result = child.GetComponent<T>();
            if (result == null)
            {
                result = child.gameObject.AddComponent<T>();
            }
            return result;
        }

        public static void SetActive(IEnumerable<UnityEngine.Object> objects, bool active)
        {
            foreach (var o in objects)
                if (o)
                    switch (o)
                    {
                        case GameObject obj: if (obj.activeSelf != active) obj.SetActive(active); break;
                        case Behaviour obj: obj.enabled = active; break;
                        case Collider obj: obj.enabled = active; break;
                        case Renderer obj: obj.enabled = active; break;
                        case LODGroup obj: obj.enabled = active; break;
                        case Rigidbody obj: obj.isKinematic = !active; obj.linearVelocity = Vector3.zero; obj.angularVelocity = Vector3.zero; break;
                    }
        }

        public static int Mod(this int x, int m)
        {
            return (x % m + m) % m;
        }

        public static void Swap<T>(ref T v1, ref T v2)
        {
            var temp = v1;
            v1 = v2;
            v2 = temp;
        }

        static List<RaycastHit> hitsPool = new List<RaycastHit>();

        /// <summary>
        /// Returns hits ordered by distance. Shooter's colliders are ignored.
        /// </summary>
        public static List<RaycastHit> GetOrderedHits(Ray ray, float maxDist = 100, int layerMask = Physics.DefaultRaycastLayers, GameObject ignoredObject = null, QueryTriggerInteraction interaction = QueryTriggerInteraction.Ignore)
        {
            hitsPool.Clear();

            var arr = Physics.RaycastAll(ray, maxDist, layerMask, interaction);
            if (arr.Length == 0)
                return hitsPool;

            for (int i = 0; i < arr.Length; i++)
            {
                //if it is not shooter collider...
                if (ignoredObject == null || !arr[i].collider.gameObject.transform.IsChildOf(ignoredObject.transform))
                    hitsPool.Add(arr[i]);
            }

            //sort
            hitsPool.Sort((h1, h2) => h1.distance.CompareTo(h2.distance));

            return hitsPool;
        }

        static RaycastHit[] hits = new RaycastHit[100];

        public static IEnumerable<RaycastHit> Raycast(Vector3 pos, Vector3 dir, LayerMask mask = default)
        {
            var count = Physics.RaycastNonAlloc(pos, dir, hits, float.MaxValue, mask == default ? -1 : mask);
            for (int i = 0; i < count; i++)
                yield return hits[i];
        }

        public static IEnumerable<RaycastHit> RaycastSphere(Vector3 pos, Vector3 dir, float radius, LayerMask mask = default)
        {
            var count = Physics.SphereCastNonAlloc(pos, radius, dir, hits, float.MaxValue, mask == default ? -1 : mask);
            for (int i = 0; i < count; i++)
                yield return hits[i];
        }

        public static IEnumerable<T> Raycast<T>(Vector3 pos, Vector3 dir, LayerMask mask = default) where T : Component
        {
            var count = Physics.RaycastNonAlloc(pos, dir, hits, float.MaxValue, mask == default ? -1 : mask);
            for (int i = 0; i < count; i++)
            {
                var c = hits[i].collider.transform.GetComponentInParent<T>();
                if (c)
                    yield return c;
            }
        }

        public static IEnumerable<T> RaycastSphere<T>(Vector3 pos, Vector3 dir, float radius, LayerMask mask = default) where T : Component
        {
            var count = Physics.SphereCastNonAlloc(pos, radius, dir, hits, float.MaxValue, mask == default ? -1 : mask);
            for (int i = 0; i < count; i++)
            {
                var c = hits[i].collider.transform.GetComponentInParent<T>();
                if (c)
                    yield return c;
            }
        }

        public static void SetGlobalScale(Transform transform, Vector3 scale)
        {
            transform.localScale = Vector3.one;
            transform.localScale = new Vector3(scale.x / transform.lossyScale.x, scale.y / transform.lossyScale.y, scale.z / transform.lossyScale.z);
        }

        public static bool IsMouseOverGUI
        {
            get { return GUIUtility.hotControl != 0 || (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()); }
        }

        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        {
            var c = obj.GetComponent<T>();
            if (c == null)
                c = obj.AddComponent<T>();

            return c;
        }

        public static IEnumerable<Transform> GetAllParents(this Transform tr, bool includeMe)
        {
            var parent = tr;
            var first = true;

            while (parent != null)
            {
                if (!first || includeMe)
                    yield return parent;

                first = false;
                if (parent == parent.parent)
                    break;
                parent = parent.parent;
            }
        }

        public static float RemapClamped(this float val, float sourceFrom, float sourceTo, float targetFrom, float targetTo)
        {
            if (sourceFrom == sourceTo)
                return targetFrom;

            var t = Mathf.Clamp01((val - sourceFrom) / (sourceTo - sourceFrom));

            return targetFrom + (targetTo - targetFrom) * t;
        }

        public static float Remap(this float val, float sourceFrom, float sourceTo, float targetFrom, float targetTo)
        {
            if (sourceFrom == sourceTo)
                return targetFrom;

            var t = (val - sourceFrom) / (sourceTo - sourceFrom);

            return targetFrom + (targetTo - targetFrom) * t;
        }

        public static float Frac(this float x) => x - Mathf.Floor(x);
        public static double Frac(this double x) => x - Math.Floor(x);

        public static float Frac(this float x, float period) => Frac(x / period) * period;

        public static float Clamp01(this float x) => Mathf.Clamp01(x);
        public static float Clamp(this float x, float min, float max) => Mathf.Clamp(x, min, max);
        public static bool InRange(this float x, float min, float max) => x >= min && x <= max;
        public static bool InRange(this float x, Vector2 limits) => x >= limits.x && x <= limits.y;
        public static bool InRangeAdv(this float x, Vector2 limits) => limits.x > limits.y ? x < limits.y || x > limits.x : x >= limits.x && x <= limits.y;
        public static bool InRangeAdv(this float x, Vector2 limits, float gap) => limits.x > limits.y ? x - gap < limits.y || x + gap > limits.x : x + gap >= limits.x && x - gap <= limits.y;

        public static float Logist(this float x, float factor = 1, float amplitude = 1) => (1 - Mathf.Exp(-Mathf.Abs(x) * factor * amplitude)) / amplitude * Mathf.Sign(x);

        static Dictionary<Type, UnityEngine.Object[]> typeToResources = new Dictionary<Type, UnityEngine.Object[]>();

        public static T[] GetFromResources<T>() where T : UnityEngine.Object
        {
            if (!typeToResources.TryGetValue(typeof(T), out var res))
                typeToResources[typeof(T)] = res = Resources.LoadAll<T>("");

            return (T[])res;
        }

        [RuntimeInitializeOnLoadMethod]
        static void ClearTypeToResources() => typeToResources.Clear();

        public static T GetFromResource<T>() where T : UnityEngine.Object
        {
            if (!typeToResources.TryGetValue(typeof(T), out var res))
                typeToResources[typeof(T)] = res = Resources.LoadAll<T>("");

            return res.Length > 0 ? (T)res[0] : default;
        }

        public static bool Contains(this LayerMask mask, int layer)
        {
            return (mask & (1 << layer)) != 0;
        }

        #endregion
    }

    //[AttributeUsage(AttributeTargets.Class)]
    //public class EnumGeneratorAttribute : Attribute
    //{
    //    string EnumName;

    //    public EnumGeneratorAttribute(string enumName)
    //    {
    //        EnumName = enumName;
    //    }

    //    public void GenerateClass
    //}

    public static class DuckExtensionMethods
    {
        public static Vector3 limit(this Vector3 THIS, float max)
        {
            float l = THIS.length();
            return l > max ? THIS.normalized(max) : THIS;
        }

        public static Vector2 limit(this Vector2 THIS, float max)
        {
            float l = THIS.magnitude;
            return l > max ? THIS.normalized(max) : THIS;
        }

        public static Vector3 minRadius(this Vector3 THIS, float min)
        {
            float l = THIS.length();
            return l < min && l > 0.01f ? THIS.normalized(min) : THIS;
        }

        public static Vector3 clamp(this Vector3 THIS, float min, float max)
        {
            float l = THIS.length();
            if (l > max) return THIS.normalized(max);
            if (l < min && l > 0.001f) return THIS.normalized(min);
            return THIS;
        }

        public static Vector3 clampAround(this Vector3 THIS, Vector3 center, float min, float max)
        {
            return THIS.sub(center).clamp(min, max).add(center);
        }

        public static Vector3 limit(this Vector3 THIS, Vector3 center, float max)
        {
            return (THIS - center).limit(max) + center;
        }

        public static Vector3 getTangentComponent(this Vector3 THIS, Vector3 normalizedAxis)
        {
            return Vector3.Project(THIS, normalizedAxis);
        }

        public static Vector3 getNormalComponent(this Vector3 THIS, Vector3 normalizedAxis)
        {
            return THIS - THIS.getTangentComponent(normalizedAxis);
        }

        public static (Vector3 tangent, Vector3 normal) getComponents(this Vector3 THIS, Vector3 normalizedAxis)
        {
            var tangent = THIS.getTangentComponent(normalizedAxis);
            return (tangent, THIS - tangent);
        }

        public static void getComponents(this Vector3 THIS, Vector3 normalizedAxis, out Vector3 tangent, out Vector3 normal)
        {
            tangent = THIS.getTangentComponent(normalizedAxis);
            normal = THIS - tangent;
        }

        public static Vector3 replaceTangent(this Vector3 THIS, Vector3 normalizedAxis, float newTangentMagnitude)
        {
            var normal = THIS.getNormalComponent(normalizedAxis);
            return normal + normalizedAxis * newTangentMagnitude;
        }

        public static Vector3 scaleTangent(this Vector3 THIS, Vector3 normalizedAxis, float tangentScale)
        {
            var c = THIS.getComponents(normalizedAxis);
            return c.normal + c.tangent * tangentScale;
        }

        public static Vector3 scaleNormal(this Vector3 THIS, Vector3 normalizedAxis, float normalScale)
        {
            var c = THIS.getComponents(normalizedAxis);
            return c.normal * normalScale + c.tangent;
        }

        public static Vector3 normalTo(this Vector3 THIS, Vector3 normalizedAxis)
        {
            Vector3 normal = THIS.getNormalComponent(normalizedAxis);
            if (normal.sqrMagnitude > 0.00001f)
            {
                return normal.normalized();
            }
            throw new Exception("normal is zero");
        }

        public static Vector2 getXy(this Vector3 v3)
        {
            return new Vector2(v3.x, v3.y);
        }

        public static Vector2 getXz(this Vector3 v3)
        {
            return new Vector2(v3.x, v3.z);
        }

        public static Vector3 withY(this Vector2 v2, float y)
        {
            return new Vector3(v2.x, y, v2.y);
        }

        public static Vector3 withZ(this Vector2 v2, float z)
        {
            return new Vector3(v2.x, v2.y, z);
        }

        public static Vector2 withX(this Vector2 v2, float x)
        {
            return new Vector2(x, v2.y);
        }

        public static Color withAlpha(this Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        public static Vector3 add(this Vector3 a, Vector3 b)
        {
            return a + b;
        }

        public static Vector3 add(this Vector3 a, float x, float y, float z)
        {
            return new Vector3(a.x + x, a.y + y, a.z + z);
        }

        public static Vector3 sub(this Vector3 a, Vector3 b)
        {
            return a - b;
        }

        public static Vector3 sub(this Vector3 a, float x, float y, float z)
        {
            return new Vector3(a.x - x, a.y - y, a.z - z);
        }

        public static float dot(this Vector3 a, Vector3 b)
        {
            return Vector3.Dot(a, b);
        }

        public static Vector3 normalized(this Vector3 a)
        {
            return Vector3.Normalize(a);
        }

        public static Vector3 normalized(this Vector3 a, float newLen)
        {
            return Vector3.Normalize(a) * newLen;
        }

        public static Vector2 normalized(this Vector2 a, float newLen)
        {
            return a.normalized * newLen;
        }

        public static float length(this Vector3 a)
        {
            return Vector3.Magnitude(a);
        }

        public static Vector3 withSetX(this Vector3 a, float value)
        {
            return new Vector3(value, a.y, a.z);
        }

        public static Vector3 withSetY(this Vector3 a, float value)
        {
            return new Vector3(a.x, value, a.z);
        }

        public static Vector3 withSetZ(this Vector3 a, float value)
        {
            return new Vector3(a.x, a.y, value);
        }

        public static float scalarProduct(this Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static Vector3 mul(this Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        public static Vector3 mul(this Vector3 a, float x, float y, float z)
        {
            return new Vector3(a.x * x, a.y * y, a.z * z);
        }

        public static Vector2 mul(this Vector2 a, Vector2 b)
        {
            return new Vector2(a.x * b.x, a.y * b.y);
        }

        public static Vector2 mul(this Vector2 a, float x, float y)
        {
            return new Vector2(a.x * x, a.y * y);
        }

        public static Vector3 pow(this Vector3 a, float x, float y, float z)
        {
            return new Vector3(Mathf.Pow(a.x, x), Mathf.Pow(a.y, y), Mathf.Pow(a.z, z));
        }

        public static Vector3 mul(this Vector3 a, float b)
        {
            return a * b;
        }

        public static Vector3 div(this Vector3 a, float b)
        {
            return a / b;
        }

        public static Vector3 div(this Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        public static Vector3 crossProduct(this Vector3 a, Vector3 b)
        {
            return Vector3.Cross(a, b);
        }

        public static float dist(this Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b);
        }

        public static float distSqr(this Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude;
        }

        public static Vector3 lerpT(this Vector3 a, Vector3 b, float speed)
        {
            return Vector3.Lerp(a, b, Time.deltaTime * speed);
        }

        public static Vector3 slerpT(this Vector3 a, Vector3 b, float speed)
        {
            return Vector3.Slerp(a, b, Time.deltaTime * speed);
        }

        public static Vector2 lerpT(this Vector2 a, Vector2 b, float speed)
        {
            return Vector2.Lerp(a, b, Time.deltaTime * speed);
        }

        public static float lerpT(this float a, float b, float speed)
        {
            return Mathf.Lerp(a, b, Time.deltaTime * speed);
        }

        public static Vector3 lerp(this Vector3 a, Vector3 b, float progress)
        {
            return Vector3.Lerp(a, b, progress);
        }

        public static Vector3 slerp(this Vector3 a, Vector3 b, float progress)
        {
            return Vector3.Slerp(a, b, progress);
        }

        public static Vector2 lerp(this Vector2 a, Vector2 b, float progress)
        {
            return Vector2.Lerp(a, b, progress);
        }

        public static float lerp(this float a, float b, float progress)
        {
            return Mathf.Lerp(a, b, progress);
        }

        public static Vector3 lerpUnclamped(this Vector3 a, Vector3 b, float progress)
        {
            return Vector3.LerpUnclamped(a, b, progress);
        }

        public static Vector3 slerpUnclamped(this Vector3 a, Vector3 b, float progress)
        {
            return Vector3.SlerpUnclamped(a, b, progress);
        }

        public static Vector2 lerpUnclamped(this Vector2 a, Vector2 b, float progress)
        {
            return Vector2.LerpUnclamped(a, b, progress);
        }

        public static float lerpUnclamped(this float a, float b, float progress)
        {
            return Mathf.LerpUnclamped(a, b, progress);
        }

        public static Vector3 rotateAroundAxis(this Vector3 vector, Vector3 axis, float degrees) =>
            Quaternion.AngleAxis(degrees, axis) * vector;

        public static Vector2 Rotate(this Vector2 v, float degrees)
        {
            float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
            float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);

            float tx = v.x;
            float ty = v.y;
            v.x = (cos * tx) - (sin * ty);
            v.y = (sin * tx) + (cos * ty);
            return v;
        }

        public static bool EqualsApprox(this Vector3 x, Vector3 other)
        {
            const float eps = 0.000001f;
            return Mathf.Abs(x.x - other.x) < eps && Mathf.Abs(x.y - other.y) < eps && Mathf.Abs(x.z - other.z) < eps;
        }

        public static bool EqualsApprox(this Vector2 x, Vector2 other)
        {
            const float eps = 0.000001f;
            return Mathf.Abs(x.x - other.x) < eps && Mathf.Abs(x.y - other.y) < eps;
        }

        public static T car<T>(this List<T> list)
        {
            return list[0];
        }

        public static T cadr<T>(this List<T> list)
        {
            return list[1];
        }

        //     https://github.com/Kent-H/blue3D/blob/master/Blue3D/src/blue3D/type/QuaternionF.java
        public static Quaternion ln(this Quaternion THIS)
        {
            float r = (float)Math.Sqrt(THIS.x * THIS.x + THIS.y * THIS.y + THIS.z * THIS.z);
            float t = r > 0.00001f ? (float)Math.Atan2(r, THIS.w) / r : 0.0f;
            return new Quaternion(THIS.x * t, THIS.y * t, THIS.z * t,
                0.5f * (float)Math.Log(THIS.w * THIS.w + THIS.x * THIS.x + THIS.y * THIS.y + THIS.z * THIS.z));
        }

        public static Quaternion exp(this Quaternion THIS)
        {
            float r = (float)Math.Sqrt(THIS.x * THIS.x + THIS.y * THIS.y + THIS.z * THIS.z);
            float et = (float)Math.Exp(THIS.w);
            float s = r >= 0.00001f ? et * (float)Math.Sin(r) / r : 0f;
            return new Quaternion(THIS.x * s, THIS.y * s, THIS.z * s, et * (float)Math.Cos(r));
        }

        public static Quaternion pow(this Quaternion THIS, float n)
        {
            return THIS.ln().scale(n).exp();
        }

        public static Quaternion rotSubDeprecated(this Quaternion THIS, Quaternion from)
        {
            return THIS.mul(from.conjug());
            //                return from.conjug().mul(THIS);
        }

        public static Quaternion rotSub(this Quaternion THIS, Quaternion from)
        {
            return from.conjug().mul(THIS);
        }

        public static Quaternion scale(this Quaternion THIS, float scale)
        {
            return new Quaternion(THIS.x * scale, THIS.y * scale, THIS.z * scale, THIS.w * scale);
        }

        public static Quaternion normalizeWithFixedW(this Quaternion THIS)
        {
            if (THIS.w > 1 || THIS.w < -1) throw new Exception("wrong w in " + THIS + " (" + THIS.w + ")");
            Vector3 xyz = new Vector3(THIS.x, THIS.y, THIS.z);
            xyz = xyz.normalized((float)Math.Sqrt(1 - THIS.w * THIS.w));
            return new Quaternion(xyz.x, xyz.y, xyz.z, THIS.w);
        }

        public static Quaternion normalizeWithFixedW(this Quaternion THIS, float w)
        {
            return THIS.withSetW(w).normalizeWithFixedW();
        }

        public static Quaternion withSetW(this Quaternion THIS, float w)
        {
            return new Quaternion(THIS.x, THIS.y, THIS.z, w);
        }

        public static Quaternion conjug(this Quaternion THIS)
        {
            return new Quaternion(-THIS.x, -THIS.y, -THIS.z, THIS.w);
        }

        public static Vector3 imaginary(this Quaternion q)
        {
            return new Vector3(q.x, q.y, q.z);
        }

        public static Vector3 rotate(this Quaternion THIS, Vector3 vector)
        {
            //            float newVar_7 = -(THIS.x * vector.x) + -(THIS.y * vector.y) + -(THIS.z * vector.z);
            //            float newVar_22 = THIS.w * vector.x + THIS.y * vector.z + -(THIS.z * vector.y);
            //            float newVar_37 = THIS.w * vector.y + THIS.z * vector.x + -(THIS.x * vector.z);
            //            float newVar_52 = THIS.w * vector.z + THIS.x * vector.y + -(THIS.y * vector.x);
            //            float mx = -THIS.x;
            //            float my = -THIS.y;
            //            float mz = -THIS.z;
            //            return new Vector3(
            //                newVar_7 * mx + newVar_22 * THIS.w + newVar_37 * mz + -(newVar_52 * my),
            //                newVar_7 * my + newVar_37 * THIS.w + newVar_52 * mx + -(newVar_22 * mz),
            //                newVar_7 * mz + newVar_52 * THIS.w + newVar_22 * my + -(newVar_37 * mx));


            return THIS * vector;
        }

        public static float magnitude(this Quaternion q)
        {
            return (float)Math.Sqrt(q.norm());
        }

        public static float norm(this Quaternion q)
        {
            return q.w * q.w + q.x * q.x + q.y * q.y + q.z * q.z;
        }

        public static Quaternion normalized(this Quaternion q)
        {
            float mag = 1.0f / q.magnitude();
            return new Quaternion(q.x * mag, q.y * mag, q.z * mag, q.w * mag);
        }

        public static Quaternion nlerp(this Quaternion from, Quaternion to, float blend)
        {
            return Quaternion.Lerp(from, to, blend);
        }

        public static Quaternion mul(this Quaternion a, Quaternion b)
        {
            return a * b;
        }
    }
}