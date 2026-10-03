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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
        private readonly int* _arches;
        private readonly int _archesLen;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
        private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _globalIndex;
        private int _remaining;
        private TTuple _tuple;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _rows;
        private int _listIdx;
        private readonly bool _storageMode;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
        private readonly int* _storages;
        private readonly int _storagesLen;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            _rows = null;
            _listIdx = 0;
            _storageMode = false;
            _storages = null;
            _storagesLen = 0;
        }

        /// <summary>Storage-mode parallel iterator: range splitting over whole storages (always dense).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter(in Range range, QueryUnsafe* query)
        {
            _range = range;
            _world = query->world;
            var storages = query->GetMatchingStorages();
            _storages = storages.Ptr;
            _storagesLen = storages.length;
            _storageMode = true;
            _arches = null;
            _archesLen = 0;
            _archIndex = -1;
            _globalIndex = 0;
            _remaining = 0;
            _tuple = default;
            _rows = null;
            _listIdx = 0;
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
                if (_rows != null) _tuple.AdvanceTo(_rows[++_listIdx]);
                else _tuple.Add();
                return true;
            }

            var rangeStart = _range.start;
            var rangeEnd = _range.end;

            if (_storageMode)
            {
                while (++_archIndex < _storagesLen)
                {
                    ref var st = ref _world->storagesList.Ptr[_storages[_archIndex]].Ref;
                    var count = st.count;
                    if (count <= 0) continue;

                    var archEnd = _globalIndex + count;
                    if (archEnd <= rangeStart) { _globalIndex = archEnd; continue; }
                    if (_globalIndex >= rangeEnd) return false;

                    var localStart = rangeStart > _globalIndex ? rangeStart - _globalIndex : 0;
                    var localEnd = rangeEnd < archEnd ? rangeEnd - _globalIndex : count;
                    var localCount = localEnd - localStart;
                    _globalIndex = archEnd;
                    if (localCount <= 0) continue;

                    ref var la = ref _world->archetypesList.Ptr[st.logicalArchetypes.Ptr[0]].Ref;
                    _rows = null;
                    _tuple.SetDataParallel(ref la, localStart);
                    _remaining = localCount - 1;
                    return true;
                }
                return false;
            }

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

                _rows = arch.RowsAreDense ? null : arch.rows.Ptr;
                if (_rows != null) {
                    _tuple.SetDataParallel(ref arch, 0);
                    _listIdx = localStart;
                    _tuple.AdvanceTo(_rows[localStart]);
                } else {
                    _tuple.SetDataParallel(ref arch, localStart);
                }
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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] 
        private readonly int* _arches;
        private readonly int _archesLen;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] 
        private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _globalIndex;
        private int _remaining;
        private TTuple _tuple;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _rows;
        private int _listIdx;
        private readonly bool _storageMode;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
        private readonly int* _storages;
        private readonly int _storagesLen;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            _rows = null;
            _listIdx = 0;
            _storageMode = false;
            _storages = null;
            _storagesLen = 0;
        }

        /// <summary>Storage-mode parallel iterator: range splitting over whole storages (always dense).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIterWithEntity(in Range range, QueryUnsafe* query)
        {
            _range = range;
            _world = query->world;
            var storages = query->GetMatchingStorages();
            _storages = storages.Ptr;
            _storagesLen = storages.length;
            _storageMode = true;
            _arches = null;
            _archesLen = 0;
            _archIndex = -1;
            _globalIndex = 0;
            _remaining = 0;
            _tuple = default;
            _rows = null;
            _listIdx = 0;
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
                if (_rows != null) _tuple.AdvanceTo(_rows[++_listIdx]);
                else _tuple.Add();
                return true;
            }

            var rangeStart = _range.start;
            var rangeEnd = _range.end;

            if (_storageMode)
            {
                while (++_archIndex < _storagesLen)
                {
                    ref var st = ref _world->storagesList.Ptr[_storages[_archIndex]].Ref;
                    var count = st.count;
                    if (count <= 0) continue;

                    var archEnd = _globalIndex + count;
                    if (archEnd <= rangeStart) { _globalIndex = archEnd; continue; }
                    if (_globalIndex >= rangeEnd) return false;

                    var localStart = rangeStart > _globalIndex ? rangeStart - _globalIndex : 0;
                    var localEnd = rangeEnd < archEnd ? rangeEnd - _globalIndex : count;
                    var localCount = localEnd - localStart;
                    _globalIndex = archEnd;
                    if (localCount <= 0) continue;

                    ref var la = ref _world->archetypesList.Ptr[st.logicalArchetypes.Ptr[0]].Ref;
                    _rows = null;
                    _tuple.SetDataParallel(ref la, la.packedEntities.Ptr, _world->entities.Ptr, localStart);
                    _remaining = localCount - 1;
                    return true;
                }
                return false;
            }

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
                _rows = arch.RowsAreDense ? null : arch.rows.Ptr;
                if (_rows != null) {
                    _tuple.SetDataParallel(ref arch, arch.packedEntities.Ptr, _world->entities.Ptr, 0);
                    _listIdx = localStart;
                    _tuple.AdvanceTo(_rows[localStart]);
                } else {
                    _tuple.SetDataParallel(ref arch, arch.packedEntities.Ptr, _world->entities.Ptr, localStart);
                }
                _remaining = localCount - 1;
                return true;
            }

            return false;
        }
    }
}