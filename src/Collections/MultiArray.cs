using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Collections
{
    public unsafe struct Segment<T>
        where T : unmanaged
    {
        public T* Ptr;
        public int Length;

        public Segment(T* ptr, int length)
        {
            Ptr = ptr;
            Length = length;
        }
    }

    public unsafe struct MultiArray<T> : IDisposable
        where T : unmanaged
    {
        private Segment<T>* _segments;

        private int _capacity;
        private int _count;
        private int _totalLength;
        private Allocator _allocator;
        public int SegmentCount => _count;
        public int Length => _totalLength;

        public MultiArray(int segmentCapacity, Allocator allocator)
        {
            _capacity = segmentCapacity;
            _count = 0;
            _totalLength = 0;

            _segments = (Segment<T>*)
                UnsafeUtility.MallocTracked(
                    (uint)segmentCapacity * sizeof(Segment<T>),
                    UnsafeUtility.AlignOf<Segment<T>>(),
                    allocator,
                    0);
            _allocator = allocator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T* ptr, int length)
        {
            if (_count >= _capacity)
                throw new InvalidOperationException(
                    "Segment capacity exceeded"
                );

            _segments[_count] =
                new Segment<T>(ptr, length);

            _count++;
            _totalLength += length;
        }

        public ref T this[int index]
        {
            get
            {
                for (int i = 0; i < _count; i++)
                {
                    ref var seg = ref _segments[i];

                    if (index < seg.Length)
                        return ref seg.Ptr[index];

                    index -= seg.Length;
                }

                throw new IndexOutOfRangeException();
            }
        }

        public Enumerator GetEnumerator()
            => new(_segments, _count);

        public void Dispose()
        {
            if (_segments != null)
            {
                UnsafeUtility.FreeTracked(_segments, _allocator);
                _segments = null;
            }

            _count = 0;
            _capacity = 0;
            _totalLength = 0;
        }

        public struct Enumerator
        {
            private Segment<T>* _segments;
            private int _segmentCount;

            private int _segmentIndex;
            private int _elementIndex;

            public Enumerator(
                Segment<T>* segments,
                int segmentCount)
            {
                _segments = segments;
                _segmentCount = segmentCount;

                _segmentIndex = 0;
                _elementIndex = -1;
            }

            public ref T Current
            {
                get
                {
                    return ref _segments[_segmentIndex]
                        .Ptr[_elementIndex];
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                while (_segmentIndex < _segmentCount)
                {
                    _elementIndex++;

                    if (_elementIndex <
                        _segments[_segmentIndex].Length)
                        return true;

                    _segmentIndex++;
                    _elementIndex = 0;
                }

                return false;
            }
        }
    }
}