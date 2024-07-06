using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe struct Entity {
        public readonly int id;
        private readonly World.WorldImpl* _world;
        private Archetype.ArchetypeImpl* _archetype;
        public ref World World => ref Nukecs.World.Get(_world->Id);
        internal Entity(int entity, World.WorldImpl* world) {
            this.id = entity;
            this._world = world;
            this._archetype = _world->GetArchetype(0).impl;
        }

        public ref T Get<T>() where T : unmanaged {
            return ref _world->GetPool<T>().GetRef<T>(id);
        }

        public void Add<T>(T component) where T : unmanaged {
            _world->GetPool<T>().Set(id, ref component);
        }

        public bool Has<T>() where T : unmanaged {
            return _archetype->Has(ComponentMeta<T>.Index);
        }
    }

    public struct Component {
        public static int Count;
    }
    public struct ComponentMeta<T> {
        public static int Index;

        static ComponentMeta() {
            Index = Component.Count++;
        }
    }
    public readonly unsafe struct UntypedUnsafeList {
        private readonly void* ptr;
        public readonly int len;
        private readonly Allocator _allocator;
        public bool IsCreated => ptr != null;
        public UntypedUnsafeList(Type type, int size, Allocator allocator) {
            _allocator = allocator;
            ptr = UnsafeUtility.Malloc(UnsafeUtility.SizeOf(type) * size, 0, allocator);
            len = size;
        }

        public ref T GetRef<T>(int index) where T : unmanaged {
            return ref UnsafeUtility.ArrayElementAsRef<T>(ptr, index);
        }

        public void Set<T>(int index, ref T value) where T : unmanaged {
            UnsafeUtility.ArrayElementAsRef<T>(ptr, index) = value;
        }

        public void Dispose() {
            UnsafeUtility.Free(ptr, _allocator);
        }
    }
    // public readonly unsafe struct UnsafeList<T> : IDisposable where T : unmanaged {
    //     private readonly T* _ptr;
    //     private readonly Allocator _allocator;
    //     public readonly int len;
    //     public bool IsCreated => _ptr != null;
    //     public UnsafeList(int size, Allocator allocator) {
    //         _allocator = allocator;
    //         _ptr = Unsafe.Malloc<T>(allocator);
    //         len = size;
    //     }
    //
    //     public ref T GetRef(int index) {
    //         return ref UnsafeUtility.ArrayElementAsRef<T>(_ptr, index);
    //     }
    //
    //     public void Set(int index, T value) {
    //         UnsafeUtility.WriteArrayElement(_ptr, index, value);
    //     }
    //
    //     public void Dispose() {
    //         UnsafeUtility.Free(_ptr, _allocator);
    //     }
    // }

    public unsafe struct Archetype {
        internal unsafe struct ArchetypeImpl {
            internal Bitmask1024 mask;
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
        }
        internal unsafe struct Edge : IDisposable {
            private readonly UnsafePtrList<Query.QueryImpl>* _addEntity;
            private readonly UnsafePtrList<Query.QueryImpl>* _removeEntity;
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

    public static unsafe class Unsafe {
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
    }
}

