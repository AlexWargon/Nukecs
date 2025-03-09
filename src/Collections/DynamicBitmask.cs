using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DynamicBitmask
    {
        private const int BitsPerUlong = 64;
        private ptr<ulong> bitmaskArray;
        private int maxBits;
        private int arraySize;

        internal void OnDeserialize(ref SerializableMemoryAllocator allocator)
        {
            bitmaskArray.OnDeserialize(ref allocator);
        }

        internal static DynamicBitmask CreateForComponents(World.WorldUnsafe* world)
        {
            return new DynamicBitmask(ComponentAmount.Value.Data, world);
        }

        internal DynamicBitmask(int maxBits, World.WorldUnsafe* world)
        {
            if (maxBits <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBits),
                    $"maxBits in {nameof(DynamicBitmask)} must be greater than zero.");

            this.maxBits = maxBits;
            arraySize = (maxBits + BitsPerUlong - 1) / BitsPerUlong; // Calculate the number of ulong elements needed
            bitmaskArray = world->_allocate_ptr<ulong>(arraySize);
            Count = 0;

            // Clear the allocated memory
            ClearBitmask();
        }

        private void ClearBitmask()
        {
            for (var i = 0; i < arraySize; i++) bitmaskArray.Ptr[i] = 0;
        }

        // Property to get the count of set bits
        public int Count { get; private set; }

        // Method to add an element (set a specific bit)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int position)
        {
            if (position < 0 || position >= maxBits)
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"{nameof(DynamicBitmask)}: Position must be between 0 and {maxBits - 1}.");

            var index = position / BitsPerUlong;
            var bitPosition = position % BitsPerUlong;

            if (!Has(position))
            {
                bitmaskArray.Ptr[index] |= 1UL << bitPosition;
                Count++;
            }
        }

        // Method to check if an element is present (a specific bit is set)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int position)
        {
            if (position < 0 || position >= maxBits)
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"{nameof(DynamicBitmask)}: {nameof(position)} must be between 0 and {maxBits - 1}. Position = {position}");

            var index = position / BitsPerUlong;
            var bitPosition = position % BitsPerUlong;

            return (bitmaskArray.Ptr[index] & (1UL << bitPosition)) != 0;
        }

        public bool HasRange(int* buffer, int range)
        {
            var matches = 0;
            for (var i = 0; i < range; i++)
            {
                if (Has(buffer[i])) matches++;
                {
                    if (matches == range) return true;
                }
            }

            return false;
        }

        // Method to clear an element (unset a specific bit)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int position)
        {
            if (position < 0 || position >= maxBits)
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"{nameof(DynamicBitmask)}: {nameof(position)} must be between 0 and {maxBits - 1}. ");
            var index = position / BitsPerUlong;
            var bitPosition = position % BitsPerUlong;

            if (Has(position))
            {
                bitmaskArray.Ptr[index] &= ~(1UL << bitPosition);
                Count--;
            }
        }

        // Override ToString() to display the bitmask in binary form
        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var i = arraySize - 1; i >= 0; i--)
                sb.Append(Convert.ToString((long)bitmaskArray.Ptr[i], 2).PadLeft(BitsPerUlong, '0'));

            return sb.ToString();
        }

        // Copy method to create a deep copy of the DynamicBitmask
        internal DynamicBitmask Copy(World.WorldUnsafe* world)
        {
            var copy = new DynamicBitmask(maxBits, world);
            var byteLength = arraySize * sizeof(ulong);
            UnsafeUtility.MemCpy(copy.bitmaskArray.Ptr, bitmaskArray.Ptr, byteLength);
            copy.Count = Count;
            return copy;
        }

        internal DynamicBitmask CopyPlusOne(World.WorldUnsafe* world)
        {
            var copy = new DynamicBitmask(maxBits + 1, world);
            var byteLength = arraySize * sizeof(ulong);
            UnsafeUtility.MemCpy(copy.bitmaskArray.Ptr, bitmaskArray.Ptr, byteLength);
            copy.Count = Count;
            return copy;
        }

        // Dispose method to release allocated memory
        public void Dispose()
        {
            // UnsafeUtility.FreeTracked(bitmaskArray.Ptr, Allocator.Persistent);
            // bitmaskArray = null;
        }

        public ulong[] AsArray()
        {
            return new Span<ulong>(bitmaskArray.Ptr, arraySize).ToArray();
        }

        public void FromArray(ulong[] array, int size)
        {
            fixed (ulong* ptr = array)
            {
                UnsafeUtility.MemCpy(bitmaskArray.Ptr, ptr, size);
                arraySize = size;
            }
        }
    }
}