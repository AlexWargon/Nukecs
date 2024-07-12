using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs {
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct Entity {
        public readonly int id;
        [NativeDisableUnsafePtrRestriction] 
        internal readonly World.WorldImpl* worldPointer;
        public ref World World => ref World.Get(worldPointer->Id);

        internal Entity(int id, World.WorldImpl* worldPointer) {
            this.id = id;
            this.worldPointer = worldPointer;
            worldPointer->entitiesArchetypes.ElementAt(this.id) = this.worldPointer->GetArchetype(0);
        }

        public ref T Get<T>() where T : unmanaged {
            return ref worldPointer->GetPool<T>().GetRef<T>(id);
        }



        internal ref ArchetypeImpl archetypeRef {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *worldPointer->entitiesArchetypes.ElementAt(this.id).impl;
        }

        public override string ToString() {
            return $"e:{id}, {archetypeRef.ToString()}";
        }
    }
    [BurstCompile]
    public static unsafe class EntityExt {
        [BurstCompile]
        public static void Add<T>(this ref Entity entity, T component) where T : unmanaged {
            //entity.archetype->OnEntityChange(ref entity, ComponentMeta<T>.Index);
            //if (entity.archetypeRef.Has<T>()) return;
            entity.worldPointer->GetPool<T>().Set(entity.id, component);
            //entity.archetypeRef.OnEntityChange(ref entity, ComponentMeta<T>.Index);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add<T>(entity.id);
        }

        // internal static void AddPtr<T>(this ref Entity entity, T* ptr) where T : unmanaged {
        //     ref var ecb = ref entity.world->ECB;
        //     ecb.Add(entity.id, ptr);
        // }
        public static void Remove<T>(this ref Entity entity) where T : unmanaged {
            //entity.archetype->OnEntityChange(ref entity, -ComponentMeta<T>.Index);
            //if (entity.archetypeRef.Has<T>() == false) return;
            entity.worldPointer->GetPool<T>().Set(entity.id, default(T));
            //entity.archetypeRef.OnEntityChange(ref entity, -ComponentMeta<T>.Index);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Remove<T>(entity.id);
        }
        [BurstCompile]
        public static void Destroy(this ref Entity entity) {
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Destroy(entity.id);
        }
        [BurstCompile]
        public static bool Has<T>(this in Entity entity) where T : unmanaged {
            return entity.worldPointer->entitiesArchetypes.ElementAt(entity.id).impl->Has<T>();
        }
    }

    public static unsafe class Unsafe {
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*) UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
    }

    public unsafe struct GenericPool : IDisposable {
        [NativeDisableUnsafePtrRestriction] internal Impl* impl;
        public bool IsCreated;

        public static GenericPool Create<T>(int size, Allocator allocator) where T : unmanaged {
            return new GenericPool() {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
        }

        public static GenericPool Create(Type type, int size, Allocator allocator) {
            return new GenericPool() {
                impl = Impl.CreateImpl(type, size, allocator),
                IsCreated = true
            };
        }

        public static GenericPool* CreatePtr<T>(int size, Allocator allocator) where T : unmanaged {
            var ptr = (GenericPool*) UnsafeUtility.Malloc(sizeof(GenericPool), UnsafeUtility.AlignOf<GenericPool>(),
                allocator);
            *ptr = new GenericPool {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
            return ptr;
        }

        internal struct Impl {
            [NativeDisableUnsafePtrRestriction] internal byte* buffer;
            internal int elementSize;
            internal int count;
            internal int capacity;
            internal Allocator allocator;

            internal static Impl* CreateImpl<T>(int size, Allocator allocator) where T : unmanaged {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);
                *ptr = new Impl {
                    elementSize = sizeof(T),
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*) UnsafeUtility.Malloc(sizeof(T) * size, UnsafeUtility.AlignOf<T>(), allocator)
                };
                return ptr;
            }

            internal static Impl* CreateImpl(Type type, int size, Allocator allocator) {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);
                var typeSize = UnsafeUtility.SizeOf(type);
                *ptr = new Impl {
                    elementSize = typeSize,
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*) UnsafeUtility.Malloc(typeSize, ComponentsMap.AlignOf(type), allocator)
                };
                return ptr;
            }
        }

        public void Set<T>(int index, T value) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException(
                    $"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }

            *(T*) (impl->buffer + index * impl->elementSize) = value;
            if (index >= impl->count) {
                impl->count = index + 1;
            }
        }

        public ref T GetRef<T>(int index) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException(
                    $"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }

            return ref *(T*) (impl->buffer + index * impl->elementSize);
        }

        public void SetPtr(int index, void* value) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException(
                    $"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }

            *(impl->buffer + index * impl->elementSize) = *(byte*) value;
            if (index >= impl->count) {
                impl->count = index + 1;
            }
        }

        public void Dispose() {
            if (impl == null) return;
            var allocator = impl->allocator;
            UnsafeUtility.Free(impl->buffer, allocator);
            UnsafeUtility.Free(impl, allocator);
            IsCreated = false;
        }
    }
    public struct DestroyEntity : IComponent { }

    public static class Nukecs
    {
        [BurstDiscard]
        public static void Log(string message) {
            UnityEngine.Debug.Log(message);
        }
    }
}