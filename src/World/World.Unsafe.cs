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
using static Wargon.Nukecs.UnsafeStatic;

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
            public MemoryList<Entity> entities;
            public MemoryList<Entity> prefabsToSpawn;
            internal MemoryList<int> reservedEntities;
            internal Archetype rootArchetype;
            public MemoryList<EntityLocation> entityLocations;
            internal HashMap<int, Archetype> archetypesMap;
            internal DynamicBitmask tempMask;
            public MemoryList<ptr<ArchetypeUnsafe>> archetypesList;
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
            internal ResStorage resStorage;
            internal EventsStorage eventsStorage;
            internal ref WorldUnsafe SelfRef => ref selfPtr.Ref;
            internal WorldUnsafe* Self => selfPtr.Ptr;
            internal Allocator Allocator => AllocatorHandler.AllocatorHandle.ToAllocator;
            internal UnityAllocatorHandler AllocatorHandler;
            internal ref MemAllocator AllocatorRef => ref AllocatorHandler.AllocatorWrapper.Allocator;
            internal ref UnityAllocatorWrapper AllocatorWrapperRef => ref AllocatorHandler.AllocatorWrapper;
            public ptr<World> ManagedWorld;
            public ref EntityCommandBuffer ECB {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Self->EntityCommandBuffer;
            }
            internal UpdateContext CurrentContext {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => UpdateContext.Update;
            }
            public ref MemoryList<EntityLocation> EntityLocations {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Self->entityLocations;
            }
            internal static WorldUnsafe* Create(byte id, WorldConfig config)
            {
                var cSize = 0;
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
                var cSize = 0;
                var minSize = (long)config.StartPoolSize * 512;
                var allocatorSize = Math.Max(cSize, minSize);
                //dbug.log($"Allocator initial size {Memory.BytesToMegabytes(allocatorSize)} MB");
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
                entityLocations = new MemoryList<EntityLocation>(worldConfig.StartEntitiesAmount, ref AllocatorRef, clear:true, lenAsCapacity:true);
                pools = new MemoryList<GenericPool>(200, ref AllocatorRef, clear:true, lenAsCapacity:true);
                queries = new MemoryList<ptr<QueryUnsafe>>(64, ref AllocatorRef, clear:true);
                archetypesList = new MemoryList<ptr<ArchetypeUnsafe>>(32, ref AllocatorRef, clear:true);
                archetypesMap = new HashMap<int, Archetype>(32, ref AllocatorHandler);
                queriesHashToIndex = new HashMap<int, int>(64, ref AllocatorHandler);
                
                DefaultNoneTypes = new MemoryList<int>(12, ref AllocatorRef, clear:true);
                config = worldConfig;
                systemsUpdateJobDependencies = default;
                systemsFixedUpdateJobDependencies = default;
                job_worker_count = JobsUtility.JobWorkerMaximumCount;
                entitiesAmount = 0;
                lastEntityIndex = FIRST_ENTITY_ID;
                poolsCount = 0;
                lastDestroyedEntity = 0;
                EntityCommandBuffer = new EntityCommandBuffer(256, Allocator.Persistent, worldSelf.Ptr);
                spinner = new Spinner();
                aspects = new Aspects(ref AllocatorRef, id);
                
                selfPtr = worldSelf;
                tempMask = DynamicBitmask.CreateForComponents(Self);
                _ = ComponentType<DestroyEntity>.Index;
                _ = ComponentType<EntityCreated>.Index;
                _ = ComponentType<IsPrefab>.Index;
                SetDefaultNone();
                //CreatePools();
               rootArchetype = CreateRootArchetype();
               resStorage = new ResStorage(ref AllocatorRef);
               eventsStorage = new EventsStorage(ref AllocatorHandler);
#if NUKECS_DEBUG
                CreateStoryLogList(1024);
                entitiesDens = new AliveEntitiesSet(config.StartEntitiesAmount, ref AllocatorRef);
#endif
                version = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Entity CreateEntity() {
                version++;
                if (lastEntityIndex >= entities.Capacity - 1) {
                    var newCapacity = lastEntityIndex * 2;
                    entities.Resize(newCapacity, ref AllocatorRef);
                    entityLocations.Resize(newCapacity, ref AllocatorRef);
                }
                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.length > 0) {
                    last = reservedEntities.ElementAt(reservedEntities.length - 1);
                    reservedEntities.RemoveAt(reservedEntities.length - 1);
                } else {
                    lastEntityIndex++;
                }

                ref var e = ref entities.ElementAt(last);
                e = new Entity(last, Id);
                entityLocations.ElementAt(e.id) = default;
#if NUKECS_DEBUG
                entitiesDens.Add(e.id, ref AllocatorRef);
#endif
                return ref e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref Entity CreateEntity(int archetype) {
                version++;
                if (lastEntityIndex >= entities.capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    entities.Resize(newCapacity, ref AllocatorRef);
                    entityLocations.Resize(newCapacity, ref AllocatorRef);
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

                ref var e = ref entities.ElementAt(last);
                e = new Entity(last, Id);
                entityLocations.ElementAt(last) = new EntityLocation { archetypeIndex = archetype, row = 0 };
#if NUKECS_DEBUG
                entitiesDens.Add(e.id, ref AllocatorRef);
#endif
                return ref e;
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
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetPool<T>() where T : unmanaged{
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
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated)
                {
                    var ctData = ComponentTypeMap.GetComponentType(poolIndex);
                    if (ctData.storageType == StorageType.Archetype) return ref pool;
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
                        //dbug.log($"ElementPool<{ComponentTypeMap.GetComponentType(poolIndex, true).ManagedType.Name}>.Index {poolIndex}");
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

            private void AddPool<T>() where T : unmanaged
            {
                var poolIndex = ComponentType<T>.Index;
                pools.ElementAt(poolIndex) = GenericPool.Create<T>(config.StartPoolSize, ref selfPtr);
                poolsCount++;
            }
            private void AddPool<T>(ref GenericPool pool) where T : unmanaged
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
                version++;
                ref var e = ref entities.ElementAt(entity);
                e = Nukecs.Entity.Null;
                reservedEntities.Add(entity, ref AllocatorRef);
                entitiesAmount--;
                lastDestroyedEntity = entity;
                entityLocations.Ptr[entity] = default;
#if NUKECS_DEBUG
                entitiesDens.Remove(entity);
#endif
                //dbug.log($"Entity {entity} destroyed");
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool EntityIsValid(int entity)
            {
                return entities.ElementAt(entity).id != 0;
            }
            // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // internal Entity CreateEntityWithEvent(int archetype) {
            //     if (lastEntityIndex >= entities.m_capacity) {
            //         var newCapacity = lastEntityIndex * 2;
            //         UnsafeHelp.ResizeUnsafeList(ref entities, newCapacity);
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
                e.Add(c1);
                return ref e;
            }
            internal ref Entity CreateEntity<T1, T2>(in T1 c1, in T2 c2) 
                where T1 : unmanaged, IComponent 
                where T2 : unmanaged, IComponent
            {
                Span<int> componentTypes = stackalloc int[2] { ComponentType<T1>.Index, ComponentType<T2>.Index };
                var arch = GetOrCreateArchetype(ref componentTypes);
                ref var e = ref arch.CreateEntity();
                e.Set(in c1);
                e.Set(in c2);
                return ref e;
            }
            internal ref Entity CreateEntity<T1, T2, T3>(in T1 c1, in T2 c2, in T3 c3) 
                where T1 : unmanaged, IComponent 
                where T2 : unmanaged, IComponent
                where T3 : unmanaged, IComponent
            {
                Span<int> componentTypes = stackalloc int[2] { ComponentType<T1>.Index, ComponentType<T2>.Index };
                var arch = GetOrCreateArchetype(ref componentTypes);
                ref var e = ref arch.CreateEntity();
                e.Set(in c1);
                e.Set(in c2);
                e.Set(in c3);
                return ref e;
            }
            internal ref Entity CreateEntity<T1, T2, T3, T4>(in T1 c1, in T2 c2, in T3 c3, in T4 c4) 
                where T1 : unmanaged, IComponent 
                where T2 : unmanaged, IComponent
                where T3 : unmanaged, IComponent
                where T4 : unmanaged, IComponent
            {
                Span<int> componentTypes = stackalloc int[4]
                {
                    ComponentType<T1>.Index, 
                    ComponentType<T2>.Index, 
                    ComponentType<T3>.Index, 
                    ComponentType<T4>.Index
                };
                var arch = GetOrCreateArchetype(ref componentTypes);
                ref var e = ref arch.CreateEntity();
                e.Set(in c1);
                e.Set(in c2);
                e.Set(in c3);
                e.Set(in c4);
                return ref e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void BatchCreateEntity(int count, int* outEntities)
            {
                BatchCreateEntity(count, outEntities, 0);
            }

            internal void BatchCreateEntity(int count, int* outEntities, int archetype)
            {
                var needed = lastEntityIndex + count;
                if (needed >= entities.Capacity)
                {
                    var newCapacity = entities.Capacity * 2;
                    if (newCapacity < needed) newCapacity = needed;
                    entities.Resize(newCapacity, ref AllocatorRef);
                    entityLocations.Resize(newCapacity, ref AllocatorRef);
                }

                entitiesAmount += count;
                var created = 0;
                var reservedCount = reservedEntities.length;
                var fromReserved = reservedCount < count ? reservedCount : count;

                for (var i = 0; i < fromReserved; i++)
                {
                    var id = reservedEntities.ElementAt(reservedCount - 1 - i);
                    entities.ElementAt(id) = new Entity(id, Id);
                    entityLocations.ElementAt(id) = new EntityLocation { archetypeIndex = archetype };
                    outEntities[created++] = id;
                }
                reservedEntities.length -= fromReserved;

                while (created < count)
                {
                    var id = lastEntityIndex++;
                    entities.ElementAt(id) = new Entity(id, Id);
                    entityLocations.ElementAt(id) = new EntityLocation { archetypeIndex = archetype };
                    outEntities[created++] = id;
                }

#if NUKECS_DEBUG
                for (var i = 0; i < count; i++)
                    entitiesDens.Add(outEntities[i], ref AllocatorRef);
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Span<Entity> BatchCreateEntity(int count)
            {
                var start = lastEntityIndex;
                return BatchCreateEntity(start, start + count, 0);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Span<Entity> BatchCreateEntityWithArch(int count, int arch)
            {
                var start = lastEntityIndex;
                return BatchCreateEntity(start, start + count, arch);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Span<Entity> BatchCreateEntity(int start, int end)
            {
                return BatchCreateEntity(start, end, 0);
            }
            internal Span<Entity> BatchCreateEntity(int start, int end, int archetype)
            {
                var count = end - start;
                if (count <= 0) return default;

                if (end >= entities.Capacity)
                {
                    var newCapacity = entities.Capacity * 2;
                    if (newCapacity < end + 1) newCapacity = end + 1;
                    entities.Resize(newCapacity, ref AllocatorRef);
                    entityLocations.Resize(newCapacity, ref AllocatorRef);
                }

                entitiesAmount += count;

                new Span<EntityLocation>(entityLocations.Ptr + start, count).Fill(new EntityLocation { archetypeIndex = archetype });
                for (var i = start; i < end; i++)
                {
                    entities.Ptr[i] = new Entity(i, Id);
#if NUKECS_DEBUG
                    entitiesDens.Add(i, ref AllocatorRef);
#endif
                }

                if (end > lastEntityIndex) lastEntityIndex = end;
                return new Span<Entity>(entities.Ptr + start, count);
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
            public Span<Entity> SpawnPrefabs(in Entity prefab, int amount)
            {
                var oldLen = prefabsToSpawn.length;
                prefabsToSpawn.Resize(oldLen + amount, ref AllocatorRef);
                var ents = new Span<Entity>(prefabsToSpawn.Ptr + oldLen, amount);
#if NUKECS_DEBUG
                AddComponentChange(new ComponentChange
                {
                    command = EntityCommandBuffer.ECBCommand.Type.SpawnPrefab,
                    entityId = prefab.id,
                    timeStamp = timeData.ElapsedTime,
                    tempData = amount
                });
#endif
                prefab.ArchetypeRef.BatchCloneEntity(prefab.id, ents);
                return ents;
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
            public ref ArchetypeUnsafe GetArchetype(in Entity entity)
            {
                return ref entity.ArchetypeRef;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal ref ArchetypeUnsafe GetArchetypeNoneIsPrefab(in Entity prefab)
            {
                ref var prefabArchetype = ref prefab.ArchetypeRef;
                tempMask.CopyFrom(ref prefabArchetype.mask);
                tempMask.Remove(ComponentType<IsPrefab>.Index);
                ref var targetArch = ref GetOrCreateArchetype(ref tempMask).ptr.Ref;
                tempMask.Clear();
                return ref targetArch;
            }
            internal Archetype CreateArchetype(ref MemoryList<int> types, bool copyList = false) {
                var idx = archetypesList.length;
                var ptr = ArchetypeUnsafe.CreatePtr(Self, ref types, idx, copyList);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->hashId] = archetype;
                return archetype;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal Archetype CreateArchetype(ref Span<int> types) {
                var idx = archetypesList.length;
                var ptr = ArchetypeUnsafe.CreatePtr(Self, idx, ref types);
                Archetype archetype;
                archetype.ptr = ptr;
                archetypesList.Add(in ptr, ref AllocatorRef);
                archetypesMap[ptr.Ptr->hashId] = archetype;
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
                archetypesMap[archetypePtr.Ptr->hashId] = archetype;
                //return archetype;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            public ref ptr<ArchetypeUnsafe> GetEntityArchetypePtr(int ent) {
                return ref archetypesList.Ptr[entityLocations.Ptr[ent].archetypeIndex];
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
                archetypesMap[ptr.Ptr->hashId] = archetype;
                return archetype;
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal Archetype GetOrCreateArchetype(ref Span<int> types) {
                var hash = DynamicBitmask.ComputeHash((int*)UnsafeUtility.AddressOf(ref types[0]), types.Length);
                if (archetypesMap.TryGetValue(hash, out var archetype)) {
                    return archetype;
                }
                return CreateArchetype(ref types);
            }
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            internal Archetype GetOrCreateArchetype(ref MemoryList<int> types, bool copyList = false) {
                var hash = DynamicBitmask.ComputeHash(types.Ptr, types.length);
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype GetOrCreateArchetype(ref DynamicBitmask mask) {
                var hash = mask.ComputeHash();
                if (archetypesMap.TryGetValue(hash, out var archetype))
                    return archetype;
                return CreateArchetype(ref mask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype CreateArchetype(ref DynamicBitmask mask) {
                var idx = archetypesList.length;
                var archetypePtr = ArchetypeUnsafe.CreatePtrFromBitmask(Self, idx, ref mask);
                Archetype archetype;
                archetype.ptr = archetypePtr;
                archetypesList.Add(in archetypePtr, ref AllocatorRef);
                archetypesMap[archetypePtr.Ptr->hashId] = archetype;
                return archetype;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype GetArchetype(int hash) {
                return archetypesMap[hash];
            }

            internal void Update()
            {
                ECB.Playback(Self);
            }

            public ptr<TParam0> GetSystemParam2<TParam0>() where TParam0 : unmanaged, ISystemParam
            {
                TParam0 paramDefault = default;
                var param = ptr<TParam0>.NULL;
                switch (paramDefault.MetaType)
                {
                    case SystemParamMetaType.Events:
                        param = eventsStorage.Get<TParam0>(ref selfPtr);
                        break;
                    case SystemParamMetaType.Resource:
                        if (resStorage.HasRes<TParam0>())
                        {
                            param = resStorage.GetRes<TParam0>();
                        }
                        else 
                        {
                            resStorage.AddRes(in paramDefault, Self);
                            param = resStorage.GetRes<TParam0>();
                            param.Ref = paramDefault;
                            param.Ref.Init(ref selfPtr);
                        }
                        break;
                    case SystemParamMetaType.Service:
                        param = AllocatorRef.AllocatePtr<TParam0>();
                        param.Ref = paramDefault;
                        param.Ref.Init(ref selfPtr);
                        break;
                    case SystemParamMetaType.Query:
                    {
                        int hash = paramDefault.GetHashCode();
                        if (queriesHashToIndex.TryGetValue(hash, out var queryIndex))
                        {
                            param = AllocatorRef.AllocatePtr<TParam0>();
                            param.Ref = paramDefault;
                            as_ref<SetQueryPtrProxy>(param.cached).SetQueryPtr(queries[queryIndex]);
                        }
                        else
                        {
                            param = AllocatorRef.AllocatePtr<TParam0>();
                            param.Ref = paramDefault;
                            param.Ref.Init(ref selfPtr);
                            queriesHashToIndex.TryAdd(hash, queries.length - 1);
                        }
                        break;
                    }
                    case SystemParamMetaType.Single:
                    case SystemParamMetaType.Local:
                        param = AllocatorRef.AllocatePtr<TParam0>();
                        param.Ref = paramDefault;
                        param.Ref.Init(ref selfPtr);
                        break;
                    case SystemParamMetaType.World:
                        break;
                    case SystemParamMetaType.State:
                        break;
                }
                
                return param;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal byte* GetComponentDataPtr(int entityId, int typeIndex)
            {
                var data = ComponentTypeMap.GetComponentType(typeIndex);
                if (data.storageType == StorageType.Pool)
                {
                    ref var pool = ref GetUntypedPool(typeIndex);
                    return pool.UnsafeGetPtr(entityId);
                }
                ref var loc = ref entityLocations.Ptr[entityId];
                ref var arch = ref archetypesList.Ptr[loc.archetypeIndex].Ref;
                return arch.GetComponentDataPtr(typeIndex, loc.row);
            }

            public void AddRes<TRes>(TRes res) where TRes : unmanaged, IRes
            {
                var resRef = new Res<TRes>(res);
                resStorage.AddRes(in resRef, Self);
            }

            public void AddResManaged<TRes>(TRes res) where TRes : class, IRes
            {
                var resRef = new ResManaged<TRes>(res);
                resStorage.AddRes(resRef, Self);
            }
        }
    }
}