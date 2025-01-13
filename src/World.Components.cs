using System;
using Unity.Burst;
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
    public partial struct World
    {
        internal unsafe partial struct WorldUnsafe
        {
            internal Aspects _aspects;
            internal T* GetAspect<T>() where T : unmanaged, IAspect<T>, IAspect
            {
                var index = AspectType<T>.Index.Data;
                T* aspect = (T*)_aspects.aspects.Ptr[index];
                if (aspect == null)
                {
                    aspect = AspectBuilder<T>.CreatePtr(ref *_aspects.world);
                    _aspects.aspects.Ptr[index] = (IntPtr)aspect;
                }
                return aspect;
            }

        }
        public unsafe ref T GetAspect<T>(in Entity entity) where T : unmanaged, IAspect<T>, IAspect
        {
            var aspect = this.UnsafeWorld->GetAspect<T>();
            aspect->Entity = entity;
            return ref *aspect;
        }
        public unsafe struct Aspects : IDisposable
        {
            internal UnsafeList<IntPtr> aspects;
            internal Allocator allocator;
            internal World* world;
            internal Aspects(Allocator allocator, int world)
            {
                this.aspects = UnsafeHelp.UnsafeListWithMaximumLenght<IntPtr>(64, allocator, NativeArrayOptions.ClearMemory);
                this.allocator = allocator;
                this.world = GetPtr(world);
            }


            public void Dispose()
            {
                foreach (var intPtr in aspects)
                {
                    Unsafe.FreeTracked((void*)intPtr, allocator);
                }

                aspects.Dispose();
            }
        }
        
    }
}