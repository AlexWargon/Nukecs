using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public partial struct World
    {
        public unsafe partial struct WorldUnsafe
        {
            internal unsafe void* _allocate_for_pool(int size, int alignment, ComponentTypeData componentTypeData, int items = 1)
            {
                dbug.log($"allocated {size} bytes of {componentTypeData.ManagedType.Name} with items {items}");
                return AllocatorHandler.AllocatorWrapper.Allocate(size, alignment, items);
            }
            
            internal unsafe void* _allocate(int size, int alignment, int items = 1)
            {
                return AllocatorHandler.AllocatorWrapper.Allocate(size, alignment, items);
            }
            public T* _allocate<T>(int items = 1) where T: unmanaged
            {
                //return AllocatorHandle.AllocatorWrapper.MemoryAllocator.AllocateD<T>(items);
                return (T*)AllocatorRef.Allocate(sizeof(T) * items);
            }
            public ptr<T> _allocate_ptr<T>(int items = 1) where T: unmanaged
            {
                return AllocatorRef.AllocatePtr<T>(sizeof(T) * items);
            }
            public void _free<T>(T* ptr) where T : unmanaged
            {
                AllocatorRef.Free(ptr);
                //AllocatorManager.Free(AllocatorHandler.AllocatorWrapper.Handle, ptr, items);
            }
            public void _free(uint offset)
            {
                AllocatorRef.Free(offset);
            }
            public unsafe void _free(void* ptr)
            {
                AllocatorManager.Free(AllocatorHandler.AllocatorWrapper.Handle, ptr);
            }
            public unsafe void _free(void* ptr, int sizeInBytes, int alignmentInBytes, int items)
            {
                AllocatorManager.Free(AllocatorHandler.AllocatorWrapper.Handle, ptr, sizeInBytes, alignmentInBytes, items);
            }
        }
    }
}