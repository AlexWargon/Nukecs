using System;
using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct RuntimeQi4Small : IComponent { public byte Value; }
    public struct RuntimeQi4Wide : IComponent { public long Left, Right; }
    public struct RuntimeQi4Mid : IComponent { public int Value; }
    public struct RuntimeQi4Double : IComponent { public double Value; }
    public struct RuntimeQi4Extra : IComponent { public int Value; }
    public struct RuntimeQi4EmptyExtra : IComponent { public int Value; }

    [TestFixture]
    public unsafe class RuntimeQueryIter4CandidateTests
    {
        private static Query<T1, T2, T3, T4> Init<T1, T2, T3, T4>(ref World world)
            where T1 : unmanaged where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged
        {
            var query = new Query<T1, T2, T3, T4>();
            var pointer = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            query.Init(ref pointer);
            query.Update(ref world, IntPtr.Zero);
            return query;
        }

        [Test]
        public void UnequalSizes_MultipleStorages_PreserveRowsAndCurrentSnapshot()
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var query = Init<RuntimeQi4Mid, RuntimeQi4Double, RuntimeQi4Small, RuntimeQi4Wide>(ref world);
                const int count = 129;
                var entities = new Entity[count];
                for (var i = 0; i < count; i++)
                {
                    var entity = world.Entity();
                    entity.Add(new RuntimeQi4Small { Value = (byte)i });
                    entity.Add(new RuntimeQi4Wide { Left = 1000 + i, Right = 2000 + i });
                    entity.Add(new RuntimeQi4Mid { Value = i });
                    entity.Add(new RuntimeQi4Double { Value = 10.25 + i });
                    if ((i & 1) == 0) entity.Add<RuntimeQi4Extra>();
                    if (i % 3 == 0) entity.Add<Qi4Tag>();
                    entities[i] = entity;
                }
                world.GetArchetype(typeof(RuntimeQi4Mid), typeof(RuntimeQi4Double),
                    typeof(RuntimeQi4Small), typeof(RuntimeQi4Wide), typeof(RuntimeQi4EmptyExtra));
                world.Update();
                query.Update(ref world, IntPtr.Zero);

                query.TryGetQuery(out var raw);
                Assert.IsTrue(raw.Ref.TryUseStorageIteration());
                Assert.GreaterOrEqual(raw.Ref.GetMatchingStorages().Length, 2);

                var iterator = query.iter_compact_runtime();
                Assert.IsTrue(iterator.MoveNext());
                var snapshot = iterator.Current;
                var (first, _, _, _) = snapshot;
                var firstId = first.Read.Value;
                Assert.IsTrue(iterator.MoveNext());
                var (stillFirst, _, _, _) = snapshot;
                Assert.AreEqual(firstId, stillFirst.Read.Value, "Current must remain a value snapshot.");

                var visited = new bool[count];
                var seen = 0;
                foreach (var (a, b, c, d) in query.iter_compact_runtime())
                {
                    var id = a.Read.Value;
                    Assert.IsFalse(visited[id], "Duplicate row.");
                    visited[id] = true;
                    Assert.AreEqual(10.25 + id, b.Read.Value);
                    Assert.AreEqual((byte)id, c.Read.Value);
                    Assert.AreEqual(1000 + id, d.Read.Left);
                    Assert.AreEqual(2000 + id, d.Read.Right);
                    a.Get.Value += 5;
                    b.Get.Value += 0.5;
                    c.Get.Value++;
                    d.Get.Right += 7;
                    seen++;
                }
                Assert.AreEqual(count, seen);
                for (var i = 0; i < count; i++)
                {
                    Assert.AreEqual(i + 5, entities[i].Get<RuntimeQi4Mid>().Value);
                    Assert.AreEqual(i + 10.75, entities[i].Get<RuntimeQi4Double>().Value);
                    Assert.AreEqual(i + 1, entities[i].Get<RuntimeQi4Small>().Value);
                    Assert.AreEqual(i + 2007, entities[i].Get<RuntimeQi4Wide>().Right);
                }
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void EmptyAndExhaustedIterator_ReturnFalse_AndNewIterationSeesNewRows()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = Init<Qi4A, Qi4B, Qi4C, Qi4D>(ref world);
                var iterator = query.iter_compact_runtime();
                Assert.IsFalse(iterator.MoveNext());
                Assert.IsFalse(iterator.MoveNext());
                var entity = world.Entity();
                entity.Add(new Qi4A { Value = 1 });
                entity.Add(new Qi4B { Value = 2 });
                entity.Add(new Qi4C { Value = 3 });
                entity.Add(new Qi4D { Value = 4 });
                world.Update();
                query.Update(ref world, IntPtr.Zero);
                iterator = query.iter_compact_runtime();
                Assert.IsTrue(iterator.MoveNext());
                var (a, b, c, d) = iterator.Current;
                Assert.AreEqual(10, a.Read.Value + b.Read.Value + c.Read.Value + d.Read.Value);
                Assert.IsFalse(iterator.MoveNext());
                Assert.IsFalse(iterator.MoveNext());
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void DiagnosticApi_RejectsTagsAndPools()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var tags = Init<Qi4A, Qi4B, Qi4C, Qi4Tag>(ref world);
                var pools = Init<Qi4A, Qi4B, Qi4C, Qi4Pool>(ref world);
                Assert.Throws<InvalidOperationException>(() => { tags.iter_compact_runtime(); });
                Assert.Throws<InvalidOperationException>(() => { pools.iter_compact_runtime(); });
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void DenseBlock_UsesCountSnapshot_WhenRowsAreAppended()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = Init<Qi4A, Qi4B, Qi4C, Qi4D>(ref world);
                var archetype = world.GetArchetype(typeof(Qi4A), typeof(Qi4B), typeof(Qi4C), typeof(Qi4D));
                archetype.CreateEntity().Get<Qi4A>().Value = 1;
                query.Update(ref world, IntPtr.Zero);
                var seen = 0;
                foreach (var (a, _, _, _) in query.iter_compact_runtime())
                {
                    seen++;
                    if (seen > 1) Assert.Fail("Iteration included a row appended after its count snapshot.");
                    archetype.CreateEntity().Get<Qi4A>().Value = 2;
                    Assert.AreEqual(1, a.Read.Value);
                }
                Assert.AreEqual(1, seen);
                query.Update(ref world, IntPtr.Zero);
                seen = 0;
                foreach (var (a, _, _, _) in query.iter_compact_runtime()) seen += a.Read.Value;
                Assert.AreEqual(3, seen);
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void DefaultNoneTypes_RejectDegradedStorage_ProductionIteratorRemainsExact()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = Init<Qi4A, Qi4B, Qi4C, Qi4D>(ref world);
                var first = world.Entity();
                first.Add(new Qi4A { Value = 1 });
                first.Add<Qi4B>(); first.Add<Qi4C>(); first.Add<Qi4D>();
                var prefab = world.Entity();
                prefab.Add(new Qi4A { Value = 100 });
                prefab.Add<Qi4B>(); prefab.Add<Qi4C>(); prefab.Add<Qi4D>();
                prefab.Add<IsPrefab>();
                world.Update();
                query.Update(ref world, IntPtr.Zero);

                Assert.Throws<InvalidOperationException>(() => { query.iter_compact_runtime(); });
                var sum = 0;
                foreach (var (a, _, _, _) in query.iter()) sum += a.Read.Value;
                Assert.AreEqual(1, sum);
                prefab.DestroyNow();
                world.Update();
                query.Update(ref world, IntPtr.Zero);
                sum = 0;
                foreach (var (a, _, _, _) in query.iter_compact_runtime()) sum += a.Read.Value;
                Assert.AreEqual(1, sum);
            }
            finally { world.Dispose(); }
        }
    }
}
