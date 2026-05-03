using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
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
}