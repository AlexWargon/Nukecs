using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Collections;

    public unsafe struct Query
    {
        [NativeDisableUnsafePtrRestriction] internal QueryUnsafe* queryUnsafe;
        internal byte worldId;
        internal int id;
        internal int version;
        
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                RestoreIfNeed();
                return queryUnsafe->count;
            }
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                RestoreIfNeed();
                return queryUnsafe->count == 0;
            }
        }

        internal int CountMulti
        {
            get
            {
                RestoreIfNeed();
                return queryUnsafe->count / queryUnsafe->world->job_worker_count;
            }
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => queryUnsafe != null;
        }

        internal Query(ptr<QueryUnsafe> query)
        {
            queryUnsafe = query.Ptr;
            worldId = query.Ref.world->Id;
            id = query.Ref.Id;
            version = 0;
        }

        internal void FixAfterDeserialize(World world) {
            if (worldId == world.Id && id >= 0) {
                var queries = world.UnsafeWorld->queries;
                if (id < queries.Length)
                    queryUnsafe = queries.Ptr[id].Ptr;
            }
        }

        public Query With<T>(ReadWrite readWrite = ReadWrite.ReadWrite) where T : unmanaged, IComponent
        {
            queryUnsafe->With(ComponentType<T>.Index);
            return this;
        }

        public Query WithArray<T>() where T : unmanaged, IArrayComponent
        {
            queryUnsafe->With(ComponentType<ComponentArray<T>>.Index);
            return this;
        }

        public Query None<T>() where T : unmanaged, IComponent
        {
            queryUnsafe->None(ComponentType<T>.Index);
            return this;
        }

        public Query With(int componentIndex)
        {
            queryUnsafe->With(componentIndex);
            return this;
        }

        public Query None(int componentIndex)
        {
            queryUnsafe->None(componentIndex);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity First()
        {
            if (Count > 0)
            {
                if (queryUnsafe->UseStorageIteration())
                {
                    var storages = queryUnsafe->GetMatchingStorages();
                    var list = queryUnsafe->world->storagesList.Ptr;
                    for (var i = 0; i < storages.length; i++)
                    {
                        ref var st = ref list[storages.Ptr[i]].Ref;
                        if (st.count > 0)
                            return ref queryUnsafe->world->entities.Ptr[st.packedEntities.Ptr[0]];
                    }
                }
                else
                {
                    var len = queryUnsafe->matchingArchetypes.length;
                    var ptr = queryUnsafe->matchingArchetypes.Ptr;
                    var arches = queryUnsafe->world->archetypesList.Ptr;
                    for (var i = 0; i < len; i++)
                    {
                        ref var arch = ref arches[ptr[i]].Ref;
                        if (arches[ptr[i]].Ref.count > 0)
                        {
                            var rowsPtr = arch.RowsAreDense ? null : arch.rows.Ptr;
                            var row0 = rowsPtr != null ? rowsPtr[0] : 0;
                            return ref queryUnsafe->world->entities.Ptr[arch.packedEntities.Ptr[row0]];
                        }
                    }
                }
            }
            throw new Exception("No entities found");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (Entity entity, bool ok) FirstOk()
        {
            return Count > 0
                ? (queryUnsafe->GetEntity(0), true)
                : (Entity.Null, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index)
        {
            return ref queryUnsafe->GetEntity(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityIndex(int index)
        {
            return queryUnsafe->GetEntity(index).id;
        }

        public override string ToString()
        {
            return queryUnsafe->ToString();
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RestoreIfNeed()
        {
            if (version != World.Get(worldId).UnsafeWorldRef.version)
            {
                queryUnsafe = World.Get(worldId).UnsafeWorldRef.queries.ElementAt(id).Ptr;
                //dbug.log("Q RESTORED");
                version = World.Get(worldId).UnsafeWorldRef.version;
            }
        }

        public static void RestoreIfNeed(ref QueryUnsafe* query, ref int version, int id, ref World world)
        {
            if (version != world.UnsafeWorldRef.version)
            {
                query = world.UnsafeWorldRef.queries.ElementAt(id).Ptr;
                version = world.UnsafeWorldRef.version;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryEnumerator2 GetEnumerator()
        {
            RestoreIfNeed();
            if (queryUnsafe->UseStorageIteration())
                return new QueryEnumerator2(queryUnsafe);
            return new QueryEnumerator2(in queryUnsafe->matchingArchetypes, queryUnsafe->world);
        }
    }


    public unsafe struct QueryUnsafe
    {
        internal DynamicBitmask with;
        internal DynamicBitmask none;

        public MemoryList<int> matchingArchetypes;
        public int matchingArchetypesCount;

        internal int entityCount;

        // ---------------- storage-mode iteration ----------------
        // A query qualifies when every `with` bit is inline-category. `none` bits of any category
        // are allowed: non-inline none-bits disqualify individual storages at match time when one
        // of their sharing logical archetypes is non-empty (prefab/dead variants live in separate
        // logical archetypes of the same storage).
        /// <summary>0 = unknown, 1 = not storage mode, 2 = storage mode.</summary>
        internal byte storageModeState;
        /// <summary>1 when at least one inline-matching storage is disqualified by a non-empty
        /// tag/pool none-bit logical archetype — the query falls back to the archetype path.</summary>
        internal byte storageDegraded;
        internal bool storageMasksDirty;
        internal int storagesBuiltForLen;
        internal int storagesBuiltAtVersion;
        /// <summary>Indices into world->storagesList whose every row matches this query.</summary>
        public MemoryList<int> matchingStorages;
        internal MemoryList<int> storageFilterBits;

        /// <summary>Number of matching entities. Storage-mode queries compute it from storages.</summary>
        public int count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (UseStorageIteration()) return StorageModeCount();
                return entityCount;
            }
        }

        internal ptr<World.WorldUnsafe> worldPtr;
        [NativeDisableUnsafePtrRestriction] public World.WorldUnsafe* world;
        internal ptr<QueryUnsafe> self;

        public int Id;
        internal byte oldVersion;
        internal byte newVersion;

        // Changed<T> storage pointers — resolved in Changed<T>.Setup (non-Burst context).
        // Burst-compiled OnUpdateBatched reads these directly — no managed calls.
        // void* is used for Burst compatibility (NativeContainer pointer cast at point of use).
        [NativeDisableUnsafePtrRestriction] public void* ChangedEntitiesPtr;
        [NativeDisableUnsafePtrRestriction] public void* ChangedOffsetsPtr;
        [NativeDisableUnsafePtrRestriction] public void* ChangedValuesPtr;
        public int ChangedComponentSize;

        public bool IsDirty()
        {
            if (oldVersion != newVersion)
            {
                oldVersion = newVersion;
                return true;
            }
            return false;
        }
        public bool IsCreated => world != null;

        internal void OnDeserialize(ref MemAllocator allocator)
        {
            with.OnDeserialize(ref allocator);
            none.OnDeserialize(ref allocator);
            matchingArchetypes.OnDeserialize(ref allocator);
            matchingStorages.OnDeserialize(ref allocator);
            storageFilterBits.OnDeserialize(ref allocator);
            self.OnDeserialize(ref allocator);
            worldPtr.OnDeserialize(ref allocator);
            world = worldPtr.Ptr;
            storageModeState = 0;
            storageMasksDirty = true;
            storagesBuiltForLen = -1;
            storagesBuiltAtVersion = -1;
        }

        internal static void Free(QueryUnsafe* queryImpl)
        {
            queryImpl->Free();
            queryImpl->world->_free(queryImpl);
        }

        private void Free()
        {
            with.Dispose();
            none.Dispose();
        }

        internal static ptr<QueryUnsafe> CreatePtrRef(ptr<World.WorldUnsafe> world, bool withDefaultNoneTypes = true)
        {
            var ptr = world.Ptr->_allocate_ptr<QueryUnsafe>();
            ptr.Ref = new QueryUnsafe(world, ptr, withDefaultNoneTypes);
            return ptr;
        }

        internal QueryUnsafe(ptr<World.WorldUnsafe> world, ptr<QueryUnsafe> self, bool withDefaultNoneTypes = true)
        {
            this.world = world.Ptr;
            this.worldPtr = world;
            this.with = DynamicBitmask.CreateForComponents(world.Ptr);
            this.none = DynamicBitmask.CreateForComponents(world.Ptr);
            this.entityCount = 0;
            this.matchingArchetypes = new MemoryList<int>(16, ref world.Ptr->AllocatorRef);
            this.matchingArchetypesCount = 0;
            this.storageModeState = 0;
            this.storageDegraded = 0;
            this.storageMasksDirty = true;
            this.storagesBuiltForLen = -1;
            this.storagesBuiltAtVersion = -1;
            this.matchingStorages = new MemoryList<int>(16, ref world.Ptr->AllocatorRef);
            this.storageFilterBits = new MemoryList<int>(16, ref world.Ptr->AllocatorRef);
            this.Id = world.Ptr->queries.Length;
            this.ChangedEntitiesPtr = null;
            this.ChangedOffsetsPtr = null;
            this.ChangedValuesPtr = null;
            this.ChangedComponentSize = 0;

            this.self = self;
            if (withDefaultNoneTypes)
            {
                foreach (var type in world.Ptr->DefaultNoneTypes)
                {
                    none.Add(type);
                }
            }

            newVersion = byte.MinValue;
            oldVersion = byte.MinValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MultiArray<int> GetEntities(Allocator allocator)
        {
            if (UseStorageIteration())
            {
                var storages = GetMatchingStorages();
                var storageArray = new MultiArray<int>(storages.length, allocator);
                for (var index = 0; index < storages.length; index++)
                {
                    ref var st = ref world->storagesList.Ptr[storages.Ptr[index]].Ref;
                    if (st.count > 0)
                        storageArray.Add(st.packedEntities.Ptr, st.count);
                }
                return storageArray;
            }
            var array = new MultiArray<int>(matchingArchetypes.length, allocator);
            for (var index = 0; index < matchingArchetypes.Length; index++)
            {
                var matchingArchetype = matchingArchetypes[index];
                ref var arch = ref world->archetypesList.ElementAt(matchingArchetype).Ref;
                var rowsPtr = arch.RowsAreDense ? null : arch.rows.Ptr;
                if (rowsPtr == null)
                {
                    array.Add(arch.packedEntities.Ptr, arch.count);
                }
                else
                {
                    array.AddGathered(arch.packedEntities.Ptr, rowsPtr, arch.count, allocator);
                }
            }
            return array;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index)
        {
            var remaining = index;
            if (UseStorageIteration())
            {
                var storages = GetMatchingStorages();
                for (var i = 0; i < storages.length; i++)
                {
                    ref var st = ref world->storagesList.Ptr[storages.Ptr[i]].Ref;
                    if (remaining < st.count)
                        return ref world->entities.Ptr[st.packedEntities.Ptr[remaining]];
                    remaining -= st.count;
                }
                return ref world->entities.Ptr[0];
            }
            for (var i = 0; i < matchingArchetypes.length; i++)
            {
                ref var arch = ref world->archetypesList.Ptr[matchingArchetypes.Ptr[i]].Ref;
                if (remaining < arch.count)
                {
                    var rowsPtr = arch.RowsAreDense ? null : arch.rows.Ptr;
                    var row = rowsPtr != null ? rowsPtr[remaining] : remaining;
                    return ref world->entities.Ptr[arch.packedEntities.Ptr[row]];
                }
                remaining -= arch.count;
            }
            return ref world->entities.Ptr[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityID(int index)
        {
            var remaining = index;
            if (UseStorageIteration())
            {
                var storages = GetMatchingStorages();
                for (var i = 0; i < storages.length; i++)
                {
                    ref var st = ref world->storagesList.Ptr[storages.Ptr[i]].Ref;
                    if (remaining < st.count)
                        return st.packedEntities.Ptr[remaining];
                    remaining -= st.count;
                }
                return -1;
            }
            for (var i = 0; i < matchingArchetypes.length; i++)
            {
                ref var arch = ref world->archetypesList.Ptr[matchingArchetypes.Ptr[i]].Ref;
                if (remaining < arch.count)
                {
                    var rowsPtr = arch.RowsAreDense ? null : arch.rows.Ptr;
                    var row = rowsPtr != null ? rowsPtr[remaining] : remaining;
                    return arch.packedEntities.Ptr[row];
                }
                remaining -= arch.count;
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(int entity)
        {
            entityCount++;
            unchecked
            {
                newVersion++;
            }
        }

        internal void AddArchetype(int archetypeIndex)
        {
            matchingArchetypes.Add(archetypeIndex, ref world->AllocatorRef);
            matchingArchetypesCount++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BatchAdd(int* entityIds, int cnt)
        {
            entityCount += cnt;
            unchecked
            {
                newVersion++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BatchAddRange(int startEntityId, int cnt)
        {
            entityCount += cnt;
            unchecked
            {
                newVersion++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Remove(int entity)
        {
            entityCount--;
            unchecked
            {
                newVersion++;
            }
        }

        internal void SyncVersion()
        {
            oldVersion = newVersion;
        }
        public QueryUnsafe* With(int type)
        {
            with.Add(type);
            storageModeState = 0;
            storageMasksDirty = true;
            return self.Ptr;
        }

        public bool HasWith(int type)
        {
            return with.Has(type);
        }

        public bool HasNone(int type)
        {
            return none.Has(type);
        }

        public QueryUnsafe* None(int type)
        {
            none.Add(type);
            storageModeState = 0;
            storageMasksDirty = true;
            return self.Ptr;
        }

        // ---------------- storage-mode ----------------

        /// <summary>
        /// True when this query can iterate whole storages densely (no per-row gather).
        /// Requires every `with` bit to be inline-category. `none` bits may be tag/pool:
        /// such bits disqualify individual storages at match time (see <see cref="GetMatchingStorages"/>).
        /// </summary>
        public bool IsStorageMode()
        {
            if (storageModeState == 0)
                storageModeState = (byte)(AllWithBitsInline() ? 2 : 1);
            return storageModeState == 2;
        }

        /// <summary>
        /// True when the query qualifies for dense storage iteration AND no storage was
        /// disqualified by non-empty tag/pool none-bit archetypes (degraded → archetype path).
        /// May rebuild the matching storages list — main thread only.
        /// </summary>
        public bool UseStorageIteration()
        {
            if (!IsStorageMode()) return false;
            GetMatchingStorages();
            return storageDegraded == 0;
        }

        /// <summary>
        /// Job-safe variant: NEVER rebuilds. True only when the snapshot is already fresh
        /// (the main thread refreshed it via RefreshStorageMode / GetMatchingStorages,
        /// e.g. from the generated Schedule before dispatching jobs) and not degraded.
        /// Stale snapshot → false → safe archetype path.
        /// </summary>
        public bool TryUseStorageIteration()
        {
            if (storageModeState != 2) return false;
            var w = world;
            if (storageMasksDirty
                || storagesBuiltForLen != w->storagesList.length
                || storagesBuiltAtVersion != w->version)
                return false;
            return storageDegraded == 0;
        }

        /// <summary>Main-thread refresh of the storage-mode snapshot before system dispatch.</summary>
        public void RefreshStorageMode()
        {
            if (storageModeState == 2) GetMatchingStorages();
        }

        private bool AllWithBitsInline()
        {
            with.ExtractSetBits(ref storageFilterBits, ref world->AllocatorRef);
            for (var i = 0; i < storageFilterBits.length; i++)
            {
                if (ComponentTypeMap.GetCategory(storageFilterBits.Ptr[i]) != ComponentCategory.Inline)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Storages whose every row matches this query. Inline filters are checked against the
        /// storage mask directly; tag/pool none-bits disqualify the storage while any of its
        /// sharing logical archetypes holding that bit is non-empty.
        /// Lazily rebuilt when filters change, new storages appear, or world structure changes
        /// (world->version is bumped by every row allocation/removal and archetype migration).
        /// </summary>
        public MemoryList<int> GetMatchingStorages()
        {
            var w = world;
            // Parallel system runners call Update(range) inside jobs — a rebuild may be
            // triggered from any thread. Serialize rebuild + snapshot reads with the world
            // spinner so iterators never observe a half-built list.
            w->spinner.Acquire();
            if (storageMasksDirty
                || storagesBuiltForLen != w->storagesList.length
                || storagesBuiltAtVersion != w->version)
            {
                RebuildMatchingStorages();
            }
            w->spinner.Release();
            return matchingStorages;
        }

        private void RebuildMatchingStorages()
        {
            var w = world;
            matchingStorages.Clear();
            var degraded = (byte)0;
            none.ExtractSetBits(ref storageFilterBits, ref w->AllocatorRef);
            var noneBits = storageFilterBits;
            for (var si = 0; si < w->storagesList.length; si++)
            {
                ref var st = ref w->storagesList.Ptr[si].Ref;
                if (!st.IsCreated) continue;
                if (!st.inlineMask.ContainsAll(ref with)) continue;
                if (!st.inlineMask.ContainsNone(ref none)) continue;
                if (!NoneBitsClearInLogicalArchetypes(w, si, ref noneBits))
                {
                    // a non-empty tag/pool none-archetype lives in this storage:
                    // per-row exclusion is impossible in dense mode → degrade the whole query
                    degraded = 1;
                    continue;
                }
                matchingStorages.Add(si, ref w->AllocatorRef);
            }
            storageDegraded = degraded;
            storagesBuiltForLen = w->storagesList.length;
            storagesBuiltAtVersion = w->version;
            storageMasksDirty = false;
        }

        private static bool NoneBitsClearInLogicalArchetypes(World.WorldUnsafe* w, int storageIndex, ref MemoryList<int> noneBits)
        {
            if (noneBits.length == 0) return true;
            ref var st = ref w->storagesList.Ptr[storageIndex].Ref;
            for (var li = 0; li < noneBits.length; li++)
            {
                var bit = noneBits.Ptr[li];
                var category = ComponentTypeMap.GetCategory(bit);
                if (category == ComponentCategory.Inline) continue; // already checked against inlineMask
                for (var ai = 0; ai < st.logicalArchetypes.length; ai++)
                {
                    ref var la = ref w->archetypesList.Ptr[st.logicalArchetypes.Ptr[ai]].Ref;
                    if (la.count == 0) continue;
                    if (category == ComponentCategory.Tag && la.tagMask.Has(bit)) return false;
                    if (category == ComponentCategory.Pool && la.poolMask.Has(bit)) return false;
                }
            }
            return true;
        }

        private int StorageModeCount()
        {
            var total = 0;
            var storages = GetMatchingStorages();
            for (var i = 0; i < storages.length; i++)
                total += world->storagesList.Ptr[storages.Ptr[i]].Ref.count;
            return total;
        }

        [BurstDiscard]
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Query");
            foreach (var typesIndex in ComponentTypeMap.TypesIndexes)
            {
                if (HasWith(typesIndex))
                {
                    sb.Append($".With<{ComponentTypeMap.GetType(typesIndex).Name}>()");
                }

                if (HasNone(typesIndex))
                {
                    sb.Append($".None<{ComponentTypeMap.GetType(typesIndex).Name}>()");
                }
            }

            sb.Append($".Count = {count}");
            return sb.ToString();
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryEnumerator2

    {
        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        [NativeDisableUnsafePtrRestriction] private ArchetypeUnsafe* _arch;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _row;
        private int _remaining;
        [NativeDisableUnsafePtrRestriction] private int* _rows;
        private readonly bool _storageMode;
        [NativeDisableUnsafePtrRestriction] private readonly int* _storages;
        private readonly int _storagesLen;
        [NativeDisableUnsafePtrRestriction] private StorageArchetype* _storage;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryEnumerator2(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _row = 0;
            _remaining = 0;
            _arch = default;
            _rows = null;
            _storageMode = false;
            _storages = null;
            _storagesLen = 0;
            _storage = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryEnumerator2(QueryUnsafe* query)
        {
            _world = query->world;
            var storages = query->GetMatchingStorages();
            _storages = storages.Ptr;
            _storagesLen = storages.length;
            _storage = null;
            _storageMode = true;
            _arches = null;
            _archesLen = 0;
            _archIndex = -1;
            _row = 0;
            _remaining = 0;
            _arch = default;
            _rows = null;
        }

        public ref Entity Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get {
                if (_storageMode)
                    return ref _world->entities.Ptr[_storage->packedEntities.Ptr[_row]];
                var row = _rows != null ? _rows[_row] : _row;
                return ref _world->entities.Ptr[_arch->packedEntities.Ptr[row]];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                _row++;
                return true;
            }

            if (_storageMode)
            {
                while (++_archIndex < _storagesLen)
                {
                    _storage = _world->storagesList.Ptr[_storages[_archIndex]].Ptr;
                    var count = _storage->count;
                    if (count <= 0) continue;
                    _row = 0;
                    _remaining = count - 1;
                    return true;
                }
                return false;
            }

            while (++_archIndex < _archesLen)
            {
                _arch = _world->archetypesList.Ptr[_arches[_archIndex]].Ptr;
                var count = _arch->count;
                if (count <= 0) continue;
                _rows = _arch->RowsAreDense ? null : _arch->rows.Ptr;
                _row = 0;
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }
    public unsafe ref struct QueryEnumerator
    {
        private int _lastIndex;
        private int _lastArch;
        private int _archRow;
        private int _countInArch;
        private readonly QueryUnsafe* _query;
        private ArchetypeUnsafe* _currentArchetype;
        [NativeDisableUnsafePtrRestriction] private int* _rows;
        private readonly bool _storageMode;
        [NativeDisableUnsafePtrRestriction] private StorageArchetype* _currentStorage;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal QueryEnumerator(QueryUnsafe* queryUnsafe)
        {
            _query = queryUnsafe;
            _lastIndex = -1;
            _lastArch = -1;
            _archRow = 0;
            _countInArch = 0;
            _currentArchetype = default;
            _rows = null;
            _storageMode = queryUnsafe->UseStorageIteration();
            _currentStorage = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (++_lastIndex >= _query->count) return false;
            if (_storageMode)
            {
                if (_lastArch < 0 || ++_archRow >= _countInArch)
                {
                    var storages = _query->GetMatchingStorages();
                    if (++_lastArch >= storages.length) return false;
                    _currentStorage = _query->world->storagesList.Ptr[storages.Ptr[_lastArch]].Ptr;
                    _countInArch = _currentStorage->count;
                    _archRow = 0;
                }
                return true;
            }
            if (_lastArch < 0 || ++_archRow >= _countInArch)
            {
                if (++_lastArch >= _query->matchingArchetypes.length) return false;
                var archIndex = _query->matchingArchetypes.Ptr[_lastArch];
                _currentArchetype = _query->world->archetypesList.Ptr[archIndex].Ptr;
                _countInArch = _currentArchetype->count;
                _rows = _currentArchetype->RowsAreDense ? null : _currentArchetype->rows.Ptr;
                _archRow = 0;
            }
            return true;
        }

        public void Reset()
        {
            _lastIndex = -1;
            _lastArch = -1;
            _archRow = -1;
            _countInArch = 0;
        }

        public ref Entity Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_storageMode)
                    return ref _query->world->entities.Ptr[_currentStorage->packedEntities.Ptr[_archRow]];
                var row = _rows != null ? _rows[_archRow] : _archRow;
                ref var e = ref _query->world->entities.Ptr[_currentArchetype->packedEntities.Ptr[row]];
                return ref e;
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Ref<TComponent> where TComponent : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] 
        public TComponent* data;

        public ref TComponent Val
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *data;
        }

        public ref TComponent Get
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *data;
        }

        public readonly ref readonly TComponent Read
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Ref(TComponent* ptr)
        {
            data = ptr;
        }
        public static implicit operator TComponent(Ref<TComponent> r)
        {
            return r.Val;
        }
        public static implicit operator Ref<TComponent>(TComponent r)
        {
            var ptr = (TComponent*)Unsafe.AsPointer(ref r);
            return new Ref<TComponent>(ptr);
        }
    }

    public readonly unsafe struct ReadRef<TComponent> where TComponent : unmanaged, IComponent
    {
        internal readonly int index;
        [NativeDisableUnsafePtrRestriction] internal readonly ComponentPoolUntyped* pool;
        [NativeDisableUnsafePtrRestriction] internal readonly Chunk* chunks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadRef(int index, ref GenericPool pool)
        {
            this.index = index;
            this.pool = pool.UnsafeBufferPtr.Ptr;
            this.chunks = this.pool->Chunks.Ptr;
        }

        public ref readonly TComponent Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Chunk.GetRef<TComponent>(chunks, index);
        }
    }

    public enum ReadWrite
    {
        Read,
        Write,
        ReadWrite,
    }
}
