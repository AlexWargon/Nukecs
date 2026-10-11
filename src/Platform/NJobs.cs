using System;
using System.Runtime.CompilerServices;
#if UNITY_5_3_OR_NEWER
using Unity.Jobs.LowLevel.Unsafe;
#endif

namespace Wargon.Nukecs
{
    /// <summary>
    /// Job scheduling for the core. In Unity every member forwards to <c>JobsUtility</c> exactly as
    /// the core called it before: job data is passed by address with its reflection data, Single and
    /// ParallelFor wait for the dependency, Run executes synchronously and ignores dependencies.
    /// Stateless in Unity, so Burst code may read the thread members. Outside Unity job data must
    /// implement <see cref="INJobData"/>.
    /// </summary>
    public static unsafe class NJobs
    {
#if UNITY_5_3_OR_NEWER
        /// <summary>Worker threads currently used by the job system.</summary>
        public static int WorkerCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => JobsUtility.JobWorkerCount;
        }

        public static int WorkerMaximumCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => JobsUtility.JobWorkerMaximumCount;
        }

        /// <summary>Index of the executing thread in [0, ThreadIndexCount); 0 outside jobs.</summary>
        public static int ThreadIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => JobsUtility.ThreadIndex;
        }

        public static int ThreadIndexCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => JobsUtility.ThreadIndexCount;
        }

        /// <summary>Runs the job once on a worker thread after <paramref name="dependsOn"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NJobHandle Schedule<T>(ref T jobData, IntPtr reflectionData, NJobHandle dependsOn)
            where T : struct
        {
            var parameters = new JobsUtility.JobScheduleParameters(Mem.AddressOf(ref jobData),
                reflectionData, dependsOn.value, ScheduleMode.Single);
            return JobsUtility.Schedule(ref parameters);
        }

        /// <summary>Splits [0, length) into batches executed by worker threads after <paramref name="dependsOn"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NJobHandle ScheduleParallelFor<T>(ref T jobData, IntPtr reflectionData, int length,
            int batchSize, NJobHandle dependsOn) where T : struct
        {
            var parameters = new JobsUtility.JobScheduleParameters(Mem.AddressOf(ref jobData),
                reflectionData, dependsOn.value, ScheduleMode.Parallel);
            return JobsUtility.ScheduleParallelFor(ref parameters, length, batchSize);
        }

        /// <summary>Executes the job on the calling thread; takes no dependency.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run<T>(ref T jobData, IntPtr reflectionData) where T : struct
        {
            var parameters = new JobsUtility.JobScheduleParameters(Mem.AddressOf(ref jobData),
                reflectionData, default, ScheduleMode.Run);
            JobsUtility.Schedule(ref parameters);
        }
#else
        public static int WorkerCount => NJobScheduler.WorkerCount;
        public static int WorkerMaximumCount => NJobScheduler.WorkerCount;
        public static int ThreadIndex => NJobScheduler.ThreadIndex;
        public static int ThreadIndexCount => NJobScheduler.ThreadIndexCount;

        /// <summary>
        /// Sets the number of worker threads (default: processor count - 1). Call before the first
        /// world or job: per-thread buffers are sized by <see cref="ThreadIndexCount"/>. 0 runs every
        /// job on the thread that makes it ready.
        /// </summary>
        public static void SetWorkerCount(int workerCount) => NJobScheduler.SetWorkerCount(workerCount);

        public static NJobHandle Schedule<T>(ref T jobData, IntPtr reflectionData, NJobHandle dependsOn)
            where T : struct, INJobData => NJobScheduler.Schedule(ref jobData, 1, 1, dependsOn);

        public static NJobHandle ScheduleParallelFor<T>(ref T jobData, IntPtr reflectionData, int length,
            int batchSize, NJobHandle dependsOn) where T : struct, INJobData =>
            NJobScheduler.Schedule(ref jobData, length, batchSize, dependsOn);

        public static void Run<T>(ref T jobData, IntPtr reflectionData) where T : struct, INJobData =>
            jobData.Execute(0, 1);
#endif
    }
}
