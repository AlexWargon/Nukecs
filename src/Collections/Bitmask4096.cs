using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    public unsafe struct Bitmask4096
    {
        private const int BITS_PER_WORD = 64;
        private const int WORD_COUNT = 64;

        private ulong _summary;
        private fixed ulong _words[WORD_COUNT];

        public int Count { get; private set; }

        public int Size()
        {
            return sizeof(Bitmask4096);
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _summary == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int bit)
        {
            Validate(bit);

            var wordIndex = bit >> 6;
            var mask = 1UL << (bit & 63);

            return (_words[wordIndex] & mask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int bit)
        {
            Validate(bit);

            var wordIndex = bit >> 6;
            var bitMask = 1UL << (bit & 63);
            var summaryMask = 1UL << wordIndex;

            ref var word = ref _words[wordIndex];

            var old = word;

            if ((old & bitMask) != 0)
                return false;

            word |= bitMask;

            if (old == 0)
                _summary |= summaryMask;

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

            ref var word = ref _words[wordIndex];

            var old = word;

            if ((old & bitMask) == 0)
                return false;

            word &= ~bitMask;

            if (word == 0)
                _summary &= ~summaryMask;

            Count--;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _summary = 0;
            Count = 0;

            for (var i = 0; i < WORD_COUNT; i++)
                _words[i] = 0;
        }

        // O(k), где k = число set bits
        public void IterateSetBits(delegate*<int, void> callback)
        {
            var summaryCopy = _summary;

            while (summaryCopy != 0)
            {
                var wordIndex =
                    BitUtils.TrailingZeroCount(summaryCopy);

                var word = _words[wordIndex];

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
            if ((uint)bit >= 4096)
                throw new ArgumentOutOfRangeException(nameof(bit));
        }
    }
}