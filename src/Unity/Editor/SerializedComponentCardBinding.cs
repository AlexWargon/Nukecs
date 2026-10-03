#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wargon.Nukecs.Editor.EcsDebugV2;

namespace Wargon.Nukecs.Editor
{
    /// <summary>Serialized authoring data adapter. All UI is built by ComponentCardDrawer.</summary>
    public sealed class SerializedComponentCardBinding : IComponentCardBinding
    {
        private readonly SerializedObject _owner;
        private readonly string _listPath;
        private readonly int _index;
        private readonly Action _refresh;
        private readonly Dictionary<string, long> _changes = new();
        private Type _componentType;

        public SerializedComponentCardBinding(SerializedObject owner, string listPath, int index, Action refresh)
        {
            _owner = owner;
            _listPath = listPath;
            _index = index;
            _refresh = refresh;
        }

        private SerializedProperty Component => _owner.FindProperty(_listPath).GetArrayElementAtIndex(_index);

        public ComponentInfo Read()
        {
            var component = Component;
            var instance = component.managedReferenceValue;
            _componentType = instance?.GetType();
            var info = new ComponentInfo { Name = _componentType?.Name ?? "Missing component" };
            if (_componentType?.IsValueType == true) {
                try { info.ByteSize = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf(_componentType); }
                catch (ArgumentException) { }
            }
            ReadChildren(component, "", info);
            if (info.Fields.Count == 0) info.Fields.Add(("#tag", FieldValue.FromString("#tag")));
            return info;
        }

        private void ReadChildren(SerializedProperty parent, string prefix, ComponentInfo info)
        {
            var child = parent.Copy();
            var end = parent.GetEndProperty();
            if (!child.NextVisible(true)) return;
            do {
                if (SerializedProperty.EqualContents(child, end)) break;
                var key = prefix + child.name;
                if (child.isArray && child.propertyType != SerializedPropertyType.String) {
                    info.Fields.Add((key, FieldValue.FromComponentArray(child.arrayElementType, child.arraySize)));
                    continue;
                }
                FieldValue value;
                switch (child.propertyType) {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.LayerMask:
                        value = FieldValue.FromNumber(child.longValue); break;
                    case SerializedPropertyType.Float:
                        value = FieldValue.FromNumber(child.doubleValue); break;
                    case SerializedPropertyType.Boolean:
                        value = FieldValue.FromBool(child.boolValue); break;
                    case SerializedPropertyType.String:
                        value = FieldValue.FromString(child.stringValue); break;
                    case SerializedPropertyType.Enum:
                        var names = child.enumNames;
                        var raw = new long[names.Length];
                        var enumType = ResolveFieldType(key);
                        for (var i = 0; i < names.Length; i++)
                            raw[i] = enumType?.IsEnum == true ? EnumValue(Enum.Parse(enumType, names[i])) : i;
                        value = FieldValue.FromEnum(names, raw, child.enumValueIndex, child.intValue);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        var obj = child.objectReferenceValue;
                        value = FieldValue.FromObjectRef(ResolveFieldType(key)?.Name ?? "Object",
                            obj ? obj.name : "null", obj ? obj.GetInstanceID() : 0, true);
                        break;
                    default:
                        if (child.hasVisibleChildren) { ReadChildren(child, key + ".", info); continue; }
                        value = FieldValue.FromObjectRef(child.type, "read-only", 0);
                        break;
                }
                info.Fields.Add((key, value));
            } while (child.NextVisible(false));
        }

        private Type ResolveFieldType(string key)
        {
            var type = _componentType;
            foreach (var name in key.Split('.')) {
                type = type?.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType;
                if (type == null) break;
            }
            return type;
        }

        private static long EnumValue(object value)
        {
            return Enum.GetUnderlyingType(value.GetType()) == typeof(ulong)
                ? unchecked((long)Convert.ToUInt64(value)) : Convert.ToInt64(value);
        }

        public void SetFieldValue(int entityId, string componentName, string fieldKey, FieldValue value)
        {
            _owner.Update();
            var field = Component.FindPropertyRelative(fieldKey);
            if (field == null) return;
            switch (field.propertyType) {
                case SerializedPropertyType.Boolean: field.boolValue = value.BoolVal; break;
                case SerializedPropertyType.String: field.stringValue = value.StringVal; break;
                case SerializedPropertyType.Float: field.doubleValue = value.NumberVal; break;
                case SerializedPropertyType.Enum: field.intValue = unchecked((int)value.EnumRawValue); break;
                case SerializedPropertyType.ObjectReference:
                    field.objectReferenceValue = EditorUtility.InstanceIDToObject(value.ObjectInstanceId); break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask: field.longValue = checked((long)value.NumberVal); break;
                default: return;
            }
            _owner.ApplyModifiedProperties(); // Native Unity Undo and prefab override tracking.
            _changes[$"{entityId}:{componentName}:{fieldKey}"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void RemoveComponent(int entityId, string componentName)
        {
            _owner.Update();
            _owner.FindProperty(_listPath).DeleteArrayElementAtIndex(_index);
            _owner.ApplyModifiedProperties();
            _refresh();
        }

        public bool TryGetChangeTime(string key, out long timestamp) => _changes.TryGetValue(key, out timestamp);
        public void SelectEntity(int entityId) { }
    }
}
#endif
