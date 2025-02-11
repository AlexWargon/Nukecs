using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs
{
    public partial struct World
    {
        internal unsafe partial struct WorldUnsafe
        {
            internal Aspects aspects;
            internal T* GetAspect<T>() where T : unmanaged, IAspect<T>, IAspect
            {
                var index = AspectType<T>.Index.Data;
                var aspect = (T*)aspects.aspects.Ptr[index];
                if (aspect == null)
                {
                    aspect = AspectBuilder<T>.CreatePtr(ref *aspects.world);
                    aspects.aspects.Ptr[index] = (IntPtr)aspect;
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
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<IntPtr> aspects;
            internal T* GetAspect<T>() where T : unmanaged, IAspect, IAspect<T>
            {
                var index = AspectType<T>.Index.Data;
                var aspect = (T*)aspects.Ptr[index];
                if (aspect == null)
                {
                    aspect = AspectBuilder<T>.CreatePtr(ref *world);
                    aspects.Ptr[index] = (IntPtr)aspect;
                }
                return aspect;
            }
            internal readonly Allocator allocator;
            internal readonly World* world;
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
                    world->UnsafeWorld->_free((void*)intPtr);
                }
                aspects.Dispose();
            }
        }
    }
}