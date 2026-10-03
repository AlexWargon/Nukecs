using System;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;

namespace Wargon.Nukecs.Tests
{
    [TestFixture]
    public unsafe class RuntimeQueryIter4PagedBenchmarks
    {
        [TestCase(0, false), TestCase(1, false), TestCase(2, false), TestCase(4, false)]
        [TestCase(0, true), TestCase(1, true), TestCase(2, true), TestCase(4, true)]
        [Performance]
        public void Compare(int pools, bool shuffledAndFiltered)
        {
            switch (pools)
            {
                case 0: Compare<Qi4A, Qi4B, Qi4C, Qi4D>(shuffledAndFiltered); break;
                case 1: Compare<Qi4A, Qi4B, Qi4C, RuntimeQi4PoolA>(shuffledAndFiltered); break;
                case 2: Compare<Qi4A, RuntimeQi4PoolB, Qi4C, RuntimeQi4PoolA>(shuffledAndFiltered); break;
                case 4: Compare<RuntimeQi4PoolA, RuntimeQi4PoolB, RuntimeQi4PoolC, RuntimeQi4PoolD>(shuffledAndFiltered); break;
            }
        }

        private static void Compare<T1, T2, T3, T4>(bool shuffled)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            var hasPools = ComponentType<T1>.Data.category == ComponentCategory.Pool ||
                ComponentType<T2>.Data.category == ComponentCategory.Pool ||
                ComponentType<T3>.Data.category == ComponentCategory.Pool ||
                ComponentType<T4>.Data.category == ComponentCategory.Pool;
            var reverse = Environment.GetEnvironmentVariable("NUKECS_PAGED_REVERSE") == "1";
            TestContext.WriteLine("Reverse measurement order: " + reverse);
            var order = reverse ? new[] { 2, 1, 0 } : new[] { 0, 1, 2 };
            foreach (var variant in order)
                if (variant != 2 || hasPools)
                    MeasureVariant<T1, T2, T3, T4>(shuffled, variant);
        }

        private static void MeasureVariant<T1, T2, T3, T4>(bool shuffled, int variant)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            const int count = 100000;
            var world = World.Create(WorldConfig.Default_1_000_000);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4));
                var entities = new Entity[count];
                if (shuffled)
                {
                    for (var i = 0; i < count; i++) entities[i] = arch.CreateEntity();
                    var random = new Random(431);
                    for (var i = count - 1; i > 0; i--)
                    {
                        var j = random.Next(i + 1);
                        var tmp = entities[i]; entities[i] = entities[j]; entities[j] = tmp;
                    }
                    // Recycled IDs are deliberately unrelated to the new physical row order.
                    for (var i = 0; i < count; i++)
                    {
                        entities[i].DestroyNow();
                        // DestroyNow enqueues ECB commands; flush small shuffled batches
                        // so sorting all commands by ID cannot undo the shuffle.
                        if ((i & 255) == 255) world.Update();
                    }
                    world.Update();
                }
                for (var i = 0; i < count; i++)
                {
                    var e = arch.CreateEntity();
                    entities[i] = e;
                    Head<T1>(e) = i; Head<T2>(e) = 1;
                    Head<T3>(e) = i; Head<T4>(e) = 1;
                    if (shuffled && i % 3 == 0) e.Add<Qi4Tag>();
                }
                world.Update();
                var query = new Query<T1, T2, T3, T4, None<Qi4Tag>>();
                var wp = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
                query.Init(ref wp);
                query.Update(ref world, IntPtr.Zero);
                var expectedCount = shuffled ? count - (count + 2) / 3 : count;
                Assert.AreEqual(expectedCount, query.Count);
                if (shuffled)
                {
                    var nonConsecutive = 0;
                    for (var i = 1; i < count; i++)
                        if (entities[i].id != entities[i - 1].id + 1) nonConsecutive++;
                    Assert.Greater(nonConsecutive, count / 2, "Fixture did not shuffle entity IDs.");
                }

                Action update = variant == 2 ? (Action)(() => PoolStep(ref query)) : variant == 1 ? (Action)(() => Paged(ref query)) : () => Mixed(ref query);
                Measure.Method(update).SampleGroup(new SampleGroup(variant == 2 ? "PoolStep" : variant == 1 ? "Paged" : "Mixed", SampleUnit.Millisecond))
                    .WarmupCount(10).MeasurementCount(100).IterationsPerMeasurement(1).Run();
                for (var i = 0; i < count; i++)
                {
                    var expected = i + (shuffled && i % 3 == 0 ? 0 : 110);
                    if (Head<T1>(entities[i]) != expected || Head<T3>(entities[i]) != expected ||
                        Head<T2>(entities[i]) != 1 || Head<T4>(entities[i]) != 1)
                        Assert.Fail("Incorrect row " + i);
                }
            }
            finally { world.Dispose(); }
        }

        // These fixture components all have an int as their first field.
        private static ref int Head<T>(Entity e) where T : unmanaged, IComponent
        {
            ref var value = ref e.Get<T>();
            return ref *(int*)UnsafeUtility.AddressOf(ref value);
        }

        private static void Mixed<T1, T2, T3, T4>(ref Query<T1, T2, T3, T4, None<Qi4Tag>> query)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            foreach (var (a, b, c, d) in query.iter_mixed_runtime())
            {
                *(int*)a.data += *(int*)b.data;
                *(int*)c.data += *(int*)d.data;
            }
        }

        private static void Paged<T1, T2, T3, T4>(ref Query<T1, T2, T3, T4, None<Qi4Tag>> query)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            foreach (var (a, b, c, d) in query.iter_paged_runtime())
            {
                *(int*)a.data += *(int*)b.data;
                *(int*)c.data += *(int*)d.data;
            }
        }
        private static void PoolStep<T1, T2, T3, T4>(ref Query<T1, T2, T3, T4, None<Qi4Tag>> query)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            foreach (var (a, b, c, d) in query.iter_paged_pool_runtime())
            {
                *(int*)a.data += *(int*)b.data;
                *(int*)c.data += *(int*)d.data;
            }
        }
    }
}
