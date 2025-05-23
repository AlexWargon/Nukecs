﻿using System.Runtime.CompilerServices;
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
            internal void OnDeserialize()
            {
                entities.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                prefabsToSpawn.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                reservedEntities.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                entitiesArchetypes.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                pools.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                queries.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                foreach (ref var query in queries)
                {
                    query.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                }
                archetypesMap.OnDeserialize(ref AllocatorRef, Allocator);
                foreach (var kvPair in archetypesMap)
                {
                    kvPair.Value.ptr.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                    kvPair.Value.ptr.Ref.OnDeserialize(ref AllocatorRef, Allocator);
                }
                archetypesList.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                foreach (ref var ptr in archetypesList)
                {
                    ptr.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                }
                //ArchetypeHashCache.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                DefaultNoneTypes.OnDeserialize(ref AllocatorWrapperRef.Allocator);
                selfPtr.OnDeserialize(ref AllocatorWrapperRef.Allocator);
            }
            internal const int FIRST_ENTITY_ID = 1;
            internal int Id;
            internal MemoryList<Entity> entities;
            internal MemoryList<Entity> prefabsToSpawn;
            internal MemoryList<int> reservedEntities;
            internal MemoryList<Archetype> entitiesArchetypes;
            internal MemoryList<GenericPool> pools;
            internal int poolsCount;
            internal MemoryList<ptr<QueryUnsafe>> queries;
            internal HashMap<int, Archetype> archetypesMap;
            internal MemoryList<ptr<ArchetypeUnsafe>> archetypesList;
            internal WorldConfig config;
            internal EntityCommandBuffer EntityCommandBuffer;
            internal JobHandle systemsUpdateJobDependencies;
            internal JobHandle systemsFixedUpdateJobDependencies;
            internal int job_worker_count;
            internal MemoryList<int> DefaultNoneTypes;
            internal int entitiesAmount;
            internal int lastEntityIndex;
            internal int lastDestroyedEntity;
            internal Spinner spinner;
            internal ptr<WorldUnsafe> selfPtr;
            internal WorldUnsafe* Self => selfPtr.Ptr;
            internal Allocator Allocator => AllocatorHandler.AllocatorHandle.ToAllocator;
            internal UnityAllocatorHandler AllocatorHandler;
            internal ref SerializableMemoryAllocator AllocatorRef => ref AllocatorHandler.AllocatorWrapper.Allocator;
            internal ref UnityAllocatorWrapper AllocatorWrapperRef => ref AllocatorHandler.AllocatorWrapper;
            internal ref EntityCommandBuffer ECB {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Self->EntityCommandBuffer;
            }
            internal UpdateContext CurrentContext {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => UpdateContext.Update;
            }
            internal static WorldUnsafe* Create(int id, WorldConfig config)
            {
                var cSize = ComponentType.GetSizeOfAllComponents(config.StartPoolSize);
                var sizeToAllocate = (long)(cSize) + 3 * 1024 * 1024;
                var allocator = new UnityAllocatorHandler(sizeToAllocate);
                var ptr = allocator.AllocatorWrapper.Allocator.AllocatePtr<WorldUnsafe>();
                ptr.Ref = new WorldUnsafe();
                ptr.Ptr->Initialize(id, config, ptr, ref allocator);
                return ptr.Ptr;
            }
            internal static ptr<WorldUnsafe> CreatePtr(int id, WorldConfig config)
            {
                var cSize = ComponentType.GetSizeOfAllComponents(config.StartPoolSize);
                var sizeToAllocate = (long)(cSize) + 3 * 1024 * 1024;
                var allocator = new UnityAllocatorHandler(sizeToAllocate);
                var ptr = allocator.AllocatorWrapper.Allocator.AllocatePtr<WorldUnsafe>();
                ptr.Ref = new WorldUnsafe();
                ptr.Ref.Initialize(id, config, ptr, ref allocator);
                return ptr;
            }
            private void Initialize(int id, WorldConfig worldConfig, ptr<WorldUnsafe> worldSelf, ref UnityAllocatorHandler allocatorHandler) {
                Id = id;
                config = worldConfig;
                AllocatorHandler = allocatorHandler;
                entities = new MemoryList<Entity>(worldConfig.StartEntitiesAmount, ref AllocatorRef, true);
                prefabsToSpawn = new MemoryList<Entity>(64, ref AllocatorRef);
                reservedEntities = new MemoryList<int>(128, ref AllocatorRef);
                entitiesArchetypes = new MemoryList<Archetype>(worldConfig.StartEntitiesAmount, ref AllocatorRef);
                pools = new MemoryList<GenericPool>(ComponentAmount.Value.Data + 1, ref AllocatorRef);
                queries = new MemoryList<ptr<QueryUnsafe>>(64, ref AllocatorRef);
                archetypesList = new MemoryList<ptr<ArchetypeUnsafe>>(32, ref AllocatorRef);
                archetypesMap = new HashMap<int, Archetype>(32, ref AllocatorHandler);
                DefaultNoneTypes = new MemoryList<int>(12, ref AllocatorRef);
                config = worldConfig;
                systemsUpdateJobDependencies = default;
                systemsFixedUpdateJobDependencies = default;
                job_worker_count = JobsUtility.JobWorkerMaximumCount;
                entitiesAmount = 0;
                lastEntityIndex = FIRST_ENTITY_ID;
                poolsCount = 0;
                lastDestroyedEntity = 0;
                EntityCommandBuffer = new EntityCommandBuffer(256, Allocator);
                spinner = new Spinner();
                aspects = new Aspects(ref AllocatorRef, id);
                
                selfPtr = worldSelf;
                
                _ = ComponentType<DestroyEntity>.Index;
                _ = ComponentType<EntityCreated>.Index;
                _ = ComponentType<IsPrefab>.Index;
                SetDefaultNone();
                //CreatePools();
                CreateRootArchetype();
            }

            internal ptr<QueryUnsafe> CreateQueryPtr(bool withDefaultNoneTypes = true)
            {
                var ptr = QueryUnsafe.CreatePtrPtr(Self, withDefaultNoneTypes);
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
            internal ref GenericPool GetPool<T>() where T : unmanaged {
                var poolIndex = ComponentType<T>.Index;
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated)
                {
                    AddPool<T>(ref pool);
                }
                return ref pool;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetUntypedPool(int poolIndex) {
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated) 
                {
                    AddPool(ref pool, poolIndex);
                }
                return ref pool;
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
                                    Self);
                        poolsCount++;
                    }
                    spinner.Release();
                }
                return ref pool;
            }

            private void AddPool<T>() where T : unmanaged
            {
                var poolIndex = ComponentType<T>.Index;
                pools.ElementAt(poolIndex) = GenericPool.Create<T>(config.StartPoolSize, Self);
                poolsCount++;
            }
            private void AddPool<T>(ref GenericPool pool) where T : unmanaged
            {
                spinner.Acquire();
                try {
                    if (!pool.IsCreated)
                    {
                        pool = GenericPool.Create<T>(config.StartPoolSize, Self);
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
                        pool = GenericPool.Create(ComponentTypeMap.GetComponentType(index), config.StartPoolSize, Self);
                        poolsCount++;
                    }
                }
                finally {
                    spinner.Release();
                }
            }

            private void CreatePools()
            {
                ComponentTypeMap.CreatePools(ref pools, config.StartPoolSize, Self, ref poolsCount);
            }
            
            [BurstDiscard]
            private void DebugPoolLog<T>(int poolIndex, int count)
            {
                Debug.Log($"pool {typeof(T)} created with index {poolIndex} and count {count}");
            }
            [BurstDiscard]
            private void DebugPoolLog(int poolIndex, int count)
            {
                Debug.Log($"untyped pool {ComponentTypeMap.GetType(poolIndex)} created with index {poolIndex} and count {count}");
            }

            internal void EntityAddComponent<T>(int id, T componeet) where T : unmanaged { }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Entity CreateEntity() {
                if (lastEntityIndex >= entities.Capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    entities.Resize(newCapacity, ref AllocatorRef);
                    entitiesArchetypes.Resize(newCapacity, ref AllocatorRef);
                    // UnsafeHelp.ResizeUnsafeList(ref entities, newCapacity);
                    // UnsafeHelp.ResizeUnsafeList(ref entitiesArchetypes, newCapacity);
                }
                Entity e;
                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.length > 0) {
                    last = reservedEntities.ElementAt(reservedEntities.length - 1);
                    reservedEntities.RemoveAt(reservedEntities.length - 1);
                    e = new Entity(last, Self);
                    entities.ElementAt(last) = e;
                    return e;
                }
                e = new Entity(last, Self);
                entities.ElementAt(last) = e;
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
                
                return e;
            }
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void OnDestroyEntity(int entity) {
                entities.Ptr[entity] = Nukecs.Entity.Null;
                reservedEntities.Add(entity, ref AllocatorRef);
                entitiesAmount--;
                lastDestroyedEntity = entity;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool EntityIsValid(int entity)
            {
                return entities.Ptr[entity].id != 0;
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
                return ref entities.Ptr[id];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Archetype CreateArchetype(params int[] types) {
                var ptr = ArchetypeUnsafe.CreatePtr(Self, types);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype CreateArchetype(ref MemoryList<int> types, bool copyList = false) {
                var ptr = ArchetypeUnsafe.CreatePtr(Self, ref types, copyList);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)][BurstDiscard]
            internal void CreateArchetype(ref MemoryList<int> types, out Archetype archetype) {
                var archetypePtr = ArchetypeUnsafe.CreatePtr(Self, ref types);
                archetype = new Archetype();
                archetype.ptr = archetypePtr;
                archetypesList.Add(in archetypePtr, ref AllocatorRef);
                archetypesMap[archetypePtr.Ptr->id] = archetype;
                //return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Archetype CreateRootArchetype() {
                var ptr = ArchetypeUnsafe.CreatePtr(Self);
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