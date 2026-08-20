using System;
using Unity.Burst;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public partial struct World
    {
        public unsafe partial struct WorldUnsafe
        {
            internal Aspects aspects;
            internal T* GetAspect<T>() where T : unmanaged, IAspect<T>, IAspect
            {
                var index = AspectType<T>.Index.Data;
                var aspect = aspects.aspects.Ptr[index].As<T>();
                if (aspect == null)
                {
                    var ptr = AspectBuilder<T>.CreatePtr(ref *aspects.world);
                    aspect = ptr.Ptr;
                    aspects.aspects.Ptr[index] = ptr.UntypedPointer;
                }
                
                return aspect;
            }
        }
        public unsafe ref T GetAspect<T>(ref Entity entity) where T : unmanaged, IAspect<T>, IAspect
        {
            var aspect = UnsafeWorld->aspects.GetAspect<T>();
            aspect->Update(ref entity);
            return ref *aspect;
        }
        public unsafe struct Aspects : IDisposable
        {
            internal MemoryList<ptr> aspects;
            internal T* GetAspect<T>() where T : unmanaged, IAspect, IAspect<T>
            {
                var index = AspectType<T>.Index.Data;
                var aspect = aspects[index].As<T>();
                if (aspect == null)
                {
                    var ptr = AspectBuilder<T>.CreatePtr(ref *world);
                    aspect = ptr.Ptr;
                    aspects.Ptr[index] = ptr.UntypedPointer;
                }
                return aspect;
            }
            internal readonly World* world;
            internal Aspects(ref MemAllocator allocator, int world)
            {
                this.aspects = new MemoryList<ptr>(64, ref allocator, true);
                this.world = GetPtr(world);
            }

            public void Dispose()
            {
                foreach (var intPtr in aspects)
                {
                    world->UnsafeWorld->_free(intPtr.offset.Offset);
                }
                aspects.Dispose();
            }

            public void OnDeserialize(ref MemAllocator allocator) {
                aspects.OnDeserialize(ref allocator);
                for (int i = 0; i < aspects.length; i++) {
                    ref var p = ref aspects[i];
                    p.OnDeserialize(ref allocator);
                }
            }
        }
    }

    public struct Rollbacks
    {
        private byte[][] rollbacks;

        public Rollbacks(int length)
        {
            rollbacks = new byte[length][];
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