using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    [JobProducerType(typeof(IEntityJobSystemExtensions.EntityJobWrapper<>))][Obsolete]
    public interface IEntityJobSystem {
        Threads Mode { get; }
        Query GetQuery(ref World world);
        void OnUpdate(ref Entity entity, ref State state);
    }
    internal unsafe class EntityJobSystemRunner<TSystem> : ISystemRunner, IQueryHolder where TSystem : struct, IEntityJobSystem {
        public TSystem System;
        public QueryUnsafe* Query;
        public Threads Mode;
        public ECBJob EcbJob;
        private int version;
        private int id;
        private int queryId = -1;
        private bool idIsZero;
        public string Name => System.GetType().Name;

        public void SetQueryId() {
            if (Query != null) queryId = Query->Id;
        }
        
        public void UpdateQueryPointer(World.WorldUnsafe* world) {
            if (queryId >= 0 && queryId < world->queries.Length)
                Query = world->queries.Ptr[queryId].Ptr;
        }
#if NUKECS_DEBUG
        Marker _marker;
#endif
        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
#if NUKECS_DEBUG
            _marker.Autostart(System);
#endif
            if (id == 0 && !idIsZero)
            {
                id = Query->Id;
                idIsZero = id == 0;
            }
            ref var world = ref state.World;
            Nukecs.Query.RestoreIfNeed(ref Query, ref version, id, ref world);
            if (Mode == Threads.Main) {
                for (var i = 0; i < Query->count; i++) {
                    System.OnUpdate(ref Query->GetEntity(i), ref state);    
                }
                EcbJob.ECB = world.GetEcbVieContext(updateContext);
                EcbJob.world = world;
                EcbJob.ECB.PlaybackMainThread(ref world);
#if NUKECS_DEBUG
                _marker.End();
#endif
                return state.Dependencies;
            }
            state.Dependencies = System.Schedule(Query, Mode, updateContext, ref state);
            EcbJob.ECB = world.GetEcbVieContext(updateContext);
            EcbJob.world = world;
#if NUKECS_DEBUG
            _marker.End();
#endif
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state) {
            for (int i = 0; i < Query->count; i++) {
                System.OnUpdate(ref this.Query->GetEntity(i), ref state);
            }
            state.World.ECB.Playback(ref state.World);
        }
    }

    // ReSharper disable once InconsistentNaming
    public static class IEntityJobSystemExtensions {
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct EntityJobWrapper<TJob> where TJob : struct, IEntityJobSystem {
            public TJob JobData;
            [NativeDisableUnsafePtrRestriction]
            public QueryUnsafe* query;
            public UpdateContext updateContext;
            public State State;
            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<EntityJobWrapper<TJob>>();

            [BurstDiscard]
            internal static void Initialize() {
                if (JobReflectionData.Data == IntPtr.Zero) {
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(EntityJobWrapper<TJob>),
                        typeof(TJob), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref EntityJobWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
            
            public static void Execute(ref EntityJobWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
                if(fullData.query->count == 0) return;
                switch (fullData.JobData.Mode) {
                    case Threads.Parallel:
                        while (true) {
                            if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                                break;
                            //JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
                            for (var i = begin; i < end; i++) {
                                ref var e = ref fullData.query->GetEntity(i);
                                if (e.IsValid()) {
                                    fullData.JobData.OnUpdate(ref e, ref fullData.State);
                                }
                            }
                        }
                        break;
                    case Threads.Single:
                        for (var i = 0; i < fullData.query->count; i++) {
                            ref var e = ref fullData.query->GetEntity(i);
                            //if (e.IsValid()) {
                            fullData.JobData.OnUpdate(ref e, ref fullData.State);
                            //}
                        }
                        break;
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IEntityJobSystem {
            EntityJobWrapper<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IEntityJobSystem {
            EntityJobWrapper<T>.Initialize();
            return EntityJobWrapper<T>.JobReflectionData.Data;
        }

        internal static unsafe JobHandle Schedule<TJob>(this TJob jobData, QueryUnsafe* query,
            Threads mode, UpdateContext updateContext, ref State state)
            where TJob : struct, IEntityJobSystem {
            var fullData = new EntityJobWrapper<TJob> {
                JobData = jobData,
                query = query,
                updateContext = updateContext,
                State = state
            };
            
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(), state.Dependencies,
                mode == Threads.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
            switch (mode) {
                case Threads.Single:
                    return JobsUtility.Schedule(ref scheduleParams);
                case Threads.Parallel:
                    return JobsUtility.ScheduleParallelFor(ref scheduleParams, query->count, 1);
            }
            //var workers = JobsUtility.JobWorkerCount;
            //var batchCount = query.Count > workers ? query.Count / workers : 1;
            return state.Dependencies;
        }
        
        public static unsafe void Run<TJob>(this TJob jobData, ref Query query, float deltaTime) where TJob : struct, IEntityJobSystem
        {
            var fullData = new EntityJobWrapper<TJob> {
                JobData = jobData,
                //query = query,
                //deltaTime = deltaTime
            };
            JobsUtility.JobScheduleParameters parameters = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(),
            new JobHandle(), 
                ScheduleMode.Run);
            JobsUtility.Schedule(ref parameters);
        }
    }
}