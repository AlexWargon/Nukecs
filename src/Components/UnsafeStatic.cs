using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace Wargon.Nukecs
{
    public static class UnsafeStatic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void write_element<T>(void* ptr, int index, in T value) where T : unmanaged
        {
            *((T*)ptr + index) = value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe ref T get_ref_element<T>(void* ptr, int index) where T : unmanaged
        {
            return ref *((T*)ptr + index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe ref T get_ref_element<T>(byte* ptr, int index) where T : unmanaged
        {
            return ref *((T*)ptr + index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe T get_element<T>(byte* ptr, int index) where T : unmanaged
        {
            return *((T*)ptr + index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe T* get_element_ptr<T>(void* ptr, int index) where T : unmanaged
        {
            return (T*)ptr + index;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe ref T get_ref<T>(void* ptr) where T : unmanaged
        {
            return ref *(T*)ptr;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void write_value<T>(void* ptr, T value) where T : unmanaged
        {
            *(T*)ptr = value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void* struct_to_ptr<T>(ref T value) where T : unmanaged
        {
            return UnsafeUtility.AddressOf(ref value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void memcpy(void* dest, void* src, int length)
        {
            UnsafeUtility.MemCpy(dest, src, length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void mem_move(void* dest, void* src, long length)
        {
            UnsafeUtility.MemMove(dest, src, length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void mem_clear(void* dest, long size)
        {
            UnsafeUtility.MemClear(dest, size);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe T* cast<T>(void* ptr) where T : unmanaged
        {
            return (T*)ptr;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static TTo cast<TFrom, TTo>(ref TFrom u) where TTo : struct where TFrom : struct
        {
            return UnsafeUtility.As<TFrom, TTo>(ref u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe T* malloc<T>(Unity.Collections.Allocator allocator) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void* malloc_t<T>(Unity.Collections.Allocator allocator) where T : struct
        {
            return UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), allocator, 0);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void* malloc_t<T>(Unity.Collections.Allocator allocator, T value) where T : struct
        {
            var ptr = UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), allocator, 0);
            UnsafeUtility.AsRef<T>(ptr) = value;
            return ptr;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe T* malloc_t_cast<T>(Unity.Collections.Allocator allocator) where T : unmanaged
        {
            return (T*)UnsafeUtility.MallocTracked(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator, 0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void free(void* ptr, Unity.Collections.Allocator allocator)
        {
            UnsafeUtility.Free(ptr, allocator);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void free_t(void* ptr, Unity.Collections.Allocator allocator)
        {
            UnsafeUtility.FreeTracked(ptr, allocator);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe ref T as_ref<T>(T* ptr) where T: unmanaged
        {
            return ref Unsafe.AsRef<T>(ptr);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe ref T as_ref<T>(void* ptr) where T: struct
        {
            return ref Unsafe.AsRef<T>(ptr);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void* as_ptr<T>(ref T value) where T: struct
        {
            return UnsafeUtility.AddressOf(ref value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static unsafe void* as_ptr_copy<T>(T value) where T: struct
        {
            return UnsafeUtility.AddressOf(ref value);
        }

        public static unsafe void ptr_write<T>(void* ptr, T value) where T : struct
        {
            Unsafe.AsRef<T>(ptr) = value;
        }
    }
}