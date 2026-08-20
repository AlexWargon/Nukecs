using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Wargon.Nukecs.Collections;
// ReSharper disable InconsistentNaming
// ReSharper disable StaticMemberInGenericType

namespace Wargon.Nukecs
{
    public unsafe struct ResStorage
    {
        private MemoryList<ptr> _resources;

        public ResStorage(ref MemAllocator allocator)
        {
            _resources = new MemoryList<ptr>(32, ref allocator);
        }

        public void OnDeserialize(ref MemAllocator allocator)
        {
            _resources.OnDeserialize(ref allocator);
        }
        internal (int len, IRes[]) GetAll(IRes[] cache)
        {
            var count = 0;
            if (_resources.length >= cache.Length)
                Array.Resize(ref cache, _resources.length + 1);
            foreach (var type in res_type.RegisteredTypes)
            {
                var data = res_type.data(type);
                if (data.index < _resources.length)
                {
                    var resPtr = _resources.Ptr[data.index];
                    var boxed = data.getBoxed(resPtr.cached);
                    cache[count++] = boxed;
                }
            }

            return (count, cache);
        }

        internal ptr<T> GetRes<T>() where T : unmanaged
        {
            var index = res_type<T>.index;
            return _resources.Ptr[index].AsTyped<T>();
        }

        public IRes GetRes(Type type)
        {
            var data = res_type.data(type);
            var res = _resources.Ptr[data.index];
            return data.getBoxed(res.cached);
        }

        public void SetRes(IRes res)
        {
            var data = res_type.data(res.GetType());
            var resPtr = _resources.Ptr[data.index];
            data.setBoxed(resPtr.cached, res);
        }

        internal bool HasRes<T>() where T : unmanaged
        {
            var index = res_type<T>.index;
            if (index >= _resources.length) return false;
            return !_resources[res_type<T>.index].IsNull;
        }

        internal bool AddRes<T>(in T resource, World.WorldUnsafe* world) where T : unmanaged
        {
            if (HasRes<T>()) return false;
            var ptr = world->_allocate_ptr<T>();
            res_type<T>.index = _resources.length;
            res_type.set<T>(res_type<T>.index);
            ptr.Ref = resource;
            _resources.Add(ptr.UntypedPointer, ref world->AllocatorRef);
            return true;
        }
    }
    
    
    internal class ReflectionData
    {
        internal GetBoxDelegate getBoxed;
        internal int index;
        internal SetBoxDelegate setBoxed;

        internal ReflectionData(int index)
        {
            this.index = index;
        }

        internal static unsafe IRes GetRes<T>(byte* ptr)
            where T : struct
        {
            ref var wrapper = ref Unsafe.AsRef<T>(ptr);
            return ((IResourceGetSet)wrapper).GetResource();
        }

        internal static unsafe void SetRes<T>(byte* ptr, IRes val)
            where T : struct
        {
            ref var wrapper = ref Unsafe.AsRef<T>(ptr);
            var boxed = (IResourceGetSet)wrapper;
            boxed.SetResource(val);
            wrapper = (T)boxed;
        }
    }

    // ReSharper disable once UnusedTypeParameter
    internal struct res_type<T>
    {
        internal static int index = -1;
    }

    internal struct res_type
    {
        private static readonly Dictionary<Type, ReflectionData> indexes = new();
        internal static IEnumerable<Type> RegisteredTypes => indexes.Keys;

        internal static ReflectionData data(Type type)
        {
            return indexes[type];
        }

        internal static int index(Type type)
        {
            return indexes[type].index;
        }

        internal static unsafe void set<T>(int index) where T : struct
        {
            var data = new ReflectionData(index)
            {
                getBoxed = ReflectionData.GetRes<T>,
                setBoxed = ReflectionData.SetRes<T>
            };
            indexes[typeof(T)] = data;
        }
    }

    internal unsafe delegate void SetBoxDelegate(byte* ptr, IRes val);

    internal unsafe delegate IRes GetBoxDelegate(byte* ptr);
}