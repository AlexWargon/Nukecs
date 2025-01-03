﻿using System.Threading;
using UnityEngine;
using Wargon.Nukecs;

namespace Wargon.Nukecs {

    using System;
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    public static class Lockers {
        public static readonly ClassPtr<object> pools;
    }
    public unsafe partial struct World : IDisposable {
        public const Allocator Allocator = Unity.Collections.Allocator.Persistent;
        private static readonly World[] worlds = new World[4];
        private static int lastFreeSlot;
        private static int lastWorldID;
        public static ref World Get(int index) => ref worlds[index];
        // ReSharper disable once InconsistentNaming
        public static ref World Default {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                ref var w = ref Get(0);
                if (!w.IsAlive) {
                    w = Create();
                }
                return ref w;
            }
        }
        private static event Action OnWorldCreatingEvent;
        private static event Action OnDisposeStaticEvent;
        public static void OnWorldCreating(Action action)
        {
            OnWorldCreatingEvent += action;
        }

        public static void OnDisposeStatic(Action action)
        {
            OnDisposeStaticEvent += action;
        }
        public static World Create() {
            OnWorldCreatingEvent?.Invoke();
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.UnsafeWorld = WorldUnsafe.Create(id, WorldConfig.Default16384);
            worlds[id] = world;
            
            return world;
        }
        public static World Create(WorldConfig config) {
            OnWorldCreatingEvent?.Invoke();
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.UnsafeWorld = WorldUnsafe.Create(id, config);
            worlds[id] = world;
            
            return world;
        }
        public static void DisposeStatic() {
            ComponentTypeMap.Dispose();
            ComponentTypeMap.Save();
            OnDisposeStaticEvent?.Invoke();
            OnDisposeStaticEvent = null;
            OnWorldCreatingEvent = null;
            WorldSystems.Dispose();
        }
        public bool IsAlive => UnsafeWorld != null;
        public WorldConfig Config => UnsafeWorld->config;
        [NativeDisableUnsafePtrRestriction] 
        internal WorldUnsafe* UnsafeWorld;

        public int LastDestroyedEntity => UnsafeWorld->lastDestroyedEntity;
        public int EntitiesAmount => UnsafeWorld->entitiesAmount;
        internal ref EntityCommandBuffer ECB => ref UnsafeWorld->ECB;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref EntityCommandBuffer GetEcbVieContext(UpdateContext context) {
            return ref context == UpdateContext.Update ? ref UnsafeWorld->ECBUpdate : ref UnsafeWorld->ECBFixed;
        }
        internal UpdateContext CurrentContext {
            get => UnsafeWorld->CurrentContext;
            set => UnsafeWorld->CurrentContext = value;
        }
        public ref JobHandle DependenciesUpdate => ref UnsafeWorld->systemsUpdateJobDependencies;
        public ref JobHandle DependenciesFixedUpdate => ref UnsafeWorld->systemsUpdateJobDependencies;
        //public ref UntypedUnsafeList GetPool<T>() where T : unmanaged => ref _impl->GetPool<T>();


