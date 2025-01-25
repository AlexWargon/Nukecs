using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public partial struct World {
        public unsafe struct Entities {
            internal UnsafeList<Entity> entities;
            internal UnsafeList<Entity> prefabsToSpawn;
            internal UnsafeList<int> reservedEntities;
            internal UnsafeList<Archetype> entitiesArchetypes;
            internal UnsafeHashMap<int, Archetype> archetypesMap;
            internal UnsafePtrList<ArchetypeUnsafe> archetypesList;
            internal UnsafePtrList<QueryUnsafe> queries;
            internal int entitiesAmount;
            internal int lastEntityIndex;
            internal int lastDestroyedEntity;
            internal WorldUnsafe* world;

            internal static Entities Create(WorldUnsafe* world) {
                return new Entities {
                    entities = UnsafeHelp.UnsafeListWithMaximumLenght<Entity>(world->config.StartEntitiesAmount, world->allocator, NativeArrayOptions.ClearMemory),
                    prefabsToSpawn = new UnsafeList<Entity>(64, world->allocator, NativeArrayOptions.ClearMemory),
                    reservedEntities = new UnsafeList<int>(128, world->allocator, NativeArrayOptions.ClearMemory),
                    entitiesArchetypes = UnsafeHelp.UnsafeListWithMaximumLenght<Archetype>(world->config.StartEntitiesAmount, world->allocator, NativeArrayOptions.ClearMemory),
                    queries = new UnsafePtrList<QueryUnsafe>(32, world->allocator),
                    archetypesList = new UnsafePtrList<ArchetypeUnsafe>(32, world->allocator),
                    archetypesMap = new UnsafeHashMap<int, Archetype>(32, world->allocator),
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
                    entities.ElementAtNoCheck(last) = e;
                    return e;
                }
                
                e = new Entity(last, world);
                entities.ElementAtNoCheck(last) = e;
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
    [System.Serializable]
    public struct WorldSerialized
    {
        public Entity[] entities;
        public Entity[] prefabsToSpawn;
        public int[] reservedEntities;
        public int[] entitiesArchetypes;
        public int[] archetypesMap;
        public int entitiesAmount;
        public int lastEntityIndex;
        public byte[][] pools;
        public QuerySerialized[] queries;


        public static unsafe void Deserialize(ref WorldSerialized worldSerialized, ref World world)
        {
            for (var i = 0; i < worldSerialized.queries.Length; i++)
            {
                QuerySerialized.Deseialize(ref worldSerialized.queries[i], world.UnsafeWorld->queries[i]);
            }
            Unsafe.Copy(ref world.UnsafeWorld->entities, ref worldSerialized.entities, worldSerialized.entities.Length);
            Unsafe.Copy(ref world.UnsafeWorld->prefabsToSpawn, ref worldSerialized.prefabsToSpawn, worldSerialized.prefabsToSpawn.Length);
            Unsafe.Copy(ref world.UnsafeWorld->reservedEntities, ref worldSerialized.reservedEntities, worldSerialized.reservedEntities.Length);

            for (int i = 0; i < worldSerialized.pools.Length; i++)
            {
                world.UnsafeWorld->pools[i].Deserialize(worldSerialized.pools[i]);
            }
        }
    }
}