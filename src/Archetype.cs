using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;

namespace Wargon.Nukecs
{
    public unsafe struct Archetype
    {
        internal ArchetypeUnsafe* impl => ptr.Ptr;
        internal ptr<ArchetypeUnsafe> ptr;
        public bool IsCreated => impl != null;
        internal bool Has<T>() where T : unmanaged
        {
            return impl->Has(ComponentType<T>.Index);
        }

        public Span<Entity> BatchCreateEntity(int count)
        {
            return ptr.Ref.BatchCreateEntity(count);
        }

        public ref Entity CreateEntity()
        {
            return ref ptr.Ref.CreateEntity();
        }
        public void Dispose()
        {
            //ArchetypeUnsafe.Destroy(impl);
        }

        public void SetArchetype(in Entity entity)
        {
            ptr.Ref.SetArchetype(entity);
        }

        public IComponent GetObject(in Entity entity, Type type)
        {
            return ptr.Ref.GetObject(entity.id, ComponentTypeMap.GetComponentType(type).index);
        }
        public struct Chunk
        {
            public MemoryArray<int> entities;
            public NativeArray<ptr<byte>> components;
            internal MemoryArray<int> componentOffsets;
            //
            // public ref T* GetComponent<T>(int entity, int size) where T : unmanaged, IComponent
            // {
            //     return components[componentOffsets.]
            // }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct EntityLocation {
        internal int archetypeIndex;
        internal int row;
    }

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct ArchetypeUnsafe
    {
        private Spinner spinner;
        internal DynamicBitmask mask;
        internal MemoryList<int> types;
        [NativeDisableUnsafePtrRestriction] internal World.WorldUnsafe* world;
        internal MemoryList<int> queries;
        internal HashMap<int, ptr<Edge>> transactions;
        internal Edge destroyEdge;
        internal int hashId;
        internal int index;

        public int count;
        public int capacity;
        public MemoryArray<int> packedEntities;
        public ptr<byte> data;
        public MemoryArray<int> componentOffsets;
        public int entityStride;

        internal bool IsCreated => world != null;

        internal void OnDeserialize(ref MemAllocator allocator, World.WorldUnsafe* worldPtr)
        {
            world = worldPtr;
            queries.OnDeserialize(ref allocator);
            transactions.OnDeserialize(ref allocator);
            destroyEdge.OnDeserialize(ref allocator, worldPtr);
            types.OnDeserialize(ref allocator);
            foreach (var kvPair in transactions)
            {
                kvPair.Value.OnDeserialize(ref allocator);
                ref var edge = ref kvPair.Value.Ref;
                edge.OnDeserialize(ref allocator, worldPtr);
            }
            mask.OnDeserialize(ref allocator);
        }

        internal void SetArchetype(in Entity entity)
        {
            ref var loc = ref world->entityLocations.Ptr[entity.id];
            ref var source = ref world->archetypesList.Ptr[loc.archetypeIndex].Ref;
            if (source.index == index) return;
            BatchMigrateQueries(ref source, ref this, entity.id);
            source.MoveEntityTo(loc.row, ref this);
        }
        internal ref T GetComponent<T>(int entity) where T : unmanaged, IComponent
        {
            ref var d = ref ComponentType<T>.Data;
            var off = componentOffsets.Ptr[d.index];
            ref var loc = ref world->entityLocations.Ptr[entity];
            return ref *(T*)(data.Ptr + off + loc.row * d.size);
        }

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
            if (localIdx < 0) return null;
            var off = componentOffsets.Ptr[localIdx];
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
            if (localIdx < 0) return;
            var off = componentOffsets.Ptr[localIdx];
            ref var loc = ref world->entityLocations.Ptr[entity];
            var ptr = data.Ptr + off + loc.row * d.size;
            ComponentHelpers.Write(ptr, 0, d.size, typeIndex, component);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte* GetComponentDataPtr(int componentTypeIndex, int row)
        {
            for (var i = 0; i < types.length; i++)
            {
                if (types.Ptr[i] == componentTypeIndex)
                {
                    var off = componentOffsets.Ptr[i];
                    if (off < 0) return null;
                    return data.Ptr + off + row * ComponentTypeMap.GetComponentType(componentTypeIndex).size;
                }
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentLocalIndex(int componentTypeIndex)
        {
            for (var i = 0; i < types.length; i++)
                if (types.Ptr[i] == componentTypeIndex) return i;
            throw new Exception("ComponentLocalIndex not found");
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentOffset(int localIndex)
        {
            return componentOffsets.Ptr[localIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetComponentSize(int localIndex)
        {
            return ComponentTypeMap.GetComponentType(types.Ptr[localIndex]).size;
        }

        private void InitPackedArrays(int initialCapacity)
        {
            count = 0;
            capacity = initialCapacity;

            packedEntities = new MemoryArray<int>(capacity, ref world->AllocatorRef, clear: true);

            if (types.length == 0) return;

            entityStride = 0;
            for (var i = 0; i < types.length; i++)
            {
                var ctData = ComponentTypeMap.GetComponentType(types.Ptr[i]);
                if (ctData.storageType == StorageType.Pool) continue;
                entityStride += ctData.size;
            }

            if (entityStride > 0)
            {
                data = world->AllocatorRef.AllocatePtr<byte>(entityStride * capacity);
                mem_clear(data.Ptr, entityStride * capacity);
            }

            componentOffsets = new MemoryArray<int>(types.length, ref world->AllocatorRef, clear: true);
            var offset = 0;
            for (var i = 0; i < types.length; i++)
            {
                var ctData = ComponentTypeMap.GetComponentType(types.Ptr[i]);
                if (ctData.storageType == StorageType.Pool)
                {
                    componentOffsets.Ptr[i] = -1;
                    continue;
                }
                componentOffsets.Ptr[i] = offset;
                offset += ctData.size * capacity;
            }
        }

        internal void EnsureCapacity(int needed)
        {
            if (types.length == 0) return;
            if (count + needed <= capacity) return;
            var newCapacity = capacity * 2;
            if (newCapacity < count + needed) newCapacity = count + needed;

            packedEntities.EnsureCapacity(newCapacity, ref world->AllocatorRef);

            if (entityStride == 0)
            {
                capacity = newCapacity;
                return;
            }

            var newData = world->AllocatorRef.AllocatePtr<byte>(entityStride * newCapacity);
            var newOffsets = new MemoryArray<int>(types.length, ref world->AllocatorRef, clear: true);

            var oldOffset = 0;
            var newOffset = 0;
            for (var i = 0; i < types.length; i++)
            {
                var ctData = ComponentTypeMap.GetComponentType(types.Ptr[i]);
                if (ctData.storageType == StorageType.Pool)
                {
                    newOffsets.Ptr[i] = -1;
                    continue;
                }
                var size = ctData.size;
                newOffsets.Ptr[i] = newOffset;
                UnsafeUtility.MemCpy(newData.Ptr + newOffset, data.Ptr + oldOffset, count * size);
                oldOffset += capacity * size;
                newOffset += newCapacity * size;
            }

            data = newData;
            componentOffsets = newOffsets;
            capacity = newCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int AllocateEntity(int entityID)
        {
            EnsureCapacity(1);
            var row = count;
            packedEntities.Ptr[row] = entityID;

            for (var i = 0; i < types.length; i++)
            {
                var off = componentOffsets.Ptr[i];
                if (off < 0) continue;
                var ctData = ComponentTypeMap.GetComponentType(types.Ptr[i]);
                var dst = data.Ptr + off + row * ctData.size;
                UnsafeUtility.MemClear(dst, ctData.size);
            }

            count++;
            return row;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveEntity(int row)
        {
            count--;
            if (row == count) return;

            packedEntities.Ptr[row] = packedEntities.Ptr[count];

            for (var i = 0; i < types.length; i++)
            {
                var off = componentOffsets.Ptr[i];
                if (off < 0) continue;
                var ctData = ComponentTypeMap.GetComponentType(types.Ptr[i]);
                var size = ctData.size;
                var src = data.Ptr + off + count * size;
                var dst = data.Ptr + off + row * size;
                UnsafeUtility.MemCpy(dst, src, size);
            }

            var swappedEntity = packedEntities.Ptr[row];
            world->entityLocations.Ptr[swappedEntity].row = row;
        }

        internal void MoveEntityTo(int row, ref ArchetypeUnsafe target)
        {
            if (packedEntities.Ptr == null || row < 0 || row >= count) return;
            world->version++;
            var entityID = packedEntities.Ptr[row];
            var newRow = target.AllocateEntity(entityID);

            for (var i = 0; i < types.length; i++)
            {
                var typeIndex = types.Ptr[i];
                var srcOff = componentOffsets.Ptr[i];
                if (srcOff < 0 || data.Ptr == null) continue;
                var srcSize = ComponentTypeMap.GetComponentType(typeIndex).size;
                var src = data.Ptr + srcOff + row * srcSize;

                for (var j = 0; j < target.types.length; j++)
                {
                    if (target.types.Ptr[j] == typeIndex)
                    {
                        var dstOff = target.componentOffsets.Ptr[j];
                        if (dstOff < 0 || target.data.Ptr == null) break;
                        var dst = target.data.Ptr + dstOff + newRow * srcSize;
                        UnsafeUtility.MemCpy(dst, src, srcSize);
                        break;
                    }
                }
            }

            world->entityLocations.Ptr[entityID] = new EntityLocation {
                archetypeIndex = target.index,
                row = newRow
            };
            world->entitiesArchetypes.Ptr[entityID] = target.index;

            RemoveEntity(row);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CopyNewComponentsFromPool(int newRow, int entityID)
        {
            for (var j = 0; j < types.length; j++)
            {
                var off = componentOffsets.Ptr[j];
                if (off < 0) continue;
                var typeIndex = types.Ptr[j];
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                var dst = data.Ptr + off + newRow * ctData.size;
                ref var pool = ref world->GetUntypedPool(typeIndex);
                var src = pool.UnsafeBuffer->GetPtr(entityID);
                if (src != null)
                    UnsafeUtility.MemCpy(dst, src, ctData.size);
                else
                    UnsafeUtility.MemClear(dst, ctData.size);
            }
        }

        private ref QueryUnsafe IdToQueryRef(int qId)
        {
            return ref world->queries.Ptr[qId].Ref;
        }

        private ref ptr<QueryUnsafe> Query(int qId)
        {
            return ref world->queries.Ptr[qId];
        }

        internal static void Destroy(ArchetypeUnsafe* archetype)
        {
            archetype->mask.Dispose();
            archetype->types.Dispose();
            archetype->queries.Dispose();
            archetype->transactions.Dispose();
            var worldPtr = archetype->world;
            worldPtr->_free(archetype);
            archetype->world = null;
        }
        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, int index, ref Span<int> types)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>();
            *ptr.Ptr = new ArchetypeUnsafe(world, index, ref types);
            return ptr;
        }
        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, int index, int[] typesSpan = null)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>();
            *ptr.Ptr = new ArchetypeUnsafe(world, index, typesSpan);
            return ptr;
        }

        internal static ptr<ArchetypeUnsafe> CreatePtrFromBitmask(World.WorldUnsafe* world, int index, ref DynamicBitmask bitmask)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>();
            ref var arch = ref *ptr.Ptr;
            arch.spinner = new Spinner();
            arch.world = world;
            arch.index = index;
            arch.count = 0;
            arch.capacity = 0;
            arch.packedEntities = default;
            arch.data = default;
            arch.componentOffsets = default;
            arch.entityStride = 0;
            arch.mask = DynamicBitmask.CreateForComponents(world);
            arch.mask.CopyFrom(ref bitmask);
            arch.hashId = bitmask.ComputeHash();
            arch.types = new MemoryList<int>(bitmask.Count, ref world->AllocatorRef);
            bitmask.ExtractSetBits(ref arch.types, ref world->AllocatorRef);
            arch.queries = new MemoryList<int>(8, ref world->AllocatorRef);
            arch.transactions = new HashMap<int, ptr<Edge>>(8, ref world->AllocatorHandler);
            arch.destroyEdge = default;
            arch.PopulateQueries(world);
            arch.destroyEdge = arch.CreateDestroyEdge();
            arch.InitPackedArrays(64);
            return ptr;
        }

        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, ref MemoryList<int> typesSpan, int index,
            bool copyList = false)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>();
            *ptr.Ptr = new ArchetypeUnsafe(world, ref typesSpan, index, copyList);
            return ptr;
        }
        internal ArchetypeUnsafe(World.WorldUnsafe* world, int index, int[] typesSpan = default)
        {
            spinner = new Spinner();
            this.world = world;
            mask = DynamicBitmask.CreateForComponents(world);
            hashId = 0;
            this.index = index;
            count = 0;
            capacity = 0;
            packedEntities = default;
            data = default;
            componentOffsets = default;
            entityStride = 0;
            if (typesSpan != null)
            {
                types = new MemoryList<int>(typesSpan.Length, ref world->AllocatorRef);
                foreach (var type in typesSpan)
                {
                    mask.Add(type);
                    types.Add(type, ref world->AllocatorRef);
                }
                hashId = mask.ComputeHash();
            }
            else
            {
                // Root Archetype
                types = new MemoryList<int>(1, ref world->AllocatorRef);
            }

            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            transactions = new HashMap<int, ptr<Edge>>(8, ref world->AllocatorHandler);
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
            InitPackedArrays(64);
        }
        internal ArchetypeUnsafe(World.WorldUnsafe* world, int index, ref Span<int> typesSpan)
        {
            spinner = new Spinner();
            this.world = world;
            mask = DynamicBitmask.CreateForComponents(world);
            hashId = 0;
            this.index = index;
            count = 0;
            capacity = 0;
            packedEntities = default;
            data = default;
            componentOffsets = default;
            entityStride = 0;
            if (typesSpan.Length > 0)
            {
                types = new MemoryList<int>(typesSpan.Length, ref world->AllocatorRef);
                foreach (var type in typesSpan)
                {
                    mask.Add(type);
                    types.Add(type, ref world->AllocatorRef);
                }
                hashId = mask.ComputeHash();
            }
            else
            {
                // Root Archetype
                types = new MemoryList<int>(1, ref world->AllocatorRef);
            }

            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            transactions = new HashMap<int, ptr<Edge>>(8, ref world->AllocatorHandler);
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
            InitPackedArrays(64);
        }

        internal ArchetypeUnsafe(World.WorldUnsafe* world, ref MemoryList<int> typesSpan, int index, bool copyList = false)
        {
            spinner = new Spinner();
            this.world = world;
            this.index = index;
            count = 0;
            capacity = 0;
            packedEntities = default;
            data = default;
            componentOffsets = default;
            entityStride = 0;
            mask = DynamicBitmask.CreateForComponents(world);
            if (typesSpan.IsCreated)
            {
                types = typesSpan;
                foreach (var type in typesSpan) mask.Add(type);
            }
            else
            {
                // Root Archetype
                types = new MemoryList<int>(1, ref world->AllocatorRef);
            }

            hashId = mask.ComputeHash();
            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            transactions = new HashMap<int, ptr<Edge>>(8, ref world->AllocatorHandler);
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
            if (copyList)
            {
                types = new MemoryList<int>(typesSpan.length, ref world->AllocatorRef);
                types.CopyFrom(ref typesSpan, ref world->AllocatorRef);
            }
            InitPackedArrays(64);
        }

        internal ref Entity CreateEntity()
        {
            ref var e = ref world->CreateEntity(index);
            var row = AllocateEntity(e.id);
            world->entityLocations.ElementAt(e.id) = new EntityLocation {
                archetypeIndex = index,
                row = row
            };
            for (var i = 0; i < queries.Length; i++) IdToQueryRef(queries.Ptr[i]).Add(e.id);
            return ref e;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span<Entity> BatchCreateEntity(int count)
        {
            return BatchCreateEntity(world->lastEntityIndex, world->lastEntityIndex + count);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span<Entity> BatchCreateEntity(int start, int end)
        {
            var result = world->BatchCreateEntity(start, end, index);
            var cnt = end - start;
            EnsureCapacity(cnt);
            var baseRow = count;
            for (int i = 0; i < cnt; i++)
            {
                packedEntities.Ptr[baseRow + i] = start + i;
                world->entityLocations.ElementAt(start + i) = new EntityLocation { archetypeIndex = index, row = baseRow + i };
            }
            for (var j = 0; j < types.length; j++)
            {
                var off = componentOffsets.Ptr[j];
                if (off < 0) continue;
                var ctData = ComponentTypeMap.GetComponentType(types.Ptr[j]);
                UnsafeUtility.MemClear(data.Ptr + off + baseRow * ctData.size, ctData.size * cnt);
            }
            count += cnt;
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

        internal void Refresh()
        {
            queries.Clear();
            PopulateQueries(world);
        }

        internal void PopulateQueries(World.WorldUnsafe* world)
        {
            if(index == 0) return;
            
            for (var i = 0; i < world->queries.Length; i++)
            {
                ref var q = ref world->queries[i];
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
                            queries.Add(q.Ptr->Id, ref world->AllocatorRef);
                            // q.Ptr->matchingArchetypes.Add(index, ref world->AllocatorRef);
                            // q.Ptr->matchingArchetypesCount++;
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
                edge.RemoveEntity.Add(in Query(queries.ElementAt(i)), ref world->AllocatorRef);
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
                else
                {
                    var off = componentOffsets.Ptr[i];
                    if (off >= 0)
                    {
                        UnsafeUtility.MemCpy(
                            data.Ptr + off + toRow * ctData.size,
                            data.Ptr + off + fromRow * ctData.size,
                            ctData.size);
                    }
                }
            }
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        #endif
        internal Entity Copy(int entity)
        {
            var newEntity = world->CreateEntity(index);
            var newRow = AllocateEntity(newEntity.id);
            world->entityLocations.Ptr[newEntity.id] = new EntityLocation {
                archetypeIndex = index,
                row = newRow
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
                else
                {
                    var off = componentOffsets.Ptr[i];
                    if (off >= 0)
                    {
                        UnsafeUtility.MemCpy(
                            data.Ptr + off + newRow * ctData.size,
                            data.Ptr + off + srcRow * ctData.size,
                            ctData.size);
                    }
                }
            }

            if (mask.Has(ComponentType<ComponentArray<Child>>.Index))
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
            var newRow = AllocateEntity(newEntity.id);
            world->entityLocations.Ptr[newEntity.id] = new EntityLocation {
                archetypeIndex = index,
                row = newRow
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
                else
                {
                    var off = componentOffsets.Ptr[i];
                    if (off >= 0)
                    {
                        if (ctData.isCopyable)
                        {
                            ctData.CopyFn().Invoke(
                                data.Ptr + off + srcRow * ctData.size, 
                                data.Ptr + off + newRow * ctData.size, 
                                entity.id, 
                                newEntity.id, 
                                0, 
                                0);
                        }
                        else
                        {
                            UnsafeUtility.MemCpy(
                                data.Ptr + off + newRow * ctData.size,
                                data.Ptr + off + srcRow * ctData.size,
                                ctData.size);
                        }
                    }
                }
            }

            if (mask.Has(ComponentType<ComponentArray<Child>>.Index))
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
        internal void OnEntityChangeECB(int entity, int component)
        {
            {
                if (transactions.TryGetValue(component, out var edge))
                {
                    world->entitiesArchetypes.ElementAt(entity) = edge.Ref.ToMove;
                    edge.Ref.Execute(entity);
                }
                else
                {
                    CreateTransaction(component);
                    edge = transactions[component];
                    world->entitiesArchetypes.ElementAt(entity) = edge.Ref.ToMove;
                    edge.Ref.Execute(entity);
                }
            }
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Destroy(int entity)
        {
            if (mask.Has(ComponentType<ComponentArray<Child>>.Index))
            {
                ref var pool = ref world->GetPool<ComponentArray<Child>>();
                ref var children = ref pool.GetRef<ComponentArray<Child>>(entity);
                foreach (ref var child in children)
                {
                    child.Value.Destroy();
                }
            }
            for (var index = 0; index < types.length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }

            destroyEdge.Execute(entity);
            world->OnDestroyEntity(entity);
        }


        internal void SetEntityData(EntityData data)
        {
            var loc = world->entityLocations.Ptr[data.Entity];
            for (var i = 0; i < data.Components.Length; i++)
            {
                var typeIndex = types[i];
                var ctData = ComponentTypeMap.GetComponentType(typeIndex);
                if (ctData.storageType == StorageType.Pool)
                {
                    ref var pool = ref world->GetUntypedPool(typeIndex);
                    pool.WriteBytes(data.Entity, data.Components[i]);
                }
                else
                {
                    var off = componentOffsets.Ptr[i];
                    if (off >= 0)
                    {
                        var dst = this.data.Ptr + off + loc.row * ctData.size;
                        fixed (byte* src = data.Components[i])
                            UnsafeUtility.MemCpy(dst, src, ctData.size);
                    }
                }
            }
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void OnEntityFree(int entity)
        {
            for (var index = 0; index < types.length; index++)
            {
                var ctData = ComponentTypeMap.GetComponentType(types[index]);
                if (ctData.storageType == StorageType.Pool)
                {
                    ref var pool = ref world->GetUntypedPool(types[index]);
                    pool.Remove(entity);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int FollowEdge(int component)
        {
            if (transactions.TryGetValue(component, out var edge))
                return edge.Ref.ToMove;
            CreateTransaction(component);
            return transactions[component].Ref.ToMove;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void BatchMigrateQueries(
            ref ArchetypeUnsafe from, ref ArchetypeUnsafe to, int entity)
        {
            for (var i = 0; i < from.queries.Length; i++)
            {
                var qId = from.queries[i];
                if (!to.queries.Contains(qId))
                    from.IdToQueryRef(qId).Remove(entity);
            }
            for (var i = 0; i < to.queries.Length; i++)
            {
                var qId = to.queries[i];
                if (!from.queries.Contains(qId))
                    to.IdToQueryRef(qId).Add(entity);
            }
        }

        private void CreateTransaction(int component)
        {
            var remove = component < 0;
            var newTypes = new MemoryList<int>(remove ? mask.Count - 1 : mask.Count + 1, ref world->AllocatorRef);
            var positiveComponent = math.abs(component);
            foreach (var type in types)
                if ((remove && type == positiveComponent) == false)
                    newTypes.Add(type, ref world->AllocatorRef);

            if (remove == false) newTypes.Add(component, ref world->AllocatorRef);

            var otherArchetypeStruct = world->GetOrCreateArchetype(ref newTypes);
            var otherArchetype = otherArchetypeStruct.impl;
            var otherEdge = new Edge(otherArchetypeStruct.ptr.Ref.index, ref world->AllocatorRef);

            for (var index = 0; index < queries.Length; index++)
            {
                var t = queries[index];
                ref var thisQuery = ref Query(t);
                if (otherArchetype->queries.Contains(thisQuery.Ref.Id) == false)
                    otherEdge.RemoveEntity.Add(thisQuery, ref world->AllocatorRef);
            }

            for (var index = 0; index < otherArchetype->queries.Length; index++)
            {
                ref var otherQuery = ref Query(otherArchetype->queries[index]);
                if (queries.Contains(otherQuery.Ref.Id) == false)
                    otherEdge.AddEntity.Add(otherQuery, ref world->AllocatorRef);
            }

            var ptr = world->_allocate_ptr<Edge>();
            ptr.Ref = otherEdge;
            transactions.TryAdd(component, ptr);
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
                    var localIdx = GetComponentLocalIndex(typeIndex);
                    var off = componentOffsets.Ptr[localIdx];
                    if (off >= 0)
                    {
                        var src = data.Ptr + off + loc.row * ctData.size;
                        buffer.Add(ComponentHelpers.Read(src, 0, ctData.size, typeIndex));
                    }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetHashCode(int[] mask)
        {
            unchecked
            {
                if (mask.Length == 0) return 0;
                var hash = (int)2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++)
                {
                    var i = mask[index];
                    hash = (hash ^ i) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(ref UnsafeList<int> mask)
        {
            unchecked
            {
                if (mask.Length == 0) return 0;
                var hash = (int)2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++)
                {
                    var i = mask[index];
                    hash = (hash ^ i) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(ref MemoryList<int> mask)
        {
            unchecked
            {
                if (mask.Length == 0) return 0;
                var hash = (int)2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++)
                {
                    var i = mask[index];
                    hash = (hash ^ i) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(ref Span<int> mask)
        {
            unchecked
            {
                if (mask.Length == 0) return 0;
                var hash = (int)2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++)
                {
                    var i = mask[index];
                    hash = (hash ^ i) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
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
            if(!archetype.mask.IsCreated) throw new Exception("Archetype mask is not created");
            return archetype.mask.Has(ComponentType<T>.Index);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this ref ArchetypeUnsafe archetype, int type)
        {
            return archetype.mask.Has(type);
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
        internal MemoryList<ptr<QueryUnsafe>> AddEntity;
        internal MemoryList<ptr<QueryUnsafe>> RemoveEntity;

        internal int ToMove;

        public Edge(int archetype, ref MemAllocator allocator) {
            ToMove = archetype;
            AddEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            RemoveEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
        }

        public Edge(ref MemAllocator allocator)
        {
            AddEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            RemoveEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            ToMove = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Execute(int entity)
        {
            for (var i = 0; i < RemoveEntity.length; i++) RemoveEntity.ElementAt(i).Ref.Remove(entity);

            for (var i = 0; i < AddEntity.length; i++) AddEntity.ElementAt(i).Ref.Add(entity);
        }

        public void OnDeserialize(ref MemAllocator alloc, World.WorldUnsafe* w)
        {
            AddEntity.OnDeserialize(ref alloc);
            foreach (ref var ptr in AddEntity)
            {
                ptr.OnDeserialize(ref alloc);
            }
            RemoveEntity.OnDeserialize(ref alloc);
            foreach (ref var ptr in RemoveEntity)
            {
                ptr.OnDeserialize(ref alloc);
            }
            //
            // QueryTest<(C1,C2,C3,C4)> d = default;
            // foreach (var (C1, C2, C3, C4) in d.par_iter())
            // {
            //     
            // }
        }
    }
}