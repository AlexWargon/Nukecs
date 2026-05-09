using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    internal class ManagedResStorage
    {
        internal static ConcurrentDictionary<int, IResource> resources = new ();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int AddResource(IResource resource)
        {
            var hash = resource.GetType().FullName!.GetHashCode();
            resources[hash] = resource;
            return hash;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T GetResource<T>(int hash) => (T)resources[hash];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RemoveResource(int hash) => resources.TryRemove(hash, out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasResource(int hash) => resources.ContainsKey(hash);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ManagedResRef<T> : IEquatable<ManagedResRef<T>>, IDisposable where T : IResource, new()
    {
        private int pointer;
        private const int INVALID_POINTER = -1;

        public ManagedResRef(T instance)
        {
            pointer = instance != null ? ManagedResStorage.AddResource(instance) : INVALID_POINTER;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(ManagedResRef<T> objectRef)
        {
            return objectRef.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ManagedResRef<T>(T instance)
        {
            return new ManagedResRef<T>(instance);
        }
        
        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => pointer == INVALID_POINTER ? default : ManagedResStorage.GetResource<T>(pointer);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (pointer != INVALID_POINTER)
                {
                    StaticObjectRefStorage.Remove(pointer);
                }
                pointer = value != null ? ManagedResStorage.AddResource(value) : INVALID_POINTER;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ManagedResRef<T> other)
        {
            return pointer == other.pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is ManagedResRef<T> other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(ManagedResRef<T> obj)
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
            return pointer != INVALID_POINTER && ManagedResStorage.HasResource(pointer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ManagedResRef<T> left, ManagedResRef<T> right)
        {
            return left.pointer == right.pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ManagedResRef<T> left, ManagedResRef<T> right)
        {
            return left.pointer != right.pointer;
        }

        public void Dispose()
        {
            if (pointer != INVALID_POINTER)
            {
                ManagedResStorage.RemoveResource(pointer);
                pointer = INVALID_POINTER;
            }
        }

        public void DisposeNotRemoving()
        {
            pointer = INVALID_POINTER;
        }
    }
}