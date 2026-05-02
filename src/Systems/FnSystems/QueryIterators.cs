using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIter<T1>
        where T1 : unmanaged
    {
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private int _archIndex;
        private int _remaining;
        private T1* _p0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _p0 = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<T1> GetEnumerator() => this;

        public ref T1 Current => ref *_p0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _p0++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;

                var li0 = arch.GetComponentLocalIndex(_type0);
                _p0 = (T1*)(arch.data.Ptr + arch.GetComponentOffset(li0));
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }
[StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterWithEntity<T1>
        where T1 : unmanaged
    {
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private int _archIndex;
        private int _remaining;
        private Tuple _tuple;
        private int _index;
        private int* _entities;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterWithEntity(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple.pe = world->entities.Ptr;
            _tuple.p0 = null;
            _index = -1;
            _entities = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterWithEntity<T1> GetEnumerator() => this;

        public Tuple Current => _tuple;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _tuple.p0++;
                _tuple.pe = &_world->entities.Ptr[_entities[_index]];
                _index++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;

                _entities = arch.packedEntities.Ptr;
                var li0 = arch.GetComponentLocalIndex(_type0);
                _tuple.p0 = (T1*)(arch.data.Ptr + arch.GetComponentOffset(li0));
                _remaining = count - 1;
                _index = 0;
                return true;
            }

            return false;
        }
        public ref struct Tuple
        {
            internal T1* p0;
            internal Entity* pe;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out Entity e, out T1* c0)
            {
                e = *pe;
                c0 = p0;
            }
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIter<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
        private int _archIndex;
        private int _remaining;
        private Tuple _tuple;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple.p0 = null;
            _tuple.p1 = null;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<T1, T2> GetEnumerator() => this;

        public Tuple Current => _tuple;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _tuple.p0++;
                _tuple.p1++;
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
                _remaining = count - 1;
                return true;
            }

            return false;
        }
        public struct Tuple
        {
            internal T1* p0;
            internal T2* p1;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out T1* c0, out T2* c1)
            {
                c0 = p0;
                c1 = p1;
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIterWithEntity<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
        private int _archIndex;
        private int _remaining;
        private Tuple _tuple;
        private int _index;
        private int* _entities;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterWithEntity(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _tuple.pe = world->entities.Ptr;
            _tuple.p0 = null;
            _tuple.p1 = null;
            _index = -1;
            _entities = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterWithEntity<T1, T2> GetEnumerator() => this;

        public Tuple Current => _tuple;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _tuple.p0++;
                _tuple.p1++;
                _tuple.pe = &_world->entities.Ptr[_entities[_index]];
                _index++;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                ref var arch = ref _world->archetypesList.Ptr[_arches[_archIndex]].Ref;
                var count = arch.count;
                if (count <= 0) continue;

                _entities = arch.packedEntities.Ptr;
                var li0 = arch.GetComponentLocalIndex(_type0);
                _tuple.p0 = (T1*)(arch.data.Ptr + arch.GetComponentOffset(li0));
                var li1 = arch.GetComponentLocalIndex(_type1);
                _tuple.p1 = (T2*)(arch.data.Ptr + arch.GetComponentOffset(li1));
                _remaining = count - 1;
                _index = 0;
                return true;
            }

            return false;
        }
        public struct Tuple
        {
            internal T1* p0;
            internal T2* p1;
            internal Entity* pe;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out Entity e, out T1* c0, out T2* c1)
            {
                e = *pe;
                c0 = p0;
                c1 = p1;
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryIter<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
        private static readonly int _type2 = ComponentType<T3>.Index;
        private int _archIndex;
        private int _remaining;
        private Tuple _tuple;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<T1, T2, T3> GetEnumerator() => this;

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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out T1* c0, out T2* c1, out T3* c2)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out T1* c0, out T2* c1)
            {
                c0 = p0;
                c1 = p1;
            }
        }
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [StructLayout(LayoutKind.Sequential)]
        public ref struct Tuple
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(out T1* c0, out T2* c1, out T3* c2)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
            }
        }
    }
}