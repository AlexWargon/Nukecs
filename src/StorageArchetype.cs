using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;

namespace Wargon.Nukecs {
    /// <summary>
    /// Owner of the entity data buffer for a given inline-component set.
    /// Shared by all logical archetypes that have the same inline mask but differ in tag/pool masks,
    /// so adding/removing a tag or pool component never moves row data — only archetype membership.
    /// Layout is defined solely by inline types: componentOffsets/offsetMap are indexed by
    /// rank within inlineTypes (tags and pool types hold no bytes here).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct StorageArchetype {
        public ptr<byte> data;
        public MemoryArray<int> packedEntities;
        public MemoryArray<int> componentOffsets;
        internal BitMap1024<int> offsetMap;
        public int count;
        public int capacity;
        public int entityStride;
        internal int index;
        /// <summary>Number of logical archetypes sharing this storage. 1 → rows are dense (iterator fast path).</summary>
        internal int refCount;
        /// <summary>Indices (into world->archetypesList) of logical archetypes sharing this storage.
        /// Used by storage-mode queries to check tag/pool none-filters per logical archetype.</summary>
        public MemoryList<int> logicalArchetypes;
        internal DynamicBitmask inlineMask;
        internal MemoryList<int> inlineTypes;
        [NativeDisableUnsafePtrRestriction] internal World.WorldUnsafe* world;

        internal bool IsCreated => world != null;

        internal void OnDeserialize(ref MemAllocator allocator, World.WorldUnsafe* worldPtr) {
            world = worldPtr;
            inlineMask.OnDeserialize(ref allocator);
            inlineTypes.OnDeserialize(ref allocator);
            logicalArchetypes.OnDeserialize(ref allocator);
            packedEntities.OnDeserialize(ref allocator);
            data.OnDeserialize(ref allocator);
            componentOffsets.OnDeserialize(ref allocator);
            offsetMap.OnDeserialize(ref allocator);
        }

        internal static ptr<StorageArchetype> CreatePtr(World.WorldUnsafe* world, int index, ref DynamicBitmask inlineMaskSrc) {
            var ptr = world->_allocate_ptr<StorageArchetype>();
            ref var st = ref ptr.Ref;
            st.world = world;
            st.index = index;
            st.count = 0;
            st.capacity = 0;
            st.refCount = 1;
            st.data = default;
            st.packedEntities = default;
            st.componentOffsets = default;
            st.offsetMap = default;
            st.entityStride = 0;
            st.inlineMask = DynamicBitmask.CreateForComponents(world);
            st.inlineMask.CopyFrom(ref inlineMaskSrc);
            st.inlineTypes = new MemoryList<int>(inlineMaskSrc.Count, ref world->AllocatorRef);
            inlineMaskSrc.ExtractSetBits(ref st.inlineTypes, ref world->AllocatorRef);
            st.logicalArchetypes = new MemoryList<int>(2, ref world->AllocatorRef);
            st.InitPackedArrays(64);
            return ptr;
        }

