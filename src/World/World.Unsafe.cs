using System;
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
            
            internal WorldConfig config;
            internal const int FIRST_ENTITY_ID = 1;
            public byte Id;
            public int version;
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
            internal HashMap<int, int> queriesHashToIndex;
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
            internal ref WorldUnsafe SelfRef => ref selfPtr.Ref;
            internal WorldUnsafe* Self => selfPtr.Ptr;
            internal Allocator Allocator => AllocatorHandler.AllocatorHandle.ToAllocator;
            internal UnityAllocatorHandler AllocatorHandler;
            internal ref MemAllocator AllocatorRef => ref AllocatorHandler.AllocatorWrapper.Allocator;
            internal ref UnityAllocatorWrapper AllocatorWrapperRef => ref AllocatorHandler.AllocatorWrapper;
            public ref EntityCommandBuffer ECB {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Self->EntityCommandBuffer;
            }
            internal UpdateContext CurrentContext {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => UpdateContext.Update;
            }

            internal static WorldUnsafe* Create(byte id, WorldConfig config)
            {
                var cSize = ComponentTypeData.GetSizeOfAllComponents(config.StartPoolSize);
                var minSize = (long)config.StartPoolSize * 512;
                var allocatorSize = Math.Max(cSize, minSize);
                var allocator = new UnityAllocatorHandler(allocatorSize);
                var ptr = allocator.AllocatorWrapper.Allocator.AllocatePtr<WorldUnsafe>();
                ptr.Ref = new WorldUnsafe();
                ptr.Ptr->AllocatorHandler = allocator;
                ptr.Ptr->Initialize(id, config, ptr);
                return ptr.Ptr;
            }
            
            internal static ptr<WorldUnsafe> CreatePtr(byte id, WorldConfig config)
            {
                var cSize = ComponentTypeData.GetSizeOfAllComponents(config.StartPoolSize);
                var minSize = (long)config.StartPoolSize * 512;
                var allocatorSize = Math.Max(cSize, minSize);
                dbug.log($"Allocator initial size {Memory.BytesToMegabytes(allocatorSize)} MB");
                var allocator = new UnityAllocatorHandler(allocatorSize);
                var ptr = allocator.AllocatorWrapper.Allocator.AllocatePtr<WorldUnsafe>();
                ptr.Ref = new WorldUnsafe();
                ptr.Ref.AllocatorHandler = allocator;
                ptr.Ref.Initialize(id, config, ptr);
                return ptr;
            }
            private void Initialize(byte id, WorldConfig worldConfig, ptr<WorldUnsafe> worldSelf) {
                Id = id;
                config = worldConfig;
                entities = new MemoryList<Entity>(worldConfig.StartEntitiesAmount, ref AllocatorRef, true, clear:true);
                prefabsToSpawn = new MemoryList<Entity>(64, ref AllocatorRef, clear:true);
                reservedEntities = new MemoryList<int>(128, ref AllocatorRef, clear:true);
                entitiesArchetypes = new MemoryList<int>(worldConfig.StartEntitiesAmount, ref AllocatorRef, clear:true);
                pools = new MemoryList<GenericPool>(200, ref AllocatorRef, clear:true, lenAsCapacity:true);
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
                version = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Entity CreateEntity() {
                if (lastEntityIndex >= entities.Capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    entities.Resize(newCapacity, ref AllocatorRef);
                    entitiesArchetypes.Resize(newCapacity, ref AllocatorRef);
                }
                ref Entity e = ref entities.ElementAt(lastEntityIndex);
                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.length > 0) {
                    last = reservedEntities.ElementAt(reservedEntities.length - 1);
                    reservedEntities.RemoveAt(reservedEntities.length - 1);
                    e = new Entity(last, Self);
                    entitiesArchetypes.ElementAt(e.id) = 0;
#if NUKECS_DEBUG
                    entitiesDens.Add(e.id, ref AllocatorRef);
#endif
                    return ref e;
                }
                e = new Entity(last, Self);
                entitiesArchetypes.ElementAt(e.id) = 0;
#if NUKECS_DEBUG
                entitiesDens.Add(e.id, ref AllocatorRef);
#endif
                lastEntityIndex++;
                return ref e;
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
                entitiesArchetypes.ElementAt(last) = archetype;
#if NUKECS_DEBUG
                entitiesDens.Add(e.id, ref AllocatorRef);
#endif
                return e;
            }
            
            internal ptr<QueryUnsafe> CreateQueryPtr(bool withDefaultNoneTypes = true)
            {
                var ptr = QueryUnsafe.CreatePtrRef(selfPtr, withDefaultNoneTypes);
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
                //if (poolIndex >= pools.Capacity) EnsurePoolCapacity(poolIndex + 32);
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated)
                {
                    AddPool<T>(ref pool);
                }
                return ref pool;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref GenericPool GetUntypedPool(int poolIndex) {
                //if (poolIndex >= pools.Capacity) EnsurePoolCapacity(poolIndex + 32);
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated) 
                {
                    AddPool(ref pool, poolIndex);
                }
                return ref pool;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public GenericPool* GetUntypedPoolPtr(int poolIndex) {
                if (poolIndex >= pools.Capacity) EnsurePoolCapacity(poolIndex + 32);
                var pool = pools.Ptr + poolIndex;
                if (!pool->IsCreated) 
                {
                    AddPool(ref *pool, poolIndex);
                }
                return pool;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetElementUntypedPool(int poolIndex) {
                if (poolIndex >= pools.Capacity) EnsurePoolCapacity(poolIndex + 32);
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

            private void EnsurePoolCapacity(int needed) {
                spinner.Acquire();
                if (needed > pools.Capacity)
                    pools.Resize(needed, ref AllocatorRef);
                spinner.Release();
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
                        //dbug.log($"pool<{typeof(T).Name}> created at {poolsCount}");
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

#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal void OnDestroyEntity(int entity)
            {
                ref var e = ref entities.ElementAt(entity);
                e = Nukecs.Entity.Null;
                reservedEntities.Add(entity, ref AllocatorRef);
                entitiesAmount--;
                lastDestroyedEntity = entity;
                entitiesArchetypes.Ptr[entity] = 0;
#if NUKECS_DEBUG
                entitiesDens.Remove(entity);
#endif
                //dbug.log($"Entity {entity} destroyed");
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

            internal ref Entity CreateEntity<T1>(in T1 c1) 
                where T1 : unmanaged, IComponent 
            {   
                ref var e = ref CreateEntity();
                e.Add(in c1);
                return ref e;
            }
            internal ref Entity CreateEntity<T1, T2>(in T1 c1, in T2 c2) 
                where T1 : unmanaged, IComponent 
                where T2 : unmanaged, IComponent 
            {
                ref var e = ref CreateEntity();
                e.Add(in c1);
                e.Add(in c2);
                return ref e;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            public Entity SpawnPrefab(in Entity prefab) {
                var e = prefab.Copy();
                prefabsToSpawn.Add(e, ref AllocatorRef);
                return e;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal ref Entity GetEntity(int id) {
                return ref entities.ElementAt(id);
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            public Archetype CreateArchetype(params int[] types) {
                var idx = archetypesList.length;
                var ptr = ArchetypeUnsafe.CreatePtr(Self, idx, types);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->id] = archetype;
                return archetype;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal Archetype CreateArchetype(ref MemoryList<int> types, bool copyList = false) {
                var idx = archetypesList.length;
                var ptr = ArchetypeUnsafe.CreatePtr(Self, ref types, idx, copyList);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->id] = archetype;
                return archetype;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal void CreateArchetype(ref MemoryList<int> types, out Archetype archetype) {
                var idx = archetypesList.length;
                var archetypePtr = ArchetypeUnsafe.CreatePtr(Self, ref types, idx);
                archetype = new Archetype();
                archetype.ptr = archetypePtr;
                archetypesList.Add(in archetypePtr, ref AllocatorRef);
                archetypesMap[archetypePtr.Ptr->id] = archetype;
                //return archetype;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal ptr<ArchetypeUnsafe> GetEntityArchetypePtr(int ent) {
                return archetypesList.Ptr[entitiesArchetypes.Ptr[ent]];
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            private Archetype CreateRootArchetype() {
                var idx = archetypesList.length;
                var ptr = ArchetypeUnsafe.CreatePtr(Self, idx);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->id] = archetype;
                return archetype;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal Archetype GetOrCreateArchetype(ref MemoryList<int> types, bool copyList = false) {
                var hash = ArchetypeUnsafe.GetHashCode(ref types);
                if (archetypesMap.TryGetValue(hash, out var archetype)) {
                    types.Dispose();
                    return archetype;
                }
                
                return CreateArchetype(ref types);
            }
            [BurstDiscard]
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal void GetOrCreateArchetype(ref MemoryList<int> types, out Archetype archetype) {
                archetype = GetOrCreateArchetype(ref types);
            }

#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal Archetype GetArchetype(int hash) {
                return archetypesMap[hash];
            }

            internal void Update()
            {
                ECB.Playback(Self);
            }

            internal ptr GetSystemParam<TParam0>(out SystemParamMetaType type) where TParam0 :  unmanaged, ISystemParam
            {
                var param = AllocatorRef.AllocatePtr<TParam0>();
                param.Ref.Init(ref selfPtr);
                type = param.Ref.MetaType;
                dbug.log($"Get {param.Ref.ParamType.Name} param, MetaType: {type}");
                return param.UntypedPointer;
            }
            public ptr<TParam0> GetSystemParam2<TParam0>() where TParam0 :  unmanaged, ISystemParam
            {
                var param = AllocatorRef.AllocatePtr<TParam0>();
                param.Ref.Init(ref selfPtr);
                //dbug.log($"Get {param.Ref.ParamType.Name} param, MetaType: {param.Ref.MetaType}");
                return param;
            }
        }
    }
}