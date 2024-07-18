using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
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
            world.impl = WorldImpl.Create(id, WorldConfig.Default_1_000_000);
            worlds[id] = world;
            new Systems.WarmupJob().Schedule().Complete();
            
            return world;
        }
        public static World Create(WorldConfig config) {
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.impl = WorldImpl.Create(id, config);
            worlds[id] = world;
            new Systems.WarmupJob().Schedule().Complete();
            
            return world;
        }
        public static void DisposeStatic() {
            ComponentsMap.ComponentTypes.Data.Dispose();
            ComponentsMap.Save();
        }
        public bool IsAlive => impl != null;
        [NativeDisableUnsafePtrRestriction] internal WorldImpl* impl;
        internal ref EntityCommandBuffer ECB => ref impl->ECB;
        internal ref EntityFilterBuffer EFB => ref impl->EFB;
        //public ref UntypedUnsafeList GetPool<T>() where T : unmanaged => ref _impl->GetPool<T>();
        internal unsafe struct WorldImpl {
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
            internal WorldImpl* self;
            
            internal static WorldImpl* Create(int id, WorldConfig config) {
                var ptr = Unsafe.Malloc<WorldImpl>(Allocator.Persistent);
                *ptr = new WorldImpl(id, config, Allocator.Persistent);
                ptr->Init(ptr);
                return ptr;
            }

            internal Query.QueryUnsafe* CreateQuery() {
                var ptr = Query.QueryUnsafe.Create(self);
                queries.Add(ptr);
                return ptr;
            }

            public WorldImpl(int id, WorldConfig config, Allocator allocator, WorldImpl* self = null) {
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

            internal void Init(WorldImpl* self) {
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
                ref var pool = ref pools.ElementAt(poolIndex);
                if (pool.impl == null) {
                    pool = GenericPool.Create<T>(config.StartPoolSize, allocator);
                    poolsCount++;
                }
                return ref pool;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref GenericPool GetUntypedPool(int poolIndex) {
                ref var pool = ref pools.ElementAt(poolIndex);
                if (pool.impl == null) {
                    pool = GenericPool.Create(ComponentsMap.GetComponentType(poolIndex), config.StartPoolSize, allocator);
                    poolsCount++;
                }
                return ref pool;
            }

            internal void EntityAddComponent<T>(int id, T componeet) where T : unmanaged { }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Entity CreateEntity() {
                var e = new Entity(lastEntityIndex, self);
                if (lastEntityIndex >= entities.m_capacity) {
                    entities.Resize(lastEntityIndex * 2, NativeArrayOptions.ClearMemory);
                    entities.m_length = entities.m_capacity;
                }
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
            if (impl == null) return;
            var id = impl->Id;
            lastFreeSlot = id;
            impl->Free();
            var allocator = impl->allocator;
            UnsafeUtility.Free(impl, allocator);
            impl = null;
            Debug.Log($"World {id} Disposed. World slot {lastFreeSlot} free");
            
        }

        public ref GenericPool GetPool<T>() where T : unmanaged {
            return ref impl->GetPool<T>();
        }

        public Entity CreateEntity() {
            return impl->CreateEntity();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int id) {
            return ref impl->GetEntity(id);
        }

        public Query CreateQuery() {
            return new Query(impl->CreateQuery());
        }
    }

    public struct IsAlive : IComponent { }

    public struct WorldConfig {
        public int StartEntitiesAmount;
        public int StartPoolSize;
        public int StartComponentsAmount;
        public Allocator WorldAllocator => Allocator.Persistent;

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

    public static class UnsafeHelp {
        public static UnsafeList<T> UnsafeListWithMaximumLenght<T>(int size, Allocator allocator,
            NativeArrayOptions options) where T : unmanaged {
            return new UnsafeList<T>(size, allocator, options) {
                m_length = size
            };
        }
        public static unsafe UnsafeList<T>* UnsafeListPtrWithMaximumLenght<T>(int size, Allocator allocator,
            NativeArrayOptions options) where T : unmanaged {
            var ptr = UnsafeList<T>.Create(size, allocator, options);
            ptr->m_length = size;
            return ptr;
        }
    }

}