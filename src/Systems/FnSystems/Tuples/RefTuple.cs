using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1> : IComponentTuple
        where T1 : unmanaged
    {
        public Ref<T1> _p1;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            if(QueryParamInfo<T2>.IsComponent)_p2.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            if(QueryParamInfo<T2>.IsComponent) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if(QueryParamInfo<T2>.IsComponent) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            _p2.data++;
            if(QueryParamInfo<T3>.IsComponent)_p3.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            if(QueryParamInfo<T3>.IsComponent) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if(QueryParamInfo<T3>.IsComponent) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            _p2.data++;
            _p3.data++;
            if(QueryParamInfo<T4>.IsComponent) _p4.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            if(QueryParamInfo<T4>.IsComponent) _p4.data = ((T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if(QueryParamInfo<T4>.IsComponent) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            _p2.data++;
            _p3.data++;
            _p4.data++;
            if(QueryParamInfo<T5>.IsComponent) _p5.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            _p4.data = ((T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))));
            if(QueryParamInfo<T5>.IsComponent) _p5.data = ((T5*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if(QueryParamInfo<T5>.IsComponent) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
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
            [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1, T2, T3, T4, T5, T6> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
    {
        public Ref<T1> _p1;
        public Ref<T2> _p2;
        public Ref<T3> _p3;
        public Ref<T4> _p4;
        public Ref<T5> _p5;
        public Ref<T6> _p6;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _p1.data++;
            _p2.data++;
            _p3.data++;
            _p4.data++;
            _p5.data++;
            if(QueryParamInfo<T5>.IsComponent) _p5.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            var ptr = archetype.data.Ptr;
            _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            _p4.data = (T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            _p5.data = (T5*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            if(QueryParamInfo<T6>.IsComponent) _p6.data = (T6*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            if(QueryParamInfo<T6>.IsComponent) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4,
            out Ref<T5> c5,
            out Ref<T6> c6)
        {
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
            c4 = _p4;
            c5 = _p5;
            c6 = _p6;
        }
    }
}