        internal struct WorldUnsafe {
            internal readonly int Id;
            internal readonly Allocator allocator;
            internal UnsafeList<Entity> entities;
            internal UnsafeList<Entity> prefabsToSpawn;
            internal UnsafeList<int> reservedEntities;
            internal UnsafeList<Archetype> entitiesArchetypes;
            internal UnsafeList<GenericPool> pools;
            internal int poolsCount;
            internal UnsafePtrList<QueryUnsafe> queries;
            internal UnsafeHashMap<int, Archetype> archetypesMap;
            internal UnsafePtrList<ArchetypeUnsafe> archetypesList;
            internal WorldConfig config;
            internal EntityCommandBuffer ECBUpdate;
            internal EntityCommandBuffer ECBFixed;
            private UpdateContext currentContext;
            internal JobHandle systemsUpdateJobDependencies;
            internal JobHandle systemsFixedUpdateJobDependencies;
            internal readonly int job_worker_count;
            internal UnsafeList<int> DefaultNoneTypes;
            internal int entitiesAmount;
            internal int lastEntityIndex;
            internal int lastDestroyedEntity;
            internal Locking locking;
            [NativeDisableUnsafePtrRestriction] internal WorldUnsafe* self;
            internal ref EntityCommandBuffer ECB {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref currentContext == UpdateContext.Update ? ref self->ECBUpdate : ref self->ECBFixed;
            }
            internal UpdateContext CurrentContext {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => currentContext;
                [MethodImpl(MethodImplOptions.AggressiveInlining)] set => currentContext = value;
            }
            internal static WorldUnsafe* Create(int id, WorldConfig config) {
                var ptr = Unsafe.AllocateWithManager<WorldUnsafe>(AllocatorManager.Persistent);
                *ptr = new WorldUnsafe(id, config, Allocator.Persistent);
                ptr->Init(ptr);
                return ptr;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal QueryUnsafe* Query(bool withDefaultNoneTypes = true) {
                var ptr = QueryUnsafe.Create(self, withDefaultNoneTypes);
                queries.Add(ptr);
                return ptr;
            }

            public WorldUnsafe(int id, WorldConfig config, Allocator allocator, WorldUnsafe* self = null) {
                this.Id = id;
                this.allocator = allocator;
                this.entities = UnsafeHelp.UnsafeListWithMaximumLenght<Entity>(config.StartEntitiesAmount, allocator,
                    NativeArrayOptions.ClearMemory);
                this.prefabsToSpawn = new UnsafeList<Entity>(64, allocator, NativeArrayOptions.ClearMemory);
                this.reservedEntities = new UnsafeList<int>(128, allocator, NativeArrayOptions.ClearMemory);
                this.entitiesArchetypes = UnsafeHelp.UnsafeListWithMaximumLenght<Archetype>(config.StartEntitiesAmount,
                    allocator, NativeArrayOptions.ClearMemory);
                this.pools = UnsafeHelp.UnsafeListWithMaximumLenght<GenericPool>(ComponentAmount.Value.Data + 1, allocator,
                    NativeArrayOptions.ClearMemory);
                this.queries = new UnsafePtrList<QueryUnsafe>(32, allocator);
                this.archetypesList = new UnsafePtrList<ArchetypeUnsafe>(32, allocator);
                this.archetypesMap = new UnsafeHashMap<int, Archetype>(32, allocator);
                this.config = config;
                this.systemsUpdateJobDependencies = default;
                this.systemsFixedUpdateJobDependencies = default;
                this.DefaultNoneTypes = new UnsafeList<int>(12, allocator, NativeArrayOptions.ClearMemory);
                this.job_worker_count = JobsUtility.JobWorkerMaximumCount;
                this.entitiesAmount = 0;
                this.lastEntityIndex = 1;
                this.poolsCount = 0;
                this.lastDestroyedEntity = 0;
                this.ECBUpdate = new EntityCommandBuffer(256);
                this.ECBFixed = new EntityCommandBuffer(256);
                this.currentContext = UpdateContext.Update;
                this.locking = Locking.Create();
                this.self = self;
                _ = ComponentType<DestroyEntity>.Index;
                _ = ComponentType<EntityCreated>.Index;
                _ = ComponentType<IsPrefab>.Index;
                SetDefaultNone();
                CreatePools();
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
            internal void Init(WorldUnsafe* self) {
                this.self = self;
                CreateArchetype();
            }
            public void Free() {
                foreach (var entity in entities) {
                    if (entity != Nukecs.Entity.Null) {
                        entity.Free();
                    }
                }
                //var entitiesToClear = entitiesAmount + reservedEntities.Length + 1;
                // for (var i = 0; i < entitiesAmount; i++) {
                //     ref var entity = ref entities.ElementAt(i);
                //     if (entity != Nukecs.Entity.Null) {
                //         entity.Free();
                //     }
                // }
                
                WorldSystems.CompleteAll(Id);

                entities.Dispose();
                entitiesArchetypes.Dispose();
                // pools list count == total components registered including arrays
                var poolsToDispose = ComponentAmount.Value.Data;
                for (var index = 0; index < poolsToDispose; index++) {
                    
                    ref var pool = ref pools.Ptr[index];
                    pool.Dispose();
                }
                pools.Dispose();
                
                for (var index = 0; index < queries.Length; index++) {
                    QueryUnsafe* ptr = queries[index];
                    QueryUnsafe.Free(ptr);
                }

                queries.Dispose();
                foreach (var kvPair in archetypesMap) {
                    kvPair.Value.Dispose();
                }
                
                archetypesList.Dispose();
                archetypesMap.Dispose();
                poolsCount = 0;
                ECBUpdate.Dispose();
                ECBFixed.Dispose();
                DefaultNoneTypes.Dispose();
                reservedEntities.Dispose();
                prefabsToSpawn.Dispose();
                locking.Dispose();
                Lockers.pools.Dispose();
                self = null;
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
                        pool = GenericPool.Create<T>(config.StartPoolSize, allocator);
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
                            allocator);
                        poolsCount++;
                    }
                }
                finally {
                    locking.Unlock();
                }
            }

