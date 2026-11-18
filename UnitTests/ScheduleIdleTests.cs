using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct IdleChainRes : IRes
    {
        public int Value;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }

    public struct IdleRow : IComponent
    {
        public int Visits;
    }

    // Every step writes the same resource, so the graph orders them in registration order and each
    // depends on all earlier ones; the digits of Value record the order they ran in.
    public static partial class IdleChainSystems
    {
        [System]
        public static void Step1(ref Res<IdleChainRes> res) => res.Ref.Value = res.Ref.Value * 10 + 1;

        [System]
        public static void Step2(ref Res<IdleChainRes> res) => res.Ref.Value = res.Ref.Value * 10 + 2;

        [System]
        public static void Step3(ref Res<IdleChainRes> res) => res.Ref.Value = res.Ref.Value * 10 + 3;

        [System]
        public static void Step4(ref Res<IdleChainRes> res) => res.Ref.Value = res.Ref.Value * 10 + 4;

        [System]
        public static void Visit(ref Query<IdleRow> query)
        {
            foreach (ref var row in query)
                row.Visits += 1;
        }
    }

    [TestFixture]
    public class ScheduleIdleTests
    {
        [SetUp]
        public void SetUp() => World.DisposeStatic();

        [TearDown]
        public void TearDown() => World.DisposeStatic();

        [Test]
        public void Flattened2_ResourceChain_KeepsOnePredecessorAndOrder()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new IdleChainRes());
            var systems = new Systems(ref world);
            systems.Add(IdleChainSystems.Step1, Threads.Single);
            systems.Add(IdleChainSystems.Step2, Threads.Single);
            systems.Add(IdleChainSystems.Step3, Threads.Single);
            systems.Add(IdleChainSystems.Step4, Threads.Single);
            systems.UseDependencyGraph(mode: GroupScheduleMode.FlattenedSchedule2);

            for (int tick = 0; tick < 3; tick++)
            {
                world.GetRes<IdleChainRes>().Value = 0;
                systems.OnUpdate(1f, tick);
                Assert.AreEqual(1234, world.GetRes<IdleChainRes>().Value, "Steps ran once each in registration order");
            }

            var preds = systems.DependencyGraph.GetPredecessors();
            var reduced = systems.GetReducedJobPredecessors();
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, preds[3], "The graph keeps every conflicting predecessor");
            CollectionAssert.AreEqual(new[] { 2 }, reduced[3], "Scheduling waits for the previous step only");
            CollectionAssert.AreEqual(new[] { 1 }, reduced[2]);
            CollectionAssert.IsEmpty(reduced[0]);

            world.Dispose();
        }

        [Test]
        public void Flattened2_MainSystemInChain_RunsFirstAndJobsKeepOrder()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new IdleChainRes());
            var systems = new Systems(ref world);
            systems.Add(IdleChainSystems.Step1, Threads.Single);
            systems.Add(IdleChainSystems.Step2, Threads.Main);
            systems.Add(IdleChainSystems.Step3, Threads.Single);
            systems.Add(IdleChainSystems.Step4, Threads.Single);
            systems.UseDependencyGraph(mode: GroupScheduleMode.FlattenedSchedule2);

            world.GetRes<IdleChainRes>().Value = 0;
            systems.OnUpdate(1f, 1f);
            // Flattened2 runs Main systems before any job.
            Assert.AreEqual(2134, world.GetRes<IdleChainRes>().Value);
            var reduced = systems.GetReducedJobPredecessors();
            CollectionAssert.AreEqual(new[] { 0 }, reduced[2], "Main predecessors have no handle to wait for");
            CollectionAssert.AreEqual(new[] { 2 }, reduced[3]);

            world.Dispose();
        }

        [TestCase(1, 1)]
        [TestCase(8, 1)]
        [TestCase(8, 16)]
        [TestCase(64, 1)]
        public void ParallelQuery_FinerBatches_VisitEveryRowOnce(int perWorker, int minSize)
        {
            int savedPerWorker = NJobs.ParallelBatching.PerWorker, savedMin = NJobs.ParallelBatching.MinSize;
            NJobs.ParallelBatching.PerWorker = perWorker;
            NJobs.ParallelBatching.MinSize = minSize;
            try
            {
                var world = World.Create(WorldConfig.Default16384);
                var systems = new Systems(ref world);
                systems.Add(IdleChainSystems.Visit, Threads.Parallel);
                const int count = 5003;
                world.GetArchetype(typeof(IdleRow)).BatchCreateEntity(count);
                world.Update();

                systems.OnUpdate(1f, 1f);
                systems.OnUpdate(1f, 2f);

                int rows = 0;
                foreach (var row in world.Query().With<IdleRow>())
                {
                    Assert.AreEqual(2, row.Get<IdleRow>().Visits);
                    rows++;
                }
                Assert.AreEqual(count, rows);
                world.Dispose();
            }
            finally
            {
                NJobs.ParallelBatching.PerWorker = savedPerWorker;
                NJobs.ParallelBatching.MinSize = savedMin;
            }
        }

        [Test]
        public void ParallelBatchSize_OneBatchPerWorkerWhenSetToOne()
        {
            int savedPerWorker = NJobs.ParallelBatching.PerWorker, savedMin = NJobs.ParallelBatching.MinSize;
            try
            {
                NJobs.ParallelBatching.PerWorker = 1;
                NJobs.ParallelBatching.MinSize = 1;
                var workers = System.Math.Max(1, NJobs.WorkerCount);
                Assert.AreEqual(System.Math.Max(1, 1000 / workers), NJobs.ParallelBatchSize(1000));
                Assert.AreEqual(1, NJobs.ParallelBatchSize(0));
                NJobs.ParallelBatching.PerWorker = 8;
                NJobs.ParallelBatching.MinSize = 16;
                Assert.AreEqual(16, NJobs.ParallelBatchSize(10));
                Assert.AreEqual(System.Math.Max(16, 100000 / (workers * 8)), NJobs.ParallelBatchSize(100000));
            }
            finally
            {
                NJobs.ParallelBatching.PerWorker = savedPerWorker;
                NJobs.ParallelBatching.MinSize = savedMin;
            }
        }
    }
}
