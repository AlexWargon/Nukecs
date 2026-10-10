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
    /// Stateless in Unity, so Burst code may read the thread members.
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
        // The Nukecs scheduler for builds without Unity is not implemented yet: no workers,
        // every thread is the main thread.
        public static int WorkerCount => 0;
        public static int WorkerMaximumCount => 0;
        public static int ThreadIndex => 0;
        public static int ThreadIndexCount => 1;

        public static NJobHandle Schedule<T>(ref T jobData, IntPtr reflectionData, NJobHandle dependsOn)
            where T : struct => throw NotImplemented();

        public static NJobHandle ScheduleParallelFor<T>(ref T jobData, IntPtr reflectionData, int length,
            int batchSize, NJobHandle dependsOn) where T : struct => throw NotImplemented();

        public static void Run<T>(ref T jobData, IntPtr reflectionData) where T : struct => throw NotImplemented();

        private static Exception NotImplemented() =>
            new NotSupportedException("The Nukecs job scheduler without Unity is not implemented yet.");
#endif
    }
}
