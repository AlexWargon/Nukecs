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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private Ref<T1> _p1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private Ref<T1> _p1;
        private Ref<T2> _p2;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            if (QueryParamInfo<T2>.IsComponent && !T2IsTag && !T2IsPool) _p2.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (QueryParamInfo<T2>.IsComponent && !T2IsTag && !T2IsPool) _p2.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if(QueryParamInfo<T2>.IsComponent)_p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if(QueryParamInfo<T2>.IsComponent)_p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (QueryParamInfo<T3>.IsComponent && !T3IsTag && !T3IsPool) _p3.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (QueryParamInfo<T3>.IsComponent && !T3IsTag && !T3IsPool) _p3.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if(QueryParamInfo<T3>.IsComponent)_p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if(QueryParamInfo<T3>.IsComponent)_p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;
        private Ref<T4> _p4;




        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag && !T4IsPool) _p4.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag && !T4IsPool) _p4.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1.data= (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2.data= (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3.data= (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;

        private Ref<T1> _p1;
        private Ref<T2> _p2;
        private Ref<T3> _p3;
        private Ref<T4> _p4;
        private Ref<T5> _p5;







        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (QueryParamInfo<T5>.IsComponent && !T5IsTag && !T5IsPool) _p5.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (QueryParamInfo<T5>.IsComponent && !T5IsTag && !T5IsPool) _p5.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();

            if (QueryParamInfo<T5>.IsComponent)
                if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
                else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (!T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;

            if (QueryParamInfo<T5>.IsComponent)
                if (!T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        private static readonly bool T6IsTag = QueryParamInfo<T6>.IsComponent && ComponentType<T6>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;
        private static readonly bool T6IsPool = ComponentType<T6>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool6;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;








        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (!T5IsTag && !T5IsPool) _p5.data++;
            if (QueryParamInfo<T6>.IsComponent && !T6IsTag && !T6IsPool) _p6.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (!T5IsTag && !T5IsPool) _p5.data += delta;
            if (QueryParamInfo<T6>.IsComponent && !T6IsTag && !T6IsPool) _p6.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();

            if (QueryParamInfo<T6>.IsComponent)
                if (!T6IsTag && !T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
                else if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (!T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (!T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;

            if (QueryParamInfo<T6>.IsComponent)
                if (!T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        private static readonly bool T6IsTag = QueryParamInfo<T6>.IsComponent && ComponentType<T6>.Data.category == ComponentCategory.Tag;
        private static readonly bool T7IsTag = QueryParamInfo<T7>.IsComponent && ComponentType<T7>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;
        private static readonly bool T6IsPool = ComponentType<T6>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool6;
        private static readonly bool T7IsPool = ComponentType<T7>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool7;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6; private Ref<T7> _p7;









        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (!T5IsTag && !T5IsPool) _p5.data++;
            if (!T6IsTag && !T6IsPool) _p6.data++;
            if (QueryParamInfo<T7>.IsComponent && !T7IsTag && !T7IsPool) _p7.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (!T5IsTag && !T5IsPool) _p5.data += delta;
            if (!T6IsTag && !T6IsPool) _p6.data += delta;
            if (QueryParamInfo<T7>.IsComponent && !T7IsTag && !T7IsPool) _p7.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag && !T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();

            if(QueryParamInfo<T7>.IsComponent)
                if (!T7IsTag && !T7IsPool) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
                else if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (!T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (!T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            if (!T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;

            if (QueryParamInfo<T7>.IsComponent && !T7IsTag) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        private static readonly bool T6IsTag = QueryParamInfo<T6>.IsComponent && ComponentType<T6>.Data.category == ComponentCategory.Tag;
        private static readonly bool T7IsTag = QueryParamInfo<T7>.IsComponent && ComponentType<T7>.Data.category == ComponentCategory.Tag;
        private static readonly bool T8IsTag = QueryParamInfo<T8>.IsComponent && ComponentType<T8>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;
        private static readonly bool T6IsPool = ComponentType<T6>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool6;
        private static readonly bool T7IsPool = ComponentType<T7>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool7;
        private static readonly bool T8IsPool = ComponentType<T8>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool8;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;
        private Ref<T7> _p7; private Ref<T8> _p8;










        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (!T5IsTag && !T5IsPool) _p5.data++;
            if (!T6IsTag && !T6IsPool) _p6.data++;
            if (!T7IsTag && !T7IsPool) _p7.data++;
            if (QueryParamInfo<T8>.IsComponent && !T8IsTag && !T8IsPool) _p8.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (!T5IsTag && !T5IsPool) _p5.data += delta;
            if (!T6IsTag && !T6IsPool) _p6.data += delta;
            if (!T7IsTag && !T7IsPool) _p7.data += delta;
            if (QueryParamInfo<T8>.IsComponent && !T8IsTag && !T8IsPool) _p8.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag && !T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (!T7IsTag && !T7IsPool) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
            else if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();

            if(QueryParamInfo<T8>.IsComponent)
                if (!T8IsTag && !T8IsPool) _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index)));
                else if (T8IsTag) _p8.data = TagSlotStub<T8>.GetPtr();
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (T8IsTag) _p8.data = TagSlotStub<T8>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T8IsPool) _pool8 = archetype.world->GetUntypedPoolPtr(ComponentType<T8>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (!T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (!T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            if (!T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            if (!T7IsPool) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            if (QueryParamInfo<T8>.IsComponent && !T8IsTag) _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (T8IsTag) _p8.data = TagSlotStub<T8>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T8IsPool) _pool8 = archetype.world->GetUntypedPoolPtr(ComponentType<T8>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(*_entities);
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
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        private static readonly bool T6IsTag = QueryParamInfo<T6>.IsComponent && ComponentType<T6>.Data.category == ComponentCategory.Tag;
        private static readonly bool T7IsTag = QueryParamInfo<T7>.IsComponent && ComponentType<T7>.Data.category == ComponentCategory.Tag;
        private static readonly bool T8IsTag = QueryParamInfo<T8>.IsComponent && ComponentType<T8>.Data.category == ComponentCategory.Tag;
        private static readonly bool T9IsTag = QueryParamInfo<T9>.IsComponent && ComponentType<T9>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;
        private static readonly bool T6IsPool = ComponentType<T6>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool6;
        private static readonly bool T7IsPool = ComponentType<T7>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool7;
        private static readonly bool T8IsPool = ComponentType<T8>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool8;
        private static readonly bool T9IsPool = ComponentType<T9>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool9;

        private Ref<T1> _p1; private Ref<T2> _p2; private Ref<T3> _p3;
        private Ref<T4> _p4; private Ref<T5> _p5; private Ref<T6> _p6;
        private Ref<T7> _p7; private Ref<T8> _p8; private Ref<T9> _p9;











        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (!T5IsTag && !T5IsPool) _p5.data++;
            if (!T6IsTag && !T6IsPool) _p6.data++;
            if (!T7IsTag && !T7IsPool) _p7.data++;
            if (!T8IsTag && !T8IsPool) _p8.data++;
            if (QueryParamInfo<T9>.IsComponent && !T9IsTag && !T9IsPool) _p9.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T9IsPool) _p9.data = (T9*)_pool9->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (!T5IsTag && !T5IsPool) _p5.data += delta;
            if (!T6IsTag && !T6IsPool) _p6.data += delta;
            if (!T7IsTag && !T7IsPool) _p7.data += delta;
            if (!T8IsTag && !T8IsPool) _p8.data += delta;
            if (QueryParamInfo<T9>.IsComponent && !T9IsTag && !T9IsPool) _p9.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T9IsPool) _p9.data = (T9*)_pool9->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag && !T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (!T7IsTag && !T7IsPool) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
            else if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (!T8IsTag && !T8IsPool) _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index)));
            else if (T8IsTag) _p8.data = TagSlotStub<T8>.GetPtr();

            if(QueryParamInfo<T9>.IsComponent)
                if (!T9IsTag && !T9IsPool) _p9.data = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T9>.Index)));
                else if (T9IsTag) _p9.data = TagSlotStub<T9>.GetPtr();
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (T8IsTag) _p8.data = TagSlotStub<T8>.GetPtr();
            if (T9IsTag) _p9.data = TagSlotStub<T9>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T8IsPool) _pool8 = archetype.world->GetUntypedPoolPtr(ComponentType<T8>.Index);
            if (T9IsPool) _pool9 = archetype.world->GetUntypedPoolPtr(ComponentType<T9>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T9IsPool) _p9.data = (T9*)_pool9->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (!T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (!T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            if (!T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            if (!T7IsPool) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            if (!T8IsPool) _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index))) + localStart;

            if(QueryParamInfo<T9>.IsComponent)
                if (!T9IsPool) _p9.data = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T9>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (T8IsTag) _p8.data = TagSlotStub<T8>.GetPtr();
            if (T9IsTag) _p9.data = TagSlotStub<T9>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T8IsPool) _pool8 = archetype.world->GetUntypedPoolPtr(ComponentType<T8>.Index);
            if (T9IsPool) _pool9 = archetype.world->GetUntypedPoolPtr(ComponentType<T9>.Index);
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T9IsPool) _p9.data = (T9*)_pool9->UnsafeGetPtr(*_entities);
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
