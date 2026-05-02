using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)] 
    public unsafe ref struct QueryParIter<T1>
        where T1 : unmanaged
    {
        private readonly Range _range;
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private int _archIndex;
        private int _globalIndex;
        private int _remaining;
        private T1* _p0;
        public QueryParIter(in Range range, in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _range = range;
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _globalIndex = 0;
            _remaining = 0;
            _p0 = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<T1> GetEnumerator()
            => this;

        public ref T1 Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_p0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            // hot path
            if (_remaining > 0)
            {
                _remaining--;
                _p0++;
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

                _p0 =
                    ((T1*)(arch.data.Ptr + arch.GetComponentOffset(li0)))
                    + localStart;

                _remaining = localCount - 1;
                return true;
            }

            return false;
        }
    }
    [StructLayout(LayoutKind.Sequential)] 
    public unsafe ref struct QueryParIter<T1, T2>
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private readonly Range _range;
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
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
        public QueryParIter<T1, T2> GetEnumerator()
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

                _tuple.p0 =
                    ((T1*)(arch.data.Ptr + arch.GetComponentOffset(li0)))
                    + localStart;

                _tuple.p1 =
                    ((T2*)(arch.data.Ptr + arch.GetComponentOffset(li1)))
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
            public void Deconstruct(
                out T1* c0,
                out T2* c1)
            {
                c0 = p0;
                c1 = p1;
            }
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryParIter<T1, T2, T3>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        private readonly Range _range;
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
        private static readonly int _type2 = ComponentType<T3>.Index;
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
        public QueryParIter<T1, T2, T3> GetEnumerator()
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

                _tuple.p0 =
                    ((T1*)(arch.data.Ptr + arch.GetComponentOffset(li0)))
                    + localStart;

                _tuple.p1 =
                    ((T2*)(arch.data.Ptr + arch.GetComponentOffset(li1)))
                    + localStart;

                _tuple.p2 =
                    ((T3*)(arch.data.Ptr + arch.GetComponentOffset(li2)))
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(
                out T1* c0,
                out T2* c1,
                out T3* c2)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
            }
            public void Deconstruct(
                out T1* c0,
                out T2* c1)
            {
                c0 = p0;
                c1 = p1;
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
        [StructLayout(LayoutKind.Sequential)]
        public ref struct Tuple
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(
                out T1* c0,
                out T2* c1,
                out T3* c2)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
            }
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryParIter<T1, T2, T3, T4, T5>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        private readonly Range _range;
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
        private static readonly int _type2 = ComponentType<T3>.Index;
        private static readonly int _type3 = ComponentType<T4>.Index;
        private static readonly int _type4 = ComponentType<T5>.Index;
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
        public QueryParIter<T1, T2, T3, T4, T5> GetEnumerator()
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
                _tuple.p4++;
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
                var li4 = arch.GetComponentLocalIndex(_type4);
                
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

                _tuple.p4 =
                    ((T5*)(arch.data.Ptr + arch.GetComponentOffset(li4)))
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
            internal T5* p4;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(
                out T1* c0,
                out T2* c1,
                out T3* c2,
                out T4* c3,
                out T5* c4)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
                c3 = p3;
                c4 = p4;
            }
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
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryParIter<T1, T2, T3, T4, T5, T6>
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
    {
        private readonly Range _range;
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private static readonly int _type0 = ComponentType<T1>.Index;
        private static readonly int _type1 = ComponentType<T2>.Index;
        private static readonly int _type2 = ComponentType<T3>.Index;
        private static readonly int _type3 = ComponentType<T4>.Index;
        private static readonly int _type4 = ComponentType<T5>.Index;
        private static readonly int _type5 = ComponentType<T6>.Index;
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
        public QueryParIter<T1, T2, T3, T4, T5, T6> GetEnumerator()
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
                _tuple.p4++;
                _tuple.p5++;
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
                var li4 = arch.GetComponentLocalIndex(_type4);
                var li5 = arch.GetComponentLocalIndex(_type5);
                
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
                
                _tuple.p4 =
                    ((T5*)(arch.data.Ptr + arch.GetComponentOffset(li4)))
                    + localStart;
                
                _tuple.p4 =
                    ((T5*)(arch.data.Ptr + arch.GetComponentOffset(li5)))
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
            internal T5* p4;
            internal T6* p5;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(
                out T1* c0,
                out T2* c1,
                out T3* c2,
                out T4* c3,
                out T5* c4,
                out T6* c5)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
                c3 = p3;
                c4 = p4;
                c5 = p5;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Deconstruct(
                out T1* c0,
                out T2* c1,
                out T3* c2,
                out T4* c3,
                out T5* c4)
            {
                c0 = p0;
                c1 = p1;
                c2 = p2;
                c3 = p3;
                c4 = p4;
            }
        }
    }
}