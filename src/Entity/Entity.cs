#if !NUKECS_DEBUG
using System.Runtime.CompilerServices;
#endif
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
        internal byte worldIndex;

        public World.WorldUnsafe* worldPointer => World.Get(worldIndex).UnsafeWorld;

        public ref World world => ref World.Get(worldPointer->Id);
        public static readonly Entity Null = default;

        public EntityIndex Index => new()
        {
            component = id % Chunk.MAX_CHUNK_SIZE,
            chunk = id / Chunk.MAX_CHUNK_SIZE
        };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(int id, byte world)
        {
            this.id = id;
            this.worldIndex = world;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(int id, World.WorldUnsafe* worldPointer)
        {
            this.id = id;
            this.worldIndex = worldPointer->Id;
        }
        internal ref ArchetypeUnsafe ArchetypeRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref worldPointer->GetEntityArchetypePtr(id).Ref;
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
            if (id == 0) return false;
            return World.Get(worldIndex).unsafeWorldPtr.Ptr->entities.ElementAt(id).id != 0;
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

        public static bool Has(this in Entity entity, int componentIndex)
        {
            return entity.ArchetypeRef.Has(componentIndex);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        [BurstCompile]
        public static ref T Get<T>(this in Entity entity) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Data;
            ref var arch = ref entity.ArchetypeRef;
            if (arch.Has(componentType.index))
            {
                // var loc = entity.worldPointer->entityLocations.Ptr[entity.id];
                // var ptr = arch.GetComponentDataPtr(componentType, loc.row);
                return ref arch.GetComponent<T>(entity.id, componentType.size, componentType.index);
            }
            throw new Exception($"Entity {entity.id} does not have a component of type {typeof(T).Name}");
        }

        [BurstCompile]
        public static ref T Get<T>(this Entity entity) where T : unmanaged, IPoolComponent
        {
            return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ref T TryGet<T>(this in Entity entity, out bool exist) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Index;
            exist = entity.ArchetypeRef.Has(componentType);
            if (exist)
            {
                var loc = entity.worldPointer->entityLocations.Ptr[entity.id];
                var ptr = entity.ArchetypeRef.GetComponentDataPtr(componentType, loc.row);
                return ref *(T*)ptr;
            }
            return ref *(T*)null;
        }

        public static ref T TryGet<T>(this Entity entity, out bool exist) where T : unmanaged, IPoolComponent
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
        [BurstCompile]
        public static void Add<T>(this in Entity entity, in T component) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Index;
            if (entity.ArchetypeRef.Has(componentType)) return;
            entity.worldPointer->ECB.Add(entity.id, component);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Add<T>(this Entity entity, in T component) where T : unmanaged, IPoolComponent
        {
            var componentType = ComponentType<T>.Index;
            if (entity.ArchetypeRef.Has(componentType)) return;
            entity.worldPointer->GetPool<T>().Set(entity.id, in component);
            entity.worldPointer->ECB.Add<T>(entity.id);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Add<T>(this in Entity entity) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Index;
            if (entity.ArchetypeRef.Has(componentType)) return;
            entity.worldPointer->ECB.Add(entity.id, componentType);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Add<T>(this Entity entity) where T : unmanaged, IPoolComponent
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
        public static void Set<T>(this in Entity entity, in T component) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Index;
            if (!entity.ArchetypeRef.Has(componentType)) return;
            ref var arch = ref entity.ArchetypeRef;
            var loc = entity.worldPointer->entityLocations.Ptr[entity.id];
            var ptr = arch.GetComponentDataPtr(componentType, loc.row);
            if (ptr != null)
                *(T*)ptr = component;
        }

        public static void Set<T>(this Entity entity, in T component) where T : unmanaged, IPoolComponent
        {
            entity.worldPointer->GetPool<T>().Set(entity.id, in component);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void AddBytes(this in Entity entity, byte[] component, int componentIndex)
        {
            if (entity.ArchetypeRef.Has(componentIndex)) return;
            var ctData = ComponentTypeMap.GetComponentType(componentIndex);
            if (ctData.storageType == StorageType.Pool)
                entity.worldPointer->GetUntypedPool(componentIndex).WriteBytes(entity.id, component);
            entity.worldPointer->ECB.Add(entity.id, componentIndex);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void AddBytesUnsafe(this in Entity entity, byte* component, int sizeInBytes,
            int componentIndex)
        {
            if (entity.ArchetypeRef.Has(componentIndex)) return;
            var ctData = ComponentTypeMap.GetComponentType(componentIndex);
            if (ctData.storageType == StorageType.Pool)
                entity.worldPointer->GetUntypedPool(componentIndex).WriteBytesUnsafe(entity.id, component, sizeInBytes);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void AddObject(this ref Entity entity, IComponent component)
        {
            var ctData = ComponentTypeMap.GetComponentType(component.GetType());
            
            if (entity.ArchetypeRef.Has(ctData.index)) return;
            ref var ecb = ref entity.worldPointer->ECB;
            if (ctData.storageType == StorageType.Pool)
            {
                entity.worldPointer->GetUntypedPool(ctData.index).AddObject(entity.id, component);
                ecb.Add(entity.id, ctData.index);
                return;
            }
            ecb.AddObject(entity.id, component, ctData);
        }
        
        public static void SetObject(this in Entity entity, IComponent component)
        {
            var ctData = ComponentTypeMap.GetComponentType(component.GetType());
            if (ctData.storageType == StorageType.Pool)
            {
                entity.worldPointer->GetUntypedPool(ctData.index).SetObject(entity.id, component);
                return;
            }
            if (!entity.ArchetypeRef.Has(ctData.index)) return;
            var loc = entity.worldPointer->entityLocations.Ptr[entity.id];
            var ptr = entity.ArchetypeRef.GetComponentDataPtr(ctData.index, loc.row);
            if (ptr != null)
                ComponentHelpers.Write(ptr, 0, ctData.size, ctData.index, component);
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
        public static ref readonly T Read<T>(this ref Entity entity) where T : unmanaged, IComponent
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
        public static ValueTuple<T1, T2> Read<T1, T2>(this ref Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return (
                entity.Get<T1>(),
                entity.Get<T2>()
            );
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ComponentTupleRO<T1, T2, T3> Read<T1, T2, T3>(this ref Entity entity)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            return new ComponentTupleRO<T1, T2, T3>(
                in entity.Get<T1>(),
                in entity.Get<T2>(),
                in entity.Get<T3>());
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
        public static void Destroy(this in Entity entity)
        {
#if NUKECS_DEBUG
            entity.worldPointer->AddComponentChange(new World.ComponentChange
            {
                command = EntityCommandBuffer.ECBCommand.Type.DestroyEntity,
                entityId = entity.id,
                timeStamp = entity.worldPointer->timeData.ElapsedTime
            });
#endif
            entity.Add(new DestroyEntity());
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void DestroyNow(this in Entity entity)
        {
            ref var ecb = ref entity.worldPointer->ECB;
#if NUKECS_DEBUG
            entity.worldPointer->AddComponentChange(new World.ComponentChange
            {
                command = EntityCommandBuffer.ECBCommand.Type.DestroyEntity,
                entityId = entity.id,
                timeStamp = entity.worldPointer->timeData.ElapsedTime
            });
#endif
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

        internal static string ToDebugString(this in Entity entity)
        {
            return $"#:{entity.id:D7}";
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetArchetypeHash(this in Entity entity)
        {
            return entity.world.UnsafeWorldRef.entityLocations.Ptr[entity.id].archetypeIndex;
        }
    }

    public ref struct EntityIndex
    {
        public int chunk;
        public int component;
    }
    
}