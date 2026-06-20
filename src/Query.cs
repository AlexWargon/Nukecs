using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        [NativeDisableUnsafePtrRestriction] internal QueryUnsafe* queryUnsafe;
        internal byte worldId;
        internal int id;
        internal int version;
        
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => queryUnsafe->count;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => queryUnsafe->count == 0;
        }

        internal int CountMulti => queryUnsafe->count / queryUnsafe->world->job_worker_count;

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => queryUnsafe != null;
        }

        internal Query(ptr<QueryUnsafe> query)
        {
            queryUnsafe = query.Ptr;
            worldId = query.Ref.world->Id;
            id = query.Ref.Id;
            version = 0;
        }

        internal void FixAfterDeserialize(World world) {
            if (worldId == world.Id && id >= 0) {
                var queries = world.UnsafeWorld->queries;
                if (id < queries.Length)
                    queryUnsafe = queries.Ptr[id].Ptr;
            }
        }

        public Query With<T>(ReadWrite readWrite = ReadWrite.ReadWrite) where T : unmanaged, IComponent
        {
            queryUnsafe->With(ComponentType<T>.Index);
            return this;
        }

        public Query WithArray<T>() where T : unmanaged, IArrayComponent
        {
            queryUnsafe->With(ComponentType<ComponentArray<T>>.Index);
            return this;
        }

        public Query None<T>() where T : unmanaged, IComponent
        {
            queryUnsafe->None(ComponentType<T>.Index);
            return this;
        }

        internal Query With(int componentIndex)
        {
            queryUnsafe->With(componentIndex);
            return this;
        }

        internal Query None(int componentIndex)
        {
            queryUnsafe->None(componentIndex);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity First()
        {
            if (Count > 0)
            {
                var len = queryUnsafe->matchingArchetypes.length;
                var ptr = queryUnsafe->matchingArchetypes.Ptr;
                var arches = queryUnsafe->world->archetypesList.Ptr;
                for (var i = 0; i < len; i++)
                {
                    ref var arch = ref arches[ptr[i]].Ref;
                    if (arches[ptr[i]].Ref.count > 0)
                    {
                        return ref queryUnsafe->world->entities.Ptr[arch.packedEntities.Ptr[0]];
                    }
                }
            }
            throw new Exception("No entities found");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (Entity entity, bool ok) FirstOk()
        {
            return Count > 0
                ? (queryUnsafe->GetEntity(0), true)
                : (Entity.Null, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index)
        {
            return ref queryUnsafe->GetEntity(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityIndex(int index)
        {
            return queryUnsafe->GetEntity(index).id;
        }

        public override string ToString()
        {
            return queryUnsafe->ToString();
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RestoreIfNeed()
        {
            if (version != World.Get(worldId).UnsafeWorldRef.version)
            {
                queryUnsafe = World.Get(worldId).UnsafeWorldRef.queries.ElementAt(id).Ptr;
                //dbug.log("Q RESTORED");
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryEnumerator2 GetEnumerator()
        {
            RestoreIfNeed();
            return new QueryEnumerator2(in queryUnsafe->matchingArchetypes, queryUnsafe->world);
        }
    }


    public unsafe struct QueryUnsafe
    {
        internal DynamicBitmask with;
        internal DynamicBitmask none;

        public MemoryList<int> matchingArchetypes;
        public int matchingArchetypesCount;

        public int count;
        internal ptr<World.WorldUnsafe> worldPtr;
        [NativeDisableUnsafePtrRestriction] public World.WorldUnsafe* world;
        internal ptr<QueryUnsafe> self;

        public int Id;
        internal byte oldVersion;
        internal byte newVersion;

        // Changed<T> storage pointers — resolved in Changed<T>.Setup (non-Burst context).
        // Burst-compiled OnUpdateBatched reads these directly — no managed calls.
        // void* is used for Burst compatibility (NativeContainer pointer cast at point of use).
        [NativeDisableUnsafePtrRestriction] public void* ChangedEntitiesPtr;
        [NativeDisableUnsafePtrRestriction] public void* ChangedOffsetsPtr;
        [NativeDisableUnsafePtrRestriction] public void* ChangedValuesPtr;
        public int ChangedComponentSize;

        public bool IsDirty()
        {
            if (oldVersion != newVersion)
            {
                oldVersion = newVersion;
                return true;
            }
            return false;
        }
        public bool IsCreated => world != null;

        internal void OnDeserialize(ref MemAllocator allocator)
        {
            with.OnDeserialize(ref allocator);
            none.OnDeserialize(ref allocator);
            matchingArchetypes.OnDeserialize(ref allocator);
            self.OnDeserialize(ref allocator);
            worldPtr.OnDeserialize(ref allocator);
            world = worldPtr.Ptr;
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
            this.count = 0;
            this.matchingArchetypes = new MemoryList<int>(16, ref world.Ptr->AllocatorRef);
            this.matchingArchetypesCount = 0;
            this.Id = world.Ptr->queries.Length;
            this.ChangedEntitiesPtr = null;
            this.ChangedOffsetsPtr = null;
            this.ChangedValuesPtr = null;
            this.ChangedComponentSize = 0;

            this.self = self;
            if (withDefaultNoneTypes)
            {
                foreach (var type in world.Ptr->DefaultNoneTypes)
                {
                    none.Add(type);
                }
            }

            newVersion = byte.MinValue;
            oldVersion = byte.MinValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MultiArray<int> GetEntities(Allocator allocator)
        {
            var array = new MultiArray<int>(matchingArchetypes.length, allocator);
            for (var index = 0; index < matchingArchetypes.Length; index++)
            {
                var matchingArchetype = matchingArchetypes[index];
                ref var arch = ref world->archetypesList.ElementAt(matchingArchetype).Ref;
                array.Add(arch.packedEntities.Ptr, arch.count);
            }
            return array;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index)
        {
            var remaining = index;
            for (var i = 0; i < matchingArchetypes.length; i++)
            {
                ref var arch = ref world->archetypesList.Ptr[matchingArchetypes.Ptr[i]].Ref;
                if (remaining < arch.count)
                    return ref world->entities.Ptr[arch.packedEntities.Ptr[remaining]];
                remaining -= arch.count;
            }
            return ref world->entities.Ptr[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityID(int index)
        {
            var remaining = index;
            for (var i = 0; i < matchingArchetypes.length; i++)
            {
                ref var arch = ref world->archetypesList.Ptr[matchingArchetypes.Ptr[i]].Ref;
                if (remaining < arch.count)
                    return arch.packedEntities.Ptr[remaining];
                remaining -= arch.count;
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(int entity)
        {
            count++;
            unchecked
            {
                newVersion++;
            }
        }

        internal void AddArchetype(int archetypeIndex)
        {
            matchingArchetypes.Add(archetypeIndex, ref world->AllocatorRef);
            matchingArchetypesCount++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BatchAdd(int* entityIds, int cnt)
        {
            count += cnt;
            unchecked
            {
                newVersion++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BatchAddRange(int startEntityId, int cnt)
        {
            count += cnt;
            unchecked
            {
                newVersion++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Remove(int entity)
        {
            count--;
            unchecked
            {
                newVersion++;
            }
        }

        internal void SyncVersion()
        {
            oldVersion = newVersion;
        }
        public QueryUnsafe* With(int type)
        {
            with.Add(type);
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
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref struct QueryEnumerator2

    {
        [NativeDisableUnsafePtrRestriction] private readonly int* _arches;
        [NativeDisableUnsafePtrRestriction] private ArchetypeUnsafe* _arch;
        private readonly int _archesLen;
        [NativeDisableUnsafePtrRestriction] private readonly World.WorldUnsafe* _world;
        private int _archIndex;
        private int _remaining;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryEnumerator2(in MemoryList<int> arches, World.WorldUnsafe* world)
        {
            _arches = arches.Ptr;
            _archesLen = arches.Length;
            _world = world;
            _archIndex = -1;
            _remaining = 0;
            _arch = default;
        }

        public ref Entity Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref _world->entities.Ptr[*_arch->packedEntities.Ptr];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining > 0)
            {
                _remaining--;
                return true;
            }

            while (++_archIndex < _archesLen)
            {
                _arch = _world->archetypesList.Ptr[_arches[_archIndex]].Ptr;
                var count = _arch->count;
                if (count <= 0) continue;
                _remaining = count - 1;
                return true;
            }

            return false;
        }
    }
    public unsafe ref struct QueryEnumerator
    {
        private int _lastIndex;
        private int _lastArch;
        private int _archRow;
        private int _countInArch;
        private readonly QueryUnsafe* _query;
        private ArchetypeUnsafe* _currentArchetype;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal QueryEnumerator(QueryUnsafe* queryUnsafe)
        {
            _query = queryUnsafe;
            _lastIndex = -1;
            _lastArch = -1;
            _archRow = 0;
            _countInArch = 0;
            _currentArchetype = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (++_lastIndex >= _query->count) return false;
            if (_lastArch < 0 || ++_archRow >= _countInArch)
            {
                if (++_lastArch >= _query->matchingArchetypes.length) return false;
                var archIndex = _query->matchingArchetypes.Ptr[_lastArch];
                _currentArchetype = _query->world->archetypesList.Ptr[archIndex].Ptr;
                _countInArch = _currentArchetype->count;
                _archRow = 0;
            }
            return true;
        }

        public void Reset()
        {
            _lastIndex = -1;
            _lastArch = -1;
            _archRow = -1;
            _countInArch = 0;
        }

        public ref Entity Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref var e = ref _query->world->entities.Ptr[_currentArchetype->packedEntities.Ptr[_archRow]];
                return ref e;
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Ref<TComponent> where TComponent : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] 
        public TComponent* data;

        public ref TComponent Val
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *data;
        }

        public ref TComponent Get
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *data;
        }

        public readonly ref readonly TComponent Read
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Ref(TComponent* ptr)
        {
            data = ptr;
        }
        public static implicit operator TComponent(Ref<TComponent> r)
        {
            return r.Val;
        }
        public static implicit operator Ref<TComponent>(TComponent r)
        {
            var ptr = (TComponent*)Unsafe.AsPointer(ref r);
            return new Ref<TComponent>(ptr);
        }
    }

    public readonly unsafe struct ReadRef<TComponent> where TComponent : unmanaged, IComponent
    {
        internal readonly int index;
        [NativeDisableUnsafePtrRestriction] internal readonly ComponentPoolUntyped* pool;
        [NativeDisableUnsafePtrRestriction] internal readonly Chunk* chunks;

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

    public enum ReadWrite
    {
        Read,
        Write,
        ReadWrite,
    }
}
