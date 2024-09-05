using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

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
        public Enumerator GetEnumerator() {
            fixed (UnsafeList<T>* ptr = &list) {
                return new Enumerator(ptr);
            }
        }
        public struct Enumerator {
            public UnsafeList<T>* listPtr;
            private int index;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(UnsafeList<T>* list) {
                listPtr = list;
                index = -1;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                index++;
                return index < listPtr->m_length;
            }
            public void Reset() {
                index = -1;
            }

            public ref T Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref listPtr->Ptr[index];
            }
            public void Dispose() {
                
            }
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
    }
}