using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Wargon.Nukecs
{
    public enum GroupScheduleMode
    {
        LegacyGroupComplete,
        ChainedGroupComplete,
        FlattenedSchedule,
        FlattenedSchedule2
    }

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
        private static Marker _flatGraph2 = new("FLAT GRAPH 2");
        private SystemDependencyGraph _dependencyGraph;
        private bool _useDependencyGraph;
        private bool _graphBuilt;
        private GroupScheduleMode _groupScheduleMode = GroupScheduleMode.LegacyGroupComplete;
        private readonly List<SystemDependencyInfo> _dependencyInfos;
        private Unity.Collections.NativeArray<Unity.Jobs.JobHandle> _handleBuffer;

        public SystemDependencyGraph DependencyGraph => _dependencyGraph;
        public bool UseDependencyGraphEnabled => _useDependencyGraph;
        public GroupScheduleMode GetGroupScheduleMode() => _groupScheduleMode;
        public IReadOnlyList<ISystemRunner> Runners => onUpdate;

        public Systems(ref World world)
        {
            Dependencies = default;
            onStart = new List<ISystemRunner>();
            onUpdate = new List<ISystemRunner>();
            onFixedUpdate = new List<ISystemRunner>();
            onDestroy = new List<ISystemRunner>();
            systemDestroyers = new List<ISystemDestroyer>();
            _dependencyInfos = new List<SystemDependencyInfo>();
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

            if (_useDependencyGraph)
            {
                if (!_graphBuilt)
                    BuildDependencyGraph();

                if (onFixedUpdate.Count == 0 && onUpdate.Count == 1)
                {
                    _state.Dependencies = onUpdate[0].Schedule(UpdateContext.Update, ref _state);
                    _state.Dependencies.Complete();
                    _allSystems.End();
                    return;
                }

                ExecuteWithDependencyGraph();
            }
            else
            {
                if (onFixedUpdate.Count == 0 && onUpdate.Count == 1)
                {
                    _state.Dependencies = onUpdate[0].Schedule(UpdateContext.Update, ref _state);
                    _state.Dependencies.Complete();
                    _allSystems.End();
                    return;
                }

                ExecuteSequentialUpdate();
            }

            _timeSinceLastFixedUpdate += dt;
            if (_timeSinceLastFixedUpdate >= FIXED_UPDATE_INTERVAL)
            {
                for (var i = 0; i < onFixedUpdate.Count; i++)
                    _state.Dependencies = onFixedUpdate[i].Schedule(UpdateContext.Update, ref _state);
                _timeSinceLastFixedUpdate = 0;
            }

            if (!_state.Dependencies.Equals(default))
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
            InvalidateDependencyGraph();
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
            InvalidateDependencyGraph();
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

            InvalidateDependencyGraph();
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

            InvalidateDependencyGraph();
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
            InvalidateDependencyGraph();
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

        private void RebuildQueryPointers(List<ISystemRunner> list, World.WorldUnsafe* worldPtr)
        {

            foreach (var runner in list)
            {
                if (runner is IQueryHolder holder)
                    holder.UpdateQueryPointer(worldPtr);
                if (runner is ISystemWithDeserialization sysDeser)
                    sysDeser.OnWorldDeserialize(World);
            }
        }

        public interface ISystemWithDeserialization
        {
            void OnWorldDeserialize(World world);
        }

        internal void OnWorldDispose()
        {
            Complete();
            OnDestroy();
            onWorldDispose?.Invoke(ref World);
            foreach (var systemDestroyer in systemDestroyers) systemDestroyer.Destroy(ref World);
            if (_handleBuffer.IsCreated) _handleBuffer.Dispose();
        }

        public Systems UseDependencyGraph(bool enable = true,
            GroupScheduleMode mode = GroupScheduleMode.LegacyGroupComplete)
        {
            _useDependencyGraph = enable;
            _groupScheduleMode = mode;
            if (enable && !_graphBuilt)
                BuildDependencyGraph();
            return this;
        }

        public void InvalidateDependencyGraph()
        {
            _graphBuilt = false;
            _dependencyGraph = null;
        }

        internal void RegisterDependencyInfo(SystemDependencyInfo info)
        {
            _dependencyInfos.Add(info);
            InvalidateDependencyGraph();
        }

        private void BuildDependencyGraph()
        {
            if (onUpdate.Count == 0)
            {
                _dependencyGraph = new SystemDependencyGraph();
                _dependencyGraph.Build(System.Array.Empty<SystemNode>());
                _graphBuilt = true;
                return;
            }

            var nodes = new SystemNode[onUpdate.Count];
            for (int i = 0; i < onUpdate.Count; i++)
            {
                var runner = onUpdate[i];
                var info = SystemDependencyInfo.Empty;

                if (runner is ISystemDependencyInfoProvider provider)
                    info = provider.DependencyInfo;
                else if (i < _dependencyInfos.Count)
                    info = _dependencyInfos[i];

                info.SystemName = runner.Name;

                var threadMode = Threads.Parallel;
                if (runner is IThreadModeProvider threadProvider)
                    threadMode = threadProvider.Mode;

                nodes[i] = new SystemNode
                {
                    Index = i,
                    Name = runner.Name,
                    Runner = runner,
                    Info = info,
                    ThreadMode = threadMode
                };
            }

            _dependencyGraph = new SystemDependencyGraph();
            _dependencyGraph.Build(nodes);
            _graphBuilt = true;
        }

        private void ExecuteWithDependencyGraph()
        {
            if (_groupScheduleMode == GroupScheduleMode.ChainedGroupComplete)
            {
                ExecuteWithDependencyGraph_Chained();
                return;
            }

            if (_groupScheduleMode == GroupScheduleMode.FlattenedSchedule)
            {
                ExecuteWithDependencyGraph_Flattened();
                return;
            }

            if (_groupScheduleMode == GroupScheduleMode.FlattenedSchedule2)
            {
                ExecuteWithDependencyGraph_Flattened2();
                return;
            }

            var groups = _dependencyGraph.GetPrecomputedGroups();
            if (groups == null || groups.Length == 0)
            {
                ExecuteSequentialUpdate();
                return;
            }

            var savedDeps = _state.Dependencies;

            for (int g = 0; g < groups.Length; g++)
            {
                ref var group = ref groups[g];

                foreach (var idx in group.MainIndices)
                {
                    _state.Dependencies = savedDeps;
                    onUpdate[idx].Schedule(UpdateContext.Update, ref _state);
                    savedDeps = _state.Dependencies;
                }

                if (group.ParallelIndices.Length == 1)
                {
                    _state.Dependencies = savedDeps;
                    onUpdate[group.ParallelIndices[0]].Schedule(UpdateContext.Update, ref _state);
                    savedDeps = _state.Dependencies;
                }
                else if (group.ParallelIndices.Length > 1)
                {
                    var count = group.ParallelIndices.Length;
                    if (!_handleBuffer.IsCreated || _handleBuffer.Length != count)
                    {
                        if (_handleBuffer.IsCreated)
                            _handleBuffer.Dispose();
                        _handleBuffer =
                            new Unity.Collections.NativeArray<Unity.Jobs.JobHandle>(count,
                                Unity.Collections.Allocator.Persistent);
                    }

                    _state.SkipECBSchedule = 1;
                    for (int i = 0; i < count; i++)
                    {
                        _state.Dependencies = savedDeps;
                        _handleBuffer[i] = onUpdate[group.ParallelIndices[i]]
                            .Schedule(UpdateContext.Update, ref _state);
                    }

                    var combined = Unity.Jobs.JobHandle.CombineDependencies(_handleBuffer);
                    combined.Complete();
                    _state.SkipECBSchedule = 0;

                    if (World.UnsafeWorld->ECB.HasCommands)
                    {
                        World.UnsafeWorld->ECB.Playback(ref World);
                    }

                    savedDeps = default;
                    _state.Dependencies = default;
                }
            }
        }

        private void ExecuteWithDependencyGraph_Chained()
        {
            var groups = _dependencyGraph.GetPrecomputedGroups();
            if (groups == null || groups.Length == 0)
            {
                ExecuteSequentialUpdate();
                return;
            }

            var savedDeps = _state.Dependencies;

            for (int g = 0; g < groups.Length; g++)
            {
                ref var group = ref groups[g];

                foreach (var idx in group.MainIndices)
                {
                    savedDeps.Complete();
                    _state.Dependencies = savedDeps;
                    onUpdate[idx].Schedule(UpdateContext.Update, ref _state);
                    savedDeps = _state.Dependencies;
                }

                if (group.ParallelIndices.Length == 1)
                {
                    _state.Dependencies = savedDeps;
                    var handle = onUpdate[group.ParallelIndices[0]].Schedule(UpdateContext.Update, ref _state);
                    if (group.HasECB)
                    {
                        savedDeps = new ECBJob
                        {
                            ECB = World.UnsafeWorld->ECB,
                            world = World,
                            updateContext = UpdateContext.Update
                        }.Schedule(handle);
                    }
                    else
                    {
                        savedDeps = handle;
                    }
                }
                else if (group.ParallelIndices.Length > 1)
                {
                    var count = group.ParallelIndices.Length;
                    if (!_handleBuffer.IsCreated || _handleBuffer.Length != count)
                    {
                        if (_handleBuffer.IsCreated)
                            _handleBuffer.Dispose();
                        _handleBuffer =
                            new Unity.Collections.NativeArray<Unity.Jobs.JobHandle>(count,
                                Unity.Collections.Allocator.Persistent);
                    }

                    _state.SkipECBSchedule = 1;
                    for (int i = 0; i < count; i++)
                    {
                        _state.Dependencies = savedDeps;
                        _handleBuffer[i] = onUpdate[group.ParallelIndices[i]]
                            .Schedule(UpdateContext.Update, ref _state);
                    }

                    var combined = Unity.Jobs.JobHandle.CombineDependencies(_handleBuffer);

                    if (group.HasECB)
                    {
                        savedDeps = new ECBJob
                        {
                            ECB = World.UnsafeWorld->ECB,
                            world = World,
                            updateContext = UpdateContext.Update
                        }.Schedule(combined);
                    }
                    else
                    {
                        savedDeps = combined;
                    }
                }
            }

            savedDeps.Complete();
            _state.SkipECBSchedule = 0;

            if (World.UnsafeWorld->ECB.HasCommands)
            {
                World.UnsafeWorld->ECB.Playback(ref World);
            }

            _state.Dependencies = default;
        }

        private void ExecuteWithDependencyGraph_Flattened()
        {
            var nodes = _dependencyGraph.Nodes;
            var preds = _dependencyGraph.GetPredecessors();
            if (nodes == null || nodes.Length == 0 || preds == null)
            {
                ExecuteSequentialUpdate();
                return;
            }

            var n = nodes.Length;
            var handles = new JobHandle[n];

            var savedDeps = _state.Dependencies;
            _state.SkipECBSchedule = 1;

            // ═══ Main/MainRun first: synchronous, registration order, no deps ═══
            for (int i = 0; i < n; i++)
            {
                if (!BlocksMainThread(nodes[i].ThreadMode))
                    continue;

                _state.Dependencies = savedDeps;
                handles[i] = onUpdate[i].Schedule(UpdateContext.Update, ref _state);
            }

            // ═══ Parallel/Single: schedule with deps among themselves ═══
            for (int i = 0; i < n; i++)
            {
                if (BlocksMainThread(nodes[i].ThreadMode))
                    continue;

                var deps = CombinePredHandles(preds[i], handles, savedDeps);
                _state.Dependencies = deps;
                handles[i] = onUpdate[i].Schedule(UpdateContext.Update, ref _state);
            }

            // ═══ Single combined Complete ═══
            int handleCount = 0;
            for (int i = 0; i < n; i++)
                if (!handles[i].Equals(default))
                    handleCount++;

            if (handleCount > 0)
            {
                if (!_handleBuffer.IsCreated || _handleBuffer.Length < handleCount)
                {
                    if (_handleBuffer.IsCreated)
                        _handleBuffer.Dispose();
                    _handleBuffer = new NativeArray<JobHandle>(handleCount, Allocator.Persistent);
                }
                int idx = 0;
                for (int i = 0; i < n; i++)
                    if (!handles[i].Equals(default))
                        _handleBuffer[idx++] = handles[i];
                JobHandle.CombineDependencies(_handleBuffer.GetSubArray(0, handleCount)).Complete();
            }

            _state.SkipECBSchedule = 0;
            if (World.UnsafeWorld->ECB.HasCommands)
                World.UnsafeWorld->ECB.Playback(ref World);
            _state.Dependencies = default;
        }

        private static bool BlocksMainThread(Threads mode) =>
            mode == Threads.Main || mode == Threads.MainRun;

        private JobHandle CombinePredHandles(int[] predIndices, JobHandle[] handles, JobHandle defaultHandle)
        {
            if (predIndices == null || predIndices.Length == 0)
                return defaultHandle;

            int count = 0;
            for (int p = 0; p < predIndices.Length; p++)
                if (!handles[predIndices[p]].Equals(default))
                    count++;

            if (count == 0) return defaultHandle;
            if (count == 1)
            {
                for (int p = 0; p < predIndices.Length; p++)
                    if (!handles[predIndices[p]].Equals(default))
                        return handles[predIndices[p]];
            }

            if (!_handleBuffer.IsCreated || _handleBuffer.Length < count)
            {
                if (_handleBuffer.IsCreated)
                    _handleBuffer.Dispose();
                _handleBuffer = new NativeArray<JobHandle>(count, Allocator.Persistent);
            }

            int idx = 0;
            for (int p = 0; p < predIndices.Length; p++)
            {
                if (!handles[predIndices[p]].Equals(default))
                    _handleBuffer[idx++] = handles[predIndices[p]];
            }

            return JobHandle.CombineDependencies(_handleBuffer.GetSubArray(0, count));
        }

        private void ExecuteSequentialUpdate()
        {
            for (var i = 0; i < onUpdate.Count; i++)
                _state.Dependencies = onUpdate[i].Schedule(UpdateContext.Update, ref _state);
        }

        private void ExecuteWithDependencyGraph_Flattened2()
        {
            _flatGraph2.Start();
            var nodes = _dependencyGraph.Nodes;
            var preds = _dependencyGraph.GetPredecessors();
            var n = nodes.Length;
            if (n == 0)
            {
                ExecuteSequentialUpdate();
                _flatGraph2.End();
                return;
            }

            var handles = new JobHandle[n];
            var savedDeps = _state.Dependencies;
            _state.SkipECBSchedule = 1;

            // ═══ Phase 1: ALL Main/MainRun systems first ═══
            // Synchronous, registration order. No dep checking — they run before
            // any parallel system starts, so no conflicts possible.
            for (int i = 0; i < n; i++)
            {
                if (!BlocksMainThread(nodes[i].ThreadMode))
                    continue;

                _state.Dependencies = savedDeps;
                handles[i] = onUpdate[i].Schedule(UpdateContext.Update, ref _state);
            }

            // ═══ Phase 2: Schedule ALL Parallel/Single systems ═══
            // Deps resolved among themselves only. Main predecessors already
            // completed synchronously — their handles are default (skipped by CombinePredHandles).
            for (int i = 0; i < n; i++)
            {
                if (BlocksMainThread(nodes[i].ThreadMode))
                    continue;

                var deps = CombinePredHandles(preds[i], handles, savedDeps);
                _state.Dependencies = deps;
                handles[i] = onUpdate[i].Schedule(UpdateContext.Update, ref _state);
            }

            // ═══ Phase 3: Single combined Complete ═══
            int handleCount = 0;
            for (int i = 0; i < n; i++)
                if (!handles[i].Equals(default))
                    handleCount++;

            if (handleCount > 0)
            {
                if (!_handleBuffer.IsCreated || _handleBuffer.Length < handleCount)
                {
                    if (_handleBuffer.IsCreated)
                        _handleBuffer.Dispose();
                    _handleBuffer = new NativeArray<JobHandle>(handleCount, Allocator.Persistent);
                }
                int idx = 0;
                for (int i = 0; i < n; i++)
                    if (!handles[i].Equals(default))
                        _handleBuffer[idx++] = handles[i];
                var h = JobHandle.CombineDependencies(_handleBuffer.GetSubArray(0, handleCount));
                _flatGraph2.End();
                h.Complete();
            }
            else
            {
                _flatGraph2.End();
            }

            _state.SkipECBSchedule = 0;
            if (World.UnsafeWorld->ECB.HasCommands)
                World.UnsafeWorld->ECB.Playback(ref World);
            _state.Dependencies = default;
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
            systems.InvalidateDependencyGraph();
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

    public interface ISystemDependencyInfoProvider
    {
        SystemDependencyInfo DependencyInfo { get; }
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