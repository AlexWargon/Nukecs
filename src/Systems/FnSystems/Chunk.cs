// ReSharper disable UnusedMember.Global

namespace Wargon.Nukecs
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using static UnsafeStatic;
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T> : IChunk where T : unmanaged, IComponent
    {
        private T* _components;
        private int _count;
        private int _remaining;
        public int Count => _count;
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var li = archetype.GetComponentLocalIndex(ComponentType<T>.Index);
            _components = (T*)(archetype.data.Ptr + archetype.GetComponentOffset(li));
            _count = archetype.count;
            _remaining = _count;
        }
        public Chunk<T> Current => this;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (--_remaining < 0) return false;
            _components++;
            return true;
        }
        public ref T Get() => ref *_components;
        public void CopyTo<TU>(TU* destination, int len = 0)
            where TU : unmanaged, IComponent
        {
            len = len == 0 ? _count : len;
            if (ComponentType<TU>.Index == ComponentType<T>.Index)
            {
                memcpy(destination, _components, len * sizeof(TU));
            }
        }
    }
        [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T1, T2> : IChunk
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        private T1* _components1;
        private T2* _components2;
        private int _count;
        private int _remaining;
        public int Count => _count;
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var li1 = archetype.GetComponentLocalIndex(ComponentType<T1>.Index);
            _components1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(li1));
            var li2 = archetype.GetComponentLocalIndex(ComponentType<T2>.Index);
            _components2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(li2));
            _count = archetype.count;
            _remaining = _count;
        }
        public bool MoveNext()
        {
            if (--_remaining < 0) return false;
            _components1++;
            _components2++;
            return true;
        }
        public ref T1 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components1;
        }
        public ref T2 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components2;
        }

        public Chunk<T1, T2> Current => this;
        public void CopyTo<TU1>(TU1* destination, int len = 0)
            where TU1 : unmanaged, IComponent
        {
            len = len == 0 ? _count : len;
            if (ComponentType<TU1>.Index == ComponentType<T1>.Index)
            {
                memcpy(destination, _components1, len * UnsafeUtility.SizeOf<TU1>());
            }
            if (ComponentType<TU1>.Index == ComponentType<T2>.Index)
            {
                memcpy(destination, _components2, len * UnsafeUtility.SizeOf<TU1>());
            }
        }
    }
    /// <summary>
    /// Continuous piece of memory
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T1, T2, T3> : IChunk
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        private T1* _components1;
        private T2* _components2;
        private T3* _components3;
        private int _count;
        private int _remaining;
        public int Count => _count;
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var li1 = archetype.GetComponentLocalIndex(ComponentType<T1>.Index);
            _components1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(li1));
            var li2 = archetype.GetComponentLocalIndex(ComponentType<T2>.Index);
            _components2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(li2));
            var li3 = archetype.GetComponentLocalIndex(ComponentType<T3>.Index);
            _components3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(li3));
            _count = archetype.count;
            _remaining = _count;
        }
        public Chunk<T1, T2, T3> Current => this;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (--_remaining < 0) return false;
            _components1++;
            _components2++;
            _components3++;
            return true;
        }
        public ref T1 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components1;
        }
        public ref T2 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components2;
        }
        public ref T3 C3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] 
            get => ref *_components3;
        }
        public void CopyTo<TU1>(TU1* destination, int len = 0)
            where TU1 : unmanaged, IComponent
        {
            len = len == 0 ? _count : len;
            if (ComponentType<TU1>.Index == ComponentType<T1>.Index)
            {
                memcpy(destination, _components1, len * UnsafeUtility.SizeOf<TU1>());
            }
            if (ComponentType<TU1>.Index == ComponentType<T2>.Index)
            {
                memcpy(destination, _components2, len * UnsafeUtility.SizeOf<TU1>());
            }
            if (ComponentType<TU1>.Index == ComponentType<T3>.Index)
            {
                memcpy(destination, _components3, len * UnsafeUtility.SizeOf<TU1>());
            }
        }
    }
    /// <summary>
    /// Continuous piece of memory
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T1, T2, T3, T4> : IChunk
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        private T1* _components1;
        private T2* _components2;
        private T3* _components3;
        private T4* _components4;
        private int _remaining;
        private int _count;
        public int Count => _count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var li1 = archetype.GetComponentLocalIndex(ComponentType<T1>.Index);
            _components1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(li1));
            var li2 = archetype.GetComponentLocalIndex(ComponentType<T2>.Index);
            _components2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(li2));
            var li3 = archetype.GetComponentLocalIndex(ComponentType<T3>.Index);
            _components3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(li3));
            var li4 = archetype.GetComponentLocalIndex(ComponentType<T4>.Index);
            _components4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(li4));
            _count = archetype.count;
            _remaining = archetype.count;
        }
        public Chunk<T1, T2, T3, T4> Current => this;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (--_remaining < 0) return false;
            _components1++;
            _components2++;
            _components3++;
            _components4++;
            return true;
        }

        public ref T1 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components1;
        }
        public ref T2 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components2;
        }
        public ref T3 C3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] 
            get => ref *_components3;
        }
        public ref T4 C4
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components4;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo<TU>(TU* destination, int len = 0)
            where TU : unmanaged, IComponent
        {
            len = len == 0 ? _count : len;
            if (ComponentType<TU>.Index == ComponentType<T1>.Index)
            {
                memcpy(destination, _components1, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T2>.Index)
            {
                memcpy(destination, _components2, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T3>.Index)
            {
                memcpy(destination, _components3, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T4>.Index)
            {
                memcpy(destination, _components4, len * UnsafeUtility.SizeOf<TU>());
            }
        }
    }
    public interface IChunk
    {
        void SetData(ref ArchetypeUnsafe archetype);
    }
}