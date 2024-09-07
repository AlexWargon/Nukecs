using System.Threading;

namespace Wargon.Nukecs.Collision2D
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BufferInt256 {
        private fixed int buffer[256];
        private volatile int count;
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => Interlocked.Exchange(ref count, value);
        }

        public int this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => buffer[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int value) {
            if (count == 255) return;
            var idx = count;
            buffer[idx] = value;
            Interlocked.Increment(ref count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            Interlocked.Exchange(ref count, 0);
        }
    }
}  