        internal void InitPackedArrays(int initialCapacity) {
            count = 0;
            capacity = initialCapacity;

            packedEntities = new MemoryArray<int>(capacity, ref world->AllocatorRef, clear: true);

            if (inlineTypes.length == 0) return;

            entityStride = 0;
            for (var i = 0; i < inlineTypes.length; i++)
                entityStride += ComponentTypeMap.GetComponentType(inlineTypes.Ptr[i]).size;

            if (entityStride > 0) {
                data = world->AllocatorRef.AllocatePtr<byte>(entityStride * capacity);
                mem_clear(data.Ptr, entityStride * capacity);
            }

            componentOffsets = new MemoryArray<int>(inlineTypes.length, ref world->AllocatorRef, clear: true);
            var offset = 0;
            for (var i = 0; i < inlineTypes.length; i++) {
                componentOffsets.Ptr[i] = offset;
                offset += ComponentTypeMap.GetComponentType(inlineTypes.Ptr[i]).size * capacity;
            }

            offsetMap = new BitMap1024<int>(inlineTypes.length, ref world->AllocatorRef);
            for (var i = 0; i < inlineTypes.length; i++)
                offsetMap.Add(inlineTypes.Ptr[i], componentOffsets.Ptr[i], ref world->AllocatorRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureCapacity(int needed) {
            if (inlineTypes.length == 0) {
                packedEntities.EnsureCapacity(count + needed, ref world->AllocatorRef);
                return;
            }
            if (count + needed <= capacity) return;
            var newCapacity = capacity * 2;
            if (newCapacity < count + needed) newCapacity = count + needed;

            packedEntities.EnsureCapacity(newCapacity, ref world->AllocatorRef);

            if (entityStride == 0) {
                capacity = newCapacity;
                return;
            }

            var newData = world->AllocatorRef.AllocatePtr<byte>(entityStride * newCapacity);
            var newOffsets = new MemoryArray<int>(inlineTypes.length, ref world->AllocatorRef, clear: true);

            var oldOffset = 0;
            var newOffset = 0;
            for (var i = 0; i < inlineTypes.length; i++) {
                var size = ComponentTypeMap.GetComponentType(inlineTypes.Ptr[i]).size;
                newOffsets.Ptr[i] = newOffset;
                memcpy(newData.Ptr + newOffset, data.Ptr + oldOffset, count * size);
                oldOffset += capacity * size;
                newOffset += newCapacity * size;
            }

            data = newData;
            componentOffsets = newOffsets;

            offsetMap = new BitMap1024<int>(inlineTypes.length, ref world->AllocatorRef);
            for (var i = 0; i < inlineTypes.length; i++)
                offsetMap.Add(inlineTypes.Ptr[i], newOffsets.Ptr[i], ref world->AllocatorRef);

            capacity = newCapacity;
        }

        /// <summary>Appends a zeroed row and returns its index.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int AllocateRow(int entityID) {
            EnsureCapacity(1);
            var row = count;
            packedEntities.Ptr[row] = entityID;

            for (var i = 0; i < inlineTypes.length; i++) {
                var off = componentOffsets.Ptr[i];
                var size = ComponentTypeMap.GetComponentType(inlineTypes.Ptr[i]).size;
                mem_clear(data.Ptr + off + row * size, size);
            }

            count++;
            world->version++;
            return row;
        }

        /// <summary>
        /// Swap-removes the row. Fixes the location (row and logical-archetype rows entry)
        /// of the entity that got moved into the vacated slot.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveRowSwap(int row) {
            count--;
            world->version++;
            if (row == count) return;

            packedEntities.Ptr[row] = packedEntities.Ptr[count];

            for (var i = 0; i < inlineTypes.length; i++) {
                var off = componentOffsets.Ptr[i];
                var size = ComponentTypeMap.GetComponentType(inlineTypes.Ptr[i]).size;
                var src = data.Ptr + off + count * size;
                var dst = data.Ptr + off + row * size;
                memcpy(dst, src, size);
            }

            FixSwappedEntityLocation(row);
        }

        /// <summary>
        /// Swap-removes the row, invoking dispose functions of the removed row's disposable
        /// components first. Fixes the location of the swapped-in entity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void DestroyRowSwap(int row) {
            for (var i = 0; i < inlineTypes.length; i++) {
                var ctData = ComponentTypeMap.GetComponentType(inlineTypes.Ptr[i]);
                if (ctData.isDisposable == false) continue;
                var off = componentOffsets.Ptr[i];
                var dst = data.Ptr + off + row * ctData.size;
                ctData.DisposeFn().Invoke(dst, 0);
            }

            count--;
            world->version++;
            if (row == count) return;

            packedEntities.Ptr[row] = packedEntities.Ptr[count];

            for (var i = 0; i < inlineTypes.length; i++) {
                var ctData = ComponentTypeMap.GetComponentType(inlineTypes.Ptr[i]);
                var off = componentOffsets.Ptr[i];
                var size = ctData.size;
                var src = data.Ptr + off + count * size;
                var dst = data.Ptr + off + row * size;
                memcpy(dst, src, size);
            }

            FixSwappedEntityLocation(row);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FixSwappedEntityLocation(int row) {
            var swappedEntity = packedEntities.Ptr[row];
            ref var loc = ref world->entityLocations.Ptr[swappedEntity];
            loc.row = row;
            ref var la = ref world->archetypesList.Ptr[loc.archetypeIndex].Ref;
            la.rows.Ptr[loc.listPos] = row;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentLocalIndex(int componentTypeIndex) {
            return offsetMap.Mask.CountBefore(componentTypeIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentOffset(int localIndex) {
            return componentOffsets.Ptr[localIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte* GetComponentDataPtr(int componentTypeIndex, int row) {
            if (!offsetMap.Mask.HasFast(componentTypeIndex)) return null;
            var off = offsetMap.GetRef(componentTypeIndex);
            if (off < 0) return null;
            return data.Ptr + off + row * ComponentTypeMap.GetComponentType(componentTypeIndex).size;
        }
    }
}
