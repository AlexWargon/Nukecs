using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once InconsistentNaming
    public unsafe struct ptr
    {
        public ptr_offset offset;
        private byte* cached;
        public static readonly ptr NULL = new (null, 0u);
        public ptr(byte* basePtr, uint offset)
        {
            this.offset = new ptr_offset(0, offset);
            cached = basePtr + offset;
        }

        public ptr(void* ptr, ptr_offset offset)
        {
            this.offset = offset;
            cached = (byte*)ptr;
        }
        public T* As<T>() where T : unmanaged
        {
            return (T*)cached;
        }
        public void OnDeserialize(ref MemAllocator allocator)
        {
            cached = allocator.BasePtr + offset.Offset;
        }

        public override string ToString()
        {
            return new IntPtr(cached).ToString();
        }
    }
    // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    public struct ptr_offset
    {
        public uint Offset;
        public uint BlockIndex;
        public const int SIZE_OF_BYTES = 8;
        public static readonly ptr_offset NULL = new (0u,0u);

        public ptr_offset(uint blockIndex, uint offset)
        {
            BlockIndex = blockIndex;
            Offset = offset;
        }
        
        public unsafe void* AsPtr(ref MemAllocator allocator)
        {
            return allocator.BasePtr + allocator.Blocks[BlockIndex].Pointer + Offset;
        }
        
        public unsafe T* AsPtr<T>(ref MemAllocator allocator) where T : unmanaged
        {
            return (T*)(allocator.BasePtr + Offset);
        }

        public unsafe void* AsPtr(byte[] buffer)
        {
            fixed (byte* ptr = buffer)
            {
                return ptr + Offset;
            }
        }

        public unsafe T* AsPtr<T>(byte[] buffer) where T : unmanaged
        {
            fixed (byte* ptr = buffer)
            {
                return (T*)(ptr + Offset);
            }
        }
    }

    // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ptr<T> : IEquatable<ptr<T>> where T : unmanaged
    {
        public ptr_offset offset;
        [NativeDisableUnsafePtrRestriction]
        private T* cached;
        public static readonly ptr<T> NULL = new (null, 0u);
        public void OnDeserialize(ref MemAllocator allocator)
        {
            cached = (T*)(allocator.BasePtr + offset.Offset);
        }

        public ptr(byte* basePtr, uint offset)
        {
            this.offset = new ptr_offset(0, offset);
            cached = (T*)(basePtr + offset);
        }

        public ptr UntypedPointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new ptr(cached, offset);
        }
        public T* Ptr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => cached;
        }
        
        public ref T Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *cached;
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *cached;
        }

        public bool Equals(ptr<T> other)
        {
            return other.offset.Offset.Equals(offset.Offset);
        }
        public static bool operator != (ptr<T> lhs, ptr<T> rhs)
        {
            return lhs.offset.Offset != rhs.offset.Offset;
        }
        public static bool operator == (ptr<T> lhs, ptr<T> rhs)
        {
            return lhs.offset.Offset == rhs.offset.Offset;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)offset.Offset;    
            }
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override string ToString()
        {
            return new IntPtr(cached).ToString();
        }
    }
}