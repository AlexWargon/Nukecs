using System;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public unsafe struct Archetype
    {
        internal ArchetypeUnsafe* impl => ptr.Ptr;
        internal ptr<ArchetypeUnsafe> ptr;
        public bool IsCreated => impl != null;
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
        private Spinner spinner;
        internal DynamicBitmask mask;
        internal MemoryList<int> types;
        [NativeDisableUnsafePtrRestriction] internal World.WorldUnsafe* world;
        internal MemoryList<int> queries;
        internal HashMap<int, ptr<Edge>> transactions;
        internal Edge destroyEdge;
        internal readonly int id;
        internal bool IsCreated => world != null;

        internal void OnDeserialize(ref MemAllocator allocator, Allocator unityAllocator)
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

        internal static ptr<ArchetypeUnsafe> CreatePtr(World.WorldUnsafe* world, ref MemoryList<int> typesSpan,
            bool copyList = false)
        {
            var ptr = world->_allocate_ptr<ArchetypeUnsafe>();
            *ptr.Ptr = new ArchetypeUnsafe(world, ref typesSpan, copyList);
            return ptr;
        }

        internal ArchetypeUnsafe(World.WorldUnsafe* world, int[] typesSpan = null)
        {
            spinner = new Spinner();
            this.world = world;
            mask = DynamicBitmask.CreateForComponents(world);
            id = 0;
            if (typesSpan != null)
            {
                types = new MemoryList<int>(typesSpan.Length, ref world->AllocatorRef);
                id = GetHashCode(typesSpan);
                foreach (var type in typesSpan)
                {
                    mask.Add(type);
                    types.Add(type, ref world->AllocatorRef);
                }
            }
            else
            {
                // Root Archetype
                types = new MemoryList<int>(1, ref world->AllocatorRef);
            }

            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            transactions = new HashMap<int, ptr<Edge>>(8, ref world->AllocatorHandler);
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
        }

        internal ArchetypeUnsafe(World.WorldUnsafe* world, ref MemoryList<int> typesSpan, bool copyList = false)
        {
            spinner = new Spinner();
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
                types = new MemoryList<int>(1, ref world->AllocatorRef);
            }

            id = GetHashCode(ref typesSpan);
            queries = new MemoryList<int>(8, ref this.world->AllocatorRef);
            transactions = new HashMap<int, ptr<Edge>>(8, ref world->AllocatorHandler);
            destroyEdge = default;
            PopulateQueries(world);
            destroyEdge = CreateDestroyEdge();
            if (copyList)
            {
                types = new MemoryList<int>(typesSpan.length, ref world->AllocatorRef);
                types.CopyFrom(ref typesSpan, ref world->AllocatorRef);
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

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
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

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Entity Copy(in Entity entity)
        {
            var newEntity = world->CreateEntity(id);
            for (var i = 0; i < queries.Length; i++)
            {
                var queryId = queries.ElementAt(i);
                Query(queryId).Ref.Add(newEntity.id);
            }

            for (var index = 0; index < types.length; index++)
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

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void OnEntityChangeECB(int entity, int component)
        {
            {
                if (transactions.TryGetValue(component, out var edge))
                {
                    world->entitiesArchetypes.ElementAt(entity) = edge.Ref.ToMove;
                    edge.Ref.Execute(entity);
                }
                else
                {
                    CreateTransaction(component);
                    edge = transactions[component];
                    world->entitiesArchetypes.ElementAt(entity) = edge.Ref.ToMove;
                    edge.Ref.Execute(entity);
                }
            }
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
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
            for (var index = 0; index < types.length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }

            destroyEdge.Execute(entity);
            world->OnDestroyEntity(entity);
        }

        internal EntityData GetEntityData(Entity entity)
        {
            EntityData data;
            data.Entity = entity.id;
            data.Components = new byte[types.length][];
            data.SizeInBytes = 0;
            for (var i = 0; i < types.length; i++)
            {
                ref var pool = ref world->GetUntypedPool(types[i]);
                data.Components[i] = pool.Serialize(entity.id);
                data.SizeInBytes += pool.UnsafeBuffer->componentTypeData.size;
            }

            return data;
        }

        internal void SetEntityData(EntityData data)
        {
            for (var i = 0; i < data.Components.Length; i++)
            {
                ref var pool = ref world->GetUntypedPool(types[i]);
                pool.WriteBytes(data.Entity, data.Components[i]);
            }
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal void OnEntityFree(int entity)
        {
            for (var index = 0; index < types.length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }
        }

        private void CreateTransaction(int component)
        {
            var remove = component < 0;
            var newTypes = new MemoryList<int>(remove ? mask.Count - 1 : mask.Count + 1, ref world->AllocatorRef);
            var positiveComponent = math.abs(component);
            foreach (var type in types)
                if ((remove && type == positiveComponent) == false)
                    newTypes.Add(type, ref world->AllocatorRef);

            if (remove == false) newTypes.Add(component, ref world->AllocatorRef);

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
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static int GetHashCode(ref MemoryList<int> mask)
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
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool Has<T>(this ref ArchetypeUnsafe archetype) where T : unmanaged
        {
            return archetype.mask.Has(ComponentType<T>.Index);
        }

        [BurstCompile]
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool Has(this ref ArchetypeUnsafe archetype, int type)
        {
            return archetype.mask.Has(type);
        }
    }
    [Serializable]
    public struct EntityData
    {
        public int Entity;
        public byte[][] Components;
        /// Size of Components
        public int SizeInBytes;
    }
    internal unsafe struct Edge
    {
        internal MemoryList<ptr<QueryUnsafe>> AddEntity;
        internal MemoryList<ptr<QueryUnsafe>> RemoveEntity;
        [NativeDisableUnsafePtrRestriction] internal readonly ArchetypeUnsafe* ToMovePtr;
        internal readonly Archetype ToMove;

        public Edge(ref Archetype archetype, ref MemAllocator allocator)
        {
            ToMovePtr = archetype.impl;
            ToMove = archetype;
            AddEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
            RemoveEntity = new MemoryList<ptr<QueryUnsafe>>(8, ref allocator);
        }

        public Edge(ref MemAllocator allocator)
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

        public void OnDeserialize(ref MemAllocator allocator)
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