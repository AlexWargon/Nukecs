using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public partial struct World
    {
        public unsafe partial struct WorldUnsafe
        {
            internal unsafe void* _allocate_for_pool(int size, int alignment, ComponentType componentType, int items = 1)
            {
                dbug.log($"allocated {size} bytes of {componentType.ManagedType.Name} with items {items}");
                return AllocatorHandler.AllocatorWrapper.Allocate(size, alignment, items);
            }
            
            internal unsafe void* _allocate(int size, int alignment, int items = 1)
            {
                return AllocatorHandler.AllocatorWrapper.Allocate(size, alignment, items);
            }
            internal unsafe T* _allocate<T>(int items = 1) where T: unmanaged
            {
                //return AllocatorHandle.AllocatorWrapper.MemoryAllocator.AllocateD<T>(items);
                return (T*)AllocatorHandler.AllocatorWrapper.Allocate(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), items);
            }
            internal unsafe ptr<T> _allocate_ptr<T>(int items = 1) where T: unmanaged
            {
                //return AllocatorHandle.AllocatorWrapper.MemoryAllocator.AllocateD<T>(items);
                return AllocatorHandler.AllocatorWrapper.Allocator.AllocatePtr<T>(UnsafeUtility.SizeOf<T>() * items);
            }
            internal unsafe void _free<T>(T* ptr, int items = 1) where T : unmanaged
            {
                AllocatorManager.Free(AllocatorHandler.AllocatorWrapper.Handle, ptr, items);
            }
            internal unsafe void _free(uint offset)
            {
                AllocatorRef.Free(offset);
            }
            internal unsafe void _free(void* ptr)
            {
                AllocatorManager.Free(AllocatorHandler.AllocatorWrapper.Handle, ptr);
            }
            internal unsafe void _free(void* ptr, int sizeInBytes, int alignmentInBytes, int items)
            {
                AllocatorManager.Free(AllocatorHandler.AllocatorWrapper.Handle, ptr, sizeInBytes, alignmentInBytes, items);
            }
        }
    }
}