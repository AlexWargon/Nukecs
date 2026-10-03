using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1> : IComponentTuple
        where T1 : unmanaged
    {
        public Ref<T1> _p1;
        
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
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
        
        private int _curRow;
        private static readonly bool T1IsTag = ComponentType<T1>.Data.category == ComponentCategory.Tag;
        private static readonly bool T2IsTag = QueryParamInfo<T2>.IsComponent && ComponentType<T2>.Data.category == ComponentCategory.Tag;
        private static readonly bool T1IsPool = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool1;
        private static readonly bool T2IsPool = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private GenericPool* _pool2;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            if (QueryParamInfo<T2>.IsComponent && !T2IsTag && !T2IsPool) _p2.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (QueryParamInfo<T2>.IsComponent && !T2IsTag && !T2IsPool) _p2.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (QueryParamInfo<T2>.IsComponent && !T2IsTag) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (QueryParamInfo<T2>.IsComponent && !T2IsTag) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[localStart]);
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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (QueryParamInfo<T3>.IsComponent && !T3IsTag && !T3IsPool) _p3.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[_curRow]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (QueryParamInfo<T3>.IsComponent && !T3IsTag && !T3IsPool) _p3.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[row]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (QueryParamInfo<T3>.IsComponent && !T3IsTag) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[0]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (QueryParamInfo<T3>.IsComponent && !T3IsTag) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[localStart]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[localStart]);
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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag && !T4IsPool) _p4.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[_curRow]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[_curRow]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag && !T4IsPool) _p4.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[row]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[row]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag) _p4.data = ((T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))));
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[0]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[0]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (QueryParamInfo<T4>.IsComponent && !T4IsTag) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (T1IsPool) _pool1 = archetype.world->GetUntypedPoolPtr(ComponentType<T1>.Index);
            if (T2IsPool) _pool2 = archetype.world->GetUntypedPoolPtr(ComponentType<T2>.Index);
            if (T3IsPool) _pool3 = archetype.world->GetUntypedPoolPtr(ComponentType<T3>.Index);
            if (T4IsPool) _pool4 = archetype.world->GetUntypedPoolPtr(ComponentType<T4>.Index);
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[localStart]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[localStart]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[localStart]);
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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (QueryParamInfo<T5>.IsComponent && !T5IsTag && !T5IsPool) _p5.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[_curRow]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[_curRow]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[_curRow]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (QueryParamInfo<T5>.IsComponent && !T5IsTag && !T5IsPool) _p5.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[row]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[row]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[row]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            _p4.data = ((T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))));
            if (QueryParamInfo<T5>.IsComponent && !T5IsTag) _p5.data = ((T5*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))));
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[0]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[0]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[0]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (!T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (QueryParamInfo<T5>.IsComponent && !T5IsTag) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[localStart]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[localStart]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[localStart]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[localStart]);
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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (!T5IsTag && !T5IsPool) _p5.data++;
            if (QueryParamInfo<T6>.IsComponent && !T6IsTag && !T6IsPool) _p6.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[_curRow]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[_curRow]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[_curRow]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[_curRow]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (!T5IsTag && !T5IsPool) _p5.data += delta;
            if (QueryParamInfo<T6>.IsComponent && !T6IsTag && !T6IsPool) _p6.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[row]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[row]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[row]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[row]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (QueryParamInfo<T6>.IsComponent && !T6IsTag) _p6.data = (T6*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[0]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[0]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[0]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[0]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (!T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (!T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            if (QueryParamInfo<T6>.IsComponent && !T6IsTag) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[localStart]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[localStart]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[localStart]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[localStart]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[localStart]);
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
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1, T2, T3, T4, T5, T6, T7> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
        where T7 : unmanaged
    {
        public Ref<T1> _p1;
        public Ref<T2> _p2;
        public Ref<T3> _p3;
        public Ref<T4> _p4;
        public Ref<T5> _p5;
        public Ref<T6> _p6;
        public Ref<T7> _p7;
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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (!T5IsTag && !T5IsPool) _p5.data++;
            if (!T6IsTag && !T6IsPool) _p6.data++;
            if (QueryParamInfo<T7>.IsComponent && !T7IsTag && !T7IsPool) _p7.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[_curRow]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[_curRow]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[_curRow]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[_curRow]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[_curRow]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (!T5IsTag && !T5IsPool) _p5.data += delta;
            if (!T6IsTag && !T6IsPool) _p6.data += delta;
            if (QueryParamInfo<T7>.IsComponent && !T7IsTag && !T7IsPool) _p7.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[row]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[row]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[row]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[row]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[row]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag && !T6IsPool) _p6.data = (T6*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (QueryParamInfo<T7>.IsComponent && !T7IsTag) _p7.data = (T7*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[0]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[0]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[0]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[0]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[0]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[localStart]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[localStart]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[localStart]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[localStart]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[localStart]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[localStart]);
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
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5; c6 = _p6;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4,
            out Ref<T5> c5,
            out Ref<T6> c6,
            out Ref<T7> c7)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5; c6 = _p6; c7 = _p7;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1, T2, T3, T4, T5, T6, T7, T8> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
        where T7 : unmanaged
        where T8 : unmanaged
    {
        public Ref<T1> _p1;
        public Ref<T2> _p2;
        public Ref<T3> _p3;
        public Ref<T4> _p4;
        public Ref<T5> _p5;
        public Ref<T6> _p6;
        public Ref<T7> _p7;
        public Ref<T8> _p8;
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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (!T5IsTag && !T5IsPool) _p5.data++;
            if (!T6IsTag && !T6IsPool) _p6.data++;
            if (!T7IsTag && !T7IsPool) _p7.data++;
            if (QueryParamInfo<T8>.IsComponent && !T8IsTag && !T8IsPool) _p8.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[_curRow]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[_curRow]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[_curRow]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[_curRow]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[_curRow]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[_curRow]);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (!T5IsTag && !T5IsPool) _p5.data += delta;
            if (!T6IsTag && !T6IsPool) _p6.data += delta;
            if (!T7IsTag && !T7IsPool) _p7.data += delta;
            if (QueryParamInfo<T8>.IsComponent && !T8IsTag && !T8IsPool) _p8.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[row]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[row]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[row]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[row]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[row]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[row]);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag && !T6IsPool) _p6.data = (T6*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (!T7IsTag && !T7IsPool) _p7.data = (T7*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
            else if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (QueryParamInfo<T8>.IsComponent && !T8IsTag) _p8.data = (T8*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index)));
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[0]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[0]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[0]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[0]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[0]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[0]);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[localStart]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[localStart]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[localStart]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[localStart]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[localStart]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[localStart]);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(_packed[localStart]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4,
            out Ref<T5> c5,
            out Ref<T6> c6,
            out Ref<T7> c7)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5; c6 = _p6; c7 = _p7;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4,
            out Ref<T5> c5,
            out Ref<T6> c6,
            out Ref<T7> c7,
            out Ref<T8> c8)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5; c6 = _p6; c7 = _p7; c8 = _p8;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, T9> : IComponentTuple
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where T6 : unmanaged
        where T7 : unmanaged
        where T8 : unmanaged
        where T9 : unmanaged
    {
        public Ref<T1> _p1;
        public Ref<T2> _p2;
        public Ref<T3> _p3;
        public Ref<T4> _p4;
        public Ref<T5> _p5;
        public Ref<T6> _p6;
        public Ref<T7> _p7;
        public Ref<T8> _p8;
        public Ref<T9> _p9;
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
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] private int* _packed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add()
        {
            if (!T1IsTag && !T1IsPool) _p1.data++;
            if (!T2IsTag && !T2IsPool) _p2.data++;
            if (!T3IsTag && !T3IsPool) _p3.data++;
            if (!T4IsTag && !T4IsPool) _p4.data++;
            if (!T5IsTag && !T5IsPool) _p5.data++;
            if (!T6IsTag && !T6IsPool) _p6.data++;
            if (!T7IsTag && !T7IsPool) _p7.data++;
            if (!T8IsTag && !T8IsPool) _p8.data++;
            if (QueryParamInfo<T9>.IsComponent && !T9IsTag && !T9IsPool) _p9.data++;
            _curRow++;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[_curRow]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[_curRow]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[_curRow]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[_curRow]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[_curRow]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[_curRow]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[_curRow]);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(_packed[_curRow]);
            if (T9IsPool) _p9.data = (T9*)_pool9->UnsafeGetPtr(_packed[_curRow]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceTo(int row)
        {
            var delta = row - _curRow;
            if (delta == 0) return;
            _curRow = row;
            if (!T1IsTag && !T1IsPool) _p1.data += delta;
            if (!T2IsTag && !T2IsPool) _p2.data += delta;
            if (!T3IsTag && !T3IsPool) _p3.data += delta;
            if (!T4IsTag && !T4IsPool) _p4.data += delta;
            if (!T5IsTag && !T5IsPool) _p5.data += delta;
            if (!T6IsTag && !T6IsPool) _p6.data += delta;
            if (!T7IsTag && !T7IsPool) _p7.data += delta;
            if (!T8IsTag && !T8IsPool) _p8.data += delta;
            if (QueryParamInfo<T9>.IsComponent && !T9IsTag && !T9IsPool) _p9.data += delta;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[row]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[row]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[row]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[row]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[row]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[row]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[row]);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(_packed[row]);
            if (T9IsPool) _p9.data = (T9*)_pool9->UnsafeGetPtr(_packed[row]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetData(ref ArchetypeUnsafe archetype)
        {
            _curRow = 0;
            var ptr = archetype.data.Ptr;
            if (!T1IsTag && !T1IsPool) _p1.data = (T1*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index)));
            else if (T1IsTag) _p1.data = TagSlotStub<T1>.GetPtr();
            if (!T2IsTag && !T2IsPool) _p2.data = (T2*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)));
            else if (T2IsTag) _p2.data = TagSlotStub<T2>.GetPtr();
            if (!T3IsTag && !T3IsPool) _p3.data = (T3*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)));
            else if (T3IsTag) _p3.data = TagSlotStub<T3>.GetPtr();
            if (!T4IsTag && !T4IsPool) _p4.data = (T4*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)));
            else if (T4IsTag) _p4.data = TagSlotStub<T4>.GetPtr();
            if (!T5IsTag && !T5IsPool) _p5.data = (T5*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index)));
            else if (T5IsTag) _p5.data = TagSlotStub<T5>.GetPtr();
            if (!T6IsTag && !T6IsPool) _p6.data = (T6*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index)));
            else if (T6IsTag) _p6.data = TagSlotStub<T6>.GetPtr();
            if (!T7IsTag && !T7IsPool) _p7.data = (T7*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index)));
            else if (T7IsTag) _p7.data = TagSlotStub<T7>.GetPtr();
            if (!T8IsTag && !T8IsPool) _p8.data = (T8*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index)));
            else if (T8IsTag) _p8.data = TagSlotStub<T8>.GetPtr();
            if (QueryParamInfo<T9>.IsComponent && !T9IsTag) _p9.data = (T9*)(ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T9>.Index)));
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[0]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[0]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[0]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[0]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[0]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[0]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[0]);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(_packed[0]);
            if (T9IsPool) _p9.data = (T9*)_pool9->UnsafeGetPtr(_packed[0]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDataParallel(ref ArchetypeUnsafe archetype, int localStart)
        {
            _curRow = localStart;
            if (!T1IsPool) _p1.data = (T1*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index))) + localStart;
            if (!T2IsPool) _p2.data = (T2*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index))) + localStart;
            if (!T3IsPool) _p3.data = (T3*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index))) + localStart;
            if (!T4IsPool) _p4.data = (T4*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index))) + localStart;
            if (!T5IsPool) _p5.data = (T5*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T5>.Index))) + localStart;
            if (!T6IsPool) _p6.data = (T6*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T6>.Index))) + localStart;
            if (!T7IsPool) _p7.data = (T7*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T7>.Index))) + localStart;
            if (!T8IsPool) _p8.data = (T8*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T8>.Index))) + localStart;
            if (QueryParamInfo<T9>.IsComponent && !T9IsTag) _p9.data = (T9*)(archetype.data.Ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T9>.Index))) + localStart;
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
            _packed = archetype.packedEntities.Ptr;
            if (T1IsPool) _p1.data = (T1*)_pool1->UnsafeGetPtr(_packed[localStart]);
            if (T2IsPool) _p2.data = (T2*)_pool2->UnsafeGetPtr(_packed[localStart]);
            if (T3IsPool) _p3.data = (T3*)_pool3->UnsafeGetPtr(_packed[localStart]);
            if (T4IsPool) _p4.data = (T4*)_pool4->UnsafeGetPtr(_packed[localStart]);
            if (T5IsPool) _p5.data = (T5*)_pool5->UnsafeGetPtr(_packed[localStart]);
            if (T6IsPool) _p6.data = (T6*)_pool6->UnsafeGetPtr(_packed[localStart]);
            if (T7IsPool) _p7.data = (T7*)_pool7->UnsafeGetPtr(_packed[localStart]);
            if (T8IsPool) _p8.data = (T8*)_pool8->UnsafeGetPtr(_packed[localStart]);
            if (T9IsPool) _p9.data = (T9*)_pool9->UnsafeGetPtr(_packed[localStart]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4,
            out Ref<T5> c5,
            out Ref<T6> c6,
            out Ref<T7> c7,
            out Ref<T8> c8)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5; c6 = _p6; c7 = _p7; c8 = _p8;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Deconstruct(
            out Ref<T1> c1,
            out Ref<T2> c2,
            out Ref<T3> c3,
            out Ref<T4> c4,
            out Ref<T5> c5,
            out Ref<T6> c6,
            out Ref<T7> c7,
            out Ref<T8> c8,
            out Ref<T9> c9)
        {
            c1 = _p1; c2 = _p2; c3 = _p3; c4 = _p4; c5 = _p5; c6 = _p6; c7 = _p7; c8 = _p8; c9 = _p9;
        }
    }
}