using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;

namespace Wargon.Nukecs
{
    [Serializable,StructLayout(LayoutKind.Sequential)][BurstCompile]
    public unsafe struct Entity : IEquatable<Entity>
    {
        public int id;
        public ushort Generation { get; internal set; }
        public ushort WorldToken { get; internal set; }
        internal byte worldIndex => (byte)(WorldToken & (World.MAX_WORLD_COUNT - 1));

        public World.WorldUnsafe* worldPointer {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (!IsValid()) throw new InvalidOperationException("The entity handle is no longer alive.");
                return World.Get(worldIndex).UnsafeWorld;
            }
        }

        public ref World world => ref World.Get(worldPointer->Id);
        public static readonly Entity Null = default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(int id, byte world) : this(id, World.Get(world).UnsafeWorld) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity(int id, World.WorldUnsafe* worldPointer)
        {
            this.id = id;
            var previous = worldPointer->entities.Ptr[id].Generation;
            if (previous == ushort.MaxValue) throw new InvalidOperationException("Entity generation exhausted.");
            Generation = (ushort)(previous + 1);
            WorldToken = worldPointer->entityWorldToken;
        }
        internal ref ArchetypeUnsafe ArchetypeRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref worldPointer->GetEntityArchetypePtr(id).Ref;
        }

        public override string ToString()
        {
            return $"e:{id}:{Generation}";
        }


