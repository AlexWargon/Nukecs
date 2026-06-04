using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterT1<T1>
        where T1 : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);
        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        [NativeDisableUnsafePtrRestriction] private T1* _data;
        [NativeDisableUnsafePtrRestriction] private int* _packedEntities;
        private int _entityRow;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterT1(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _data = null;
            _packedEntities = null;
            _entityRow = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIterT1<T1> GetEnumerator() => this;

        public ref T1 Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref *_data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                if (T1IsEntity)
                {
                    _entityRow++;
                    _data = (T1*)(&(_world->entities.Ptr[_packedEntities[_entityRow]]));
                }
                else
                {
                    _data++;
                }
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;
                if (T1IsEntity)
                {
                    _packedEntities = arch.packedEntities.Ptr;
                    _entityRow = 0;
                    _data = (T1*)(&(_world->entities.Ptr[_packedEntities[0]]));
                }
                else
                {
                    _data = (T1*)(arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)));
                }
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIter<TTuple>
        where TTuple : unmanaged, IComponentTuple
    {
        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        private TTuple _tuple;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<TTuple> GetEnumerator() => this;

        public readonly TTuple Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _tuple.Add();
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;
                _tuple.SetData(ref arch);
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterWithEntity<TTuple>
        where TTuple : unmanaged, IComponentEntityTuple
    {
        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        private TTuple _tuple;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterWithEntity(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIterWithEntity<TTuple> GetEnumerator() => this;
        public readonly TTuple Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _tuple;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _tuple.Add();
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;
                _tuple.SetData(ref arch, arch.packedEntities.Ptr, _world->entities.Ptr);
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterObject<TTuple>
        where TTuple : struct, IComponentEntityTupleRanged
    {
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _remaining;
        private TTuple _tuple;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterObject(int archetype, World.WorldUnsafe* world, ref TTuple tuple, Range range)
        {
            _world = world;
            _remaining = 0;
            _tuple = tuple;
            ref var arch = ref _world->archetypesList.Ptr[archetype].Ref;
            _tuple.SetData(ref arch, arch.packedEntities.Ptr, _world->entities.Ptr, range);
            _remaining = range.end - range.start + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIterObject<TTuple> GetEnumerator() => this;
        public readonly TTuple Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _tuple;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining <= 0) return false;
            _remaining--;
            _tuple.Add();
            return true;
        }
    }
    public interface IComponentTuple
    {
        void Add();
        void SetData(ref ArchetypeUnsafe archetype);
        void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart);
    }
    public unsafe interface IComponentEntityTuple
    {
        void Add();
        void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities);
        void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart);
    }
    public unsafe interface IComponentEntityTupleRanged
    {
        void Add();
        void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, Range range);
    }
    public interface IRef<T> where T : unmanaged
    {
        void Add();
        unsafe void Set(T* ptr);
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ComponentRef<T> where T : unmanaged
    {
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] internal T* _data;
        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_data;
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryChunkIter<TChunk>
        where TChunk : unmanaged, IChunk
    {
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private TChunk _chunk;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryChunkIter(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _chunk = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<TChunk> GetEnumerator() => this;

        public readonly TChunk Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _chunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;
                _chunk.SetData(ref arch);
                return true;
            }

            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Ptr4<T1, T2, T3, T4>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        public T1* p0;
        public T2* p1;
        public T3* p2;
        public T4* p3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* p0, out T2* p1, out T3* p2, out T4* p3)
        {
            p0 = this.p0;
            p1 = this.p1;
            p2 = this.p2;
            p3 = this.p3;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterTN<T1, T2, T3, TOption>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where TOption : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        private Ptr4<T1, T2, T3, TOption> _tuple;
        private readonly bool _optIsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterTN(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple = default;
            _optIsComponent = QueryParamInfo<TOption>.IsComponent;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIterTN<T1, T2, T3, TOption> GetEnumerator() => this;

        public readonly Ptr4<T1, T2, T3, TOption> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _tuple.p0++;
                _tuple.p1++;
                _tuple.p2++;
                if (_optIsComponent) _tuple.p3++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;
                var ptr = arch.data.Ptr;
                _tuple.p0 = (T1*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)));
                _tuple.p1 = (T2*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)));
                _tuple.p2 = (T3*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T3>.Index)));
                if (_optIsComponent)
                    _tuple.p3 = (TOption*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<TOption>.Index)));
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Ptr5<T1, T2, T3, T4, T5>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] public T1* p0;
        [NativeDisableUnsafePtrRestriction] public T2* p1;
        [NativeDisableUnsafePtrRestriction] public T3* p2;
        [NativeDisableUnsafePtrRestriction] public T4* p3;
        [NativeDisableUnsafePtrRestriction] public T5* p4;
        [NativeDisableUnsafePtrRestriction] public int* entity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity* e, out T2* c1, out T3* c2, out T4* c3, out T5* c4)
        {
            e = (Entity*)p0 + *entity;
            c1 = p1; c2 = p2; c3 = p3; c4 = p4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2, out T3* c3, out T4* c4, out T5* c5)
        {
            c1 = p0; c2 = p1; c3 = p2; c4 = p3; c5 = p4;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity* e, out T2* c1, out T3* c2, out T4* c3)
        {
            e = (Entity*)p0 + *entity;
            c1 = p1; c2 = p2; c3 = p3;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out T1* c1, out T2* c2, out T3* c3, out T4* c4)
        {
            c1 = p0; c2 = p1; c3 = p2; c4 = p3;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIter5<T1, T2, T3, T4, T5>
        where T1 : unmanaged
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);
        
        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        private Ptr5<T1, T2, T3, T4, T5> _tuple;
        private readonly bool _t5IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter5(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple = default;
            if (T1IsEntity)
            {
                _tuple.p0 = (T1*)world->entities.Ptr;
            }
            _t5IsComponent = QueryParamInfo<T5>.IsComponent;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter5<T1, T2, T3, T4, T5> GetEnumerator() => this;

        public readonly Ptr5<T1, T2, T3, T4, T5> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                if (T1IsEntity) _tuple.entity++;
                else _tuple.p0++;
                _tuple.p1++;
                _tuple.p2++;
                _tuple.p3++;
                if (_t5IsComponent) _tuple.p4++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;
                var ptr = arch.data.Ptr;
                if (T1IsEntity)
                    _tuple.entity = arch.packedEntities.Ptr;
                else
                    _tuple.p0 = (T1*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)));
                _tuple.p1 = (T2*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)));
                _tuple.p2 = (T3*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T3>.Index)));
                _tuple.p3 = (T4*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T4>.Index)));
                if (_t5IsComponent)
                    _tuple.p4 = (T5*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T5>.Index)));
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Ref5<T1, T2, T3, T4, T5>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] public Ref<T1> p0;
        [NativeDisableUnsafePtrRestriction] public Ref<T2> p1;
        [NativeDisableUnsafePtrRestriction] public Ref<T3> p2;
        [NativeDisableUnsafePtrRestriction] public Ref<T4> p3;
        [NativeDisableUnsafePtrRestriction] public Ref<T5> p4;
        [NativeDisableUnsafePtrRestriction] public int* entity;
        [NativeDisableUnsafePtrRestriction] public Entity* allEntities;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c0,
            out Ref<T2> c1,
            out Ref<T3> c2,
            out Ref<T4> c3,
            out Ref<T5> c4)
        {
            c0 = p0; c1 = p1; c2 = p2; c3 = p3; c4 = p4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c0,
            out Ref<T2> c1,
            out Ref<T3> c2,
            out Ref<T4> c3)
        {
            c0 = p0; c1 = p1; c2 = p2; c3 = p3;
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryRefIter5<T1, T2, T3, T4, T5>
        where T1 : unmanaged
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        private Ref5<T1, T2, T3, T4, T5> _tuple;
        private readonly bool _t5IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryRefIter5(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple = default;
            _tuple.allEntities = world->entities.Ptr;
            _t5IsComponent = QueryParamInfo<T5>.IsComponent;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryRefIter5<T1, T2, T3, T4, T5> GetEnumerator() => this;

        public readonly Ref5<T1, T2, T3, T4, T5> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                if (T1IsEntity)
                {
                    _tuple.entity++;
                    _tuple.p0.data = (T1*)(_tuple.allEntities + *_tuple.entity);
                }
                else _tuple.p0.data++;
                _tuple.p1.data++;
                _tuple.p2.data++;
                _tuple.p3.data++;
                if (_t5IsComponent) _tuple.p4.data++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;
                var ptr = arch.data.Ptr;
                if (T1IsEntity)
                {
                    _tuple.entity = arch.packedEntities.Ptr;
                    _tuple.p0.data = (T1*)(_tuple.allEntities + *_tuple.entity);
                }
                else
                    _tuple.p0.data = (T1*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)));
                _tuple.p1.data = (T2*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)));
                _tuple.p2.data = (T3*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T3>.Index)));
                _tuple.p3.data = (T4*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T4>.Index)));
                if (_t5IsComponent)
                    _tuple.p4.data = (T5*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T5>.Index)));
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }
    
    public static class IterExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetEnumerator<T>(this T iter) where T : unmanaged, IChunk
        {
            return iter;
        }
    }
    
    public unsafe struct Items
    {
        [NativeDisableUnsafePtrRestriction] public byte* _items;
        public int count;

    }

    public struct typeOf
    {
        private static readonly SharedStatic<short> data = SharedStatic<short>.GetOrCreate<typeOf>();
        public static ref short Amount => ref data.Data;
    }

    public struct typeOf<T> where T : unmanaged
    {
        public static short id;

        static typeOf()
        {
            var d = typeOf.Amount;
            id = d;
            typeOf.Amount++;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityTuple2<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged
    {
        public Entity Entity;
        [NativeDisableUnsafePtrRestriction] public Ref<T1> c1;
        [NativeDisableUnsafePtrRestriction] public Ref<T2> c2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> r1)
        {
            e = Entity; r1 = c1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> r1, out Ref<T2> r2)
        {
            e = Entity; r1 = c1; r2 = c2;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterE2<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged
    {
        private static readonly bool _t2IsComponent = QueryParamInfo<T2>.IsComponent;

        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        private EntityTuple2<T1, T2> _tuple;
        [NativeDisableUnsafePtrRestriction] private int* _packedEntities;
        private int _entityRow;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterE2(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple = default;
            _packedEntities = null;
            _entityRow = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIterE2<T1, T2> GetEnumerator() => this;

        public readonly EntityTuple2<T1, T2> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _entityRow++;
                _tuple.Entity = _world->entities.Ptr[_packedEntities[_entityRow]];
                _tuple.c1.data++;
                if (_t2IsComponent) _tuple.c2.data++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                int count = arch.count;
                if (count <= 0) continue;
                byte* ptr = arch.data.Ptr;
                _packedEntities = arch.packedEntities.Ptr;
                _entityRow = 0;
                _tuple.Entity = _world->entities.Ptr[_packedEntities[0]];
                _tuple.c1.data = (T1*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)));
                if (_t2IsComponent) _tuple.c2.data = (T2*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)));
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityTuple3<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged
    {
        public Entity Entity;
        [NativeDisableUnsafePtrRestriction] public Ref<T1> c1;
        [NativeDisableUnsafePtrRestriction] public Ref<T2> c2;
        [NativeDisableUnsafePtrRestriction] public Ref<T3> c3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> r1, out Ref<T2> r2)
        {
            e = Entity; r1 = c1; r2 = c2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> r1, out Ref<T2> r2, out Ref<T3> r3)
        {
            e = Entity; r1 = c1; r2 = c2; r3 = c3;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterE3<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged
    {
        private static readonly bool _t3IsComponent = QueryParamInfo<T3>.IsComponent;

        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        private EntityTuple3<T1, T2, T3> _tuple;
        [NativeDisableUnsafePtrRestriction] private int* _packedEntities;
        private int _entityRow;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterE3(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple = default;
            _packedEntities = null;
            _entityRow = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIterE3<T1, T2, T3> GetEnumerator() => this;

        public readonly EntityTuple3<T1, T2, T3> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _entityRow++;
                _tuple.Entity = _world->entities.Ptr[_packedEntities[_entityRow]];
                _tuple.c1.data++;
                _tuple.c2.data++;
                if (_t3IsComponent) _tuple.c3.data++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                int count = arch.count;
                if (count <= 0) continue;
                byte* ptr = arch.data.Ptr;
                _packedEntities = arch.packedEntities.Ptr;
                _entityRow = 0;
                _tuple.Entity = _world->entities.Ptr[_packedEntities[0]];
                _tuple.c1.data = (T1*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)));
                _tuple.c2.data = (T2*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)));
                if (_t3IsComponent) _tuple.c3.data = (T3*)(ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T3>.Index)));
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }
}