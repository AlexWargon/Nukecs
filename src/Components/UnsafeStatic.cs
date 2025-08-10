using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public static class UnsafeStatic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void write_element<T>(void* ptr, int index, T value) where T : unmanaged
        {
            *((T*)ptr + index) = value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T get_element<T>(void* ptr, int index) where T : unmanaged
        {
            return ref *((T*)ptr + index);
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
        public static unsafe void memmove(void* dest, void* src, long length)
        {
            UnsafeUtility.MemMove(dest, src, length);
        }
    }
 
    public unsafe struct size<T> where T : unmanaged
    {
        public static readonly int get = sizeof(T);
    }
}