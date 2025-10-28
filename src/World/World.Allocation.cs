using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public partial struct World
    {
        public unsafe partial struct WorldUnsafe
        {
            public T* _allocate<T>(int items = 1) where T: unmanaged
            {
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
        }
    }
}