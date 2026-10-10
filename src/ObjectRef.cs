using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs {
    internal static class StaticObjectRefStorage
    {
        internal static readonly AutoArray<object> Objects = new AutoArray<object>(32, 1);

        internal static int Add<T>(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item), "Cannot add null to StaticObjectRefStorage");
            return Objects.Add(item);
        }

        internal static void Remove(int index)
        {
            Objects.Remove(index);
        }

        internal static void Clear()
        {
            Objects.Clear();
        }
    }

    internal class AutoArray<T>
    {
        private int _count;
        private int _freeCount;
        private T[] _array;
        private int[] _freeIndices;

        public AutoArray(int capacity, int start = 0)
        {
            _count = start;
            _freeCount = 0;
            _array = new T[capacity];
            _freeIndices = new int[capacity];
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (index < 0 || index >= _array.Length) throw new IndexOutOfRangeException();
                return _array[index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (index < 0) throw new IndexOutOfRangeException();
                if (index >= _array.Length)
                {
                    var newSize = Math.Max(_array.Length * 2, index + 1);
                    Array.Resize(ref _array, newSize);
                    Array.Resize(ref _freeIndices, newSize);
                }
                _array[index] = value;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Add(T value)
        {
            int index;
            if (_freeCount > 0)
            {
                index = _freeIndices[--_freeCount];
            }
            else
            {
                index = _count++;
            }
            this[index] = value;
            return index;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index)
        {
            if (index < 0 || index >= _array.Length) throw new IndexOutOfRangeException();
            _array[index] = default;
            if (_freeCount >= _freeIndices.Length)
            {
                Array.Resize(ref _freeIndices, _freeIndices.Length * 2);
            }
            _freeIndices[_freeCount++] = index;
        }

        public void Clear()
        {
            Array.Clear(_array, 0, _array.Length);
            Array.Clear(_freeIndices, 0, _freeIndices.Length);
            _count = 0;
            _freeCount = 0;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ObjectRef<T> : IEquatable<ObjectRef<T>>, IDisposable where T : class
    {
        private int pointer;
        private const int INVALID_POINTER = -1;

        public ObjectRef(T instance)
        {
            pointer = instance != null ? StaticObjectRefStorage.Add(instance) : INVALID_POINTER;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(ObjectRef<T> objectRef)
        {
            return objectRef.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ObjectRef<T>(T instance)
        {
            return new ObjectRef<T>(instance);
        }
        
        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => pointer == INVALID_POINTER ? null : (T)StaticObjectRefStorage.Objects[pointer];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (pointer != INVALID_POINTER)
                {
                    StaticObjectRefStorage.Remove(pointer);
                }
                pointer = value != null ? StaticObjectRefStorage.Add(value) : INVALID_POINTER;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ObjectRef<T> other)
        {
            return pointer == other.pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is ObjectRef<T> other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(ObjectRef<T> obj)
        {
            return obj.IsValid();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return pointer.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            return pointer != INVALID_POINTER && StaticObjectRefStorage.Objects[pointer] != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ObjectRef<T> left, ObjectRef<T> right)
        {
            return left.pointer == right.pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ObjectRef<T> left, ObjectRef<T> right)
        {
            return left.pointer != right.pointer;
        }

        public void Dispose()
        {
            if (pointer != INVALID_POINTER)
            {
                StaticObjectRefStorage.Remove(pointer);
                pointer = INVALID_POINTER;
            }
        }

        public void DisposeNotRemoving()
        {
            pointer = INVALID_POINTER;
        }
        
    }
}
