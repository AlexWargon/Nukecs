

using UnityEngine;


namespace Wargon.Nukecs {
    
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    
    public readonly unsafe struct Query : IDisposable {
        [NativeDisableUnsafePtrRestriction] internal readonly QueryUnsafe* impl;
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => impl->count;
        }
        internal int CountMulti => impl->count / impl->world->job_worker_count;
        public bool IsValid {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => impl !=null && impl->IsCreated;
        }
        internal Query(World.WorldUnsafe* world, bool withDefaultNoneTypes = true) {
            impl = QueryUnsafe.Create(world, withDefaultNoneTypes);
        }
        internal Query(QueryUnsafe* impl) {
            this.impl = impl;
        }
        public Query With<T>() where T :  unmanaged, IComponent {
            impl->With(ComponentType<T>.Index);
            return this;
        }
        public Query WithArray<T>() where T : unmanaged, IArrayComponent {
            impl->With(ComponentType<ComponentArray<T>>.Index);
            return this;
        }
        public Query None<T>() where T : unmanaged, IComponent {
            impl->None(ComponentType<T>.Index);
            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index) {
            return ref impl->world->entities.ElementAtNoCheck(impl->entities.ElementAtNoCheck(index));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityIndex(int index) {
            return impl->entities.ElementAtNoCheck(index);
        }
        public void Dispose() {
            var allocator = impl->world->allocator;
            UnsafeUtility.Free(impl, allocator);
        }

        public override string ToString() {
            return impl->ToString();
        }

        public QueryEnumerator GetEnumerator() {
            return new QueryEnumerator(impl);
        }
    }
    internal unsafe struct QueryUnsafe {
        internal DynamicBitmask with;
        internal DynamicBitmask none;
        internal UnsafeList<int> entities;
        internal UnsafeParallelHashMap<int, int> entitiesMap;
        internal int count;
        [NativeDisableUnsafePtrRestriction] internal readonly World.WorldUnsafe* world;
        [NativeDisableUnsafePtrRestriction] internal readonly QueryUnsafe* self;
        public bool IsCreated => world != null;
        internal static void Free(QueryUnsafe* queryImpl) {
            queryImpl->Free();
            var allocator = queryImpl->world->allocator;
            UnsafeUtility.Free(queryImpl, allocator);
        }

        private void Free() {
            with.Dispose();
            none.Dispose();
            entities.Dispose();
            entitiesMap.Dispose();
        }

        internal static QueryUnsafe* Create(World.WorldUnsafe* world, bool withDefaultNoneTypes = true) {
            var ptr = Unsafe.Malloc<QueryUnsafe>(world->allocator);
            *ptr = new QueryUnsafe(world, ptr, withDefaultNoneTypes);
            return ptr;
        }

