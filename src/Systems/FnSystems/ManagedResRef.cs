using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Wargon.Nukecs
{
    internal class ManagedResStorage
    {
        internal static ConcurrentDictionary<int, IRes> resources = new ();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int AddResource(IRes res)
        {
            var hash = GetStableHashCode(res.GetType().FullName);
            resources.AddOrUpdate(hash, res, (_, _) => res);
            return hash;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T GetResource<T>(int hash) => (T)resources[hash];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGetResource<T>(int hash, out T value)
        {
            if (resources.TryGetValue(hash, out var obj))
            {
                value = (T)obj;
                return true;
            }

            value = default;
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RemoveResource(int hash) => resources.TryRemove(hash, out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasResource(int hash) => resources.ContainsKey(hash);
        private static int GetStableHashCode(string str)
        {
            unchecked
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ManagedResRef<T> : IEquatable<ManagedResRef<T>>, IDisposable where T : IRes
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
            get => INVALID_POINTER != pointer ? ManagedResStorage.GetResource<T>(pointer) : default;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value == null)
                {
                    ManagedResStorage.RemoveResource(pointer);
                    pointer = INVALID_POINTER;
                    return;
                }
                var newPointer = ManagedResStorage.AddResource(value);
                if (pointer == newPointer) return;
                ManagedResStorage.RemoveResource(pointer);
                pointer = newPointer;
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