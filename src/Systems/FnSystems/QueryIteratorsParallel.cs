using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryParIter<TTuple>
        where TTuple : unmanaged, IComponentTuple
    {
        private readonly Range _range;
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _globalIndex;
        private int _remaining;
        private TTuple _tuple;
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
        public readonly QueryParIter<TTuple> GetEnumerator()
            => this;

        public readonly TTuple Current
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
                _tuple.Add();
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

                _tuple.SetDataParallel(ref arch, localStart);
                _remaining = localCount - 1;
                return true;
            }

            return false;
        }
    }
    
        [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryParIterWithEntity<TTuple>
        where TTuple : unmanaged, IComponentEntityTuple
    {
        private readonly Range _range;
        private readonly int* _arches;
        private readonly int _archesLen;
        private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _globalIndex;
        private int _remaining;
        private TTuple _tuple;
        public QueryParIterWithEntity(in Range range, in MemoryList<int> arches, World.WorldUnsafe* world)
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
        public readonly QueryParIterWithEntity<TTuple> GetEnumerator()
            => this;

        public readonly TTuple Current
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
                _tuple.Add();
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
                _tuple.SetDataParallel(ref arch, arch.packedEntities.Ptr, _world->entities.Ptr, localStart);
                _remaining = localCount - 1;
                return true;
            }

            return false;
        }
    }
}