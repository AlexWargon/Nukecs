using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reflect
{
    public unsafe struct _type
    {
        public int len;
        public fixed int offsets[32];
        public fixed short types[32];
        public Type[] fieldTypes;
        public _type[] subTypes;
        public int[] fieldSizes;
        public static _type Create(Type type)
        {
            var flds = type.GetFields();
            var len = flds.Length;
            var idx = 0;
            _type t = default;
            t.fieldTypes = new Type[len];
            t.subTypes = new _type[len];
            t.fieldSizes = new int[len];
            foreach (var fieldInfo in flds)
            {
                var ft = fieldInfo.FieldType;
                t.offsets[idx] = UnsafeUtility.GetFieldOffset(fieldInfo);
                t.types[idx] = (short)getFieldType(ft);
                t.fieldTypes[idx] = ft;
                if (ft.IsValueType && !ft.IsEnum && !ft.IsPrimitive && ft != typeof(Entity))
                    t.subTypes[idx] = Create(ft);
                t.fieldSizes[idx] = ft.IsValueType
                    ? System.Runtime.InteropServices.Marshal.SizeOf(ft)
                    : IntPtr.Size;
                idx++;
            }

            t.len = len;
            return t;
        }

        private static _field_type getFieldType(Type type)
        {
            if (type == typeof(bool))
                return _field_type.Bool;
            if (type == typeof(byte))
                return _field_type.Byte;
            if (type == typeof(short))
                return _field_type.Short;
            if (type == typeof(int))
                return _field_type.Int;
            if (type == typeof(long))
                return _field_type.Long;
            if (type == typeof(float))
                return _field_type.Float;
            if (type == typeof(double))
                return _field_type.Double;
            if (type == typeof(Entity))
                return _field_type.Entity;

            if (type.IsClass)
            {
                if (type.IsSubclassOf(typeof(UnityEngine.Object)))
                {
                    return _field_type.UnityObject;
                }
                return _field_type.Object;
            }
            if (type.IsValueType && !type.IsEnum)
                return _field_type.Struct;
            throw new Exception("Unknown field type: " + type.Name);
        }
        public void SetFieldObj(int field, void* instance, object value)
        {
            var ptr = (byte*)instance + offsets[field];
            var type = (_field_type)types[field];
            switch (type)
            {
                case _field_type.Bool:
                    Unsafe.Write(ptr, Unsafe.Unbox<bool>(value));break;
                case _field_type.Byte:
                    Unsafe.Write(ptr, Unsafe.Unbox<byte>(value));break;
                case _field_type.Short:
                    Unsafe.Write(ptr, Unsafe.Unbox<short>(value));break;
                case _field_type.Int:
                    Unsafe.Write(ptr, Unsafe.Unbox<int>(value));break;
                case _field_type.Long:
                    Unsafe.Write(ptr, Unsafe.Unbox<long>(value));break;
                case _field_type.Float:
                    Unsafe.Write(ptr, Unsafe.Unbox<float>(value));break;
                case _field_type.Double:
                    Unsafe.Write(ptr, Unsafe.Unbox<double>(value));break;
                case _field_type.Entity:
                    Unsafe.Write(ptr, Unsafe.Unbox<Entity>(value));break;
                case _field_type.Object:
                    Unsafe.Write(ptr, value);break;
                case _field_type.UnityObject:
                    Unsafe.Write(ptr, (UnityEngine.Object)value);break;
                case _field_type.ObjectRef_Object:
                    ref var t = ref Unsafe.AsRef<ObjectRef<object>>(ptr);
                    t.Value = value;break;
                case _field_type.ObjectRef_UnityObject:
                    ref var ut = ref Unsafe.AsRef<ObjectRef<UnityEngine.Object>>(ptr);
                    ut.Value = (UnityEngine.Object)value;break;
                case _field_type.Struct:
                    ref var src = ref Unsafe.As<object, byte>(ref value);
                    Unsafe.CopyBlock(ref *ptr, ref src, (uint)fieldSizes[field]);
                    break;
            }
        }
        public void SetField<T>(int field, void* instance, T value)
        {
            var ptr = (byte*)instance + offsets[field];
            Unsafe.Write<T>(ptr, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetValue<T>(byte* ptr, T value)
        {
            Unsafe.Write<T>(ptr, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetValue(byte* ptr, object value)
        {
            Unsafe.Write(ptr, value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object GetValue<T>(byte* ptr) where T : unmanaged
        {
            return Unsafe.AsRef<T>(ptr);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetField<T>(int field, void* instance)
        {
            return ref Unsafe.AsRef<T>((byte*)instance + offsets[field]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetFieldBoxed(int field, void* instance)
        {
            var ptr = (byte*)instance + offsets[field];
            return Unsafe.Read<object>(ptr);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetField(int field, void* instance)
        {
            var ptr = (byte*)instance + offsets[field];
            var type = (_field_type)types[field];
            switch (type)
            {
                case _field_type.Bool:
                    return Unsafe.AsRef<bool>(ptr);
                case _field_type.Byte:
                    return Unsafe.AsRef<byte>(ptr);
                case _field_type.Short:
                    return Unsafe.AsRef<short>(ptr);
                case _field_type.Int:
                    return Unsafe.AsRef<int>(ptr);
                case _field_type.Long:
                    return Unsafe.AsRef<long>(ptr);
                case _field_type.Float:
                    return Unsafe.AsRef<float>(ptr);
                case _field_type.Double:
                    return Unsafe.AsRef<double>(ptr);
                case _field_type.Entity:
                    return Unsafe.AsRef<Entity>(ptr);
                case _field_type.Object:
                    return Unsafe.AsRef<object>(ptr);
                case _field_type.UnityObject:
                    return Unsafe.Read<UnityEngine.Object>(ptr);
                case _field_type.ObjectRef_Object:
                    ref var t = ref Unsafe.AsRef<ObjectRef<object>>(ptr);
                    return t.Value;
                case _field_type.ObjectRef_UnityObject:
                    ref var ut = ref Unsafe.AsRef<ObjectRef<UnityEngine.Object>>(ptr);
                    return ut.Value;
                case _field_type.Struct:
                    var boxed = RuntimeHelpers.GetUninitializedObject(fieldTypes[field]);
                    ref var dest = ref Unsafe.As<object, byte>(ref boxed);
                    Unsafe.CopyBlock(ref dest, ref *ptr, (uint)fieldSizes[field]);
                    return boxed;
            }

            return null;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public _object GetField_object(int field, void* instance)
        {
            var ptr = (byte*)instance + offsets[field];
            var type = (_field_type)types[field];
            switch (type)
            {
                case _field_type.Bool:
                    return new _object() { bool_val = *(bool*)ptr, field_type = type};
                case _field_type.Byte:
                    return new _object() { byte_val = *ptr};
                case _field_type.Short:
                    return new _object() { short_val = *(short*)ptr, field_type = type};
                case _field_type.Int:
                    return new _object() { int_val = *(int*)ptr, field_type = type};
                case _field_type.Long:
                    return new _object() { long_val = *(long*)ptr, field_type = type};
                case _field_type.Float:
                    return new _object() { float_val = *(float*)ptr, field_type = type};
                case _field_type.Double:
                    return new _object() { double_val = Unsafe.Read<double>(ptr), field_type = type};
                case _field_type.Entity:
                    return new _object() { entity_val = Unsafe.Read<Entity>(ptr), field_type = type};
                case _field_type.Object:
                    return new _object() { object_val = Unsafe.Read<object>(ptr), field_type = type};
                case _field_type.UnityObject:
                    return new _object() { object_val = Unsafe.Read<UnityEngine.Object>(ptr), field_type = type};
                case _field_type.ObjectRef_Object:
                    ref var t = ref Unsafe.AsRef<ObjectRef<object>>(ptr); 
                    return new _object() { object_val = t.Value, field_type = type};
                case _field_type.ObjectRef_UnityObject:
                    ref var ut = ref Unsafe.AsRef<ObjectRef<UnityEngine.Object>>(ptr);
                    return new _object() { object_val = ut.Value, field_type = type};
                case _field_type.Struct:
                    return new _object { object_val = GetField(field, instance), field_type = type };
            }

            return default;
        }

        public void SetField(int field, void* instance, _object value)
        {
            var ptr = (byte*)instance + offsets[field];
            var type = (_field_type)types[field];
            switch (type)
            {
                case _field_type.Bool:   *(bool*)ptr = value.bool_val; break;
                case _field_type.Byte:   *ptr = value.byte_val; break;
                case _field_type.Short:  *(short*)ptr = value.short_val; break;
                case _field_type.Int:    *(int*)ptr = value.int_val; break;
                case _field_type.Long:   *(long*)ptr = value.long_val; break;
                case _field_type.Float:  *(float*)ptr = value.float_val; break;
                case _field_type.Double: *(double*)ptr = value.double_val; break;
                case _field_type.Entity: *(Entity*)ptr = value.entity_val; break;
                case _field_type.Object:
                case _field_type.UnityObject:
                    Unsafe.Write(ptr, value.object_val);
                    break;
                case _field_type.ObjectRef_Object:
                    Unsafe.AsRef<ObjectRef<object>>(ptr).Value = value.object_val;
                    break;
                case _field_type.ObjectRef_UnityObject:
                    Unsafe.AsRef<ObjectRef<UnityEngine.Object>>(ptr).Value = (UnityEngine.Object)value.object_val;
                    break;
                case _field_type.Struct:
                    ref var src = ref Unsafe.As<object, byte>(ref value.object_val);
                    Unsafe.CopyBlock(ref *ptr, ref src, (uint)fieldSizes[field]);
                    break;
            }
        }

        public ref T GetSubField<T>(int field, int subField, void* instance)
        {
            var basePtr = (byte*)instance + offsets[field];
            var sub = subTypes[field];
            return ref sub.GetField<T>(subField, basePtr);
        }

        public _type GetSubType(int field) => subTypes[field];

        public void* GetStructPtr(int field, void* instance)
            => (byte*)instance + offsets[field];

        public object GetClassField(int field, object classInstance)
        {
            var subFieldType = fieldTypes[field];
            var fi = classInstance.GetType().GetField(
                subFieldType.Name, BindingFlags.Instance | BindingFlags.Public);
            return fi?.GetValue(classInstance);
        }
    }
}