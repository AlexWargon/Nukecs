using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public unsafe struct DynamicArray : IDisposable
    {
        private byte* buffer;
        private int maxElementSize;
        private int size;
        private int len;
        private Allocator allocator;
        internal static int maximumElementSize;
        public DynamicArray(int size, AllocatorHandle allocator)
        {
            this.buffer = (byte*)Mem.MallocTracked(size * maximumElementSize, Mem.AlignOf<byte>(), allocator, 0);
            this.maxElementSize = maximumElementSize;
            this.size = size;
            this.len = 0;
            this.allocator = allocator;
        }
        public DynamicArray(int size, int maxElementSize, AllocatorHandle allocator)
        {
            this.buffer = (byte*)Mem.MallocTracked(size * maxElementSize, Mem.AlignOf<byte>(), allocator, 0);
            this.maxElementSize = maxElementSize;
            this.size = size;
            this.len = 0;
            this.allocator = allocator;
        }

        public void Add<T>(T item) where T : unmanaged, IDynamic
        {
            *(T*)(buffer + len * maxElementSize) = item;
            len++;
        }

        public ref T Get<T>(int index) where T : unmanaged, IDynamic
        {
            return ref *(T*)(buffer + index * maxElementSize);
        }

        public void Dispose()
        {
            Mem.FreeTracked(buffer, allocator);
        }

    }
    public interface IDynamic { }
}