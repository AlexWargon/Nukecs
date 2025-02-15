using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs
{
public unsafe partial struct World
    {
        internal unsafe partial struct WorldUnsafe
        {
            public void Free()
            {
                var info = AllocatorHandler.AllocatorWrapper.Allocator.GetMemoryInfo();
                var sb = new System.Text.StringBuilder();
                sb.Append($"Allocator on free: Total memory: {info.totalSize} . Free: {info.freeSize} bytes. Used {info.usedSize} bytes. Defragmentation Cycles: {info.defragmentationCycles}. Blocks: {info.blockCount}");
                sb.AppendLine();
                var memoryView = AllocatorHandler.AllocatorWrapper.Allocator.GetMemoryView();
                long unUsedMemory = 0;
                long usedMemory = 0;
                for (int i = 0; i < memoryView.BlockCount; i++)
                {
                    ref var block = ref memoryView.Blocks[i];
                    if (block.IsUsed)
                    {
                        usedMemory += block.Size;
                    }
                    else
                    {
                        unUsedMemory += block.Size;
                    }

                    var used = block.IsUsed ? "[Used]" : "UnUsed";
                    if (block.Size > 10_000_000)
                    {
                        sb.Append($"Giant Block #{i}: {used}. Total size: {block.Size} bytes");
                    }
                    else
                    if (block.Size > 10_000)
                    {
                        sb.Append($"Big Block #{i}: {used}. Total size: {block.Size} bytes");
                    }
                    else
                    {
                        sb.Append($"Block #{i}: {used}. Total size: {block.Size} bytes");
                    }
                    sb.AppendLine();
                }
                sb.Append($"Used Memory: {usedMemory} bytes");
                sb.AppendLine();
                sb.Append($"Unused Memory: {unUsedMemory} bytes");
                sb.AppendLine();
                Debug.Log(sb.ToString());
                
                
                foreach (var entity in entities) {
                    if (entity != Nukecs.Entity.Null) {
                        entity.Free();
                    }
                }
                WorldSystems.CompleteAll(Id);
                poolsCount = 0;
                ECBUpdate.Dispose();
            }
            
        }
        public unsafe void Dispose() {
            //if (UnsafeWorld == null) return;
            var id = UnsafeWorld->Id;
            lastFreeSlot = id;
            ref var allocator = ref UnsafeWorld->AllocatorHandler;
            UnsafeWorld->Free();
            allocator.AllocatorWrapper.Allocator.Free(UnsafeWorldPtr);
            allocator.Dispose();
            
            //UnsafeUtility.FreeTracked(UnsafeWorld, Unity.Collections.Allocator.Persistent);
            
            Debug.Log($"World {id} Disposed. World slot {lastFreeSlot} free");
        }
    }
}