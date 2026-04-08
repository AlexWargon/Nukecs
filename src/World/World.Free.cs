using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs
{
public unsafe partial struct World
    {
        public unsafe partial struct WorldUnsafe
        {
            public void Free()
            {
                ECB.Dispose();
                FixedUpdateECB.Dispose();
                WorldSystems.CompleteAll(Id);
                
                foreach (var entity in entities) {
                    if (entity != Nukecs.Entity.Null) {
                        entity.Free();
                    }
                }

                entities.Dispose();
                entitiesArchetypes.Dispose();
                entityGenerations.Dispose();

                for (var index = 0; index < queries.length; index++) {
                    QueryUnsafe.Free(queries.Ptr[index].Ptr);
                }
                queries.Dispose();

                for (var index = 0; index < archetypesList.length; index++) {
                    ArchetypeUnsafe.Destroy(archetypesList.Ptr[index].Ptr);
                }
                archetypesList.Dispose();
                archetypesMap.Dispose();

                pools.Dispose();
                DefaultNoneTypes.Dispose();
                reservedEntities.Dispose();
                prefabsToSpawn.Dispose();
                aspects.Dispose();
            }
            
        }
        public unsafe void Dispose() {
            //if (UnsafeWorld == null) return;
            var id = UnsafeWorld->Id;
            lastFreeSlot = id;
            var allctr = UnsafeWorld->AllocatorHandler;
            UnsafeWorld->Free();
            //AllocatorManager.Free(allocator.AllocatorHandle, UnsafeWorld);
            allctr.Dispose();
            //UnsafeUtility.FreeTracked(UnsafeWorld, Unity.Collections.Allocator.Persistent);
            
            Debug.Log($"World {id} Disposed. World slot {lastFreeSlot} free");
        }
    }
}