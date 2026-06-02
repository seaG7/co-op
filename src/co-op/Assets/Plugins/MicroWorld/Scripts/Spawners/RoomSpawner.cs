using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Linq;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// The spawner is designed to create building elements - walls, windows, doors, balconies, fences, columns, steps, and so on.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/1vjbYEHIz3ImNsSFFh7J9uqYQmq9SOgXeJuz8NxcbzMg/edit?tab=t.0#heading=h.ljwb2m4liiln")]
    public class RoomSpawner : BaseSpawner
    {
        [Tooltip("Use vertical scaling.")]
        [SerializeField] public bool StretchByY = false;
        [Space]
        [Tooltip("List of materials that are used in building.")]
        [SerializeField] internal Material[] Materials;
        [Space]
        [SerializeField] List<GeometryElement> Elements = new List<GeometryElement>();
        public InspectorButton _CreateElement;

        internal Dictionary<GeometryPlace, List<GeometryElement>> placeToElements = new Dictionary<GeometryPlace, List<GeometryElement>>();

        private void Start()
        {
        }

        public override IEnumerator Prepare(MicroWorld builder)
        {
            yield return base.Prepare(builder);

            if (Materials == null || Materials.Length == 0)
                Materials = new Material[1] { Resources.Load<Material>("Ruins")};

            // make dict place to elements
            placeToElements.Clear();
            var places = Enum.GetValues(typeof(GeometryPlace)).Cast<GeometryPlace>().ToArray();

            var exclSegGroups = new Dictionary<string, int>();

            foreach (var element in Elements)
            {
                element.Prepare(Materials);
                if (!element.Enabled)
                    continue;

                // create id for exclusive groups (to increase performance in following usage)
                var exclGroupId = -1;
                if (element.SegmentExclusiveGroup.NotNullOrEmpty())
                {
                    if (!exclSegGroups.TryGetValue(element.SegmentExclusiveGroup, out exclGroupId))
                        exclGroupId = exclSegGroups[element.SegmentExclusiveGroup] = exclSegGroups.Count;
                }

                element.ExclusiveSegmentGroupId = exclGroupId;

                // create element list by places
                foreach (var place in places)
                    if (place != GeometryPlace.None)
                    if (element.Place.HasFlag(place))
                    {
                        if (!placeToElements.TryGetValue(place, out var list))
                            placeToElements[place] = list = new List<GeometryElement>();
                        list.Add(element);
                    }
            }
        }

#if UNITY_EDITOR
        void CreateElement()
        {
            Elements.Add(new GeometryElement());
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnValidate()
        {
            if (Elements.Count == 1)
                if (!Elements[0].Enabled && Elements[0].Place == GeometryPlace.None && Elements[0].EdgeType == 0 && Elements[0].Chance == 0f)
                    Elements[0] = new GeometryElement();

            for (int i = 0; i < Elements.Count; i++)
            {
                var e = Elements[i];
                if (e == null) continue;
                if (e.Shapes == null)
                    e = Elements[i] = new GeometryElement();
                e.OnValidate();
            }
        }
    }
}