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
                // Arena Guard: one cold walk — corruption planted during the session is
                // reported HERE (clear error) instead of crashing the editor later when
                // the damaged heap block is touched by unrelated code (e.g. TextCore).
                AllocatorHandler.AllocatorWrapper.Allocator.ValidateAndReport($"world {Id} dispose");
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