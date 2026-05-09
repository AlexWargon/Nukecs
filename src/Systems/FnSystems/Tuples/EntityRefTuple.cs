using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1> : IComponentEntityTuple
        where T1 : unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;
        private Ref<T1> _p1;
        private static readonly int Type1 = ComponentType<T1>.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1, T2> : IComponentEntityTuple
        where T1 : unmanaged
        where T2 : unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;
        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T2>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++;
            if(IsOptionComponent)_p2.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            if(IsOptionComponent)_p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            if(IsOptionComponent)_p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1, T2, T3> : IComponentEntityTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;
        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly bool IsOptionComponent = QueryParamInfo<T3>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++;
            _p2.data++;
            if(IsOptionComponent)_p3.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            if(IsOptionComponent)_p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            if(IsOptionComponent)_p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1, T2, T3, T4> : IComponentEntityTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;
        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;
        private Ref<T4> _p4;
        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly bool IsType4Component = QueryParamInfo<T4>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++;
            _p2.data++;
            _p3.data++;
            if(IsType4Component) _p4.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            if(IsType4Component) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data= (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data= (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3.data= (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            if(IsType4Component) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
            c4 = _p4;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1, T2, T3, T4, T5> : IComponentEntityTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;

        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;
        private Ref<T4> _p4;
        private Ref<T5> _p5;

        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;

        private static readonly bool IsOptionComponent = QueryParamInfo<T5>.IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++;
            if (IsOptionComponent) _p5.data++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));

            if (IsOptionComponent)
                _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;

            if (IsOptionComponent)
                _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4)
        {
            e = _allEntities[*_entities];
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5)
        {
            e = _allEntities[*_entities];
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5;
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1, T2, T3, T4, T5, T6> : IComponentEntityTuple
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;

        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly int Type6 = ComponentType<T6>.Index;

        private static readonly bool IsOptionComponent = QueryParamInfo<T6>.IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++; _p5.data++;
            if (IsOptionComponent) _p6.data++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));

            if (IsOptionComponent)
                _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;

            if (IsOptionComponent)
                _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5)
        {
            e = _allEntities[*_entities];
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6)
        {
            e = _allEntities[*_entities];
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5; c6 = _p6;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1,T2,T3,T4,T5,T6,T7> : IComponentEntityTuple
        where T1: unmanaged where T2: unmanaged where T3: unmanaged
        where T4: unmanaged where T5: unmanaged where T6: unmanaged
        where T7: unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6; private Ref<T7> _p7;

        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly int Type6 = ComponentType<T6>.Index;
        private static readonly int Type7 = ComponentType<T7>.Index;

        private static readonly bool IsOptionComponent = QueryParamInfo<T7>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++; _p5.data++; _p6.data++;
            if(IsOptionComponent) _p7.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6)));

            if(IsOptionComponent)
                _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6))) + localStart;

            if(IsOptionComponent) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out Ref<T1> c1,out Ref<T2> c2,out Ref<T3> c3,
            out Ref<T4> c4,out Ref<T5> c5,out Ref<T6> c6)
        {
            e = _allEntities[*_entities];
            c1=_p1; c2=_p2; c3=_p3; c4=_p4; c5=_p5; c6=_p6;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out Ref<T1> c1,out Ref<T2> c2,out Ref<T3> c3,
            out Ref<T4> c4,out Ref<T5> c5,out Ref<T6> c6,out Ref<T7> c7)
        {
            e = _allEntities[*_entities];
            c1=_p1; c2=_p2; c3=_p3; c4=_p4; c5=_p5; c6=_p6; c7=_p7;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1,T2,T3,T4,T5,T6,T7,T8> : IComponentEntityTuple
        where T1: unmanaged where T2: unmanaged where T3: unmanaged
        where T4: unmanaged where T5: unmanaged where T6: unmanaged
        where T7: unmanaged where T8: unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;
        private Ref<T7> _p7; private Ref<T8> _p8;

        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly int Type6 = ComponentType<T6>.Index;
        private static readonly int Type7 = ComponentType<T7>.Index;
        private static readonly int Type8 = ComponentType<T8>.Index;

        private static readonly bool IsOptionComponent = QueryParamInfo<T8>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++;
            _p5.data++; _p6.data++; _p7.data++;

            if(IsOptionComponent) _p8.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6)));
            _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7)));

            if(IsOptionComponent)
                _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type8)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6))) + localStart;
            _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7))) + localStart;
            if(IsOptionComponent) _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type8))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out Ref<T1> c1,out Ref<T2> c2,out Ref<T3> c3,out Ref<T4> c4,
            out Ref<T5> c5,out Ref<T6> c6,out Ref<T7> c7)
        {
            e = _allEntities[*_entities];

            c1=_p1; c2=_p2; c3=_p3; c4=_p4;
            c5=_p5; c6=_p6; c7=_p7;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out Ref<T1> c1,out Ref<T2> c2,out Ref<T3> c3,out Ref<T4> c4,
            out Ref<T5> c5,out Ref<T6> c6,out Ref<T7> c7,out Ref<T8> c8)
        {
            e = _allEntities[*_entities];

            c1=_p1; c2=_p2; c3=_p3; c4=_p4;
            c5=_p5; c6=_p6; c7=_p7; c8=_p8;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1,T2,T3,T4,T5,T6,T7,T8,T9> : IComponentEntityTuple
        where T1: unmanaged where T2: unmanaged where T3: unmanaged
        where T4: unmanaged where T5: unmanaged where T6: unmanaged
        where T7: unmanaged where T8: unmanaged where T9: unmanaged
    {
        private int* _entities;
        private Entity* _allEntities;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;
        private Ref<T7> _p7; private Ref<T8> _p8; private Ref<T9> _p9;

        private static readonly int Type1 = ComponentType<T1>.Index;
        private static readonly int Type2 = ComponentType<T2>.Index;
        private static readonly int Type3 = ComponentType<T3>.Index;
        private static readonly int Type4 = ComponentType<T4>.Index;
        private static readonly int Type5 = ComponentType<T5>.Index;
        private static readonly int Type6 = ComponentType<T6>.Index;
        private static readonly int Type7 = ComponentType<T7>.Index;
        private static readonly int Type8 = ComponentType<T8>.Index;
        private static readonly int Type9 = ComponentType<T9>.Index;

        private static readonly bool IsOptionComponent = QueryParamInfo<T9>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++;
            _p5.data++; _p6.data++; _p7.data++; _p8.data++;

            if(IsOptionComponent) _p9.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4)));
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5)));
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6)));
            _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7)));
            _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type8)));

            if(IsOptionComponent)
                _p9.data = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type9)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type1))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type2))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type3))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type4))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type5))) + localStart;
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type6))) + localStart;
            _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type7))) + localStart;
            _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type8))) + localStart;

            if(IsOptionComponent)
                _p9.data = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(Type9))) + localStart;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out Ref<T1> c1,out Ref<T2> c2,out Ref<T3> c3,out Ref<T4> c4,
            out Ref<T5> c5,out Ref<T6> c6,out Ref<T7> c7,out Ref<T8> c8)
        {
            e = _allEntities[*_entities];

            c1=_p1; c2=_p2; c3=_p3; c4=_p4;
            c5=_p5; c6=_p6; c7=_p7; c8=_p8;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out Ref<T1> c1,out Ref<T2> c2,out Ref<T3> c3,out Ref<T4> c4,
            out Ref<T5> c5,out Ref<T6> c6,out Ref<T7> c7,out Ref<T8> c8,out Ref<T9> c9)
        {
            e = _allEntities[*_entities];
            c1=_p1; c2=_p2; c3=_p3; c4=_p4;
            c5=_p5; c6=_p6; c7=_p7; c8=_p8; c9=_p9;
        }
    }
}
