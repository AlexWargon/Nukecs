using System.Runtime.InteropServices;
using System.Threading;

namespace Wargon.Nukecs.Collision2D
{
    using System.Runtime.CompilerServices;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BufferInt128
    {
        private fixed int buffer[128];
        private int count;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Interlocked.CompareExchange(ref count, 0, 0);
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
            if (idx >= 128)
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