using System.Threading;

namespace Wargon.Nukecs.Collision2D
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BufferInt256
    {
        private fixed int buffer[256];
        private int count; // Убираем volatile, так как используем Interlocked

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Interlocked.CompareExchange(ref count, 0, 0); // Атомарное чтение
        }

        public int this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => buffer[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int value)
        {
            int idx = Interlocked.Increment(ref count) - 1;
            if (idx >= 256)
            {
                Interlocked.Decrement(ref count);
                return false;
            }
            buffer[idx] = value;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Interlocked.Exchange(ref count, 0);
        }
    }
}  