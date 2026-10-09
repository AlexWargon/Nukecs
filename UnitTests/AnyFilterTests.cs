using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;

namespace Wargon.Nukecs.Tests
{
    public struct AnyA : IComponent { public int V; }
    public struct AnyB : IComponent { public int V; }
    public struct AnyC : IComponent { public int V; }
    public struct AnyD : IComponent { public int V; }
    public struct AnyTag : IComponent { }
    public struct AnyPool : IPoolComponent { public int V; }

    public static partial class AnyFilterSystems
    {
        [System, BurstCompile, RequireBatch]
        public static void IncrementAnyBC(ref Query<AnyA, (Any<AnyB, AnyC>, None<AnyD>)> query)
        {
            foreach (ref var a in query) a.V++;
        }

        [System, BurstCompile, RequireBatch]
        public static void IncrementAnyTagPool(ref Query<AnyA, Any<AnyTag, AnyPool>> query)
        {
            foreach (ref var a in query) a.V++;
        }
    }

    [TestFixture]
    public unsafe class AnyFilterTests
    {
        private static HashSet<int> Ids(Query query)
        {
            var ids = new HashSet<int>();
            foreach (ref var e in query) ids.Add(e.id);
            return ids;
        }

        private static Entity Entity3<T1, T2, T3>(ref World world, T1 c1, T2 c2, T3 c3)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent
        {
            var e = world.Entity(c1, c2);
            e.Add(c3);
            return e;
        }

        private static Query TypedAnyQuery(ref World world)
        {
            var typed = new Query<AnyA, (Any<AnyB, AnyC>, None<AnyD>)>();
            typed.Init(ref world.unsafeWorldPtr);
            return new Query(typed._query);
        }

        [Test]
        public void Matches_ArchetypesCreatedBeforeAndAfterQuery()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var ab = world.Entity(new AnyA(), new AnyB());
                var a = world.Entity(new AnyA());
                world.Update();

                var fluent = world.Query().With<AnyA>().Any<AnyB>().Any<AnyC>();
                var typed = TypedAnyQuery(ref world);
                Assert.AreEqual(1, fluent.Count);
                Assert.AreEqual(1, typed.Count);

                // archetypes that appear after both queries exist
                var ac = world.Entity(new AnyA(), new AnyC());
                var abc = Entity3(ref world, new AnyA(), new AnyB(), new AnyC());
                var acd = Entity3(ref world, new AnyA(), new AnyC(), new AnyD());
                world.Update();

                CollectionAssert.AreEquivalent(new[] { ab.id, ac.id, abc.id, acd.id }, Ids(fluent));
                CollectionAssert.AreEquivalent(new[] { ab.id, ac.id, abc.id }, Ids(typed), "None<AnyD> must still exclude.");
                Assert.IsFalse(Ids(fluent).Contains(a.id));
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void EntityEntersAndLeaves_WhenAnyComponentsChange()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = world.Query().With<AnyA>().Any<AnyB>().Any<AnyC>();
                var e = world.Entity(new AnyA());
                world.Update();
                Assert.AreEqual(0, query.Count, "no Any component");

                e.Add(new AnyB());
                world.Update();
                Assert.AreEqual(1, query.Count, "one Any component");

                e.Add(new AnyC());
                world.Update();
                Assert.AreEqual(1, query.Count, "two Any components count once");

                e.Remove<AnyB>();
                world.Update();
                Assert.AreEqual(1, query.Count, "back to one");

                e.Remove<AnyC>();
                world.Update();
                Assert.AreEqual(0, query.Count, "zero Any components");
                Assert.AreEqual(0, Ids(query).Count);
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void CombinesWithWithAndNone_AndWorksAlone()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var anyOnly = world.Query().Any<AnyB>().Any<AnyC>();
                var combined = world.Query().With<AnyA>().None<AnyD>().Any<AnyB>().Any<AnyC>();
                var b = world.Entity(new AnyB());
                var c = world.Entity(new AnyC());
                var ab = world.Entity(new AnyA(), new AnyB());
                var abd = Entity3(ref world, new AnyA(), new AnyB(), new AnyD());
                var a = world.Entity(new AnyA());
                var d = world.Entity(new AnyD());
                world.Update();

                CollectionAssert.AreEquivalent(new[] { b.id, c.id, ab.id, abd.id }, Ids(anyOnly));
                CollectionAssert.AreEquivalent(new[] { ab.id }, Ids(combined));
                Assert.AreEqual(4, anyOnly.Count);
                Assert.AreEqual(1, combined.Count);
                Assert.IsFalse(Ids(anyOnly).Contains(a.id) || Ids(anyOnly).Contains(d.id));
                StringAssert.Contains(".Any<AnyB>()", combined.ToString());
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void InlineOnlyAny_UsesDenseStorageIteration()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = world.Query().With<AnyA>().Any<AnyB>().Any<AnyC>();
                var ab = world.Entity(new AnyA(), new AnyB());
                var ac = world.Entity(new AnyA(), new AnyC());
                world.Entity(new AnyA());
                world.Update();

                Assert.IsTrue(query.queryUnsafe->UseStorageIteration(), "inline Any must keep the storage path");
                Assert.AreEqual(0, query.queryUnsafe->storageDegraded);
                Assert.AreEqual(2, query.queryUnsafe->GetMatchingStorages().length);
                Assert.AreEqual(2, query.Count);
                CollectionAssert.AreEquivalent(new[] { ab.id, ac.id }, Ids(query));
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void TagAny_DenseWhenStorageIsUniform_DegradedWhenMixed()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = world.Query().With<AnyA>().Any<AnyTag>();
                var tagged = world.Entity(new AnyA());
                tagged.Add<AnyTag>();
                world.Update();

