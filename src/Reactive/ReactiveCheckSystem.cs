using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactive
{
    [BurstCompile]
    public unsafe struct ReactiveCheckSystem<T> : IJobSystem, IOnCreate where T : unmanaged, IComponent, IReactive
    {
        private Query compare;
        public void OnCreate(ref World world)
        {
            compare = world.Query().With<T>().With<Reactive<T>>();
        }
        public void OnUpdate(ref State state)
        {
            if(compare.Count > 0)
                foreach (ref var entity in compare)
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
    }
}