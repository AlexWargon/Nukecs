using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DynamicBitmask
    {
        private const int BITS_PER_ULONG = 64;
        private ptr<ulong> bitmaskArray;
        private int maxBits;
        private int arraySize;
        public bool IsCreated => bitmaskArray.cached != null;
        public long GetMemorySizeUsed()
        {
            return sizeof(DynamicBitmask) + sizeof(ulong) * arraySize;
        }
        internal void OnDeserialize(ref MemAllocator allocator)
        {
            bitmaskArray.OnDeserialize(ref allocator);
        }

        internal static DynamicBitmask CreateForComponents(World.WorldUnsafe* world)
        {
            return new DynamicBitmask(math.max(ComponentAmount.Value.Data, 256), world);
        }
        
        public DynamicBitmask(int maxBits, World.WorldUnsafe* world)
        {
            if (maxBits <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBits),
                    $"maxBits in {nameof(DynamicBitmask)} must be greater than zero.");

            this.maxBits = maxBits;
            arraySize = (maxBits + BITS_PER_ULONG - 1) / BITS_PER_ULONG; // Calculate the number of ulong elements needed
            bitmaskArray = world->_allocate_ptr<ulong>(arraySize);
            Count = 0;

            // Clear the allocated memory
            ClearBitmask();
        }

        public void Clear()
        {
            mem_clear(bitmaskArray.Ptr, sizeof(ulong) * arraySize);
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

            var index = position / BITS_PER_ULONG;
            var bitPosition = position % BITS_PER_ULONG;

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

            var index = position / BITS_PER_ULONG;
            var bitPosition = position % BITS_PER_ULONG;

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
            var index = position / BITS_PER_ULONG;
            var bitPosition = position % BITS_PER_ULONG;

            if (Has(position))
            {
                bitmaskArray.Ptr[index] &= ~(1UL << bitPosition);
                Count--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(ref DynamicBitmask source)
        {
            var bytes = source.arraySize * sizeof(ulong);
            UnsafeUtility.MemCpy(bitmaskArray.Ptr, source.bitmaskArray.Ptr, bytes);
            Count = source.Count;
        }

        /// <summary>
        /// Fills this bitmask with the union of the three category masks (inline | tag | pool)
        /// and recounts set bits. All masks must share the same array size.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyUnion(ref DynamicBitmask inline, ref DynamicBitmask tag, ref DynamicBitmask pool)
        {
            var newCount = 0;
            for (var i = 0; i < arraySize; i++)
            {
                var word = inline.bitmaskArray.Ptr[i] | tag.bitmaskArray.Ptr[i] | pool.bitmaskArray.Ptr[i];
                bitmaskArray.Ptr[i] = word;
                newCount += math.countbits(word);
            }
            Count = newCount;
        }

        /// <summary>True when every bit set in <paramref name="other"/> is also set in this mask.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAll(ref DynamicBitmask other)
        {
            var words = bitmaskArray.Ptr;
            var otherWords = other.bitmaskArray.Ptr;
            var n = arraySize < other.arraySize ? arraySize : other.arraySize;
            for (var i = 0; i < n; i++)
                if ((words[i] & otherWords[i]) != otherWords[i]) return false;
            // bits in words beyond this mask can never be present here
            for (var i = n; i < other.arraySize; i++)
                if (otherWords[i] != 0) return false;
            return true;
        }

        /// <summary>True when none of the bits set in <paramref name="other"/> are set in this mask.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsNone(ref DynamicBitmask other)
        {
            var words = bitmaskArray.Ptr;
            var otherWords = other.bitmaskArray.Ptr;
            var n = arraySize < other.arraySize ? arraySize : other.arraySize;
            for (var i = 0; i < n; i++)
                if ((words[i] & otherWords[i]) != 0) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SequenceEqual(ref DynamicBitmask other)
        {
            if (arraySize != other.arraySize) return false;
            var words = bitmaskArray.Ptr;
            var otherWords = other.bitmaskArray.Ptr;
            for (var i = 0; i < arraySize; i++)
                if (words[i] != otherWords[i]) return false;
            return true;
        }

        /// <summary>
        /// FNV-1a hash over the byte representation of (this | tag | pool), byte-identical
        /// to <see cref="ComputeHash"/> of a union bitmask. this = inline mask.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ComputeUnionHash(ref DynamicBitmask tag, ref DynamicBitmask pool)
        {
            unchecked
            {
                var hash = (int)2166136261;
                const int p = 16777619;
                var inlineWords = bitmaskArray.Ptr;
                var tagWords = tag.bitmaskArray.Ptr;
                var poolWords = pool.bitmaskArray.Ptr;
                for (var i = 0; i < arraySize; i++)
                {
                    var word = inlineWords[i] | tagWords[i] | poolWords[i];
                    var wptr = (byte*)&word;
                    for (var b = 0; b < 8; b++)
                        hash = (hash ^ wptr[b]) * p;
                }
                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        /// <summary>True when this bitmask equals (inline | tag | pool) word-by-word.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EqualsUnion(ref DynamicBitmask inline, ref DynamicBitmask tag, ref DynamicBitmask pool)
        {
            if (arraySize != inline.arraySize) return false;
            var words = bitmaskArray.Ptr;
            var inlineWords = inline.bitmaskArray.Ptr;
            var tagWords = tag.bitmaskArray.Ptr;
            var poolWords = pool.bitmaskArray.Ptr;
            for (var i = 0; i < arraySize; i++)
                if (words[i] != (inlineWords[i] | tagWords[i] | poolWords[i])) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExtractSetBits(ref MemoryList<int> output, ref MemAllocator allocator)
        {
            output.Clear();
            for (var bitPos = 0; bitPos < maxBits; bitPos++)
            {
                var idx = bitPos / BITS_PER_ULONG;
                var shift = bitPos % BITS_PER_ULONG;
                if ((bitmaskArray.Ptr[idx] & (1UL << shift)) != 0)
                    output.Add(bitPos, ref allocator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ComputeHash()
        {
            unchecked
            {
                var hash = (int)2166136261;
                const int p = 16777619;
                var byteLen = arraySize * sizeof(ulong);
                var ptr = (byte*)bitmaskArray.Ptr;
                for (var i = 0; i < byteLen; i++)
                    hash = (hash ^ ptr[i]) * p;
                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ComputeHash(int* types, int count)
        {
            var maxBits = math.max(ComponentAmount.Value.Data, 256);
            var sz = (maxBits + 63) / 64;
            var bits = stackalloc ulong[sz];
            UnsafeUtility.MemClear(bits, sz * sizeof(ulong));
            for (var i = 0; i < count; i++)
            {
                var t = types[i];
                bits[t / 64] |= 1UL << (t % 64);
            }
            unchecked
            {
                var hash = (int)2166136261;
                const int p = 16777619;
                var byteLen = sz * sizeof(ulong);
                var ptr = (byte*)bits;
                for (var i = 0; i < byteLen; i++)
                    hash = (hash ^ ptr[i]) * p;
                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        // Override ToString() to display the bitmask in binary form
        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var i = arraySize - 1; i >= 0; i--)
                sb.Append(Convert.ToString((long)bitmaskArray.Ptr[i], 2).PadLeft(BITS_PER_ULONG, '0'));

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

        public int Size()
        {
            return sizeof(ulong) * arraySize + sizeof(DynamicBitmask);
        }
    }
    unsafe struct DeBruijnTable
    {
        public fixed byte Values[64];
    }
    public static class BitUtils
    {
        private const ulong DE_BRUIJN =
            0x03F79D71B4CB0A89UL;

        private static readonly int[] Index =
        {
            0,  1, 48,  2, 57, 49, 28,  3,
            61, 58, 50, 42, 38, 29, 17,  4,
            62, 55, 59, 36, 53, 51, 43, 22,
            45, 39, 33, 30, 24, 18, 12,  5,
            63, 47, 56, 27, 60, 41, 37, 16,
            54, 35, 52, 21, 44, 32, 23, 11,
            46, 26, 40, 15, 34, 20, 31, 10,
            25, 14, 19,  9, 13,  8,  7,  6
        };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(ulong x)
            => math.tzcnt(x);
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public static int TrailingZeroCount(ulong value)
        // {
        //     if (value == 0)
        //         return 64;
        //
        //     ulong isolated = value & (ulong)-(long)value;
        //
        //     return Index[
        //         (isolated * DE_BRUIJN) >> 58
        //     ];
        // }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(ulong value)
        {
            return math.countbits(value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount2(ulong x)
        {
            x -= (x >> 1) & 0x5555555555555555UL;
            x = (x & 0x3333333333333333UL)
                + ((x >> 2) & 0x3333333333333333UL);

            x = (x + (x >> 4))
                & 0x0F0F0F0F0F0F0F0FUL;

            x += x >> 8;
            x += x >> 16;
            x += x >> 32;

            return (int)(x & 0x7F);
        }
    }
}