#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        // Identity remains stable after destruction, arena relocation and ID reuse.
        public bool Equals(Entity other)
        {
            return id == other.id && WorldToken == other.WorldToken && Generation == other.Generation;
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
            return HashCode.Combine(id, WorldToken, Generation);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static bool operator ==(in Entity one, in Entity two)
        {
            return one.Equals(two);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static bool operator !=(in Entity one, in Entity two)
        {
            return !one.Equals(two);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public bool IsValid()
        {
            if (id <= 0 || Generation == 0 || WorldToken == 0) return false;
            ref var currentWorld = ref World.Get(worldIndex);
            if (!currentWorld.IsAlive) return false;
            var w = currentWorld.UnsafeWorld;
            return w->entityWorldToken == WorldToken && id < w->lastEntityIndex && w->entities.Ptr[id].id == id
                && w->entities.Ptr[id].Generation == Generation;
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
            return entity.IsValid() && entity.ArchetypeRef.Has<T>();
        }

        public static bool Has(this in Entity entity, int componentIndex)
        {
            return entity.IsValid() && entity.ArchetypeRef.Has(componentIndex);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile]
        public static ref T Get<T>(this in Entity entity) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Data;
            ref var loc = ref entity.worldPointer->entityLocations.Ptr[entity.id];
            ref var arch = ref entity.worldPointer->archetypesList.Ptr[loc.archetypeIndex].Ref;
            // Generic callers constrained to IComponent also accept IPoolComponent.
            if (componentType.category == ComponentCategory.Pool && arch.Has(componentType.index))
                return ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id);
            if (componentType.category == ComponentCategory.Tag)
            {
                if (arch.Has(componentType.index))
                    return ref *TagSlotStub<T>.GetPtr(); // tags carry no data
                throw new Exception($"Entity {entity.id} does not have a component of type {typeof(T).Name}");
            }
            if (arch.offsetMap.Mask.HasFast(componentType.index))
            {
                var off = arch.offsetMap.GetRef(componentType.index);
                return ref *(T*)(arch.data.Ptr + off + loc.row * componentType.size);
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
            if (!entity.IsValid()) { exist = false; return ref *(T*)null; }
            var componentType = ComponentType<T>.Index;
            exist = entity.ArchetypeRef.Has(componentType);
            if (exist)
            {
                if (ComponentType<T>.Data.category == ComponentCategory.Tag)
                    return ref *TagSlotStub<T>.GetPtr(); // tags carry no data
                var loc = entity.worldPointer->entityLocations.Ptr[entity.id];
                var ptr = entity.ArchetypeRef.GetComponentDataPtr(componentType, loc.row);
                return ref *(T*)ptr;
            }
            return ref *(T*)null;
        }

        public static ref T TryGet<T>(this Entity entity, out bool exist) where T : unmanaged, IPoolComponent
        {
            if (!entity.IsValid()) { exist = false; return ref *(T*)null; }
            exist = entity.ArchetypeRef.Has(ComponentType<T>.Index);
            return ref (exist ? ref entity.worldPointer->GetPool<T>().GetRef<T>(entity.id) : ref *(T*)null);
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
            entity.worldPointer->ECB.Add(entity.id, component);
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
            var componentType = ComponentType<T>.Data;
            ref var arch = ref entity.ArchetypeRef;
            if (!arch.Has(componentType.index)) return;
            if (componentType.storageType == StorageType.Pool)
            {
                entity.worldPointer->GetPool<T>().Set(entity.id, component);
                return;
            }
            var loc = entity.worldPointer->entityLocations.Ptr[entity.id];
            var ptr = arch.GetComponentDataPtr(componentType.index, loc.row);
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
            fixed (byte* data = component)
                entity.worldPointer->ECB.AddBytes(entity.id, data, componentIndex);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static void AddBytesUnsafe(this in Entity entity, byte* component, int sizeInBytes,
            int componentIndex)
        {
            if (entity.ArchetypeRef.Has(componentIndex)) return;
            entity.worldPointer->ECB.AddBytes(entity.id, component, componentIndex);
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
                ecb.AddObject(entity.id, component, ctData);
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
        public static ref readonly T Read<T>(this in Entity entity) where T : unmanaged, IComponent
        {
            var componentType = ComponentType<T>.Data;
            ref var loc = ref entity.worldPointer->entityLocations.Ptr[entity.id];
            ref var arch = ref entity.worldPointer->archetypesList.Ptr[loc.archetypeIndex].Ref;
            return ref *(T*)(arch.data.Ptr + arch.offsetMap.GetRef(componentType.index) + loc.row * componentType.size);
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
            if (!entity.IsValid()) return;
#if NUKECS_DEBUG
            entity.worldPointer->AddComponentChange(new World.ComponentChange
            {
                command = EntityCommandBuffer.ECBCommand.Type.DestroyEntity,
                entityId = entity.id,
                timeStamp = entity.worldPointer->timeData.ElapsedTime
            });
#endif
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Destroy(entity.id);
            //entity.Add(new DestroyEntity());
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        /// <summary>
        /// Immediately removes this entity and disposes its installed components. Pending
        /// ECB commands expire by generation and are cleaned up on playback. Requires exclusive
        /// world access: complete outstanding jobs and do not call during query iteration.
        /// </summary>
        public static void DestroyNow(this in Entity entity)
        {
            if (!entity.IsValid()) return;
            var world = entity.worldPointer;
#if NUKECS_DEBUG
            entity.worldPointer->AddComponentChange(new World.ComponentChange
            {
                command = EntityCommandBuffer.ECBCommand.Type.DestroyEntity,
                entityId = entity.id,
                timeStamp = entity.worldPointer->timeData.ElapsedTime
            });
#endif
            var entityId = entity.id;
            var loc = world->entityLocations.Ptr[entityId];
            ref var arch = ref world->archetypesList.Ptr[loc.archetypeIndex].Ref;
            for (var i = 0; i < arch.types.length; i++) {
                var type = arch.types.Ptr[i];
                if (ComponentTypeMap.GetComponentType(type).storageType == StorageType.Pool)
                    world->GetUntypedPool(type).Remove(entityId);
            }
            if (loc.archetypeIndex != 0) {
                arch.DestroyEntity(loc.row);
                arch.ExecuteDestroyEdge(entityId);
            }
            world->OnDestroyEntity(entityId);
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
        /// <summary>Creates an empty entity and queues an ECB copy of every component from
        /// <paramref name="entity"/> into it (data lands on ECB playback).</summary>
        public static Entity CopyViaECB(this in Entity entity)
        {
            var e = entity.worldPointer->CreateEntity();
            entity.worldPointer->ECB.Copy(entity.id, e.id);
            return e;
        }

        internal static string ToDebugString(this in Entity entity)
        {
            return $"#:{entity.id:D7}";
        }

        /// <summary>Index of the entity's current logical archetype in world.archetypesList
        /// (historical name says Hash but it is an archetype index, not a hash).</summary>
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
