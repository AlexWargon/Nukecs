using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using Wargon.Nukecs.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public unsafe partial struct World
    {
        [StructLayout(LayoutKind.Sequential)]
        public partial struct WorldUnsafe {
            internal void OnDeserialize(ref MemAllocator allocator)
            {
                selfPtr.OnDeserialize(ref allocator);
#if NUKECS_DEBUG
                entitiesDens.OnDeserialize(ref allocator);
                storyLog.OnDeserialize(ref allocator);
#endif
                entities.OnDeserialize(ref allocator);
                prefabsToSpawn.OnDeserialize(ref allocator);
                reservedEntities.OnDeserialize(ref allocator);
                rootArchetype.ptr.OnDeserialize(ref allocator);
                rootArchetype.ptr.Ref.OnDeserialize(ref allocator, Allocator, selfPtr.Ptr);
                entitiesArchetypes.OnDeserialize(ref allocator);

                pools.OnDeserialize(ref allocator);

                foreach (ref var genericPool in pools)
                {
                    if(genericPool.IsCreated)
                        genericPool.OnDeserialize(ref allocator);
                }
                queries.OnDeserialize(ref allocator);
                foreach (ref var query in queries)
                {
                    query.OnDeserialize(ref allocator);
                }
                archetypesList.OnDeserialize(ref allocator);
                foreach (ref var ptr in archetypesList)
                {
                    ptr.OnDeserialize(ref allocator);
                    ptr.Ref.OnDeserialize(ref allocator, Allocator, selfPtr.Ptr);
                }
                archetypesMap.OnDeserialize(ref allocator, Allocator);
                foreach (var kvPair in archetypesMap)
                {
                    kvPair.Value.ptr.OnDeserialize(ref allocator);
                    kvPair.Value.ptr.Ref.OnDeserialize(ref allocator, Allocator, selfPtr.Ptr);
                }

                DefaultNoneTypes.OnDeserialize(ref allocator);
            }
            
            internal WorldConfig config;
            internal const int FIRST_ENTITY_ID = 1;
            public byte Id;
#if NUKECS_DEBUG
            internal AliveEntitiesSet entitiesDens;
#endif
            internal MemoryList<Entity> entities;
            public MemoryList<Entity> prefabsToSpawn;
            internal MemoryList<int> reservedEntities;
            internal Archetype rootArchetype;
            internal MemoryList<int> entitiesArchetypes;
            internal HashMap<int, Archetype> archetypesMap;
            internal MemoryList<ptr<ArchetypeUnsafe>> archetypesList;
            internal MemoryList<GenericPool> pools;
            internal int poolsCount;
            internal MemoryList<ptr<QueryUnsafe>> queries;
            internal EntityCommandBuffer EntityCommandBuffer;
            internal JobHandle systemsUpdateJobDependencies;
            internal JobHandle systemsFixedUpdateJobDependencies;
            internal int job_worker_count;
            internal MemoryList<int> DefaultNoneTypes;
            internal int entitiesAmount;
            internal int lastEntityIndex;
            internal int lastDestroyedEntity;
            internal Spinner spinner;
            internal TimeData timeData;
            internal ptr<WorldUnsafe> selfPtr;
            internal WorldUnsafe* Self => selfPtr.Ptr;
            internal Allocator Allocator => AllocatorHandler.AllocatorHandle.ToAllocator;
            internal UnityAllocatorHandler AllocatorHandler;
            internal ref MemAllocator AllocatorRef => ref AllocatorHandler.AllocatorWrapper.Allocator;
            internal ref UnityAllocatorWrapper AllocatorWrapperRef => ref AllocatorHandler.AllocatorWrapper;
            internal ref EntityCommandBuffer ECB {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Self->EntityCommandBuffer;
            }
            internal UpdateContext CurrentContext {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => UpdateContext.Update;
            }

            internal static WorldUnsafe* Create(byte id, WorldConfig config)
            {
                var cSize = ComponentTypeData.GetSizeOfAllComponents(config.StartPoolSize);
                var sizeToAllocate = (long)(cSize) + 3 * 1024 * 1024;
                var allocator = new UnityAllocatorHandler(sizeToAllocate);
                var ptr = allocator.AllocatorWrapper.Allocator.AllocatePtr<WorldUnsafe>();
                ptr.Ref = new WorldUnsafe();
                ptr.Ptr->Initialize(id, config, ptr, ref allocator);
                return ptr.Ptr;
            }
            
            internal static ptr<WorldUnsafe> CreatePtr(byte id, WorldConfig config)
            {
                var cSize = ComponentTypeData.GetSizeOfAllComponents(config.StartPoolSize);
                var sizeToAllocate = (long)(cSize) + Memory.MEGABYTE*3 + Memory.MEGABYTE*4;
                var allocator = new UnityAllocatorHandler(sizeToAllocate);
                var ptr = allocator.AllocatorWrapper.Allocator.AllocatePtr<WorldUnsafe>();
                ptr.Ref = new WorldUnsafe();
                ptr.Ref.Initialize(id, config, ptr, ref allocator);
                return ptr;
            }
            private void Initialize(byte id, WorldConfig worldConfig, ptr<WorldUnsafe> worldSelf, ref UnityAllocatorHandler allocatorHandler) {
                Id = id;
                config = worldConfig;
                AllocatorHandler = allocatorHandler;
                entities = new MemoryList<Entity>(worldConfig.StartEntitiesAmount, ref AllocatorRef, true, clear:true);
                prefabsToSpawn = new MemoryList<Entity>(64, ref AllocatorRef, clear:true);
                reservedEntities = new MemoryList<int>(128, ref AllocatorRef, clear:true);
                entitiesArchetypes = new MemoryList<int>(worldConfig.StartEntitiesAmount, ref AllocatorRef, clear:true);
                pools = new MemoryList<GenericPool>(ComponentAmount.Value.Data + 1, ref AllocatorRef, clear:true, lenAsCapacity:true);
                queries = new MemoryList<ptr<QueryUnsafe>>(64, ref AllocatorRef, clear:true);
                archetypesList = new MemoryList<ptr<ArchetypeUnsafe>>(32, ref AllocatorRef, clear:true);
                archetypesMap = new HashMap<int, Archetype>(32, ref AllocatorHandler);
                DefaultNoneTypes = new MemoryList<int>(12, ref AllocatorRef, clear:true);
                config = worldConfig;
                systemsUpdateJobDependencies = default;
                systemsFixedUpdateJobDependencies = default;
                job_worker_count = JobsUtility.JobWorkerMaximumCount;
                entitiesAmount = 0;
                lastEntityIndex = FIRST_ENTITY_ID;
                poolsCount = 0;
                lastDestroyedEntity = 0;
                EntityCommandBuffer = new EntityCommandBuffer(256, Allocator.Persistent);
                spinner = new Spinner();
                aspects = new Aspects(ref AllocatorRef, id);
                
                selfPtr = worldSelf;
                
                _ = ComponentType<DestroyEntity>.Index;
                _ = ComponentType<EntityCreated>.Index;
                _ = ComponentType<IsPrefab>.Index;
                SetDefaultNone();
                //CreatePools();
               rootArchetype = CreateRootArchetype();

#if NUKECS_DEBUG
                CreateStoryLogList(1024);
                entitiesDens = new AliveEntitiesSet(config.StartEntitiesAmount, ref AllocatorRef);
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Entity CreateEntity() {
                if (lastEntityIndex >= entities.Capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    entities.Resize(newCapacity, ref AllocatorRef);
                    entitiesArchetypes.Resize(newCapacity, ref AllocatorRef);
                }
                Entity e;
                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.length > 0) {
                    last = reservedEntities.ElementAt(reservedEntities.length - 1);
                    reservedEntities.RemoveAt(reservedEntities.length - 1);
                    e = new Entity(last, Self);
                    entitiesArchetypes.ElementAt(e.id) = 0;
                    entities.ElementAt(last) = e;
#if NUKECS_DEBUG
                    entitiesDens.Add(e.id, ref AllocatorRef);
#endif
                    return e;
                }
                e = new Entity(last, Self);
                entities.ElementAt(last) = e;
                entitiesArchetypes.ElementAt(e.id) = 0;
#if NUKECS_DEBUG
                entitiesDens.Add(e.id, ref AllocatorRef);
#endif
                lastEntityIndex++;
                return e;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Entity CreateEntity(int archetype) {
                if (lastEntityIndex >= entities.capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    entities.Resize(newCapacity, ref AllocatorRef);
                    entitiesArchetypes.Resize(newCapacity, ref AllocatorRef);
                }

                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.length > 0) {
                    last = reservedEntities.ElementAt(reservedEntities.length - 1);
                    reservedEntities.RemoveAt(reservedEntities.length - 1);
                }
                else
                {
                    lastEntityIndex++;
                }
                var e = new Entity(last, Self, archetype);
                entities.ElementAt(last) = e;
#if NUKECS_DEBUG
                entitiesDens.Add(e.id, ref AllocatorRef);
#endif
                return e;
            }
            
            internal ptr<QueryUnsafe> CreateQueryPtr(bool withDefaultNoneTypes = true)
            {
                var ptr = QueryUnsafe.CreatePtrRef(Self, withDefaultNoneTypes);
                queries.Add(ptr, ref AllocatorRef);
                return ptr;
            }
            
            internal void RefreshArchetypes()
            {
                for (int i = 0; i < archetypesList.length; i++)
                {
                    ref var archetype = ref archetypesList.Ptr[i];
                    archetype.Ptr->Refresh();
                }
            }
            
            private void SetDefaultNone() {
                DefaultNoneTypes.Add(ComponentType<IsPrefab>.Index, ref AllocatorRef);
                DefaultNoneTypes.Add(ComponentType<DestroyEntity>.Index, ref AllocatorRef);
                AddPool<DestroyEntity>();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetPool<T>() where T : unmanaged, IComponent {
                var poolIndex = ComponentType<T>.Index;
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated)
                {
                    AddPool<T>(ref pool);
                }
                return ref pool;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref GenericPool GetUntypedPool(int poolIndex) {
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated) 
                {
                    AddPool(ref pool, poolIndex);
                }
                return ref pool;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public GenericPool* GetUntypedPoolPtr(int poolIndex) {
                var pool = pools.Ptr + poolIndex;
                if (!pool->IsCreated) 
                {
                    AddPool(ref *pool, poolIndex);
                }
                return pool;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetElementUntypedPool(int poolIndex) {
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated) 
                {
                    spinner.Acquire();
                    if (!pool.IsCreated) {
                        pool = GenericPool.Create(
                                    ComponentTypeMap.GetComponentType(poolIndex, true), 
                                    config.StartPoolSize * ComponentArray.DEFAULT_MAX_CAPACITY, 
                                    ref selfPtr);
                        poolsCount++;
                    }
                    spinner.Release();
                }
                return ref pool;
            }

            private void AddPool<T>() where T : unmanaged, IComponent
            {
                var poolIndex = ComponentType<T>.Index;
                pools.ElementAt(poolIndex) = GenericPool.Create<T>(config.StartPoolSize, ref selfPtr);
                poolsCount++;
            }
            private void AddPool<T>(ref GenericPool pool) where T : unmanaged, IComponent
            {
                spinner.Acquire();
                try {
                    if (!pool.IsCreated)
                    {
                        pool = GenericPool.Create<T>(config.StartPoolSize, ref selfPtr);
                        poolsCount++;
                    }
                }
                finally {
                    spinner.Release();
                }
            }

            private void AddPool(ref GenericPool pool, int index)
            {
                spinner.Acquire();
                try {
                    if (!pool.IsCreated) {
                        pool = GenericPool.Create(ComponentTypeMap.GetComponentType(index), config.StartPoolSize, ref selfPtr);
                        poolsCount++;
                    }
                }
                finally {
                    spinner.Release();
                }
            }

            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void OnDestroyEntity(int entity) {
                entities.ElementAt(entity) = Nukecs.Entity.Null;
                reservedEntities.Add(entity, ref AllocatorRef);
                entitiesAmount--;
                lastDestroyedEntity = entity;
                entitiesArchetypes.Ptr[entity] = 0;
#if NUKECS_DEBUG
                entitiesDens.Remove(entity);
#endif
            }
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool EntityIsValid(int entity)
            {
                return entities.ElementAt(entity).id != 0;
            }
            // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // internal Entity CreateEntityWithEvent(int archetype) {
            //     if (lastEntityIndex >= entities.m_capacity) {
            //         var newCapacity = lastEntityIndex * 2;
            //         UnsafeHelp.ResizeUnsafeList(ref entities, newCapacity);
            //         UnsafeHelp.ResizeUnsafeList(ref entitiesArchetypes, newCapacity);
            //     }
            //     Entity e;
            //     entitiesAmount++;
            //     var last = lastEntityIndex;
            //     if (reservedEntities.m_length > 0) {
            //         last = reservedEntities.ElementAtNoCheck(reservedEntities.m_length - 1);
            //         reservedEntities.RemoveAt(reservedEntities.m_length - 1);
            //     }
            //     e = new Entity(last, self, archetype);
            //     entities.ElementAtNoCheck(last) = e;
            //     lastEntityIndex++;
            //     return e;
            // }

            internal Entity CreateEntity<T1>(in T1 c1) 
                where T1 : unmanaged, IComponent 
            {   
                var e = CreateEntity();
                e.Add(in c1);
                return e;
            }
            internal Entity CreateEntity<T1, T2>(in T1 c1, in T2 c2) 
                where T1 : unmanaged, IComponent 
                where T2 : unmanaged, IComponent 
            {
                var e = CreateEntity();
                e.Add(in c1);
                e.Add(in c2);
                return e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Entity SpawnPrefab(in Entity prefab) {
                var e = prefab.Copy();
                prefabsToSpawn.Add(e, ref AllocatorRef);
                return e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref Entity GetEntity(int id) {
                return ref entities.ElementAt(id);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Archetype CreateArchetype(params int[] types) {
                var idx = archetypesList.length;
                var ptr = ArchetypeUnsafe.CreatePtr(Self, idx, types);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype CreateArchetype(ref MemoryList<int> types, bool copyList = false) {
                var idx = archetypesList.length;
                var ptr = ArchetypeUnsafe.CreatePtr(Self, ref types, idx, copyList);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)][BurstDiscard]
            internal void CreateArchetype(ref MemoryList<int> types, out Archetype archetype) {
                var idx = archetypesList.length;
                var archetypePtr = ArchetypeUnsafe.CreatePtr(Self, ref types, idx);
                archetype = new Archetype();
                archetype.ptr = archetypePtr;
                archetypesList.Add(in archetypePtr, ref AllocatorRef);
                archetypesMap[archetypePtr.Ptr->id] = archetype;
                //return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ptr<ArchetypeUnsafe> GetEntityArchetypePtr(int ent) {
                return archetypesList.Ptr[entitiesArchetypes.Ptr[ent]];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Archetype CreateRootArchetype() {
                var idx = archetypesList.length;
                var ptr = ArchetypeUnsafe.CreatePtr(Self, idx);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->id] = archetype;
                return archetype;
            }
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype GetOrCreateArchetype(ref MemoryList<int> types, bool copyList = false) {
                var hash = ArchetypeUnsafe.GetHashCode(ref types);
                if (archetypesMap.TryGetValue(hash, out var archetype)) {
                    types.Dispose();
                    return archetype;
                }
                
                return CreateArchetype(ref types);
            }
            [BurstDiscard][MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void GetOrCreateArchetype(ref MemoryList<int> types, out Archetype archetype) {
                archetype = GetOrCreateArchetype(ref types);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype GetArchetype(int hash) {
                return archetypesMap[hash];
            }

            internal void Update()
            {
                ECB.Playback(Self);
            }
        }
    }
}