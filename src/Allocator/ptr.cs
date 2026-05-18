using Unity.Collections;
// ReSharper disable InconsistentNaming

namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using static UnsafeStatic;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ptr
    {
        public ptr_offset offset;
        [NativeDisableUnsafePtrRestriction]
        public byte* cached;
        public static readonly ptr NULL = new (null, 0u);
        public bool IsNull => cached == null;
        public ptr(byte* basePtr, uint offset)
        {
            this.offset = new ptr_offset(0, offset);
            cached = basePtr + offset;
        }
        
        public ptr(byte* regionBase, ptr_offset off)
        {
            this.offset = off;
            cached = regionBase + off.Offset;
        }

        public ptr(void* ptr, ptr_offset offset)
        {
            this.offset = offset;
            this.cached = (byte*)ptr;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* As<T>() where T : unmanaged
        {
            return (T*)cached;
        }

        public ptr<T> AsTyped<T>() where T : unmanaged
        {
            return new ptr<T>(cached, offset.Offset, true);
        }
        
        internal ptr_str<T> as_ptr_str<T>() where T : struct
        {
            return new ptr_str<T>(cached, offset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AsRef<T>() where T : unmanaged
        {
            return ref *(T*)cached;
        }

        public T AsObject<T>() 
        {
            return Unsafe.As<IntPtr,T>(ref *(IntPtr*)cached);
        }
        public void OnDeserialize(ref MemAllocator allocator)
        {
            if (offset.BlockIndex < allocator.RegionCount)
                cached = allocator.GetRegionPtr((int)offset.BlockIndex) + offset.Offset;
        }

        public override string ToString()
        {
            return new IntPtr(cached).ToString();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ptr_offset
    {
        public uint Offset;
        public uint BlockIndex;
        public const int SIZE_OF_BYTES = 8;
        public static readonly ptr_offset NULL = new (uint.MaxValue, uint.MaxValue);

        public bool IsNull => BlockIndex == uint.MaxValue;

        public ptr_offset(uint blockIndex, uint offset)
        {
            BlockIndex = blockIndex;
            Offset = offset;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* AsPtr(ref MemAllocator allocator)
        {
            if (BlockIndex == uint.MaxValue) return null;
            return allocator.GetRegionPtr((int)BlockIndex) + Offset;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T* AsPtr<T>(ref MemAllocator allocator) where T : unmanaged
        {
            if (BlockIndex == uint.MaxValue) return null;
            return (T*)(allocator.GetRegionPtr((int)BlockIndex) + Offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T* AsPtr<T>(void* ptr) where T : unmanaged
        {
            return (T*)ptr;
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
    internal unsafe struct ptr_str<T> : IEquatable<ptr_str<T>> where T : struct
    {
        public ptr_offset offset;
        [NativeDisableUnsafePtrRestriction]
        public byte* cached;

        public ref T Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref as_ref<T>(cached);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ptr_str(byte* regionBase, ptr_offset off)
        {
            offset = off;
            cached = regionBase + off.Offset;
        }
        
        public void OnDeserialize(ref MemAllocator allocator)
        {
            if (offset.BlockIndex < allocator.RegionCount)
                cached = allocator.GetRegionPtr((int)offset.BlockIndex) + offset.Offset;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ptr_str<T> other)
        {
            return other.offset.Offset.Equals(offset.Offset);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator != (ptr_str<T> lhs, ptr_str<T> rhs)
        {
            return lhs.offset.Offset != rhs.offset.Offset || lhs.offset.BlockIndex != rhs.offset.BlockIndex;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator == (ptr_str<T> lhs, ptr_str<T> rhs)
        {
            return lhs.offset.Offset == rhs.offset.Offset && lhs.offset.BlockIndex == rhs.offset.BlockIndex;
        }
        
        public override bool Equals(object obj)
        {
            return obj is ptr_str<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(offset.Offset, offset.BlockIndex);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once InconsistentNaming
    public unsafe struct ptr<T> : IEquatable<ptr<T>> where T : unmanaged
    {
        public ptr_offset offset;
        [NativeDisableUnsafePtrRestriction]
        public T* cached;
        public static readonly ptr<T> NULL = new (null, 0u);

        public bool IsNull {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get=>cached == null;
        }

        public bool IsDefault {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => offset.Offset == 0u;
        }
        public void OnDeserialize(ref MemAllocator allocator)
        {
            if (offset.BlockIndex < allocator.RegionCount)
                cached = (T*)(allocator.GetRegionPtr((int)offset.BlockIndex) + offset.Offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ptr(byte* basePtr, uint offset)
        {
            this.offset = new ptr_offset(0, offset);
            cached = (T*)(basePtr + offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ptr(byte* regionBase, ptr_offset off)
        {
            offset = off;
            cached = (T*)(regionBase + off.Offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ptr(byte* ptr, uint offset, bool fromOffseted = true)
        {
            this.offset = new ptr_offset(0, offset);
            cached = (T*)ptr;
        }
        public ptr UntypedPointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new (cached, offset);
        }
        public T* Ptr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => cached;
        }
        public ref T Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref *cached;
            }
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *cached;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ptr<T> other)
        {
            return other.offset.Offset.Equals(offset.Offset);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator != (ptr<T> lhs, ptr<T> rhs)
        {
            return lhs.offset.Offset != rhs.offset.Offset;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator == (ptr<T> lhs, ptr<T> rhs)
        {
            return lhs.offset.Offset == rhs.offset.Offset;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                return (int)offset.Offset;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return new IntPtr(cached).ToString();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ptr<T>(ptr<byte> ptr)
        {
            return new ptr<T>(basePtr:ptr.cached, ptr.offset.Offset);
        }
    }

    public unsafe struct safe_ptr<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private T* _ptr;
        public ref T Ref => ref *_ptr;

        public static safe_ptr<T> New()
        {
            return new safe_ptr<T>
            {
                _ptr = malloc<T>(Allocator.Persistent)
            };
        }

        public void Dispose()
        {
            free(_ptr, Allocator.Persistent);
            
        }
    }

    public unsafe struct _object
    {
        private void* ptr;
    }

    public unsafe struct Reader
    {
        private byte* buffer;
        private long offset;

        public Reader(byte[] data)
        {
            buffer = (byte*)Unsafe.AsPointer(ref data);
            offset = 0;
        }
        public byte ReadByte()
        {
            var oldOffset = offset;
            offset += size_of<byte>();
            return as_ref(buffer + oldOffset);
        }
    }
    public unsafe struct Writer
    {
        private byte* ptr;
        private long offset;
        public void WriteInt(int value)
        {
            as_ref<int>(ptr + offset) = value;
            offset += sizeof(int);
        }
        public void WriteFloat(float value)
        {
            as_ref<float>(ptr + offset) = value;
            offset += sizeof(float);
        }
        public void WriteBool(bool value)
        {
            as_ref<bool>(ptr + offset) = value;
            offset += sizeof(bool);
        }

        public void WriteByte(byte value)
        {
            as_ref<byte>(ptr + offset) = value;
            offset += sizeof(byte);
        }

        public void WriteULong(ulong value)
        {
            as_ref<ulong>(ptr + offset) = value;
            offset += sizeof(ulong);
        }

        public void WriteShort(short value)
        {
            as_ref<short>(ptr + offset) = value;
            offset += sizeof(short);
        }

        public void WriteUShort(ushort value)
        {
            as_ref<ushort>(ptr + offset) = value;
            offset += sizeof(ushort);
        }

        public void WriteLong(long value)
        {
            as_ref<long>(ptr + offset) = value;
            offset += sizeof(long);
        }

        public void WriteStruct<T>(T value) where T : struct
        {
            as_ref<T>(ptr + offset) = value;
            offset += size_of<T>();
        }
    }
}
