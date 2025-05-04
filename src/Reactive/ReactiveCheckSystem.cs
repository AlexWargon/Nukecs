using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactive
{
    // [BurstCompile]
    // public unsafe struct ReactiveCheckSystem<T> : IJobSystem, IOnCreate where T : unmanaged, IComponent, IReactive
    // {
    //     private Query compare;
    //     public void OnCreate(ref World world)
    //     {
    //         compare = world.Query().With<T>().With<Reactive<T>>();
    //     }
    //     public void OnUpdate(ref State state)
    //     {
    //         if(compare.Count > 0)
    //             foreach (ref var entity in compare)
    //             {
    //                 ref var c = ref entity.Get<T>();
    //                 ref var cOld = ref entity.Get<Reactive<T>>();
    //                 if(UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref c), UnsafeUtility.AddressOf(ref cOld.oldValue), UnsafeUtility.SizeOf<T>()) != 0)
    //                 {
    //                     entity.Add<Changed<T>>();
    //                     cOld.oldValue = c;
    //                 }
    //             }
    //     }
    // }
    public unsafe struct ReactiveCheckSystemPointerReflectionSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;
        private int componentIndex;
        private int reactiveGenericIndex;
        private int componentChangedTagIndex;
        private long sizeOfComponent;
        private GenericPool componentPool;
        private GenericPool reactiveGenericPool;
        public ReactiveCheckSystemPointerReflectionSystem(int componentIndexToCheck, int reactiveGenericIndexToCheck, int componentChangedTag, long componentSize, ref World world)
        {
            componentIndex = componentIndexToCheck;
            reactiveGenericIndex = reactiveGenericIndexToCheck;
            componentChangedTagIndex = componentChangedTag;
            sizeOfComponent = componentSize;
            componentPool = world.UnsafeWorld->GetUntypedPool(componentIndex);
            reactiveGenericPool = world.UnsafeWorld->GetUntypedPool(reactiveGenericIndex);
        }
        public Query GetQuery(ref World world)
        {
            return world.Query().With(componentIndex).With(reactiveGenericIndex);
        }

        public void OnUpdate(ref Entity entity, ref State state)
        {
            var component = componentPool.UnsafeGetPtr(entity.id);
            var reactiveComponent = reactiveGenericPool.UnsafeGetPtr(entity.id);
            if(UnsafeUtility.MemCmp(component, reactiveComponent, sizeOfComponent) != 0)
            {
                entity.AddByComponentIndex(componentChangedTagIndex);
                dbug.log("changed");
            }
            
        }
    }

    public unsafe struct ReactiveCheckSystem<T> : IEntityJobSystem where T : unmanaged, IComponent, IReactive
    {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<T>().With<Reactive<T>>();
        }
        public void OnUpdate(ref Entity entity, ref State state)
        {
            ref var c = ref entity.Get<T>();
            ref var cOld = ref entity.Get<Reactive<T>>();
            if(UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref c), UnsafeUtility.AddressOf(ref cOld.oldValue), UnsafeUtility.SizeOf<T>()) != 0)
            {
                entity.Add<Changed<T>>();
                cOld.oldValue = c;
            }
        }
    }
    public static class SystemsExtensions
    {
        public static Systems AddReactive<T>(this Systems systems) where T : unmanaged, IComponent, IReactive
        {
            // var reactiveCheckSystem = new ReactiveCheckSystemPointerReflectionSystem(
            //     ComponentType<T>.Index, 
            //     ComponentType<Reactive<T>>.Index,
            //     ComponentType<Changed<T>>.Index, 
            //     ComponentType<T>.Data.size, 
            //     ref systems.world);

            systems.Add<ReactAndClearSystem<T>>().Add<ReactiveCheckSystem<T>>();
            return systems;
        }
    }
}