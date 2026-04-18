using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs
{
    public unsafe partial struct World
    {
        public partial struct WorldUnsafe
        {
            public void Free()
            {
                ECB.Dispose();
                WorldSystems.CompleteAll(Id);
                selfPtr = default;
            }
        }
        public void Dispose() {
            //if (UnsafeWorld == null) return;
            var id = UnsafeWorld->Id;
            lastFreeSlot = id;
            var allctr = UnsafeWorld->AllocatorHandler;
            UnsafeWorld->Free();
            //AllocatorManager.Free(allocator.AllocatorHandle, UnsafeWorld);
            allctr.Dispose();
            unsafeWorldPtr = default;
            //UnsafeUtility.FreeTracked(UnsafeWorld, Unity.Collections.Allocator.Persistent);
            worldCount--;
            if (worldCount == 0)
            {
                DisposeStatic();
            }
            Debug.Log($"World {id} Disposed. World slot {lastFreeSlot} free");
        }
    }
}