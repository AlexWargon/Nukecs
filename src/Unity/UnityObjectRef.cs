using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_6000_4_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

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
        public NativeHashMap<ObjectId, int> InstanceIDMap;
        public NativeList<ObjectId> InstanceIDs;

        public bool IsCreated => InstanceIDs.IsCreated && InstanceIDMap.IsCreated;

        public UnityObjectRefMap(Allocator allocator)
        {
            InstanceIDMap = new NativeHashMap<ObjectId, int>(0, allocator);
            InstanceIDs = new NativeList<ObjectId>(0, allocator);
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
                UnityObjectId.ToObjectList(InstanceIDs.AsArray(), objects);

            return objects.ToArray();
        }

        public int Add(ObjectId instanceId)
        {
            var index = -1;
            if (!UnityObjectId.IsNone(instanceId) && IsCreated)
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
        internal ObjectId instanceId;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UntypedUnityObjectRef other)
        {
            return instanceId.Equals(other.instanceId);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is UntypedUnityObjectRef other && Equals(other);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return instanceId.GetHashCode();
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
            return FromInstanceID(UnityObjectId.Of(instance));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static UnityObjectRef<T> FromInstanceID(ObjectId instanceId)
        {
            var result = new UnityObjectRef<T>{Id = new UntypedUnityObjectRef{ instanceId = instanceId }};
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(UnityObjectRef<T> unityObjectRef)
        {
            return (T) UnityObjectId.ToObject(unityObjectRef.Id.instanceId);
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
            return Id.instanceId.Equals(other.Id.instanceId);
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
            return UnityObjectId.IsValid(Id.instanceId);
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
}
