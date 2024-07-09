using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe struct World : IDisposable {
        private static readonly World[] worlds = new World[4];
        public static ref World Get(int index) => ref worlds[index];

        public static World Create() {
            World world;
            world._impl = WorldImpl.Create(worlds.Length, WorldConfig.Default);
            return world;
        }

        [NativeDisableUnsafePtrRestriction] internal WorldImpl* _impl;
        internal ref EntityCommandBuffer ecb => ref _impl->ECB;

        //public ref UntypedUnsafeList GetPool<T>() where T : unmanaged => ref _impl->GetPool<T>();
        internal unsafe struct WorldImpl {
            internal int Id;
            internal Allocator allocator;
            internal UnsafeList<Entity> entities;
            internal UnsafeList<GenericPool> pools;
            internal int poolsCount;
            internal UnsafePtrList<Query.QueryImpl> queries;
            internal UnsafeHashMap<int, Archetype> archetypesMap;
            internal UnsafePtrList<Archetype.ArchetypeImpl> archetypesList;
            internal int lastEntityIndex;
            internal WorldConfig config;
            [NativeDisableUnsafePtrRestriction] internal WorldImpl* self;
            internal DynamicBitmask poolsMask;
            internal EntityCommandBuffer ECB;

            internal static WorldImpl* Create(int id, WorldConfig config) {
                var ptr = Unsafe.Malloc<WorldImpl>(Allocator.Persistent);
                *ptr = new WorldImpl(id, config, Allocator.Persistent);
                ptr->Init(ptr);
                return ptr;
            }

            internal Query.QueryImpl* CreateQuery() {
                var ptr = Query.QueryImpl.Create(self);
                queries.Add(ptr);
                return ptr;
            }

            public WorldImpl(int id, WorldConfig config, Allocator allocator, WorldImpl* self = null) {
                this.Id = id;
                this.allocator = allocator;
                this.entities = UnsafeHelp.UnsafeListWithMaximumLenght<Entity>(config.StartEntitiesAmount, allocator,
                    NativeArrayOptions.ClearMemory);
                this.entities.m_length = this.entities.m_capacity;
                this.pools = UnsafeHelp.UnsafeListWithMaximumLenght<GenericPool>(IComponent.Count() + 1, allocator,
                    NativeArrayOptions.ClearMemory);
                this.queries = new UnsafePtrList<Query.QueryImpl>(32, allocator);
                this.archetypesList = new UnsafePtrList<Archetype.ArchetypeImpl>(32, allocator);
                this.archetypesMap = new UnsafeHashMap<int, Archetype>(32, allocator);
                this.lastEntityIndex = 0;
                this.poolsCount = 0;
                this.config = config;
                this.poolsMask = DynamicBitmask.CreateForComponents();
                this.ECB = new EntityCommandBuffer(256);
                this.self = self;
                var s = ComponentMeta<DestroyEntity>.Index;
            }

            internal void Init(WorldImpl* self) {
                this.self = self;
                CreateArchetype();
            }

            public void Free() {
                entities.Dispose();
                for (var index = 0; index < poolsCount; index++) {
                    var pool = pools[index];
                    pool.Dispose();
                }

                pools.Dispose();
                for (var index = 0; index < queries.Length; index++) {
                    Query.QueryImpl* ptr = queries[index];
                    Query.QueryImpl.Free(ptr);
                }

                queries.Dispose();
                foreach (var kvPair in archetypesMap) {
                    kvPair.Value.Dispose();
                }

                archetypesList.Dispose();
                archetypesMap.Dispose();
                poolsCount = 0;
                poolsMask.Dispose();
                self = null;
            }

            internal ref GenericPool GetPool<T>() where T : unmanaged {
                var poolIndex = ComponentMeta<T>.Index;
                ref var pool = ref pools.ElementAt(poolIndex);
                if (pool.impl == null) {
                    pool = GenericPool.Create<T>(config.StartPoolSize, allocator);
                    poolsCount++;
                }

                return ref pool;
            }

            internal ref GenericPool GetUntypedPool(int poolIndex) {
                ref var pool = ref pools.ElementAt(poolIndex);
                if (pool.impl == null) {
                    pool = GenericPool.Create(ComponentsMap.GetType(poolIndex), config.StartPoolSize, allocator);
                    poolsCount++;
                }

                return ref pool;
            }

            internal void EntityAddComponent<T>(int id, T componeet) where T : unmanaged { }

            internal Entity CreateEntity() {
                var e = new Entity(lastEntityIndex, self);
                if (lastEntityIndex >= entities.m_capacity) {
                    entities.Resize(lastEntityIndex * 2);
                    entities.m_length = entities.m_capacity;
                }
                entities.ElementAt(lastEntityIndex) = e;
                lastEntityIndex++;
                return e;
            }

            internal ref Entity GetEntity(int id) {
                return ref entities.ElementAt(id);
            }
            [BurstDiscard]
            public Archetype CreateArchetype(params int[] types) {
                var ptr = Archetype.ArchetypeImpl.Create(self, types);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [BurstDiscard]
            internal Archetype CreateArchetype(ref UnsafeList<int> types) {
                var ptr = Archetype.ArchetypeImpl.Create(self, ref types);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [BurstDiscard]
            private Archetype CreateArchetype() {
                var ptr = Archetype.ArchetypeImpl.Create(self);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }
            [BurstDiscard]
            internal Archetype GetOrCreateArchetype(ref UnsafeList<int> types) {
                var hash = Archetype.ArchetypeImpl.GetHashCode(ref types);
                if (archetypesMap.TryGetValue(hash, out var archetype)) {
                    return archetype;
                }

                return CreateArchetype(ref types);
            }

            internal Archetype GetArchetype(int hash) {
                return archetypesMap[hash];
            }
        }

        public void Dispose() {
            if (_impl == null) return;
            var id = _impl->Id;
            _impl->Free();
            var allocator = _impl->allocator;
            UnsafeUtility.Free(_impl, allocator);
            Debug.Log($"World {id} Disposed");
        }

        public ref GenericPool GetPool<T>() where T : unmanaged {
            return ref _impl->GetPool<T>();
        }

        public Entity CreateEntity() {
            return _impl->CreateEntity();
        }

        public ref Entity GetEntity(int id) {
            return ref _impl->GetEntity(id);
        }

        public Query CreateQuery() {
            return new Query(_impl->CreateQuery());
        }
    }

    public struct IsAlive : IComponent { }

    public struct WorldConfig {
        public int StartEntitiesAmount;
        public int StartPoolSize;
        public int StartComponentsAmount;
        public Allocator WorldAllocator => Allocator.Persistent;

        public static WorldConfig Default => new WorldConfig() {
            StartPoolSize = 128,
            StartEntitiesAmount = 128,
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
    }
}