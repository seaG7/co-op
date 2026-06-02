using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace MicroWorldNS
{
    public class PopupAttribute : PropertyAttribute
    {
        public string listSourcePropertyName;
        public bool IsEditable;

        public PopupAttribute(string listSourcePropertyName, bool isEditable = false)
        {
            this.listSourcePropertyName = listSourcePropertyName;
            IsEditable = isEditable;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class LabelAttribute : PropertyAttribute
    {
        public string Label;

        public LabelAttribute(string label)
        {
            this.Label = label;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class CommentAttribute : PropertyAttribute
    {
        public readonly string text;

        public CommentAttribute(string text)
        {
            this.text = text;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class HighlightAttribute : ShowIfAttribute
    {
        public Color Color = Color.red;

        public HighlightAttribute(float r, float g, float b) : base(null)
        {
            Color = new Color(r, g, b);
        }

        public HighlightAttribute(float r, float g, float b, string condPropertyName, params object[] condValues) : base(condPropertyName, condValues)
        {
            Color = new Color(r, g, b);
        }

        public HighlightAttribute(float r, float g, float b, string condPropertyName) : base(condPropertyName, true)
        {
            Color = new Color(r, g, b);
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PreviewIconAttribute : PropertyAttribute
    {
        public float Height = 0;
        public bool Transparent = true;
        public string InfoPropertyName;

        public PreviewIconAttribute(float height = 100)
        {
            this.Height = height;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class InfoAttribute : PropertyAttribute
    {
        public float MinHeight = 0;
        public enum MessageType { NONE, INFO, WARNING, ERROR }
        public MessageType messageType = MessageType.INFO;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ReadOnlyAttribute : ShowIfAttribute
    {
        public ReadOnlyAttribute() : base(null)
        {
        }

        public ReadOnlyAttribute(string condPropertyName, params object[] condValues) : base(condPropertyName, condValues)
        {
        }

        public ReadOnlyAttribute(string condPropertyName) : base(condPropertyName, true)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class HideIfAttribute : ShowIfAttribute
    {
        public HideIfAttribute(string condPropertyName, params object[] condValues) : base(condPropertyName, condValues)
        {
        }

        public HideIfAttribute(string condPropertyName) : base(condPropertyName, true)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public string CondPropertyName;// property, field or method name
        public System.Object[] CondValues;
        public DrawIfOp Op = DrawIfOp.AnyTrue;

        public ShowIfAttribute(string condPropertyName, params object[] condValues)
        {
            this.CondPropertyName = condPropertyName;
            this.CondValues = condValues;
        }

        public ShowIfAttribute(string condPropertyName) : this(condPropertyName, true)
        {
        }
    }

    public enum DrawIfOp
    {
        AnyTrue, AllTrue, AnyFalse, AllFalse
    }

    //[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    //public class ButtonFieldAttribute : PropertyAttribute
    //{
    //    public string Method;
    //    public string[] Texts { get; private set; }
    //    public Color Color = Color.clear;

    //    public ButtonFieldAttribute()
    //    {
    //    }

    //    public ButtonFieldAttribute(string method, params string[] texts)
    //    {
    //        this.Method = method;
    //        this.Texts = texts;
    //        if (Texts.Length == 0)
    //            Texts = new string[] { Method };
    //    }
    //}

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class FlagsAsCheckboxesAttribute : PropertyAttribute
    {
        public float CheckboxWidth = 150;
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class LayerAttribute : PropertyAttribute
    {
    }

    [Serializable]
    public enum InspectorButton : byte
    {
    }

#if UNITY_EDITOR

    static class MemberInfoCache
    {
        public static readonly Dictionary<(Type, string), MemberInfo> Cache = new Dictionary<(Type, string), MemberInfo>();
        public static readonly Dictionary<Type, Array> EnumValuesCache = new Dictionary<Type, Array>();

        public static Array GetEnumValues(Type enumType)
        {
            if (EnumValuesCache.TryGetValue(enumType, out var arr))
                return arr;

            return EnumValuesCache[enumType] = Enum.GetValues(enumType);
        }


        public static T Get<T>(Type type, string name) where T : MemberInfo
        {
            if (Cache.TryGetValue((type, name), out var info))
                return info as T;

            var info2 = type.GetMember(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic).FirstOrDefault();
            Cache[(type, name)] = info2;

            return info2 as T;
        }

        public static System.Object GetObject(Type type, SerializedProperty property)
        {
            //Look for the sourcefield within the object that the property belongs to
            string propertyPath = property.propertyPath; //returns the property path of the property we want to apply the attribute to
            string conditionPath = propertyPath.Replace(property.name, "").TrimEnd('.'); //changes the path to the conditionalsource property path
            SerializedProperty sourcePropertyValue = property.serializedObject.FindProperty(conditionPath);

            if (sourcePropertyValue == null)
                return property.serializedObject.targetObject;

#if UNITY_2022_1_OR_NEWER
            return sourcePropertyValue.boxedValue;
#else
            return GetTargetObjectOfProperty(property, 1);
#endif      
        }

        public static object GetTargetObjectOfProperty(SerializedProperty prop, int skipLast = 0)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements.SkipLast(skipLast))
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }

        public static System.Object GetPropertyValue(Type type, SerializedProperty property, string fieldName)
        {
            var obj = GetObject(type, property);
            var info = Get<MemberInfo>(type, fieldName);
            if (obj == null || info == null)
                return null;

            switch (info)
            {
                case PropertyInfo propertyInfo: return propertyInfo.GetValue(obj, null);
                case FieldInfo fInfo: return fInfo.GetValue(obj);
                case MethodInfo mInfo: return mInfo.Invoke(obj, null);
            }

            return null;
        }
    }

    [CustomPropertyDrawer(typeof(PopupAttribute))]
    public class PopupDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = attribute as PopupAttribute;
            var list = MemberInfoCache.GetPropertyValue(fieldInfo.DeclaringType, property, attr.listSourcePropertyName) as IEnumerable<string>;

            if (!attr.IsEditable)
            {
                FixedPopup(position, property, label, list);
                return;
            }

            const int W = 20;

            var rect = new Rect(position.x, position.y, position.width - W - 4, position.height);
            EditorGUI.PropertyField(rect, property, label);

            if (list != null && list.Any())
            {
                var array = Enumerable.Range(0, 1).Select(_ => "<Custom>").Union(list).ToArray();
                var selectedIndex = Mathf.Max(Array.IndexOf(array, property.stringValue), 0);

                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                var oldWidth = EditorStyles.popup.fixedWidth;
                EditorStyles.popup.fixedWidth = W;
                rect = new Rect(position.x + position.width - W, position.y, W, position.height);
                selectedIndex = EditorGUI.Popup(rect, "", selectedIndex, array);
                EditorStyles.popup.fixedWidth = oldWidth;
                EditorGUI.indentLevel = oldIndent;
                if (selectedIndex > 0)
                    property.stringValue = array[selectedIndex];
            }
        }

        private static void FixedPopup(Rect position, SerializedProperty property, GUIContent label, IEnumerable<string> list)
        {
            if (list != null && list.Any())
            {
                var array = list.ToArray();
                var selectedIndex = Mathf.Max(Array.IndexOf(array, property.stringValue), 0);
                selectedIndex = EditorGUI.Popup(position, property.name, selectedIndex, array);
                property.stringValue = array[selectedIndex];
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }
    }

    [CustomPropertyDrawer(typeof(CommentAttribute))]
    public class CommentDrawer : DecoratorDrawer
    {
        private const float HEIGHT_PADDING = 4f;

        private CommentAttribute Target => attribute as CommentAttribute;

        public override void OnGUI(Rect _rect)
        {
            float indent = GetIndentLength(_rect);

            _rect.Set(
                _rect.x + indent, _rect.y,
                _rect.width - indent, GetBoxHeight() - HEIGHT_PADDING * 0.5f);

            EditorGUI.HelpBox(_rect, Target.text, MessageType.None);
        }

        public static float GetIndentLength(Rect _sourceRect)
        {
            Rect indentRect = EditorGUI.IndentedRect(_sourceRect);
            float indentLength = indentRect.x - _sourceRect.x;

            return indentLength;
        }

        //How tall the GUI is for this decorator
        public override float GetHeight()
        {
            return GetBoxHeight() + HEIGHT_PADDING;
        }

        private float GetBoxHeight()
        {
            float width = EditorGUIUtility.currentViewWidth;
            float minHeight = EditorGUIUtility.singleLineHeight * 2f;

            // Icon, Scrollbar, Indent
            width -= 68;

            //Need a little extra for correct sizing of InfoBox
            float actualHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(Target.text), width);
            return Mathf.Max(minHeight, actualHeight);
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(UnityEngine.Object), true, isFallback = true)]
    public class Inspector1Editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            this.DrawDefaultInspector();
        }
    }

    [CustomPropertyDrawer(typeof(LayerAttribute))]
    public class LayerPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                // generate the taglist + custom tags
                List<string> tagList = new List<string>();
                tagList.AddRange(UnityEditorInternal.InternalEditorUtility.layers);

                int index = tagList.IndexOf(LayerMask.LayerToName(property.intValue));

                // Draw the popup box with the current selected index
                index = EditorGUI.Popup(position, property.displayName, index, tagList.ToArray());
                //index = EditorGUILayout.Popup(property.displayName, index, tagList.ToArray());

                // Adjust the actual string value of the property based on the selection
                property.intValue = LayerMask.NameToLayer(tagList[index]);
            }
        }
    }

    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public class LabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var labelAttribute = attribute as LabelAttribute;
            var guiContent = new GUIContent(labelAttribute.Label);
            EditorGUI.PropertyField(position, property, guiContent, true);
        }
    }

    [CustomPropertyDrawer(typeof(InfoAttribute))]
    public class InfoDrawer : PropertyDrawer
    {
        private const float HEIGHT_PADDING = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (attribute as InfoAttribute);
            var val = property.stringValue;
            if (string.IsNullOrWhiteSpace(val))
                return;

            float indent = GetIndentLength(position);

            position.Set(
                position.x + indent, position.y,
                position.width - indent, GetBoxHeight(property) - HEIGHT_PADDING * 0.5f);

            var type = GetMessageType(val);

            EditorGUI.HelpBox(position, val, (UnityEditor.MessageType)(int)type);
        }

        private static InfoAttribute.MessageType GetMessageType(string val)
        {
            var res = InfoAttribute.MessageType.INFO;
            if (val.Contains("!"))
            {
                res = InfoAttribute.MessageType.WARNING;
                if (val.Contains("!!"))
                    res = InfoAttribute.MessageType.ERROR;
            }

            return res;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetBoxHeight(property);
        }

        public static float GetIndentLength(Rect _sourceRect)
        {
            Rect indentRect = EditorGUI.IndentedRect(_sourceRect);
            float indentLength = indentRect.x - _sourceRect.x;

            return indentLength;
        }

        private float GetBoxHeight(SerializedProperty property)
        {
            var attr = (attribute as InfoAttribute);
            var val = property.stringValue;
            if (string.IsNullOrWhiteSpace(val))
                return -EditorGUIUtility.standardVerticalSpacing;

            float width = EditorGUIUtility.currentViewWidth;
            float minHeight = Mathf.Max(attr.MinHeight, EditorGUIUtility.singleLineHeight * 2f);

            // Icon, Scrollbar, Indent
            width -= 68;

            //Need a little extra for correct sizing of InfoBox
            float actualHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(val), width);
            return Mathf.Max(minHeight, actualHeight);
        }
    }

    [CustomPropertyDrawer(typeof(PreviewIconAttribute))]
    public class PreviewIconPropertyDrawer : PropertyDrawer
    {
        const float _space = 10;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var initRect = position;
            var attr = (attribute as PreviewIconAttribute);
            var _height = attr.Height;
            var _width = attr.Height;
            //EditorGUI.indentLevel = 0;

            if (property.objectReferenceValue == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            Texture2D texture = AssetPreview.GetAssetPreview(property.objectReferenceValue);
            if (texture)
            {
                var imageAspect = texture.width / (float)texture.height;
                _width = texture.width;
                GUI.DrawTexture(new Rect(position.position, new Vector2(_width, _height)), texture, ScaleMode.ScaleToFit, attr.Transparent, imageAspect);
            }

            if (attr.InfoPropertyName.NotNullOrEmpty())
            {
                var val = MemberInfoCache.GetPropertyValue(fieldInfo.DeclaringType, property, attr.InfoPropertyName)?.ToString();
                if (val != null)
                {
                    position.x = initRect.x + _width;
                    position.height = _height;
                    position.width = initRect.width - _width;
                    GUI.Label(position, new GUIContent(val));
                }
            }

            position = initRect;
            position.y += _height + _space;
            position.height += -_height - _space;
            EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var has = property.objectReferenceValue;
            var _height = (attribute as PreviewIconAttribute).Height;
            return base.GetPropertyHeight(property, label) + (has ? (_height + _space) : 0);
        }
    }

    [CustomPropertyDrawer(typeof(ShowIfAttribute), true)]
    public class ShowIfDrawer : PropertyDrawer
    {
        protected float _height;
        bool isConditionOK;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (_height > 0)
            {
                var attr = (attribute as ShowIfAttribute);
                var type = attribute.GetType();

                var prevColor = GUI.color;
                var prevEnabled = GUI.enabled;

                //ReadOnlyAttribute ?
                GUI.enabled &= isConditionOK || type != typeof(ReadOnlyAttribute);

                //HighlightAttribute ?
                if (type == typeof(HighlightAttribute) && !isConditionOK)
                    GUI.color = (attribute as HighlightAttribute).Color;

                EditorGUI.PropertyField(position, property, label, true);

                GUI.enabled = prevEnabled;
                GUI.color = prevColor;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attr = (attribute as ShowIfAttribute);
            var type = attribute.GetType();

            isConditionOK = false;

            if (!string.IsNullOrWhiteSpace(attr.CondPropertyName))
            {
                var val = MemberInfoCache.GetPropertyValue(fieldInfo.DeclaringType, property, attr.CondPropertyName);
                switch (attr.Op)
                {
                    case DrawIfOp.AnyTrue: isConditionOK = attr.CondValues.Any(v => System.Object.Equals(val, v)); break;
                    case DrawIfOp.AllTrue: isConditionOK = attr.CondValues.All(v => System.Object.Equals(val, v)); break;
                    case DrawIfOp.AnyFalse: isConditionOK = attr.CondValues.Any(v => !System.Object.Equals(val, v)); break;
                    case DrawIfOp.AllFalse: isConditionOK = attr.CondValues.All(v => !System.Object.Equals(val, v)); break;
                }

                if (type == typeof(HideIfAttribute))
                    isConditionOK = !isConditionOK;
            }

            if (isConditionOK || type == typeof(ReadOnlyAttribute) || type == typeof(HighlightAttribute))
                return _height = base.GetPropertyHeight(property, label);
            else
                return _height = -EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(InspectorButton))]
    public class ButtonFieldObjDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var parts = property.name.Trim('_').Split('_');
            var methodName = parts[0];
            var title = ToReadableFormat(parts.Length > 1 ? parts[1] : methodName);
            var mi = MemberInfoCache.Get<MethodInfo>(fieldInfo.DeclaringType, methodName);

            if (mi != null)
            {
                if (parts.Length > 2)
                {
                    var w = position.width / (parts.Length - 1);
                    var x = position.x;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (GUI.Button(new Rect(x, position.y, w, position.height), ToReadableFormat(parts[i])))
                            ExecuteMethod(property, methodName, i - 1);
                        x += w;
                    }
                }
                else
                if (GUI.Button(position, title))
                    ExecuteMethod(property, methodName);
            }
            else
            {
                GUI.Label(position, "Unknown method: " + methodName);
            }
        }

        string ToReadableFormat(string fieldName)
        {
            // –егул€рное выражение дл€ поиска заглавных букв
            string result = Regex.Replace(fieldName, "(\\B[A-Z])", " $1");

            // ѕреобразование первой буквы в заглавную
            return char.ToUpper(result[0]) + result.Substring(1);
        }

        void ExecuteMethod(SerializedProperty property, string methodName, int parameter = -1)
        {
            var obj = MemberInfoCache.GetObject(fieldInfo.DeclaringType, property);
            var info = MemberInfoCache.Get<MemberInfo>(fieldInfo.DeclaringType, methodName);
            if (obj == null || info == null)
                return;

            switch (info)
            {
                case MethodInfo mInfo:
                    if (parameter >= 0)
                        mInfo.Invoke(obj, new object[] { parameter });
                    else
                        mInfo.Invoke(obj, null);
                    break;
            }
        }
    }

    //[CustomPropertyDrawer(typeof(ButtonFieldAttribute))]
    //public class ButtonFieldDrawer : PropertyDrawer
    //{
    //    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    //    {
    //        var but = attribute as ButtonFieldAttribute;
    //        var prevColor = GUI.color;

    //        if (but.Color != Color.clear)
    //            GUI.color = but.Color;

    //        if (but.Texts.Length == 1)
    //        {
    //            if (GUI.Button(position, but.Texts[0]))
    //                ExecuteMethod(property, but.Method);
    //        }else
    //        {
    //            var w = position.width / but.Texts.Length;
    //            var x = position.x;
    //            for (int i = 0; i < but.Texts.Length; i++)
    //            {
    //                if (GUI.Button(new Rect(x, position.y, w, position.height), but.Texts[i]))
    //                    ExecuteMethod(property, but.Method, i);
    //                x += w;
    //            }
    //        }

    //        GUI.color = prevColor;
    //    }

    //    void ExecuteMethod(SerializedProperty property, string methodName, int parameter = -1)
    //    {
    //        var obj = MemberInfoCache.GetObject(fieldInfo.DeclaringType, property);
    //        var info = MemberInfoCache.Get<MemberInfo>(fieldInfo.DeclaringType, methodName);
    //        if (obj == null || info == null)
    //            return;

    //        switch (info)
    //        {
    //            case MethodInfo mInfo:
    //                if (parameter >= 0)
    //                    mInfo.Invoke(obj, new object[] { parameter });
    //                else
    //                    mInfo.Invoke(obj, null);
    //                break;
    //        }
    //    }

    //    private static void ShowPropertyField(SerializedProperty property)
    //    {
    //        property.serializedObject.Update();

    //        EditorGUILayout.PropertyField(property);

    //        property.serializedObject.ApplyModifiedProperties();
    //    }
    //}

    [CustomPropertyDrawer(typeof(FlagsAsCheckboxesAttribute))]
    public class FlagsAsCheckboxesDrawer : PropertyDrawer
    {
        Array values;
        float rowHeight;
        int perRow;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (attribute as FlagsAsCheckboxesAttribute);

            var itemWidth = position.width / perRow;
            var init = position;
            var resValue = property.enumValueFlag;

            for (int i = 0; i < values.Length - 1; i++)
            {
                var iCol = i % perRow;
                var iRow = i / perRow;
                var val = values.GetValue(i + 1);
                var rect = new Rect(init.x + iCol * itemWidth, init.y + iRow * rowHeight, itemWidth, rowHeight);
                var name = property.enumDisplayNames[i + 1];
                var res = EditorGUI.ToggleLeft(rect, name, (resValue & (int)val) != 0);
                resValue = res ? resValue | (int)val : resValue & ~(int)val;
                //EditorGUI.PropertyField(new Rect(init.x + iCol * itemWidth, init.y + iRow * rowHeight, itemWidth, rowHeight), property, label, true);
            }

            if (property.enumValueFlag != resValue)
                property.enumValueFlag = resValue;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attr = (attribute as FlagsAsCheckboxesAttribute);
            var obj = MemberInfoCache.GetPropertyValue(fieldInfo.ReflectedType, property, property.name);
            values = MemberInfoCache.GetEnumValues(obj.GetType());
            var valCount = values.Length - 1;
            perRow = (int)(EditorGUIUtility.currentViewWidth / attr.CheckboxWidth);
            if (perRow < 1) perRow = 1;
            var rows = valCount / perRow + (valCount % perRow == 0 ? 0 : 1);
            rowHeight = base.GetPropertyHeight(property, label);
            return rowHeight * rows;
        }
    }
#endif
}
