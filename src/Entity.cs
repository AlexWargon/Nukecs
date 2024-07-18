using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct Entity {
        public readonly int id;
        [NativeDisableUnsafePtrRestriction] internal readonly World.WorldImpl* worldPointer;
        public ref World World => ref World.Get(worldPointer->Id);

        internal Entity(int id, World.WorldImpl* worldPointer) {
            this.id = id;
            this.worldPointer = worldPointer;
            this.worldPointer->entitiesArchetypes.ElementAt(this.id) =
                this.worldPointer->GetArchetype(0);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(this ref Entity entity, in T component) where T : unmanaged {
            //entity.archetype->OnEntityChange(ref entity, ComponentMeta<T>.Index);
            //if (entity.archetypeRef.Has<T>()) return;
            entity.worldPointer->GetPool<T>().Set(entity.id, in component);
            //entity.archetypeRef.OnEntityChange(ref entity, ComponentMeta<T>.Index);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add<T>(entity.id);
        }

        internal static void AddBytes(this in Entity entity, byte[] component, int componentIndex) {
            if (entity.archetypeRef.Has(componentIndex)) return;
            entity.worldPointer->GetUntypedPool(componentIndex).WriteBytes(entity.id, component);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add(entity.id, componentIndex);
        }

        internal static void AddBytesUnsafe(this in Entity entity, byte* component, int sizeInBytes,
            int componentIndex) {
            if (entity.archetypeRef.Has(componentIndex)) return;
            entity.worldPointer->GetUntypedPool(componentIndex).WriteBytesUnsafe(entity.id, component, sizeInBytes);
        }

        internal static void AddObject(this in Entity entity, IComponent component) {
            var componentIndex = ComponentsMap.Index(component.GetType());
            entity.worldPointer->GetUntypedPool(componentIndex).SetObject(entity.id, component);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add(entity.id, componentIndex);
        }

        // internal static void AddPtr<T>(this ref Entity entity, T* ptr) where T : unmanaged {
        //     ref var ecb = ref entity.world->ECB;
        //     ecb.Add(entity.id, ptr);
        // }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove<T>(this ref Entity entity) where T : unmanaged {
            entity.worldPointer->GetPool<T>().Set(entity.id, default(T));
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Remove<T>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Get<T>(this in Entity entity) where T : unmanaged {
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T Read<T>(this in Entity entity) where T : unmanaged {
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
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

    public struct DestroyEntity : IComponent { }

    public static class Nukecs {
        [BurstDiscard]
        public static void Log(string message) {
            UnityEngine.Debug.Log(message);
        }
    }

    // public unsafe struct Pools {
    //     public void* pools;
    //     public int count;
    //     public void Add<T>() where T : unmanaged {
    //         var array = new NativeArray<T>(256, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    //         pools[count++] = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>()
    //     }
    // }
}