using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct Entity : IEquatable<Entity> {
        public readonly int id;
        [NativeDisableUnsafePtrRestriction] internal readonly World.WorldUnsafe* worldPointer;
        public ref World World => ref World.Get(worldPointer->Id);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(int id, World.WorldUnsafe* worldPointer) {
            this.id = id;
            this.worldPointer = worldPointer;
            worldPointer->entitiesArchetypes.ElementAt(this.id) =
                this.worldPointer->GetArchetype(0);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(int id, World.WorldUnsafe* worldPointer, int archetype) {
            this.id = id;
            this.worldPointer = worldPointer;
            worldPointer->entitiesArchetypes.ElementAt(this.id) =
                this.worldPointer->GetArchetype(archetype);
        }
        internal ref ArchetypeImpl archetypeRef {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *worldPointer->entitiesArchetypes.ElementAt(this.id).impl;
        }

        public override string ToString() {
            return $"e:{id}, {archetypeRef.ToString()}";
        }

        public static Entity Null => default;

        public bool Equals(Entity other) {
            return id == other.id && worldPointer == other.worldPointer;
        }
        public override bool Equals(object obj) {
            return obj is Entity other && Equals(other);
        }
        public override int GetHashCode() {
            return HashCode.Combine(id, unchecked((int) (long) worldPointer));
        }
    }

    [BurstCompile]
    public static unsafe class EntityExt {
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref DynamicBuffer<T> GetBuffer<T>(this ref Entity entity) where T : unmanaged {
            ref var pool = ref entity.worldPointer->GetPool<DynamicBuffer<T>>();
            return ref pool.GetRef<DynamicBuffer<T>>(entity.id);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref DynamicBuffer<T> AddBuffer<T>(this ref Entity entity) where T : unmanaged {
            ref var pool = ref entity.worldPointer->GetPool<DynamicBuffer<T>>();
            pool.Set(entity.id, new DynamicBuffer<T>(6));
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add<DynamicBuffer<T>>(entity.id);
            return ref pool.GetRef<DynamicBuffer<T>>(entity.id);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveBuffer<T>(this ref Entity entity) where T : unmanaged , IComponent {
            ref var pool = ref entity.worldPointer->GetPool<DynamicBuffer<T>>();
            ref var buffer = ref pool.GetRef<DynamicBuffer<T>>(entity.id);
            buffer.Dispose();
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Remove<DynamicBuffer<T>>(entity.id);
        }
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(this ref Entity entity, in T component) where T : unmanaged, IComponent  {
            //entity.archetype->OnEntityChange(ref entity, ComponentMeta<T>.Index);
            //if (entity.archetypeRef.Has<T>()) return;
            entity.worldPointer->GetPool<T>().Set(entity.id, in component);
            //entity.archetypeRef.OnEntityChange(ref entity, ComponentMeta<T>.Index);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add<T>(entity.id);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddBytes(this in Entity entity, byte[] component, int componentIndex) {
            if (entity.archetypeRef.Has(componentIndex)) return;
            entity.worldPointer->GetUntypedPool(componentIndex).WriteBytes(entity.id, component);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add(entity.id, componentIndex);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddBytesUnsafe(this in Entity entity, byte* component, int sizeInBytes,
            int componentIndex) {
            if (entity.archetypeRef.Has(componentIndex)) return;
            entity.worldPointer->GetUntypedPool(componentIndex).WriteBytesUnsafe(entity.id, component, sizeInBytes);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        public static void Remove<T>(this ref Entity entity) where T : unmanaged, IComponent  {
            entity.worldPointer->GetPool<T>().Set(entity.id, default(T));
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Remove<T>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Get<T>(this in Entity entity) where T : unmanaged, IComponent  {
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }

        public static (Ref<T1>, Ref<T2>) Get<T1, T2>(this in Entity entity) 
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent 
        {
            return (
                new Ref<T1>{index = entity.id, pool = entity.worldPointer->GetPool<T1>()},
                new Ref<T2>{index = entity.id, pool = entity.worldPointer->GetPool<T2>()});
        }
        public static (Ref<T1>, Ref<T2>, Ref<T3>) Get<T1, T2, T3>(this in Entity entity) 
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return (
                new Ref<T1>{index = entity.id, pool = entity.worldPointer->GetPool<T1>()},
                new Ref<T2>{index = entity.id, pool = entity.worldPointer->GetPool<T2>()},
                new Ref<T3>{index = entity.id, pool = entity.worldPointer->GetPool<T3>()});
        }
        public static (Ref<T1>, Ref<T2>, Ref<T3>,Ref<T4>) Get<T1, T2, T3, T4>(this in Entity entity) 
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
        {
            return (
                new Ref<T1>{index = entity.id, pool = entity.worldPointer->GetPool<T1>()},
                new Ref<T2>{index = entity.id, pool = entity.worldPointer->GetPool<T2>()},
                new Ref<T3>{index = entity.id, pool = entity.worldPointer->GetPool<T3>()},
                new Ref<T4>{index = entity.id, pool = entity.worldPointer->GetPool<T4>()});
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T Read<T>(this in Entity entity) where T : unmanaged, IComponent  {
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroyLate(this ref Entity entity) {
            entity.Add(new DestroyEntity());
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Destroy(this in Entity entity) {
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Destroy(entity.id);
        }

        [BurstCompile][MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has<T>(this in Entity entity) where T : unmanaged, IComponent  {
            return entity.worldPointer->entitiesArchetypes.ElementAt(entity.id).impl->Has<T>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Entity Copy(this in Entity entity) {
            ref var arch = ref entity.archetypeRef;
            return arch.Copy(in entity);
        }

        public static Entity CopyVieECB(this in Entity entity) {
            var e = entity.worldPointer->CreateEntity();
            entity.worldPointer->ECB.Copy(from:entity.id, to:e.id);
            return e;
        }
    }

    public static unsafe class Unsafe {
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*) UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
    }

    

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