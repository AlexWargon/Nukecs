using UnityEngine;

namespace Wargon.Nukecs {

    using System;
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;


    public unsafe struct World : IDisposable {
        private static readonly World[] worlds = new World[4];
        private static int lastFreeSlot;
        private static int lastWorldID;
        public static ref World Get(int index) => ref worlds[index];
        // ReSharper disable once InconsistentNaming
        public static ref World Default => ref Get(0);
        public static World Create() {
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.UnsafeWorld = WorldUnsafe.Create(id, WorldConfig.Default_1_000_000);
            worlds[id] = world;
            return world;
        }
        public static World Create(WorldConfig config) {
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.UnsafeWorld = WorldUnsafe.Create(id, config);
            worlds[id] = world;
            return world;
        }
        public static void DisposeStatic() {
            ComponentsMap.Dispose();
            ComponentsMap.Save();
        }
        public bool IsAlive => UnsafeWorld != null;
        [NativeDisableUnsafePtrRestriction] 
        internal WorldUnsafe* UnsafeWorld;

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
            internal UnsafeList<Entity> prefabesToSpawn;
            internal UnsafeList<int> reservedEntities;
            internal UnsafeList<Archetype> entitiesArchetypes;
            internal UnsafeList<GenericPool> pools;
            internal int poolsCount;
            internal UnsafePtrList<QueryUnsafe> queries;
            internal UnsafeHashMap<int, Archetype> archetypesMap;
            internal UnsafePtrList<ArchetypeImpl> archetypesList;
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
                var ptr = Wargon.Nukecs.Unsafe.Malloc<WorldUnsafe>(Allocator.Persistent);
                *ptr = new WorldUnsafe(id, config, Allocator.Persistent);
                ptr->Init(ptr);
                return ptr;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal QueryUnsafe* Query() {
                var ptr = QueryUnsafe.Create(self);
                queries.Add(ptr);
                return ptr;
            }

            public WorldUnsafe(int id, WorldConfig config, Allocator allocator, WorldUnsafe* self = null) {
                this.Id = id;
                this.allocator = allocator;
                this.entities = UnsafeHelp.UnsafeListWithMaximumLenght<Entity>(config.StartEntitiesAmount, allocator,
                    NativeArrayOptions.ClearMemory);
                this.prefabesToSpawn = new UnsafeList<Entity>(64, allocator, NativeArrayOptions.ClearMemory);
                this.reservedEntities = new UnsafeList<int>(128, allocator, NativeArrayOptions.ClearMemory);
                this.entitiesArchetypes = UnsafeHelp.UnsafeListWithMaximumLenght<Archetype>(config.StartEntitiesAmount,
                    allocator, NativeArrayOptions.ClearMemory);
                this.pools = UnsafeHelp.UnsafeListWithMaximumLenght<GenericPool>(ComponentAmount.Value.Data + 1, allocator,
                    NativeArrayOptions.ClearMemory);
                this.queries = new UnsafePtrList<QueryUnsafe>(32, allocator);
                this.archetypesList = new UnsafePtrList<ArchetypeImpl>(32, allocator);
                this.archetypesMap = new UnsafeHashMap<int, Archetype>(32, allocator);

                this.config = config;
                this.systemsUpdateJobDependencies = default;
                this.systemsFixedUpdateJobDependencies = default;
                this.DefaultNoneTypes = new UnsafeList<int>(12, allocator, NativeArrayOptions.ClearMemory);
                
                this.job_worker_count = JobsUtility.JobWorkerMaximumCount;
                this.entitiesAmount = 0;
                this.lastEntityIndex = 1;
                this.poolsCount = 0;
                this.ECBUpdate = new EntityCommandBuffer(256);
                this.ECBFixed = new EntityCommandBuffer(256);
                this.currentContext = UpdateContext.Update;
                this.self = self;
                _ = ComponentType<DestroyEntity>.Index;
                _ = ComponentType<EntityCreated>.Index;
                _ = ComponentType<IsPrefab>.Index;
                SetDefaultNone();
            }

            private void SetDefaultNone() {
                DefaultNoneTypes.Add(ComponentType<IsPrefab>.Index);
            }
            internal void Init(WorldUnsafe* self) {
                this.self = self;
                CreateArchetype();
            }
            public void Free() {

                var entitiesToClear = entitiesAmount + reservedEntities.Length + 1;
                for (var i = 0; i < entitiesToClear; i++) {
                    ref var entity = ref entities.ElementAt(i);
                    if (entity != Nukecs.Entity.Null) {
                        entity.Free();
                    }
                }
                
                WorldSystems.CompleteAll(Id);

                entities.Dispose();
                entitiesArchetypes.Dispose();
                for (var index = 0; index < poolsCount; index++) {
                    var pool = pools[index];
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
                prefabesToSpawn.Dispose();
                self = null;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetPool<T>() where T : unmanaged {
                var poolIndex = ComponentType<T>.Index;
                ref var pool = ref pools.ElementAtNoCheck(poolIndex);
                if (!pool.IsCreated) {
                    pool = GenericPool.Create<T>(config.StartPoolSize, allocator);
                    poolsCount++;
                }
                return ref pool;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetUntypedPool(int poolIndex) {
                ref var pool = ref pools.ElementAtNoCheck(poolIndex);
                if (!pool.IsCreated) {
                    pool = GenericPool.Create(ComponentsMap.GetComponentType(poolIndex), config.StartPoolSize, allocator);
                    poolsCount++;
                }
                return ref pool;
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void OnDestroyEntity(int entity) {
                entities.ElementAtNoCheck(entity) = Nukecs.Entity.Null;
                reservedEntities.Add(entity);
                entitiesAmount--;
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
                prefabesToSpawn.Add(e);
                return e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref Entity GetEntity(int id) {
                return ref entities.ElementAtNoCheck(id);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Archetype CreateArchetype(params int[] types) {
                var ptr = ArchetypeImpl.Create(self, types);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype CreateArchetype(ref UnsafeList<int> types) {
                var ptr = ArchetypeImpl.Create(self, ref types);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Archetype CreateArchetype() {
                var ptr = ArchetypeImpl.Create(self);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Archetype GetOrCreateArchetype(ref UnsafeList<int> types) {
                var hash = ArchetypeImpl.GetHashCode(ref types);
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

            internal void Dispose<T>(ref GenericPool pool, ref Entity entity) where T : unmanaged, IComponent, IDisposable
            {
                ref var component = ref pool.GetRef<T>(entity.id);
                component.Dispose();
                pool.Set(entity.id, default(T));
                ECB.Remove<T>(entity.id);
                ECB.Remove<Dispose<T>>(entity.id);
            }
        }

        public void Dispose() {
            if (UnsafeWorld == null) return;
            var id = UnsafeWorld->Id;
            lastFreeSlot = id;
            UnsafeWorld->Free();
            var allocator = UnsafeWorld->allocator;
            UnsafeUtility.Free(UnsafeWorld, allocator);
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

        public Query Query() {
            return new Query(UnsafeWorld->Query());
        }

        public Query Query<T>() where T: struct, ITuple {
            return new Query(UnsafeWorld->Query());
        }
        public Query Query<T>(byte dymmy = 1) where T: struct, IFilter {
            return new Query(UnsafeWorld->Query());
        }
        public void Update() {
            UnsafeWorld->ECB.Playback(ref this);
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
}