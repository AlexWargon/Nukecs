using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public unsafe struct Archetype {
        internal unsafe struct ArchetypeImpl {
            internal DynamicBitmask mask;
            internal UnsafeList<int> types;
            internal World.WorldImpl* world;
            internal UnsafePtrList<Query.QueryImpl> queries;
            internal UnsafeHashMap<int, Edge> transactions;
            internal readonly int id;
            internal bool IsCreated => world != null;
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
                this.mask = default;
                if (typesSpan != null) {
                    this.types = new UnsafeList<int>(typesSpan.Length, world->allocator);
                    foreach (var type in typesSpan) {
                        this.mask.Add(type);
                        this.types.Add(in type);
                    }
                }
                else {
                    this.types = new UnsafeList<int>(1, world->allocator);
                }

                this.id = GetHashCode(typesSpan);
                this.queries = new UnsafePtrList<Query.QueryImpl>(8, world->allocator);
                this.transactions = new UnsafeHashMap<int, Edge>(8, world->allocator);
                PopulateQueries(world); 
            }
            internal ArchetypeImpl(World.WorldImpl* world, ref UnsafeList<int> typesSpan) {
                this.world = world;
                this.mask = default;
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
                foreach (Query.QueryImpl* q in world->queries) {
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
            //if component remove, component will be negative
            internal void OnEntityChange(int entity, int component) {
                if (transactions.TryGetValue(component, out var edge)) {
                    edge.Execute(entity);
                    return;
                }
                CreateTransaction(component, out edge);
                edge.Execute(entity);
            }

            internal void CreateTransaction(int component, out Edge edge) {
                ref var allQueries = ref world->queries;
                var remove = component < 0;
                var newMask = mask.Copy();
                if (remove) {
                    newMask.Remove(component);
                }
                else {
                    newMask.Add(component);
                }

                var newTypes = new UnsafeList<int>(newMask.Count, world->allocator);
                newTypes.CopyFrom(in types);
                if (remove) {
                    var index = newTypes.IndexOf(component);
                    newTypes.RemoveAtSwapBack(index);
                }
                else {
                    newTypes.Add(component);
                }

                var otherArchetype = world->GetOrCreateArchetype(ref newTypes).impl;
                var otherEdge = new Edge(otherArchetype, world->allocator);
                
                for (var index = 0; index < queries.Length; index++) {
                    var thisQuery = queries[index];
                    if (otherArchetype->queries.Contains(thisQuery) == false) {
                        otherEdge._removeEntity->Add(thisQuery);
                    }
                }

                for (var index = 0; index < otherArchetype->queries.Length; index++) {
                    var otherQuery = otherArchetype->queries[index];
                    if (queries.Contains(otherQuery) == false) {
                        otherEdge._addEntity->Add(otherQuery);
                    }
                }

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
        }
        internal unsafe struct Edge : IDisposable {
            internal readonly UnsafePtrList<Query.QueryImpl>* _addEntity;
            internal readonly UnsafePtrList<Query.QueryImpl>* _removeEntity;
            internal ArchetypeImpl* toMove;
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
        internal ArchetypeImpl* impl;
        
        internal bool Has<T>() where T : unmanaged {
            return impl->Has(ComponentMeta<T>.Index);
        }
    }
}