                Assert.IsTrue(query.queryUnsafe->UseStorageIteration(), "every non-empty row satisfies Any");
                CollectionAssert.AreEquivalent(new[] { tagged.id }, Ids(query));

                // the same storage now also holds a row without the tag
                var plain = world.Entity(new AnyA());
                world.Update();
                Assert.IsFalse(query.queryUnsafe->UseStorageIteration(), "mixed storage must degrade");
                Assert.AreEqual(1, query.queryUnsafe->storageDegraded);
                Assert.AreEqual(1, query.Count);
                CollectionAssert.AreEquivalent(new[] { tagged.id }, Ids(query));
                Assert.IsFalse(Ids(query).Contains(plain.id));

                // no row satisfies Any any more: storage skipped, not degraded
                tagged.Remove<AnyTag>();
                world.Update();
                Assert.AreEqual(0, query.Count);
                Assert.AreEqual(0, Ids(query).Count);
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void PoolAny_MatchesOnlyRowsWithPoolComponent()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = world.Query().With<AnyA>().Any<AnyPool>().Any<AnyTag>();
                var pooled = world.Entity(new AnyA());
                pooled.Add(new AnyPool { V = 3 });
                var tagged = world.Entity(new AnyA());
                tagged.Add<AnyTag>();
                var plain = world.Entity(new AnyA());
                world.Update();

                Assert.IsFalse(query.queryUnsafe->UseStorageIteration());
                Assert.AreEqual(2, query.Count);
                CollectionAssert.AreEquivalent(new[] { pooled.id, tagged.id }, Ids(query));
                Assert.IsFalse(Ids(query).Contains(plain.id));
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void AnyMask_SurvivesSerialization()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = world.Query().With<AnyA>().Any<AnyB>().Any<AnyC>();
                var ab = world.Entity(new AnyA(), new AnyB());
                world.Entity(new AnyA());
                world.Update();
                Assert.AreEqual(1, query.Count);

                var data = world.Serialize();
                world.Deserialize(data);

                var ac = world.Entity(new AnyA(), new AnyC());
                world.Update();
                Assert.AreEqual(2, query.Count);
                CollectionAssert.AreEquivalent(new[] { ab.id, ac.id }, Ids(query));
            }
            finally { world.Dispose(); }
        }

        [TestCase(Threads.Main)]
        [TestCase(Threads.MainRun)]
        [TestCase(Threads.Single)]
        [TestCase(Threads.Parallel)]
        public void GeneratedSystem_IteratesOnlyAnyMatches(Threads mode)
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var systems = new Systems(ref world).Add(AnyFilterSystems.IncrementAnyBC, mode);
                var info = ((ISystemCompilationInfoProvider)systems.Runners[0]).CompilationInfo;
                Assert.AreEqual(SystemCompilationKind.PointerBatch, info.Kind);
                Assert.AreEqual(BatchFallbackReason.None, info.FallbackReason);
                var dependencyInfo = ((ISystemDependencyInfoProvider)systems.Runners[0]).DependencyInfo;
                foreach (var access in dependencyInfo.Components)
                    Assert.AreEqual(ComponentType<AnyA>.Index, access.ComponentTypeIndex, "Any/None types are filters, not accesses");

                const int count = 1500;
                var entities = new Entity[count];
                var expected = new bool[count];
                for (var i = 0; i < count; i++)
                {
                    switch (i % 5)
                    {
                        case 0: entities[i] = world.Entity(new AnyA()); break;
                        case 1: entities[i] = world.Entity(new AnyA(), new AnyB()); expected[i] = true; break;
                        case 2: entities[i] = world.Entity(new AnyA(), new AnyC()); expected[i] = true; break;
                        case 3: entities[i] = Entity3(ref world, new AnyA(), new AnyB(), new AnyC()); expected[i] = true; break;
                        default: entities[i] = Entity3(ref world, new AnyA(), new AnyB(), new AnyD()); break;
                    }
                }
                world.Update();
                systems.OnUpdate(0.016f, 0.016f);
                systems.Complete();
                for (var i = 0; i < count; i++)
                    Assert.AreEqual(expected[i] ? 1 : 0, entities[i].Get<AnyA>().V, $"entity {i} kind {i % 5}");
            }
            finally { world.Dispose(); }
        }

        [TestCase(Threads.Main)]
        [TestCase(Threads.Parallel)]
        public void GeneratedSystem_TagAndPoolAny_UsesArchetypeWalk(Threads mode)
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var systems = new Systems(ref world).Add(AnyFilterSystems.IncrementAnyTagPool, mode);
                Assert.AreEqual(SystemCompilationKind.PointerBatch,
                    ((ISystemCompilationInfoProvider)systems.Runners[0]).CompilationInfo.Kind);

                const int count = 900;
                var entities = new Entity[count];
                for (var i = 0; i < count; i++)
                {
                    entities[i] = world.Entity(new AnyA());
                    if (i % 3 == 1) entities[i].Add<AnyTag>();
                    if (i % 3 == 2) entities[i].Add(new AnyPool());
                }
                world.Update();
                systems.OnUpdate(0.016f, 0.016f);
                systems.Complete();
                for (var i = 0; i < count; i++)
                    Assert.AreEqual(i % 3 == 0 ? 0 : 1, entities[i].Get<AnyA>().V, $"entity {i}");
            }
            finally { world.Dispose(); }
        }
    }
}
