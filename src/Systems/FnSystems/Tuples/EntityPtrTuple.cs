using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1> : IComponentEntityTuple
        where T1 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        [NativeDisableUnsafePtrRestriction] private T1* _p1;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;
            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            _entities = localEntities;
            _allEntities = globalEntities;
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1, T2> : IComponentEntityTuple
        where T1 : unmanaged
        where T2 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        [NativeDisableUnsafePtrRestriction] private T1* _p1;
        [NativeDisableUnsafePtrRestriction] private T2* _p2;
        private static readonly bool IsOptionComponent = QueryParamInfo<T2>.IsComponent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            if (QueryParamInfo<T2>.IsComponent && !T2IsTag && !T2IsPool) _p2++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (QueryParamInfo<T2>.IsComponent && !T2IsTag && !T2IsPool) _p2 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;
            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if(IsOptionComponent)_p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if(IsOptionComponent)_p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1, out T2* c2)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
        }
    }
    
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1, T2, T3> : IComponentEntityTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        [NativeDisableUnsafePtrRestriction] private T1* _p1;
        [NativeDisableUnsafePtrRestriction] private T2* _p2;
        [NativeDisableUnsafePtrRestriction] private T3* _p3;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            if (!T2IsTag && !T2IsPool) _p2++;
            if (QueryParamInfo<T3>.IsComponent && !T3IsTag && !T3IsPool) _p3++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (!T2IsTag && !T2IsPool) _p2 += delta;
            if (QueryParamInfo<T3>.IsComponent && !T3IsTag && !T3IsPool) _p3 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;
            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (QueryParamInfo<T3>.IsComponent && !T3IsPool && !T3IsTag) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (QueryParamInfo<T3>.IsComponent && !T3IsPool && !T3IsTag) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1, out T2* c2)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1, out T2* c2, out T3* c3)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1, T2, T3, T4> : IComponentEntityTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        [NativeDisableUnsafePtrRestriction] private T1* _p1;
        [NativeDisableUnsafePtrRestriction] private T2* _p2;
        [NativeDisableUnsafePtrRestriction] private T3* _p3;
        [NativeDisableUnsafePtrRestriction] private T4* _p4;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            if (!T2IsTag && !T2IsPool) _p2++;
            if (!T3IsTag && !T3IsPool) _p3++;
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag && !T4IsPool) _p4++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (!T2IsTag && !T2IsPool) _p2 += delta;
            if (!T3IsTag && !T3IsPool) _p3 += delta;
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag && !T4IsPool) _p4 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;
            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (QueryParamInfo<T4>.IsComponent && !T4IsPool && !T4IsTag) _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            _entities = localEntities + localStart;
            _allEntities = globalEntities;
            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (QueryParamInfo<T4>.IsComponent && !T4IsPool && !T4IsTag) _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1, out T2* c2, out T3* c3)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1, out T2* c2, out T3* c3, out T4* c4)
        {
            e = _allEntities[*_entities];
            c1 = _p1;
            c2 = _p2;
            c3 = _p3;
            c4 = _p4;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1, T2, T3, T4, T5> : IComponentEntityTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;

        [NativeDisableUnsafePtrRestriction] private T1* _p1;
        [NativeDisableUnsafePtrRestriction] private T2* _p2;
        [NativeDisableUnsafePtrRestriction] private T3* _p3;
        [NativeDisableUnsafePtrRestriction] private T4* _p4;
        [NativeDisableUnsafePtrRestriction] private T5* _p5;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            if (!T2IsTag && !T2IsPool) _p2++;
            if (!T3IsTag && !T3IsPool) _p3++;
            if (!T4IsTag && !T4IsPool) _p4++;
            if (QueryParamInfo<T5>.IsComponent && !T5IsTag && !T5IsPool) _p5++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (!T2IsTag && !T2IsPool) _p2 += delta;
            if (!T3IsTag && !T3IsPool) _p3 += delta;
            if (!T4IsTag && !T4IsPool) _p4 += delta;
            if (QueryParamInfo<T5>.IsComponent && !T5IsTag && !T5IsPool) _p5 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag) _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();

            if (QueryParamInfo<T5>.IsComponent && !T5IsPool && !T5IsTag) _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;

            if (QueryParamInfo<T5>.IsComponent && !T5IsPool && !T5IsTag) _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1, out T2* c2, out T3* c3, out T4* c4)
        {
            e = _allEntities[*_entities];
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e, out T1* c1, out T2* c2, out T3* c3, out T4* c4, out T5* c5)
        {
            e = _allEntities[*_entities];
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5;
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1, T2, T3, T4, T5, T6> : IComponentEntityTuple
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;
        private static readonly bool T6IsPool = ComponentType<T6>.Data.category == ComponentCategory.Pool;
        private static readonly bool T6IsTag = QueryParamInfo<T6>.IsComponent && ComponentType<T6>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool6;

        [NativeDisableUnsafePtrRestriction] private T1* _p1; 
        [NativeDisableUnsafePtrRestriction] private T2* _p2; 
        [NativeDisableUnsafePtrRestriction] private T3* _p3;
        [NativeDisableUnsafePtrRestriction] private T4* _p4; 
        [NativeDisableUnsafePtrRestriction] private T5* _p5; 
        [NativeDisableUnsafePtrRestriction] private T6* _p6;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            if (!T2IsTag && !T2IsPool) _p2++;
            if (!T3IsTag && !T3IsPool) _p3++;
            if (!T4IsTag && !T4IsPool) _p4++;
            if (!T5IsTag && !T5IsPool) _p5++;
            if (QueryParamInfo<T6>.IsComponent && !T6IsTag && !T6IsPool) _p6++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (!T2IsTag && !T2IsPool) _p2 += delta;
            if (!T3IsTag && !T3IsPool) _p3 += delta;
            if (!T4IsTag && !T4IsPool) _p4 += delta;
            if (!T5IsTag && !T5IsPool) _p5 += delta;
            if (QueryParamInfo<T6>.IsComponent && !T6IsTag && !T6IsPool) _p6 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag) _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag) _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();

            if (QueryParamInfo<T6>.IsComponent && !T6IsPool && !T6IsTag) _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;

            if (QueryParamInfo<T6>.IsComponent && !T6IsPool && !T6IsTag) _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out T1* c1, out T2* c2, out T3* c3, out T4* c4, out T5* c5)
        {
            e = _allEntities[*_entities];
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out T1* c1, out T2* c2, out T3* c3, out T4* c4, out T5* c5, out T6* c6)
        {
            e = _allEntities[*_entities];
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5; c6 = _p6;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1,T2,T3,T4,T5,T6,T7> : IComponentEntityTuple
        where T1: unmanaged where T2: unmanaged where T3: unmanaged
        where T4: unmanaged where T5: unmanaged where T6: unmanaged
        where T7: unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;
        private static readonly bool T6IsPool = ComponentType<T6>.Data.category == ComponentCategory.Pool;
        private static readonly bool T6IsTag = QueryParamInfo<T6>.IsComponent && ComponentType<T6>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool6;
        private static readonly bool T7IsPool = ComponentType<T7>.Data.category == ComponentCategory.Pool;
        private static readonly bool T7IsTag = QueryParamInfo<T7>.IsComponent && ComponentType<T7>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool7;

        [NativeDisableUnsafePtrRestriction] private T1* _p1; 
        [NativeDisableUnsafePtrRestriction] private T2* _p2; 
        [NativeDisableUnsafePtrRestriction] private T3* _p3;
        [NativeDisableUnsafePtrRestriction] private T4* _p4; 
        [NativeDisableUnsafePtrRestriction] private T5* _p5; 
        [NativeDisableUnsafePtrRestriction] private T6* _p6; 
        [NativeDisableUnsafePtrRestriction] private T7* _p7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            if (!T2IsTag && !T2IsPool) _p2++;
            if (!T3IsTag && !T3IsPool) _p3++;
            if (!T4IsTag && !T4IsPool) _p4++;
            if (!T5IsTag && !T5IsPool) _p5++;
            if (!T6IsTag && !T6IsPool) _p6++;
            if (QueryParamInfo<T7>.IsComponent && !T7IsTag && !T7IsPool) _p7++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (!T2IsTag && !T2IsPool) _p2 += delta;
            if (!T3IsTag && !T3IsPool) _p3 += delta;
            if (!T4IsTag && !T4IsPool) _p4 += delta;
            if (!T5IsTag && !T5IsPool) _p5 += delta;
            if (!T6IsTag && !T6IsPool) _p6 += delta;
            if (QueryParamInfo<T7>.IsComponent && !T7IsTag && !T7IsPool) _p7 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag) _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag) _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag) _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();

            if (QueryParamInfo<T7>.IsComponent && !T7IsPool && !T7IsTag) _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7 = TagSlotStub<T7>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;

            if (QueryParamInfo<T7>.IsComponent && !T7IsPool && !T7IsTag) _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7 = TagSlotStub<T7>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out T1* c1,out T2* c2,out T3* c3,
            out T4* c4,out T5* c5,out T6* c6,out T7* c7)
        {
            e = _allEntities[*_entities];
            c1=_p1; c2=_p2; c3=_p3; c4=_p4; c5=_p5; c6=_p6; c7=_p7;
        }
        
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1,T2,T3,T4,T5,T6,T7,T8> : IComponentEntityTuple
        where T1: unmanaged where T2: unmanaged where T3: unmanaged
        where T4: unmanaged where T5: unmanaged where T6: unmanaged
        where T7: unmanaged where T8: unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;
        private static readonly bool T6IsPool = ComponentType<T6>.Data.category == ComponentCategory.Pool;
        private static readonly bool T6IsTag = QueryParamInfo<T6>.IsComponent && ComponentType<T6>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool6;
        private static readonly bool T7IsPool = ComponentType<T7>.Data.category == ComponentCategory.Pool;
        private static readonly bool T7IsTag = QueryParamInfo<T7>.IsComponent && ComponentType<T7>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool7;
        private static readonly bool T8IsPool = ComponentType<T8>.Data.category == ComponentCategory.Pool;
        private static readonly bool T8IsTag = QueryParamInfo<T8>.IsComponent && ComponentType<T8>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool8;

        [NativeDisableUnsafePtrRestriction] private T1* _p1;
        [NativeDisableUnsafePtrRestriction] private T2* _p2; 
        [NativeDisableUnsafePtrRestriction] private T3* _p3;
        [NativeDisableUnsafePtrRestriction] private T4* _p4; 
        [NativeDisableUnsafePtrRestriction] private T5* _p5; 
        [NativeDisableUnsafePtrRestriction] private T6* _p6;
        [NativeDisableUnsafePtrRestriction] private T7* _p7; 
        [NativeDisableUnsafePtrRestriction] private T8* _p8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            if (!T2IsTag && !T2IsPool) _p2++;
            if (!T3IsTag && !T3IsPool) _p3++;
            if (!T4IsTag && !T4IsPool) _p4++;
            if (!T5IsTag && !T5IsPool) _p5++;
            if (!T6IsTag && !T6IsPool) _p6++;
            if (!T7IsTag && !T7IsPool) _p7++;
            if (QueryParamInfo<T8>.IsComponent && !T8IsTag && !T8IsPool) _p8++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8 = (T8*)_pool8->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (!T2IsTag && !T2IsPool) _p2 += delta;
            if (!T3IsTag && !T3IsPool) _p3 += delta;
            if (!T4IsTag && !T4IsPool) _p4 += delta;
            if (!T5IsTag && !T5IsPool) _p5 += delta;
            if (!T6IsTag && !T6IsPool) _p6 += delta;
            if (!T7IsTag && !T7IsPool) _p7 += delta;
            if (QueryParamInfo<T8>.IsComponent && !T8IsTag && !T8IsPool) _p8 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8 = (T8*)_pool8->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag) _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag) _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag) _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
            if (!T7IsTag) _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
            else if (T7IsTag) _p7 = TagSlotStub<T7>.GetPtr();

            if (QueryParamInfo<T8>.IsComponent && !T8IsPool && !T8IsTag) _p8 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index)));
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T8IsPool) _pool8 = archetype.world->GetUntypedPoolPtr(ComponentType<T8>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8 = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7 = TagSlotStub<T7>.GetPtr();
            if (T8IsTag) _p8 = TagSlotStub<T8>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T8IsPool) _pool8 = archetype.world->GetUntypedPoolPtr(ComponentType<T8>.Index);
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            if (QueryParamInfo<T8>.IsComponent && !T8IsPool && !T8IsTag) _p8 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8 = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7 = TagSlotStub<T7>.GetPtr();
            if (T8IsTag) _p8 = TagSlotStub<T8>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out T1* c1,out T2* c2,out T3* c3,out T4* c4,
            out T5* c5,out T6* c6,out T7* c7)
        {
            e = _allEntities[*_entities];

            c1=_p1; c2=_p2; c3=_p3; c4=_p4;
            c5=_p5; c6=_p6; c7=_p7;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out T1* c1,out T2* c2,out T3* c3,out T4* c4,
            out T5* c5,out T6* c6,out T7* c7,out T8* c8)
        {
            e = _allEntities[*_entities];

            c1=_p1; c2=_p2; c3=_p3; c4=_p4;
            c5=_p5; c6=_p6; c7=_p7; c8=_p8;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EntityPtrTuple<T1,T2,T3,T4,T5,T6,T7,T8,T9> : IComponentEntityTuple
        where T1: unmanaged where T2: unmanaged where T3: unmanaged
        where T4: unmanaged where T5: unmanaged where T6: unmanaged
        where T7: unmanaged where T8: unmanaged where T9: unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private int* _entities;
        [NativeDisableUnsafePtrRestriction] private Entity* _allEntities;
        private int _curRow;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        private static readonly bool T3IsPool = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        private static readonly bool T3IsTag = QueryParamInfo<T3>.IsComponent && ComponentType<T3>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool3;
        private static readonly bool T4IsPool = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        private static readonly bool T4IsTag = QueryParamInfo<T4>.IsComponent && ComponentType<T4>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool4;
        private static readonly bool T5IsPool = ComponentType<T5>.Data.category == ComponentCategory.Pool;
        private static readonly bool T5IsTag = QueryParamInfo<T5>.IsComponent && ComponentType<T5>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool5;
        private static readonly bool T6IsPool = ComponentType<T6>.Data.category == ComponentCategory.Pool;
        private static readonly bool T6IsTag = QueryParamInfo<T6>.IsComponent && ComponentType<T6>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool6;
        private static readonly bool T7IsPool = ComponentType<T7>.Data.category == ComponentCategory.Pool;
        private static readonly bool T7IsTag = QueryParamInfo<T7>.IsComponent && ComponentType<T7>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool7;
        private static readonly bool T8IsPool = ComponentType<T8>.Data.category == ComponentCategory.Pool;
        private static readonly bool T8IsTag = QueryParamInfo<T8>.IsComponent && ComponentType<T8>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool8;
        private static readonly bool T9IsPool = ComponentType<T9>.Data.category == ComponentCategory.Pool;
        private static readonly bool T9IsTag = QueryParamInfo<T9>.IsComponent && ComponentType<T9>.Data.category == ComponentCategory.Tag;
        [NativeDisableUnsafePtrRestriction] private GenericPool* _pool9;

        [NativeDisableUnsafePtrRestriction] private T1* _p1; 
        [NativeDisableUnsafePtrRestriction] private T2* _p2; 
        [NativeDisableUnsafePtrRestriction] private T3* _p3;
        [NativeDisableUnsafePtrRestriction] private T4* _p4; 
        [NativeDisableUnsafePtrRestriction] private T5* _p5; 
        [NativeDisableUnsafePtrRestriction] private T6* _p6;
        [NativeDisableUnsafePtrRestriction] private T7* _p7; 
        [NativeDisableUnsafePtrRestriction] private T8* _p8; 
        [NativeDisableUnsafePtrRestriction] private T9* _p9;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            _entities++;
            if (!T1IsPool && !T1IsTag) _p1++;
            if (!T2IsTag && !T2IsPool) _p2++;
            if (!T3IsTag && !T3IsPool) _p3++;
            if (!T4IsTag && !T4IsPool) _p4++;
            if (!T5IsTag && !T5IsPool) _p5++;
            if (!T6IsTag && !T6IsPool) _p6++;
            if (!T7IsTag && !T7IsPool) _p7++;
            if (!T8IsTag && !T8IsPool) _p8++;
            if (QueryParamInfo<T9>.IsComponent && !T9IsTag && !T9IsPool) _p9++;
            _curRow++;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8 = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T9IsPool) _p9 = (T9*)_pool9->UnsafeGetPtr(*_entities);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            _entities += delta;
            if (!T1IsPool && !T1IsTag) _p1 += delta;
            if (!T2IsTag && !T2IsPool) _p2 += delta;
            if (!T3IsTag && !T3IsPool) _p3 += delta;
            if (!T4IsTag && !T4IsPool) _p4 += delta;
            if (!T5IsTag && !T5IsPool) _p5 += delta;
            if (!T6IsTag && !T6IsPool) _p6 += delta;
            if (!T7IsTag && !T7IsPool) _p7 += delta;
            if (!T8IsTag && !T8IsPool) _p8 += delta;
            if (QueryParamInfo<T9>.IsComponent && !T9IsTag && !T9IsPool) _p9 += delta;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8 = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T9IsPool) _p9 = (T9*)_pool9->UnsafeGetPtr(*_entities);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities)
        {
            _curRow = 0;
            _entities = localEntities;
            _allEntities = globalEntities;

            if (!T1IsTag) _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));

            else if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag) _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag) _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag) _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag) _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag) _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
            if (!T7IsTag) _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
            else if (T7IsTag) _p7 = TagSlotStub<T7>.GetPtr();
            if (!T8IsTag) _p8 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index)));
            else if (T8IsTag) _p8 = TagSlotStub<T8>.GetPtr();

            if (QueryParamInfo<T9>.IsComponent && !T9IsPool && !T9IsTag) _p9 = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T9>.Index)));
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T8IsPool) _pool8 = archetype.world->GetUntypedPoolPtr(ComponentType<T8>.Index);
            if (T9IsPool) _pool9 = archetype.world->GetUntypedPoolPtr(ComponentType<T9>.Index);
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8 = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T9IsPool) _p9 = (T9*)_pool9->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7 = TagSlotStub<T7>.GetPtr();
            if (T8IsTag) _p8 = TagSlotStub<T8>.GetPtr();
            if (T9IsTag) _p9 = TagSlotStub<T9>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, int localStart)
        {
            _curRow = localStart;
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            if (T5IsPool) _pool5 = archetype.world->GetUntypedPoolPtr(ComponentType<T5>.Index);
            if (T6IsPool) _pool6 = archetype.world->GetUntypedPoolPtr(ComponentType<T6>.Index);
            if (T7IsPool) _pool7 = archetype.world->GetUntypedPoolPtr(ComponentType<T7>.Index);
            if (T8IsPool) _pool8 = archetype.world->GetUntypedPoolPtr(ComponentType<T8>.Index);
            if (T9IsPool) _pool9 = archetype.world->GetUntypedPoolPtr(ComponentType<T9>.Index);
            _entities = localEntities + localStart;
            _allEntities = globalEntities;

            _p1 = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            _p2 = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            _p3 = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            _p4 = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            _p5 = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            _p6 = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            _p7 = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            if (QueryParamInfo<T9>.IsComponent && !T9IsPool && !T9IsTag) _p8 = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index))) + localStart;
            if (T1IsPool) _p1 = (T1*)_pool1->UnsafeGetPtr(*_entities);
            if (T2IsPool) _p2 = (T2*)_pool2->UnsafeGetPtr(*_entities);
            if (T3IsPool) _p3 = (T3*)_pool3->UnsafeGetPtr(*_entities);
            if (T4IsPool) _p4 = (T4*)_pool4->UnsafeGetPtr(*_entities);
            if (T5IsPool) _p5 = (T5*)_pool5->UnsafeGetPtr(*_entities);
            if (T6IsPool) _p6 = (T6*)_pool6->UnsafeGetPtr(*_entities);
            if (T7IsPool) _p7 = (T7*)_pool7->UnsafeGetPtr(*_entities);
            if (T8IsPool) _p8 = (T8*)_pool8->UnsafeGetPtr(*_entities);
            if (T9IsPool) _p9 = (T9*)_pool9->UnsafeGetPtr(*_entities);
            if (T1IsTag) _p1 = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2 = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3 = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4 = TagSlotStub<T4>.GetPtr();
            if (T5IsTag) _p5 = TagSlotStub<T5>.GetPtr();
            if (T6IsTag) _p6 = TagSlotStub<T6>.GetPtr();
            if (T7IsTag) _p7 = TagSlotStub<T7>.GetPtr();
            if (T8IsTag) _p8 = TagSlotStub<T8>.GetPtr();
            if (T9IsTag) _p9 = TagSlotStub<T9>.GetPtr();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out T1* c1,out T2* c2,out T3* c3,out T4* c4,
            out T5* c5,out T6* c6,out T7* c7,out T8* c8)
        {
            e = _allEntities[*_entities];

            c1=_p1; c2=_p2; c3=_p3; c4=_p4;
            c5=_p5; c6=_p6; c7=_p7; c8=_p8;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(out Entity e,
            out T1* c1,out T2* c2,out T3* c3,out T4* c4,
            out T5* c5,out T6* c6,out T7* c7,out T8* c8,out T9* c9)
        {
            e = _allEntities[*_entities];
            c1=_p1; c2=_p2; c3=_p3; c4=_p4;
            c5=_p5; c6=_p6; c7=_p7; c8=_p8; c9=_p9;
        }
    }
    
    
    public struct T3_1<T1,T2,T3> 
        where T1 : unmanaged, IComponent 
        where T2 : unmanaged, IComponent 
        where T3 : unmanaged,IComponent 
    {
            
    }
    public struct T3_1Pool<T1,T2,T3> 
        where T1 : unmanaged, IPoolComponent 
        where T2 : unmanaged, IPoolComponent 
        where T3 : unmanaged, IPoolComponent 
    {
        
    }
}