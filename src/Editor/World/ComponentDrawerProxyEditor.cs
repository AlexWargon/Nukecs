using UnityEngine.UIElements;

#if UNITY_EDITOR && NUKECS_DEBUG

namespace Wargon.Nukecs.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Unity.Mathematics;
    using UnityEditor;
    using UnityEngine;

    public class ComponentDrawerProxy : ScriptableObject
    {
        public IComponent boxedComponent;
        public int typeIndex;
        public int entity;
        public byte world;
    }


    [CustomEditor(typeof(ComponentDrawerProxy))]
    public class ComponentDrawerProxyEditor : Editor
    {
        private const string VALUE_FIELD = "Value";
        private static bool _writeToWorld = true;
        private static GUIStyle _goStyle;
        private static readonly Dictionary<Type, FieldData[]> cachedFields = new();

        private sealed class FieldData {
            public string name;
            public Type fieldType;
            public Func<object, object> getter;
            public Action<object, object> setter;
            public FieldInfo fieldInfo;
            public type_data typeData;
            public bool displayAs;
            public type_data displayType;
        }
        static FieldData[] GetFieldData(Type type) {
            if (cachedFields.TryGetValue(type, out var fields)) return fields;

            var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            fields = new FieldData[fieldInfos.Length];
            for (int i = 0; i < fieldInfos.Length; i++) {
                var fi = fieldInfos[i];
                var displayAs = fi.GetCustomAttributes(typeof(DrawAsAttribute), false).Length > 0; 
                fields[i] = new FieldData {
                    name = fi.Name,
                    fieldType = fi.FieldType,
                    getter = FastReflectionAccessor.GetGetter(type, fi.Name),
                    setter = FastReflectionAccessor.GetSetter(type, fi.Name),
                    fieldInfo = fi,
                    typeData = type_db.get_type_data(fi.FieldType),
                    displayAs = displayAs,
                    displayType = displayAs ? type_db.get_type_data(fi.GetCustomAttribute<DrawAsAttribute>()?.drawerType) : null
                };
            }
            cachedFields[type] = fields;
            return fields;
        }
        private static GUIStyle ObjectFieldStyle =>
            _goStyle ??= new GUIStyle(GUI.skin.GetStyle("ObjectField"))
            {
                fixedHeight = EditorGUIUtility.singleLineHeight
            };

        public override void OnInspectorGUI()
        {
            var proxy = (ComponentDrawerProxy)target;
            if (proxy.boxedComponent == null)
            {
                EditorGUILayout.LabelField("Null component");
                return;
            }

            var type = proxy.boxedComponent.GetType();
            var fields = GetFieldData(type);
            var anyChanges = false;

            foreach (var field in fields)
            {
                if (field.fieldType.IsPointer)
                {
                    EditorGUILayout.LabelField(field.name, "<pointer field - not supported>");
                    continue;
                }

                var value = field.getter(proxy.boxedComponent);
                EditorGUI.BeginChangeCheck();
                var newValue = DrawField(field.name, field.typeData, value);
                if (EditorGUI.EndChangeCheck())
                {
                    field.fieldInfo.SetValue(proxy.boxedComponent, newValue);
                    anyChanges = true;
                }
            }

            if (anyChanges && _writeToWorld)
            {
                ref var pool = ref World.Get(proxy.world).UnsafeWorldRef
                    .GetUntypedPool(proxy.typeIndex);

                pool.SetObject(proxy.entity, proxy.boxedComponent);
            }

            _writeToWorld = true;
        }
        internal static object DrawField(string label, type_data fieldType, object value, Type overrideType = null, bool displayAs = false)
        {
            if (fieldType.is_pointer || fieldType.is_generic && fieldType.generic_type_definition == typeof(ptr<>) || fieldType.index == type<ptr>.index)
            {
                EditorGUILayout.LabelField(label, value.ToString());
                return value;
            }
            //shiiiiiiiiiiiiiiit
            if (fieldType.index == type<bool>.index)
                return EditorGUILayout.Toggle(label, (bool)value);
            if (fieldType.index == type<int>.index)
                return EditorGUILayout.IntField(label, (int)value);
            if (fieldType.index == type<float>.index)
                return EditorGUILayout.FloatField(label, (float)value);
            if (fieldType.index == type<double>.index)
                return EditorGUILayout.Toggle(label, (bool)value);
            if (fieldType.index == type<string>.index)
                return EditorGUILayout.TextField(label, (string)value);
            if (fieldType.index == type<Vector2>.index)
                return EditorGUILayout.Vector2Field(label, (Vector2)value);
            if (fieldType.index == type<float2>.index)
                return (float2)EditorGUILayout.Vector2Field(label, (float2)value);
            if (fieldType.index == type<Vector3>.index)
                return EditorGUILayout.Vector3Field(label, (Vector3)value);
            if (fieldType.index == type<float3>.index)
                return (float3)EditorGUILayout.Vector3Field(label, (float3)value);
            if (fieldType.index == type<Vector4>.index)
                return EditorGUILayout.Vector4Field(label, (Vector4)value);
            if (fieldType.index == type<float4>.index)
                return (float4)EditorGUILayout.Vector4Field(label, (float4)value);
            if (fieldType.index == type<Quaternion>.index)
                return EditorGUILayout.Vector3Field(label, ((Quaternion)value).eulerAngles);
            if (fieldType.index == type<quaternion>.index)
                return EditorGUILayout.Vector3Field(label, math.Euler((quaternion)value));
            if (fieldType.is_enum)
                return EditorGUILayout.EnumFlagsField(label, (Enum)value);
            if (type<UnityEngine.Object>.is_assignable_from(fieldType))
                return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, fieldType.val, true);
            if(fieldType.index == type<Color>.index)
                return EditorGUILayout.ColorField(label, (Color)value);
            if (fieldType.index == type<Entity>.index)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4));

                var e = (Entity)value;
                if (!World.Get(0).IsAlive || !EditorApplication.isPlaying) return value;
                var eName = e.Has<Name>() ? $"(e:{e.id}) {e.Get<Name>().value.Value}" : $"(e:{e.id})";
                var content = EditorGUIUtility.ObjectContent(null, typeof(GameObject));
                content.text = eName;
                content.image = EditorGUIUtility.IconContent("greenLight").image;
                if (GUILayout.Button(content, ObjectFieldStyle))
                {
                    _writeToWorld = false;
                    ECSDebugWindowUI.CanWriteToWorld = false;
                    ECSDebugWindowUI.Instance.SelectEntityFromField(e);
                }
                EditorGUILayout.EndHorizontal();
                return value;
            }

            if (fieldType.is_generic && fieldType.generic_type_definition == typeof(ObjectRef<>))
            {
                var innerType = fieldType.generic_argument00;
                var innerValue = value.GetPropertyValue(fieldType.val, VALUE_FIELD);
                var newInnerValue = DrawField(label, innerType, innerValue);
                if (!Equals(innerValue, newInnerValue))
                {
                    fieldType.val.GetProperty(VALUE_FIELD)!.SetValue(value, newInnerValue);
                }
                return value;
            }
            
            if ((fieldType.is_value && !fieldType.is_primitive && !fieldType.is_enum) || 
                (fieldType.is_class && !type<UnityEngine.Object>.is_assignable_from(fieldType)))
            {
                EditorGUILayout.LabelField($"{label} ({fieldType.name})");
                EditorGUI.indentLevel++;
                if (value == null && fieldType.is_class)
                    value = Activator.CreateInstance(fieldType.val);
                var fields = GetFieldData(fieldType.val);
                foreach (var subField in fields)
                {
                    var subValue = value.GetFieldValue(fieldType.val, subField.name);
                    var newSubValue = DrawField(subField.name, subField.typeData, subValue);
                    if (!Equals(subValue, newSubValue))
                        subField.fieldInfo.SetValue(value, newSubValue);
                }
                EditorGUI.indentLevel--;
                return value;
            }
            EditorGUILayout.LabelField($"{label} ({fieldType.name}) — unsupported");
            return value;
        }
    }
}
#endif