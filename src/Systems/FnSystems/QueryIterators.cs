using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterT1<T1>
        where T1 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;
        [NativeDisableUnsafePtrRestriction] private T1* _data;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterT1(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _data = null;
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
                _data++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;
                _data = (T1*)(arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)));
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

    public static class IterExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetEnumerator<T>(this T iter) where T : unmanaged, IChunk
        {
            return iter;
        }
    }
}