using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public unsafe class Systems
    {
        public readonly List<ISystemDestroyer> systemDestroyers;
        public JobHandle Dependencies;
        public readonly List<ISystemRunner> fixedRunners;
        public readonly List<ISystemRunner> runners;
        public readonly List<ISystemRunner> mtFixedRunners;
        public readonly List<ISystemRunner> mtRunners;
        internal SystemsDependencies SystemsDependencies;
        public World World;
        private State _state;
        internal ref State State => ref _state;
        private const float FIXED_UPDATE_INTERVAL = 0.016f;
        private float _timeSinceLastFixedUpdate;
        internal ActionRef<World> onWorldDispose;
        public Systems(ref World world)
        {
            Dependencies = default;
            runners = new List<ISystemRunner>();
            fixedRunners = new List<ISystemRunner>();
            mtRunners = new List<ISystemRunner>();
            mtFixedRunners = new List<ISystemRunner>();
            systemDestroyers = new List<ISystemDestroyer>();
            SystemsDependencies = SystemsDependencies.Create();
            World = world;
            WorldSystems.Add(world.UnsafeWorld->Id, this);
        }

        private static Marker _allSystems = new("ALL SYSTEMS");
        public void OnUpdate(float dt, float time)
        {
            _allSystems.Start();

            _state.Dependencies = World.DependenciesUpdate;
            _state.World = World;
            _state.Time.DeltaTime = dt;
            _state.Time.Time = time;
            _state.Time.ElapsedTime += dt;
            _state.Time.TickCount++;
            _state.Time.DeltaTimeFixed = FIXED_UPDATE_INTERVAL;
            World.UnsafeWorld->timeData = _state.Time;
            
            if (mtRunners.Count == 0 && fixedRunners.Count == 0 && mtFixedRunners.Count == 0 && runners.Count == 1)
            {
                _state.Dependencies = runners[0].Schedule(UpdateContext.Update, ref _state);
                _state.Dependencies.Complete();
                _allSystems.End();
                return;
            }
            for (var i = 0; i < mtRunners.Count; i++)
                _state.Dependencies = mtRunners[i].Schedule(UpdateContext.Update, ref _state);
            for (var i = 0; i < runners.Count; i++)
                _state.Dependencies = runners[i].Schedule(UpdateContext.Update, ref _state);

            _timeSinceLastFixedUpdate += dt;
            if (_timeSinceLastFixedUpdate >= FIXED_UPDATE_INTERVAL)
            {
                for (var i = 0; i < mtFixedRunners.Count; i++)
                    _state.Dependencies = mtFixedRunners[i].Schedule(UpdateContext.Update, ref _state);
                for (var i = 0; i < fixedRunners.Count; i++)
                    _state.Dependencies = fixedRunners[i].Schedule(UpdateContext.Update, ref _state);
                _timeSinceLastFixedUpdate = 0;
            }
            _state.Dependencies.Complete();
            _allSystems.End();
        }
        
        public static Systems Default(ref World world)
        {
            return new Systems(ref world).AddDefaults();
        }

        public Systems AddDefaults()
        {
            Add<EntityDestroySystem>();
            Add<OnPrefabSpawnSystem>();
            Add<ClearEntityCreatedEventSystem>();
            return this;
        }

        public Systems RemoveComponent<T>() where T : unmanaged, IComponent
        {
            var system = new RemoveComponentSystem
            {
                Type = ComponentType<T>.Index
            };
            var runner = new EntityJobSystemRunner<RemoveComponentSystem>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref World).queryUnsafe;
            runners.Add(runner);
            return this;
        }

        public Systems Add<T>() where T : struct, IJobSystem
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new JobSystemRunner<T>
            {
                System = system,
                EcbJob = default,
                isComplete = system is IComplete
            };
            if (system is IFixed)
                fixedRunners.Add(runner);
            else
                runners.Add(runner);

            return this;
        }

        internal Systems AddRef<T>(ref T system) where T : struct, IEntityJobSystem
        {
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new EntityJobSystemRunner<T>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref World).queryUnsafe;
            if (system is IFixed)
                fixedRunners.Add(runner);
            else
                runners.Add(runner);
            return this;
        }

        public Systems Add<T>(bool dymmy = false) where T : struct, IEntityJobSystem
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new EntityJobSystemRunner<T>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref World).queryUnsafe;
            if (system is IFixed)
                fixedRunners.Add(runner);
            else
                runners.Add(runner);
            return this;
        }

        public Systems Add<T>(ushort dymmy = 1) where T : unmanaged, IEntityJobSystem, IOnDestroy
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new EntityJobSystemRunner<T>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            systemDestroyers.Add(new SystemDestroyer<T>(ref runner.System));
            runner.Query = runner.System.GetQuery(ref World).queryUnsafe;
            if (system is IFixed)
                fixedRunners.Add(runner);
            else
                runners.Add(runner);
            return this;
        }


        public unsafe Systems Add(delegate*<void> path, params delegate*<void>[] args)
        {
            return this;
        }
        public Systems Add<T>(int dymmy = 1) where T : struct, ISystem
        {
            T system = default;
            if (system is IOnCreate onCreate)
            {
                onCreate.OnCreate(ref World);
                system = (T)onCreate;
            }

            var runner = new SystemMainThreadRunnerStruct<T>
            {
                System = system,
                EcbJob = default
            };

            if (system is IFixed)
            {
                mtFixedRunners.Add(runner);
            }
            else
            if (system is IJobRunner)
            {
                runners.Add(runner);
            }
            else
            {
                mtRunners.Add(runner);
            }
            
            return this;
        }

        public Systems Add<T>(long dymmy = 1) where T : class, ISystem, new()
        {
            var system = new T();
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new SystemMainThreadRunnerClass<T>
            {
                System = system,
                EcbJob = default
            };
            if (system is IFixed)
            {
                mtFixedRunners.Add(runner);
            }
            else
            if (system is IJobRunner)
            {
                runners.Add(runner);
            }
            else
            {
                mtRunners.Add(runner);
            }

            if (system is IOnDestroy onDestroySystem)
            {
                systemDestroyers.Add(new SystemClassDestroyer(onDestroySystem));
            }
            return this;
        }

        public Systems Add<T>(T group) where T : SystemsGroup
        {
            group.world = World;
            runners.AddRange(group.runners);
            fixedRunners.AddRange(group.fixedRunners);
            mtRunners.AddRange(group.mainThreadRunners);
            mtFixedRunners.AddRange(group.mainThreadFixedRunners);
            systemDestroyers.AddRange(group.destroyRunners);
            return this;
        }
        


        internal void Complete()
        {
            _state.Dependencies.Complete();
        }

        internal void OnWorldDeserialize(World.WorldUnsafe* world) {
            RebuildQueryPointers(runners, world);
            RebuildQueryPointers(fixedRunners, world);
            RebuildQueryPointers(mtRunners, world);
            RebuildQueryPointers(mtFixedRunners, world);
        }

        private unsafe void RebuildQueryPointers(List<ISystemRunner> list, World.WorldUnsafe* worldPtr) {
            foreach (var runner in list) {
                if (runner is IQueryHolder holder)
                    holder.UpdateQueryPointer(worldPtr);
                if (runner is ISystemWithDeserialization sysDeser)
                    sysDeser.OnWorldDeserialize(World);
            }
        }

        internal interface ISystemWithDeserialization {
            void OnWorldDeserialize(World world);
        }

        internal void OnWorldDispose()
        {
            Complete();
            onWorldDispose?.Invoke(ref World);
            foreach (var systemDestroyer in systemDestroyers) systemDestroyer.Destroy(ref World);
            SystemsDependencies.Dispose();
        }
        // public void Run(float dt) {
        //     for (var i = 0; i < runners.Count; i++) {
        //         runners[i].Run(ref world, dt);
        //     }
        // }
    }

    public static partial class SystemsExtensions
    {
        public static unsafe Systems Add<T>(this Systems systems, Threads threads)
            where T : struct, IEntityJobSystem
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref systems.World);
                system = (T)s;
            }

            var runner = new EntityJobSystemRunner<T>
            {
                System = system,
                Mode = threads,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref systems.World).queryUnsafe;
            systems.runners.Add(runner);
            return systems;
        }
        public static Systems AddSystem(this Systems systems, Action system)
        {
            return systems;
        }
    }
    public interface IQueryHolder {
        unsafe void UpdateQueryPointer(World.WorldUnsafe* world);
    }

    public enum UpdateContext
    {
        Update,
        FixedUpdate
    }

    public interface ISystemDestroyer
    {
        void Destroy(ref World world);
    }

    public interface ISystemRunner
    {
        JobHandle Schedule(UpdateContext updateContext, ref State state);
        void Run(ref State state);
        string Name { get; }
    }


    internal class GenericSystemMainThreadRunner<TSystem> : ISystemRunner where TSystem : struct, ISystem
    {
        internal ECBJob EcbJob;
        internal TSystem System;
        public string Name => System.GetType().Name;
        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
            System.OnUpdate(ref state);
            EcbJob.ECB = state.World.ECB;
            EcbJob.world = state.World;
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state)
        {
            System.OnUpdate(ref state);
            state.World.ECB.Playback(ref state.World);
        }
    }

    public static unsafe class SystemPath
    {
        public static delegate*<void> OnCreate;
        public static delegate*<void> OnUpdate;
        public static delegate*<void> OnFixedUpdate;
        public static delegate*<void> OnDestroy;
    }
    public enum Threads
    {
        /// <summary>
        /// Execute system on main thread.
        /// In feature Main and MainRun will be same.
        /// </summary>
        Main,
        /// <summary>
        /// Execute system on main thread using Unity Job System Run.
        /// In feature Main and MainRun will be same.
        /// </summary>
        MainRun,
        /// <summary>
        /// Execute system on all parallel threads.
        /// </summary>
        Parallel,
        /// <summary>
        /// Execute system on one non main thread.
        /// </summary>
        Single
    }

    public interface IOnCreate
    {
        void OnCreate(ref World world);
    }

    public interface IOnUpdate {
        void OnUpdate(ref State state);
    }
    public interface IFixed
    {
    }

    public interface IJobRunner
    {
        
    }
    
    public interface IOnDestroy
    {
        void OnDestroy(ref World world);
    }
    public delegate void ActionRef<T>(ref T value) where T : struct;
    public interface IComplete
    {
    }
    public interface ISystem
    {
        void OnUpdate(ref State state);
    }

    public interface IOnWorldDeserialize {
        void OnWorldDeserialize(ref World world);
    }

    [BurstCompile]
    public struct ClearEntityCreatedEventSystem : IEntityJobSystem
    {
        public Threads Mode => Threads.Single;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<EntityCreated>();
        }

        public void OnUpdate(ref Entity entity, ref State state)
        {
            entity.Remove<EntityCreated>();
        }
    }

    
    [BurstCompile]
    internal struct RemoveComponentSystem : IEntityJobSystem
    {
        internal int Type;
        public Threads Mode => Threads.Single;

        public Query GetQuery(ref World world)
        {
            return world.Query().With(Type);
        }

        [BurstCompile]
        public void OnUpdate(ref Entity entity, ref State state)
        {
            state.World.ECB.Remove(entity.id, Type);
        }
    }

    public struct JobCallback : IJob
    {
        public FunctionPointer<Action> callback;

        public void Execute()
        {
            callback.Invoke();
        }
    }

    public static class JobParallelForExtensions
    {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, int len, int batchCount,
            JobHandle dependencies = default)
            where T : struct, IJobParallelFor
        {
            return new JobCallback
            {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(len, batchCount, dependencies));
        }
    }
    
    public static class JobForExtensions
    {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, int len,
            JobHandle dependencies = default)
            where T : struct, IJobFor
        {
            return new JobCallback
            {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(len, dependencies));
        }
    }

    public static class JobExtensions
    {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, JobHandle dependencies = default)
            where T : struct, IJob
        {
            return new JobCallback
            {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(dependencies));
        }

        public static JobHandle ScheduleWithCallback<T>(this T jobData, int arrayLength, int indicesPerJobCount,
            Action callback,
            JobHandle dependsOn = default)
            where T : struct, IJobParallelForBatch
        {
            var handle = jobData.ScheduleBatch(arrayLength, indicesPerJobCount, dependsOn);
            return new JobCallback
            {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(handle);
        }
    }

    internal struct SystemInfo<T>
    {
        internal static SharedStatic<int> data = SharedStatic<int>.GetOrCreate<SystemInfo<T>>();
        internal static int Index => data.Data;
    }

    public struct SystemsDependencies
    {
        private NativeList<JobHandle> list;
        private NativeArray<JobHandle> array;
        //private int lastDefault;

        public static SystemsDependencies Create()
        {
            var systemsDependencies = new SystemsDependencies
            {
                list = new NativeList<JobHandle>(16, Allocator.Persistent),
                //lastDefault = 0
            };
            systemsDependencies.list.Add(new JobHandle());
            return systemsDependencies;
        }

        public void Complete()
        {
            if (!array.IsCreated) array = list.AsArray();
            JobHandle.CompleteAll(array);
        }

        public void Dispose()
        {
            if (list.IsCreated) list.Dispose();
            if (array.IsCreated) array.Dispose();
        }

        public int GetIndex<T>()
        {
            return SystemInfo<T>.Index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JobHandle GetDependencies<T>()
        {
            return list[SystemInfo<T>.Index];
        }

        public void SetDependenciesNew<TTo>(JobHandle handle = default)
        {
            SystemInfo<TTo>.data.Data = list.Length;
            list.Add(handle);
        }

        public void SetDependencies<TFrom, TTo>()
        {
            SystemInfo<TTo>.data.Data = SystemInfo<TFrom>.Index;
        }

        public void SetDependenciesDefault<TTo>()
        {
            SystemInfo<TTo>.data.Data = 0;
        }
    }


    [AttributeUsage((AttributeTargets.Method))]
    public class SystemAttribute : Attribute
    {
        public Threads mode;
        public SystemAttribute()
        {
            this.mode = Threads.Parallel;
        }
        public SystemAttribute(Threads mode)
        {
            this.mode = mode;
        }
    }

    public interface ISystemsGroup
    {
        void Build(Systems systems, ref World world);
    }

    public static class SystemsGroupExt
    {
        public static Systems AddGroup(this Systems systems, ISystemsGroup group)
        {
            group.Build(systems, ref systems.World);
            if (group is IOnDestroy onDestroy)
            {
                systems.onWorldDispose += onDestroy.OnDestroy;
            }
            return systems;
        }
    }
    
    public static class DefaultSystems
    {

    }
}