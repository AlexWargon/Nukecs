#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public enum FieldAccessorKind
    {
        Number,
        Bool,
        String,
        EntityRef,
        Enum,
        ObjectRef,
        ComponentArray
    }

    public struct FieldAccessorEntry
    {
        public string Name;
        public FieldAccessorKind ValueType;
        public int ByteOffset;
        public Type FieldType;
        public bool IsManaged;
        public string[] EnumNames;
        public Type EnumUnderlyingType;
        public Type GenericArgType;
    }

    public struct TypeAccessor
    {
        public int TypeIndex;
        public Type ComponentType;
        public int Size;
        public bool IsTag;
        public bool HasManagedFields;
        public FieldAccessorEntry[] Fields;
    }

    public static unsafe class ComponentFieldAccessorCache
    {
        private static readonly Dictionary<int, TypeAccessor> _cache = new ();

        public static TypeAccessor GetOrCreate(int typeIndex)
        {
            if (_cache.TryGetValue(typeIndex, out var accessor))
                return accessor;

            var ctData = ComponentTypeMap.GetComponentType(typeIndex);
            var t = ComponentTypeMap.GetType(typeIndex);
            if (t == null)
            {
                accessor = new TypeAccessor
                {
                    TypeIndex = typeIndex,
                    ComponentType = null,
                    Size = ctData.size,
                    IsTag = ctData.isTag,
                    Fields = Array.Empty<FieldAccessorEntry>()
                };
                _cache[typeIndex] = accessor;
                return accessor;
            }

            var fields = new List<FieldAccessorEntry>();
            var hasManaged = false;
            BuildFieldAccessors(t, string.Empty, 0, fields, ref hasManaged);

            accessor = new TypeAccessor
            {
                TypeIndex = typeIndex,
                ComponentType = t,
                Size = ctData.size,
                IsTag = ctData.isTag,
                HasManagedFields = hasManaged,
                Fields = fields.ToArray()
            };
            _cache[typeIndex] = accessor;
            return accessor;
        }

        public static void ReadFieldsPointer(int typeIndex, byte* ptr, List<(string Key, FieldValue Value)> fields)
        {
            var accessor = GetOrCreate(typeIndex);
            if (accessor.IsTag)
            {
                fields.Add(("#tag", FieldValue.FromBool(true)));
                return;
            }

            if (accessor.Fields == null || accessor.Fields.Length == 0) return;

            if (accessor.HasManagedFields)
            {
                var boxed = ReadBoxedFromPointer(ptr, accessor);
                ComponentFieldReader.ReadFields(boxed, fields);
                return;
            }

            for (int i = 0; i < accessor.Fields.Length; i++)
            {
                ref var fa = ref accessor.Fields[i];
                var fieldPtr = ptr + fa.ByteOffset;
                switch (fa.ValueType)
                {
                    case FieldAccessorKind.Number:
                        fields.Add((fa.Name, ReadNumber(fieldPtr, fa.FieldType)));
                        break;
                    case FieldAccessorKind.Bool:
                        fields.Add((fa.Name, FieldValue.FromBool(*(bool*)fieldPtr)));
                        break;
                    case FieldAccessorKind.String:
                        fields.Add((fa.Name, FieldValue.FromString("")));
                        break;
                    case FieldAccessorKind.EntityRef:
                        fields.Add((fa.Name, FieldValue.FromEntityRef(*(int*)fieldPtr)));
                        break;
                }
            }
        }

        public static void WriteFieldPointer(int typeIndex, byte* ptr, string fieldKey, FieldValue value)
        {
            var accessor = GetOrCreate(typeIndex);

            if (accessor.HasManagedFields)
            {
                var boxed = ReadBoxedFromPointer(ptr, accessor);
                ComponentFieldWriter.WriteField(boxed, fieldKey, value);
                WriteBoxedToPointer(ptr, boxed, accessor.Size);
                return;
            }

            if (accessor.Fields == null) return;

            for (int i = 0; i < accessor.Fields.Length; i++)
            {
                ref var fa = ref accessor.Fields[i];
                if (fa.Name != fieldKey) continue;

                var fieldPtr = ptr + fa.ByteOffset;
                switch (fa.ValueType)
                {
                    case FieldAccessorKind.Number:
                        WriteNumber(fieldPtr, fa.FieldType, value.NumberVal);
                        break;
                    case FieldAccessorKind.Bool:
                        *(bool*)fieldPtr = value.BoolVal;
                        break;
                    case FieldAccessorKind.EntityRef:
                        *(int*)fieldPtr = value.EntityRefVal;
                        break;
                }
                return;
            }
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static object ReadBoxedFromPointer(byte* ptr, TypeAccessor accessor)
        {
            if (accessor.ComponentType == null) return null;
            var boxed = RuntimeHelpers.GetUninitializedObject(accessor.ComponentType);
            ref var dest = ref Unsafe.As<object, byte>(ref boxed);
            Unsafe.CopyBlock(ref dest, ref *ptr, (uint)accessor.Size);
            return boxed;
        }

        private static void WriteBoxedToPointer(byte* ptr, object boxed, int size)
        {
            ref var src = ref Unsafe.As<object, byte>(ref boxed);
            Unsafe.CopyBlock(ref *ptr, ref src, (uint)size);
        }

        private static void BuildFieldAccessors(Type compType, string prefix, int baseOffset, List<FieldAccessorEntry> fields, ref bool hasManaged)
        {
            var flds = compType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var fi in flds)
            {
                var ft = fi.FieldType;
                var offset = baseOffset + GetFieldOffset(fi);
                var fullName = string.IsNullOrEmpty(prefix) ? fi.Name : $"{prefix}.{fi.Name}";

                if (ft == typeof(float) || ft == typeof(double) || ft == typeof(int) || ft == typeof(long)
                    || ft == typeof(uint) || ft == typeof(short) || ft == typeof(byte)
                    || ft == typeof(ushort) || ft == typeof(sbyte))
                {
                    fields.Add(new FieldAccessorEntry
                    {
                        Name = fullName,
                        ValueType = FieldAccessorKind.Number,
                        ByteOffset = offset,
                        FieldType = ft,
                        IsManaged = false
                    });
                }
                else if (ft.IsEnum)
                {
                    hasManaged = true;
                    fields.Add(new FieldAccessorEntry
                    {
                        Name = fullName,
                        ValueType = FieldAccessorKind.Enum,
                        ByteOffset = offset,
                        FieldType = ft,
                        IsManaged = true,
                        EnumNames = Enum.GetNames(ft),
                        EnumUnderlyingType = Enum.GetUnderlyingType(ft)
                    });
                }
                else if (ft == typeof(bool))
                {
                    fields.Add(new FieldAccessorEntry
                    {
                        Name = fullName,
                        ValueType = FieldAccessorKind.Bool,
                        ByteOffset = offset,
                        FieldType = ft,
                        IsManaged = false
                    });
                }
                else if (ft == typeof(string))
                {
                    hasManaged = true;
                    fields.Add(new FieldAccessorEntry
                    {
                        Name = fullName,
                        ValueType = FieldAccessorKind.String,
                        ByteOffset = offset,
                        FieldType = ft,
                        IsManaged = true
                    });
                }
                else if (ft == typeof(Entity))
                {
                    fields.Add(new FieldAccessorEntry
                    {
                        Name = fullName,
                        ValueType = FieldAccessorKind.EntityRef,
                        ByteOffset = offset,
                        FieldType = ft,
                        IsManaged = false
                    });
                }
                else if (ft == typeof(Vector2) || ft.Name == "float2")
                {
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.x", ValueType = FieldAccessorKind.Number, ByteOffset = offset, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.y", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 4, FieldType = typeof(float), IsManaged = false });
                }
                else if (ft == typeof(Vector3) || ft.Name == "float3")
                {
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.x", ValueType = FieldAccessorKind.Number, ByteOffset = offset, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.y", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 4, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.z", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 8, FieldType = typeof(float), IsManaged = false });
                }
                else if (ft == typeof(Vector4) || ft.Name == "float4")
                {
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.x", ValueType = FieldAccessorKind.Number, ByteOffset = offset, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.y", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 4, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.z", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 8, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.w", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 12, FieldType = typeof(float), IsManaged = false });
                }
                else if (ft == typeof(Quaternion) || ft.Name == "quaternion")
                {
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.x", ValueType = FieldAccessorKind.Number, ByteOffset = offset, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.y", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 4, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.z", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 8, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.w", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 12, FieldType = typeof(float), IsManaged = false });
                }
                else if (ft == typeof(Color))
                {
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.r", ValueType = FieldAccessorKind.Number, ByteOffset = offset, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.g", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 4, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.b", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 8, FieldType = typeof(float), IsManaged = false });
                    fields.Add(new FieldAccessorEntry { Name = $"{fullName}.a", ValueType = FieldAccessorKind.Number, ByteOffset = offset + 12, FieldType = typeof(float), IsManaged = false });
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(ft))
                {
                    hasManaged = true;
                    fields.Add(new FieldAccessorEntry
                    {
                        Name = fullName,
                        ValueType = FieldAccessorKind.ObjectRef,
                        ByteOffset = offset,
                        FieldType = ft,
                        IsManaged = true,
                        GenericArgType = ft
                    });
                }
                else if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(ObjectRef<>))
                {
                    hasManaged = true;
                    fields.Add(new FieldAccessorEntry
                    {
                        Name = fullName,
                        ValueType = FieldAccessorKind.ObjectRef,
                        ByteOffset = offset,
                        FieldType = ft,
                        IsManaged = true,
                        GenericArgType = ft.GetGenericArguments()[0]
                    });
                }
                else if (typeof(IComponent).IsAssignableFrom(ft) && ft.IsValueType)
                {
                    BuildFieldAccessors(ft, fullName, offset, fields, ref hasManaged);
                }
            }
        }

        private static int GetFieldOffset(FieldInfo fi)
        {
            try
            {
                var dm = new DynamicMethod("GetOffset_" + fi.Name, typeof(int), new[] { typeof(object) }, true);
                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, fi);
                il.Emit(OpCodes.Conv_I);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Conv_I);
                il.Emit(OpCodes.Sub);
                il.Emit(OpCodes.Ret);
                var func = (Func<object, int>)dm.CreateDelegate(typeof(Func<object, int>));
                var dummy = RuntimeHelpers.GetUninitializedObject(fi.DeclaringType);
                return func(dummy);
            }
            catch
            {
                try
                {
                    return (int)Marshal.OffsetOf(fi.DeclaringType, fi.Name);
                }
                catch
                {
                    return 0;
                }
            }
        }

        internal static FieldValue ReadNumber(byte* ptr, Type ft)
        {
            if (ft == typeof(float)) return FieldValue.FromNumber(*(float*)ptr);
            if (ft == typeof(double)) return FieldValue.FromNumber(*(double*)ptr);
            if (ft == typeof(int)) return FieldValue.FromNumber(*(int*)ptr);
            if (ft == typeof(long)) return FieldValue.FromNumber(*(long*)ptr);
            if (ft == typeof(uint)) return FieldValue.FromNumber(*(uint*)ptr);
            if (ft == typeof(short)) return FieldValue.FromNumber(*(short*)ptr);
            if (ft == typeof(byte)) return FieldValue.FromNumber(*(byte*)ptr);
            if (ft == typeof(ushort)) return FieldValue.FromNumber(*(ushort*)ptr);
            if (ft == typeof(sbyte)) return FieldValue.FromNumber(*(sbyte*)ptr);
            return FieldValue.FromNumber(0);
        }

        internal static void WriteNumber(byte* ptr, Type ft, double val)
        {
            if (ft == typeof(float)) { *(float*)ptr = (float)val; return; }
            if (ft == typeof(double)) { *(double*)ptr = val; return; }
            if (ft == typeof(int)) { *(int*)ptr = (int)val; return; }
            if (ft == typeof(long)) { *(long*)ptr = (long)val; return; }
            if (ft == typeof(uint)) { *(uint*)ptr = (uint)val; return; }
            if (ft == typeof(short)) { *(short*)ptr = (short)val; return; }
            if (ft == typeof(byte)) { *(byte*)ptr = (byte)val; return; }
            if (ft == typeof(ushort)) { *(ushort*)ptr = (ushort)val; return; }
            if (ft == typeof(sbyte)) { *(sbyte*)ptr = (sbyte)val; return; }
        }
    }
}
#endif
