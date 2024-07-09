using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Wargon.Nukecs {
    public unsafe struct Entity {
        public readonly int id;
        [NativeDisableUnsafePtrRestriction]
        internal readonly World.WorldImpl* world;
        [NativeDisableUnsafePtrRestriction]
        internal Archetype.ArchetypeImpl* archetype;
        public ref World World => ref Nukecs.World.Get(world->Id);
        internal Entity(int entity, World.WorldImpl* world) {
            this.id = entity;
            this.world = world;
            this.archetype = this.world->GetArchetype(0).impl;
        }

        public ref T Get<T>() where T : unmanaged {
            return ref world->GetPool<T>().GetRef<T>(id);
        }

        public bool Has<T>() where T : unmanaged {
            return archetype->Has(ComponentMeta<T>.Index);
        }

        internal ref Archetype.ArchetypeImpl Arch
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *archetype;
        }
    }

    public static unsafe class EntityExt {
        public static void Add<T>(this ref Entity entity, T component) where T : unmanaged {
            //entity.archetype->OnEntityChange(ref entity, ComponentMeta<T>.Index);
            if(entity.Arch.Has<T>()) return;
            entity.world->GetPool<T>().Set(entity.id, component);
            ref var ecb = ref entity.world->ECB;
            ecb.Add<T>(entity.id);
        }

        // internal static void AddPtr<T>(this ref Entity entity, T* ptr) where T : unmanaged {
        //     ref var ecb = ref entity.world->ECB;
        //     ecb.Add(entity.id, ptr);
        // }
        public static void Remove<T>(this ref Entity entity) where T : unmanaged {
            //entity.archetype->OnEntityChange(ref entity, -ComponentMeta<T>.Index);
            if(entity.Arch.Has<T>() == false) return;
            entity.world->GetPool<T>().Set(entity.id, default(T));
            ref var ecb = ref entity.world->ECB;
            ecb.Remove<T>(entity.id);
        }
    }

    public static unsafe class Unsafe {
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
    }

    public interface IComponent {
        private static int count = -1;

        [RuntimeInitializeOnLoadMethod]
        public static void Initialization()
        {
            Count();
        }
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
            ComponentsMap.Add(typeof(T), UnsafeUtility.AlignOf<T>(), Index);
        }
    }

    public static class ComponentsMap
    {
        private static readonly Dictionary<Type, int> Aligns = new();
        private static readonly Dictionary<int, Type> TypeByIndex = new();
        private static readonly Dictionary<Type, int> IndexByType = new();
        public static void Add(Type type, int align, int index)
        {
            Aligns[type] = align;
            TypeByIndex[index] = type;
            IndexByType[type] = index;
        }

        public static int AlignOf(Type type) => Aligns[type];
        public static Type GetType(int index) => TypeByIndex[index];
        public static int Index(Type type) => IndexByType[type];
    }
    public unsafe struct GenericPool : IDisposable {
        [NativeDisableUnsafePtrRestriction] internal Impl* impl;
        public bool IsCreated;
        public static GenericPool Create<T>(int size, Allocator allocator) where T : unmanaged {

            return new GenericPool() {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
        }
        public static GenericPool Create(Type type ,int size, Allocator allocator) {

            return new GenericPool() {
                impl = Impl.CreateImpl(type, size, allocator),
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
            [NativeDisableUnsafePtrRestriction] internal byte* buffer;
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
            internal static Impl* CreateImpl(Type type, int size, Allocator allocator) {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);
                var typeSize = UnsafeUtility.SizeOf(type);
                *ptr = new Impl {
                    elementSize = typeSize,
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*)UnsafeUtility.Malloc(typeSize, ComponentsMap.AlignOf(type), allocator)
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

        public void SetPtr(int index, void* value)
        {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            
            *(impl->buffer + index * impl->elementSize) = *(byte*)value;
            if (index >= impl->count) {
                impl->count = index + 1;
            }
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
    [BurstCompile]
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
        public Systems(ref World world) {
            this.dependencies = default;
            this.runners = new List<ISystemRunner>();
            this.world = world;
        }

        public Systems Add<T>() where T : struct, ISystem
        {
            T system = default;
            if (system is ICreate s)
            {
                s.OnCreate(ref world);
                system = (T)s;
            }
            runners.Add(new SystemRunner<T> {
                runner = new SystemJobRunner<T> {
                    system = system,
                },
                ecbJob = default
            });
            return this;
        }

        public void OnUpdate(float dt) {
            dependencies.Complete();
            for (var i = 0; i < runners.Count; i++) {
                dependencies = runners[i].OnUpdate(ref world, ref dependencies);
            }
        }
    }
    [BurstCompile]
    public struct ECBJob : IJob {
        public EntityCommandBuffer ECB;
        public World world;
        public void Execute() {
            ECB.PerformCommand(ref world);
        }
    }
    
    [JobProducerType(typeof(JobSystemExt.JobSystem<>))]
    public interface IJobSystem
    {
        void OnUpdate(ref World world, float deltaTime);
    }

    public static class JobSystemExt
    {
        public struct JobSystem<TJob> where TJob : struct, IJobSystem
        {
            public World World;
            public TJob JobData;
            internal static readonly SharedStatic<IntPtr> jobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<JobSystem<TJob>>();
            public static void Execute(ref JobSystem<TJob> fullData, IntPtr additionalPtr, IntPtr bufferRangePatchData,ref JobRanges ranges, int jobIndex)
            {
                //fullData.JobData.OnUpdate();
            }
        }

    }
}

