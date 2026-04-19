using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Collections;

    public unsafe struct Query
    {
        [NativeDisableUnsafePtrRestriction] internal QueryUnsafe* InternalPointer;
        internal byte worldId;
        internal int id;
        internal int version;

        public int Count
        {
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
            worldId = query.Ref.world->Id;
            id = query.Ref.Id;
            version = 0;
        }

        public Query With<T>(ReadWrite readWrite = ReadWrite.ReadWrite) where T : unmanaged, IComponent
        {
            InternalPointer->With(ComponentType<T>.Index);
            return this;
        }

        public Query WithArray<T>() where T : unmanaged, IArrayComponent
        {
            InternalPointer->With(ComponentType<ComponentArray<T>>.Index);
            return this;
        }

        public Query None<T>() where T : unmanaged, IComponent
        {
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
        public (Entity entity, bool ok) FirstOk()
        {
            return Count > 0
                ? (InternalPointer->world->entities.Ptr[InternalPointer->entities.Ptr[0]], true)
                : (Entity.Null, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index)
        {
            return ref InternalPointer->world->entities.Ptr[InternalPointer->entities.Ptr[index]];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityIndex(int index)
        {
            return InternalPointer->entities.ElementAt(index);
        }

        public override string ToString()
        {
            return InternalPointer->ToString();
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RestoreIfNeed()
        {
            if (version != World.Get(worldId).UnsafeWorldRef.version)
            {
                InternalPointer = World.Get(worldId).UnsafeWorldRef.queries.ElementAt(id).Ptr;
                dbug.log("Q RESTORED");
                version = World.Get(worldId).UnsafeWorldRef.version;
            }
        }

        public static void RestoreIfNeed(ref QueryUnsafe* query, ref int version, int id, ref World world)
        {
            if (version != world.UnsafeWorldRef.version)
            {
                query = world.UnsafeWorldRef.queries.ElementAt(id).Ptr;
                version = world.UnsafeWorldRef.version;
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryEnumerator GetEnumerator()
        {
            RestoreIfNeed();
            return new QueryEnumerator(InternalPointer);
        }

        public QueryIterator<T1, T2, T3> Iter<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return new QueryIterator<T1, T2, T3>(0, Count, InternalPointer->world);
        }

        public Query ForceUpdate()
        {
            foreach (var ptr in InternalPointer->world->archetypesList)
            {
                ptr.Ref.TryAddQuery(InternalPointer);
            }

            return this;
        }
    }


    public unsafe struct QueryUnsafe
    {
        internal DynamicBitmask with;
        internal DynamicBitmask none;
        internal MemoryArray<int> entities;

        internal MemoryArray<int> entitiesMap;

// #if UNITY_EDITOR
//         internal MemoryList<int> withDebug;
//         internal MemoryList<int> noneDebug;
// #endif
        internal int count;
        internal ptr<World.WorldUnsafe> worldPtr;
        [NativeDisableUnsafePtrRestriction] internal World.WorldUnsafe* world;
        internal ptr<QueryUnsafe> self;

        internal int Id;
        public bool IsCreated => world != null;

        internal void OnDeserialize(ref MemAllocator allocator)
        {
            with.OnDeserialize(ref allocator);
            none.OnDeserialize(ref allocator);
            entities.OnDeserialize(ref allocator);
            entitiesMap.OnDeserialize(ref allocator);
            self.OnDeserialize(ref allocator);
            worldPtr.OnDeserialize(ref allocator);
            world = worldPtr.Ptr;
// #if UNITY_EDITOR
//             withDebug.OnDeserialize(ref allocator);
//             noneDebug.OnDeserialize(ref allocator);
// #endif
        }

        internal static void Free(QueryUnsafe* queryImpl)
        {
            queryImpl->Free();
            queryImpl->world->_free(queryImpl);
        }

        private void Free()
        {
            with.Dispose();
            none.Dispose();
            entities.Dispose();
            entitiesMap.Dispose();
        }

        internal static ptr<QueryUnsafe> CreatePtrRef(ptr<World.WorldUnsafe> world, bool withDefaultNoneTypes = true)
        {
            var ptr = world.Ptr->_allocate_ptr<QueryUnsafe>();
            ptr.Ref = new QueryUnsafe(world, ptr, withDefaultNoneTypes);
            return ptr;
        }

        internal QueryUnsafe(ptr<World.WorldUnsafe> world, ptr<QueryUnsafe> self, bool withDefaultNoneTypes = true)
        {
            this.world = world.Ptr;
            this.worldPtr = world;
            this.with = DynamicBitmask.CreateForComponents(world.Ptr);
            this.none = DynamicBitmask.CreateForComponents(world.Ptr);
// #if UNITY_EDITOR
//             this.withDebug = new MemoryList<int>(16, ref world->AllocatorRef);
//             this.noneDebug = new MemoryList<int>(8, ref world->AllocatorRef);
// #endif
            this.count = 0;
            this.entities =
                new MemoryArray<int>(world.Ptr->config.StartEntitiesAmount, ref world.Ptr->AllocatorRef, clear: true);
            this.entitiesMap =
                new MemoryArray<int>(world.Ptr->config.StartEntitiesAmount, ref world.Ptr->AllocatorRef, clear: true);
            this.Id = world.Ptr->queries.Length;

            this.self = self;
            if (withDefaultNoneTypes)
            {
                foreach (var type in world.Ptr->DefaultNoneTypes)
                {
// #if UNITY_EDITOR
//                     noneDebug.Add(type, ref world->AllocatorRef);
// #endif
                    none.Add(type);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index)
        {
            return ref world->entities.ElementAt(entities.ElementAt(index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityID(int index)
        {
            return entities.ElementAt(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(int entity)
        {
            entities.EnsureCapacity(count + 1, ref world->AllocatorRef);
            entities.ElementAt(count) = entity;
            count++;
            entitiesMap.EnsureCapacity(entity + 1, ref world->AllocatorRef);
            entitiesMap[entity] = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BatchAdd(int* entityIds, int cnt)
        {
            if (cnt <= 0) return;
            entities.EnsureCapacity(count + cnt, ref world->AllocatorRef);
            var maxId = 0;
            for (var i = 0; i < cnt; i++)
            {
                if (entityIds[i] > maxId) maxId = entityIds[i];
            }
            entitiesMap.EnsureCapacity(maxId + 1, ref world->AllocatorRef);
            for (var i = 0; i < cnt; i++)
            {
                entities.ElementAt(count) = entityIds[i];
                count++;
                entitiesMap[entityIds[i]] = count;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BatchAddRange(int startEntityId, int cnt)
        {
            if (cnt <= 0) return;
            var endId = startEntityId + cnt;
            entities.EnsureCapacity(count + cnt, ref world->AllocatorRef);
            entitiesMap.EnsureCapacity(endId + 1, ref world->AllocatorRef);
            for (var i = 0; i < cnt; i++)
            {
                entities.Ptr[count + i] = startEntityId + i;
                entitiesMap.Ptr[startEntityId + i] = count + i + 1;
            }
            count += cnt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Remove(int entity)
        {
            if (entity < 0 || entity >= entitiesMap.Capacity) return;
            var index = entitiesMap[entity] - 1;
            if (index < 0)
            {
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

        public QueryUnsafe* With(int type)
        {
            with.Add(type);
// #if UNITY_EDITOR
//             withDebug.Add(type, ref world->AllocatorRef);
// #endif
            return self.Ptr;
        }

        public bool HasWith(int type)
        {
            return with.Has(type);
        }

        public bool HasNone(int type)
        {
            return none.Has(type);
        }

        public QueryUnsafe* None(int type)
        {
            none.Add(type);
// #if UNITY_EDITOR
//             noneDebug.Add(type, ref world->AllocatorRef);
// #endif
            return self.Ptr;
        }

        [BurstDiscard]
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Query");
            foreach (var typesIndex in ComponentTypeMap.TypesIndexes)
            {
                if (HasWith(typesIndex))
                {
                    sb.Append($".With<{ComponentTypeMap.GetType(typesIndex).Name}>()");
                }

                if (HasNone(typesIndex))
                {
                    sb.Append($".None<{ComponentTypeMap.GetType(typesIndex).Name}>()");
                }
            }

            sb.Append($".Count = {count}");
            return sb.ToString();
        }
    }

    public unsafe ref struct QueryEnumerator
    {
        private int _lastIndex;

        private readonly QueryUnsafe* _query;

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal QueryEnumerator(QueryUnsafe* queryUnsafe)
        {
            _query = queryUnsafe;
            _lastIndex = -1;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _lastIndex++;
            return _query->count > _lastIndex;
        }

        public void Reset()
        {
            _lastIndex = -1;
        }

        public ref Entity Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _query->GetEntity(_lastIndex);
        }
    }

    public unsafe struct Ref<TComponent> where TComponent : unmanaged
    {
        public int index;
        [NativeDisableUnsafePtrRestriction] public ComponentPoolUntyped* pool;
        [NativeDisableUnsafePtrRestriction] internal Chunk* chunks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResolveChunks() { if ((IntPtr)pool != IntPtr.Zero) chunks = pool->Chunks.Ptr; }

        public ref TComponent Val
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Chunk.GetRef<TComponent>(chunks, index);
        }

        public ref TComponent Get
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Chunk.GetRef<TComponent>(chunks, index);
        }

        public TComponent Read
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Chunk.GetRef<TComponent>(chunks, index);
        }
    }


    public readonly unsafe struct ReadRef<TComponent> where TComponent : unmanaged, IComponent
    {
        internal readonly int index;
        [NativeDisableUnsafePtrRestriction] internal readonly unsafe ComponentPoolUntyped* pool;
        [NativeDisableUnsafePtrRestriction] internal readonly unsafe Chunk* chunks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadRef(int index, ref GenericPool pool)
        {
            this.index = index;
            this.pool = pool.UnsafeBufferPtr.Ptr;
            this.chunks = this.pool->Chunks.Ptr;
        }

        public ref readonly TComponent Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Chunk.GetRef<TComponent>(chunks, index);
        }
    }

    public struct QueryTuple<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {

        public int entity;
        public GenericPool pool1;
        public GenericPool pool2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator QueryTuple<T1, T2>((Ref<T1>, Ref<T2>) instance)
        {
            return new QueryTuple<T1, T2>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe implicit operator (Ref<T1>, Ref<T2>)(QueryTuple<T1, T2> queryTuple)
        {
            var ref1 = new Ref<T1>
            {
                pool = queryTuple.pool1.UnsafeBufferPtr.Ptr,
                index = queryTuple.entity
            };
            var ref2 = new Ref<T2>
            {
                pool = queryTuple.pool2.UnsafeBufferPtr.Ptr,
                index = queryTuple.entity
            };
            return (ref1, ref2);
        }
    }

    public unsafe ref struct QueryIterator<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        private int _start;
        private int _end;
        private World.WorldUnsafe* wrld;


        internal QueryIterator(int start, int end, World.WorldUnsafe* world)
        {
            _start = start;
            _end = end;
            wrld = world;
        }

        public IterEnumerator GetEnumerator()
        {
            return new IterEnumerator(0, _end, wrld);
        }

        public ref struct IterEnumerator
        {
            private int _lastIndex;
            private int _end;
            private Ref<T1> c1;
            private Ref<T2> c2;
            private Ref<T3> c3;

            public IterEnumerator(int start, int end, World.WorldUnsafe* world)
            {
                _lastIndex = start - 1;
                _end = end;
                c1 = default;
                c1.pool = world->GetPool<T1>().UnsafeBufferPtr.Ptr;
                c2 = default;
                c2.pool = world->GetPool<T2>().UnsafeBufferPtr.Ptr;
                c3 = default;
                c3.pool = world->GetPool<T3>().UnsafeBufferPtr.Ptr;
            }

            public bool MoveNext()
            {
                _lastIndex++;
                c1.index = _lastIndex;
                c2.index = _lastIndex;
                c3.index = _lastIndex;
                return _end > _lastIndex;
            }

            public void Reset()
            {
                _lastIndex = -1;
            }

            public (Ref<T1>, Ref<T2>, Ref<T3>) Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (c1, c2, c3);
            }
        }
    }

    public enum ReadWrite
    {
        Read,
        Write,
        ReadWrite,
    }
}
