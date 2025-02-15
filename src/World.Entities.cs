using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs {
    public partial struct World {
        public unsafe struct Entities2
        {
            internal MemoryList<Entity> entities;
            internal MemoryList<_Ptr<QueryUnsafe>> queries;
            internal MemoryList<_Ptr<ArchetypeUnsafe>> archetypes;
            internal HashMap<int, _Ptr<ArchetypeUnsafe>> archetypesMap;
            internal _Ptr<WorldUnsafe> world;
            ArchetypeUnsafe* GetArchetype(int hash)
            {
                var ptr = archetypesMap[hash];
                ptr.OnDeserialize(ref world.Ptr->AllocatorRef);
                return archetypesMap[hash].Ptr;
            }
        }
        public unsafe struct Entities {
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<Entity> entities;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<Entity> prefabsToSpawn;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<int> reservedEntities;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<Archetype> entitiesArchetypes;
            internal UnsafeHashMap<int, Archetype> archetypesMap;
            internal UnsafePtrList<ArchetypeUnsafe> archetypesList;
            internal UnsafePtrList<QueryUnsafe> queries;
            internal int entitiesAmount;
            internal int lastEntityIndex;
            internal int lastDestroyedEntity;
            internal WorldUnsafe* world;

            internal static Entities Create(WorldUnsafe* world) {
                return new Entities {
                    entities = UnsafeHelp.UnsafeListWithMaximumLenght<Entity>(world->config.StartEntitiesAmount, world->Allocator, NativeArrayOptions.ClearMemory),
                    prefabsToSpawn = new Unity.Collections.LowLevel.Unsafe.UnsafeList<Entity>(64, world->Allocator, NativeArrayOptions.ClearMemory),
                    reservedEntities = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(128, world->Allocator, NativeArrayOptions.ClearMemory),
                    entitiesArchetypes = UnsafeHelp.UnsafeListWithMaximumLenght<Archetype>(world->config.StartEntitiesAmount, world->Allocator, NativeArrayOptions.ClearMemory),
                    queries = new UnsafePtrList<QueryUnsafe>(32, world->Allocator),
                    archetypesList = new UnsafePtrList<ArchetypeUnsafe>(32, world->Allocator),
                    archetypesMap = new UnsafeHashMap<int, Archetype>(32, world->Allocator),
                    entitiesAmount = 0,
                    lastEntityIndex = 1,
                    lastDestroyedEntity = 0,
                    world = world
                };
            }
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
                    e = new Entity(last, world);
                    entities.ElementAt(last) = e;
                    return e;
                }
                
                e = new Entity(last, world);
                entities.ElementAt(last) = e;
                lastEntityIndex++;
                return e;
            }
            public void Dispose(int id) {
                foreach (var entity in entities) {
                    if (entity != Nukecs.Entity.Null) {
                        entity.Free();
                    }
                }
                WorldSystems.CompleteAll(id);
                for (var index = 0; index < queries.Length; index++) {
                    QueryUnsafe* ptr = queries[index];
                    QueryUnsafe.Free(ptr);
                }
                foreach (var kvPair in archetypesMap) {
                    kvPair.Value.Dispose();
                }
                entities.Dispose();
                prefabsToSpawn.Dispose();
                reservedEntities.Dispose();
                entitiesArchetypes.Dispose();
                archetypesMap.Dispose();
                archetypesList.Dispose();
                queries.Dispose();
            }
        }
    }
}