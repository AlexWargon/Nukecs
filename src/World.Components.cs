using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public partial struct World {
        public unsafe struct Components {
            internal UnsafeList<GenericPool> pools;
            internal int poolsCount;
            internal UnsafeList<int> defaultNoneTypes;

            public Components(ref WorldConfig config, Allocator allocator) {
                this.pools = UnsafeHelp.UnsafeListWithMaximumLenght<GenericPool>(ComponentAmount.Value.Data + 1, allocator,
                    NativeArrayOptions.ClearMemory);
                poolsCount = 0;
                this.defaultNoneTypes = new UnsafeList<int>(12, allocator, NativeArrayOptions.ClearMemory);
                InitializeDefaultComponents();
                CreatePools(ref config, allocator);
            }

            private void InitializeDefaultComponents() {
                _ = ComponentType<DestroyEntity>.Index;
                _ = ComponentType<EntityCreated>.Index;
                _ = ComponentType<IsPrefab>.Index;
            }
            internal void CreatePools(ref WorldConfig config, Allocator allocator)
            {
                ComponentTypeMap.CreatePools(ref pools, config.StartPoolSize, allocator, ref poolsCount);
            }
            public void Dispose() {
                var poolsToDispose = ComponentAmount.Value.Data;
                for (var index = 0; index < poolsToDispose; index++) {
                    
                    ref var pool = ref pools.Ptr[index];
                    pool.Dispose();
                }
                pools.Dispose();
            }
        }
    }
}