using System;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public unsafe struct Archetype
    {
        internal ArchetypeUnsafe* impl => ptr.Ptr;
        internal ptr<ArchetypeUnsafe> ptr;

        internal bool Has<T>() where T : unmanaged
        {
            return impl->Has(ComponentType<T>.Index);
        }

        internal void Refresh()
        {
            impl->queries.Clear();
            impl->PopulateQueries(impl->world);
        }

        public void Dispose()
        {
            //ArchetypeUnsafe.Destroy(impl);
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    internal unsafe struct ArchetypeUnsafe
    {
        internal DynamicBitmask mask;
        internal UnsafeList<int> types;
        [NativeDisableUnsafePtrRestriction] internal World.WorldUnsafe* world;
        internal MemoryList<int> queries;
        internal HashMap<int, ptr<Edge>> transactions;
        internal Edge destroyEdge;
        internal readonly int id;
        internal bool IsCreated => world != null;

        internal void OnDeserialize(ref SerializableMemoryAllocator allocator, Allocator unityAllocator)
        {
            mask.OnDeserialize(ref allocator);
            queries.OnDeserialize(ref allocator);
            transactions.OnDeserialize(ref allocator, unityAllocator);
            foreach (var kvPair in transactions)
            {
                kvPair.Value.OnDeserialize(ref allocator);
                ref var edge = ref kvPair.Value.Ref;
                edge.RemoveEntity.OnDeserialize(ref allocator);
                edge.AddEntity.OnDeserialize(ref allocator);
            }
        }

        private ref QueryUnsafe IdToQueryRef(int qId)
        {
            return ref world->queries.Ptr[qId].Ref;
        }

        private ref ptr<QueryUnsafe> Query(int qId)
        {
            return ref world->queries.Ptr[qId];
        }

        internal static void Destroy(ArchetypeUnsafe* archetype)
        {
            archetype->mask.Dispose();
            archetype->types.Dispose();
            archetype->queries.Dispose();
            foreach (var kvPair in archetype->transactions) kvPair.Value.Ref.Dispose();
            archetype->destroyEdge.Dispose();
            archetype->transactions.Dispose();
            var worldPtr = archetype->world;
            worldPtr->_free(archetype);
            archetype->world = null;
        }

        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, int[] typesSpan = null)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>();
            *ptr.Ptr = new ArchetypeUnsafe(world, typesSpan);
            return ptr;
        }

        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, ref UnsafeList<int> typesSpan,
            bool copyList = false)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>();
            *ptr.Ptr = new ArchetypeUnsafe(world, ref typesSpan, copyList);
            return ptr;
        }

        internal ArchetypeUnsafe(World.WorldUnsafe* world, int[] typesSpan = null)
        {
            this.world = world;
            mask = DynamicBitmask.CreateForComponents(world);
            id = 0;
            if (typesSpan != null)
            {
                types = new UnsafeList<int>(typesSpan.Length, world->Allocator);
                id = GetHashCode(typesSpan);
                foreach (var type in typesSpan)
                {
                    mask.Add(type);
                    types.Add(in type);
                }
            }
            else
            {
                // Root Archetype
                types = new UnsafeList<int>(1, world->Allocator);
            }

            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            transactions = new HashMap<int, ptr<Edge>>(8, ref world->AllocatorHandler);
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
        }

        internal ArchetypeUnsafe(World.WorldUnsafe* world, ref UnsafeList<int> typesSpan, bool copyList = false)
        {
            this.world = world;
            mask = DynamicBitmask.CreateForComponents(world);
            if (typesSpan.IsCreated)
            {
                types = typesSpan;
                foreach (var type in typesSpan) mask.Add(type);
            }
            else
            {
                // Root Archetype
                types = new UnsafeList<int>(1, world->Allocator);
            }

            id = GetHashCode(ref typesSpan);
            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            transactions = new HashMap<int, ptr<Edge>>(8, ref world->AllocatorHandler);
            destroyEdge = default;

            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
            if (copyList)
            {
                types = new UnsafeList<int>(typesSpan.m_length, world->Allocator);
                types.CopyFrom(in typesSpan);
            }
        }

        internal Entity CreateEntity()
        {
            var e = world->CreateEntity(id);
            for (var i = 0; i < queries.Length; i++) IdToQueryRef(queries.Ptr[i]).Add(e.id);
            return e;
        }

        internal void Refresh()
        {
            queries.Clear();
            PopulateQueries(world);
        }

        internal void PopulateQueries(World.WorldUnsafe* world)
        {
            for (var i = 0; i < world->queries.Length; i++)
            {
                var q = world->queries[i];
                var matches = 0;
                var hasNone = false;
                foreach (var type in types)
                {
                    if (q.Ptr->HasNone(type))
                    {
                        hasNone = true;
                        break;
                    }
                }

                if (hasNone) continue;
                foreach (var type in types)
                {
                    if (q.Ptr->HasWith(type))
                    {
                        matches++;
                        if (matches == q.Ptr->with.Count)
                        {
                            queries.Add(q.Ptr->Id, ref this.world->AllocatorRef);
                            break;
                        }
                    }
                }
            }
        }

        private Edge CreateDestroyEdge()
        {
            var edge = new Edge(ref world->AllocatorRef);
            for (var i = 0; i < queries.Length; i++)
                edge.RemoveEntity.Add(in Query(queries.ElementAt(i)), ref world->AllocatorRef);
            return edge;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Copy(in int from, in int to)
        {
            for (var i = 0; i < queries.Length; i++)
            {
                var queryId = queries.ElementAt(i);
                Query(queryId).Ref.Add(to);
            }

            foreach (var type in types)
            {
                ref var pool = ref world->GetUntypedPool(type);
                pool.Copy(from, to);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity Copy(in Entity entity)
        {
            var newEntity = world->CreateEntity(id);
            for (var i = 0; i < queries.Length; i++)
            {
                var queryId = queries.ElementAt(i);
                Query(queryId).Ref.Add(newEntity.id);
            }

            for (var index = 0; index < types.m_length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Copy(entity.id, newEntity.id);
            }

            if (mask.Has(ComponentType<ComponentArray<Child>>.Index))
            {
                ref var pool = ref world->GetPool<ComponentArray<Child>>();
                ref var fromC = ref pool.GetRef<ComponentArray<Child>>(entity.id);
                ref var to = ref pool.GetRef<ComponentArray<Child>>(newEntity.id);

                for (var i = 0; i < fromC.Length; i++)
                {
                    ref var child = ref fromC.ElementAt(i);
                    ref var childNew = ref to.ElementAt(i);
                    childNew.Value = child.Value.Copy();
                    childNew.Value.Get<ChildOf>().Value = newEntity;
                }
            }

            //newEntity.Add<EntityCreated>();
            return newEntity;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnEntityChangeECB(int entity, int component)
        {
            if (transactions.TryGetValue(component, out var edge))
            {
                world->entitiesArchetypes.ElementAt(entity) = edge.Ref.ToMove;
                edge.Ref.Execute(entity);
                return;
            }

            CreateTransaction(component);
            edge = transactions[component];
            world->entitiesArchetypes.ElementAt(entity) = edge.Ref.ToMove;
            edge.Ref.Execute(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Destroy(int entity)
        {
            if (mask.Has(ComponentType<ComponentArray<Child>>.Index))
            {
                ref var pool = ref world->GetPool<ComponentArray<Child>>();
                ref var children = ref pool.GetRef<ComponentArray<Child>>(entity);
                foreach (ref var child in children)
                {
                    child.Value.Destroy();
                }
            }
            for (var index = 0; index < types.m_length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }

            destroyEdge.Execute(entity);
            world->OnDestroyEntity(entity);
        }

        internal void OnEntityFree(int entity)
        {
            for (var index = 0; index < types.m_length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }
        }

        private void CreateTransaction(int component)
        {
            var remove = component < 0;
            var newTypes = new UnsafeList<int>(remove ? mask.Count - 1 : mask.Count + 1, world->Allocator,
                NativeArrayOptions.ClearMemory);
            var positiveComponent = math.abs(component);
            foreach (var type in types)
                if ((remove && type == positiveComponent) == false)
                    newTypes.Add(type);

            if (remove == false) newTypes.Add(component);

            var otherArchetypeStruct = world->GetOrCreateArchetype(ref newTypes);
            var otherArchetype = otherArchetypeStruct.impl;
            var otherEdge = new Edge(ref otherArchetypeStruct, ref world->AllocatorRef);

            for (var index = 0; index < queries.Length; index++)
            {
                var t = queries[index];
                ref var thisQuery = ref Query(t);
                if (otherArchetype->queries.Contains(thisQuery.Ref.Id) == false)
                    otherEdge.RemoveEntity.Add(thisQuery, ref world->AllocatorRef);
            }

            for (var index = 0; index < otherArchetype->queries.Length; index++)
            {
                ref var otherQuery = ref Query(otherArchetype->queries[index]);
                if (queries.Contains(otherQuery.Ref.Id) == false)
                    otherEdge.AddEntity.Add(otherQuery, ref world->AllocatorRef);
            }

            var ptr = world->_allocate_ptr<Edge>();
            ptr.Ref = otherEdge;
            transactions.TryAdd(component, ptr);
        }

        [BurstDiscard]
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("<color=#FFB200>Archetype</color>");
            if (mask.Count == 0)
            {
                sb.Append(".Empty");
                return sb.ToString();
            }

            for (var i = 0; i < types.Length; i++) sb.Append($"[{ComponentTypeMap.GetType(types[i]).Name}]");

            sb.Append(Environment.NewLine);
            for (var index = 0; index < queries.Length; index++)
            {
                ref var ptr = ref Query(queries.ElementAt(index));
                sb.Append($"<color=#6CFF6C>{ptr.Ref.ToString()}</color>;{Environment.NewLine}");
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetHashCode(int[] mask)
        {
            unchecked
            {
                if (mask.Length == 0) return 0;
                var hash = (int)2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++)
                {
                    var i = mask[index];
                    hash = (hash ^ i) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(ref UnsafeList<int> mask)
        {
            unchecked
            {
                if (mask.Length == 0) return 0;
                var hash = (int)2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++)
                {
                    var i = mask[index];
                    hash = (hash ^ i) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(ref Span<int> mask)
        {
            unchecked
            {
                if (mask.Length == 0) return 0;
                var hash = (int)2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++)
                {
                    var i = mask[index];
                    hash = (hash ^ i) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }
    }

    [BurstCompile]
    public static class ArchetypePointerExtensions
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Has<T>(this ref ArchetypeUnsafe archetype) where T : unmanaged
        {
            return archetype.mask.Has(ComponentType<T>.Index);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Has(this ref ArchetypeUnsafe archetype, int type)
        {
            return archetype.mask.Has(type);
        }
    }

    internal unsafe struct Edge : IDisposable
    {
        internal MemoryList<ptr<QueryUnsafe>> AddEntity;
        internal MemoryList<ptr<QueryUnsafe>> RemoveEntity;
        [NativeDisableUnsafePtrRestriction] internal ArchetypeUnsafe* ToMovePtr;
        internal readonly Archetype ToMove;

        public Edge(ref Archetype archetype, ref SerializableMemoryAllocator allocator)
        {
            ToMovePtr = archetype.impl;
            ToMove = archetype;
            AddEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            RemoveEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
        }

        public Edge(ref SerializableMemoryAllocator allocator)
        {
            AddEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            RemoveEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            ToMovePtr = default;
            ToMove = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Execute(int entity)
        {
            for (var i = 0; i < RemoveEntity.length; i++) RemoveEntity.ElementAt(i).Ref.Remove(entity);

            for (var i = 0; i < AddEntity.length; i++) AddEntity.ElementAt(i).Ref.Add(entity);
        }

        public void Dispose()
        {
            //UnsafePtrList<QueryUnsafe>.Destroy(AddEntity);
            //UnsafePtrList<QueryUnsafe>.Destroy(RemoveEntity);
        }

        public void OnDeserialize(ref SerializableMemoryAllocator allocator)
        {
            foreach (ref var ptr in AddEntity)
            {
                ptr.OnDeserialize(ref allocator);
            }

            foreach (ref var ptr in RemoveEntity)
            {
                ptr.OnDeserialize(ref allocator);
            }
        }
    }
}