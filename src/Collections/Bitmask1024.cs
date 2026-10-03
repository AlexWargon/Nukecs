using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Bitmask1024
    {
        private const int BITS_PER_WORD = 64;
        private const int WORD_COUNT = 16;

        private ulong summary;
        private fixed ulong words[WORD_COUNT];

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get; 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] private set;
        }

        public int Size()
        {
            return sizeof(Bitmask1024);
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => summary == 0;
        }

        //no validation for bit
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFast(int bit)
        {
            var wordIndex = bit >> 6;
            var mask = 1UL << (bit & 63);
            return (words[wordIndex] & mask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int bit)
        {
            Validate(bit);
            var wordIndex = bit >> 6;
            var mask = 1UL << (bit & 63);
            return (words[wordIndex] & mask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int bit)
        {
            Validate(bit);

            var wordIndex = bit >> 6;
            var bitMask = 1UL << (bit & 63);
            var summaryMask = 1UL << wordIndex;

            ref var word = ref words[wordIndex];

            var old = word;

            if ((old & bitMask) != 0)
                return false;

            word |= bitMask;

            if (old == 0)
                summary |= summaryMask;

            Count++;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(int bit)
        {
            Validate(bit);

            var wordIndex = bit >> 6;
            var bitMask = 1UL << (bit & 63);
            var summaryMask = 1UL << wordIndex;

            ref var word = ref words[wordIndex];

            var old = word;

            if ((old & bitMask) == 0)
                return false;

            word &= ~bitMask;

            if (word == 0)
                summary &= ~summaryMask;

            Count--;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            summary = 0;
            Count = 0;

            for (var i = 0; i < WORD_COUNT; i++)
                words[i] = 0;
        }

        // O(k), где k = число set bits
        public void IterateSetBits(delegate*<int, void> callback)
        {
            var summaryCopy = summary;

            while (summaryCopy != 0)
            {
                var wordIndex =
                    BitUtils.TrailingZeroCount(summaryCopy);

                var word = words[wordIndex];

                while (word != 0)
                {
                    var bit =
                        BitUtils.TrailingZeroCount(word);

                    callback((wordIndex << 6) + bit);

                    word &= word - 1;
                }

                summaryCopy &= summaryCopy - 1;
            }
        }
        
        public int CountBefore(int bit)
        {
            var wordIndex = bit >> 6;
            var bitIndex = bit & 63;

            int count = 0;

            ulong activeWords =
                summary & ((1UL << wordIndex) - 1);

            while (activeWords != 0)
            {
                var i =
                    BitUtils.TrailingZeroCount(activeWords);

                count +=
                    BitUtils.PopCount(words[i]);

                activeWords &= activeWords - 1;
            }

            ulong partial =
                words[wordIndex] &
                ((1UL << bitIndex) - 1);

            count +=
                BitUtils.PopCount(partial);

            return count;
        }

        public int GetBitAtIndex(int rank)
        {
            int count = 0;
            for (int w = 0; w < WORD_COUNT; w++)
            {
                var word = words[w];
                if (word == 0) continue;
                var pop = BitUtils.PopCount(word);
                if (count + pop > rank)
                {
                    var remaining = rank - count;
                    while (remaining-- > 0)
                        word &= word - 1;
                    return (w << 6) + BitUtils.TrailingZeroCount(word);
                }
                count += pop;
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Validate(int bit)
        {
            if ((uint)bit >= 1024)
                throw new ArgumentOutOfRangeException(nameof(bit));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(ref Bitmask1024 source)
        {
            summary = source.summary;
            fixed (ulong* ptr = words)
            {
                fixed (ulong* src = source.words)
                {
                    Unsafe.CopyBlock(ptr, src, sizeof(ulong) * WORD_COUNT);
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExtractSetBits(ref MemoryList<int> output, ref MemAllocator allocator)
        {
            output.Clear();
            for (var bitPos = 0; bitPos < 1024; bitPos++)
            {
                var idx = bitPos / BITS_PER_WORD;
                var shift = bitPos % BITS_PER_WORD;
                if ((words[idx] & (1UL << shift)) != 0)
                    output.Add(bitPos, ref allocator);
            }
        }

        /// <summary>Fills a span with set bit positions (ascending). Returns the count written.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FillTypes(Span<int> output)
        {
            var n = 0;
            for (var w = 0; w < WORD_COUNT; w++)
            {
                var word = words[w];
                while (word != 0)
                {
                    var b = BitUtils.TrailingZeroCount(word);
                    output[n++] = w * BITS_PER_WORD + b;
                    word &= word - 1;
                }
            }
            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ComputeHash(int* types, int count)
        {
            var maxBits = 1024;
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
    }
}