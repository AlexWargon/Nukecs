using System.Runtime.CompilerServices;
#if UNITY_5_3_OR_NEWER
using Unity.Mathematics;
#endif

namespace Wargon.Nukecs
{
    /// <summary>
    /// Scalar and bit helpers used by the core. In Unity each forwards to Unity.Mathematics
    /// (Burst intrinsics); elsewhere it is implemented here.
    /// </summary>
    public static class NMath
    {
#if UNITY_5_3_OR_NEWER
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Max(int a, int b) => math.max(a, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Min(int a, int b) => math.min(a, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int CeilPow2(int x) => math.ceilpow2(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Lzcnt(int x) => math.lzcnt(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Tzcnt(ulong x) => math.tzcnt(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int CountBits(ulong x) => math.countbits(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Lerp(float a, float b, float t) => math.lerp(a, b, t);
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Max(int a, int b) => a > b ? a : b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Min(int a, int b) => a < b ? a : b;

        /// <summary>Smallest power of two >= x (same bit trick as math.ceilpow2).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CeilPow2(int x)
        {
            x -= 1;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        /// <summary>Leading zero bits of x as a 32-bit value (32 for 0).</summary>
        public static int Lzcnt(int x)
        {
            var v = (uint)x;
            if (v == 0) return 32;
            var n = 0;
            if ((v & 0xFFFF0000u) == 0) { n += 16; v <<= 16; }
            if ((v & 0xFF000000u) == 0) { n += 8; v <<= 8; }
            if ((v & 0xF0000000u) == 0) { n += 4; v <<= 4; }
            if ((v & 0xC0000000u) == 0) { n += 2; v <<= 2; }
            if ((v & 0x80000000u) == 0) { n += 1; }
            return n;
        }

        /// <summary>Trailing zero bits of x (64 for 0).</summary>
        public static int Tzcnt(ulong x)
        {
            if (x == 0) return 64;
            var n = 0;
            if ((x & 0xFFFFFFFFul) == 0) { n += 32; x >>= 32; }
            if ((x & 0xFFFFul) == 0) { n += 16; x >>= 16; }
            if ((x & 0xFFul) == 0) { n += 8; x >>= 8; }
            if ((x & 0xFul) == 0) { n += 4; x >>= 4; }
            if ((x & 0x3ul) == 0) { n += 2; x >>= 2; }
            if ((x & 0x1ul) == 0) { n += 1; }
            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountBits(ulong x)
        {
            x -= (x >> 1) & 0x5555555555555555ul;
            x = (x & 0x3333333333333333ul) + ((x >> 2) & 0x3333333333333333ul);
            x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0Ful;
            return (int)((x * 0x0101010101010101ul) >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Lerp(float a, float b, float t) => a + t * (b - a);
#endif
    }
}
