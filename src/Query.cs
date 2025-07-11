﻿using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs {
    
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    
    public readonly unsafe struct Query {
        [NativeDisableUnsafePtrRestriction]
        internal readonly QueryUnsafe* InternalPointer;
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InternalPointer->count;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InternalPointer->count == 0;
        }
        internal int CountMulti => InternalPointer->count / InternalPointer->world->job_worker_count;
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InternalPointer != null;
        }

        internal Query(ptr<QueryUnsafe> query)
        {
            InternalPointer = query.Ptr;
        }

        public Query With<T>() where T :  unmanaged, IComponent {
            InternalPointer->With(ComponentType<T>.Index);
            return this;
        }
        public Query WithArray<T>() where T : unmanaged, IArrayComponent {
            InternalPointer->With(ComponentType<ComponentArray<T>>.Index);
            return this;
        }
        public Query None<T>() where T : unmanaged, IComponent {
            InternalPointer->None(ComponentType<T>.Index);
            return this;
        }

        internal Query With(int componentIndex)
        {
            InternalPointer->With(componentIndex);
            return this;
        }

        internal Query None(int componentIndex)
        {
            InternalPointer->None(componentIndex);
            return this;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity First()
        {
            if (Count > 0)
            {
                return ref InternalPointer->world->entities.Ptr[InternalPointer->entities.Ptr[0]];
            }
            throw new Exception("No entities found");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index) {
            return ref InternalPointer->world->entities.Ptr[InternalPointer->entities.Ptr[index]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityIndex(int index) {
            return InternalPointer->entities.ElementAt(index);
        }

        public override string ToString() {
            return InternalPointer->ToString();
        }

        public QueryEnumerator GetEnumerator() {
            return new QueryEnumerator(InternalPointer);
        }
    }


    internal unsafe struct QueryUnsafe {
        internal DynamicBitmask with;
        internal DynamicBitmask none;
        internal MemoryList<int> entities;
        internal MemoryList<int> entitiesMap;
#if UNITY_EDITOR
        internal MemoryList<int> withDebug;
        internal MemoryList<int> noneDebug;
#endif
        internal int count;
        [NativeDisableUnsafePtrRestriction] internal readonly World.WorldUnsafe* world;
        [NativeDisableUnsafePtrRestriction] internal readonly QueryUnsafe* self;
        internal int Id;
        public bool IsCreated => world != null;
        internal static void Free(QueryUnsafe* queryImpl) {
            queryImpl->Free();
            queryImpl->world->_free(queryImpl);
        }

        private void Free() {
            with.Dispose();
            none.Dispose();
            entities.Dispose();
            entitiesMap.Dispose();
        }

        internal static QueryUnsafe* Create(World.WorldUnsafe* world, bool withDefaultNoneTypes = true)
        {
            var ptr = world->_allocate<QueryUnsafe>();
            *ptr = new QueryUnsafe(world, ptr, withDefaultNoneTypes);
            return ptr;
        }
        internal static ptr<QueryUnsafe> CreatePtr(World.WorldUnsafe* world, bool withDefaultNoneTypes = true)
        {
            var ptr = world->_allocate_ptr<QueryUnsafe>();
            ptr.Ref = new QueryUnsafe(world, ptr.Ptr, withDefaultNoneTypes);
            return ptr;
        }
        internal static ptr<QueryUnsafe> CreatePtrPtr(World.WorldUnsafe* world, bool withDefaultNoneTypes = true)
        {
            var ptr = world->_allocate_ptr<QueryUnsafe>();
            ptr.Ref = new QueryUnsafe(world, ptr.Ptr, withDefaultNoneTypes);
            return ptr;
        }
        internal QueryUnsafe(World.WorldUnsafe* world, QueryUnsafe* self, bool withDefaultNoneTypes = true) {
            this.world = world;
            this.with = DynamicBitmask.CreateForComponents(world);
            this.none = DynamicBitmask.CreateForComponents(world);
#if UNITY_EDITOR
            this.withDebug = new MemoryList<int>(16, ref world->AllocatorRef);
            this.noneDebug = new MemoryList<int>(8, ref world->AllocatorRef);
#endif
            this.count = 0;
            this.entities = new MemoryList<int>(world->config.StartEntitiesAmount, ref world->AllocatorRef, true);
            this.entitiesMap = new MemoryList<int>(world->config.StartEntitiesAmount, ref world->AllocatorRef, true);
            this.Id = world->queries.Length;
            this.self = self;
            if (withDefaultNoneTypes) {
                foreach (var type in world->DefaultNoneTypes) {
#if UNITY_EDITOR
                    noneDebug.Add(type, ref world->AllocatorRef);
#endif
                    none.Add(type);
                }    
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index) {
            return ref world->entities[entities.ElementAt(index)];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityID(int index)
        {
            return entities.ElementAt(index);
        }

        internal void Add(int entity) 
        {
            entities.ElementAt(count++) = entity;
            entitiesMap[entity] = count;
        }

        internal void Remove(int entity)
        {
            var index = entitiesMap[entity] - 1;
            //dbug.log($"Removing entity {entity} from query {Id}, index = {index}, count = {count}");
            if (index < 0)
            {
                //dbug.error($"Warning: Entity {entity} not found in query {Id}");
                return;
            }
            entitiesMap[entity] = 0;
            count--;
            if (count > index)
            {
                entities[index] = entities[count];
                entitiesMap[entities[index]] = index + 1;
            }
        }
        public QueryUnsafe* With(int type) {
            with.Add(type);
#if UNITY_EDITOR
            withDebug.Add(type, ref world->AllocatorRef);
#endif
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
            #if UNITY_EDITOR
            noneDebug.Add(type, ref world->AllocatorRef);
            #endif
            return self;
        }
        [BurstDiscard]
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

        internal fixed ulong bitmaskArray[ArraySize];
        internal int count;

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
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
                bitmaskArray[index] |= 1UL << bitPosition;
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

            return (bitmaskArray[index] & bitMask) != 0;
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
                bitmaskArray[index] &= ~(1UL << bitPosition);
                count--;
            }
        }

        public override string ToString() {
            char[] bitChars = new char[ArraySize * BitsPerElement];
            for (int i = 0; i < ArraySize; i++) {
                string bits = Convert.ToString((long) bitmaskArray[i], 2).PadLeft(BitsPerElement, '0');
                for (int j = 0; j < BitsPerElement; j++) {
                    bitChars[i * BitsPerElement + j] = bits[j];
                }
            }

            Array.Reverse(bitChars);
            return new string(bitChars);
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
            var ref1 = new Ref<T1> {
                pool = queryTuple.pool1.UnsafeBuffer,
                index = queryTuple.entity
            };
            var ref2 = new Ref<T2> {
                pool = queryTuple.pool2.UnsafeBuffer,
                index = queryTuple.entity
            };
            return (ref1, ref2);
        }
    }

}