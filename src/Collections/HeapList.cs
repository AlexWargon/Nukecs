using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Collections
{
    /// <summary>
    /// Growable list on the heap with handle semantics (like NativeList): the struct holds a
    /// pointer to a separately allocated header, so copies share length and storage.
    /// Not thread-safe; give each writer its own list.
    /// </summary>
    public unsafe struct HeapList<T> : IDisposable where T : unmanaged
    {
        private struct Header
        {
            public T* Ptr;
            public int Length;
            public int Capacity;
            public AllocatorHandle Allocator;
        }

        [NativeDisableUnsafePtrRestriction]
        private Header* header;

        public HeapList(int capacity, AllocatorHandle allocator)
        {
            header = (Header*)Mem.Malloc(sizeof(Header), Mem.AlignOf<Header>(), allocator);
            header->Ptr = null;
            header->Length = 0;
            header->Capacity = 0;
            header->Allocator = allocator;
            SetCapacity(capacity);
        }

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => header != null;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => header->Length;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => header == null || header->Length == 0;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => header->Capacity;
            set => SetCapacity(value);
        }

        public T* Ptr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => header->Ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetUnsafePtr() => header->Ptr;

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref header->Ptr[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index) => ref header->Ptr[index];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T value)
        {
            var idx = header->Length;
            if (idx == header->Capacity) Grow(idx + 1);
            header->Ptr[idx] = value;
            header->Length = idx + 1;
        }

        public void AddRange(void* source, int count)
        {
            var idx = header->Length;
            var newLength = idx + count;
            if (newLength > header->Capacity) Grow(newLength);
            Mem.MemCpy(header->Ptr + idx, source, (long)count * sizeof(T));
            header->Length = newLength;
        }

        /// <summary>Sets the length; new elements are not cleared.</summary>
        public void ResizeUninitialized(int length)
        {
            if (length > header->Capacity) Grow(length);
            header->Length = length;
        }

        public void RemoveAtSwapBack(int index)
        {
            var last = header->Length - 1;
            if (index != last) header->Ptr[index] = header->Ptr[last];
            header->Length = last;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => header->Length = 0;

        public void Dispose()
        {
            if (header == null) return;
            var allocator = header->Allocator;
            if (header->Ptr != null) Mem.Free(header->Ptr, allocator);
            Mem.Free(header, allocator);
            header = null;
        }

        private void Grow(int minCapacity)
        {
            var capacity = header->Capacity > 0 ? header->Capacity : 4;
            while (capacity < minCapacity) capacity *= 2;
            SetCapacity(capacity);
        }

        private void SetCapacity(int capacity)
        {
            if (capacity < header->Length) capacity = header->Length;
            if (capacity == header->Capacity) return;
            T* newPtr = null;
            if (capacity > 0)
            {
                newPtr = (T*)Mem.Malloc((long)capacity * sizeof(T), Mem.AlignOf<T>(), header->Allocator);
                if (header->Length > 0) Mem.MemCpy(newPtr, header->Ptr, (long)header->Length * sizeof(T));
            }
            if (header->Ptr != null) Mem.Free(header->Ptr, header->Allocator);
            header->Ptr = newPtr;
            header->Capacity = capacity;
        }
    }
}
