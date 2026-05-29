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
                WorldSystems.CompleteAll(Id);
                WorldSystems.Remove(Id);
                ECB.Dispose();
                selfPtr = default;
            }
        }
        public void Dispose() {
            //if (UnsafeWorld == null) return;
            var id = UnsafeWorld->Id;
            lastFreeSlot = id;
            var allocator = UnsafeWorld->AllocatorHandler;
            UnsafeWorld->Free();
            allocator.Dispose();
            unsafeWorldPtr = ptr<WorldUnsafe>.NULL;
            worldCount--;
            Get(id) = this;
        }
    }
}