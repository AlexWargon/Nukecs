using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityRefTuple<T1> : IComponentEntityTuple
        where T1 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private Ref<T1> _p1;

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
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
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
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private Ref<T1> _p1;
        private Ref<T2> _p2;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++;
            if(QueryParamInfo<T2>.IsComponent)_p2.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            if(QueryParamInfo<T2>.IsComponent)_p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if(QueryParamInfo<T2>.IsComponent)_p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
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
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++;
            _p2.data++;
            if(QueryParamInfo<T3>.IsComponent)_p3.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            if(QueryParamInfo<T3>.IsComponent)_p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if(QueryParamInfo<T3>.IsComponent)_p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
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
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;
        private Ref<T4> _p4;




        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++;
            _p2.data++;
            _p3.data++;
            if(QueryParamInfo<T4>.IsComponent) _p4.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;
            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            if(QueryParamInfo<T4>.IsComponent) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data= (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data= (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data= (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if(QueryParamInfo<T4>.IsComponent) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
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
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;

        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;
        private Ref<T4> _p4;
        private Ref<T5> _p5;







        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++;
            if (QueryParamInfo<T5>.IsComponent) _p5.data++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));

            if (QueryParamInfo<T5>.IsComponent)
                _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;

            if (QueryParamInfo<T5>.IsComponent)
                _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
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
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;








        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++; _p5.data++;
            if (QueryParamInfo<T6>.IsComponent) _p6.data++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));

            if (QueryParamInfo<T6>.IsComponent)
                _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;

            if (QueryParamInfo<T6>.IsComponent)
                _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
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
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6; private Ref<T7> _p7;









        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++; _p5.data++; _p6.data++;
            if(QueryParamInfo<T7>.IsComponent) _p7.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));

            if(QueryParamInfo<T7>.IsComponent)
                _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;

            if(QueryParamInfo<T7>.IsComponent) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
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
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;
        private Ref<T7> _p7; private Ref<T8> _p8;










        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++;
            _p5.data++; _p6.data++; _p7.data++;

            if(QueryParamInfo<T8>.IsComponent) _p8.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));

            if(QueryParamInfo<T8>.IsComponent)
                _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            if(QueryParamInfo<T8>.IsComponent) _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index))) + localStart;
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
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;
        private Ref<T7> _p7; private Ref<T8> _p8; private Ref<T9> _p9;











        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            _p1.data++; _p2.data++; _p3.data++; _p4.data++;
            _p5.data++; _p6.data++; _p7.data++; _p8.data++;

            if(QueryParamInfo<T9>.IsComponent) _p9.data++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _entities = localEntities;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
            _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index)));

            if(QueryParamInfo<T9>.IsComponent)
                _p9.data = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T9>.Index)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index))) + localStart;

            if(QueryParamInfo<T9>.IsComponent)
                _p9.data = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T9>.Index))) + localStart;
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
