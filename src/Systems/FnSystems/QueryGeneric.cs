using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs {
    public interface IQuery {
        int Count { get; }
    }
    [BurstCompile(CompileSynchronously = true)]
    public static class QueryGenericExtensions {
        // internal static void FetchRange<TQuery>(this TQuery query, int start, int end)
        //     where TQuery : unmanaged, IQuery {
        //     query.Range = new Range(start, end);
        // }
        

        // public static bool MoveNext<TQuery>(this TQuery query)  where TQuery : unmanaged, IQuery {
        //     var range = query.Range;
        //     return false;
        // }
        [BurstCompile(CompileSynchronously = true)][MethodImpl(inline.YES)]
        public static TQueryEnumerator GetEnumerator<TQueryEnumerator>(this TQueryEnumerator query)
            where TQueryEnumerator :  struct, IQuery
        {
            return query;
        }
    }

    public enum SystemParamMetaType : byte
    {
        None = 0,
        Query = 1,
        World = 2,
        Single = 3,
        Service = 4,
        State = 5,
        Resource = 6,
    }

    public interface ISystemParam {
        SystemParamMetaType MetaType { get; }
        void Init(ref ptr<World.WorldUnsafe> world);
        void Update(ref World world, IntPtr data);
        IntPtr GetData();
        bool TryGetQuery(out ptr<QueryUnsafe> query);
        public Type ParamType => GetType();
    }

    public interface IRanged
    {
        void SetRange(Range range);
        Range GetRange();
    }
    public readonly struct Range {
        public readonly int start;
        public readonly int end;

        public Range(int start, int end)
        {
            this.start = start;
            this.end = end;
        }

        public override string ToString()
        {
            return $"Range:[{start}, {end}]";
        }
    }

    public struct SystemParams 
    {
        private MemoryList<ptr> _pointers;
        private ptr<World.WorldUnsafe> _world;
        private int _count;
        
        public ref TParam Get<TParam>() where TParam : unmanaged, ISystemParam 
        {
            return ref _pointers[SystemParamData<TParam>.Index].AsRef<TParam>();
        }

        public void Add<TParam>(in TParam param) where TParam : unmanaged, ISystemParam 
        {
            var ptr = _world.Ref._allocate_ptr<TParam>();
            SystemParamData<TParam>.Set(new SystemParamData
            {
                metaType = param.MetaType,
                index = _count
            });
            _pointers[_count] = ptr.UntypedPointer;
        }
    }

    public struct SystemParamData<TParam> where TParam : unmanaged, ISystemParam
    {
        private static readonly SharedStatic<SystemParamData> Data = 
            SharedStatic<SystemParamData>.GetOrCreate<SystemParamData<TParam>>();
        public static ref readonly int Index => ref Data.Data.index;
        public static SystemParamMetaType MetaType => Data.Data.metaType;

        public static void Set(SystemParamData data)
        {
            Data.Data = data;
        }
    }

    public struct SystemParamData
    {
        public SystemParamMetaType metaType;
        public int index;
    }
    public struct Serv<TService> where TService : unmanaged, ISystemParam, IService
    {
        private ptr<ServiceStorage> _storage;
        public ref TService Ref => ref _storage.Ref.Get<TService>();
    }

    internal struct ServicesCount
    {
        internal static readonly SharedStatic<int> Count = SharedStatic<int>.GetOrCreate<ServicesCount>();
    }
    internal struct ServiceID<TService> where TService : unmanaged, ISystemParam, IService
    {
        internal static readonly SharedStatic<int> StaticInstance = SharedStatic<int>.GetOrCreate<ServiceID<TService>>();
        
        static ServiceID()
        {
            StaticInstance.Data = ServicesCount.Count.Data++;
        }
    }
    internal struct ServiceStorage
    {
        private UnsafeList<ptr> serviceList;

        internal void Register<TService>(ref ptr<World.WorldUnsafe> world) where TService : unmanaged, ISystemParam, IService
        {
            serviceList[ServiceID<TService>.StaticInstance.Data] =
                world.Ref.AllocatorRef.AllocatePtr<TService>().UntypedPointer;
        }
        internal ref TService Get<TService>() where TService : unmanaged, ISystemParam, IService
        {
            return ref serviceList[ServiceID<TService>.StaticInstance.Data].AsRef<TService>();
        }
    }
     
    public partial struct World : ISystemParam
    {
        public SystemParamMetaType MetaType => SystemParamMetaType.World;
        void ISystemParam.Init(ref ptr<WorldUnsafe> world)
        {
            throw new NotImplementedException();
        }

        void ISystemParam.Update(ref World world, IntPtr data)
        {
            throw new NotImplementedException();
        }

        IntPtr ISystemParam.GetData()
        {
            throw new NotImplementedException();
        }

        bool ISystemParam.TryGetQuery(out ptr<QueryUnsafe> query)
        {
            throw new NotImplementedException();
        }
    }

    public unsafe struct Ptr<T> where T : unmanaged
    {
        internal T* data;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Ptr(T* data) => this.data = data;

        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T*(Ptr<T> ptr) => ptr.data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIter<T1, T2, T3, T4>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
        private static readonly int _type2 = ComponentType<T3>.Index;
        private static readonly int _type3 = ComponentType<T4>.Index;
        private int _archIndex;
        private int _remaining;
        private Tuple _tuple;
        public QueryIter(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple.p0 = null;
            _tuple.p1 = null;
            _tuple.p2 = null;
            _tuple.p3 = null;
        }
        
        public ref T1 C0
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_tuple.p0;
        }

        public ref T2 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_tuple.p1;
        }

        public ref T3 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_tuple.p2;
        }

        public ref T4 C3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_tuple.p3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<T1, T2, T3, T4> GetEnumerator() => this;

        public Tuple Current => _tuple;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _tuple.p0++;
                _tuple.p1++;
                _tuple.p2++;
                _tuple.p3++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;

                var li0 = arch.GetComponentLocalIndex(_type0);
                _tuple.p0 = (T1*)(arch.data.Ptr + arch.GetComponentOffset(li0));
                var li1 = arch.GetComponentLocalIndex(_type1);
                _tuple.p1 = (T2*)(arch.data.Ptr + arch.GetComponentOffset(li1));
                var li2 = arch.GetComponentLocalIndex(_type2);
                _tuple.p2 = (T3*)(arch.data.Ptr + arch.GetComponentOffset(li2));
                var li3 = arch.GetComponentLocalIndex(_type3);
                _tuple.p3 = (T4*)(arch.data.Ptr + arch.GetComponentOffset(li3));

                _remaining = count - 1;
                return true;
            }

            return false;
        }
        public struct Tuple
        {
            internal T1* p0;
            internal T2* p1;
            internal T3* p2;
            internal T4* p3;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out T1* c0, out T2* c1, out T3* c2, out T4* c3)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
                c3 = p3;
            }
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryParIter<T1, T2, T3, T4>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        private readonly Range _range;

        private readonly int* _arches;
        private readonly int _archesLen;

        private readonly World.WorldUnsafe* _world;

        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
        private static readonly int _type2 = ComponentType<T3>.Index;
        private static readonly int _type3 = ComponentType<T4>.Index;

        private int _archIndex;
        private int _globalIndex;

        private int _remaining;

        private Tuple _tuple;

        public QueryParIter(in Range range, in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _range = range;
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _globalIndex = 0;
            _remaining = 0;
            _tuple = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<T1, T2, T3, T4> GetEnumerator()
            => this;

        public Tuple Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            // hot path
            if (_remaining > 0)
            {
                _remaining--;

                _tuple.p0++;
                _tuple.p1++;
                _tuple.p2++;
                _tuple.p3++;

                return true;
            }

            var rangeStart = _range.start;
            var rangeEnd = _range.end;

            while (++_archIndex < _archesLen)
            {
                ref var arch =
                    ref _world->archetypesList
                        .Ptr[_arches[_archIndex]]
                        .Ref;

                var count = arch.count;

                if (count <= 0)
                    continue;

                var archEnd = _globalIndex + count;

                // whole archetype before range
                if (archEnd <= rangeStart)
                {
                    _globalIndex = archEnd;
                    continue;
                }

                // already past range
                if (_globalIndex >= rangeEnd)
                    return false;

                var localStart =
                    rangeStart > _globalIndex
                        ? rangeStart - _globalIndex
                        : 0;

                var localEnd =
                    rangeEnd < archEnd
                        ? rangeEnd - _globalIndex
                        : count;

                var localCount = localEnd - localStart;

                _globalIndex = archEnd;

                if (localCount <= 0)
                    continue;

                var li0 = arch.GetComponentLocalIndex(_type0);
                var li1 = arch.GetComponentLocalIndex(_type1);
                var li2 = arch.GetComponentLocalIndex(_type2);
                var li3 = arch.GetComponentLocalIndex(_type3);

                _tuple.p0 =
                    ((T1*)(arch.data.Ptr + arch.GetComponentOffset(li0)))
                    + localStart;

                _tuple.p1 =
                    ((T2*)(arch.data.Ptr + arch.GetComponentOffset(li1)))
                    + localStart;

                _tuple.p2 =
                    ((T3*)(arch.data.Ptr + arch.GetComponentOffset(li2)))
                    + localStart;

                _tuple.p3 =
                    ((T4*)(arch.data.Ptr + arch.GetComponentOffset(li3)))
                    + localStart;

                _remaining = localCount - 1;

                return true;
            }

            return false;
        }

        public struct Tuple
        {
            internal T1* p0;
            internal T2* p1;
            internal T3* p2;
            internal T4* p3;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(
                out T1* c0,
                out T2* c1,
                out T3* c2,
                out T4* c3)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
                c3 = p3;
            }
        }
    }
}