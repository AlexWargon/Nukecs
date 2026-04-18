using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MemoryArray<T> where T : unmanaged
    {
        public ptr_offset PtrOffset;
        internal int capacity;
        [NativeDisableUnsafePtrRestriction]
        public T* Ptr;

        public int Capacity => capacity;

        public MemoryArray(int capacity, ref MemAllocator allocator, bool clear = false)
        {
            PtrOffset = allocator.AllocateRaw(sizeof(T) * capacity);
            Ptr = PtrOffset.AsPtr<T>(ref allocator);
            this.capacity = capacity;
            if (clear) UnsafeUtility.MemClear(Ptr, sizeof(T) * capacity);
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Ptr[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index) => ref Ptr[index];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* ElementAtPtr(int index) => Ptr + index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int required, ref MemAllocator allocator)
        {
            if (required <= capacity) return;
            var newCapacity = capacity * 2;
            if (newCapacity < required) newCapacity = required;
            var newOffset = allocator.AllocateRaw(sizeof(T) * newCapacity);
            var newPtr = newOffset.AsPtr<T>(ref allocator);
            if (Ptr != null && capacity > 0)
                UnsafeUtility.MemCpy(newPtr, Ptr, sizeof(T) * capacity);
            UnsafeUtility.MemClear(newPtr + capacity, sizeof(T) * (newCapacity - capacity));
            Ptr = newPtr;
            PtrOffset = newOffset;
            capacity = newCapacity;
        }

        public void OnDeserialize(ref MemAllocator allocator)
        {
            Ptr = PtrOffset.AsPtr<T>(ref allocator);
        }

        public void Dispose()
        {
            Ptr = null;
            PtrOffset = ptr_offset.NULL;
            capacity = 0;
        }
    }
}
