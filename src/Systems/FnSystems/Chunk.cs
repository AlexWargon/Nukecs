// ReSharper disable UnusedMember.Global

namespace Wargon.Nukecs
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using static UnsafeStatic;
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T> : IChunk where T : unmanaged
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
            where TU : unmanaged
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
        where T1 : unmanaged
        where T2 : unmanaged
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
            where TU1 : unmanaged
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
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
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
            where TU1 : unmanaged
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
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
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
            where TU : unmanaged
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
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T1, T2, T3, T4, T5> : IChunk
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        private T1* _components1;
        private T2* _components2;
        private T3* _components3;
        private T4* _components4;
        private T5* _components5;
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
            var li5 = archetype.GetComponentLocalIndex(ComponentType<T5>.Index);
            _components5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(li5));
            _count = archetype.count;
            _remaining = archetype.count;
        }
        public Chunk<T1, T2, T3, T4, T5> Current => this;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (--_remaining < 0) return false;
            _components1++;
            _components2++;
            _components3++;
            _components4++;
            _components5++;
            return true;
        }

        public ref T1 C0
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components1;
        }
        public ref T2 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components2;
        }
        public ref T3 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] 
            get => ref *_components3;
        }
        public ref T4 C3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components4;
        }
        public ref T5 C4
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components5;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo<TU>(TU* destination, int len = 0)
            where TU : unmanaged
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
            if (ComponentType<TU>.Index == ComponentType<T5>.Index)
            {
                memcpy(destination, _components5, len * UnsafeUtility.SizeOf<TU>());
            }
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T1, T2, T3, T4, T5, T6> : IChunk
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
    {
        private T1* _components0;
        private T2* _components1;
        private T3* _components2;
        private T4* _components3;
        private T5* _components4;
        private T6* _components5;
        private int _remaining;
        private int _count;
        public int Count => _count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var li1 = archetype.GetComponentLocalIndex(ComponentType<T1>.Index);
            _components0 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(li1));
            var li2 = archetype.GetComponentLocalIndex(ComponentType<T2>.Index);
            _components1 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(li2));
            var li3 = archetype.GetComponentLocalIndex(ComponentType<T3>.Index);
            _components2 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(li3));
            var li4 = archetype.GetComponentLocalIndex(ComponentType<T4>.Index);
            _components3 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(li4));
            var li5 = archetype.GetComponentLocalIndex(ComponentType<T5>.Index);
            _components4 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(li5));
            var li6 = archetype.GetComponentLocalIndex(ComponentType<T6>.Index);
            _components5 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(li6));
            _count = archetype.count;
            _remaining = archetype.count;
        }
        public Chunk<T1, T2, T3, T4, T5, T6> Current => this;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (--_remaining < 0) return false;
            _components0++;
            _components1++;
            _components2++;
            _components3++;
            _components4++;
            _components5++;
            return true;
        }

        public ref T1 C0
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components0;
        }
        public ref T2 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components1;
        }
        public ref T3 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] 
            get => ref *_components2;
        }
        public ref T4 C3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components3;
        }
        public ref T5 C4
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components4;
        }
        public ref T6 C5
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components5;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo<TU>(TU* destination, int len = 0)
            where TU : unmanaged
        {
            len = len == 0 ? _count : len;
            if (ComponentType<TU>.Index == ComponentType<T1>.Index)
            {
                memcpy(destination, _components0, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T2>.Index)
            {
                memcpy(destination, _components1, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T3>.Index)
            {
                memcpy(destination, _components2, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T4>.Index)
            {
                memcpy(destination, _components3, len * UnsafeUtility.SizeOf<TU>());
            }
            if (ComponentType<TU>.Index == ComponentType<T5>.Index)
            {
                memcpy(destination, _components4, len * UnsafeUtility.SizeOf<TU>());
            }
            if (ComponentType<TU>.Index == ComponentType<T6>.Index)
            {
                memcpy(destination, _components5, len * UnsafeUtility.SizeOf<TU>());
            }
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T1, T2, T3, T4, T5, T6, T7> : IChunk
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
        where T7 : unmanaged
    {
        private T1* _components0;
        private T2* _components1;
        private T3* _components2;
        private T4* _components3;
        private T5* _components4;
        private T6* _components5;
        private T7* _components6;
        private int _remaining;
        private int _count;
        public int Count => _count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var li1 = archetype.GetComponentLocalIndex(ComponentType<T1>.Index);
            _components0 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(li1));
            var li2 = archetype.GetComponentLocalIndex(ComponentType<T2>.Index);
            _components1 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(li2));
            var li3 = archetype.GetComponentLocalIndex(ComponentType<T3>.Index);
            _components2 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(li3));
            var li4 = archetype.GetComponentLocalIndex(ComponentType<T4>.Index);
            _components3 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(li4));
            var li5 = archetype.GetComponentLocalIndex(ComponentType<T5>.Index);
            _components4 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(li5));
            var li6 = archetype.GetComponentLocalIndex(ComponentType<T6>.Index);
            _components5 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(li6));
            var li7 = archetype.GetComponentLocalIndex(ComponentType<T7>.Index);
            _components6 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(li7));
            _count = archetype.count;
            _remaining = archetype.count;
        }
        public Chunk<T1, T2, T3, T4, T5, T6, T7> Current => this;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (--_remaining < 0) return false;
            _components0++;
            _components1++;
            _components2++;
            _components3++;
            _components4++;
            _components5++;
            _components6++;
            return true;
        }

        public ref T1 C0
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components0;
        }
        public ref T2 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components1;
        }
        public ref T3 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] 
            get => ref *_components2;
        }
        public ref T4 C3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components3;
        }
        public ref T5 C4
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components4;
        }
        public ref T6 C5
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components5;
        }
        public ref T7 C6
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components6;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo<TU>(TU* destination, int len = 0)
            where TU : unmanaged
        {
            len = len == 0 ? _count : len;
            if (ComponentType<TU>.Index == ComponentType<T1>.Index)
            {
                memcpy(destination, _components0, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T2>.Index)
            {
                memcpy(destination, _components1, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T3>.Index)
            {
                memcpy(destination, _components2, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T4>.Index)
            {
                memcpy(destination, _components3, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T5>.Index)
            {
                memcpy(destination, _components4, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T6>.Index)
            {
                memcpy(destination, _components5, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T6>.Index)
            {
                memcpy(destination, _components5, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T7>.Index)
            {
                memcpy(destination, _components6, len * UnsafeUtility.SizeOf<TU>());
            }
        }
    }
    
        [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Chunk<T1, T2, T3, T4, T5, T6, T7, T8> : IChunk
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
        where T7 : unmanaged
        where T8 : unmanaged
    {
        private T1* _components0;
        private T2* _components1;
        private T3* _components2;
        private T4* _components3;
        private T5* _components4;
        private T6* _components5;
        private T7* _components6;
        private T8* _components7;
        private int _remaining;
        private int _count;
        public int Count => _count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var li1 = archetype.GetComponentLocalIndex(ComponentType<T1>.Index);
            _components0 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(li1));
            var li2 = archetype.GetComponentLocalIndex(ComponentType<T2>.Index);
            _components1 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(li2));
            var li3 = archetype.GetComponentLocalIndex(ComponentType<T3>.Index);
            _components2 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(li3));
            var li4 = archetype.GetComponentLocalIndex(ComponentType<T4>.Index);
            _components3 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(li4));
            var li5 = archetype.GetComponentLocalIndex(ComponentType<T5>.Index);
            _components4 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(li5));
            var li6 = archetype.GetComponentLocalIndex(ComponentType<T6>.Index);
            _components5 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(li6));
            var li7 = archetype.GetComponentLocalIndex(ComponentType<T7>.Index);
            _components6 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(li7));
            var li8 = archetype.GetComponentLocalIndex(ComponentType<T8>.Index);
            _components7 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(li8));
            _count = archetype.count;
            _remaining = archetype.count;
        }
        public Chunk<T1, T2, T3, T4, T5, T6, T7, T8> Current => this;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (--_remaining < 0) return false;
            _components0++;
            _components1++;
            _components2++;
            _components3++;
            _components4++;
            _components5++;
            _components6++;
            _components7++;
            return true;
        }

        public ref T1 C0
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components0;
        }
        public ref T2 C1
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components1;
        }
        public ref T3 C2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] 
            get => ref *_components2;
        }
        public ref T4 C3
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components3;
        }
        public ref T5 C4
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components4;
        }
        public ref T6 C5
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components5;
        }
        public ref T7 C6
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components6;
        }
        public ref T8 C7
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_components7;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo<TU>(TU* destination, int len = 0)
            where TU : unmanaged
        {
            len = len == 0 ? _count : len;
            if (ComponentType<TU>.Index == ComponentType<T1>.Index)
            {
                memcpy(destination, _components0, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T2>.Index)
            {
                memcpy(destination, _components1, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T3>.Index)
            {
                memcpy(destination, _components2, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T4>.Index)
            {
                memcpy(destination, _components3, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T5>.Index)
            {
                memcpy(destination, _components4, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T6>.Index)
            {
                memcpy(destination, _components5, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T6>.Index)
            {
                memcpy(destination, _components5, len * UnsafeUtility.SizeOf<TU>());
                return;
            }
            if (ComponentType<TU>.Index == ComponentType<T7>.Index)
            {
                memcpy(destination, _components6, len * UnsafeUtility.SizeOf<TU>());
            }
        }
    }
    
    public interface IChunk
    {
        void SetData(ref ArchetypeUnsafe archetype);
    }
}