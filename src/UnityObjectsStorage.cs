using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wargon.Nukecs {
    // public class UnityObjectsStorage {
    //     private static bool created;
    //     private static UnityObjectsStorage singletone;
    //
    //     public static UnityObjectsStorage Singletone {
    //         get {
    //             if (created != false) return singletone;
    //             singletone = new UnityObjectsStorage();
    //             created = true;
    //             return singletone;
    //         }
    //     }
    //
    //     private Dictionary<int, UnityEngine.Object> map = new();
    //
    //     public int Add<T>(T obj) where T : UnityEngine.Object {
    //         var id = obj.GetInstanceID();
    //         map[id] = obj;
    //         return id;
    //     }
    //
    //     public T Get<T>(int guid) where T : UnityEngine.Object {
    //         return (T) map[guid];
    //     }
    // }

    // public struct UnityRef<T> where T : UnityEngine.Object {
    //     private int _guid;
    //
    //     public T Value {
    //         get => UnityObjectsStorage.Singletone.Get<T>(_guid);
    //         set => _guid = UnityObjectsStorage.Singletone.Add(value);
    //     }
    // }
    
    
    internal struct UnityObjectRefMap : IDisposable
    {
        public NativeHashMap<int, int> InstanceIDMap;
        public NativeList<int> InstanceIDs;

        public bool IsCreated => InstanceIDs.IsCreated && InstanceIDMap.IsCreated;

        public UnityObjectRefMap(Allocator allocator)
        {
            InstanceIDMap = new NativeHashMap<int, int>(0, allocator);
            InstanceIDs = new NativeList<int>(0, allocator);
        }

        public void Dispose()
        {
            InstanceIDMap.Dispose();
            InstanceIDs.Dispose();
        }

        public UnityEngine.Object[] ToObjectArray()
        {
            var objects = new System.Collections.Generic.List<UnityEngine.Object>();

            if (IsCreated && InstanceIDs.Length > 0)
                Resources.InstanceIDToObjectList(InstanceIDs.AsArray(), objects);

            return objects.ToArray();
        }

        public int Add(int instanceId)
        {
            var index = -1;
            if (instanceId != 0 && IsCreated)
            {
                if (!InstanceIDMap.TryGetValue(instanceId, out index))
                {
                    index = InstanceIDs.Length;
                    InstanceIDMap.Add(instanceId, index);
                    InstanceIDs.Add(instanceId);
                }
            }

            return index;
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct UntypedUnityObjectRef : IEquatable<UntypedUnityObjectRef>
    {
        [SerializeField]
        internal int instanceId;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UntypedUnityObjectRef other)
        {
            return instanceId == other.instanceId;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is UntypedUnityObjectRef other && Equals(other);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return instanceId;
        }
    }

    /// <summary>
    /// A utility structure that stores a reference of an <see cref="UnityEngine.Object"/> for the BakingSystem to process in an unmanaged component.
    /// </summary>
    /// <typeparam name="T">Type of the Object that is going to be referenced by UnityObjectRef.</typeparam>
    /// <remarks>Stores the Object's instance ID. This means that the reference is only valid during the baking process.</remarks>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct UnityObjectRef<T> : IEquatable<UnityObjectRef<T>>
        where T : Object
    {
        [SerializeField]
        internal UntypedUnityObjectRef Id;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator UnityObjectRef<T>(T instance)
        {
            var instanceId = instance == null ? 0 : instance.GetInstanceID();

            return FromInstanceID(instanceId);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static UnityObjectRef<T> FromInstanceID(int instanceId)
        {
            var result = new UnityObjectRef<T>{Id = new UntypedUnityObjectRef{ instanceId = instanceId }};
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(UnityObjectRef<T> unityObjectRef)
        {
            if (unityObjectRef.Id.instanceId == 0)
                return null;
            return (T) Resources.InstanceIDToObject(unityObjectRef.Id.instanceId);
        }
        
        /// <summary>
        /// Object being referenced by this <see cref="UnityObjectRef{T}"/>.
        /// </summary>
        public T Value
        {
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this;
            [ExcludeFromBurstCompatTesting("Sets managed object")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UnityObjectRef<T> other)
        {
            return Id.instanceId == other.Id.instanceId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is UnityObjectRef<T> other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(UnityObjectRef<T> obj)
        {
            return obj.IsValid();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return Id.instanceId.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            return Resources.InstanceIDIsValid(Id.instanceId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UnityObjectRef<T> left, UnityObjectRef<T> right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UnityObjectRef<T> left, UnityObjectRef<T> right)
        {
            return !left.Equals(right);
        }
    }

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