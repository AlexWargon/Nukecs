using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe struct World : IDisposable {
        private static readonly World[] worlds = new World[4];
        private static int lastFreeSlot;
        private static int lastWorldID;
        public static ref World Get(int index) => ref worlds[index];

        public static World Create() {
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.Unsafe = WorldUnsafe.Create(id, WorldConfig.Default_1_000_000);
            worlds[id] = world;
            return world;
        }
        public static World Create(WorldConfig config) {
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.Unsafe = WorldUnsafe.Create(id, config);
            worlds[id] = world;
            return world;
        }
        public static void DisposeStatic() {
            ComponentsMap.ComponentTypes.Data.Dispose();
            ComponentsMap.Save();
        }
        public bool IsAlive => Unsafe != null;
        [NativeDisableUnsafePtrRestriction] 
        internal WorldUnsafe* Unsafe;
        internal ref EntityCommandBuffer ECB => ref Unsafe->ECB;
        internal ref EntityFilterBuffer EFB => ref Unsafe->EFB;
        //public ref UntypedUnsafeList GetPool<T>() where T : unmanaged => ref _impl->GetPool<T>();
        internal unsafe struct WorldUnsafe {
            internal int Id;
            internal Allocator allocator;
            internal UnsafeList<Entity> entities;
            internal UnsafeList<Archetype> entitiesArchetypes;
            internal UnsafeList<GenericPool> pools;
            internal int poolsCount;
            internal UnsafePtrList<Query.QueryUnsafe> queries;
            internal UnsafeHashMap<int, Archetype> archetypesMap;
            internal UnsafePtrList<ArchetypeImpl> archetypesList;
            internal int lastEntityIndex;
            internal WorldConfig config;
            internal DynamicBitmask poolsMask;
            internal EntityCommandBuffer ECB;
            internal EntityFilterBuffer EFB;
            [NativeDisableUnsafePtrRestriction] 
            internal WorldUnsafe* self;
            
            internal static WorldUnsafe* Create(int id, WorldConfig config) {
                var ptr = Wargon.Nukecs.Unsafe.Malloc<WorldUnsafe>(Allocator.Persistent);
                *ptr = new WorldUnsafe(id, config, Allocator.Persistent);
                ptr->Init(ptr);
                return ptr;
            }

            internal Query.QueryUnsafe* CreateQuery() {
                var ptr = Query.QueryUnsafe.Create(self);
                queries.Add(ptr);
                return ptr;
            }

            public WorldUnsafe(int id, WorldConfig config, Allocator allocator, WorldUnsafe* self = null) {
                this.Id = id;
                this.allocator = allocator;
                this.entities = UnsafeHelp.UnsafeListWithMaximumLenght<Entity>(config.StartEntitiesAmount, allocator,
                    NativeArrayOptions.ClearMemory);
                this.entitiesArchetypes = UnsafeHelp.UnsafeListWithMaximumLenght<Archetype>(config.StartEntitiesAmount,
                    allocator, NativeArrayOptions.ClearMemory);
                this.pools = UnsafeHelp.UnsafeListWithMaximumLenght<GenericPool>(config.StartComponentsAmount, allocator,
                    NativeArrayOptions.ClearMemory);
                this.queries = new UnsafePtrList<Query.QueryUnsafe>(32, allocator);
                this.archetypesList = new UnsafePtrList<ArchetypeImpl>(32, allocator);
                this.archetypesMap = new UnsafeHashMap<int, Archetype>(32, allocator);
                this.lastEntityIndex = 1;
                this.poolsCount = 0;
                this.config = config;
                this.poolsMask = DynamicBitmask.CreateForComponents();
                this.ECB = new EntityCommandBuffer(256);
                this.EFB = new EntityFilterBuffer(256);
                this.self = self;
                var s = ComponentType<DestroyEntity>.Index;
            }

            internal void Init(WorldUnsafe* self) {
                this.self = self;
                CreateArchetype();
            }

            public void Free() {
                entities.Dispose();
                entitiesArchetypes.Dispose();
                for (var index = 0; index < poolsCount; index++) {
                    var pool = pools[index];
                    pool.Dispose();
                }

                pools.Dispose();
                for (var index = 0; index < queries.Length; index++) {
                    Query.QueryUnsafe* ptr = queries[index];
                    Query.QueryUnsafe.Free(ptr);
                }

                queries.Dispose();
                foreach (var kvPair in archetypesMap) {
                    kvPair.Value.Dispose();
                }

                archetypesList.Dispose();
                archetypesMap.Dispose();
                poolsCount = 0;
                poolsMask.Dispose();
                ECB.Dispose();
                EFB.Dispose();
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
            [BurstDiscard]
            private void LogPool<T>(int index) {
                Debug.Log($"Pool [{typeof(T).Name}. Index {index}]");
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetUntypedPool(int poolIndex) {
                ref var pool = ref pools.ElementAt(poolIndex);
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
                    entities.Resize(lastEntityIndex * 2);
                    entities.m_length = entities.m_capacity;
                    
                    entitiesArchetypes.Resize(lastEntityIndex * 2);
                    entitiesArchetypes.m_length = entitiesArchetypes.m_capacity;
                }
                var e = new Entity(lastEntityIndex, self);
                entities.ElementAt(lastEntityIndex) = e;
                lastEntityIndex++;
                return e;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref Entity GetEntity(int id) {
                return ref entities.ElementAt(id);
            }

            public Archetype CreateArchetype(params int[] types) {
                var ptr = ArchetypeImpl.Create(self, types);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }

            internal Archetype CreateArchetype(ref UnsafeList<int> types) {
                var ptr = ArchetypeImpl.Create(self, ref types);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }

            private Archetype CreateArchetype() {
                var ptr = ArchetypeImpl.Create(self);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            
            internal Archetype GetOrCreateArchetype(ref UnsafeList<int> types) {
                var hash = ArchetypeImpl.GetHashCode(ref types);
                if (archetypesMap.TryGetValue(hash, out var archetype)) {
                    return archetype;
                }

                return CreateArchetype(ref types);
            }
            [BurstDiscard]
            internal void GetOrCreateArchetype(ref UnsafeList<int> types, out Archetype archetype) {
                archetype = GetOrCreateArchetype(ref types);
            }
            internal Archetype GetArchetype(int hash) {
                return archetypesMap[hash];
            }
        }

        public void Dispose() {
            if (Unsafe == null) return;
            var id = Unsafe->Id;
            lastFreeSlot = id;
            Unsafe->Free();
            var allocator = Unsafe->allocator;
            UnsafeUtility.Free(Unsafe, allocator);
            Unsafe = null;
            Debug.Log($"World {id} Disposed. World slot {lastFreeSlot} free");
        }

        public ref GenericPool GetPool<T>() where T : unmanaged {
            return ref Unsafe->GetPool<T>();
        }

        public Entity CreateEntity() {
            return Unsafe->CreateEntity();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int id) {
            return ref Unsafe->GetEntity(id);
        }

        public Query CreateQuery() {
            return new Query(Unsafe->CreateQuery());
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
        public static WorldConfig Default16384 => new WorldConfig() {
            StartPoolSize = 16384,
            StartEntitiesAmount = 16384,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default163840 => new WorldConfig() {
            StartPoolSize = 163840,
            StartEntitiesAmount = 163840,
            StartComponentsAmount = 32
        };
        public static WorldConfig Default_1_000_000 => new WorldConfig() {
            StartPoolSize = 1_000_001,
            StartEntitiesAmount = 1_000_001,
            StartComponentsAmount = 32
        };
    }



}