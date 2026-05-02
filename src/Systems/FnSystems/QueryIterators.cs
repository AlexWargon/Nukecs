using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIter<TTuple>
        where TTuple : unmanaged, IComponentTuple
    {
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
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
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
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

    public interface IRef<T> where T : unmanaged
    {
        void Add();
        unsafe void Set(T* ptr);
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ComponentRef<T> where T : unmanaged
    {
        internal T* _data;
        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_data;
        }
    }
}