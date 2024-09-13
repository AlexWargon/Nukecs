using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe struct Archetype {
        [NativeDisableUnsafePtrRestriction] internal ArchetypeImpl* impl;

        internal bool Has<T>() where T : unmanaged {
            return impl->Has(ComponentType<T>.Index);
        }

        public void Dispose() {
            ArchetypeImpl.Destroy(impl);
            impl = null;
        }
    }

    internal unsafe struct ArchetypePureUnsafe {
        internal UnsafeHashMap<int, GenericPool> pools;

        public ref T GetComponent<T>(int id) where T : unmanaged, IComponent {
            return ref pools[ComponentType<T>.Index].GetRef<T>(id);
        }
    }
    internal unsafe struct ArchetypeImpl {
        internal DynamicBitmask mask;
        internal UnsafeList<int> types;
        [NativeDisableUnsafePtrRestriction] internal World.WorldUnsafe* world;
        internal UnsafePtrList<QueryUnsafe> queries;
        internal UnsafeHashMap<int, Edge> transactions;
        internal Edge destroyEdge;
        internal readonly int id;
        internal bool IsCreated => world != null;

        internal static void Destroy(ArchetypeImpl* archetype) {
            archetype->mask.Dispose();
            archetype->types.Dispose();
            archetype->queries.Dispose();
            foreach (var kvPair in archetype->transactions) {
                kvPair.Value.Dispose();
            }

            archetype->destroyEdge.Dispose();
            archetype->transactions.Dispose();
            var allocator = archetype->world->allocator;
            archetype->world = null;
            UnsafeUtility.Free(archetype, allocator);
        }

        internal static ArchetypeImpl* Create(World.WorldUnsafe* world, int[] typesSpan = null) {
            var ptr = Unsafe.Malloc<ArchetypeImpl>(world->allocator);
            *ptr = new ArchetypeImpl(world, typesSpan);
            return ptr;
        }

        internal static ArchetypeImpl* Create(World.WorldUnsafe* world, ref UnsafeList<int> typesSpan) {
            var ptr = Unsafe.Malloc<ArchetypeImpl>(world->allocator);
            *ptr = new ArchetypeImpl(world, ref typesSpan);
            return ptr;
        }

        internal ArchetypeImpl(World.WorldUnsafe* world, int[] typesSpan = null) {
            this.world = world;
            this.mask = DynamicBitmask.CreateForComponents();
            this.id = 0;
            if (typesSpan != null) {
                this.types = new UnsafeList<int>(typesSpan.Length, world->allocator);
                this.id = GetHashCode(typesSpan);
                foreach (var type in typesSpan) {
                    this.mask.Add(type);
                    this.types.Add(in type);
                }
            }
            else {
                this.types = new UnsafeList<int>(1, world->allocator);
            }
            this.queries = new UnsafePtrList<QueryUnsafe>(8, world->allocator);
            this.transactions = new UnsafeHashMap<int, Edge>(8, world->allocator);
            this.destroyEdge = default;
            this.PopulateQueries(world);
            this.destroyEdge = CreateDestroyEdge();
        }

        internal ArchetypeImpl(World.WorldUnsafe* world, ref UnsafeList<int> typesSpan) {
            this.world = world;
            this.mask = DynamicBitmask.CreateForComponents();
            if (typesSpan.IsCreated) {
                this.types = typesSpan;
                foreach (var type in typesSpan) {
                    this.mask.Add(type);
                }
            }
            else {
                this.types = new UnsafeList<int>(1, world->allocator);
            }
            this.id = GetHashCode(ref typesSpan);
            this.queries = new UnsafePtrList<QueryUnsafe>(8, world->allocator);
            this.transactions = new UnsafeHashMap<int, Edge>(8, world->allocator);
            this.destroyEdge = default;
            
            this.PopulateQueries(world);
            this.destroyEdge = CreateDestroyEdge();
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
            var edge = new Edge(world->allocator);
            for (int i = 0; i < queries.Length; i++) {
                edge.removeEntity->Add(queries.ElementAt(i));
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
                world->entitiesArchetypes.ElementAt(entity) = edge.toMove;
                edge.Execute(entity);
                return;
            }
            CreateTransaction(component);
            edge = transactions[component];
            world->entitiesArchetypes.ElementAt(entity) = edge.toMove;
            edge.Execute(entity);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Destroy(int entity) {
            destroyEdge.Execute(entity);
            for (var index = 0; index < types.m_length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }
            world->OnDestroyEntity(entity);
        }

        internal void OnEntityFree(int entity) {
            //destroyEdge.Execute(entity);
            for (var index = 0; index < types.m_length; index++)
            {
                ref var pool = ref world->GetUntypedPool(types[index]);
                pool.Remove(entity);
            }
        }
        private void CreateTransaction(int component) {
            var remove = component < 0;
            var newTypes = new UnsafeList<int>(remove ? mask.Count - 1 : mask.Count + 1, world->allocator,
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
            var otherEdge = new Edge(ref otherArchetypeStruct, world->allocator);

            for (var index = 0; index < queries.Length; index++) {
                var thisQuery = queries[index];
                if (otherArchetype->queries.Contains(thisQuery) == false) {
                    otherEdge.removeEntity->Add(thisQuery);
                }
            }
            for (var index = 0; index < otherArchetype->queries.Length; index++) {
                var otherQuery = otherArchetype->queries[index];
                if (queries.Contains(otherQuery) == false) {
                    otherEdge.addEntity->Add(otherQuery);
                }
            }
            if (transactions.ContainsKey(component)) {
                return;
            }
            transactions.Add(component, otherEdge);
        }

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
        public static int GetHashCode(ref UnsafeList<int> mask) {
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
        internal static bool Has<T>(this ref ArchetypeImpl archetype) where T : unmanaged {
            return archetype.mask.Has(ComponentType<T>.Index);
        }
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Has(this ref ArchetypeImpl archetype, int type) {
            return archetype.mask.Has(type);
        }
    }
    
    internal readonly unsafe struct Edge : IDisposable {
        [NativeDisableUnsafePtrRestriction] internal readonly UnsafePtrList<QueryUnsafe>* addEntity;
        [NativeDisableUnsafePtrRestriction] internal readonly UnsafePtrList<QueryUnsafe>* removeEntity;
        [NativeDisableUnsafePtrRestriction] internal readonly ArchetypeImpl* toMovePtr;
        internal readonly Archetype toMove;

        public Edge(ref Archetype archetype, Allocator allocator) {
            this.toMovePtr = archetype.impl;
            this.toMove = archetype;
            this.addEntity = UnsafePtrList<QueryUnsafe>.Create(6, allocator);
            this.removeEntity = UnsafePtrList<QueryUnsafe>.Create(6, allocator);
        }

        public Edge(Allocator allocator) {
            this.addEntity = UnsafePtrList<QueryUnsafe>.Create(6, allocator);
            this.removeEntity = UnsafePtrList<QueryUnsafe>.Create(6, allocator);
            toMovePtr = null;
            toMove = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Execute(int entity) {
            for (int i = 0; i < removeEntity->Length; i++) {
                removeEntity->ElementAt(i)->Remove(entity);
            }

            for (int i = 0; i < addEntity->Length; i++) {
                addEntity->ElementAt(i)->Add(entity);
            }
        }

        public void Dispose() {
            UnsafePtrList<QueryUnsafe>.Destroy(addEntity);
            UnsafePtrList<QueryUnsafe>.Destroy(removeEntity);
        }
    }
}