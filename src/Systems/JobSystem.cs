using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    internal class JobSystemRunner<TSystem> : ISystemRunner where TSystem : struct, IJobSystem {
        public TSystem System;
        public ECBJob EcbJob;
        public bool isComplete;
        public string Name => System.GetType().Name;
        public JobHandle Schedule(UpdateContext updateContext, ref State state) {
            System.Schedule(updateContext, ref state);
            if(isComplete) state.Dependencies.Complete();
            EcbJob.ECB = state.World.GetEcbByContext(updateContext);
            EcbJob.world = state.World;
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state) {
            System.OnUpdate(ref state);
            state.World.ECB.Playback(ref state.World);
        }
    }
    /// <summary>
    /// Run on single thread. Can be bursted
    /// </summary>
    [JobProducerType(typeof(JobSystemExtensions.JobSystemWrapper<>))]
    public interface IJobSystem {
        void OnUpdate(ref State state);
    }
    
    public static class JobSystemExtensions {
        [StructLayout(LayoutKind.Sequential)]
        public struct JobSystemWrapper<TJob> where TJob : struct, IJobSystem {
            public TJob JobData;
            public State State;
            public UpdateContext UpdateContext;
            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<JobSystemWrapper<TJob>>();

            [BurstDiscard]
            internal static void Initialize() {
                if (JobReflectionData.Data == IntPtr.Zero) {
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobSystemWrapper<TJob>),
                        typeof(TJob), (ExecuteJobFunction) Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobSystemWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public static void Execute(ref JobSystemWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
                fullData.JobData.OnUpdate(ref fullData.State);
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IJobSystem {
            JobSystemWrapper<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IJobSystem {
            JobSystemWrapper<T>.Initialize();
            return JobSystemWrapper<T>.JobReflectionData.Data;
        }

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData,
            UpdateContext updateContext, ref State state)
            where TJob : struct, IJobSystem {
            var fullData = new JobSystemWrapper<TJob> {
                JobData = jobData,
                State = state,
                UpdateContext = updateContext
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(), state.Dependencies, ScheduleMode.Single);
            return JobsUtility.Schedule(ref scheduleParams);
        }
    }
}