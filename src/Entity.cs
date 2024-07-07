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
            this._archetype = _world->GetArchetype(0).impl;
        }

        public ref T Get<T>() where T : unmanaged {
            return ref _world->GetPool<T>().GetRef<T>(id);
        }

        public void Add<T>(T component) where T : unmanaged {
            //_world->GetPool<T>().Set(id, ref component);
            _archetype->OnEntityChange(id, ComponentMeta<T>.Index);
        }

        public void Remove<T>() where T : unmanaged {
            _archetype->OnEntityChange(id, -ComponentMeta<T>.Index);
        }
        public bool Has<T>() where T : unmanaged {
            return _archetype->Has(ComponentMeta<T>.Index);
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

    public static unsafe class Unsafe {
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
    }

    public interface IComponent {
        private static int count = -1;
        [RuntimeInitializeOnLoadMethod]
        public static int Count() {
            if (count != -1) return count;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    if (typeof(IComponent).IsAssignableFrom(type)) {
                        count++;
                    }
                }
            }
            return count;
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
}

