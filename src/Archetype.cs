using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;

namespace Wargon.Nukecs
{
    public unsafe struct Archetype
    {
        internal ptr<ArchetypeUnsafe> ptr;
        internal ArchetypeUnsafe* Unsafe
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] 
            get => ptr.Ptr;
        }
        public bool IsCreated => Unsafe != null;
        internal bool Has<T>() where T : unmanaged
        {
            return Unsafe->Has(ComponentType<T>.Index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity CreateEntity()
        {
            return ref Unsafe->CreateEntity();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Entity> BatchCreateEntity(int count)
        {
            return Unsafe->BatchCreateEntity(count);
        }
        public void Dispose()
        {
            //ArchetypeUnsafe.Destroy(impl);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetArchetype(in Entity entity)
        {
            Unsafe->SetArchetype(entity);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IComponent GetObject(in Entity entity, Type type)
        {
            return Unsafe->GetObject(entity.id, ComponentTypeMap.GetComponentType(type).index);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct EntityLocation {
        public int archetypeIndex;
        public int row;
        /// <summary>Position of this entity's row index inside the logical archetype's rows list (for O(1) removal).</summary>
        public int listPos;
    }

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct ArchetypeUnsafe
    {
        private Spinner _spinner;
        // Category-split identity masks. Bit positions are global component type indices.
        // inlineMask — data components stored in the archetype data buffer;
        // tagMask — zero-sized filter-only components (no bytes in data);
        // poolMask — per-entity GenericPool components (no bytes in data).
        internal DynamicBitmask inlineMask;
        internal DynamicBitmask tagMask;
        internal DynamicBitmask poolMask;
        internal MemoryList<int> types;
        internal MemoryList<int> queries;
        /// <summary>Bumped whenever the set of attached query ids changes
        /// (CheckQuery attach, PopulateQueries, Refresh). Pair-edge caches validate against it.</summary>
        internal int queriesVersion;
        /// <summary>Per-pair migration edge cache (from→to), keyed by (to.index &lt;&lt; 32) | from.index.
        /// Stores precomputed remove/add query lists so ECB playback avoids the quadratic
        /// Contains scan in BatchMigrateQueries. Lazily invalidated via queriesVersion.</summary>
        internal HashMap<long, ptr<Edge>> pairEdges;
        internal Edge destroyEdge;
        // Shared data owner: all logical archetypes with the same inlineMask point to one StorageArchetype.
        // rows — storage row indices owned by this logical archetype, iterated densely (rows.length == count).
        internal ptr<StorageArchetype> storagePtr;
        public MemoryList<int> rows;
        [NativeDisableUnsafePtrRestriction]
        public World.WorldUnsafe* world;
        internal int hashId;
        internal int index;
        internal bool IsCreated => world != null;

        // Storage-backed accessors kept for compatibility with existing read sites.
        public ptr<byte> data {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => storagePtr.Ref.data;
        }
        public MemoryArray<int> packedEntities {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => storagePtr.Ref.packedEntities;
        }
        public MemoryArray<int> componentOffsets {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => storagePtr.Ref.componentOffsets;
        }
        internal BitMap1024<int> offsetMap {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => storagePtr.Ref.offsetMap;
        }
        public int entityStride {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => storagePtr.Ref.entityStride;
        }
        public int capacity {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => storagePtr.Ref.capacity;
        }
        /// <summary>Number of entities in this logical archetype (== rows.length), not the whole storage.</summary>
        public int count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => rows.length;
        }
        /// <summary>
        /// True when this is the only logical archetype on its storage — all storage rows belong to it,
        /// so iterators can walk rows 0..count-1 sequentially (pointer-increment fast path).
        /// </summary>
        public bool RowsAreDense {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => storagePtr.Ref.refCount <= 1;
        }
        internal ref StorageArchetype Storage {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref storagePtr.Ref;
        }

        internal void OnDeserialize(ref MemAllocator allocator, World.WorldUnsafe* worldPtr)
        {
            world = worldPtr;
            inlineMask.OnDeserialize(ref allocator);
            tagMask.OnDeserialize(ref allocator);
            poolMask.OnDeserialize(ref allocator);
            queries.OnDeserialize(ref allocator);
            destroyEdge.OnDeserialize(ref allocator, worldPtr);
            types.OnDeserialize(ref allocator);
            storagePtr.OnDeserialize(ref allocator);
            rows.OnDeserialize(ref allocator);
            pairEdges.OnDeserialize(ref allocator);
            foreach (var pairEdge in pairEdges)
            {
                pairEdge.Value.OnDeserialize(ref allocator);
                pairEdge.Value.Ref.OnDeserialize(ref allocator, worldPtr);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddTypeToMasks(ref DynamicBitmask inline, ref DynamicBitmask tag, ref DynamicBitmask pool, int type)
        {
            switch (ComponentTypeMap.GetCategory(type))
            {
                case ComponentCategory.Tag:
                    tag.Add(type);
                    break;
                case ComponentCategory.Pool:
                    pool.Add(type);
                    break;
                default:
                    inline.Add(type);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RemoveTypeFromMasks(ref DynamicBitmask inline, ref DynamicBitmask tag, ref DynamicBitmask pool, int type)
        {
            switch (ComponentTypeMap.GetCategory(type))
            {
                case ComponentCategory.Tag:
                    tag.Remove(type);
                    break;
                case ComponentCategory.Pool:
                    pool.Remove(type);
                    break;
                default:
                    inline.Remove(type);
                    break;
            }
        }

        /// <summary>Checks membership in the category mask matching the given type. One branch + one bit test.
        /// Out-of-capacity indexes (types registered after this archetype was created) read as absent.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int componentTypeIndex)
        {
            switch (ComponentTypeMap.GetCategory(componentTypeIndex))
            {
                case ComponentCategory.Tag:
                    return tagMask.Contains(componentTypeIndex);
                case ComponentCategory.Pool:
                    return poolMask.Contains(componentTypeIndex);
                default:
                    return inlineMask.Contains(componentTypeIndex);
            }
        }

        /// <summary>
        /// Canonical identity hash. Byte-identical to FNV-1a over the union of all three masks,
        /// so it matches <see cref="DynamicBitmask.ComputeHash"/> of a full component bitmask.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ComputeIdentityHash(ref DynamicBitmask inline, ref DynamicBitmask tag, ref DynamicBitmask pool)
        {
            return inline.ComputeUnionHash(ref tag, ref pool);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool MasksSequenceEqual(ref ArchetypeUnsafe other)
        {
            return inlineMask.SequenceEqual(ref other.inlineMask)
                   && tagMask.SequenceEqual(ref other.tagMask)
                   && poolMask.SequenceEqual(ref other.poolMask);
        }

        /// <summary>True when the given full (all categories) bitmask equals this archetype's union of masks.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool FullMaskEquals(ref DynamicBitmask fullMask)
        {
            return fullMask.EqualsUnion(ref inlineMask, ref tagMask, ref poolMask);
        }

        /// <summary>True when this archetype's type set equals the given type list (order-independent, unique types).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool MatchesTypes(int* typesList, int count)
        {
            var total = inlineMask.Count + tagMask.Count + poolMask.Count;
            if (total != count) return false;
            for (var i = 0; i < count; i++)
                if (!Has(typesList[i])) return false;
            return true;
        }

        /// <summary>Rebuilds a full (all categories) bitmask from the three category masks. Used by ECB playback.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CopyMasksTo(ref DynamicBitmask target)
        {
            target.CopyUnion(ref inlineMask, ref tagMask, ref poolMask);
        }

        /// <summary>
        /// Rebuilds a full type mask from the three category masks into a fixed 1024-bit
        /// scratch mask (no world-allocator involvement — safe across save/load, no growth).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CopyMasksTo(ref Bitmask1024 target)
        {
            target.Clear();
            inlineMask.CopySetBitsTo(ref target);
            tagMask.CopySetBitsTo(ref target);
            poolMask.CopySetBitsTo(ref target);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetArchetype(in Entity entity)
        {
            ref var loc = ref world->entityLocations.Ptr[entity.id];
            ref var source = ref world->archetypesList.Ptr[loc.archetypeIndex].Ref;
            if (source.index == index) return;
            BatchMigrateQueries(ref source, ref this, entity.id);
            source.MoveEntityTo(loc.row, ref this);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref T GetComponent<T>(int entity, int size, int componentIndex) where T : unmanaged, IComponent
        {
            var off = offsetMap.GetRef(componentIndex);
            ref var loc = ref world->entityLocations.Ptr[entity];
            return ref *(T*)(data.Ptr + off + loc.row * size);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref T GetComponent<T>(int entity) where T : unmanaged, IComponent
        {
            ref var componentTypeData = ref ComponentType<T>.Data;
            var off = offsetMap.GetRef(componentTypeData.index);
            ref var loc = ref world->entityLocations.Ptr[entity];
            return ref *(T*)(data.Ptr + off + loc.row * componentTypeData.size);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetComponentByRow<T>(int row) where T : unmanaged, IComponent
        {
            ref var componentTypeData = ref ComponentType<T>.Data;
            var off = offsetMap.GetRef(componentTypeData.index);
            return ref *(T*)(data.Ptr + off + row * componentTypeData.size);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetComponentDataPtr(int componentTypeIndex, int row)
        {
            if (!offsetMap.Mask.HasFast(componentTypeIndex)) return null;
            var off = offsetMap.GetRef(componentTypeIndex);
            if (off < 0) return null;
            return data.Ptr + off + row * ComponentTypeMap.GetComponentType(componentTypeIndex).size;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref Entity GetEntity(int idx)
        {
            return ref world->entities.Ptr[packedEntities.Ptr[idx]];
        }
        public IComponent GetObject(int entity, int typeIndex)
        {
            var d = ComponentTypeMap.GetComponentType(typeIndex);
            if (d.storageType == StorageType.Pool)
                return world->GetUntypedPool(typeIndex).GetObject(entity);
            var localIdx = GetComponentLocalIndex(typeIndex);
            if (!offsetMap.Mask.HasFast(typeIndex)) return null;
            var off = componentOffsets.Ptr[localIdx];
            if (off < 0) return null; // tag / pool-stored — no inline data
            ref var loc = ref world->entityLocations.Ptr[entity];
            var ptr = data.Ptr + off + loc.row * d.size;
            return ComponentHelpers.Read(ptr, 0, d.size, typeIndex);
        }

        public void SetObject(int entity, int typeIndex, IComponent component)
        {
            var d = ComponentTypeMap.GetComponentType(typeIndex);
            if (d.storageType == StorageType.Pool)
            {
                world->GetUntypedPool(typeIndex).SetObject(entity, component);
                return;
            }
            var localIdx = GetComponentLocalIndex(typeIndex);
            if (!offsetMap.Mask.HasFast(typeIndex)) return;
            var off = componentOffsets.Ptr[localIdx];
            if (off < 0) return; // tag / pool-stored — no inline data
            ref var loc = ref world->entityLocations.Ptr[entity];
            var ptr = data.Ptr + off + loc.row * d.size;
            ComponentHelpers.Write(ptr, 0, d.size, typeIndex, component);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentLocalIndex(int componentTypeIndex)
        {
            return offsetMap.Mask.CountBefore(componentTypeIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentOffset(int localIndex)
        {
            return componentOffsets.Ptr[localIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetComponentSize(int localIndex)
        {
            return ComponentTypeMap.GetComponentType(storagePtr.Ref.inlineTypes.Ptr[localIndex]).size;
        }

        // ------------------------------------------------------------------
        // Logical-archetype membership over shared storage rows
        // ------------------------------------------------------------------

        private static void SortTypes(ref MemoryList<int> types) {
            if (types.length > 1) {
                for (int i = 1; i < types.length; i++) {
                    var key = types.Ptr[i];
                    int j = i - 1;
                    while (j >= 0 && types.Ptr[j] > key) {
                        types.Ptr[j + 1] = types.Ptr[j];
                        j--;
                    }
                    types.Ptr[j + 1] = key;
                }
            }
        }

        /// <summary>Appends a storage row index to this archetype's rows list, returns its position.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int AddRow(int row) {
            rows.Add(row, ref world->AllocatorRef);
            return rows.length - 1;
        }

        /// <summary>Swap-removes the rows-list entry at listPos, fixing listPos of the moved entry's entity.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveRowAt(int listPos) {
            var lastPos = rows.length - 1;
            if (listPos != lastPos) {
                var movedRow = rows.Ptr[lastPos];
                rows.Ptr[listPos] = movedRow;
                var movedEntity = storagePtr.Ref.packedEntities.Ptr[movedRow];
                world->entityLocations.Ptr[movedEntity].listPos = listPos;
            }
            rows.length--;
        }

        /// <summary>Removes the entity's row from this archetype and swap-removes the storage row.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveEntity(int row) {
            var entity = storagePtr.Ref.packedEntities.Ptr[row];
            var listPos = world->entityLocations.Ptr[entity].listPos;
            storagePtr.Ref.RemoveRowSwap(row); // fixes rows/locations of the swapped entity
            RemoveRowAt(listPos);
        }

        /// <summary>Removes the entity's row from this archetype, disposing the storage row's components.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void DestroyEntity(int row) {
            var entity = storagePtr.Ref.packedEntities.Ptr[row];
            var listPos = world->entityLocations.Ptr[entity].listPos;
            storagePtr.Ref.DestroyRowSwap(row);
            RemoveRowAt(listPos);
        }

        /// <summary>
        /// Moves an entity from this logical archetype to the target. When storages are the same
        /// (only tags/pools changed) no row data is touched — only rows-list membership.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MoveEntityTo(int row, ref ArchetypeUnsafe target) {
            ref var srcStorage = ref storagePtr.Ref;
            if (srcStorage.packedEntities.Ptr == null || row < 0 || row >= srcStorage.count) return;
            world->version++;
            var entityID = srcStorage.packedEntities.Ptr[row];
            ref var loc = ref world->entityLocations.Ptr[entityID];

            int newRow;
            if (storagePtr.Ptr != target.storagePtr.Ptr) {
                ref var dstStorage = ref target.storagePtr.Ref;
                newRow = dstStorage.AllocateRow(entityID);

                for (var i = 0; i < srcStorage.inlineTypes.length; i++) {
                    var typeIndex = srcStorage.inlineTypes.Ptr[i];
                    var srcOff = srcStorage.componentOffsets.Ptr[i];
                    if (!dstStorage.offsetMap.Mask.HasFast(typeIndex)) continue;
                    var dstOff = dstStorage.offsetMap.GetRef(typeIndex);
                    if (dstOff < 0 || dstStorage.data.Ptr == null) continue;
                    var size = ComponentTypeMap.GetComponentType(typeIndex).size;
                    var src = srcStorage.data.Ptr + srcOff + row * size;
                    var dst = dstStorage.data.Ptr + dstOff + newRow * size;
                    memcpy(dst, src, size);
                }

                RemoveEntity(row); // swap-removes the old storage row, fixes rows of the swapped entity
            } else {
                newRow = row; // same storage — data stays in place
                RemoveRowAt(loc.listPos);
            }

            var listPos = target.AddRow(newRow);
            loc.archetypeIndex = target.index;
            loc.row = newRow;
            loc.listPos = listPos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CopyNewComponentsFromPool(int newRow, int entityID)
        {
            for (var j = 0; j < types.length; j++)
            {
                var typeIndex = types.Ptr[j];
                if (ComponentTypeMap.GetCategory(typeIndex) != ComponentCategory.Inline) continue;
                var dst = GetComponentDataPtr(typeIndex, newRow);
                if (dst == null) continue;
                ref var pool = ref world->GetUntypedPool(typeIndex);
                var src = pool.UnsafeBuffer->GetPtr(entityID);
                if (src != null)
                    memcpy(dst, src, ComponentTypeMap.GetComponentType(typeIndex).size);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref QueryUnsafe IdToQueryRef(int qId)
        {
            return ref world->queries.Ptr[qId].Ref;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref ptr<QueryUnsafe> Query(int qId)
        {
            return ref world->queries.Ptr[qId];
        }

        internal static void Destroy(ArchetypeUnsafe* archetype)
        {
            archetype->inlineMask.Dispose();
            archetype->tagMask.Dispose();
            archetype->poolMask.Dispose();
            archetype->rows.Dispose();
            archetype->types.Dispose();
            archetype->queries.Dispose();
            // storage is shared between logical archetypes — not disposed here
            var worldPtr = archetype->world;
            worldPtr->_free(archetype);
            archetype->world = null;
        }
        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, int index, ref Span<int> types)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>(1, AllocatorTags.Archetype);
            *ptr.Ptr = new ArchetypeUnsafe(world, index, ref types);
            return ptr;
        }
        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, int index, int[] typesSpan = null)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>(1, AllocatorTags.Archetype);
            *ptr.Ptr = new ArchetypeUnsafe(world, index, typesSpan);
            return ptr;
        }

        internal static ptr<ArchetypeUnsafe> CreatePtrFromBitmask(World.WorldUnsafe* world, int index, ref DynamicBitmask bitmask)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>(1, AllocatorTags.Archetype);
            ref var arch = ref *ptr.Ptr;
            arch._spinner = new Spinner();
            arch.world = world;
            arch.index = index;
            arch.storagePtr = default;
            arch.rows = default;
            arch.inlineMask = DynamicBitmask.CreateForComponents(world);
            arch.tagMask = DynamicBitmask.CreateForComponents(world);
            arch.poolMask = DynamicBitmask.CreateForComponents(world);
            arch.types = new MemoryList<int>(bitmask.Count, ref world->AllocatorRef);
            bitmask.ExtractSetBits(ref arch.types, ref world->AllocatorRef);
            foreach (var type in arch.types)
                AddTypeToMasks(ref arch.inlineMask, ref arch.tagMask, ref arch.poolMask, type);
            arch.hashId = ComputeIdentityHash(ref arch.inlineMask, ref arch.tagMask, ref arch.poolMask);
            arch.queries = new MemoryList<int>(8, ref world->AllocatorRef);
            arch.pairEdges = new HashMap<long, ptr<Edge>>(8, ref world->AllocatorHandler);
            arch.queriesVersion = 0;
            arch.destroyEdge = default;
            arch.PopulateQueries(world);
            arch.destroyEdge = arch.CreateDestroyEdge();
            arch.storagePtr = world->GetOrCreateStorage(ref arch.inlineMask);
            arch.rows = new MemoryList<int>(8, ref world->AllocatorRef);
            return ptr;
        }

        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, ref MemoryList<int> typesSpan, int index,
            bool copyList = false)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>(1, AllocatorTags.Archetype);
            *ptr.Ptr = new ArchetypeUnsafe(world, ref typesSpan, index, copyList);
            return ptr;
        }
        internal ArchetypeUnsafe(World.WorldUnsafe* world, int index, int[] typesSpan = default)
        {
            _spinner = new Spinner();
            this.world = world;
            inlineMask = DynamicBitmask.CreateForComponents(world);
            tagMask = DynamicBitmask.CreateForComponents(world);
            poolMask = DynamicBitmask.CreateForComponents(world);
            hashId = 0;
            this.index = index;
            storagePtr = default;
            rows = default;
            if (typesSpan != null)
            {
                types = new MemoryList<int>(typesSpan.Length, ref world->AllocatorRef);
                foreach (var type in typesSpan)
                {
                    types.Add(type, ref world->AllocatorRef);
                }
                foreach (var type in types) AddTypeToMasks(ref inlineMask, ref tagMask, ref poolMask, type);
                SortTypes(ref types);
                hashId = ComputeIdentityHash(ref inlineMask, ref tagMask, ref poolMask);
            }
            else
            {
                // Root Archetype
                types = new MemoryList<int>(1, ref world->AllocatorRef);
            }

            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            pairEdges = new HashMap<long, ptr<Edge>>(8, ref this.world->AllocatorHandler);
            queriesVersion = 0;
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
            storagePtr = world->GetOrCreateStorage(ref inlineMask);
            rows = new MemoryList<int>(8, ref world->AllocatorRef);
        }
        internal ArchetypeUnsafe(World.WorldUnsafe* world, int index, ref Span<int> typesSpan)
        {
            _spinner = new Spinner();
            this.world = world;
            inlineMask = DynamicBitmask.CreateForComponents(world);
            tagMask = DynamicBitmask.CreateForComponents(world);
            poolMask = DynamicBitmask.CreateForComponents(world);
            hashId = 0;
            this.index = index;
            storagePtr = default;
            rows = default;
            if (typesSpan.Length > 0)
            {
                types = new MemoryList<int>(typesSpan.Length, ref world->AllocatorRef);
                foreach (var type in typesSpan)
                {
                    types.Add(type, ref world->AllocatorRef);
                }
                foreach (var type in types) AddTypeToMasks(ref inlineMask, ref tagMask, ref poolMask, type);
                SortTypes(ref types);
                hashId = ComputeIdentityHash(ref inlineMask, ref tagMask, ref poolMask);
            }
            else
            {
                // Root Archetype
                types = new MemoryList<int>(1, ref world->AllocatorRef);
            }

            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            pairEdges = new HashMap<long, ptr<Edge>>(8, ref this.world->AllocatorHandler);
            queriesVersion = 0;
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
            storagePtr = world->GetOrCreateStorage(ref inlineMask);
            rows = new MemoryList<int>(8, ref world->AllocatorRef);
        }

        internal ArchetypeUnsafe(World.WorldUnsafe* world, ref MemoryList<int> typesSpan, int index, bool copyList = false)
        {
            _spinner = new Spinner();
            this.world = world;
            this.index = index;
            storagePtr = default;
            rows = default;
            inlineMask = DynamicBitmask.CreateForComponents(world);
            tagMask = DynamicBitmask.CreateForComponents(world);
            poolMask = DynamicBitmask.CreateForComponents(world);
            if (typesSpan.IsCreated)
            {
                types = typesSpan;
                foreach (var type in typesSpan) AddTypeToMasks(ref inlineMask, ref tagMask, ref poolMask, type);
                SortTypes(ref types);
            }
            else
            {
                // Root Archetype
                types = new MemoryList<int>(1, ref world->AllocatorRef);
            }

            hashId = ComputeIdentityHash(ref inlineMask, ref tagMask, ref poolMask);
            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            pairEdges = new HashMap<long, ptr<Edge>>(8, ref this.world->AllocatorHandler);
            queriesVersion = 0;
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
            if (copyList)
            {
                types = new MemoryList<int>(typesSpan.length, ref world->AllocatorRef);
                types.CopyFrom(ref typesSpan, ref world->AllocatorRef);
            }
            storagePtr = world->GetOrCreateStorage(ref inlineMask);
            rows = new MemoryList<int>(8, ref world->AllocatorRef);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref Entity CreateEntity()
        {
            ref var e = ref world->CreateEntity(index);
            var row = storagePtr.Ref.AllocateRow(e.id);
            var listPos = AddRow(row);
            world->entityLocations.ElementAt(e.id) = new EntityLocation {
                archetypeIndex = index,
                row = row,
                listPos = listPos
            };
            for (var i = 0; i < queries.Length; i++) IdToQueryRef(queries.Ptr[i]).Add(e.id);
            return ref e;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span<Entity> BatchCreateEntity(int amount)
        {
            return BatchCreateEntity(world->lastEntityIndex, world->lastEntityIndex + amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span<Entity> BatchCreateEntity(int start, int end)
        {
            var result = world->BatchCreateEntity(start, end, index);
            var cnt = end - start;
            ref var st = ref storagePtr.Ref;
            st.EnsureCapacity(cnt);
            var baseRow = st.count;
            for (int i = 0; i < cnt; i++)
            {
                var row = baseRow + i;
                st.packedEntities.Ptr[row] = start + i;
                var listPos = AddRow(row);
                world->entityLocations.ElementAt(start + i) = new EntityLocation {
                    archetypeIndex = index, row = row, listPos = listPos
                };
            }
            for (var j = 0; j < st.inlineTypes.length; j++)
            {
                var off = st.componentOffsets.Ptr[j];
                var ctData = ComponentTypeMap.GetComponentType(st.inlineTypes.Ptr[j]);
                mem_clear(st.data.Ptr + off + baseRow * ctData.size, ctData.size * cnt);
            }
            st.count += cnt;
            for (var i = 0; i < queries.Length; i++)
                IdToQueryRef(queries.Ptr[i]).BatchAddRange(start, cnt);
            for (var i = 0; i < types.length; i++)
            {
                var ctData = ComponentTypeMap.GetComponentType(types.Ptr[i]);
                if (ctData.storageType != StorageType.Pool) continue;
                world->GetUntypedPool(types[i]).UnsafeBufferPtr.Ref.BatchAdd(start, end);
            }
            return result;
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void BatchCloneEntity(int srcEntityId, Span<Entity> outEntities)
        {
            var cnt = outEntities.Length;
            var ids = stackalloc int[cnt];
            world->BatchCreateEntity(cnt, ids, index);

            ref var st = ref storagePtr.Ref;
            st.EnsureCapacity(cnt);
            var baseRow = st.count;
            var srcRow = world->entityLocations.Ptr[srcEntityId].row;

            for (int i = 0; i < cnt; i++)
            {
                var row = baseRow + i;
                st.packedEntities.Ptr[row] = ids[i];
                var listPos = AddRow(row);
                ref var loc = ref world->entityLocations.Ptr[ids[i]];
                loc.row = row;
                loc.listPos = listPos;
                outEntities[i] = new Entity(ids[i], world->Id);
            }

            for (var j = 0; j < st.inlineTypes.length; j++)
            {
                var off = st.componentOffsets.Ptr[j];
                var typeIndex = st.inlineTypes.Ptr[j];
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                var srcPtr = st.data.Ptr + off + srcRow * ctData.size;
                var baseDst = st.data.Ptr + off + baseRow * ctData.size;
                if (ctData.isCopyable)
                {
                    for (var i = 0; i < cnt; i++)
                    {
                        ctData.CopyFn().Invoke(srcPtr,
                            baseDst + i * ctData.size,
                            0,
                            ids[i],
                            0,
                            0);
                    }
                    continue;
                }
                for (var i = 0; i < cnt; i++)
                {
                    memcpy(baseDst + i * ctData.size, srcPtr, ctData.size);
                }
            }
            st.count += cnt;

            for (var i = 0; i < types.length; i++)
            {
                var typeIndex = types.Ptr[i];
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                if (ctData.storageType != StorageType.Pool) continue;
                ref var pool = ref world->GetUntypedPool(typeIndex);
                for (int k = 0; k < cnt; k++)
                {
                    pool.Copy(srcEntityId, ids[k]);
                }
            }

            for (var i = 0; i < queries.Length; i++)
                IdToQueryRef(queries.Ptr[i]).BatchAddRange(0, cnt);

        }

        internal void Refresh()
        {
            queries.Clear();
            PopulateQueries(world);
            queriesVersion++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CheckQuery(in ptr<QueryUnsafe> query)
        {
            if(index == 0) return;

            ref var q = ref query.Ref;
            var matches = 0;
            var hasNone = false;
            foreach (var type in types)
            {
                if (q.HasNone(type))
                {
                    hasNone = true;
                    break;
                }
            }

            if (hasNone) return;
            foreach (var type in types)
            {
                if (q.HasWith(type))
                {
                    matches++;
                    if (matches == q.with.Count)
                    {
                        q.AddArchetype(index);
                        queries.Add(q.Id, ref world->AllocatorRef);
                        queriesVersion++;
                        q.BatchAdd(packedEntities.Ptr, count);
                        break;
                    }
                }
            }
        }
        internal void PopulateQueries(World.WorldUnsafe* worldPtr)
        {
            if(index == 0) return;

            for (var i = 0; i < worldPtr->queries.Length; i++)
            {
                ref var q = ref worldPtr->queries[i];
                var matches = 0;
                var hasNone = false;
                foreach (var type in types)
                {
                    if (q.Ptr->HasNone(type))
                    {
                        hasNone = true;
                        break;
                    }
                }

                if (hasNone) continue;
                foreach (var type in types)
                {
                    if (q.Ptr->HasWith(type))
                    {
                        matches++;
                        if (matches == q.Ptr->with.Count)
                        {
                            q.Ref.AddArchetype(index);
                            queries.Add(q.Ptr->Id, ref worldPtr->AllocatorRef);
                            queriesVersion++;
                            break;
                        }
                    }
                }
            }
        }

        private Edge CreateDestroyEdge()
        {
            var edge = new Edge(ref world->AllocatorRef);
            for (var i = 0; i < queries.Length; i++)
                edge.removeEntity.Add(in Query(queries.ElementAt(i)), ref world->AllocatorRef);
            return edge;
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void Copy(int from, int to)
        {
            for (var i = 0; i < queries.Length; i++)
            {
                var queryId = queries.ElementAt(i);
                Query(queryId).Ref.Add(to);
            }

            var fromRow = world->entityLocations.Ptr[from].row;
            var toRow = world->entityLocations.Ptr[to].row;

            for (var i = 0; i < types.length; i++)
            {
                var typeIndex = types.Ptr[i];
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                if (ctData.storageType == StorageType.Pool)
                {
                    ref var pool = ref world->GetUntypedPool(typeIndex);
                    pool.Copy(from, to);
                }
                else if (ctData.category == ComponentCategory.Inline)
                {
                    var src = GetComponentDataPtr(typeIndex, fromRow);
                    var dst = GetComponentDataPtr(typeIndex, toRow);
                    if (src != null && dst != null)
                        memcpy(dst, src, ctData.size);
                }
            }
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Entity Copy(int entity)
        {
            var newEntity = world->CreateEntity(index);
            var newRow = storagePtr.Ref.AllocateRow(newEntity.id);
            var listPos = AddRow(newRow);
            world->entityLocations.Ptr[newEntity.id] = new EntityLocation {
                archetypeIndex = index,
                row = newRow,
                listPos = listPos
            };
            for (var i = 0; i < queries.Length; i++)
            {
                var queryId = queries.ElementAt(i);
                Query(queryId).Ref.Add(newEntity.id);
            }

            var srcRow = world->entityLocations.Ptr[entity].row;

            for (var i = 0; i < types.length; i++)
            {
                var typeIndex = types.Ptr[i];
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                if (ctData.storageType == StorageType.Pool)
                {
                    ref var pool = ref world->GetUntypedPool(typeIndex);
                    pool.Copy(entity, newEntity.id);
                }
                else if (ctData.category == ComponentCategory.Inline)
                {
                    var src = GetComponentDataPtr(typeIndex, srcRow);
                    var dst = GetComponentDataPtr(typeIndex, newRow);
                    if (src != null && dst != null)
                        memcpy(dst, src, ctData.size);
                }
            }

            if (Has(ComponentType<ComponentArray<Child>>.Index))
            {
                ref var fromC = ref GetComponent<ComponentArray<Child>>(entity);
                ref var to = ref GetComponent<ComponentArray<Child>>(newEntity.id);

                for (var i = 0; i < fromC.Length; i++)
                {
                    ref var child = ref fromC.ElementAt(i);
                    ref var childNew = ref to.ElementAt(i);
                    childNew.Value = child.Value.Copy();
                    childNew.Value.Get<ChildOf>().Value = newEntity;
                }
            }

            return newEntity;
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Entity Copy(in Entity entity)
        {
            var newEntity = world->CreateEntity(index);
            var newRow = storagePtr.Ref.AllocateRow(newEntity.id);
            var listPos = AddRow(newRow);
            world->entityLocations.Ptr[newEntity.id] = new EntityLocation {
                archetypeIndex = index,
                row = newRow,
                listPos = listPos
            };
            for (var i = 0; i < queries.Length; i++)
            {
                var queryId = queries.ElementAt(i);
                Query(queryId).Ref.Add(newEntity.id);
            }

            var srcRow = world->entityLocations.Ptr[entity.id].row;

            for (var i = 0; i < types.length; i++)
            {
                var typeIndex = types.Ptr[i];
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                if (ctData.storageType == StorageType.Pool)
                {
                    ref var pool = ref world->GetUntypedPool(typeIndex);
                    pool.Copy(entity.id, newEntity.id);
                }
                else if (ctData.category == ComponentCategory.Inline)
                {
                    var src = GetComponentDataPtr(typeIndex, srcRow);
                    var dst = GetComponentDataPtr(typeIndex, newRow);
                    if (src != null && dst != null)
                    {
                        if (ctData.isCopyable)
                        {
                            ctData.CopyFn().Invoke(src, dst, entity.id, newEntity.id, 0, 0);
                        }
                        else
                        {
                            memcpy(dst, src, ctData.size);
                        }
                    }
                }
            }

            if (Has(ComponentType<ComponentArray<Child>>.Index))
            {
                ref var fromC = ref GetComponent<ComponentArray<Child>>(entity.id);
                ref var to = ref GetComponent<ComponentArray<Child>>(newEntity.id);

                for (var i = 0; i < fromC.Length; i++)
                {
                    ref var child = ref fromC.ElementAt(i);
                    ref var childNew = ref to.ElementAt(i);
                    childNew.Value = child.Value.Copy();
                    childNew.Value.Get<ChildOf>().Value = newEntity;
                }
            }

            return newEntity;
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Destroy(int entity)
        {
            if (Has(ComponentType<ComponentArray<Child>>.Index))
            {
                ref var pool = ref world->GetPool<ComponentArray<Child>>();
                ref var children = ref pool.GetRef<ComponentArray<Child>>(entity);
                foreach (ref var child in children)
                {
                    child.Value.Destroy();
                }
            }
            for (var idx = 0; idx < types.length; idx++)
            {
                ref var pool = ref world->GetUntypedPool(types[idx]);
                pool.Remove(entity);
            }

            destroyEdge.Execute(entity);
            world->OnDestroyEntity(entity);
        }


        internal void SetEntityData(EntityData eData)
        {
            var loc = world->entityLocations.Ptr[eData.Entity];
            for (var i = 0; i < eData.Components.Length; i++)
            {
                var typeIndex = types[i];
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                if (ctData.storageType == StorageType.Pool)
                {
                    ref var pool = ref world->GetUntypedPool(typeIndex);
                    pool.WriteBytes(eData.Entity, eData.Components[i]);
                }
                else if (ctData.category == ComponentCategory.Inline)
                {
                    var dst = GetComponentDataPtr(typeIndex, loc.row);
                    if (dst != null)
                    {
                        fixed (byte* src = eData.Components[i])
                            memcpy(dst, src, ctData.size);
                    }
                }
            }
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void OnEntityFree(int entity)
        {
            for (var idx = 0; idx < types.length; idx++)
            {
                var ctData = ComponentTypeMap.GetComponentType(types[idx]);
                if (ctData.storageType == StorageType.Pool)
                {
                    ref var pool = ref world->GetUntypedPool(types[idx]);
                    pool.Remove(entity);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void BatchMigrateQueries(
            ref ArchetypeUnsafe from, ref ArchetypeUnsafe to, int entity)
        {
            // Pair-edge cache: remove/add query lists are precomputed once per (from→to)
            // transition and applied linearly per entity — instead of rescanning both
            // attached-query lists with linear Contains on every migration (quadratic in Q).
            var key = (long)to.index << 32 | (uint)from.index;
            if (!from.pairEdges.TryGetValue(key, out var edge))
            {
                var e = new Edge(ref from.world->AllocatorRef);
                FillPairEdge(ref e, ref from, ref to);
                edge = from.world->_allocate_ptr<Edge>(1, AllocatorTags.Archetype);
                edge.Ref = e;
                from.pairEdges.TryAdd(key, edge);
            }
            else if (edge.Ref.fromQueriesVersion != from.queriesVersion
                     || edge.Ref.toQueriesVersion != to.queriesVersion)
            {
                FillPairEdge(ref edge.Ref, ref from, ref to);
            }

            edge.Ref.Execute(entity);
        }

        private static void FillPairEdge(ref Edge edge, ref ArchetypeUnsafe from, ref ArchetypeUnsafe to)
        {
            edge.removeEntity.Clear();
            for (var i = 0; i < from.queries.Length; i++)
            {
                var qId = from.queries[i];
                if (!to.queries.Contains(qId))
                    edge.removeEntity.Add(from.Query(qId), ref from.world->AllocatorRef);
            }
            edge.addEntity.Clear();
            for (var i = 0; i < to.queries.Length; i++)
            {
                var qId = to.queries[i];
                if (!from.queries.Contains(qId))
                    edge.addEntity.Add(to.Query(qId), ref to.world->AllocatorRef);
            }
            edge.fromQueriesVersion = from.queriesVersion;
            edge.toQueriesVersion = to.queriesVersion;
        }

        internal System.Collections.Generic.List<IComponent> GetAllComponents(int entity, System.Collections.Generic.List<IComponent> buffer)
        {
            var loc = world->entityLocations.Ptr[entity];
            foreach (var typeIndex in types)
            {
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                if (ctData.storageType == StorageType.Pool)
                {
                    buffer.Add(world->GetUntypedPool(typeIndex).GetObject(entity));
                }
                else
                {
                    var src = GetComponentDataPtr(typeIndex, loc.row);
                    if (src != null)
                        buffer.Add(ComponentHelpers.Read(src, 0, ctData.size, typeIndex));
                }
            }

            return buffer;
        }
        // [BurstDiscard]
        // public override string ToString()
        // {
        //     var sb = new StringBuilder();
        //     sb.Append("<color=#FFB200>Archetype</color>");
        //     if (mask.Count == 0)
        //     {
        //         sb.Append(".Empty");
        //         return sb.ToString();
        //     }
        //
        //     for (var i = 0; i < types.Length; i++) sb.Append($"[{ComponentTypeMap.GetType(types[i]).Name}]");
        //
        //     sb.Append(Environment.NewLine);
        //     for (var index = 0; index < queries.Length; index++)
        //     {
        //         ref var ptr = ref Query(queries.ElementAt(index));
        //         sb.Append($"<color=#6CFF6C>{ptr.Ref.ToString()}</color>;{Environment.NewLine}");
        //     }
        //
        //     return sb.ToString();
        // }
    }
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ArchetypeComponentMetaData
    {
        public readonly int localIndex;
        public readonly int offset;

        public ArchetypeComponentMetaData(int localIndex, int offset)
        {
            this.localIndex = localIndex;
            this.offset = offset;
        }
    }
    public static class WorldArchetypeExtensions
    {
        public static unsafe Archetype GetArchetype(this ref World world, params Type[] types)
        {
            Span<int> span = stackalloc int[types.Length];
            for (var i = span.Length - 1; i >= 0; i--)
            {
                span[i] = ComponentTypeMap.Index(types[i]);
            }
            return world.UnsafeWorldRef.GetOrCreateArchetype(ref span);
        }
        public static Archetype GetArchetype(this ref World world, in Entity entity)
        {
            var hash = entity.ArchetypeRef.hashId;
            return world.UnsafeWorldRef.GetArchetype(hash);
        }
    }
    [BurstCompile]
    public static class ArchetypePointerExtensions
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Has<T>(this ref ArchetypeUnsafe archetype) where T : unmanaged
        {
            if(!archetype.inlineMask.IsCreated) throw new Exception("Archetype mask is not created");
            return archetype.Has(ComponentType<T>.Index);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this ref ArchetypeUnsafe archetype, int type)
        {
            return archetype.Has(type);
        }
    }
    [Serializable]
    public struct EntityData
    {
        public int Entity;
        public byte[][] Components;
        /// Size of Components
        public int SizeInBytes;
    }
    internal unsafe struct Edge
    {
        internal MemoryList<ptr<QueryUnsafe>> addEntity;
        internal MemoryList<ptr<QueryUnsafe>> removeEntity;

        /// <summary>Attached-query set versions this edge's lists were built against
        /// (pair-edge cache invalidation, see BatchMigrateQueries).</summary>
        internal int fromQueriesVersion;
        internal int toQueriesVersion;

        public Edge(ref MemAllocator allocator)
        {
            addEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            removeEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            fromQueriesVersion = 0;
            toQueriesVersion = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Execute(int entity)
        {
            for (var i = 0; i < removeEntity.length; i++) removeEntity.ElementAt(i).Ref.Remove(entity);
            for (var i = 0; i < addEntity.length; i++) addEntity.ElementAt(i).Ref.Add(entity);
        }

        public void OnDeserialize(ref MemAllocator alloc, World.WorldUnsafe* w)
        {
            addEntity.OnDeserialize(ref alloc);
            foreach (ref var ptr in addEntity)
            {
                ptr.OnDeserialize(ref alloc);
            }
            removeEntity.OnDeserialize(ref alloc);
            foreach (ref var ptr in removeEntity)
            {
                ptr.OnDeserialize(ref alloc);
            }
        }
    }
}