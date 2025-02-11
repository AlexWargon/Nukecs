using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs {
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    
    public unsafe struct Archetype {
        [NativeDisableUnsafePtrRestriction] internal ArchetypeUnsafe* impl;

        internal bool Has<T>() where T : unmanaged {
            return impl->Has(ComponentType<T>.Index);
        }

        internal void Refresh()
        {
            impl->queries.Clear();
            impl->PopulateQueries(impl->world);
        }
        
        public void Dispose() {
            ArchetypeUnsafe.Destroy(impl);
            impl = null;
        }
    }

    internal unsafe struct ArchetypePureUnsafe {
        internal UnsafeHashMap<int, GenericPool> pools;

        public ref T GetComponent<T>(int id) where T : unmanaged, IComponent {
            return ref pools[ComponentType<T>.Index].GetRef<T>(id);
        }
    }
    internal unsafe struct ArchetypeUnsafe {
        internal DynamicBitmask mask;
        internal Unity.Collections.LowLevel.Unsafe.UnsafeList<int> types;
        [NativeDisableUnsafePtrRestriction] internal World.WorldUnsafe* world;
        internal UnsafePtrList<QueryUnsafe> queries;
        internal HashMap<int, Edge> transactions;
        internal Edge destroyEdge;
        internal readonly int id;
        internal bool IsCreated => world != null;

        internal static void Destroy(ArchetypeUnsafe* archetype) {
            archetype->mask.Dispose();
            archetype->types.Dispose();
            archetype->queries.Dispose();
            foreach (var kvPair in archetype->transactions) {
                kvPair.Value.Dispose();
            }
            archetype->destroyEdge.Dispose();
            archetype->transactions.Dispose();
            var w = archetype->world;
            w->_free(archetype);
            archetype->world = null;
        }

        internal static ArchetypeUnsafe* Create(World.WorldUnsafe* world, int[] typesSpan = null) {
            var ptr = world->_allocate<ArchetypeUnsafe>();
            *ptr = new ArchetypeUnsafe(world, typesSpan);
            return ptr;
        }

        internal static ArchetypeUnsafe* Create(World.WorldUnsafe* world, ref Unity.Collections.LowLevel.Unsafe.UnsafeList<int> typesSpan, bool copyList = false) {
            var ptr = world->_allocate<ArchetypeUnsafe>();
            *ptr = new ArchetypeUnsafe(world, ref typesSpan, copyList);
            return ptr;
        }

        internal ArchetypeUnsafe(World.WorldUnsafe* world, int[] typesSpan = null) {
            this.world = world;
            this.mask = DynamicBitmask.CreateForComponents(world);
            this.id = 0;
            if (typesSpan != null) {
                this.types = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(typesSpan.Length, world->Allocator);
                this.id = GetHashCode(typesSpan);
                foreach (var type in typesSpan) {
                    this.mask.Add(type);
                    this.types.Add(in type);
                }
            }
            else {
                // Root Archetype
                this.types = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(1, world->Allocator);
            }
            this.queries = new UnsafePtrList<QueryUnsafe>(8, world->Allocator);
            this.transactions = new HashMap<int, Edge>(8, ref world->AllocatorHandler);
            this.destroyEdge = default;
            this.PopulateQueries(world);
            this.destroyEdge = CreateDestroyEdge();
        }

        internal ArchetypeUnsafe(World.WorldUnsafe* world, ref Unity.Collections.LowLevel.Unsafe.UnsafeList<int> typesSpan, bool copyList = false) {
            this.world = world;
            this.mask = DynamicBitmask.CreateForComponents(world);
            if (typesSpan.IsCreated) {
                this.types = typesSpan;
                foreach (var type in typesSpan) {
                    this.mask.Add(type);
                }
            }
            else {
                // Root Archetype
                this.types = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(1, world->Allocator);
            }
            this.id = GetHashCode(ref typesSpan);
            this.queries = new UnsafePtrList<QueryUnsafe>(8, world->Allocator);
            this.transactions = new HashMap<int, Edge>(8, ref world->AllocatorHandler);
            this.destroyEdge = default;
            
            this.PopulateQueries(world);
            this.destroyEdge = CreateDestroyEdge();
            if (copyList)
            {
                this.types = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(typesSpan.m_length, world->Allocator);
                this.types.CopyFrom(in typesSpan);
            }
        }

        internal Entity CreateEntity()
        {
            var e = world->CreateEntity(id);
            for (var i = 0; i < queries.m_length; i++)
            {
                queries.Ptr[i]->Add(e.id);
            }
            return e;
        }
        
        internal void Refresh()
        {
            queries.Clear();
            PopulateQueries(world);
        }
        
        internal void PopulateQueries(World.WorldUnsafe* world) {
            for (var i = 0; i < world->queries.Length; i++) {
                var q = world->queries[i];
                var matches = 0;
                var hasNone = false;
                for (var index = 0; index < types.Length; index++) {
                    var type = types[index];
                    if (q->HasNone(type)) {
                        hasNone = true;
                        break;
                    }
                }
                if(hasNone) continue;
                for (var index = 0; index < types.Length; index++) {
                    var type = types[index];
                    if (q->HasWith(type)) {
                        matches++;
                        if (matches == q->with.Count) {
                            queries.Add(q);
                            break;
                        }
                    }
                }
            }
        }

        internal Edge CreateDestroyEdge() {
            var edge = new Edge(world->Allocator);
            for (int i = 0; i < queries.Length; i++) {
                edge.RemoveEntity->Add(queries.ElementAt(i));
            }
            return edge;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Copy(in int from, in int to) {
            for (int i = 0; i < queries.Length; i++) {
                var q = queries.ElementAtNoCheck(i);
                q->Add(to);
            }

            foreach (var type in types) {
                ref var pool = ref world->GetUntypedPool(type);
                pool.Copy(from, to);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity Copy(in Entity entity) {
            var newEntity = world->CreateEntity(id);
            for (var i = 0; i < queries.Length; i++) {
                var q = queries.ElementAtNoCheck(i);
                q->Add(newEntity.id);
            }
            
            for (var index = 0; index < types.m_length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Copy(entity.id, newEntity.id);
            }
            
            if (mask.Has(ComponentType<ComponentArray<Child>>.Index)) {
                ref var pool = ref world->GetPool<ComponentArray<Child>>();
                ref var fromC = ref pool.GetRef<ComponentArray<Child>>(entity.id);
                ref var to = ref pool.GetRef<ComponentArray<Child>>(newEntity.id);

                for (var i = 0; i < fromC.Length; i++) {
                    ref var child = ref fromC.ElementAt(i);
                    ref var childNew = ref to.ElementAt(i);
                    childNew.Value = child.Value.Copy();
                    childNew.Value.Get<ChildOf>().Value = newEntity;
                }
            }
            //newEntity.Add<EntityCreated>();
            return newEntity;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnEntityChangeECB(int entity, int component) {
            if (transactions.TryGetValue(component, out var edge)) {
                world->entitiesArchetypes.ElementAt(entity) = edge.ToMove;
                edge.Execute(entity);
                return;
            }
            CreateTransaction(component);
            edge = transactions[component];
            world->entitiesArchetypes.ElementAt(entity) = edge.ToMove;
            edge.Execute(entity);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Destroy(int entity) {
            for (var index = 0; index < types.m_length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }
            destroyEdge.Execute(entity);
            world->OnDestroyEntity(entity);
        }

        internal void OnEntityFree(int entity) {
            for (var index = 0; index < types.m_length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }
        }
        private void CreateTransaction(int component) {
            var remove = component < 0;
            var newTypes = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(remove ? mask.Count - 1 : mask.Count + 1, world->Allocator,
                NativeArrayOptions.ClearMemory);
            var positiveComponent = math.abs(component);
            foreach (var type in types) {
                if ((remove && type == positiveComponent) == false) {
                    newTypes.Add(type);
                }
            }

            if (remove == false) {
                newTypes.Add(component);
            }

            var otherArchetypeStruct = world->GetOrCreateArchetype(ref newTypes);
            var otherArchetype = otherArchetypeStruct.impl;
            var otherEdge = new Edge(ref otherArchetypeStruct, world->Allocator);

            for (var index = 0; index < queries.Length; index++) {
                var thisQuery = queries[index];
                if (otherArchetype->queries.Contains(thisQuery) == false) {
                    otherEdge.RemoveEntity->Add(thisQuery);
                }
            }
            for (var index = 0; index < otherArchetype->queries.Length; index++) {
                var otherQuery = otherArchetype->queries[index];
                if (queries.Contains(otherQuery) == false) {
                    otherEdge.AddEntity->Add(otherQuery);
                }
            }

            transactions.TryAdd(component, otherEdge);
        }
        [BurstDiscard]
        public override string ToString() {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("<color=#FFB200>Archetype</color>");
            if (mask.Count == 0) {
                sb.Append(".Empty");
                return sb.ToString();
            }

            for (int i = 0; i < types.Length; i++) {
                sb.Append($"[{ComponentTypeMap.GetType(types[i]).Name}]");
            }

            sb.Append(Environment.NewLine);
            for (var index = 0; index < queries.Length; index++) {
                QueryUnsafe* ptr = queries.ElementAt(index);
                sb.Append($"<color=#6CFF6C>{ptr->ToString()}</color>;{Environment.NewLine}");
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetHashCode(int[] mask) {
            unchecked {
                if (mask.Length == 0) return 0;
                var hash = (int) 2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++) {
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
        public static int GetHashCode(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<int> mask) {
            unchecked {
                if (mask.Length == 0) return 0;
                var hash = (int) 2166136261;
                const int p = 16777619;
                for (var index = 0; index < mask.Length; index++) {
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
    public static class ArchetypePointerExtensions {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Has<T>(this ref ArchetypeUnsafe archetype) where T : unmanaged {
            return archetype.mask.Has(ComponentType<T>.Index);
        }
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Has(this ref ArchetypeUnsafe archetype, int type) {
            return archetype.mask.Has(type);
        }
    }
    
    internal readonly unsafe struct Edge : IDisposable {
        [NativeDisableUnsafePtrRestriction] internal readonly UnsafePtrList<QueryUnsafe>* AddEntity;
        [NativeDisableUnsafePtrRestriction] internal readonly UnsafePtrList<QueryUnsafe>* RemoveEntity;
        [NativeDisableUnsafePtrRestriction] internal readonly ArchetypeUnsafe* ToMovePtr;
        internal readonly Archetype ToMove;

        public Edge(ref Archetype archetype, Allocator allocator) {
            ToMovePtr = archetype.impl;
            ToMove = archetype;
            AddEntity = UnsafePtrList<QueryUnsafe>.Create(8, allocator);
            RemoveEntity = UnsafePtrList<QueryUnsafe>.Create(8, allocator);
        }

        public Edge(Allocator allocator) {
            AddEntity = UnsafePtrList<QueryUnsafe>.Create(8, allocator);
            RemoveEntity = UnsafePtrList<QueryUnsafe>.Create(8, allocator);
            ToMovePtr = null;
            ToMove = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Execute(int entity) {
            for (var i = 0; i < RemoveEntity->m_length; i++) {
                RemoveEntity->ElementAt(i)->Remove(entity);
            }

            for (var i = 0; i < AddEntity->m_length; i++) {
                AddEntity->ElementAt(i)->Add(entity);
            }
        }

        public void Dispose() {
            UnsafePtrList<QueryUnsafe>.Destroy(AddEntity);
            UnsafePtrList<QueryUnsafe>.Destroy(RemoveEntity);
        }
    }
}