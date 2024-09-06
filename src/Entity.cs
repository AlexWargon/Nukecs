using Unity.Burst;

namespace Wargon.Nukecs {
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Transforms;

    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct Entity : IEquatable<Entity> {
        public readonly int id;
        [NativeDisableUnsafePtrRestriction] internal readonly World.WorldUnsafe* worldPointer;
        public ref World world => ref World.Get(worldPointer->Id);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(int id, World.WorldUnsafe* worldPointer) {
            this.id = id;
            this.worldPointer = worldPointer;
            this.worldPointer->entitiesArchetypes.ElementAtNoCheck(this.id) =
                this.worldPointer->GetArchetype(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(int id, World.WorldUnsafe* worldPointer, int archetype) {
            this.id = id;
            this.worldPointer = worldPointer;
            this.worldPointer->entitiesArchetypes.ElementAtNoCheck(this.id) =
                this.worldPointer->GetArchetype(archetype);
        }

        internal ref ArchetypeImpl archetypeRef {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *worldPointer->entitiesArchetypes.ElementAtNoCheck(this.id).impl;
        }

        public override string ToString() {
            return $"e:{id}, {archetypeRef.ToString()}";
        }

        public static readonly Entity Null = default;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Entity other) {
            return id == other.id;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) {
            return obj is Entity other && Equals(other);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() {
            return HashCode.Combine(id, unchecked((int) (long) worldPointer));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in Entity one, in Entity two) {
            return one.id == two.id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in Entity one, in Entity two) {
            return one.id != two.id;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid() {
            return worldPointer != null;
        }
    }
    [BurstCompile]
    public static unsafe class EntityExtensions {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref ComponentArray<T> GetArray<T>(this ref Entity entity) where T : unmanaged {
            ref var pool = ref entity.worldPointer->GetPool<ComponentArray<T>>();
            return ref pool.GetRef<ComponentArray<T>>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref ComponentArray<T> AddArray<T>(this ref Entity entity) where T : unmanaged {
            ref var pool = ref entity.worldPointer->GetPool<ComponentArray<T>>();
            pool.Set(entity.id, new ComponentArray<T>(6));
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add<ComponentArray<T>>(entity.id);
            return ref pool.GetRef<ComponentArray<T>>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveBuffer<T>(this ref Entity entity) where T : unmanaged, IComponent {
            ref var pool = ref entity.worldPointer->GetPool<ComponentArray<T>>();
            ref var buffer = ref pool.GetRef<ComponentArray<T>>(entity.id);
            buffer.Dispose(ref buffer);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Remove<ComponentArray<T>>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(this ref Entity entity, in T component) where T : unmanaged, IComponent {
            entity.worldPointer->GetPool<T>().Set(entity.id, in component);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add<T>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(this ref Entity entity) where T : unmanaged, IComponent {
            entity.worldPointer->GetPool<T>().Set(entity.id);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove<T>(this ref Entity entity) where T : unmanaged, IComponent {
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Remove<T>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Get<T>(this in Entity entity) where T : unmanaged, IComponent {
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Ref<T1>, Ref<T2>) Get<T1, T2>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent {
            return (
                new Ref<T1> {index = entity.id, pool = entity.worldPointer->GetPool<T1>()},
                new Ref<T2> {index = entity.id, pool = entity.worldPointer->GetPool<T2>()});
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Ref<T1>, Ref<T2>, Ref<T3>) Get<T1, T2, T3>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent {
            return (
                new Ref<T1> {index = entity.id, pool = entity.worldPointer->GetPool<T1>()},
                new Ref<T2> {index = entity.id, pool = entity.worldPointer->GetPool<T2>()},
                new Ref<T3> {index = entity.id, pool = entity.worldPointer->GetPool<T3>()});
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Ref<T1>, Ref<T2>, Ref<T3>, Ref<T4>) Get<T1, T2, T3, T4>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent {
            return (
                new Ref<T1> {index = entity.id, pool = entity.worldPointer->GetPool<T1>()},
                new Ref<T2> {index = entity.id, pool = entity.worldPointer->GetPool<T2>()},
                new Ref<T3> {index = entity.id, pool = entity.worldPointer->GetPool<T3>()},
                new Ref<T4> {index = entity.id, pool = entity.worldPointer->GetPool<T4>()});
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T Read<T>(this in Entity entity) where T : unmanaged, IComponent {
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ReadRef<T1>, ReadRef<T2>) ReadRef<T1, T2>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent {
            return (
                new ReadRef<T1>(entity.id, ref entity.worldPointer->GetPool<T1>()),
                new ReadRef<T2>(entity.id, ref entity.worldPointer->GetPool<T2>())
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTuple<T1, T2> Read<T1, T2>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent {
            return (
                entity.worldPointer->GetPool<T1>().GetRef<T1>(entity.id),
                entity.worldPointer->GetPool<T2>().GetRef<T2>(entity.id)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTuple<T1, T2, T3> Read<T1, T2, T3>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent {
            return (entity.worldPointer->GetPool<T1>().GetRef<T1>(entity.id),
                entity.worldPointer->GetPool<T2>().GetRef<T2>(entity.id),
                entity.worldPointer->GetPool<T3>().GetRef<T3>(entity.id));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (T1, T2, T3, T4) Read<T1, T2, T3, T4>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent {
            return (entity.worldPointer->GetPool<T1>().GetRef<T1>(entity.id),
                entity.worldPointer->GetPool<T2>().GetRef<T2>(entity.id),
                entity.worldPointer->GetPool<T3>().GetRef<T3>(entity.id),
                entity.worldPointer->GetPool<T4>().GetRef<T4>(entity.id));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Destroy(this ref Entity entity) {
            entity.Add(new DestroyEntity());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DestroyNow(this in Entity entity) {
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Destroy(entity.id);
        }

        internal static void Free(this in Entity entity) {
            entity.archetypeRef.Destroy(entity.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has<T>(this in Entity entity) where T : unmanaged, IComponent {
            return entity.archetypeRef.Has<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Entity Copy(this in Entity entity) {
            ref var arch = ref entity.archetypeRef;
            return arch.Copy(in entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Entity CopyVieECB(this in Entity entity) {
            var e = entity.worldPointer->CreateEntity();
            entity.worldPointer->ECB.Copy(from: entity.id, to: e.id);
            return e;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddChild(this ref Entity entity, Entity child) {
            if (child.Has<ChildOf>()) {
                ref var oldParent = ref child.Get<ChildOf>().Value;
                ref var children = ref oldParent.GetArray<Child>();
                foreach (ref var child1 in children) {
                    if (child1.Value == child) {
                        children.RemoveAtSwapBack(in child1);
                        break;
                    }
                }
                child.Get<ChildOf>().Value = entity;
            }
            else {
                child.Add(new ChildOf {Value = entity});
            }
            if (entity.Has<ComponentArray<Child>>()) {
                ref var childrenNew = ref entity.GetArray<Child>();
                childrenNew.Add(new Child{Value = child});
            }
            else {
                ref var childrenNew = ref entity.AddArray<Child>();
                childrenNew.Add(new Child{Value = child});
            }
            //entity.GetBuffer<Child>().Add(new Child(){Value = child});
            child.Add(new OnAddChildWithTransformEvent());
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Entity GetChild(this ref Entity entity, int index) {
            return ref entity.GetArray<Child>().ElementAt(index).Value;
        }
        public static void RemoveChild(this ref Entity entity, Entity child) {
            if(!entity.Has<ComponentArray<Child>>()) return;
            ref var children = ref entity.GetArray<Child>();
            foreach (ref var child1 in children) {
                if (child1.Value == child) {
                    children.RemoveAtSwapBack(in child1);
                    break;
                }
            }
            child.Remove<ChildOf>();
        }
    }

    public static unsafe class Unsafe {
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*) UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
    }
}