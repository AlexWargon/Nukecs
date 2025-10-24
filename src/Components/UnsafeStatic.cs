using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace Wargon.Nukecs
{
    public static class UnsafeStatic
    {

        [MethodImpl(inline.YES)]
        public static unsafe void write_element<T>(void* ptr, int index, in T value) where T : unmanaged
        {
            *((T*)ptr + index) = value;
        }
        
        [MethodImpl(inline.YES)]
        public static unsafe ref T get_ref_element<T>(void* ptr, int index) where T : unmanaged
        {
            return ref *((T*)ptr + index);
        }
        
        [MethodImpl(inline.YES)]
        public static unsafe ref T get_ref_element<T>(byte* ptr, int index) where T : unmanaged
        {
            return ref *((T*)ptr + index);
        }
        
        [MethodImpl(inline.YES)]
        public static unsafe T get_element<T>(byte* ptr, int index) where T : unmanaged
        {
            return *((T*)ptr + index);
        } 
        
        [MethodImpl(inline.YES)]
        public static unsafe ref T get_ref<T>(void* ptr) where T : unmanaged
        {
            return ref *(T*)ptr;
        }
        
        [MethodImpl(inline.YES)] 
        public static unsafe void write_value<T>(void* ptr, T value) where T : unmanaged
        {
            *(T*)ptr = value;
        }
        
        [MethodImpl(inline.YES)] 
        public static unsafe void* struct_to_ptr<T>(ref T value) where T : unmanaged
        {
            return UnsafeUtility.AddressOf(ref value);
        }
        
        [MethodImpl(inline.YES)] 
        public static unsafe void memcpy(void* dest, void* src, int length)
        {
            UnsafeUtility.MemCpy(dest, src, length);
        }
        
        [MethodImpl(inline.YES)] 
        public static unsafe void mem_move(void* dest, void* src, long length)
        {
            UnsafeUtility.MemMove(dest, src, length);
        }
        [MethodImpl(inline.YES)] 
        public static unsafe void mem_clear(void* dest, long size)
        {
            UnsafeUtility.MemClear(dest, size);
        }
        [MethodImpl(inline.YES)] 
        public static unsafe T* cast<T>(void* ptr) where T : unmanaged
        {
            return (T*)ptr;
        }
        
        [MethodImpl(inline.YES)] 
        public static TTo cast<TFrom, TTo>(ref TFrom u) where TTo : struct where TFrom : struct
        {
            return UnsafeUtility.As<TFrom, TTo>(ref u);
        }

        public static unsafe T* malloc<T>(Unity.Collections.Allocator allocator) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
        public static unsafe T* malloc_t<T>(Unity.Collections.Allocator allocator) where T : unmanaged
        {
            return (T*)UnsafeUtility.MallocTracked(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator, 0);
        }
    }
    public static class inline
    {
        public const int YES = 256;
        public const int NO = 8;
    }
}