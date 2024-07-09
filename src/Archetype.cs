using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe struct Archetype : IDisposable {
        [NativeDisableUnsafePtrRestriction]
        internal ArchetypeImpl* impl;
        internal unsafe struct ArchetypeImpl {
            internal DynamicBitmask mask;
            internal UnsafeList<int> types;
            [NativeDisableUnsafePtrRestriction]
            internal World.WorldImpl* world;
            internal UnsafePtrList<Query.QueryImpl> queries;
            internal UnsafeHashMap<int, Edge> transactions;
            internal readonly int id;
            internal bool IsCreated => world != null;

            internal static void Free(ArchetypeImpl* archetype) {
                archetype->mask.Dispose();
                archetype->types.Dispose();
                archetype->queries.Dispose();
                foreach (var kvPair in archetype->transactions) {
                    kvPair.Value.Dispose();
                }

                archetype->transactions.Dispose();
                var allocator = archetype->world->allocator;
                archetype->world = null;
                UnsafeUtility.Free(archetype, allocator);
            }
            internal static ArchetypeImpl* Create(World.WorldImpl* world, int[] typesSpan = null) {
                var ptr = Unsafe.Malloc<ArchetypeImpl>(world->allocator);
                *ptr = new ArchetypeImpl(world, typesSpan);
                return ptr;
            }
            internal static ArchetypeImpl* Create(World.WorldImpl* world, ref UnsafeList<int> typesSpan) {
                var ptr = Unsafe.Malloc<ArchetypeImpl>(world->allocator);
                *ptr = new ArchetypeImpl(world, ref typesSpan);
                return ptr;
            }
            internal ArchetypeImpl(World.WorldImpl* world, int[] typesSpan = null) {
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
                
                this.queries = new UnsafePtrList<Query.QueryImpl>(8, world->allocator);
                this.transactions = new UnsafeHashMap<int, Edge>(8, world->allocator);
                PopulateQueries(world); 
            }
            internal ArchetypeImpl(World.WorldImpl* world, ref UnsafeList<int> typesSpan) {
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
                this.queries = new UnsafePtrList<Query.QueryImpl>(8, world->allocator);
                this.transactions = new UnsafeHashMap<int, Edge>(8, world->allocator);
                PopulateQueries(world); 
            }
            internal void PopulateQueries(World.WorldImpl* world) {
                for (var i = 0; i < world->queries.Length; i++) {
                    Query.QueryImpl* q = world->queries[i];
                    var matches = 0;
                    foreach (var type in types) {
                        if (q->HasNone(type)) {
                            break;
                        }

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
            
            internal bool Has(int type) {
                return mask.Has(type);
            }
            internal bool Has<T>() where T : unmanaged {
                return mask.Has(ComponentMeta<T>.Index);
            }
            //if component remove, component will be negative
            [BurstCompile]
            internal void OnEntityChange(ref Entity entity, int component) {
                if (transactions.TryGetValue(component, out var edge)) {
                    edge.Execute(entity.id);
                    entity.archetype = edge.toMove;
                    Debug.Log($"EXIST {edge.toMove->id}");
                    return;
                }
                CreateTransaction(component, out edge);
                transactions[component] = edge;
                edge.Execute(entity.id);
                entity.archetype = edge.toMove;
                //Debug.Log($"CREATED {edge.toMove->id}");
            }

            internal void CreateTransaction(int component, out Edge edge) {
                var remove = component < 0;

                var newTypes = new UnsafeList<int>(remove ? mask.Count-1 : mask.Count+1, world->allocator);
                newTypes.CopyFrom(in types);
                if (remove) {
                    var index = newTypes.IndexOf(math.abs(component));
                    newTypes.RemoveAtSwapBack(index);
                }
                else {
                    newTypes.Add(component);
                }
                //Debug.Log($"Component {component}");
                //Debug.Log($"REMOVE? {remove}");
                var otherArchetype = world->GetOrCreateArchetype(ref newTypes).impl;
                var otherEdge = new Edge(otherArchetype, world->allocator);
                
                for (var index = 0; index < queries.Length; index++) {
                    var thisQuery = queries[index];
                    if (otherArchetype->queries.Contains(thisQuery) == false) {
                        otherEdge._removeEntity->Add(thisQuery);
                        //Debug.Log("REMOVE QQQQQ");
                    }
                }

                for (var index = 0; index < otherArchetype->queries.Length; index++) {
                    var otherQuery = otherArchetype->queries[index];
                    if (queries.Contains(otherQuery) == false) {
                        otherEdge._addEntity->Add(otherQuery);
                    }
                }

                //Debug.Log(otherArchetype->id);
                edge = otherEdge;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GetHashCode(int[] mask) {
                unchecked {
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
                unchecked
                {
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
        internal unsafe struct Edge : IDisposable {
            [NativeDisableUnsafePtrRestriction] internal readonly UnsafePtrList<Query.QueryImpl>* _addEntity;
            [NativeDisableUnsafePtrRestriction] internal readonly UnsafePtrList<Query.QueryImpl>* _removeEntity;
            [NativeDisableUnsafePtrRestriction] internal ArchetypeImpl* toMove;
            public Edge(ArchetypeImpl* toMove, Allocator allocator) {
                this.toMove = toMove;
                this._addEntity = UnsafePtrList<Query.QueryImpl>.Create(6, allocator);
                this._removeEntity = UnsafePtrList<Query.QueryImpl>.Create(6, allocator);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Execute(int entity) {
                for (int i = 0; i < _removeEntity->m_length; i++) {
                    _removeEntity->ElementAt(i)->Remove(entity);
                }
                for (int i = 0; i < _addEntity->m_length; i++) {
                    _addEntity->ElementAt(i)->Add(entity);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AddToAddEntity(Query.QueryImpl* q) {
                _addEntity->Add(q);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AddToRemoveEntity(Query.QueryImpl* q) {
                _removeEntity->Add(q);
            }

            public void Dispose() {
                UnsafePtrList<Query.QueryImpl>.Destroy(_addEntity);
                UnsafePtrList<Query.QueryImpl>.Destroy(_removeEntity);
            }
        }
        
        internal bool Has<T>() where T : unmanaged {
            return impl->Has(ComponentMeta<T>.Index);
        }

        public void Dispose() {
            ArchetypeImpl.Free(impl);
        }
    }
}