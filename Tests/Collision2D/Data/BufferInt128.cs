namespace Wargon.Nukecs.Collision2D
{
    using System.Runtime.CompilerServices;

    public unsafe struct BufferInt128 {
        private fixed int buffer[128];
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => buffer[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int value) {
            if (Count == 127) return;
            buffer[Count++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            Count = 0;
        }
    }
}  