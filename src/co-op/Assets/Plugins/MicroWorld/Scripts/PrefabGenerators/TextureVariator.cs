using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace MicroWorldNS
{
    /// <summary>
    /// Prefab generator, applies random textures to random meshes
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    class TextureVariator : MonoBehaviour, IPrefabGenerator
    {
        [SerializeField] int Seed = 0;
        [SerializeField] Gradient RndColor = new Gradient() { alphaKeys = new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0, 0) }, mode = GradientMode.Fixed };
        [SerializeField] string RndColorName = "_FlowerColor";
        [SerializeField] int SubMesh = 0;
        [SerializeField] Mesh[] Meshes;
        [SerializeField] Texture[] Textures;

        public List<GameObject> GetPrefabs(int microWorldSeed, int amountOfSet, Transform holder)
        {
            var mr = GetComponent<MeshRenderer>();
            var mf = GetComponent<MeshFilter>();
            var material = mr.sharedMaterials[SubMesh];

            var meshes = ListPool<Mesh>.Get();
            meshes.AddRange(Meshes.Where(t => t != null));
            if (meshes.Count == 0)
                meshes.Add(mf.sharedMesh);

            var textures = ListPool<Texture>.Get();
            textures.AddRange(Textures.Where(t => t != null));
            if (textures.Count == 0)
                textures.Add(material.mainTexture);

            var rnd = new Rnd(name, Rnd.CombineHashCodes(this.Seed, microWorldSeed));
            rnd.ShuffleFisherYates(textures);
            rnd.ShuffleFisherYates(meshes);
            var dict = new Dictionary<(Mesh, Texture, Color), GameObject>();

            for (int i = 0; i < amountOfSet; i++)
            {
                var mesh = meshes[i % meshes.Count];
                var tex = textures[i % textures.Count];
                var color = RndColor.Evaluate(rnd.Float());

                var key = (mesh, tex, color);
                if (dict.TryGetValue(key, out var go))
                {
                    amountOfSet--;
                    continue;
                }

                go = new GameObject($"{name} #{i}", typeof(MeshRenderer), typeof(MeshFilter));
                go.transform.SetParent(holder);
                go.layer = gameObject.layer;
                go.isStatic = gameObject.isStatic;
                dict[key] = go;


                var mat = Instantiate(material);
                mat.mainTexture = tex;
                mat.SetColor(RndColorName, color);
                go.GetOrAddComponent<MeshFilter>().mesh = mesh;
                var newMr = go.GetOrAddComponent<MeshRenderer>();
                var mats = mr.sharedMaterials;
                mats[SubMesh] = mat;
                newMr.sharedMaterials = mats;
                newMr.shadowCastingMode = mr.shadowCastingMode;
                newMr.receiveShadows = mr.receiveShadows;
            }

            ListPool<Mesh>.Release(meshes);

            return dict.Values.ToList();
        }

        public InspectorButton _Test;

        void Test()
        {
            var temps = GetComponentsInChildren<Transform>().Where(t => t.name == "_temp").ToArray();
            foreach (var t in temps)
                Helper.DestroySafe(t.gameObject);

            var prefabs = GetPrefabs(UnityEngine.Random.Range(0, 10000), 1, transform);
            var go = new Rnd().GetRnd(prefabs);
            go.transform.localPosition = new Vector3(2, 0, 1f);
            go.name = "_temp";
            go.hideFlags = HideFlags.DontSave;
        }
    }
}


