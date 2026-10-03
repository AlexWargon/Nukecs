using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Thread-safe (spinlock-protected) queue of unmanaged values. Mirrors the
    /// parallel-write pattern of <c>Events&lt;T&gt;.AddPar</c> in
    /// <c>src/Systems/FnSystems/Events.cs</c>. Reader side is single-threaded
    /// (the dispatch system drains the queue on a Single thread / main thread).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ChangedQueue<T> where T : unmanaged
    {
        private NativeList<T> _list;
        private Spinner _spinner;
        private byte _created;

        public bool IsCreated => _created != 0;

        public ChangedQueue(int capacity, Allocator allocator)
        {
            _list = new NativeList<T>(capacity, allocator);
            _spinner = default;
            _created = 1;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _list.Length;
        }

        public int Capacity => _list.Capacity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(in T item)
        {
            _list.Add(item);
        }

        /// <summary>
        /// Parallel-safe enqueue. Use from Burst/Parallel systems.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnqueuePar(in T item)
        {
            _spinner.Acquire();
            _list.Add(item);
            _spinner.Release();
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _list.ElementAt(index);
        }

        public unsafe T* GetUnsafePtr()
        {
            return (T*)_list.GetUnsafePtr();
        }

        public void Clear()
        {
            _list.Clear();
        }

        public void Dispose()
        {
            if (_created == 0) return;
            if (_list.IsCreated) _list.Dispose();
            _created = 0;
        }
    }
}
