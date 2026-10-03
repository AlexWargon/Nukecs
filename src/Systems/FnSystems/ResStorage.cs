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
            foreach (ref var resource in _resources)
                if (!resource.IsNull) resource.OnDeserialize(ref allocator);
        }

        internal ptr<TParam> GetLocal<TParam>(int slot, World.WorldUnsafe* world)
            where TParam : unmanaged, ISystemParam
        {
            if (slot < 0 || default(TParam).MetaType != SystemParamMetaType.Local)
                throw new ArgumentException("A local slot requires a Local system parameter.");
            while (_resources.length <= slot)
                _resources.Add(default, ref world->AllocatorRef);
            if (!_resources.Ptr[slot].IsNull)
                return _resources.Ptr[slot].AsTyped<TParam>();
            var value = world->AllocatorRef.AllocatePtr<TParam>();
            value.Ref = default;
            _resources.Ptr[slot] = value.UntypedPointer;
            value.Ref.Init(ref world->selfPtr);
            return value;
        }
        internal (int len, IRes[]) GetAll(IRes[] cache)
        {
            var count = 0;
            if (_resources.length >= cache.Length)
                Array.Resize(ref cache, _resources.length + 1);
            foreach (var type in res_type.RegisteredTypes)
            {
                var data = res_type.data(type);
                if (data.index < 0 || data.index >= _resources.length) continue;
                var resPtr = _resources.Ptr[data.index];
                if (resPtr.IsNull) continue; // this world never added that resource
                var boxed = data.getBoxed(resPtr.cached);
                cache[count++] = boxed;
            }

            return (count, cache);
        }

        internal ptr<T> GetRes<T>() where T : unmanaged
        {
            var index = res_type<T>.index;
            if (index < 0 || index >= _resources.length)
                throw new InvalidOperationException($"[Nukecs] World does not have resource {typeof(T).Name}");
            return _resources.Ptr[index].AsTyped<T>();
        }

        public IRes GetRes(Type type)
        {
            var data = res_type.data(type);
            if (data.index < 0 || data.index >= _resources.length)
                throw new InvalidOperationException($"[Nukecs] World does not have resource {type.Name}");
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
            // index < 0 = type never registered in this domain; >= length or null = this world
            // never added it (slot ids are global, per-world lists are padded)
            if (index < 0 || index >= _resources.length) return false;
            return !_resources[index].IsNull;
        }

        internal bool AddRes<T>(in T resource, World.WorldUnsafe* world) where T : unmanaged
        {
            if (HasRes<T>()) return false;
            var ptr = world->_allocate_ptr<T>(1, AllocatorTags.WorldMisc);
            // slot id is GLOBAL (first registration wins). Deriving it from this world's list
            // length made world B's first resource alias world A's slot 0 — cross-world
            // type-confused reads.
            var slot = res_type.AcquireSlot<T>();
            // pad this world's list so the global slot exists here too (null ptr = absent)
            while (_resources.length <= slot)
                _resources.Add(default, ref world->AllocatorRef);
            ptr.Ref = resource;
            _resources.Ptr[slot] = ptr.UntypedPointer;
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
        // grows monotonically per domain — resource slot ids are globally stable so that
        // several worlds can index their own per-world resource lists with the same ids

        internal static ReflectionData data(Type type)
        {
            return indexes[type];
        }

        internal static int index(Type type)
        {
            return indexes[type].index;
        }

        /// <summary>Returns the globally stable slot id for T, registering it on first use.</summary>
        internal static int AcquireSlot<T>() where T : struct
        {
            if (res_type<T>.index >= 0) return res_type<T>.index;
            res_type<T>.index = ResourceSlotIds.Acquire();
            set<T>(res_type<T>.index); // also registers the boxing delegates
            return res_type<T>.index;
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
