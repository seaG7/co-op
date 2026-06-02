using System;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MicroWorldNS
{
    [Serializable]
    public struct RangeFloat
    {
        public float Min;
        public float Max;
        public RangeType Type;

        public RangeFloat(float min, float max, RangeType type = RangeType.Inside)
        {
            Min = min;
            Max = max;
            Type = type;
        }

        public bool InRange(float value)
        {
            var res = value >= Min && value <= Max;
            return Type == RangeType.Inside ? res : !res;
        }

        public override string ToString()
        {
            return $"{Type} {Min:0.}:{Max:0.}";
        }
    }

    [Serializable]
    public struct RangeInt
    {
        public int Min;
        public int Max;
        public RangeType Type;

        public RangeInt(int min, int max, RangeType type = RangeType.Inside)
        {
            Min = min;
            Max = max;
            Type = type;
        }

        public bool IsInRange(int value)
        {
            var res = value >= Min && value <= Max;
            return Type == RangeType.Inside ? res : !res;
        }
    }

    [Serializable]
    public enum RangeType : byte
    {
        Inside, Outside
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class RangeModeAttribute : PropertyAttribute
    {
        public bool CanBeOutside;
        public float Min;
        public float Max;
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(RangeFloat), true)]
    public class RangeFloatDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var mode = this.fieldInfo.GetCustomAttributes(typeof(RangeModeAttribute), true).FirstOrDefault() as RangeModeAttribute;
            if (mode == null) mode = new RangeModeAttribute();


            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            EditorGUI.indentLevel = 0; // PropertyDrawer Indent fix for nested inspectors

            var minProp = property.FindPropertyRelative("Min");
            var maxProp = property.FindPropertyRelative("Max");
            var typeProp = property.FindPropertyRelative("Type");

            if (maxProp.floatValue < minProp.floatValue)
            {
                typeProp.enumValueIndex = (int)RangeType.Outside;
                var temp = maxProp.floatValue;
                maxProp.floatValue = minProp.floatValue;
                minProp.floatValue = temp;
            }

            var drawSlider = !mode.CanBeOutside && (mode.Min != 0f || mode.Max != 0f) && typeProp.enumValueIndex == 0 && position.width > 120;

            var labelWidth = mode.CanBeOutside ? Mathf.Min(60, position.width / 3) : 0;
            var spaceWidth = drawSlider ? 50f : 5f;
            var valWidth = (position.width / 2) - (labelWidth / 2f) - (spaceWidth / 2);

            if (drawSlider)
            {
                valWidth = 35;
                spaceWidth = position.width - valWidth * 2;
            }

            position.width = valWidth;
            EditorGUI.PropertyField(position, minProp, GUIContent.none);

            position.x += valWidth;
            position.width = spaceWidth;
            if (drawSlider)
            {
                var min = minProp.floatValue;
                var max = maxProp.floatValue;
                EditorGUI.MinMaxSlider(position, ref min, ref max, mode.Min, mode.Max);
                if (min != minProp.floatValue || max != maxProp.floatValue)
                {
                    minProp.floatValue = min;
                    maxProp.floatValue = max;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                EditorGUI.LabelField(position, ":");
            }

            position.x += spaceWidth;
            position.width = valWidth;
            EditorGUI.PropertyField(position, maxProp, GUIContent.none);

            position.x += valWidth;
            if (mode.CanBeOutside)
            {
                position.width = labelWidth;
                if (GUI.Button(position, typeProp.enumValueIndex == 0 ? "Inside" : "Outside"))
                    typeProp.enumValueIndex = (typeProp.enumValueIndex + 1) % 2;
            }

            if (GUI.changed)
            {
                if (mode.Max != 0 || mode.Min != 0)
                {
                    minProp.floatValue = Mathf.Max(mode.Min, minProp.floatValue);
                    maxProp.floatValue = Mathf.Min(mode.Max, maxProp.floatValue);
                }

                if (maxProp.floatValue < minProp.floatValue) maxProp.floatValue = minProp.floatValue;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(RangeInt), true)]
    public class RangeIntDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var mode = this.fieldInfo.GetCustomAttributes(typeof(RangeModeAttribute), true).FirstOrDefault() as RangeModeAttribute;
            if (mode == null) mode = new RangeModeAttribute();


            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            EditorGUI.indentLevel = 0; // PropertyDrawer Indent fix for nested inspectors

            var minProp = property.FindPropertyRelative("Min");
            var maxProp = property.FindPropertyRelative("Max");
            var typeProp = property.FindPropertyRelative("Type");

            if (maxProp.intValue < minProp.intValue)
            {
                typeProp.enumValueIndex = (int)RangeType.Outside;
                var temp = maxProp.intValue;
                maxProp.intValue = minProp.intValue;
                minProp.intValue = temp;
            }

            var labelWidth = mode.CanBeOutside ? Mathf.Min(60, position.width / 3) : 0;
            var spaceWidth = 5f;
            var valWidth = (position.width / 2) - (labelWidth / 2f) - (spaceWidth / 2);

            position.width = valWidth;
            EditorGUI.PropertyField(position, minProp, GUIContent.none);

            position.x += valWidth;
            position.width = spaceWidth;
            EditorGUI.LabelField(position, ":");

            position.x += spaceWidth;
            position.width = valWidth;
            EditorGUI.PropertyField(position, maxProp, GUIContent.none);

            position.x += valWidth;
            if (mode.CanBeOutside)
            {
                position.width = labelWidth;
                if (GUI.Button(position, typeProp.enumValueIndex == 0 ? "Inside" : "Outside"))
                    typeProp.enumValueIndex = (typeProp.enumValueIndex + 1) % 2;
            }

            if (GUI.changed)
            {
                if (mode.Max != 0 || mode.Min != 0)
                {
                    minProp.intValue = Mathf.Max((int)mode.Min, minProp.intValue);
                    maxProp.intValue = Mathf.Min((int)mode.Max, maxProp.intValue);
                }

                if (maxProp.intValue < minProp.intValue) maxProp.intValue = minProp.intValue;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }
    }
#endif
}