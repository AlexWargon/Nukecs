using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public unsafe struct Entity : IEquatable<Entity>
    {
        public int id;

        [NativeDisableUnsafePtrRestriction][NonSerialized]
        internal World.WorldUnsafe* worldPointer;

        public ref World world => ref World.Get(worldPointer->Id);
        public static readonly Entity Null = default;
        
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Entity(int id, World.WorldUnsafe* worldPointer)
        {
            this.id = id;
            this.worldPointer = worldPointer;
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal Entity(int id, World.WorldUnsafe* worldPointer, int archetype)
        {
            this.id = id;
            this.worldPointer = worldPointer;
            this.worldPointer->entitiesArchetypes.ElementAt(this.id) =
                this.worldPointer->GetArchetype(archetype);
        }

        internal ref ArchetypeUnsafe ArchetypeRef
        {
#if !NUKECS_DEBUG
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            get
            {
#if NUKECS_DEBUG
                if(worldPointer == null) throw new Exception("World pointer is null");
#endif
                var arch = worldPointer->entitiesArchetypes.ElementAt(id).ptr.Ptr;
#if NUKECS_DEBUG
                if (arch == null) throw new Exception("Archetype reference is null");
#endif
                return ref *arch;
            }
        }

        public override string ToString()
        {
            return $"e:{id}";
        }


#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public bool Equals(Entity other)
        {
            return id == other.id;
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public override bool Equals(object obj)
        {
            return obj is Entity other && Equals(other);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public override int GetHashCode()
        {
            return HashCode.Combine(id, unchecked((int)(long)worldPointer));
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static bool operator ==(in Entity one, in Entity two)
        {
            return one.id == two.id;
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static bool operator !=(in Entity one, in Entity two)
        {
            return one.id != two.id;
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public bool IsValid()
        {
            return worldPointer != null && worldPointer->EntityIsValid(id);
        }
    }

    [BurstCompile]
    public static unsafe class EntityExtensions
    {

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static bool Has<T>(this in Entity entity) where T : unmanaged, IComponent
        {
            return entity.ArchetypeRef.Has<T>();
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ref T Get<T>(this ref Entity entity) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Index;
            if (!entity.ArchetypeRef.Has(componentType))
            {
                ref var pool = ref entity.worldPointer->GetPool<T>();
                pool.Set(entity.id);
                entity.worldPointer->ECB.Add(entity.id, componentType);
                return ref pool.GetRef<T>(entity.id);
            }

            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ref T TryGet<T>(this in Entity entity, out bool exist) where T : unmanaged, IComponent
        {
            exist = entity.ArchetypeRef.Has(ComponentType<T>.Index);
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }

        [BurstDiscard]
        private static Exception NoComponentException<T>()
        {
            return new NoComponentException($"Entity has no component array {typeof(T).Name}");
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Add<T>(this ref Entity entity, in T component) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Index;
            if (entity.ArchetypeRef.Has(componentType)) return;
            entity.worldPointer->ECB.Add(entity.id, component);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Add<T>(this ref Entity entity) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Index;
            if (entity.ArchetypeRef.Has(componentType)) return;
            entity.worldPointer->GetPool<T>().Set(entity.id);
            entity.worldPointer->ECB.Add(entity.id, componentType);
        }
        
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void AddIndex(this ref Entity entity, int component)
        {
            if (entity.ArchetypeRef.Has(component)) return;
            entity.worldPointer->ECB.Add(entity.id, component);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Set<T>(this ref Entity entity, in T component) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Index;
            if (!entity.ArchetypeRef.Has(componentType)) return;
            entity.worldPointer->GetPool<T>().Set(entity.id, in component);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void AddBytes(this in Entity entity, byte[] component, int componentIndex)
        {
            if (entity.ArchetypeRef.Has(componentIndex)) return;
            entity.worldPointer->GetUntypedPool(componentIndex).WriteBytes(entity.id, component);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add(entity.id, componentIndex);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void AddBytesUnsafe(this in Entity entity, byte* component, int sizeInBytes,
            int componentIndex)
        {
            if (entity.ArchetypeRef.Has(componentIndex)) return;
            entity.worldPointer->GetUntypedPool(componentIndex).WriteBytesUnsafe(entity.id, component, sizeInBytes);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void AddObject(this in Entity entity, IComponent component)
        {
            var componentIndex = ComponentTypeMap.Index(component.GetType());
            entity.worldPointer->GetUntypedPool(componentIndex).AddObject(entity.id, component);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add(entity.id, componentIndex);
        }

        internal static void SetObject(this in Entity entity, IComponent component)
        {
            var componentIndex = ComponentTypeMap.Index(component.GetType());
            entity.worldPointer->GetUntypedPool(componentIndex).SetObject(entity.id, component);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Remove<T>(this in Entity entity) where T : unmanaged, IComponent
        {
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Remove<T>(entity.id);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void RemoveIndex(this in Entity entity, int componentType)
        {
            entity.worldPointer->ECB.Remove(entity.id, componentType);
        }


#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static (Ref<T1>, Ref<T2>) Get<T1, T2>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return (
                new Ref<T1> { index = entity.id, pool = entity.worldPointer->GetPool<T1>().UnsafeBuffer },
                new Ref<T2> { index = entity.id, pool = entity.worldPointer->GetPool<T2>().UnsafeBuffer });
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static (Ref<T1>, Ref<T2>, Ref<T3>) Get<T1, T2, T3>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return (
                new Ref<T1> { index = entity.id, pool = entity.worldPointer->GetPool<T1>().UnsafeBuffer },
                new Ref<T2> { index = entity.id, pool = entity.worldPointer->GetPool<T2>().UnsafeBuffer },
                new Ref<T3> { index = entity.id, pool = entity.worldPointer->GetPool<T3>().UnsafeBuffer });
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static (Ref<T1>, Ref<T2>, Ref<T3>, Ref<T4>) Get<T1, T2, T3, T4>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
        {
            return (
                new Ref<T1> { index = entity.id, pool = entity.worldPointer->GetPool<T1>().UnsafeBuffer },
                new Ref<T2> { index = entity.id, pool = entity.worldPointer->GetPool<T2>().UnsafeBuffer },
                new Ref<T3> { index = entity.id, pool = entity.worldPointer->GetPool<T3>().UnsafeBuffer },
                new Ref<T4> { index = entity.id, pool = entity.worldPointer->GetPool<T4>().UnsafeBuffer });
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ref readonly T Read<T>(this in Entity entity) where T : unmanaged, IComponent
        {
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static (ReadRef<T1>, ReadRef<T2>) ReadRef<T1, T2>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return (
                new ReadRef<T1>(entity.id, ref entity.worldPointer->GetPool<T1>()),
                new ReadRef<T2>(entity.id, ref entity.worldPointer->GetPool<T2>())
            );
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ValueTuple<T1, T2> Read<T1, T2>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return (
                entity.worldPointer->GetPool<T1>().GetRef<T1>(entity.id),
                entity.worldPointer->GetPool<T2>().GetRef<T2>(entity.id)
            );
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ComponentTupleRO<T1, T2, T3> Read<T1, T2, T3>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return new ComponentTupleRO<T1, T2, T3>(
                in entity.worldPointer->GetPool<T1>().GetRef<T1>(entity.id),
                in entity.worldPointer->GetPool<T2>().GetRef<T2>(entity.id),
                in entity.worldPointer->GetPool<T3>().GetRef<T3>(entity.id));
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static (T1, T2, T3, T4) Read<T1, T2, T3, T4>(this in Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
        {
            return (
                entity.worldPointer->GetPool<T1>().GetRef<T1>(entity.id),
                entity.worldPointer->GetPool<T2>().GetRef<T2>(entity.id),
                entity.worldPointer->GetPool<T3>().GetRef<T3>(entity.id),
                entity.worldPointer->GetPool<T4>().GetRef<T4>(entity.id));
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Destroy(this ref Entity entity)
        {
            entity.Add(new DestroyEntity());
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void DestroyNow(this in Entity entity)
        {
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Destroy(entity.id);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void Free(this in Entity entity)
        {
            entity.ArchetypeRef.OnEntityFree(entity.id);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static bool TryGetRef<T>(this in Entity entity, out Ref<T> component) where T : unmanaged, IComponent
        {
            if (entity.ArchetypeRef.Has<T>())
            {
                component.index = entity.id;
                component.pool = entity.worldPointer->GetPool<T>().UnsafeBuffer;
                return true;
            }

            component = default;
            return false;
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static Entity Copy(this in Entity entity)
        {
            ref var arch = ref entity.ArchetypeRef;
#if NUKECS_DEBUG
            entity.worldPointer->AddComponentChange(new World.ComponentChange
            {
                command = EntityCommandBuffer.ECBCommand.Type.Copy,
                entityId = entity.id,
                timeStamp = entity.worldPointer->timeData.ElapsedTime
            });
#endif
            return arch.Copy(in entity);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static Entity CopyVieECB(this in Entity entity)
        {
            var e = entity.worldPointer->CreateEntity();
            entity.worldPointer->ECB.Copy(entity.id, e.id);
            return e;
        }
    }
}