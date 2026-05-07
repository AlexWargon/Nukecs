using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    public unsafe struct Bitmask1024
    {
        private const int BitsPerWord = 64;
        private const int WordCount = 16;

        private ulong summary;
        private fixed ulong words[WordCount];

        public int Count { get; private set; }
        public int Size() => sizeof(Bitmask1024);
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => summary == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int bit)
        {
            Validate(bit);

            int wordIndex = bit >> 6;
            ulong mask = 1UL << (bit & 63);

            return (words[wordIndex] & mask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int bit)
        {
            Validate(bit);

            int wordIndex = bit >> 6;
            ulong bitMask = 1UL << (bit & 63);
            ulong summaryMask = 1UL << wordIndex;

            ref ulong word = ref words[wordIndex];

            ulong old = word;

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

            int wordIndex = bit >> 6;
            ulong bitMask = 1UL << (bit & 63);
            ulong summaryMask = 1UL << wordIndex;

            ref ulong word = ref words[wordIndex];

            ulong old = word;

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

            for (int i = 0; i < WordCount; i++)
                words[i] = 0;
        }

        // O(k), где k = число set bits
        public void IterateSetBits(delegate*<int, void> callback)
        {
            ulong summaryCopy = summary;

            while (summaryCopy != 0)
            {
                int wordIndex =
                    BitUtils.TrailingZeroCount(summaryCopy);

                ulong word = words[wordIndex];

                while (word != 0)
                {
                    int bit =
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
            if ((uint)bit >= 4096)
                throw new ArgumentOutOfRangeException(nameof(bit));
        }
    }
    public unsafe struct Bitmask4096
    {
        private const int BitsPerWord = 64;
        private const int WordCount = 64;

        private ulong summary;
        private fixed ulong words[WordCount];

        public int Count { get; private set; }
        public int Size() => sizeof(Bitmask4096);
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => summary == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int bit)
        {
            Validate(bit);

            int wordIndex = bit >> 6;
            ulong mask = 1UL << (bit & 63);

            return (words[wordIndex] & mask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int bit)
        {
            Validate(bit);

            int wordIndex = bit >> 6;
            ulong bitMask = 1UL << (bit & 63);
            ulong summaryMask = 1UL << wordIndex;

            ref ulong word = ref words[wordIndex];

            ulong old = word;

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

            int wordIndex = bit >> 6;
            ulong bitMask = 1UL << (bit & 63);
            ulong summaryMask = 1UL << wordIndex;

            ref ulong word = ref words[wordIndex];

            ulong old = word;

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

            for (int i = 0; i < WordCount; i++)
                words[i] = 0;
        }

        // O(k), где k = число set bits
        public void IterateSetBits(delegate*<int, void> callback)
        {
            ulong summaryCopy = summary;

            while (summaryCopy != 0)
            {
                int wordIndex =
                    BitUtils.TrailingZeroCount(summaryCopy);

                ulong word = words[wordIndex];

                while (word != 0)
                {
                    int bit =
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
            if ((uint)bit >= 4096)
                throw new ArgumentOutOfRangeException(nameof(bit));
        }
    }
}