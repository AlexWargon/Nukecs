using NUnit.Framework;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests
{
    // Storage-mode query tests: inline-only with-filters iterate whole storages densely.
    public struct SmData : IComponent
    {
        public int Value;
    }

    public struct SmVel : IComponent
    {
        public float Speed;
    }

    public struct SmTagA : IComponent { }

    public struct SmTagB : IComponent { }

    public struct SmExtra : IComponent
    {
        public int E;
    }

    public struct SmCounter : IRes
    {
        public int Sum;
        public int Count;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }

    public static class SmStorageModeSystems
    {
        // inline-only query → generated batch path takes the storage loop
        [System]
        public static void SumDataSystem(
            ref Query<SmData, SmVel> query,
            ref Res<SmCounter> counter)
        {
            
            foreach (var (d, v) in query)
            {
                counter.Ref.Sum += d.Read.Value;
                counter.Ref.Count++;
            }
        }

        // inline-only query with Entity
        [System]
        public static void SumDataEntitySystem(
            ref Query<Entity, SmData> query,
            ref Res<SmCounter> counter)
        {
            
            foreach (var (e, d) in query)
            {
                counter.Ref.Sum += e.id + d.Read.Value;
            }
        }

        // parallel iterator over a shared storage must cover all rows exactly once
        [System]
        public static void ParSumSystem(
            ref Query<SmData, SmVel> query,
            ref Res<SmCounter> counter)
        {
            foreach (var (d, v) in query.par_iter())
            {
                counter.Ref.Sum += d.Read.Value;
                counter.Ref.Count++;
            }
        }

        // REGRESSION (weapon not following parent): With<TagEvent> in TOption puts a TAG
        // into with-bits — the query is NOT storage-mode. The generated batch dispatcher
        // must fall back to the archetype path instead of silently walking an empty
        // storage list (dead system: the tag was never removed).
        [System]
        public static void RemoveTagSystem(
            ref Query<Entity, SmData, With<SmTagA>> query,
            ref Res<SmCounter> counter)
        {
            foreach (var (e, d) in query)
            {
                counter.Ref.Count++;
                e.Remove<SmTagA>();
            }
        }

        // REGRESSION: paired None<TagEvent> consumer — same query shape as
        // TransformsGroup.TransformChildSystem (3 inline components + None<tag>)
        [System]
        public static void SumUntaggedSystem(
            ref Query<SmData, SmVel, SmExtra, None<SmTagA>> query,
            ref Res<SmCounter> counter)
        {
            foreach (var (d, v, x) in query)
            {
                counter.Ref.Sum += d.Read.Value;
            }
        }
    }

    [TestFixture]
    public class StorageModeQueryTests
    {
        [Test]
        public void InlineOnlyQuery_IteratesSharedStorage_FullyAndOnce()
        {
            var world = World.Create(WorldConfig.Default256);
            // fluent query created BEFORE entities (tag variants will share the storage)
            var q = world.Query().With<SmData>();

            var e1 = world.Entity(new SmData { Value = 1 });
            e1.Add<SmTagA>();
            var e2 = world.Entity(new SmData { Value = 2 });
            e2.Add<SmTagA>();
            var e3 = world.Entity(new SmData { Value = 3 });
            e3.Add<SmTagB>();
            world.Update();

            // three tag-variant logical archetypes share one storage → refCount > 1
            Assert.AreEqual(3, q.Count, "Storage-mode Count must sum all rows of the shared storage");

            var sum = 0;
            var seen = 0;
            foreach (ref var e in q)
            {
                sum += e.Get<SmData>().Value;
                seen++;
            }
            Assert.AreEqual(3, seen, "Storage-mode iteration must yield every entity exactly once");
            Assert.AreEqual(6, sum);
            world.Dispose();
        }

        [Test]
        public void InlineOnlyQuery_CreatedAfterEntities_Matches()
        {
            // storage-mode fixes lazy matching for inline-only queries: they see pre-existing storages
            var world = World.Create(WorldConfig.Default256);
            var e = world.Entity(new SmData { Value = 5 });
            world.Update();

            var q = world.Query().With<SmData>();
            Assert.AreEqual(1, q.Count, "Inline-only query created after entities must match existing storages");
            world.Dispose();
        }

        [Test]
        public void TagFilteredQuery_StillUsesArchetypePath()
        {
            var world = World.Create(WorldConfig.Default256);
            var qa = world.Query().With<SmData>().With<SmTagA>();
            var qb = world.Query().With<SmData>().None<SmTagA>();

            world.Entity(new SmData { Value = 1 }).Add<SmTagA>();
            world.Entity(new SmData { Value = 2 }).Add<SmTagA>();
            world.Entity(new SmData { Value = 3 });
            world.Update();

            Assert.AreEqual(2, qa.Count, "With-tag query count (archetype path)");
            Assert.AreEqual(1, qb.Count, "None-tag query count (archetype path)");

            var sum = 0;
            foreach (ref var e in qa)
                sum += e.Get<SmData>().Value;
            Assert.AreEqual(3, sum, "With-tag iteration values");
            world.Dispose();
        }

        [Test]
        public void NoneInlineFilter_ExcludesWholeStorage()
        {
            var world = World.Create(WorldConfig.Default256);
            var q = world.Query().With<SmVel>().None<SmData>();

            world.Entity(new SmVel { Speed = 1f });
            world.Entity(new SmData { Value = 1 });
            world.Update();

            Assert.AreEqual(1, q.Count, "None<inline> must exclude the storage containing SmData");
            world.Dispose();
        }

        [Test]
        public void SourceGenBatchSystem_StorageLoop_SumsAllRows()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new SmCounter());
            var systems = new Systems(ref world);
            systems.Add(SmStorageModeSystems.SumDataSystem, Threads.Main);

            // tag variants to force storage sharing (refCount > 1)
            var e1 = world.Entity(new SmData { Value = 10 }, new SmVel { Speed = 1 });
            e1.Add<SmTagA>();
            var e2 = world.Entity(new SmData { Value = 20 }, new SmVel { Speed = 2 });
            e2.Add<SmTagB>();
            var e3 = world.Entity(new SmData { Value = 30 }, new SmVel { Speed = 3 });
            world.Update();

            systems.OnUpdate(0.016f, 0f);
            Assert.AreEqual(60, new Res<SmCounter>().Ref.Sum, "Generated storage loop must sum every row");
            Assert.AreEqual(3, new Res<SmCounter>().Ref.Count);
            world.Dispose();
        }

        [Test]
        public void SourceGenBatchSystem_EntityParam_StorageLoop()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new SmCounter());
            var systems = new Systems(ref world);
            systems.Add(SmStorageModeSystems.SumDataEntitySystem, Threads.Main);

            var e1 = world.Entity(new SmData { Value = 10 });
            var e2 = world.Entity(new SmData { Value = 20 });
            e2.Add<SmTagA>();
            world.Update();

            systems.OnUpdate(0.016f, 0f);
            Assert.AreEqual(e1.id + 10 + e2.id + 20, new Res<SmCounter>().Ref.Sum,
                "Storage loop with Entity param must resolve correct entities");
            world.Dispose();
        }

        [Test]
        public void ParIter_CoversAllRows_ExactlyOnce()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new SmCounter());
            var systems = new Systems(ref world);
            systems.Add(SmStorageModeSystems.ParSumSystem, Threads.Single);

            const int total = 50;
            for (var i = 0; i < total; i++)
            {
                var e = world.Entity(new SmData { Value = 1 }, new SmVel { Speed = 1 });
                if (i % 2 == 0) e.Add<SmTagA>();
                if (i % 3 == 0) e.Add<SmTagB>();
            }
            world.Update();

            systems.OnUpdate(0.016f, 0f);
            Assert.AreEqual(total, new Res<SmCounter>().Ref.Count,
                "par_iter over shared storage must cover all rows exactly once");
            Assert.AreEqual(total, new Res<SmCounter>().Ref.Sum);
            world.Dispose();
        }

        [Test]
        public void First_And_Indexer_StorageMode()
        {
            var world = World.Create(WorldConfig.Default256);
            var q = world.Query().With<SmData>();

            var e1 = world.Entity(new SmData { Value = 100 });
            var e2 = world.Entity(new SmData { Value = 200 });
            e2.Add<SmTagA>();
            world.Update();

            Assert.AreEqual(e1.id, q.First().id, "First must return the first storage row entity");
            Assert.AreEqual(e2.id, q.GetEntity(1).id, "GetEntity(1) must index across storages");
            world.Dispose();
        }

        [Test]
        public void TagChurn_BetweenFrames_StaysConsistent()
        {
            var world = World.Create(WorldConfig.Default256);
            var q = world.Query().With<SmData>();

            var e = world.Entity(new SmData { Value = 7 });
            world.Update();
            Assert.AreEqual(1, q.Count);

            for (var i = 0; i < 5; i++)
            {
                e.Add<SmTagA>();
                world.Update();
                Assert.AreEqual(1, q.Count, $"iteration {i}: add tag must not change storage-mode count");
                Assert.AreEqual(7, e.Get<SmData>().Value);

                e.Remove<SmTagA>();
                world.Update();
                Assert.AreEqual(1, q.Count, $"iteration {i}: remove tag must not change storage-mode count");
                Assert.AreEqual(7, e.Get<SmData>().Value);
            }
            world.Dispose();
        }

        [Test]
        public void Destroy_ReducesCount_AndIterationStaysCorrect()
        {
            var world = World.Create(WorldConfig.Default256);
            var q = world.Query().With<SmData>();

            var e1 = world.Entity(new SmData { Value = 1 });
            var e2 = world.Entity(new SmData { Value = 2 });
            var e3 = world.Entity(new SmData { Value = 3 });
            e1.Add<SmTagA>();
            e3.Add<SmTagB>();
            world.Update();
            Assert.AreEqual(3, q.Count);

            e2.Destroy();
            world.Update();
            Assert.AreEqual(2, q.Count, "Destroy must reduce storage-mode count");

            var sum = 0;
            foreach (ref var e in q)
                sum += e.Get<SmData>().Value;
            Assert.AreEqual(4, sum, "Remaining rows must iterate with correct data after swap-remove");
            world.Dispose();
        }

        [Test]
        public void WithTagOption_BatchSystem_FallsBackToArchetypePath()
        {
            // regression (Storage-Rework): a [System] whose query carries a TAG in
            // With<> TOption is not storage-mode — with-bits can never be contained in a
            // storage inlineMask. RebuildMatchingStorages must mark it degraded so the
            // generated batch dispatcher takes the archetype path; before the fix it
            // walked an EMPTY storage list and the system silently did nothing.
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new SmCounter());
            var systems = new Systems(ref world);
            systems.Add(SmStorageModeSystems.RemoveTagSystem, Threads.Main);
            systems.Add(SmStorageModeSystems.SumUntaggedSystem, Threads.Main);

            var e1 = world.Entity(new SmData { Value = 10 }, new SmVel { Speed = 1 });
            var e2 = world.Entity(new SmData { Value = 20 }, new SmVel { Speed = 2 });
            e1.Add(new SmExtra { E = 1 });
            e2.Add(new SmExtra { E = 2 });
            e1.Add<SmTagA>();
            e2.Add<SmTagA>();
            world.Update();

            systems.OnUpdate(0.016f, 0f);
            Assert.AreEqual(2, new Res<SmCounter>().Ref.Count,
                "With<Tag> option must iterate via the archetype path (dead storage-walk bug)");

            // Threads.Main runners flush the ECB right after each system: the tags are
            // already removed, and the None<> consumer sees the entities in the SAME frame
            // (query recovers from degradation once the none-tag LA is empty).
            Assert.IsFalse(e1.Has<SmTagA>(), "Tag must be removed after the Main-runner ECB flush");
            Assert.IsFalse(e2.Has<SmTagA>(), "Tag must be removed after the Main-runner ECB flush");
            Assert.AreEqual(30, new Res<SmCounter>().Ref.Sum,
                "None<Tag> query must see entities once the tag is removed (TransformChildSystem shape)");

            world.Update(); // no-op for ECB here (already flushed by the runner), keeps frame pacing realistic
            systems.OnUpdate(0.016f, 0f);
            Assert.AreEqual(2, new Res<SmCounter>().Ref.Count, "Second frame: no tagged entities must match");
            Assert.AreEqual(60, new Res<SmCounter>().Ref.Sum, "Second frame accumulates once per entity");
            world.Dispose();
        }

        [Test]
        public void PrefabEntity_DegradesToArchetypePath_AndExcludesPrefab()
        {
            var world = World.Create(WorldConfig.Default256);
            var q = world.Query().With<SmData>();

            var normal = world.Entity(new SmData { Value = 1 });
            world.Update();
            Assert.AreEqual(1, q.Count);

            // prefab entity with the same inline set — none-tag must exclude it;
            // the query degrades to the archetype path for correctness
            var prefab = world.Entity(new SmData { Value = 999 });
            prefab.Add<IsPrefab>();
            world.Update();

            Assert.AreEqual(1, q.Count, "Prefab entity must be excluded after degradation");
            var sum = 0;
            foreach (ref var e in q)
                sum += e.Get<SmData>().Value;
            Assert.AreEqual(1, sum, "Only the non-prefab entity must be iterated");

            // once the prefab is gone the storage becomes eligible again
            prefab.Destroy();
            world.Update();
            Assert.AreEqual(1, q.Count);
            sum = 0;
            foreach (ref var e in q)
                sum += e.Get<SmData>().Value;
            Assert.AreEqual(1, sum, "Storage-mode must resume after prefab removal");
            world.Dispose();
        }
    }
}
