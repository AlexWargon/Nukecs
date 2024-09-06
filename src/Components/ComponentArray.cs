using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public unsafe struct ComponentArray<T> : IComponent, IDisposable<ComponentArray<T>>, ICopyable<ComponentArray<T>> where T : unmanaged {
        internal UnsafeList<T> list;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentArray(int capacity) {
            list = new UnsafeList<T>(capacity, Allocator.Persistent);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref T ElementAt(int index) => ref list.Ptr[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T item) {
            list.Add(in item);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index) {
            list.RemoveAt(index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            list.Clear();
        }
        public void Dispose(ref ComponentArray<T> value) {
            value.list.Dispose();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentArray(ref ComponentArray<T> other) {
            list = new UnsafeList<T>(other.list.m_capacity, other.list.Allocator);
            list.CopyFrom(in other.list);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentArray<T> Copy(ref ComponentArray<T> toCopy) {
            return new ComponentArray<T>(ref toCopy);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentArray<T> CreateAndFill(in T data, int size, Allocator allocator) {
            var array = new ComponentArray<T>(size);
            array.list.AddReplicate(in data, size);
            return array;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() {
            return new Enumerator(list.Ptr, list.m_length);
        }
        public struct Enumerator {
            private readonly T* _listPtr;
            private readonly int _len;
            private int _index;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(T* list, int lenght) {
                _listPtr = list;
                _len = lenght;
                _index = -1;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                _index++;
                return _index < _len;
            }
            public void Reset() {
                _index = -1;
            }

            public ref T Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _listPtr[_index];
            }
            public void Dispose() {
                
            }
        }

    }
}