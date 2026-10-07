using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Stable address for tag components.
    /// Tags carry no data and occupy no bytes in the archetype data buffer, so
    /// pointer-based iteration and Entity.Get&lt;T&gt;() for a tag resolve to this stub.
    /// Tags have no fields, so the value behind the pointer is never read.
    /// All tag types share one fixed-size SharedStatic block (unmanaged, Burst-compatible):
    /// a per-type SharedStatic&lt;T&gt; would fix its size to sizeof(T) for the whole editor
    /// session, and turning a tag into a data component would then fail with a size mismatch.
    /// </summary>
    public static unsafe class TagSlotStub<T> where T : unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* GetPtr()
        {
            return (T*)TagSlotStorage.Slot.UnsafeDataPointer;
        }
    }

    internal struct TagSlotStorage
    {
        private struct Context { }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        internal struct Block { }

        internal static readonly SharedStatic<Block> Slot = SharedStatic<Block>.GetOrCreate<Context>(16);
    }
}
