using NUnit.Framework;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Wargon.Nukecs.Tests
{
    public struct RuntimeQi4A : IComponent { public float3 Value; }
    public struct RuntimeQi4B : IComponent { public float3 Value; }
    public struct RuntimeQi4C : IComponent { public float3 Value; }
    public struct RuntimeQi4D : IComponent { public float3 Value; }

    public static class RuntimeQueryIter4GeneratedHarness
    {
        [System]
        public static void MixedPointerIteration(
            ref Query<RuntimeQi4A, RuntimeQi4B, RuntimeQi4C, RuntimeQi4D> query)
        {
            var countSnapshot = query.Count;
            foreach (var (a, b, c, d) in query.iter_mixed_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (countSnapshot < 0) dbug.log("unreachable");
        }

        [System]
        public static void CompactPointerIteration(
            ref Query<RuntimeQi4A, RuntimeQi4B, RuntimeQi4C, RuntimeQi4D> query)
        {
            var countSnapshot = query.Count;
            foreach (var (a, b, c, d) in query.iter_compact_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (countSnapshot < 0) dbug.log("unreachable");
        }

        [System]
        public static void RuntimeIteration(
            ref Query<RuntimeQi4A, RuntimeQi4B, RuntimeQi4C, RuntimeQi4D> query)
        {
            // A second statement deliberately keeps this harness out of the
            // source-generator batch rewrite while preserving the normal runner.
            var countSnapshot = query.Count;
            foreach (var (a, b, c, d) in query.iter())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (countSnapshot < 0) dbug.log("unreachable");
        }
    }

    public unsafe struct RuntimeQueryIter4PerformanceSystem : ISystem, IOnCreate
    {
        private Query<RuntimeQi4A, RuntimeQi4B, RuntimeQi4C, RuntimeQi4D> _query;

        public void OnCreate(ref World world)
        {
            var worldPtr = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            _query.Init(ref worldPtr);
        }

        public void OnUpdate(ref State state)
        {
            foreach (var (a, b, c, d) in _query.iter())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
        }
    }

    public unsafe struct RuntimeQueryIter4GenericPerformanceSystem : ISystem, IOnCreate
    {
        private Query<RuntimeQi4A, RuntimeQi4B, RuntimeQi4C, RuntimeQi4D> _query;

        public void OnCreate(ref World world)
        {
            var worldPtr = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            _query.Init(ref worldPtr);
        }

        public void OnUpdate(ref State state)
        {
            _query.TryGetQuery(out var query);
            var iterator = query.Ref.TryUseStorageIteration()
                ? new QueryIter<RefTuple<RuntimeQi4A, RuntimeQi4B, RuntimeQi4C, RuntimeQi4D>>(query.Ptr)
                : new QueryIter<RefTuple<RuntimeQi4A, RuntimeQi4B, RuntimeQi4C, RuntimeQi4D>>(
                    in query.Ref.matchingArchetypes, query.Ref.world);

            foreach (var (a, b, c, d) in iterator)
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
        }
    }

    public unsafe struct RuntimeQueryIter4PtrPerformanceSystem : ISystem, IOnCreate
    {
        private Query<RuntimeQi4A, RuntimeQi4B, RuntimeQi4C, RuntimeQi4D> _query;

        public void OnCreate(ref World world)
        {
            var worldPtr = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            _query.Init(ref worldPtr);
        }

        public void OnUpdate(ref State state)
        {
            foreach (var (a, b, c, d) in _query.iter_unsafe())
            {
                a->Value += b->Value;
                c->Value += d->Value;
            }
        }
    }

    public unsafe struct RuntimeQueryIter4System : ISystem, IOnCreate
    {
        private Query<Qi4A, Qi4B, Qi4C, Qi4D> _query;

        public void OnCreate(ref World world)
        {
            var worldPtr = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            _query.Init(ref worldPtr);
        }

        public void OnUpdate(ref State state)
        {
            foreach (var (a, b, c, d) in _query.iter())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
        }
    }

    public unsafe struct RuntimeQueryIter4NoneSystem : ISystem, IOnCreate
    {
        private Query<Qi4A, Qi4B, Qi4C, None<Qi4Tag>> _query;

        public void OnCreate(ref World world)
        {
            var worldPtr = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            _query.Init(ref worldPtr);
        }

        public void OnUpdate(ref State state)
        {
            foreach (var (a, b, c) in _query.iter())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += 1;
            }
        }
    }

    [TestFixture]
    public class RuntimeQueryIter4Tests
    {
        [Test]
        public void DenseInline_IteratesWithoutSourceGeneration()
        {
            const int count = 1024;
            var world = World.Create(WorldConfig.Default16384);
            var systems = new Systems(ref world).Add<RuntimeQueryIter4System>();
            var entities = world.BatchCreateEntity(count);

            for (var i = 0; i < entities.Length; i++)
            {
                entities[i].Add(new Qi4A { Value = i });
                entities[i].Add(new Qi4B { Value = 2 });
                entities[i].Add(new Qi4C { Value = i * 2 });
                entities[i].Add(new Qi4D { Value = 3 });
            }

            world.Update();
            systems.OnUpdate(1f, 1f);

            for (var i = 0; i < entities.Length; i++)
            {
                Assert.AreEqual(i + 2, entities[i].Get<Qi4A>().Value);
                Assert.AreEqual(i * 2 + 3, entities[i].Get<Qi4C>().Value);
            }

            world.Dispose();
        }

        [Test]
        public void NoneFilter_UsesGeneralRuntimeFallback()
        {
            const int count = 64;
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).Add<RuntimeQueryIter4NoneSystem>();
            var entities = world.BatchCreateEntity(count);

            for (var i = 0; i < entities.Length; i++)
            {
                entities[i].Add(new Qi4A { Value = i });
                entities[i].Add(new Qi4B { Value = 2 });
                entities[i].Add(new Qi4C { Value = i * 2 });
                if ((i & 1) == 0) entities[i].Add<Qi4Tag>();
            }

            world.Update();
            systems.OnUpdate(1f, 1f);

            for (var i = 0; i < entities.Length; i++)
            {
                var expectedA = (i & 1) == 0 ? i : i + 2;
                var expectedC = (i & 1) == 0 ? i * 2 : i * 2 + 1;
                Assert.AreEqual(expectedA, entities[i].Get<Qi4A>().Value);
                Assert.AreEqual(expectedC, entities[i].Get<Qi4C>().Value);
            }

            world.Dispose();
        }

        [Test, Performance]
        public void DenseInline_PerformanceWithoutSourceGeneration()
        {
            const int count = 100000;
            var world = World.Create(WorldConfig.Default_1_000_000);
            var systems = new Systems(ref world).Add<RuntimeQueryIter4PerformanceSystem>();
            var entities = world.BatchCreateEntity(count);
            var value = new float3(1, 2, 3);
            for (var i = 0; i < entities.Length; i++)
            {
                entities[i].Add(new RuntimeQi4A { Value = value });
                entities[i].Add(new RuntimeQi4B { Value = value });
                entities[i].Add(new RuntimeQi4C { Value = value });
                entities[i].Add(new RuntimeQi4D { Value = value });
            }

            world.Update();
            Measure.Method(() => systems.OnUpdate(1f, 1f))
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();

            world.Dispose();
        }

        [Test, Performance]
        public void DenseInline_GenericBaselineWithoutSourceGeneration()
        {
            const int count = 100000;
            var world = World.Create(WorldConfig.Default_1_000_000);
            var systems = new Systems(ref world).Add<RuntimeQueryIter4GenericPerformanceSystem>();
            var entities = world.BatchCreateEntity(count);
            var value = new float3(1, 2, 3);
            for (var i = 0; i < entities.Length; i++)
            {
                entities[i].Add(new RuntimeQi4A { Value = value });
                entities[i].Add(new RuntimeQi4B { Value = value });
                entities[i].Add(new RuntimeQi4C { Value = value });
                entities[i].Add(new RuntimeQi4D { Value = value });
            }

            world.Update();
            Measure.Method(() => systems.OnUpdate(1f, 1f))
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();

            world.Dispose();
        }

        [Test, Performance]
        public void DenseInline_PointerBaselineWithoutSourceGeneration()
        {
            const int count = 100000;
            var world = World.Create(WorldConfig.Default_1_000_000);
            var systems = new Systems(ref world).Add<RuntimeQueryIter4PtrPerformanceSystem>();
            var entities = world.BatchCreateEntity(count);
            var value = new float3(1, 2, 3);
            for (var i = 0; i < entities.Length; i++)
            {
                entities[i].Add(new RuntimeQi4A { Value = value });
                entities[i].Add(new RuntimeQi4B { Value = value });
                entities[i].Add(new RuntimeQi4C { Value = value });
                entities[i].Add(new RuntimeQi4D { Value = value });
            }

            world.Update();
            Measure.Method(() => systems.OnUpdate(1f, 1f))
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();

            world.Dispose();
        }

        [Test, Performance]
        public void DenseInline_RuntimeIterationWithSystemRunner()
        {
            MeasureGeneratedRunner(0);
        }

        [Test, Performance]
        public void DenseInline_CompactPointerWithSystemRunner()
        {
            MeasureGeneratedRunner(1);
        }

        [Test, Performance]
        public void DenseInline_MixedPointerWithSystemRunner()
        {
            MeasureGeneratedRunner(2);
        }

        private static void MeasureGeneratedRunner(int variant)
        {
            const int count = 100000;
            var world = World.Create(WorldConfig.Default_1_000_000);
            try
            {
                var systems = new Systems(ref world);
                if (variant == 2) systems.Add(RuntimeQueryIter4GeneratedHarness.MixedPointerIteration, Threads.Main);
                else if (variant == 1) systems.Add(RuntimeQueryIter4GeneratedHarness.CompactPointerIteration, Threads.Main);
                else systems.Add(RuntimeQueryIter4GeneratedHarness.RuntimeIteration, Threads.Main);
                var entities = world.BatchCreateEntity(count);
                var value = new float3(1, 2, 3);
                for (var i = 0; i < entities.Length; i++)
                {
                    entities[i].Add(new RuntimeQi4A { Value = value });
                    entities[i].Add(new RuntimeQi4B { Value = value });
                    entities[i].Add(new RuntimeQi4C { Value = value });
                    entities[i].Add(new RuntimeQi4D { Value = value });
                }

                world.Update();
                Measure.Method(() => systems.OnUpdate(1f, 1f))
                    .WarmupCount(10)
                    .MeasurementCount(100)
                    .IterationsPerMeasurement(1)
                    .Run();

                // 10 warmups + 100 measured updates. Verify every row outside timing.
                for (var i = 0; i < entities.Length; i++)
                {
                    if (!math.all(value * 111 == entities[i].Get<RuntimeQi4A>().Value) ||
                        !math.all(value * 111 == entities[i].Get<RuntimeQi4C>().Value) ||
                        !math.all(value == entities[i].Get<RuntimeQi4B>().Value) ||
                        !math.all(value == entities[i].Get<RuntimeQi4D>().Value))
                        Assert.Fail("Incorrect component values at row " + i);
                }

            }
            finally { world.Dispose(); }
        }
    }
}
