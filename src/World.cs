using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public unsafe struct World {
        private static World[] worlds = new World[4];
        public static ref World Get(int index) => ref worlds[index];

        public static World Craete() {
            World world;
            world._impl = WorldImpl.Create(worlds.Length, WorldConfig.Default);
            return world;
        }

        private WorldImpl* _impl;

        public Entity CreateEntity() {
            return _impl->CreateEntity();
        }

        public ref Entity GetEntity(int id) {
            return ref _impl->GetEntity(id);
        }

        public Query CreateQuery() {
            return new Query(_impl->CreateQuery());
        }
        public ref UntypedUnsafeList GetPool<T>() where T : unmanaged => ref _impl->GetPool<T>();
        internal unsafe struct WorldImpl {
            internal readonly int Id;
            internal readonly Allocator allocator;
            internal readonly UnsafeList<Entity> entities;
            internal readonly UntypedUnsafeList pools;
            internal readonly UnsafePtrList<Query.QueryImpl> queries;
            internal UnsafeHashMap<int, Archetype> archetypesMap;
            internal UnsafePtrList<Archetype.ArchetypeImpl> archetypesList;
            internal int lastEntityIndex;
            internal readonly WorldConfig config;
            internal readonly WorldImpl* self;
            internal static WorldImpl* Create(int id, WorldConfig config) {
                var ptr = Unsafe.Malloc<WorldImpl>(Allocator.Persistent);
                *ptr = new WorldImpl(id, config, Allocator.Persistent, ptr);
                return ptr;
            }

            internal Query.QueryImpl* CreateQuery() {
                var ptr = Query.QueryImpl.Create(self);
                queries.Add(ptr);
                return ptr;
            }
            public WorldImpl(int id, WorldConfig config, Allocator allocator, WorldImpl* self) {
                
                this.Id = id;
                this.allocator = allocator;
                this.entities = new UnsafeList<Entity>(config.StartEntitiesAmount, allocator);
                this.pools = new UntypedUnsafeList(typeof(UntypedUnsafeList),config.StartComponentsAmount, allocator);
                this.queries = new UnsafePtrList<Query.QueryImpl>(32, allocator);
                this.archetypesList = new UnsafePtrList<Archetype.ArchetypeImpl>(32, allocator);
                this.archetypesMap = new UnsafeHashMap<int, Archetype>(32, allocator);
                this.lastEntityIndex = 0;
                this.config = config;
                this.self = self;
                CreateArchetype();
            }
            internal ref UntypedUnsafeList GetPool<T>() {
                var poolIndex = ComponentMeta<T>.Index;
                ref var pool = ref pools.GetRef<UntypedUnsafeList>(poolIndex);
                if (pool.IsCreated == false) {
                    pool = new UntypedUnsafeList(typeof(T), config.StartPoolSize, allocator);
                    pools.Set(poolIndex, ref pool);
                }

                return ref pool;
            }

            internal Entity CreateEntity() {
                var e = new Entity(lastEntityIndex, self);
                entities.ElementAt(lastEntityIndex) = e;
                lastEntityIndex++;
                return e;
            }

            internal ref Entity GetEntity(int id) {
                return ref entities.ElementAt(id);
            }

            public Archetype CreateArchetype(params int[] types) {
                var ptr = Archetype.ArchetypeImpl.Create(self, types);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }

            private Archetype CreateArchetype() {
                var ptr = Archetype.ArchetypeImpl.Create(self);
                Archetype archetype;
                archetype.impl = ptr;
                archetypesList.Add(ptr);
                archetypesMap[ptr->id] = archetype;
                return archetype;
            }

            internal Archetype GetArchetype(int hash) {
                return archetypesMap[hash];
            }
        }

    }
    
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
}