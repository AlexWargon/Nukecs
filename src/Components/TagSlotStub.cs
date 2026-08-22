using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Per-type storage used as a stable address for tag components.
    /// Tags carry no data and occupy no bytes in the archetype data buffer, so
    /// pointer-based iteration and Entity.Get&lt;T&gt;() for a tag resolve to this stub.
    /// Backed by SharedStatic (unmanaged, Burst-compatible, stable address) —
    /// the same pattern as ComponentType&lt;T&gt;.ID.
    /// Tags have no fields, so the value behind the pointer is never read.
    /// </summary>
    public static unsafe class TagSlotStub<T> where T : unmanaged
    {
        private struct Context { }
        private static readonly SharedStatic<T> Slot = SharedStatic<T>.GetOrCreate<Context>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* GetPtr()
        {
            return (T*)Slot.UnsafeDataPointer;
        }
    }
}
