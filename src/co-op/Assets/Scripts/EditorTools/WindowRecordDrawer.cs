#if UNITY_EDITOR
using Data.Configs;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    [CustomPropertyDrawer(typeof(WindowRecord))]
    public sealed class WindowRecordDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
        {
            var id = prop.FindPropertyRelative("windowID");
            var pf = prop.FindPropertyRelative("prefab");
            float w = pos.width;
            float gap = 4f;
            float left = w * 0.36f;
            float right = w - left - gap;

            EditorGUI.PropertyField(new Rect(pos.x, pos.y, left, pos.height), id, GUIContent.none);
            EditorGUI.PropertyField(new Rect(pos.x + left + gap, pos.y, right, pos.height), pf, GUIContent.none);
        }
    }
}
#endif
