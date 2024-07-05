using System;
using System.Collections;
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
            this._archetype = _world->GetArchetype(0)._impl;
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
            internal readonly int mask;
            internal readonly UnsafeList<int> types;
            internal readonly World.WorldImpl* world;
            internal UnsafePtrList<Query.QueryImpl> queries;
            internal UnsafeHashMap<int, ArchetypeImpl> transactions;
            internal int id;
            internal static ArchetypeImpl* Create(World.WorldImpl* world, int[] typesSpan = null) {
                var ptr = Unsafe.Malloc<ArchetypeImpl>(world->allocator);
                *ptr = new ArchetypeImpl(world, typesSpan);
                return ptr;
            }
            
            internal ArchetypeImpl(World.WorldImpl* world, int[] typesSpan) {
                this.world = world;
                this.mask = 0;
                this.types = new UnsafeList<int>(typesSpan.Length, world->allocator);
                if (typesSpan != null) {
                    foreach (var type in typesSpan) {
                        this.mask |= type;
                        this.types.Add(in type);
                    }
                }
                id = 0;
                queries = new UnsafePtrList<Query.QueryImpl>(8, world->allocator);
                transactions = new UnsafeHashMap<int, ArchetypeImpl>(8, world->allocator);
            }

            internal void PopulateQueries(World.WorldImpl* world) {
                foreach (Query.QueryImpl* q in world->queries) {
                    var matches = 0;
                    foreach (var type in types) {
                        if (q->HasNone(type)) {
                            continue;
                        }

                        if (q->HasWith(type)) {
                            matches++;
                            if (matches == q->with.count) {
                                queries.Add(q);
                            }
                        }
                    }
                }
            }
            internal bool Has(int type) {
                return (mask & type) == type;
            }
        }

        internal ArchetypeImpl* _impl;
        
        internal bool Has<T>() where T : unmanaged {
            return _impl->Has(ComponentMeta<T>.Index);
        }
    }

    public static unsafe class Unsafe {
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
    }
}

