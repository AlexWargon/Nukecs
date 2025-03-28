﻿using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs {
    public partial struct World {
        public unsafe struct Components {
            internal MemoryList<GenericPool> pools;
            internal int poolsCount;
            internal MemoryList<int> defaultNoneTypes;

            internal Components(ref WorldConfig config, WorldUnsafe* world) {
                this.pools = new MemoryList<GenericPool>(ComponentAmount.Value.Data + 1, ref world->AllocatorRef, true);
                poolsCount = 0;
                this.defaultNoneTypes = new MemoryList<int>(12, ref world->AllocatorRef);
                InitializeDefaultComponents();
                CreatePools(ref config, world);
            }

            private void InitializeDefaultComponents() {
                _ = ComponentType<DestroyEntity>.Index;
                _ = ComponentType<EntityCreated>.Index;
                _ = ComponentType<IsPrefab>.Index;
            }
            internal void CreatePools(ref WorldConfig config, WorldUnsafe* world)
            {
                ComponentTypeMap.CreatePools(ref pools, config.StartPoolSize, world, ref poolsCount);
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

    public struct AspectType
    {
        public static readonly SharedStatic<int> Count = SharedStatic<int>.GetOrCreate<AspectType>();

        static AspectType()
        {
            Count.Data = 0;
        }
    }
    internal struct AspectType<T> where T : unmanaged, IAspect<T>, IAspect
    {
        public static readonly SharedStatic<int> Index = SharedStatic<int>.GetOrCreate<AspectType<T>>();

        static AspectType()
        {
            Index.Data = AspectType.Count.Data++;
        }
    }
    
}