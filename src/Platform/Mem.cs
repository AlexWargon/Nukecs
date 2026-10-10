using System;
using System.Runtime.CompilerServices;
#if UNITY_5_3_OR_NEWER
using Unity.Collections.LowLevel.Unsafe;
#else
using System.Runtime.InteropServices;
#endif

namespace Wargon.Nukecs
{
    /// <summary>
    /// Raw memory primitives used by the core. In Unity every method forwards to
    /// <c>UnsafeUtility</c> (inlined, Burst-compatible); elsewhere it uses
    /// System.Runtime.CompilerServices.Unsafe, Buffer and Marshal.
    /// </summary>
    public static unsafe class Mem
    {
#if UNITY_5_3_OR_NEWER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* Malloc(long size, int alignment, AllocatorHandle allocator) =>
            UnsafeUtility.Malloc(size, alignment, allocator);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* MallocTracked(long size, int alignment, AllocatorHandle allocator, int callstacksToSkip = 0) =>
            UnsafeUtility.MallocTracked(size, alignment, allocator, callstacksToSkip);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(void* memory, AllocatorHandle allocator) => UnsafeUtility.Free(memory, allocator);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FreeTracked(void* memory, AllocatorHandle allocator) => UnsafeUtility.FreeTracked(memory, allocator);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>() where T : struct => UnsafeUtility.SizeOf<T>();

        public static int SizeOf(Type type) => UnsafeUtility.SizeOf(type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignOf<T>() where T : struct => UnsafeUtility.AlignOf<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* AddressOf<T>(ref T output) where T : struct => UnsafeUtility.AddressOf(ref output);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AsRef<T>(void* ptr) where T : struct => ref UnsafeUtility.AsRef<T>(ptr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T As<U, T>(ref U from) => ref UnsafeUtility.As<U, T>(ref from);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadArrayElement<T>(void* source, int index) => UnsafeUtility.ReadArrayElement<T>(source, index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteArrayElement<T>(void* destination, int index, T value) =>
            UnsafeUtility.WriteArrayElement(destination, index, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemCpy(void* destination, void* source, long size) =>
            UnsafeUtility.MemCpy(destination, source, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemMove(void* destination, void* source, long size) =>
            UnsafeUtility.MemMove(destination, source, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemSet(void* destination, byte value, long size) =>
            UnsafeUtility.MemSet(destination, value, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemClear(void* destination, long size) => UnsafeUtility.MemClear(destination, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MemCmp(void* ptr1, void* ptr2, long size) => UnsafeUtility.MemCmp(ptr1, ptr2, size);
#else
        // Every label maps to the process heap. The original block start is stored just
        // before the aligned pointer so Free can release it.
        public static void* Malloc(long size, int alignment, AllocatorHandle allocator)
        {
            if (alignment < sizeof(void*)) alignment = sizeof(void*);
            var raw = (byte*)Marshal.AllocHGlobal((IntPtr)(size + alignment + sizeof(void*)));
            var aligned = (byte*)(((ulong)(raw + sizeof(void*)) + (ulong)alignment - 1) & ~((ulong)alignment - 1));
            ((void**)aligned)[-1] = raw;
            return aligned;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* MallocTracked(long size, int alignment, AllocatorHandle allocator, int callstacksToSkip = 0) =>
            Malloc(size, alignment, allocator);

        public static void Free(void* memory, AllocatorHandle allocator)
        {
            if (memory == null) return;
            Marshal.FreeHGlobal((IntPtr)((void**)memory)[-1]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FreeTracked(void* memory, AllocatorHandle allocator) => Free(memory, allocator);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>() where T : struct => Unsafe.SizeOf<T>();

        public static int SizeOf(Type type) =>
            (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(type).Invoke(null, null);

        private struct AlignOfHelper<T> where T : struct
        {
            public byte dummy;
            public T data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignOf<T>() where T : struct => Unsafe.SizeOf<AlignOfHelper<T>>() - Unsafe.SizeOf<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* AddressOf<T>(ref T output) where T : struct => Unsafe.AsPointer(ref output);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AsRef<T>(void* ptr) where T : struct => ref Unsafe.AsRef<T>(ptr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T As<U, T>(ref U from) => ref Unsafe.As<U, T>(ref from);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadArrayElement<T>(void* source, int index) =>
            Unsafe.Read<T>((byte*)source + (long)index * Unsafe.SizeOf<T>());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteArrayElement<T>(void* destination, int index, T value) =>
            Unsafe.Write((byte*)destination + (long)index * Unsafe.SizeOf<T>(), value);

        // Buffer.MemoryCopy handles overlapping ranges, so it serves both copy and move.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemCpy(void* destination, void* source, long size) =>
            Buffer.MemoryCopy(source, destination, size, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemMove(void* destination, void* source, long size) =>
            Buffer.MemoryCopy(source, destination, size, size);

        public static void MemSet(void* destination, byte value, long size)
        {
            var p = (byte*)destination;
            while (size > 0)
            {
                var chunk = (uint)Math.Min(size, uint.MaxValue);
                Unsafe.InitBlockUnaligned(p, value, chunk);
                p += chunk;
                size -= chunk;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemClear(void* destination, long size) => MemSet(destination, 0, size);

        public static int MemCmp(void* ptr1, void* ptr2, long size)
        {
            var a = (byte*)ptr1;
            var b = (byte*)ptr2;
            while (size > 0)
            {
                var chunk = (int)Math.Min(size, int.MaxValue);
                var result = new ReadOnlySpan<byte>(a, chunk).SequenceCompareTo(new ReadOnlySpan<byte>(b, chunk));
                if (result != 0) return result;
                a += chunk;
                b += chunk;
                size -= chunk;
            }
            return 0;
        }
#endif
    }
}
