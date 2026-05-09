using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Nukecs.Collections;

// ReSharper disable InconsistentNaming
// ReSharper disable StaticMemberInGenericType

namespace Wargon.Nukecs
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Res<TRes> : ISystemParam, IResourceGetSet where TRes : struct, IResource
    {
        private TRes _reference;
        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;
        public readonly TRes Val => _reference;

        void IResourceGetSet.SetResource(IResource resource)
        {
            _reference = (TRes)resource;
        }

        IResource IResourceGetSet.GetResource()
        {
            return _reference;
        }

        internal void Set(IResource resource)
        {
            _reference = (TRes)resource;
        }

        public Res(in TRes resource)
        {
            _reference = resource;
        }

        public static implicit operator TRes(in Res<TRes> res)
        {
            return res._reference;
        }

        public static explicit operator Res<TRes>(in TRes res)
        {
            return new Res<TRes>(in res);
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _reference.Init(ref world.Ref.ManagedWorld.Ref);
        }

        public void Update(ref World world, IntPtr data)
        {
            _reference.Update(ref world);
        }

        public IntPtr GetData()
        {
            return IntPtr.Zero;
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = default;
            return false;
        }
    }

    internal class ReflectionData
    {
        internal GetBoxDelegate get_boxed;
        internal int index;
        internal SetBoxDelegate set_boxed;

        internal ReflectionData(int index)
        {
            this.index = index;
        }

        internal static unsafe IResource GetRes<T>(byte* ptr)
            where T : struct
        {
            ref var wrapper = ref Unsafe.AsRef<T>(ptr);
            return ((IResourceGetSet)wrapper).GetResource();
        }

        internal static unsafe void SetRes<T>(byte* ptr, IResource val)
            where T : struct
        {
            ref var wrapper = ref Unsafe.AsRef<T>(ptr);
            var boxed = (IResourceGetSet)wrapper;
            boxed.SetResource(val);
            wrapper = (T)boxed;
        }
    }

    // ReSharper disable once UnusedTypeParameter
    internal struct param_type<T>
    {
        public static int index = -1;
    }

    internal struct param_type
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
                get_boxed = ReflectionData.GetRes<T>,
                set_boxed = ReflectionData.SetRes<T>
            };
            indexes[typeof(T)] = data;
        }
    }

    internal unsafe delegate void SetBoxDelegate(byte* ptr, IResource val);

    internal unsafe delegate IResource GetBoxDelegate(byte* ptr);

    public unsafe struct ResStorage
    {
        private MemoryList<ptr> _resources;

        public ResStorage(ref MemAllocator allocator)
        {
            _resources = new MemoryList<ptr>(32, ref allocator);
        }

        internal (int len, IResource[]) GetAll(IResource[] cache)
        {
            var count = 0;
            if (_resources.length >= cache.Length)
                Array.Resize(ref cache, _resources.length + 1);
            foreach (var type in param_type.RegisteredTypes)
            {
                var data = param_type.data(type);
                if (data.index < _resources.length)
                {
                    var resPtr = _resources.Ptr[data.index];
                    var boxed = data.get_boxed(resPtr.cached);
                    cache[count++] = boxed;
                }
            }

            return (count, cache);
        }

        internal ptr<T> Get<T>() where T : unmanaged
        {
            var index = param_type<T>.index;
            return _resources.Ptr[index].AsTyped<T>();
        }

        public IResource Get(Type type)
        {
            var data = param_type.data(type);
            var res = _resources.Ptr[data.index];
            return data.get_boxed(res.cached);
        }

        public void Set(IResource resource)
        {
            var data = param_type.data(resource.GetType());
            var res = _resources.Ptr[data.index];
            data.set_boxed(res.cached, resource);
        }

        internal bool Has<T>() where T : unmanaged
        {
            var index = param_type<T>.index;
            if (index >= _resources.length) return false;
            return !_resources[param_type<T>.index].IsNull;
        }

        internal bool Add<T>(in T resource, World.WorldUnsafe* world) where T : unmanaged
        {
            if (Has<T>()) return false;
            var ptr = world->_allocate_ptr<T>();
            param_type<T>.index = _resources.length;
            param_type.set<T>(param_type<T>.index);
            ptr.Ref = resource;
            _resources.Add(ptr.UntypedPointer, ref world->AllocatorRef);
            return true;
        }
    }
}