            internal void CreatePools()
            {
                ComponentTypeMap.CreatePools(ref pools, config.StartPoolSize, allocator, ref poolsCount);
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
                if (lastEntityIndex >= entities.m_capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    UnsafeHelp.ResizeUnsafeList(ref entities, newCapacity);
                    UnsafeHelp.ResizeUnsafeList(ref entitiesArchetypes, newCapacity);
                }
                Entity e;
                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.m_length > 0) {
                    last = reservedEntities.ElementAt(reservedEntities.m_length - 1);
                    reservedEntities.RemoveAt(reservedEntities.m_length - 1);
                    e = new Entity(last, self);
                    entities.ElementAtNoCheck(last) = e;
                    return e;
                }
                e = new Entity(last, self);
                entities.ElementAtNoCheck(last) = e;
                lastEntityIndex++;
                return e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Entity CreateEntity(int archetype) {
                if (lastEntityIndex >= entities.m_capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    UnsafeHelp.ResizeUnsafeList(ref entities, newCapacity);
                    UnsafeHelp.ResizeUnsafeList(ref entitiesArchetypes, newCapacity);
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
                entities.ElementAtNoCheck(last) = e;
                
                return e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Entity CreateEntityWithEvent(int archetype) {
                if (lastEntityIndex >= entities.m_capacity) {
                    var newCapacity = lastEntityIndex * 2;
                    UnsafeHelp.ResizeUnsafeList(ref entities, newCapacity);
                    UnsafeHelp.ResizeUnsafeList(ref entitiesArchetypes, newCapacity);
                }
                Entity e;
                entitiesAmount++;
                var last = lastEntityIndex;
                if (reservedEntities.m_length > 0) {
                    last = reservedEntities.ElementAtNoCheck(reservedEntities.m_length - 1);
                    reservedEntities.RemoveAt(reservedEntities.m_length - 1);
                }
                e = new Entity(last, self, archetype);
                entities.ElementAtNoCheck(last) = e;
                lastEntityIndex++;
                return e;
            }

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
                prefabsToSpawn.Add(e);
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
            internal Archetype CreateArchetype(ref UnsafeList<int> types, bool copyList = false) {
                var ptr = ArchetypeUnsafe.Create(self, ref types, copyList);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)][BurstDiscard]
            internal void CreateArchetype(ref UnsafeList<int> types, out Archetype archetype) {
                var ptr = ArchetypeUnsafe.Create(self, ref types);
                archetype = new Archetype();
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                //return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Archetype CreateArchetype() {
                var ptr = ArchetypeUnsafe.Create(self);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype GetOrCreateArchetype(ref UnsafeList<int> types, bool copyList = false) {
                var hash = ArchetypeUnsafe.GetHashCode(ref types);
                if (archetypesMap.TryGetValue(hash, out var archetype)) {
                    types.Dispose();
                    return archetype;
                }

                return CreateArchetype(ref types);
            }
            [BurstDiscard][MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void GetOrCreateArchetype(ref UnsafeList<int> types, out Archetype archetype) {
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

        public void Dispose() {
            //if (UnsafeWorld == null) return;
            var id = UnsafeWorld->Id;
            lastFreeSlot = id;
            UnsafeWorld-> Free();
            var allocator = UnsafeWorld->allocator;
            Unsafe.FreeWithManager(UnsafeWorld, allocator);
            UnsafeWorld = null;
            Debug.Log($"World {id} Disposed. World slot {lastFreeSlot} free");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref GenericPool GetPool<T>() where T : unmanaged {
            return ref UnsafeWorld->GetPool<T>();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Entity() {
            return UnsafeWorld->CreateEntity();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity SpawnPrefab(in Entity prefab) {
            return UnsafeWorld->SpawnPrefab(in prefab);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Entity<T1>(in T1 c1) where T1 : unmanaged, IComponent {
            return UnsafeWorld->CreateEntity(in c1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Entity<T1>() where T1 : unmanaged, IComponent {
            return UnsafeWorld->CreateEntity(default(T1));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Entity<T1,T2>(in T1 c1, in T2 c2) 
            where T1 : unmanaged, IComponent 
            where T2 : unmanaged, IComponent
        {
            return UnsafeWorld->CreateEntity(in c1, in c2);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int id) {
            return ref UnsafeWorld->GetEntity(id);
        }

        public Query Query(bool withDefaultNoneTypes = true) {
            return new Query(UnsafeWorld->Query(withDefaultNoneTypes));
        }
        
        public Query Query<T>() where T: struct, ITuple {
            return new Query(UnsafeWorld->Query());
        }
        public Query Query<T>(byte dymmy = 1) where T: struct, IFilter {
            return new Query(UnsafeWorld->Query());
        }
        /// <summary>
        /// Update dirty entities and queries
        /// </summary>
        public void Update() {
            UnsafeWorld->ECB.Playback(ref this);
            UnsafeWorld->ECBFixed.Playback(ref this);
        }
    }



    public struct WorldConfig {
        public int StartEntitiesAmount;
        public int StartPoolSize;
        public int StartComponentsAmount;
        public Allocator WorldAllocator => Allocator.Persistent;
        public static WorldConfig Default16 => new WorldConfig() {
            StartPoolSize = 16,
            StartEntitiesAmount = 16,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default => new WorldConfig() {
            StartPoolSize = 64,
            StartEntitiesAmount = 64,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default256 => new WorldConfig() {
            StartPoolSize = 256,
            StartEntitiesAmount = 256,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default1024 => new WorldConfig() {
            StartPoolSize = 1025,
            StartEntitiesAmount = 1025,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default16384 => new WorldConfig() {
            StartPoolSize = 16385,
            StartEntitiesAmount = 16385,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default65536 => new WorldConfig()
        {
            StartPoolSize = 65536,
            StartEntitiesAmount = 65536,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default163840 => new WorldConfig() {
            StartPoolSize = 163841,
            StartEntitiesAmount = 163841,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default256000 => new WorldConfig() {
            StartPoolSize = 256001,
            StartEntitiesAmount = 256001,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default_1_000_000 => new WorldConfig() {
            StartPoolSize = 1_000_001,
            StartEntitiesAmount = 1_000_001,
            StartComponentsAmount = 32
        };
    }

    public static class dbug
    {
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(object massage)
        {
            UnityEngine.Debug.Log(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(string massage)
        {
            UnityEngine.Debug.Log(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error(string massage)
        {
            UnityEngine.Debug.LogError(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error_no_componnet<T>(Entity entity)
        {
            UnityEngine.Debug.LogError($"entity: {entity.id}, has no componnet {typeof(T).Name}" );
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void warn(string massage)
        {
            UnityEngine.Debug.LogWarning(massage);
        }
    }

    public unsafe struct Locking : IDisposable {
        public NativeReference<int> locks;
        
        public static Locking Create() 
        {
            return new Locking() 
            {
                locks = new NativeReference<int>(0, Allocator.Persistent)
            };
        }

        public void Lock() {
            while (Interlocked.CompareExchange(ref *locks.GetUnsafePtrWithoutChecks(), 1, 0) != 0)
            {
                Unity.Burst.Intrinsics.Common.Pause();
            }
        }

        public void Unlock() {
            locks.Value = 0;
        }
        public void Dispose() {
            locks.Dispose();
        }
    }
    public unsafe struct WorldLock {
        public int locks;
        internal World.WorldUnsafe* world;
        public bool IsLocked => locks > 0;
        public bool IsMerging => locks < 0;
        public void Lock() {
            if(IsMerging) return;
            locks++;
        }

        public void Unlock() {
            if(IsMerging) return;
            locks--;
            if (locks == 0) {
                locks = -1;
                world->ECB.Playback(world);
                locks = 0;
            }
        }
    }
}