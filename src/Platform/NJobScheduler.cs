// Job scheduler for builds without Unity; in Unity NJobs forwards to JobsUtility instead.
#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Fixed worker threads executing <see cref="INJobData"/> jobs. Every job is a range [0, length)
    /// claimed in batches through an atomic counter, by worker tickets and by threads waiting in
    /// <see cref="Complete"/>, which help only the awaited job and its dependencies. A job becomes
    /// claimable when its dependency completes. Job records are pooled and reused right after
    /// completion: a handle carries the record's version, and a record is claimable only between
    /// becoming ready and completing, so a late ticket can at most help the record's next job.
    /// Workers are thread indices 1..WorkerCount, any other thread is 0. Without workers a job runs
    /// on the thread that makes it ready.
    /// </summary>
    internal static class NJobScheduler
    {
        private const long Unclaimable = long.MaxValue / 2;

        private static readonly object InitLock = new object();
        private static volatile bool _initialized;
        private static int _requestedWorkers = -1;
        private static int _workerCount;
        private static readonly ConcurrentQueue<Job> Tickets = new ConcurrentQueue<Job>();
        private static readonly SemaphoreSlim TicketSignal = new SemaphoreSlim(0);
        private static readonly object CompletionLock = new object();

        private static readonly object SlotsLock = new object();
        private static Job[] _slots = new Job[256];
        private static int _slotCount;

        [ThreadStatic] private static int _threadIndex;

        internal static int WorkerCount
        {
            get
            {
                EnsureInitialized();
                return _workerCount;
            }
        }

        internal static int ThreadIndex => _threadIndex;
        internal static int ThreadIndexCount => WorkerCount + 1;

        internal static void SetWorkerCount(int workerCount)
        {
            if (workerCount < 0) throw new ArgumentOutOfRangeException(nameof(workerCount));
            lock (InitLock)
            {
                if (_initialized)
                    throw new InvalidOperationException("The job scheduler has already started; set the worker count before the first world or job.");
                _requestedWorkers = workerCount;
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (InitLock)
            {
                if (_initialized) return;
                _workerCount = _requestedWorkers >= 0
                    ? _requestedWorkers
                    : Math.Max(0, Math.Min(Environment.ProcessorCount - 1, 63));
                for (var i = 0; i < _workerCount; i++)
                {
                    var index = i + 1;
                    new Thread(() => WorkerLoop(index)) { IsBackground = true, Name = "Nukecs Worker " + index }.Start();
                }
                _initialized = true;
            }
        }

        private static void WorkerLoop(int index)
        {
            _threadIndex = index;
            while (true)
            {
                TicketSignal.Wait();
                if (Tickets.TryDequeue(out var job)) RunRanges(job);
            }
        }

        // ---------------- scheduling ----------------

        internal static NJobHandle Schedule<T>(ref T data, int length, int batchSize, NJobHandle dependsOn)
            where T : struct, INJobData
        {
            EnsureInitialized();
            var job = TypedJob<T>.Rent();
            job.Data = data;
            job.Length = length;
            job.Batch = Math.Max(1, batchSize);
            return Start(job, dependsOn);
        }

        internal static NJobHandle Combine(NJobHandle a, NJobHandle b)
        {
            var ja = Resolve(a);
            var jb = Resolve(b);
            if (ja == null) return jb == null ? default : b;
            if (jb == null) return a;
            var join = JoinJob.Rent();
            join.Add(ja, a.version);
            join.Add(jb, b.version);
            return StartJoin(join);
        }

        internal static NJobHandle Combine(NJobHandle[] handles, int count)
        {
            var live = 0;
            var last = default(NJobHandle);
            for (var i = 0; i < count; i++)
                if (Resolve(handles[i]) != null) { live++; last = handles[i]; }
            if (live <= 1) return last;
            var join = JoinJob.Rent();
            for (var i = 0; i < count; i++)
            {
                var job = Resolve(handles[i]);
                if (job != null) join.Add(job, handles[i].version);
            }
            return StartJoin(join);
        }

        private static NJobHandle Start(Job job, NJobHandle dependsOn)
        {
            job.Pending = 1; // released below, after the dependency is attached
            var dependency = Resolve(dependsOn);
            if (dependency != null && Attach(dependency, dependsOn.version, job))
                job.Dependency = dependency;
            var handle = job.Handle;
            if (Interlocked.Decrement(ref job.Pending) == 0) MakeReady(job);
            return handle;
        }

        private static NJobHandle StartJoin(JoinJob join)
        {
            join.Pending = 1;
            for (var i = 0; i < join.DependencyCount; i++)
                if (!Attach(join.Dependencies[i], join.Versions[i], join)) join.Dependencies[i] = null;
            var handle = join.Handle;
            if (Interlocked.Decrement(ref join.Pending) == 0) MakeReady(join);
            return handle;
        }

        // Makes `continuation` wait for `dependency` unless it already completed.
        private static bool Attach(Job dependency, int version, Job continuation)
        {
            lock (dependency)
            {
                if (dependency.Version != version || dependency.Completed) return false;
                Interlocked.Increment(ref continuation.Pending);
                dependency.Continuations.Add(continuation);
                return true;
            }
        }

        private static void MakeReady(Job job)
        {
            if (job.Length <= 0)
            {
                Finish(job);
                return;
            }
            Volatile.Write(ref job.Next, 0);
            if (_workerCount == 0)
            {
                RunRanges(job);
                return;
            }
            var batches = (job.Length + job.Batch - 1) / job.Batch;
            var tickets = Math.Min(batches, _workerCount);
            for (var i = 0; i < tickets; i++) Tickets.Enqueue(job);
            TicketSignal.Release(tickets);
        }

        // Claims and executes batches until none are left; any thread may call it at any time.
        private static void RunRanges(Job job)
        {
            while (true)
            {
                var batch = Volatile.Read(ref job.Batch);
                var begin = Interlocked.Add(ref job.Next, batch) - batch;
                var length = Volatile.Read(ref job.Length);
                if (begin >= length) return;
                var end = (int)Math.Min(begin + batch, length);
                try
                {
                    job.Execute((int)begin, end);
                }
                catch (Exception e)
                {
                    dbug.error("Nukecs job failed: " + e);
                }
                if (Interlocked.Add(ref job.Done, end - (int)begin) == length) Finish(job);
            }
        }

        private static void Finish(Job job)
        {
            List<Job> continuations;
            lock (job)
            {
                Volatile.Write(ref job.Next, Unclaimable);
                job.Completed = true;
                continuations = job.TakeContinuations();
            }
            for (var i = 0; i < continuations.Count; i++)
            {
                var continuation = continuations[i];
                if (Interlocked.Decrement(ref continuation.Pending) == 0) MakeReady(continuation);
            }
            continuations.Clear();
            lock (CompletionLock) Monitor.PulseAll(CompletionLock);
            job.Release(continuations);
        }

        // ---------------- completion ----------------

        internal static bool IsCompleted(NJobHandle handle) => Resolve(handle) == null;

        internal static void Complete(NJobHandle handle)
        {
            var job = Resolve(handle);
            if (job == null) return;
            var spin = new SpinWait();
            while (job.Version == handle.version && !job.Completed)
            {
                Help(job, handle.version, 0);
                if (job.Version != handle.version || job.Completed) return;
                if (!spin.NextSpinWillYield)
                {
                    spin.SpinOnce();
                    continue;
                }
                lock (CompletionLock)
                {
                    if (job.Version == handle.version && !job.Completed)
                        Monitor.Wait(CompletionLock, 1);
                }
            }
        }

        // Runs claimable batches of the job, or of its unfinished dependencies when it is not ready.
        private static void Help(Job job, int version, int depth)
        {
            if (depth > 64 || job.Version != version || job.Completed) return;
            if (job.Pending == 0)
            {
                RunRanges(job);
                return;
            }
            var dependency = job.Dependency;
            if (dependency != null) Help(dependency, dependency.Version, depth + 1);
            if (job is JoinJob join)
            {
                for (var i = 0; i < join.DependencyCount; i++)
                {
                    var d = join.Dependencies[i];
                    if (d != null) Help(d, join.Versions[i], depth + 1);
                }
            }
        }

        private static Job Resolve(NJobHandle handle)
        {
            if (handle.id == 0) return null;
            var job = Volatile.Read(ref _slots)[handle.id - 1];
            return job.Version == handle.version && !job.Completed ? job : null;
        }

        private static int AllocateSlot(Job job)
        {
            lock (SlotsLock)
            {
                if (_slotCount == _slots.Length)
                {
                    var grown = new Job[_slots.Length * 2];
                    Array.Copy(_slots, grown, _slotCount);
                    Volatile.Write(ref _slots, grown);
                }
                _slots[_slotCount] = job;
                return ++_slotCount;
            }
        }

        // ---------------- job records ----------------

        private abstract class Job
        {
            internal readonly int Slot;
            internal volatile int Version;
            internal volatile bool Completed;
            internal int Pending;
            internal long Next = Unclaimable;
            internal int Done;
            internal int Length;
            internal int Batch = 1;
            internal Job Dependency;
            private List<Job> _continuations = new List<Job>();

            protected Job() => Slot = AllocateSlot(this);

            internal NJobHandle Handle => new NJobHandle { id = (ulong)Slot, version = Version };
            internal List<Job> Continuations => _continuations;

            internal List<Job> TakeContinuations()
            {
                var taken = _continuations;
                _continuations = null;
                return taken;
            }

            // Called on a pooled record before it is scheduled again.
            protected void Reset()
            {
                Interlocked.Increment(ref Version);
                Volatile.Write(ref Next, Unclaimable);
                Done = 0;
                Pending = 0;
                Dependency = null;
                Completed = false;
            }

            internal abstract void Execute(int begin, int end);

            // `emptyList` is the cleared continuation list to reuse.
            internal virtual void Release(List<Job> emptyList)
            {
                lock (this) _continuations = emptyList;
            }
        }

        private sealed class TypedJob<T> : Job where T : struct, INJobData
        {
            private static readonly Stack<TypedJob<T>> Pool = new Stack<TypedJob<T>>();
            internal T Data;

            internal static TypedJob<T> Rent()
            {
                TypedJob<T> job = null;
                lock (Pool)
                    if (Pool.Count > 0) job = Pool.Pop();
                job ??= new TypedJob<T>();
                job.Reset();
                return job;
            }

            internal override void Execute(int begin, int end) => Data.Execute(begin, end);

            internal override void Release(List<Job> emptyList)
            {
                base.Release(emptyList);
                Data = default;
                lock (Pool) Pool.Push(this);
            }
        }

        private sealed class JoinJob : Job
        {
            private static readonly Stack<JoinJob> Pool = new Stack<JoinJob>();
            internal Job[] Dependencies = new Job[4];
            internal int[] Versions = new int[4];
            internal int DependencyCount;

            internal static JoinJob Rent()
            {
                JoinJob job = null;
                lock (Pool)
                    if (Pool.Count > 0) job = Pool.Pop();
                job ??= new JoinJob();
                job.Reset();
                job.DependencyCount = 0;
                return job;
            }

            internal void Add(Job dependency, int version)
            {
                if (DependencyCount == Dependencies.Length)
                {
                    Array.Resize(ref Dependencies, DependencyCount * 2);
                    Array.Resize(ref Versions, DependencyCount * 2);
                }
                Dependencies[DependencyCount] = dependency;
                Versions[DependencyCount++] = version;
            }

            internal override void Execute(int begin, int end) { }

            internal override void Release(List<Job> emptyList)
            {
                base.Release(emptyList);
                Array.Clear(Dependencies, 0, DependencyCount);
                DependencyCount = 0;
                lock (Pool) Pool.Push(this);
            }
        }
    }
}
#endif
