using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1> : IComponentTuple
        where T1 : unmanaged
    {
        public Ref<T1> _p1;
        private static readonly int Type1 = ComponentType<T1>.Index;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Ref<T1> c1)
        {
            c1 = _p1;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1, T2> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
    {
        public Ref<T1> _p1;
        public Ref<T2> _p2;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T2>.IsComponent;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            if(IsOptionComponent)_p2.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            if(IsOptionComponent) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            if(IsOptionComponent) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Ref<T1> c1)
        {
            c1 = _p1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Ref<T1> c1, out Ref<T2> c2)
        {
            c1 = _p1;
            c2 = _p2;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1, T2, T3> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        public Ref<T1> _p1;
        public Ref<T2> _p2;
        public Ref<T3> _p3;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T3>.IsComponent;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            _p2.data++;
            if(IsOptionComponent)_p3.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            if(IsOptionComponent) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            if(IsOptionComponent) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Ref<T1> c1, out Ref<T2> c2)
        {
            c1 = _p1;
            c2 = _p2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1, T2, T3, T4> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        public Ref<T1> _p1;
        public Ref<T2> _p2;
        public Ref<T3> _p3;
        public Ref<T4> _p4;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T4>.IsComponent;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            _p2.data++;
            _p3.data++;
            if(IsOptionComponent) _p4.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            if(IsOptionComponent) _p4.data = ((T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            if(IsOptionComponent) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
            c4 = _p4;
        }
    }
        [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1, T2, T3, T4, T5> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        public Ref<T1> _p1;
        public Ref<T2> _p2;
        public Ref<T3> _p3;
        public Ref<T4> _p4;
        public Ref<T5> _p5;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T5>.IsComponent;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            _p2.data++;
            _p3.data++;
            _p4.data++;
            if(IsOptionComponent) _p5.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4.data = ((T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))));
            if(IsOptionComponent) _p5.data = ((T5*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            if(IsOptionComponent) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
            c4 = _p4;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4,
            out Ref<T5> c5)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
            c4 = _p4;
            c5 = _p5;
        }
    }
}