        internal QueryUnsafe(World.WorldUnsafe* world, QueryUnsafe* self, bool withDefaultNoneTypes = true) {
            this.world = world;
            this.with = DynamicBitmask.CreateForComponents();
            this.none = DynamicBitmask.CreateForComponents();
            this.count = 0;
            this.entities = UnsafeHelp.UnsafeListWithMaximumLenght<int>(world->config.StartEntitiesAmount,
                world->allocator, NativeArrayOptions.ClearMemory);
            this.entitiesMap = new UnsafeParallelHashMap<int,int>(world->config.StartEntitiesAmount, world->allocator);
            this.self = self;
            if (withDefaultNoneTypes) {
                foreach (var type in world->DefaultNoneTypes) {
                    none.Add(type);
                }    
            }
            
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index) {
            return ref world->entities.ElementAtNoCheck(entities.ElementAtNoCheck(index));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityID(int index)
        {
            return entities.ElementAtNoCheck(index);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Has(int entity) {
            //if (entitiesMap.m_length <= entity) return false;
            return entitiesMap[entity] > 0;
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity > entities.m_capacity)
            {
                int newCapacity = math.max(entities.m_capacity * 2, requiredCapacity);
                UnsafeHelp.ResizeUnsafeList(ref entities, newCapacity, NativeArrayOptions.ClearMemory);
                //UnsafeHelp.ResizeUnsafeList(ref entitiesMap, newCapacity, NativeArrayOptions.ClearMemory);
            }
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(int entity) 
        {
            //EnsureCapacity(count + 1);
            entities.ElementAtNoCheck(count++) = entity;
            entitiesMap[entity] = count;
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Remove(int entity) {
            var index = entitiesMap[entity] - 1;
            entitiesMap[entity] = 0;
            count--;
            if (count > index) {
                // if(index < 0) return;
                // if(count < 0) return;
                entities[index] = entities[count];
                entitiesMap[entities[index]] = index + 1;
            }
        }

        public QueryUnsafe* With(int type) {
            with.Add(type);
            return self;
        }

        public bool HasWith(int type) {
            return with.Has(type);
        }

        public bool HasNone(int type) {
            return none.Has(type);
        }

        public QueryUnsafe* None(int type) {
            none.Add(type);
            return self;
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.Append($"Query");
            foreach (var typesIndex in ComponentTypeMap.TypesIndexes) {
                if (HasWith(typesIndex)) {
                    sb.Append($".With<{ComponentTypeMap.GetType(typesIndex).Name}>()");
                }

                if (HasNone(typesIndex)) {
                    sb.Append($".None<{ComponentTypeMap.GetType(typesIndex).Name}>()");
                }
            }

            sb.Append($".Count = {count}");
            return sb.ToString();
        }
    }
    public unsafe ref struct QueryEnumerator {
        private int _lastIndex;
        private readonly QueryUnsafe* _query;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal QueryEnumerator(QueryUnsafe* queryUnsafe) {
            _query = queryUnsafe;
            _lastIndex = -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() {
            _lastIndex++;
            return _query->count > _lastIndex;
        }

        public void Reset() {
            _lastIndex = -1;
        }

        public ref Entity Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _query->GetEntity(_lastIndex);
        }
    }
    public unsafe struct Bitmask1024 {
        internal const int BitsPerElement = 64;
        internal const int MaxBits = 1024;
        public const int ArraySize = MaxBits / BitsPerElement;

        internal fixed ulong _bitmaskArray[ArraySize];
        internal int count;

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return count; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int position) {
            if (position < 0 || position >= MaxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 0 and {MaxBits - 1}.");
            }

            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;

            if (!Has(position)) {
                _bitmaskArray[index] |= 1UL << bitPosition;
                count++;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int position) {
            if (position >= MaxBits) {
                return false;
            }

            var index = position / BitsPerElement;
            var bitMask = 1UL << (position % BitsPerElement);

            return (_bitmaskArray[index] & bitMask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int position) {
            if (position < 0 || position >= MaxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 0 and {MaxBits - 1}.");
            }

            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;

            if (Has(position)) {
                _bitmaskArray[index] &= ~(1UL << bitPosition);
                count--;
            }
        }

        public override string ToString() {
            char[] bitChars = new char[ArraySize * BitsPerElement];
            for (int i = 0; i < ArraySize; i++) {
                string bits = Convert.ToString((long) _bitmaskArray[i], 2).PadLeft(BitsPerElement, '0');
                for (int j = 0; j < BitsPerElement; j++) {
                    bitChars[i * BitsPerElement + j] = bits[j];
                }
            }

            Array.Reverse(bitChars);
            return new string(bitChars);
        }
    }

    public static class Bitmask1024Ext {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool Has2(ref this Bitmask1024 bitmask1024, int position) {
            var index = position / Bitmask1024.BitsPerElement;
            var bitPosition = position % Bitmask1024.BitsPerElement;
            return (bitmask1024._bitmaskArray[index] & (1UL << bitPosition)) != 0;
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DynamicBitmask {
        private const int BitsPerUlong = 64;
        [NativeDisableUnsafePtrRestriction] private ulong* bitmaskArray;
        private int count;
        private int maxBits;
        private int arraySize;
        
        public static DynamicBitmask CreateForComponents() {
            return new DynamicBitmask(ComponentAmount.Value.Data);
        }

        public DynamicBitmask(int maxBits) {
            if (maxBits <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBits), $"maxBits in {nameof(DynamicBitmask)} must be greater than zero.");

            this.maxBits = maxBits;
            arraySize = (maxBits + BitsPerUlong - 1) / BitsPerUlong; // Calculate the number of ulong elements needed
            bitmaskArray = (ulong*) UnsafeUtility.MallocTracked(arraySize * sizeof(ulong), UnsafeUtility.AlignOf<ulong>(),
                Allocator.Persistent, 0);
            count = 0;

            // Clear the allocated memory
            ClearBitmask();
        }

        private void ClearBitmask() {
            for (int i = 0; i < arraySize; i++) {
                bitmaskArray[i] = 0;
            }
        }

        // Property to get the count of set bits
        public int Count => count;

        // Method to add an element (set a specific bit)
        public void Add(int position) {
            if (position < 0 || position >= maxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 0 and {maxBits - 1}.");
            }

            int index = position / BitsPerUlong;
            int bitPosition = position % BitsPerUlong;

            if (!Has(position)) {
                bitmaskArray[index] |= 1UL << bitPosition;
                count++;
            }
        }

        // Method to check if an element is present (a specific bit is set)
        public bool Has(int position) {
            if (position < 0 || position >= maxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"{nameof(DynamicBitmask)}: {nameof(position)} must be between 0 and {maxBits - 1}. Position = {position}");
            }

            int index = position / BitsPerUlong;
            int bitPosition = position % BitsPerUlong;

            return (bitmaskArray[index] & (1UL << bitPosition)) != 0;
        }

        // Method to clear an element (unset a specific bit)
        public void Remove(int position) {
            if (position < 0 || position >= maxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"{nameof(DynamicBitmask)}: {nameof(position)} must be between 0 and {maxBits - 1}. ");
            }
            int index = position / BitsPerUlong;
            int bitPosition = position % BitsPerUlong;

            if (Has(position)) {
                bitmaskArray[index] &= ~(1UL << bitPosition);
                count--;
            }
        }

        // Override ToString() to display the bitmask in binary form
        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            for (int i = arraySize - 1; i >= 0; i--) {
                sb.Append(Convert.ToString((long) bitmaskArray[i], 2).PadLeft(BitsPerUlong, '0'));
            }

            return sb.ToString();
        }

        // Copy method to create a deep copy of the DynamicBitmask
        public DynamicBitmask Copy() {
            var copy = new DynamicBitmask(maxBits);
            var byteLength = arraySize * sizeof(ulong);
            UnsafeUtility.MemCpy(bitmaskArray, copy.bitmaskArray, byteLength);
            copy.count = count;
            return copy;
        }

        public DynamicBitmask CopyPlusOne() {
            var copy = new DynamicBitmask(maxBits + 1);
            var byteLength = arraySize * sizeof(ulong);
            UnsafeUtility.MemCpy(bitmaskArray, copy.bitmaskArray, byteLength);
            copy.count = count;
            return copy;
        }

        // Dispose method to release allocated memory
        public void Dispose() {
            UnsafeUtility.FreeTracked(bitmaskArray, Allocator.Persistent);
            bitmaskArray = null;
        }
    }

    public unsafe struct Ref<TComponent> where TComponent : unmanaged, IComponent {
        internal int index;
        internal GenericPool.GenericPoolUnsafe* pool;
        public ref TComponent Value {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref pool->GetRef<TComponent>(index);
        }
    }
    public readonly ref struct Read<TComponent> where TComponent : unmanaged, IComponent {
        internal readonly TComponent Value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Read(ref TComponent value){
            this.Value = value;
        }
    }
    public readonly struct ReadRef<TComponent> where TComponent : unmanaged, IComponent {
        internal readonly int index;
        internal readonly GenericPool pool;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadRef(int index, ref GenericPool pool){
            this.index = index;
            this.pool = pool;
        }
        public unsafe ref readonly TComponent Value {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref ((TComponent*) pool.UnsafeBuffer->buffer)[index];
        }
    }
    
    public struct QueryTuple<T1,T2> 
        where T1: unmanaged, IComponent
        where T2: unmanaged, IComponent {

        public int entity;
        public GenericPool pool1;
        public GenericPool pool2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator QueryTuple<T1,T2>((Ref<T1>,Ref<T2>) instance)
        {
            return new QueryTuple<T1, T2>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static implicit operator (Ref<T1>,Ref<T2>)(QueryTuple<T1,T2> queryTuple) {
            Ref<T1> ref1 = new Ref<T1>() {
                pool = queryTuple.pool1.UnsafeBuffer,
                index = queryTuple.entity
            };
            Ref<T2> ref2 = new Ref<T2>() {
                pool = queryTuple.pool2.UnsafeBuffer,
                index = queryTuple.entity
            };
            return (ref1, ref2);
        }
    }
    public unsafe struct Query<TTuple> where TTuple : struct, ITuple {
        [NativeDisableUnsafePtrRestriction] private readonly QueryUnsafe* _unsafe;
        public int Count => _unsafe->count;
        private void* _queryTuplePtr;
        internal Query(TTuple tuple, World.WorldUnsafe* worldUnsafe) {
            _unsafe = QueryUnsafe.Create(worldUnsafe);
            
            for (int i = 0; i < tuple.Length; i++) {
                var t = tuple[i];
                _unsafe->With(ComponentTypeMap.Index(t.GetType()));
            }

            _queryTuplePtr = null;
        }

        public (Ref<TC1>, Ref<TC2>) Get<TC1, TC2>(int index) 
            where TC1 : unmanaged, IComponent
            where TC2 : unmanaged, IComponent
        {
            if (_queryTuplePtr == null) {
                var ptr = Unsafe.MallocTracked<QueryTuple<TC1, TC2>>(Allocator.Persistent);
                *ptr = new QueryTuple<TC1, TC2> {
                    pool1 = _unsafe->world->GetPool<TC1>(),
                    pool2 = _unsafe->world->GetPool<TC2>()
                };
                _queryTuplePtr = ptr;
            }
            var tuple = (QueryTuple<TC1, TC2>*)_queryTuplePtr;
            tuple->entity = _unsafe->GetEntity(index).id;
            return *tuple;
        }

        public QueryIterator<T1, T2> Iter<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent {
            QueryIterator<T1, T2> iterator = new() {
                _query = this
            };
            return iterator;
        }

        public struct QueryIterator<T1,T2> 
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            internal Query<TTuple> _query;
            public QueryEnumerator GetEnumerator() {
                return new QueryEnumerator(ref _query);
            }
            public ref struct QueryEnumerator {
                private int _lastIndex;
                private Query<TTuple> _query;
                private readonly QueryUnsafe* _queryUnsafe;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal QueryEnumerator(ref Query<TTuple> query) {
                    _query = query;
                    _queryUnsafe = query._unsafe;
                    _lastIndex = -1;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() {
                    _lastIndex++;
                    return _queryUnsafe->count > _lastIndex;
                }

                public void Reset() {
                    _lastIndex = -1;
                    _lastIndex.InRange(1,6);
                }

                public (Ref<T1>, Ref<T2>) Current {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => _query.Get<T1,T2>(_lastIndex);
                }
            }
        }
    }
    public interface IFilter
    {
        
    }
    public struct With<T> : IFilter where T: struct, ITuple { }
    public struct None<T> : IFilter where T: struct, ITuple { }

    public static class Exts {
        public static bool InRange(this int integer, int min, int max) {
            return integer >= min && integer <= max;
        }
    }
}