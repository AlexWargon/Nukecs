using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs
{
    public unsafe partial struct World
    {
        internal partial struct WorldUnsafe {
            internal int Id;
            
            internal MemoryList<Entity> entities;
            internal MemoryList<Entity> prefabsToSpawn;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<int> reservedEntities;
            internal MemoryList<Archetype> entitiesArchetypes;
            internal MemoryList<GenericPool> pools;
            internal int poolsCount;
            internal MemoryList<_Ptr<QueryUnsafe>> queries;
            internal UnsafeHashMap<int, Archetype> archetypesMap;
            internal UnsafePtrList<ArchetypeUnsafe> archetypesList;
            internal MemoryList<int> ArchetypeHashCache;
            internal WorldConfig config;
            internal EntityCommandBuffer ECBUpdate;
            internal JobHandle systemsUpdateJobDependencies;
            internal JobHandle systemsFixedUpdateJobDependencies;
            internal int job_worker_count;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<int> DefaultNoneTypes;
            internal int entitiesAmount;
            internal int lastEntityIndex;
            internal int lastDestroyedEntity;
            internal Locking locking;
            [NativeDisableUnsafePtrRestriction] internal WorldUnsafe* self;
            internal Allocator Allocator => AllocatorHandler.AllocatorHandle.ToAllocator;
            internal UnityAllocatorHandler AllocatorHandler;
            internal ref SerializableMemoryAllocator AllocatorRef => ref AllocatorHandler.AllocatorWrapper.Allocator;
            internal ref UnityAllocatorWrapper AllocatorWrapperRef => ref AllocatorHandler.AllocatorWrapper;
            internal ref EntityCommandBuffer ECB {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref self->ECBUpdate;
            }
            internal UpdateContext CurrentContext {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => UpdateContext.Update;
            }
            internal static WorldUnsafe* Create(int id, WorldConfig config)
            {
                var cSize = ComponentType.GetSizeOfAllComponents(config.StartPoolSize);
                var sizeToAllocate = (long)(cSize * 2.3) + 3 * 1024 * 1024;
                var allocator = new UnityAllocatorHandler(sizeToAllocate);
                
                var ptr = (WorldUnsafe*)allocator.AllocatorWrapper.Allocate(sizeof(WorldUnsafe), UnsafeUtility.AlignOf<WorldUnsafe>());
                *ptr = new WorldUnsafe();
                if (ptr == null)
                {
                    Debug.Log("Failed to create World Unsafe");
                }
                ptr->Initialize(id, config, ptr, ref allocator);
                ptr->CreatePools();
                return ptr;
            }

            private void Initialize(int id, WorldConfig worldConfig, WorldUnsafe* worldSelf, ref UnityAllocatorHandler allocatorHandler) {
                this.Id = id;
                this.config = worldConfig;
                {
                    this.AllocatorHandler = allocatorHandler;
                }

                this.entities = new MemoryList<Entity>(worldConfig.StartEntitiesAmount, ref AllocatorWrapperRef, true);
                this.prefabsToSpawn = new MemoryList<Entity>(64, ref AllocatorWrapperRef);
                this.reservedEntities = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(128, AllocatorHandler.AllocatorWrapper.Handle, NativeArrayOptions.ClearMemory);
                this.entitiesArchetypes = new MemoryList<Archetype>(worldConfig.StartEntitiesAmount, ref AllocatorWrapperRef);
                this.pools = new MemoryList<GenericPool>(ComponentAmount.Value.Data + 1, ref AllocatorWrapperRef);
                this.queries = new MemoryList<_Ptr<QueryUnsafe>>(64, ref AllocatorWrapperRef);
                this.archetypesList = new UnsafePtrList<ArchetypeUnsafe>(32, Allocator);
                this.archetypesMap = new UnsafeHashMap<int, Archetype>(32, Allocator);
                this.config = worldConfig;
                this.systemsUpdateJobDependencies = default;
                this.systemsFixedUpdateJobDependencies = default;
                this.DefaultNoneTypes = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(12, Allocator, NativeArrayOptions.ClearMemory);
                this.job_worker_count = JobsUtility.JobWorkerMaximumCount;
                this.entitiesAmount = 0;
                this.lastEntityIndex = 1;
                this.poolsCount = 0;
                this.lastDestroyedEntity = 0;
                this.ECBUpdate = new EntityCommandBuffer(256, Allocator);
                this.locking = Locking.Create(Allocator);
                this.aspects = new Aspects(Allocator, id);
                
                this.self = worldSelf;
                
                _ = ComponentType<DestroyEntity>.Index;
                _ = ComponentType<EntityCreated>.Index;
                _ = ComponentType<IsPrefab>.Index;
                SetDefaultNone();
                CreatePools();
                CreateRootArchetype();
            }

            // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // internal QueryUnsafe* Query(bool withDefaultNoneTypes = true) {
            //     var ptr = QueryUnsafe.Create(self, withDefaultNoneTypes);
            //     queries.Add(ptr);
            //     return ptr;
            // }

            internal _Ptr<QueryUnsafe> QueryPtr(bool withDefaultNoneTypes = true)
            {
                var ptr = QueryUnsafe.CreatePtr(self, withDefaultNoneTypes);
                queries.Add(ptr, ref AllocatorWrapperRef);
                return ptr;
            }
            internal void RefreshArchetypes()
            {
                for (int i = 0; i < archetypesList.m_length; i++)
                {
                    var archetype = archetypesList.Ptr[i];
                    archetype->Refresh();
                }
            }
            private void SetDefaultNone() {
                DefaultNoneTypes.Add(ComponentType<IsPrefab>.Index);
                DefaultNoneTypes.Add(ComponentType<DestroyEntity>.Index);
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetPool<T>() where T : unmanaged {
                var poolIndex = ComponentType<T>.Index;
                ref var pool = ref pools.Ptr[poolIndex];
                if (!pool.IsCreated)
                {
                    AddPool<T>(ref pool, poolIndex);
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
            //[BurstDiscard]
            private void AddPool<T>(ref GenericPool pool, int index) where T : unmanaged
            {
                locking.Lock();
                try {
                    if (!pool.IsCreated)
                    {
                        pool = GenericPool.Create<T>(config.StartPoolSize, self);
                        poolsCount++;
                    }
                }
                finally {
                    locking.Unlock();
                }
            }

            private void AddPool(ref GenericPool pool, int index)
            {
                locking.Lock();
                try {
                    if (!pool.IsCreated) {
                        pool = GenericPool.Create(ComponentTypeMap.GetComponentType(index), config.StartPoolSize,
                            self);
                        poolsCount++;
                    }
                }
                finally {
                    locking.Unlock();
                }
            }

            internal void CreatePools()
            {
                ComponentTypeMap.CreatePools(ref pools, config.StartPoolSize, self, ref poolsCount);
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
                    entities.Resize(newCapacity, ref AllocatorWrapperRef);
                    entitiesArchetypes.Resize(newCapacity, ref AllocatorWrapperRef);
                    // UnsafeHelp.ResizeUnsafeList(ref entities, newCapacity);
                    // UnsafeHelp.ResizeUnsafeList(ref entitiesArchetypes, newCapacity);
                }
                Entity e;
                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.m_length > 0) {
                    last = reservedEntities.ElementAt(reservedEntities.m_length - 1);
                    reservedEntities.RemoveAt(reservedEntities.m_length - 1);
                    e = new Entity(last, self);
                    entities.ElementAt(last) = e;
                    return e;
                }
                e = new Entity(last, self);
                entities.ElementAt(last) = e;
                lastEntityIndex++;
                return e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Entity CreateEntity(int archetype) {
                if (lastEntityIndex >= entities.capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    entities.Resize(newCapacity, ref AllocatorWrapperRef);
                    entitiesArchetypes.Resize(newCapacity, ref AllocatorWrapperRef);
                }

                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.m_length > 0) {
                    last = reservedEntities.ElementAtNoCheck(reservedEntities.m_length - 1);
                    reservedEntities.RemoveAt(reservedEntities.m_length - 1);
                }
                else
                {
                    lastEntityIndex++;
                }
                var e = new Entity(last, self, archetype);
                entities.ElementAt(last) = e;
                
                return e;
            }
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void OnDestroyEntity(int entity) {
                entities.Ptr[entity] = Nukecs.Entity.Null;
                reservedEntities.Add(entity);
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
                prefabsToSpawn.Add(e, ref AllocatorWrapperRef);
                return e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref Entity GetEntity(int id) {
                return ref entities.Ptr[id];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Archetype CreateArchetype(params int[] types) {
                var ptr = ArchetypeUnsafe.Create(self, types);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype CreateArchetype(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<int> types, bool copyList = false) {
                var ptr = ArchetypeUnsafe.Create(self, ref types, copyList);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)][BurstDiscard]
            internal void CreateArchetype(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<int> types, out Archetype archetype) {
                var ptr = ArchetypeUnsafe.Create(self, ref types);
                archetype = new Archetype();
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                //return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Archetype CreateRootArchetype() {
                var ptr = ArchetypeUnsafe.Create(self);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype GetOrCreateArchetype(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<int> types, bool copyList = false) {
                var hash = ArchetypeUnsafe.GetHashCode(ref types);
                if (archetypesMap.TryGetValue(hash, out var archetype)) {
                    types.Dispose();
                    return archetype;
                }

                return CreateArchetype(ref types);
            }
            [BurstDiscard][MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void GetOrCreateArchetype(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<int> types, out Archetype archetype) {
                archetype = GetOrCreateArchetype(ref types);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype GetArchetype(int hash) {
                return archetypesMap[hash];
            }

            internal void Update()
            {
                ECB.Playback(self);
            }
        }
    }
}