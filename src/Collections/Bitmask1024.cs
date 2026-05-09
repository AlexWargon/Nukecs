using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Bitmask1024
    {
        private const int BITS_PER_WORD = 64;
        private const int WORD_COUNT = 16;

        private ulong summary;
        private fixed ulong words[WORD_COUNT];

        public int Count { get; private set; }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Validate(int bit)
        {
            if ((uint)bit >= 1024)
                throw new ArgumentOutOfRangeException(nameof(bit));
        }
    }
}