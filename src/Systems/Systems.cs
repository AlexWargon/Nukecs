using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Jobs;

// ReSharper disable InconsistentNaming

namespace Wargon.Nukecs
{
    public unsafe class Systems
    {
        public JobHandle Dependencies;
        public World World;
        private State _state;
        internal ref State State => ref _state;
        private const float FIXED_UPDATE_INTERVAL = 0.016f;
        private float _timeSinceLastFixedUpdate;
        internal ActionRef<World> onWorldDispose;
        private static Marker _allSystems = new("ALL SYSTEMS");
        public Systems(ref World world)
        {
            Dependencies = default;
            onStart = new List<ISystemRunner>();
            onUpdate = new List<ISystemRunner>();
            onFixedUpdate = new List<ISystemRunner>();
            onDestroy = new List<ISystemRunner>();
            systemDestroyers = new List<ISystemDestroyer>();
            World = world;
            WorldSystems.Add(world.UnsafeWorld->Id, this);
        }
        private readonly List<ISystemDestroyer> systemDestroyers;
        internal readonly List<ISystemRunner> onStart;
        internal readonly List<ISystemRunner> onUpdate;
        internal readonly List<ISystemRunner> onFixedUpdate;
        internal readonly List<ISystemRunner> onDestroy;

        public void OnStart()
        {
            _state.Dependencies = World.DependenciesUpdate;
            _state.World = World;
            _state.Time.DeltaTime = 0;
            _state.Time.Time = 0;
            _state.Time.ElapsedTime = 0;
            _state.Time.DeltaTimeFixed = FIXED_UPDATE_INTERVAL;
            World.UnsafeWorld->timeData = _state.Time;
            for (var i = 0; i < onStart.Count; i++)
                _state.Dependencies = onStart[i].Schedule(UpdateContext.Update, ref _state);
            _state.Dependencies.Complete();
        }
        public void OnDestroy()
        {
            _state.Dependencies = World.DependenciesUpdate;
            _state.World = World;
            _state.Time.DeltaTime = 0;
            _state.Time.Time = 0;
            _state.Time.ElapsedTime = 0;
            _state.Time.DeltaTimeFixed = FIXED_UPDATE_INTERVAL;
            World.UnsafeWorld->timeData = _state.Time;
            for (var i = 0; i < onDestroy.Count; i++)
                _state.Dependencies = onDestroy[i].Schedule(UpdateContext.Update, ref _state);
            _state.Dependencies.Complete();
        }
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
            
            if (onFixedUpdate.Count == 0 && onUpdate.Count == 1)
            {
                _state.Dependencies = onUpdate[0].Schedule(UpdateContext.Update, ref _state);
                _state.Dependencies.Complete();
                _allSystems.End();
                return;
            }

            for (var i = 0; i < onUpdate.Count; i++)
                _state.Dependencies = onUpdate[i].Schedule(UpdateContext.Update, ref _state);
            
            _timeSinceLastFixedUpdate += dt;
            if (_timeSinceLastFixedUpdate >= FIXED_UPDATE_INTERVAL)
            {
                for (var i = 0; i < onFixedUpdate.Count; i++)
                    _state.Dependencies = onFixedUpdate[i].Schedule(UpdateContext.Update, ref _state);
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
            this.Add(DefaultSystems.EntityDestroySystem, Threads.MainRun);
            this.Add(DefaultSystems.OnPrefabSpawn);
            this.Add(DefaultSystems.ClearEvents, Threads.MainRun);
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
            onUpdate.Add(runner);
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
                onFixedUpdate.Add(runner);
            else
                onUpdate.Add(runner);
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
                onFixedUpdate.Add(runner);
            else
                onUpdate.Add(runner);
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
                onFixedUpdate.Add(runner);
            }
            else
            {
                onUpdate.Add(runner);
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
                onFixedUpdate.Add(runner);
            }
            else
            {
                onUpdate.Add(runner);
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
            onStart.AddRange(group.runners);
            onUpdate.AddRange(group.fixedRunners);
            onFixedUpdate.AddRange(group.mainThreadRunners);
            onDestroy.AddRange(group.mainThreadFixedRunners);
            systemDestroyers.AddRange(group.destroyRunners);
            return this;
        }


        internal void Complete()
        {
            _state.Dependencies.Complete();
        }

        public void OnWorldDeserialize(World.WorldUnsafe* world)
        {
            World.unsafeWorldPtr = world->selfPtr;
            RebuildQueryPointers(onStart, world);
            RebuildQueryPointers(onUpdate, world);
            RebuildQueryPointers(onFixedUpdate, world);
            RebuildQueryPointers(onDestroy, world);
        }
        private void RebuildQueryPointers(List<ISystemRunner> list, World.WorldUnsafe* worldPtr) {
            
            foreach (var runner in list) {
                if (runner is IQueryHolder holder)
                    holder.UpdateQueryPointer(worldPtr);
                if (runner is ISystemWithDeserialization sysDeser)
                    sysDeser.OnWorldDeserialize(World);
            }
        }

        public interface ISystemWithDeserialization {
            void OnWorldDeserialize(World world);
        }

        internal void OnWorldDispose()
        {
            Complete();
            OnDestroy();
            onWorldDispose?.Invoke(ref World);
            foreach (var systemDestroyer in systemDestroyers) systemDestroyer.Destroy(ref World);
        }
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
            systems.onUpdate.Add(runner);
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

    public static class SystemPath
    {
        public const int Start       = 0;
        public const int Update      = 1;
        public const int FixedUpdate = 2;
        public const int Destroy     = 3;
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

        public static ITuple Chain(this ITuple tuple)
        {
            return tuple;
        }
    }
}