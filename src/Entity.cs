using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe struct Entity {
        public readonly int id;
        internal readonly World.WorldImpl* _world;
        internal Archetype.ArchetypeImpl* archetype;
        public ref World World => ref Nukecs.World.Get(_world->Id);
        internal Entity(int entity, World.WorldImpl* world) {
            this.id = entity;
            this._world = world;
            this.archetype = _world->GetArchetype(0).impl;
        }

        public ref T Get<T>() where T : unmanaged {
            return ref _world->GetPool<T>().GetRef<T>(id);
        }


        public bool Has<T>() where T : unmanaged {
            return archetype->Has(ComponentMeta<T>.Index);
        }
    }

    public static unsafe class EntityExt {
        public static void Add<T>(this ref Entity entity, T component) where T : unmanaged {
            entity.archetype->OnEntityChange(ref entity, ComponentMeta<T>.Index);
            entity._world->GetPool<T>().Set(entity.id, component);
        }

        static void AddWithBuffer<T>(this ref Entity entity, T ptr) where T : unmanaged {
            ref var ecb = ref entity._world->ECB;
            ecb.Add(entity.id, ptr);
        }
        // public void Remove<T>() where T : unmanaged {
        //     archetype->OnEntityChange(ref id, -ComponentMeta<T>.Index);
        // }
    }

    public static unsafe class Unsafe {
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
    }

    public interface IComponent {
        private static int count = -1;
        [RuntimeInitializeOnLoadMethod]
        public static int Count() {
            if (count != -1) return count;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    if (typeof(IComponent).IsAssignableFrom(type)) {
                        count++;
                    }
                }
            }
            return count;
        }
    }
    
    public struct Component {
        public static int Count;
    }
    public struct ComponentMeta<T> where T: unmanaged {
        public static int Index;

        static ComponentMeta() {
            Index = Component.Count++;
            CTS<T>.ID.Data = Index;
        }
    }

    public unsafe struct GenericPool : IDisposable {
        internal Impl* impl;
        public bool IsCreated;
        public static GenericPool Create<T>(int size, Allocator allocator) where T : unmanaged {

            return new GenericPool() {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
        }
        public static GenericPool* CreatePtr<T>(int size, Allocator allocator) where T : unmanaged {
            var ptr = (GenericPool*)UnsafeUtility.Malloc(sizeof(GenericPool), UnsafeUtility.AlignOf<GenericPool>(), allocator);
            *ptr =  new GenericPool {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
            return ptr;
        }
        internal struct Impl {
            internal byte* buffer;
            internal int elementSize;
            internal int count;
            internal int capacity;
            internal Allocator allocator;
            internal static Impl* CreateImpl<T>(int size, Allocator allocator) where T : unmanaged {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);
                *ptr = new Impl {
                    elementSize = sizeof(T),
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*)UnsafeUtility.Malloc(sizeof(T) * size, UnsafeUtility.AlignOf<T>(), allocator)
                };
                return ptr;
            }

        }
        public void Set<T>(int index, T value) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }

            *(T*) (impl->buffer + index * impl->elementSize) = value;
            if (index >= impl->count) {
                impl->count = index + 1;
            }
        }
        public ref T GetRef<T>(int index) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            return ref *(T*)(impl->buffer + index * impl->elementSize);
        }

        public void Dispose() {
            if(impl == null) return;
            var allocator = impl->allocator;
            UnsafeUtility.Free(impl->buffer, allocator);
            UnsafeUtility.Free(impl, allocator);
            IsCreated = false;
        }
    }
    /// <summary>
    ///     Component Type Shared
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class CTS<T> where T : struct {
        public static readonly SharedStatic<int> ID;
        static CTS() {
            ID = SharedStatic<int>.GetOrCreate<CTS<T>>();
        }
    }
    public struct DestroyEntity : IComponent{}
    public interface ISystem {
        void OnUpdate(float deltaTime);
    }

    public interface ICreate {
        void OnCreate(ref World world);
    }
    
    public unsafe struct SystemJobRunner<TSystem> : IJobParallelFor where TSystem : ISystem {
        internal TSystem system;
        internal float dt;
        public void Execute(int index) {
            
            system.OnUpdate(dt);
        }
    }

    internal unsafe interface ISystemRunner {
        JobHandle OnUpdate(ref World worldImpl, ref JobHandle jobHandle);
    }
    internal class SystemRunner<TSystem> : ISystemRunner where TSystem : ISystem {
        public SystemJobRunner<TSystem> runner;
        public ECBJob ecbJob;

        public JobHandle OnUpdate(ref World worldImpl, ref JobHandle jobHandle) {
            jobHandle = runner.Schedule(1,1, jobHandle);
            ecbJob.world = worldImpl;
            ecbJob.ECB = worldImpl.ecb;
            return ecbJob.Schedule(jobHandle);
        }
    }
    
    public unsafe struct Systems {
        internal JobHandle dependencies;
        private List<ISystemRunner> runners;
        private World world;
        public Systems(World world) {
            this.dependencies = default;
            this.runners = new List<ISystemRunner>();
            this.world = world;
        }

        public Systems Add<T>() where T : struct, ISystem{
            runners.Add(new SystemRunner<T>() {
                runner = new SystemJobRunner<T>() {
                    system = default,
                },
                ecbJob = default
            });
            return this;
        }

        public void OnUpdate(float dt) {
            for (var i = 0; i < runners.Count; i++) {
                dependencies = runners[i].OnUpdate(ref world, ref dependencies);
            }
        }

    }

    public struct ECBJob : IJob {
        public EntityCommandBuffer ECB;
        public World world;
        public void Execute() {
            ECB.PerformCommand(ref world);
        }
    }
}

