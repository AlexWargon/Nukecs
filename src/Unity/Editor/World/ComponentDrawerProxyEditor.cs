#if UNITY_EDITOR
using System;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Wargon.Nukecs.Editor
{
    public class ComponentDrawerProxy : ScriptableObject
    {
        public object boxedComponent;
        public int typeIndex;
        public int entity;
        public byte world;
    }
    [CustomEditor(typeof(ComponentDrawerProxy))]
    public class ComponentDrawerProxyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var proxy = (ComponentDrawerProxy)target;
            if (proxy.boxedComponent == null)
            {
                EditorGUILayout.LabelField("Null component");
                return;
            }

            var type = proxy.boxedComponent.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            bool anyChanges = false;

            foreach (var field in fields)
            {
                if (field.FieldType.IsPointer)
                {
                    EditorGUILayout.LabelField(field.Name, "<pointer field - not supported>");
                    continue;
                }
                //object value = field.GetValue(proxy.boxedComponent);
                var getter = FastFieldAccessor.GetGetter(type, field.Name);
                //var setter = FastFieldAccessor.GetSetter(type, field.Name);
                object value = getter(proxy.boxedComponent);
                EditorGUI.BeginChangeCheck();
                object newValue = DrawField(field.Name, field.FieldType, value);
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(proxy.boxedComponent, newValue);
                    //setter(proxy.boxedComponent, newValue);
                    anyChanges = true;
                    //dbug.log("changed");
                }
            }

            if (anyChanges)
            {
                ref var pool = ref World.Get(proxy.world).UnsafeWorldRef
                    .GetUntypedPool(proxy.typeIndex);

                pool.SetObject(proxy.entity, (IComponent)proxy.boxedComponent);
            }
        }
        private object DrawField(string label, Type fieldType, object value)
        {
            if (fieldType == typeof(int))
                return EditorGUILayout.IntField(label, (int)value);
            if (fieldType == typeof(float))
                return EditorGUILayout.FloatField(label, (float)value);
            if (fieldType == typeof(bool))
                return EditorGUILayout.Toggle(label, (bool)value);
            if (fieldType == typeof(string))
                return EditorGUILayout.TextField(label, (string)value);
            if (fieldType == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, (Vector3)value);
            if (fieldType == typeof(float3))
                return (float3)(EditorGUILayout.Vector3Field(label, (float3)value));
            if (fieldType == typeof(Vector2))
                return EditorGUILayout.Vector2Field(label, (Vector2)value);
            if (fieldType == typeof(float2))
                return (float2)EditorGUILayout.Vector2Field(label, (float2)value);
            if (fieldType == typeof(Quaternion))
            {
                EditorGUILayout.Vector3Field(label, ((Quaternion)value).eulerAngles);
                return value;
            }
            if (fieldType == typeof(quaternion))
            {
                EditorGUILayout.Vector3Field(label, math.Euler((quaternion)value));
                return value;
            }

            if (fieldType == typeof(Entity))
            {
                GUI.enabled = false;
                EditorGUILayout.IntField(label, ((Entity)value).id);
                GUI.enabled = true;
                return value;
            }
            // // Для вложенных структур рекурсивно
            // if (fieldType.IsValueType && !fieldType.IsPrimitive)
            // {
            //     EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            //     var fields = fieldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            //     foreach (var subField in fields)
            //     {
            //         object subValue = subField.GetValue(value);
            //         object newSubValue = DrawField(subField.Name, subField.FieldType, subValue);
            //         subField.SetValueDirect(__makeref(value), newSubValue);
            //     }
            //     return value;
            // }
            // ObjectRef<T>
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(ObjectRef<>))
            {
                var innerType = fieldType.GetGenericArguments()[0];
                var innerValue = fieldType.GetProperty("Value").GetValue(value);
                object newInnerValue = DrawField(label, innerType, innerValue);
                if (!Equals(innerValue, newInnerValue))
                {
                    fieldType.GetProperty("Value").SetValue(value, newInnerValue);
                }
                return value;
            }
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(ComponentArray<>))
            {
                var innerType = fieldType.GetGenericArguments()[0];
                var innerValue = fieldType.GetProperty("Value").GetValue(value);
                object newInnerValue = DrawField(label, innerType, innerValue);
                if (!Equals(innerValue, newInnerValue))
                {
                    fieldType.GetProperty("Value").SetValue(value, newInnerValue);
                }
                return value;
            }
            EditorGUILayout.LabelField($"{label} ({fieldType.Name}) — unsupported");
            return value;
        }
    }
}
#endif