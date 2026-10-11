using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Wargon.Nukecs.Tests.DotNet
{
    public struct SchedPos : IComponent { public float X; }
    public struct SchedVel : IComponent { public float X; }
    public struct SchedTagged : IComponent { }

    public static partial class SchedulerTestSystems
    {
        [System]
        public static void Move(ref Query<SchedPos, SchedVel> query, ref State state)
        {
            foreach (var (p, v) in query)
                p.Get.X += v.Read.X;
        }

        [System]
        public static void Mark(ref Query<Entity, SchedPos, None<SchedTagged>> query)
        {
            foreach (var (e, p) in query)
                if (p.Read.X >= 10) e.Add(new SchedTagged());
        }
    }

    public class SchedulerTests
    {
        private struct CountJob : INJobParallelFor
        {
            public int[] Hits;
            public int[] Threads;

            public void Execute(int index)
            {
                Interlocked.Increment(ref Hits[index]);
                Interlocked.Increment(ref Threads[NJobs.ThreadIndex]);
            }
        }

        private struct StampJob : INJob
        {
            public int Id;
            public int[] Dependencies;
            public int[] Stamp;
            public int[] Violations;
            public int[] Clock;

            public void Execute()
            {
                foreach (var dependency in Dependencies)
                    if (Volatile.Read(ref Stamp[dependency]) == 0) Interlocked.Increment(ref Violations[0]);
                Thread.SpinWait(Id % 50);
                Volatile.Write(ref Stamp[Id], Interlocked.Increment(ref Clock[0]));
            }
        }

        private struct ThrowJob : INJob
        {
            public void Execute() => throw new InvalidOperationException("expected test exception");
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(7, 3)]
        [TestCase(64, 1)]
        [TestCase(50000, 50000)]
        [TestCase(100003, 7)]
        public void ParallelFor_ExecutesEveryIndexOnce(int length, int batch)
        {
            var job = new CountJob { Hits = new int[length], Threads = new int[NJobs.ThreadIndexCount] };
            job.Schedule(length, batch).Complete();
            Assert.That(job.Hits.All(h => h == 1));
            Assert.AreEqual(length, job.Threads.Sum());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Jobs_NeverRunBeforeTheirDependencies(int round)
        {
            const int n = 20000;
            var stamp = new int[n];
            var violations = new int[1];
            var clock = new int[1];
            var handles = new NJobHandle[n];
            var rng = new Random(round);
            for (var i = 0; i < n; i++)
            {
                NJobHandle dependsOn = default;
                int[] dependencies;
                if (i == 0)
                    dependencies = Array.Empty<int>();
                else if (round == 0)
                    dependencies = new[] { i - 1 };
                else if (round == 1)
                    dependencies = new[] { rng.Next(0, i) };
                else
                    dependencies = new[] { rng.Next(0, i), rng.Next(0, i), rng.Next(0, i) };
                foreach (var d in dependencies) dependsOn = NJobHandle.Combine(dependsOn, handles[d]);
                handles[i] = new StampJob { Id = i, Dependencies = dependencies, Stamp = stamp, Violations = violations, Clock = clock }
                    .Schedule(dependsOn);
                if (rng.Next(100) == 0) handles[rng.Next(0, i + 1)].Complete();
            }
            var buffer = new NJobHandleBuffer();
            buffer.EnsureCapacity(n);
            for (var i = 0; i < n; i++) buffer[i] = handles[i];
            buffer.Combine(n).Complete();
            buffer.Dispose();
            Assert.That(stamp.All(s => s > 0), "every job ran");
            Assert.AreEqual(0, violations[0], "jobs that ran before a dependency");
            Assert.That(handles.All(h => h.IsCompleted), "handles of reused job records stay completed");
        }

        [Test]
        public void ThrowingJob_IsReportedAndCompletes()
        {
            var handle = new ThrowJob().Schedule();
            handle.Complete();
            Assert.IsTrue(handle.IsCompleted);
        }

        [Test]
        public void DefaultHandle_IsCompleted()
        {
            NJobHandle handle = default;
            handle.Complete();
            Assert.IsTrue(handle.IsCompleted);
            Assert.IsTrue(handle.IsDefault);
        }

        private static readonly Threads[] ThreadModes = { Threads.Parallel, Threads.Single, Threads.MainRun, Threads.Main };
        private static readonly int[] GraphModes = { -1, 0, 1, 2, 3 };

        [Test]
        public void Systems_RunInEveryThreadAndGraphMode([ValueSource(nameof(ThreadModes))] Threads threads,
            [ValueSource(nameof(GraphModes))] int graphMode)
        {
            var world = World.Create(WorldConfig.Default65536);
            try
            {
                const int count = 50000;
                for (var i = 0; i < count; i++) world.Entity(new SchedPos { X = 0 }, new SchedVel { X = 1 });
                world.Update();
                var systems = new Systems(ref world)
                    .Add(SchedulerTestSystems.Move, threads)
                    .Add(SchedulerTestSystems.Mark, threads);
                if (graphMode >= 0) systems.UseDependencyGraph(true, (GroupScheduleMode)graphMode);
                for (var frame = 1; frame <= 12; frame++) systems.OnUpdate(0.016f, frame * 0.016f);
                world.Update();
                Assert.AreEqual(count, world.Query().With<SchedTagged>().Count);
                foreach (ref var e in world.Query().With<SchedPos>())
                    Assert.AreEqual(12f, e.Get<SchedPos>().X);
            }
            finally
            {
                world.Dispose();
            }
        }
    }
}
