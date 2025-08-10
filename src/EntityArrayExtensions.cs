using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;

namespace Wargon.Nukecs
{
    [BurstCompile]
    public static unsafe class EntityArrayExtensions
    {
        /// <summary>
        ///     <para>!!!WARNING!!!</para>
        ///     <para>Use 'ref' keyword!</para>
        /// </summary>
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ref ComponentArray<T> GetArray<T>(this ref Entity entity, int sizeToCreate = 6,
            Allocator allocator = Allocator.Persistent) where T : unmanaged, IArrayComponent
        {
            if (!entity.ArchetypeRef.Has<ComponentArray<T>>()) throw NoComponentException<T>();
            ref var pool = ref entity.worldPointer->GetPool<ComponentArray<T>>();
            return ref pool.GetRef<ComponentArray<T>>(entity.id);
        }

        /// <summary>
        ///     <para>!!!WARNING!!!</para>
        ///     <para>Use 'ref' keyword!</para>
        /// </summary>
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ref ComponentArray<T> GetOrCreateArray<T>(this ref Entity entity)
            where T : unmanaged, IArrayComponent
        {
            if (!entity.ArchetypeRef.Has<ComponentArray<T>>()) return ref AddArray<T>(ref entity);
            ref var pool = ref entity.worldPointer->GetPool<ComponentArray<T>>();

            return ref pool.GetRef<ComponentArray<T>>(entity.id);
        }
        /// <summary>
        ///     <para>Use 'ref' keyword!</para>
        /// </summary>
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static ref ComponentArray<T> AddArray<T>(this ref Entity entity) where T : unmanaged, IArrayComponent
        {
            var poolIndex = ComponentType<ComponentArray<T>>.Index;
            entity.ArchetypeRef.OnEntityChangeECB(entity.id, poolIndex);
            ref var pool = ref entity.worldPointer->GetPool<ComponentArray<T>>();
            var elementIndex = poolIndex + 1;
            ref var elementPool = ref entity.worldPointer->GetElementUntypedPool(elementIndex);
            var array = new ComponentArray<T>(ref elementPool, entity);
            pool.Set(entity.id, in array);
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Add<ComponentArray<T>>(entity.id);
            return ref pool.GetRef<ComponentArray<T>>(entity.id);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void RemoveArray<T>(this ref Entity entity) where T : unmanaged, IArrayComponent
        {
            ref var pool = ref entity.worldPointer->GetPool<ComponentArray<T>>();
            ref var buffer = ref pool.GetRef<ComponentArray<T>>(entity.id);
            buffer.Dispose();
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Remove<ComponentArray<T>>(entity.id);
        }
        
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static bool HasArray<T>(this in Entity entity) where T : unmanaged, IArrayComponent
        {
            return entity.ArchetypeRef.Has<ComponentArray<T>>();
        }
        
        [BurstDiscard]
        private static Exception NoComponentException<T>()
        {
            return new NoComponentException($"Entity has no component array {typeof(T).Name}");
        }
    }
}