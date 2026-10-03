using System;
using System.Runtime.CompilerServices;
namespace Wargon.Nukecs.Tests.RuntimeDispatch
{
    // Handwritten foreach binding. No generated enumerator is used in this namespace.
    public static unsafe class RuntimeDispatchQueryExtensions
    {
        public static RuntimeDispatchIter<RuntimeDispatchRefs<T1,T2,T3,T4>> GetEnumerator<T1,T2,T3,T4>(this Query<T1,T2,T3,T4> query)
            where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimeDispatchIter<RuntimeDispatchRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimeDispatchIter<RuntimeDispatchRefs<T1,T2,T3,T4>> GetEnumerator<T1,T2,T3,T4,TFilter>(this Query<T1,T2,T3,T4,TFilter> query)
            where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent where TFilter : unmanaged
        {
            if (QueryParamInfo<TFilter>.IsComponent && ComponentType<TFilter>.Data.category != ComponentCategory.Tag)
                throw new InvalidOperationException("The fifth parameter must be a filter or tag.");
            query.TryGetQuery(out var raw);
            return new RuntimeDispatchIter<RuntimeDispatchRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
    }
    public unsafe interface IRuntimeDispatchTuple : IRuntimeDenseTuple
    {
        int Init(ref RuntimePageRows columns, World.WorldUnsafe* world);
        bool UsesPools();
        void GatherInline(ref RuntimePageRows columns,int row);
        void SetArchetype(ref ArchetypeUnsafe arch);
        void Gather(ref RuntimePageRows columns, int row);
    }
    public unsafe struct RuntimeDispatchRefs<T1,T2,T3,T4> : IRuntimeDispatchTuple
        where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent
    {
        // Storage category is a property of the component type, not the current world.
        // Mono can specialize these readonly flags after the closed tuple type is initialized.
        private static readonly bool Pool0 = ComponentType<T1>.Data.category == ComponentCategory.Pool;
        private static readonly bool Pool1 = ComponentType<T2>.Data.category == ComponentCategory.Pool;
        private static readonly bool Pool2 = ComponentType<T3>.Data.category == ComponentCategory.Pool;
        private static readonly bool Pool3 = ComponentType<T4>.Data.category == ComponentCategory.Pool;
        private static readonly bool HasPools = Pool0 | Pool1 | Pool2 | Pool3;
        // Inline tuples retain compact relative offsets; pooled tuples store four addresses.
        // The representation is fixed per closed tuple type and Current stays 32 bytes.
        private T1* a;
        private T2* b;
        private T3* c;
        private T4* d;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPools()=>HasPools;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GatherInline(ref RuntimePageRows columns,int row)
        {
            Set(columns.A.Base+row*sizeof(T1),columns.B.Base+row*sizeof(T2),
                columns.C.Base+row*sizeof(T3),columns.D.Base+row*sizeof(T4));
        }
        public int Init(ref RuntimePageRows columns, World.WorldUnsafe* world)
        {
            var mask=(columns.A.Init<T1>(world)?1:0) | (columns.B.Init<T2>(world)?2:0) |
                     (columns.C.Init<T3>(world)?4:0) | (columns.D.Init<T4>(world)?8:0);
            columns.HasPools=mask!=0;
            if(((Pool0?1:0)|(Pool1?2:0)|(Pool2?4:0)|(Pool3?8:0)) != mask)
                throw new InvalidOperationException("Component storage category changed after tuple initialization.");
            return mask;
        }
        public void SetStorage(ref StorageArchetype storage)
        {
            var offset=storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T1>.Index));
            a=(T1*)(storage.data.Ptr+offset);
            b=(T2*)(storage.data.Ptr+storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T2>.Index)));
            c=(T3*)(storage.data.Ptr+storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T3>.Index)));
            d=(T4*)(storage.data.Ptr+storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T4>.Index)));
            Set((byte*)a,(byte*)b,(byte*)c,(byte*)d);
        }
        public void SetArchetype(ref ArchetypeUnsafe arch)
        {
            var offset=arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index));
            a=(T1*)(arch.data.Ptr+offset);
            b=(T2*)(arch.data.Ptr+arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)));
            c=(T3*)(arch.data.Ptr+arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T3>.Index)));
            d=(T4*)(arch.data.Ptr+arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T4>.Index)));
            Set((byte*)a,(byte*)b,(byte*)c,(byte*)d);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetEnd(int count)=>(byte*)(a+count);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Advance(byte* end)
        {
            a++;
            if(HasPools) { b++; c++; d++; }
            else
            {
                if(sizeof(T2)!=sizeof(T1)) b=(T2*)((byte*)b+sizeof(T2)-sizeof(T1));
                if(sizeof(T3)!=sizeof(T1)) c=(T3*)((byte*)c+sizeof(T3)-sizeof(T1));
                if(sizeof(T4)!=sizeof(T1)) d=(T4*)((byte*)d+sizeof(T4)-sizeof(T1));
            }
            return (byte*)a<end;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Set(byte* p0,byte* p1,byte* p2,byte* p3)
        {
            a=(T1*)p0;
            if(HasPools) { b=(T2*)p1; c=(T3*)p2; d=(T4*)p3; }
            else { b=(T2*)(p1-p0); c=(T3*)(p2-p0); d=(T4*)(p3-p0); }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref RuntimePageRows columns,int row)
        {
            var slot=0;
            if(HasPools)
            {
                var entity=columns.Entities[row];
                var page=entity>>Chunk.CHUNK_INDEX_BITSFIFT;
                slot=entity&(Chunk.MAX_CHUNK_SIZE-1);
                if(page!=columns.PageIndex) columns.LoadPage(page);
            }
            Set(columns.A.Base+(Pool0?slot:row)*sizeof(T1),columns.B.Base+(Pool1?slot:row)*sizeof(T2),
                columns.C.Base+(Pool2?slot:row)*sizeof(T3),columns.D.Base+(Pool3?slot:row)*sizeof(T4));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> p1,out Ref<T2> p2,out Ref<T3> p3,out Ref<T4> p4)
        {
            p1.data=a;
            if(HasPools) { p2.data=b; p3.data=c; p4.data=d; }
            else { p2.data=(T2*)((byte*)a+(long)b); p3.data=(T3*)((byte*)a+(long)c); p4.data=(T4*)((byte*)a+(long)d); }
        }
    }
    public unsafe ref struct RuntimeDispatchIter<TTuple> where TTuple : unmanaged,IRuntimeDispatchTuple
    {
        private TTuple current;
        private RuntimePageRows columns;
        private readonly World.WorldUnsafe* world;
        private readonly int* matches;
        private readonly int matchCount;
        private readonly bool storageMode;
        private readonly int mask;
        private int block,rowIndex,rowCount;
        private int* rows;
        private byte* end;
        private bool dense;
        public RuntimeDispatchIter(QueryUnsafe* query)
        {
            current=default; columns=default; world=query->world;
            mask=current.Init(ref columns,world);
            storageMode=mask==0 && query->TryUseStorageIteration();
            var list=storageMode?query->GetMatchingStorages():query->matchingArchetypes;
            matches=list.Ptr; matchCount=list.Length;
            block=-1; rowIndex=rowCount=0; rows=null; end=null; dense=mask==0;
        }
        public RuntimeDispatchIter<TTuple> GetEnumerator()=>this;
        public readonly TTuple Current { [MethodImpl(MethodImplOptions.AggressiveInlining)] get=>current; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if(!current.UsesPools()) return current.Advance(end)||MoveNextInlineSparse();
            if(rowIndex>=rowCount) return NextBlock();
            var row=rows!=null?rows[rowIndex]:rowIndex;
            current.Gather(ref columns,row);
            rowIndex++;
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MoveNextInlineSparse()
        {
            if(rowIndex>=rowCount) return NextBlock();
            current.GatherInline(ref columns,rows!=null?rows[rowIndex]:rowIndex);
            rowIndex++;
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool NextBlock()
        {
            while(++block<matchCount)
            {
                if(storageMode)
                {
                    ref var storage=ref world->storagesList.Ptr[matches[block]].Ref;
                    if(storage.count==0) continue;
                    current.SetStorage(ref storage); end=current.GetEnd(storage.count); dense=true;
                    return true;
                }
                ref var arch=ref world->archetypesList.Ptr[matches[block]].Ref;
                var count=arch.count;
                if(count==0) continue;
                dense=mask==0 && arch.RowsAreDense;
                if(dense)
                {
                    current.SetArchetype(ref arch); end=current.GetEnd(count);
                    return true;
                }
                columns.SetArchetype(ref arch);
                end=null;
                rows=arch.RowsAreDense?null:arch.rows.Ptr;
                rowCount=count; rowIndex=1;
                current.Gather(ref columns,rows!=null?rows[0]:0);
                return true;
            }
            end=null; rowIndex=rowCount;
            return false;
        }
    }
}
