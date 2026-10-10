using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public unsafe partial struct World
    {
        public partial struct WorldUnsafe
        {
            public void Free()
            {
                WorldIoRequests.Cancel(Id);
                WorldSystems.CompleteAll(Id);
                WorldSystems.Remove(Id);
                // Arena Guard: one cold walk — corruption planted during the session is
                // reported HERE (clear error) instead of crashing the editor later when
                // the damaged heap block is touched by unrelated code (e.g. TextCore).
                AllocatorRef.ValidateAndReport($"world {Id} dispose");
                ECB.Dispose();
                selfPtr = default;
            }
        }
        public void Dispose() {
            //if (UnsafeWorld == null) return;
            var id = UnsafeWorld->Id;
            NukecsLifecycle.RaiseWorldDisposing(id);
            lastFreeSlot = id;
            var allocatorBox = UnsafeWorld->allocatorBox;
            var managedWorld = UnsafeWorld->ManagedWorld;
            UnsafeWorld->Free();
            domainAllocator.Data.Free(managedWorld.UntypedPointer);
            WorldUnsafe.DestroyAllocatorBox(allocatorBox);
            unsafeWorldPtr = ptr<WorldUnsafe>.NULL;
            worldCount--;
            Get(id) = this;
        }
    }
}
