using NUnit.Framework;
using Unity.Burst;

namespace Wargon.Nukecs.Tests
{
    public struct ResizeTestValue : IComponent
    {
        public int Value;
    }

    public struct ResizeTestMultiplier : IComponent
    {
        public int Factor;
    }

    public struct ResizeTestMarkDestroyed : IComponent
    {
        public int Dummy;
    }

    // ========== ISystem (main thread, class) ==========

    public class ResizeIncrementMainSystem : ISystem, IOnCreate
    {
        private Query query;
        public void OnCreate(ref World world) => query = world.Query().With<ResizeTestValue>();
        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in query)
                entity.Get<ResizeTestValue>().Value += 1;
        }
    }

    public class ResizeMultiplyMainSystem : ISystem, IOnCreate
    {
        private Query query;
        public void OnCreate(ref World world) => query = world.Query().With<ResizeTestValue>().With<ResizeTestMultiplier>();
        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in query)
            {
                ref var v = ref entity.Get<ResizeTestValue>();
                ref var m = ref entity.Get<ResizeTestMultiplier>();
                v.Value *= m.Factor;
            }
        }
    }

    public class ResizeDestroyMainSystem : ISystem, IOnCreate
    {
        private Query query;
        public void OnCreate(ref World world) => query = world.Query().With<ResizeTestMarkDestroyed>();
        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in query)
                entity.Destroy();
        }
    }

    // ========== IEntityJobSystem (burst, single/parallel) ==========

    [BurstCompile]
    public struct ResizeIncrementJobSystem : IEntityJobSystem
    {
        public readonly Threads Mode => Threads.Single;
        public readonly Query GetQuery(ref World world) => world.Query().With<ResizeTestValue>();
        public readonly void OnUpdate(ref Entity entity, ref State state) => entity.Get<ResizeTestValue>().Value += 1;
    }

    [BurstCompile]
    public struct ResizeMultiplyJobSystem : IEntityJobSystem
    {
        public readonly Threads Mode => Threads.Single;
        public readonly Query GetQuery(ref World world) => world.Query().With<ResizeTestValue>().With<ResizeTestMultiplier>();
        public readonly void OnUpdate(ref Entity entity, ref State state)
        {
            ref var v = ref entity.Get<ResizeTestValue>();
            ref var m = ref entity.Get<ResizeTestMultiplier>();
            v.Value *= m.Factor;
        }
    }

    [BurstCompile]
    public struct ResizeDestroyJobSystem : IEntityJobSystem
    {
        public readonly Threads Mode => Threads.Single;
        public readonly Query GetQuery(ref World world) => world.Query().With<ResizeTestMarkDestroyed>();
        public readonly void OnUpdate(ref Entity entity, ref State state) => entity.Destroy();
    }

    [BurstCompile]
    public struct ResizeIncrementParallelJobSystem : IEntityJobSystem
    {
        public readonly Threads Mode => Threads.Parallel;
        public readonly Query GetQuery(ref World world) => world.Query().With<ResizeTestValue>();

        public readonly void OnUpdate(ref Entity entity, ref State state)
        {
            entity.Get<ResizeTestValue>().Value += 1;
            //dbug.log($"ResizeTestValue.Value {entity.Get<ResizeTestValue>().Value}");
        }
    }

    [BurstCompile]
    public struct ResizeMultiplyParallelJobSystem : IEntityJobSystem
    {
        public readonly Threads Mode => Threads.Parallel;
        public readonly Query GetQuery(ref World world) => world.Query().With<ResizeTestValue>().With<ResizeTestMultiplier>();
        public readonly void OnUpdate(ref Entity entity, ref State state)
        {
            ref var v = ref entity.Get<ResizeTestValue>();
            ref var m = ref entity.Get<ResizeTestMultiplier>();
            v.Value *= m.Factor;
        }
    }

    // ========== Static [System] methods ==========

    [BurstCompile]
    public struct ResizeStaticSystems
    {
        [BurstCompile, System]
        public static void Increment(ref Query<ResizeTestValue> query)
        {
            foreach (ref var v in query)
                v.Value += 1;
        }

        [BurstCompile, System]
        public static void Multiply(ref Query<ResizeTestValue, ResizeTestMultiplier> query)
        {
            foreach (var (v, m) in query)
                v.Val.Value *= m.Val.Factor;
        }

        [BurstCompile, System]
        public static void DestroyMarked(ref Query<ResizeTestMarkDestroyed>.WithEntity query, ref State state)
        {
            foreach (var (entity, _) in query)
                entity.Destroy();
        }
    }

    [TestFixture]
    public class ResizeTests
    {
        [SetUp]
        public void SetUp() => World.DisposeStatic();

        [TearDown]
        public void TearDown() => World.DisposeStatic();

        // ===== Increment =====

        [Test]
        public void MainThread_IncrementBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<ResizeIncrementMainSystem>();
            const int total = 200;
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = 0 });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);
            AssertValues(ref world, ref query, total, 1);
            world.Dispose();
        }

        [Test]
        public void JobSingle_IncrementBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<ResizeIncrementJobSystem>();
            const int total = 200;
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = 0 });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);
            AssertValues(ref world, ref query, total, 1);
            world.Dispose();
        }

        [Test]
        public void JobParallel_IncrementBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<ResizeIncrementParallelJobSystem>();
            const int total = 200;
            for (int i = 0; i < total; i++)
                world.Entity().Add(new ResizeTestValue { Value = 0 });
            world.Update();
            systems.OnUpdate(1f, 1f);
            AssertValues(ref world, ref query, total, 1);
            world.Dispose();
        }

        [Test]
        public void StaticSystem_IncrementBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add(ResizeStaticSystems.Increment);
            const int total = 200;
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = 0 });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);
            AssertValues(ref world, ref query, total, 1);
            world.Dispose();
        }

        // ===== Multiply =====

        [Test]
        public void MainThread_MultiplyBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var systems = new Systems(ref world).AddDefaults().Add<ResizeMultiplyMainSystem>();
            RunMultiplyAndAssert(ref world, ref systems);
        }

        [Test]
        public void JobSingle_MultiplyBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var systems = new Systems(ref world).AddDefaults().Add<ResizeMultiplyJobSystem>();
            RunMultiplyAndAssert(ref world, ref systems);
        }

        [Test]
        public void JobParallel_MultiplyBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var systems = new Systems(ref world).AddDefaults().Add<ResizeMultiplyParallelJobSystem>();
            RunMultiplyAndAssert(ref world, ref systems);
        }

        [Test]
        public void StaticSystem_MultiplyBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var systems = new Systems(ref world).AddDefaults().Add(ResizeStaticSystems.Multiply);
            RunMultiplyAndAssert(ref world, ref systems);
        }

        // ===== Destroy + recreate =====

        [Test]
        public void MainThread_DestroyAndRecreateBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var systems = new Systems(ref world).AddDefaults().Add<ResizeDestroyMainSystem>();
            DestroyAndRecreateInner(ref world, ref systems);
        }

        [Test]
        public void JobSingle_DestroyAndRecreateBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var systems = new Systems(ref world).AddDefaults().Add<ResizeDestroyJobSystem>();
            DestroyAndRecreateInner(ref world, ref systems);
        }

        [Test]
        public void StaticSystem_DestroyAndRecreateBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var systems = new Systems(ref world).AddDefaults().Add(ResizeStaticSystems.DestroyMarked);
            DestroyAndRecreateInner(ref world, ref systems);
        }

        // ===== Multiple resizes, data preserved =====

        [Test]
        public void MainThread_MultipleResizes_DataPreserved()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<ResizeIncrementMainSystem>();
            MultipleResizesAndAssert(ref world, ref query,ref systems);
        }

        [Test]
        public void JobSingle_MultipleResizes_DataPreserved()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<ResizeIncrementJobSystem>();
            MultipleResizesAndAssert(ref world, ref query, ref systems);
        }

        [Test]
        public void JobParallel_MultipleResizes_DataPreserved()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<ResizeIncrementParallelJobSystem>();
            MultipleResizesAndAssert(ref world, ref query, ref systems);
        }

        [Test]
        public void StaticSystem_MultipleResizes_DataPreserved()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add(ResizeStaticSystems.Increment);
            MultipleResizesAndAssert(ref world, ref query, ref systems);
        }

        // ===== Determinism across modes =====

        [Test]
        public void ResultsMatch_AllIncrementModes()
        {
            int[] mainValues = RunIncrementPass_Systems<ResizeIncrementMainSystem>();
            int[] jobValues = RunIncrementPass_Job<ResizeIncrementJobSystem>();
            int[] parallelValues = RunIncrementPass_Job<ResizeIncrementParallelJobSystem>();

            Assert.AreEqual(mainValues.Length, jobValues.Length, "Job count != Main count");
            Assert.AreEqual(mainValues.Length, parallelValues.Length, "Parallel count != Main count");
            for (int i = 0; i < mainValues.Length; i++)
            {
                Assert.AreEqual(mainValues[i], jobValues[i], $"Job mismatch at {i}");
                Assert.AreEqual(mainValues[i], parallelValues[i], $"Parallel mismatch at {i}");
            }
        }

        [Test]
        public void ResultsMatch_AllMultiplyModes()
        {
            int[] mainValues = RunMultiplyPass_Systems<ResizeMultiplyMainSystem>();
            int[] jobValues = RunMultiplyPass_Job<ResizeMultiplyJobSystem>();
            int[] parallelValues = RunMultiplyPass_Job<ResizeMultiplyParallelJobSystem>();

            Assert.AreEqual(mainValues.Length, jobValues.Length, "Job count != Main count");
            Assert.AreEqual(mainValues.Length, parallelValues.Length, "Parallel count != Main count");
            for (int i = 0; i < mainValues.Length; i++)
            {
                Assert.AreEqual(mainValues[i], jobValues[i], $"Job mismatch at {i}");
                Assert.AreEqual(mainValues[i], parallelValues[i], $"Parallel mismatch at {i}");
            }
        }

        // ===== Helpers =====

        private static void AssertValues(ref World world, ref Query query , int expectedCount, int expectedValue)
        {
            int count = 0;
            world.Update();
            dbug.log($"query.Count:{query.Count}");
            foreach (ref var entity in query)
            {
                Assert.AreEqual(expectedValue, entity.Get<ResizeTestValue>().Value,
                    $"Entity {entity.id} wrong value");
                count++;
            }
            Assert.AreEqual(expectedCount, count);
        }

        private static void RunMultiplyAndAssert(ref World world, ref Systems systems)
        {
            const int total = 128;
            var query = world.Query().With<ResizeTestValue>();
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = i }, new ResizeTestMultiplier { Factor = 3 });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);

            int count = 0;
            foreach (ref var entity in query)
            {
                Assert.AreEqual(count * 3, entity.Get<ResizeTestValue>().Value,
                    $"Entity {entity.id} wrong multiply result");
                count++;
            }
            Assert.AreEqual(total, count);
            world.Dispose();
        }

        private static void DestroyAndRecreateInner(ref World world, ref Systems systems)
        {
            const int initial = 16;
            var query = world.Query().With<ResizeTestValue>();
            for (int i = 0; i < initial; i++)
                world.Entity(new ResizeTestValue { Value = i }, new ResizeTestMarkDestroyed());
            world.Update();
            Assert.AreEqual(initial, world.EntitiesAmount);

            systems.OnUpdate(0.016f, 0.016f);
            systems.OnUpdate(0.016f, 0.016f);
            systems.OnUpdate(0.016f, 0.016f);

            Assert.AreEqual(0, query.Count, "All marked entities should be destroyed");
            world.Update();
            const int nextBatch = 64;
            for (int i = 0; i < nextBatch; i++)
                world.Entity(new ResizeTestValue { Value = 100 + i });
            world.Update();
            Assert.AreEqual(nextBatch, world.EntitiesAmount);
            world.Dispose();
        }

        private static void MultipleResizesAndAssert(ref World world, ref Query query, ref Systems systems)
        {
            const int total = 300;
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = 0 });
            world.Update();

            systems.OnUpdate(0.016f, 0.016f);
            systems.OnUpdate(0.016f, 0.016f);
            systems.OnUpdate(0.016f, 0.016f);
            AssertValues(ref world, ref query, total, 3);
            world.Dispose();
        }

        private static int[] RunIncrementPass_Job<TJob>() where TJob : struct, IEntityJobSystem
        {
            World.DisposeStatic();
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<TJob>();
            const int total = 200;
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = i });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);

            var values = new int[query.Count];
            int idx = 0;
            foreach (ref var entity in query)
                values[idx++] = entity.Get<ResizeTestValue>().Value;
            world.Dispose();
            return values;
        }

        private static int[] RunIncrementPass_Systems<TSystem>() where TSystem : class, ISystem, new()
        {
            World.DisposeStatic();
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<TSystem>();
            const int total = 200;
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = i });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);

            var values = new int[query.Count];
            int idx = 0;
            foreach (ref var entity in query)
                values[idx++] = entity.Get<ResizeTestValue>().Value;
            world.Dispose();
            return values;
        }

        private static int[] RunMultiplyPass_Job<TJob>() where TJob : struct, IEntityJobSystem
        {
            World.DisposeStatic();
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<TJob>();
            const int total = 128;
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = i }, new ResizeTestMultiplier { Factor = 3 });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);

            var values = new int[query.Count];
            int idx = 0;
            foreach (ref var entity in query)
                values[idx++] = entity.Get<ResizeTestValue>().Value;
            world.Dispose();
            return values;
        }

        private static int[] RunMultiplyPass_Systems<TSystem>() where TSystem : class, ISystem, new()
        {
            World.DisposeStatic();
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<ResizeTestValue>();
            var systems = new Systems(ref world).AddDefaults().Add<TSystem>();
            const int total = 128;
            for (int i = 0; i < total; i++)
                world.Entity(new ResizeTestValue { Value = i }, new ResizeTestMultiplier { Factor = 3 });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);

            var values = new int[query.Count];
            int idx = 0;
            foreach (ref var entity in query)
                values[idx++] = entity.Get<ResizeTestValue>().Value;
            world.Dispose();
            return values;
        }
    }
}
