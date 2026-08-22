using NUnit.Framework;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests
{
    // Inline data components
    public struct TmData : IComponent
    {
        public int Value;
    }

    public struct TmDataB : IComponent
    {
        public float Speed;
    }

    // Tag components (zero data, filter-only)
    public struct TmTagA : IComponent { }

    public struct TmTagB : IComponent { }

    // Pool-stored component
    public struct TmPool : IPoolComponent
    {
        public int Value;
    }

    public struct TmCounter : IRes
    {
        public int Sum;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }

    public static class TmPoolSystems
    {
        [System]
        public static unsafe void SumPoolSystem(
            ref Query<TmPool, None<TmTagB>> query,
            ref Res<TmCounter> counter)
        {
            foreach (var (pc, _) in query.iter())
            {
                counter.Ref.Sum += pc.Read.Value;
            }
        }

        [System]
        public static void SumPoolDirectSystem(
            ref Query<TmPool, None<TmTagB>> query,
            ref Res<TmCounter> counter)
        {
            foreach (ref var pc in query)
            {
                counter.Ref.Sum += pc.Value;
            }
        }
    }

    [TestFixture]
    public class TagPoolMaskTests
    {
        [Test]
        public void TagAddRemove_UpdatesQueryAfterPlayback()
        {
            var world = World.Create(WorldConfig.Default256);
            var e = world.Entity();
            e.Add(new TmData { Value = 42 });
            world.Update();

            var withTag = world.Query().With<TmTagA>();
            Assert.AreEqual(0, withTag.Count, "No entity should match tag before it is added");

            e.Add<TmTagA>();
            world.Update();
            Assert.AreEqual(1, withTag.Count, "Tag must be visible after ECB playback");
            Assert.IsTrue(e.Has<TmTagA>(), "Entity.Has must report the tag after playback");

            e.Remove<TmTagA>();
            world.Update();
            Assert.AreEqual(0, withTag.Count, "Tag removal must be visible after playback");
            Assert.IsFalse(e.Has<TmTagA>());

            world.Dispose();
        }

        [Test]
        public void TagAddRemove_PreservesInlineData()
        {
            var world = World.Create(WorldConfig.Default256);
            var e = world.Entity();
            e.Add(new TmData { Value = 7 });
            e.Add(new TmDataB { Speed = 3.5f });
            world.Update();

            e.Add<TmTagA>();
            world.Update();
            Assert.AreEqual(7, e.Get<TmData>().Value, "Inline data must survive tag-only migration");
            Assert.AreEqual(3.5f, e.Get<TmDataB>().Speed);

            e.Remove<TmTagA>();
            world.Update();
            Assert.AreEqual(7, e.Get<TmData>().Value, "Inline data must survive tag removal");
            Assert.AreEqual(3.5f, e.Get<TmDataB>().Speed);

            world.Dispose();
        }

        [Test]
        public void MixedTags_SameInlineData_IteratesCorrectly()
        {
            var world = World.Create(WorldConfig.Default256);

            var qa = world.Query().With<TmTagA>();
            var qb = world.Query().With<TmTagB>();
            var all = world.Query().With<TmData>();

            var e1 = world.Entity();
            e1.Add(new TmData { Value = 1 });
            e1.Add<TmTagA>();
            var e2 = world.Entity();
            e2.Add(new TmData { Value = 2 });
            e2.Add<TmTagB>();
            world.Update();

            Assert.AreEqual(1, qa.Count);
            foreach (ref var e in qa)
                Assert.AreEqual(1, e.Get<TmData>().Value, "Gather iteration must read the right row for TmTagA entity");

            Assert.AreEqual(1, qb.Count);
            foreach (ref var e in qb)
                Assert.AreEqual(2, e.Get<TmData>().Value, "Gather iteration must read the right row for TmTagB entity");

            // both share the same storage (same inline mask) — tags must not duplicate or lose entities
            Assert.AreEqual(2, all.Count);

            world.Dispose();
        }

        [Test]
        public void TagChurn_RepeatedAddRemove_StaysConsistent()
        {
            var world = World.Create(WorldConfig.Default256);
            var e = world.Entity();
            e.Add(new TmData { Value = 5 });
            world.Update();

            var withTag = world.Query().With<TmTagA>();
            for (var i = 0; i < 10; i++)
            {
                e.Add<TmTagA>();
                world.Update();
                Assert.AreEqual(1, withTag.Count, $"iteration {i}: tag add must be visible");
                Assert.AreEqual(5, e.Get<TmData>().Value);

                e.Remove<TmTagA>();
                world.Update();
                Assert.AreEqual(0, withTag.Count, $"iteration {i}: tag remove must be visible");
            }

            world.Dispose();
        }

        [Test]
        public void NoneFilter_WithTags_ExcludesTaggedEntities()
        {
            var world = World.Create(WorldConfig.Default256);
            var noneTag = world.Query().With<TmData>().None<TmTagA>();

            var tagged = world.Entity();
            tagged.Add(new TmData { Value = 1 });
            tagged.Add<TmTagA>();
            var untagged = world.Entity();
            untagged.Add(new TmData { Value = 2 });
            world.Update();

            Assert.AreEqual(1, noneTag.Count, "None<TmTagA> must exclude the tagged entity");
            foreach (ref var e in noneTag)
                Assert.AreEqual(2, e.Get<TmData>().Value);

            world.Dispose();
        }

        [Test]
        public void Destroy_WithSwapRemove_KeepsTaggedRowsConsistent()
        {
            var world = World.Create(WorldConfig.Default256);
            var qa = world.Query().With<TmTagA>();
            var qb = world.Query().With<TmTagB>();

            var e1 = world.Entity();
            e1.Add(new TmData { Value = 1 });
            e1.Add<TmTagA>();
            var e2 = world.Entity();
            e2.Add(new TmData { Value = 2 });
            e2.Add<TmTagB>();
            var e3 = world.Entity();
            e3.Add(new TmData { Value = 3 });
            e3.Add<TmTagA>();
            world.Update();

            // Destroy the middle entity — swap-remove must fix rows/listPos of the moved entity
            e2.Destroy();
            world.Update();

            Assert.AreEqual(2, qa.Count);
            var sum = 0;
            foreach (ref var e in qa)
                sum += e.Get<TmData>().Value;
            Assert.AreEqual(4, sum, "Both TmTagA entities (values 1 and 3) must survive the swap");

            Assert.AreEqual(0, qb.Count);
            Assert.IsTrue(e1.IsValid());
            Assert.AreEqual(1, e1.Get<TmData>().Value);
            Assert.AreEqual(3, e3.Get<TmData>().Value);

            world.Dispose();
        }

        [Test]
        public void PoolComponent_DataSurvivesTagMigration()
        {
            var world = World.Create(WorldConfig.Default256);
            var withTag = world.Query().With<TmTagA>();
            var e = world.Entity();
            e.Add(new TmData { Value = 1 });
            world.Update();

            e.Add(new TmPool { Value = 9 });
            world.Update();
            Assert.AreEqual(9, e.Get<TmPool>().Value);

            // tag-only migration must not lose pool data
            e.Add<TmTagA>();
            world.Update();
            Assert.AreEqual(9, e.Get<TmPool>().Value, "Pool data must survive tag-only migration");
            Assert.AreEqual(1, withTag.Count);

            e.Remove<TmTagA>();
            world.Update();
            Assert.AreEqual(9, e.Get<TmPool>().Value, "Pool data must survive tag removal");

            // pool removal
            e.Remove<TmPool>();
            world.Update();
            Assert.IsFalse(e.Has<TmPool>());

            world.Dispose();
        }

        [Test]
        public void PoolComponent_QueryCount_And_TupleDirectAccess()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new TmCounter { Sum = 0 });
            var poolQuery = world.Query().With<TmPool>();

            var e1 = world.Entity();
            e1.Add(new TmPool { Value = 5 });
            var e2 = world.Entity();
            e2.Add(new TmPool { Value = 7 });
            world.Update();

            if (poolQuery.Count != 2)
            {
                Assert.Fail($"poolQuery.Count={poolQuery.Count}\n{world.DumpArchetypes()}" +
                            $"\ne1.Has<TmPool>={e1.Has<TmPool>()}, e1.Get={e1.Get<TmPool>().Value}");
            }

            var systems = new Systems(ref world);
            systems.Add(TmPoolSystems.SumPoolSystem, Threads.Main);
            systems.OnUpdate(0.016f, 0f);

            Assert.AreEqual(12, new Res<TmCounter>().Ref.Sum,
                "Pool component must be readable as a direct tuple field (gather from GenericPool)");

            world.Dispose();
        }

        [Test]
        public void PoolComponent_DirectForEach_SystemPath()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new TmCounter { Sum = 0 });
            var poolQuery = world.Query().With<TmPool>();

            var e1 = world.Entity();
            e1.Add(new TmPool { Value = 5 });
            var e2 = world.Entity();
            e2.Add(new TmPool { Value = 7 });
            world.Update();

            Assert.AreEqual(2, poolQuery.Count, "pool query count");

            var systems = new Systems(ref world);
            systems.Add(TmPoolSystems.SumPoolDirectSystem, Threads.Main);
            systems.OnUpdate(0.016f, 0f);

            Assert.AreEqual(12, new Res<TmCounter>().Ref.Sum,
                "Direct foreach (source-gen path) must read pool component fields");

            world.Dispose();
        }

        [Test]
        public void BatchCreate_ThenTagSubsets_IteratesCorrectly()
        {
            var world = World.Create(WorldConfig.Default256);
            var qa = world.Query().With<TmTagA>();
            var all = world.Query().With<TmData>();
            var entities = world.BatchCreateEntity(4);
            for (var i = 0; i < entities.Length; i++)
            {
                ref var e = ref entities[i];
                e.Add(new TmData { Value = i });
                if (i % 2 == 0) e.Add<TmTagA>();
            }
            world.Update();

            Assert.AreEqual(2, qa.Count);
            var sum = 0;
            foreach (ref var e in qa)
                sum += e.Get<TmData>().Value;
            Assert.AreEqual(0 + 2, sum, "Tagged subset (indices 0 and 2) must iterate with correct data");
            Assert.AreEqual(4, all.Count);

            world.Dispose();
        }

        [Test]
        public void TagAdd_OnArchetypeWithPoolAndInline_ComposesCorrectly()
        {
            var world = World.Create(WorldConfig.Default256);
            var both = world.Query().With<TmTagA>().With<TmTagB>();
            var onlyB = world.Query().With<TmTagB>().None<TmTagA>();
            var e = world.Entity();
            e.Add(new TmData { Value = 3 });
            e.Add(new TmPool { Value = 4 });
            world.Update();

            e.Add<TmTagA>();
            e.Add<TmTagB>();
            world.Update();

            Assert.IsTrue(e.Has<TmTagA>() && e.Has<TmTagB>());
            Assert.AreEqual(3, e.Get<TmData>().Value);
            Assert.AreEqual(4, e.Get<TmPool>().Value);

            Assert.AreEqual(1, both.Count, "Both tags must compose in query");

            e.Remove<TmTagA>();
            world.Update();
            Assert.AreEqual(0, both.Count);
            Assert.AreEqual(1, onlyB.Count);

            world.Dispose();
        }
    }
}
