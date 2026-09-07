using System;
using NUnit.Framework;
namespace Wargon.Nukecs.Tests.RuntimeDispatch
{
    [TestFixture]
    public unsafe class RuntimeQueryIter4DispatchTests
    {
        [Test]
        public void PageTableGrowth_DuringEnumeration_PreservesPointersAndCurrent()
        {
            var world=World.Create(WorldConfig.Default1024);
            try
            {
                var arch=world.GetArchetype(typeof(RuntimeQi4PoolA),typeof(RuntimeQi4PoolB),typeof(RuntimeQi4PoolC),typeof(RuntimeQi4PoolD));
                for(var i=0;i<129;i++)
                {
                    var e=arch.CreateEntity();
                    e.Get<RuntimeQi4PoolA>().Id=i;
                    e.Get<RuntimeQi4PoolB>().Value=1000+i;
                    e.Get<RuntimeQi4PoolC>().Value=10.5+i;
                    e.Get<RuntimeQi4PoolD>().Value=(byte)i;
                }
                var query=new Query<RuntimeQi4PoolA,RuntimeQi4PoolB,RuntimeQi4PoolC,RuntimeQi4PoolD>();
                Init(ref query,ref world);
                var iterator=query.GetEnumerator();
                var control=query.iter_paged_pool_runtime();
                Assert.IsTrue(iterator.MoveNext()); Assert.IsTrue(control.MoveNext());
                var saved=iterator.Current;
                var (first,_,_,_)=saved;
                var savedId=first.Read.Id;
                world.UnsafeWorld->GetUntypedPoolPtr(ComponentType<RuntimeQi4PoolA>.Index)->UnsafeBuffer->GetPtr(65536);
                world.UnsafeWorld->GetUntypedPoolPtr(ComponentType<RuntimeQi4PoolB>.Index)->UnsafeBuffer->GetPtr(65536);
                world.UnsafeWorld->GetUntypedPoolPtr(ComponentType<RuntimeQi4PoolC>.Index)->UnsafeBuffer->GetPtr(65536);
                world.UnsafeWorld->GetUntypedPoolPtr(ComponentType<RuntimeQi4PoolD>.Index)->UnsafeBuffer->GetPtr(65536);
                var count=0;
                do
                {
                    var (a,b,c,d)=iterator.Current;
                    var (x,y,z,w)=control.Current;
                    Assert.AreEqual((IntPtr)x.data,(IntPtr)a.data); Assert.AreEqual((IntPtr)y.data,(IntPtr)b.data);
                    Assert.AreEqual((IntPtr)z.data,(IntPtr)c.data); Assert.AreEqual((IntPtr)w.data,(IntPtr)d.data);
                    Assert.AreEqual(1000+a.Read.Id,b.Read.Value); Assert.AreEqual(10.5+a.Read.Id,c.Read.Value);
                    Assert.AreEqual((byte)a.Read.Id,d.Read.Value);
                    count++;
                    var next=control.MoveNext(); Assert.AreEqual(next,iterator.MoveNext());
                    if(!next) break;
                } while(true);
                Assert.AreEqual(129,count); Assert.IsFalse(iterator.MoveNext());
                var (snapshot,_,_,_)=saved; Assert.AreEqual(savedId,snapshot.Read.Id);
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void DefaultPools_LazyPages_AndPageTableGrowth_RemainSupported()
        {
            var world = World.Create(WorldConfig.Default16);
            try
            {
                var entities = world.BatchCreateEntity(257);
                var first = entities[0];
                var last = entities[256];
                first.Add<RuntimeQi4PoolA>(); first.Add<RuntimeQi4PoolB>();
                first.Add<RuntimeQi4PoolC>(); first.Add<RuntimeQi4PoolD>();
                last.Add<RuntimeQi4PoolA>(); last.Add<RuntimeQi4PoolB>();
                last.Add<RuntimeQi4PoolC>(); last.Add<RuntimeQi4PoolD>();
                world.Update();
                var query = new Query<RuntimeQi4PoolA, RuntimeQi4PoolB, RuntimeQi4PoolC, RuntimeQi4PoolD>();
                Init(ref query, ref world);
                var count = 0;
                foreach (var (a, b, c, d) in query.GetEnumerator())
                {
                    Assert.AreEqual(0, a.Read.Id); Assert.AreEqual(0, b.Read.Id);
                    Assert.AreEqual(0, c.Read.Id); Assert.AreEqual(0, d.Read.Id);
                    a.Get.Id = 7; b.Get.Id = 8; c.Get.Id = 9; d.Get.Id = 10;
                    count++;
                }
                Assert.AreEqual(2, count);
                Assert.AreEqual(7, first.Get<RuntimeQi4PoolA>().Id);
                Assert.AreEqual(10, last.Get<RuntimeQi4PoolD>().Id);
                AssertSameRows(query.GetEnumerator(), query.iter_paged_pool_runtime());
            }
            finally { world.Dispose(); }
        }

        private static void Init<TQuery>(ref TQuery query, ref World world) where TQuery : struct, ISystemParam
        {
            var pointer = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            query.Init(ref pointer);
            query.Update(ref world, IntPtr.Zero);
        }

        private static Entity[] Populate(ref World world)
        {
            var entities = new Entity[256];
            for (var i = 0; i < entities.Length; i++)
            {
                var e = world.Entity();
                e.Add(new Qi4A { Value = i }); e.Add(new Qi4B { Value = i });
                e.Add(new Qi4C { Value = i }); e.Add(new Qi4D { Value = i });
                if (i % 2 == 0) e.Add(new RuntimeQi4PoolA { Id = i });
                if (i % 3 == 0) e.Add(new RuntimeQi4PoolB { Id = i, Value = 1000 + i });
                if (i % 5 == 0) e.Add(new RuntimeQi4PoolC { Id = i, Value = 10.5 + i });
                if (i % 7 == 0) e.Add(new RuntimeQi4PoolD { Id = i, Value = (byte)i });
                if (i % 4 == 0) e.Add<Qi4Tag>();
                if (i % 11 == 0) e.Add<IsPrefab>();
                if (i % 13 == 0) e.Add<RuntimeQi4Extra>();
                entities[i] = e;
            }
            world.Update();
            // Re-add after a logical archetype change: physical rows must not be confused with entity IDs.
            entities[2].Remove<RuntimeQi4PoolA>();
            world.Update();
            entities[2].Add(new RuntimeQi4PoolA { Id = 2 });
            world.Update();
            for (var i = 0; i < entities.Length; i++)
            {
                Assert.AreEqual(i % 2 == 0, entities[i].Has<RuntimeQi4PoolA>(), "Pool membership before iteration: " + i);
                if (i % 2 == 0)
                    Assert.AreEqual(i, entities[i].Get<RuntimeQi4PoolA>().Id, "Pool value before iteration: " + i);
            }
            return entities;
        }

        private static void Check<T1, T2, T3, T4>(
            RuntimeDispatchIter<RuntimeDispatchRefs<T1, T2, T3, T4>> iterator, Entity[] entities,
            Func<Entity, bool> filter = null)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            var seen = new bool[entities.Length];
            var saved = default(RuntimeDispatchRefs<T1, T2, T3, T4>);
            var savedId = -1;
            while (iterator.MoveNext())
            {
                var (a, b, c, d) = iterator.Current;
                var id = *(int*)a.data;
                Assert.That(id, Is.InRange(0, entities.Length - 1));
                Assert.IsFalse(seen[id], "Duplicate row");
                seen[id] = true;
                var e = entities[id];
                Assert.AreEqual(e.Get<T1>(), a.Read);
                Assert.AreEqual(e.Get<T2>(), b.Read);
                Assert.AreEqual(e.Get<T3>(), c.Read);
                Assert.AreEqual(e.Get<T4>(), d.Read);
                // Verify writes reach the actual component (inline or pool), then restore the identity.
                *(int*)a.data = id + 10000;
                Assert.AreEqual(a.Read, e.Get<T1>());
                *(int*)a.data = id;
                if (savedId < 0) { saved = iterator.Current; savedId = id; }
                var (snapshot, _, _, _) = saved;
                Assert.AreEqual(savedId, *(int*)snapshot.data, "Current must remain a value snapshot.");
            }
            Assert.IsFalse(iterator.MoveNext());
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var expected = e.Has<T1>() && e.Has<T2>() && e.Has<T3>() && e.Has<T4>() &&
                               !e.Has<IsPrefab>() && (filter == null || filter(e));
                Assert.AreEqual(expected, seen[i], "Wrong membership for entity " + i);
            }
        }

        private static void CheckQuery<T1, T2, T3, T4>(ref World world, Entity[] entities)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            var query = new Query<T1, T2, T3, T4>();
            Init(ref query, ref world);
            Check(query.GetEnumerator(), entities);
            if (ComponentType<T1>.Data.category == ComponentCategory.Pool ||
                ComponentType<T2>.Data.category == ComponentCategory.Pool ||
                ComponentType<T3>.Data.category == ComponentCategory.Pool ||
                ComponentType<T4>.Data.category == ComponentCategory.Pool)
                AssertSameRows(query.GetEnumerator(), query.iter_paged_pool_runtime());
            else
                Assert.Throws<InvalidOperationException>(() => { query.iter_paged_pool_runtime(); });
        }

        private static void AssertSameRows<T1, T2, T3, T4>(
            RuntimeDispatchIter<RuntimeDispatchRefs<T1, T2, T3, T4>> expected,
            RuntimePagedPoolIter<RuntimePagedRefs<T1, T2, T3, T4>> actual)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            while (expected.MoveNext())
            {
                Assert.IsTrue(actual.MoveNext());
                var (a, b, c, d) = actual.Current;
                var (x, y, z, w) = expected.Current;
                Assert.AreEqual((IntPtr)x.data, (IntPtr)a.data);
                Assert.AreEqual((IntPtr)y.data, (IntPtr)b.data);
                Assert.AreEqual((IntPtr)z.data, (IntPtr)c.data);
                Assert.AreEqual((IntPtr)w.data, (IntPtr)d.data);
            }
            Assert.IsFalse(actual.MoveNext());
            Assert.IsFalse(actual.MoveNext());
        }

        [Test]
        public void MixedPools_AllSlots_MultiplePools_SparseRows_AndInlineFallback()
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var entities = Populate(ref world);
                CheckQuery<Qi4A, Qi4B, Qi4C, Qi4D>(ref world, entities);
                CheckQuery<RuntimeQi4PoolA, Qi4B, Qi4C, Qi4D>(ref world, entities);
                CheckQuery<Qi4A, RuntimeQi4PoolB, Qi4C, Qi4D>(ref world, entities);
                CheckQuery<Qi4A, Qi4B, RuntimeQi4PoolC, Qi4D>(ref world, entities);
                CheckQuery<Qi4A, Qi4B, Qi4C, RuntimeQi4PoolD>(ref world, entities);
                CheckQuery<RuntimeQi4PoolA, Qi4B, RuntimeQi4PoolC, Qi4D>(ref world, entities);
                CheckQuery<RuntimeQi4PoolA, RuntimeQi4PoolB, RuntimeQi4PoolC, RuntimeQi4PoolD>(ref world, entities);
                CheckQuery<RuntimeQi4PoolA, RuntimeQi4PoolB, Qi4C, Qi4D>(ref world, entities);
                CheckQuery<Qi4A, RuntimeQi4PoolB, RuntimeQi4PoolC, Qi4D>(ref world, entities);
                CheckQuery<RuntimeQi4PoolA, RuntimeQi4PoolB, RuntimeQi4PoolC, Qi4D>(ref world, entities);
                CheckQuery<RuntimeQi4PoolA, Qi4B, Qi4C, RuntimeQi4PoolD>(ref world, entities);
                CheckQuery<Qi4A, RuntimeQi4PoolB, Qi4C, RuntimeQi4PoolD>(ref world, entities);
                CheckQuery<RuntimeQi4PoolA, RuntimeQi4PoolB, Qi4C, RuntimeQi4PoolD>(ref world, entities);
                CheckQuery<Qi4A, Qi4B, RuntimeQi4PoolC, RuntimeQi4PoolD>(ref world, entities);
                CheckQuery<RuntimeQi4PoolA, Qi4B, RuntimeQi4PoolC, RuntimeQi4PoolD>(ref world, entities);
                CheckQuery<Qi4A, RuntimeQi4PoolB, RuntimeQi4PoolC, RuntimeQi4PoolD>(ref world, entities);
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void TagsOnlyFilter_NoTagPayload_InlineAndPoolQueries()
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var entities = Populate(ref world);
                var with = new Query<Qi4A, Qi4B, Qi4C, Qi4D, With<Qi4Tag>>();
                Init(ref with, ref world);
                Check(with.GetEnumerator(), entities, e => e.Has<Qi4Tag>());
                var none = new Query<RuntimeQi4PoolA, Qi4B, Qi4C, Qi4D, None<Qi4Tag>>();
                Init(ref none, ref world);
                Check(none.GetEnumerator(), entities, e => !e.Has<Qi4Tag>());
                AssertSameRows(none.GetEnumerator(), none.iter_paged_pool_runtime());
                var tag = new Query<Qi4A, RuntimeQi4PoolB, Qi4C, Qi4D, Qi4Tag>();
                Init(ref tag, ref world);
                Check(tag.GetEnumerator(), entities, e => e.Has<Qi4Tag>());
                AssertSameRows(tag.GetEnumerator(), tag.iter_paged_pool_runtime());
                var nonePool = new Query<Qi4A, Qi4B, Qi4C, Qi4D, None<RuntimeQi4PoolA>>();
                Init(ref nonePool, ref world);
                Check(nonePool.GetEnumerator(), entities, e => !e.Has<RuntimeQi4PoolA>());

                var invalidTag = new Query<Qi4A, Qi4B, Qi4C, Qi4Tag>();
                Init(ref invalidTag, ref world);
                Assert.Throws<InvalidOperationException>(() => { invalidTag.GetEnumerator(); });
                var invalidFifth = new Query<Qi4A, Qi4B, Qi4C, Qi4D, RuntimeQi4PoolA>();
                Init(ref invalidFifth, ref world);
                Assert.Throws<InvalidOperationException>(() => { invalidFifth.GetEnumerator(); });
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void AllPools_StorageWithoutInlineColumns_EmptyAndAppendSnapshot()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var query = new Query<RuntimeQi4PoolA, RuntimeQi4PoolB, RuntimeQi4PoolC, RuntimeQi4PoolD>();
                Init(ref query, ref world);
                var iterator = query.GetEnumerator();
                Assert.IsFalse(iterator.MoveNext()); Assert.IsFalse(iterator.MoveNext());
                var poolIterator = query.iter_paged_pool_runtime();
                Assert.IsFalse(poolIterator.MoveNext()); Assert.IsFalse(poolIterator.MoveNext());
                var arch = world.GetArchetype(typeof(RuntimeQi4PoolA), typeof(RuntimeQi4PoolB),
                    typeof(RuntimeQi4PoolC), typeof(RuntimeQi4PoolD));
                arch.CreateEntity().Get<RuntimeQi4PoolA>().Id = 1;
                query.Update(ref world, IntPtr.Zero);
                var count = 0;
                foreach (var (a, b, c, d) in query.GetEnumerator())
                {
                    if (++count > 1) Assert.Fail("Appended row was included in the active block.");
                    Assert.AreEqual(1, a.Read.Id);
                    arch.CreateEntity().Get<RuntimeQi4PoolA>().Id = 2;
                }
                Assert.AreEqual(1, count);
                query.Update(ref world, IntPtr.Zero);
                var sum = 0;
                foreach (var (a, _, _, _) in query.GetEnumerator()) sum += a.Read.Id;
                Assert.AreEqual(3, sum);
                poolIterator = query.iter_paged_pool_runtime();
                Assert.IsTrue(poolIterator.MoveNext());
                var snapshot = poolIterator.Current;
                arch.CreateEntity().Get<RuntimeQi4PoolA>().Id = 3;
                Assert.IsTrue(poolIterator.MoveNext());
                var (saved, _, _, _) = snapshot;
                Assert.AreEqual(1, saved.Read.Id);
                Assert.IsFalse(poolIterator.MoveNext(), "Appended row extended the pool iterator snapshot.");
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void InlineUnequalSizes_MatchesCompact_AndUsesAppendSnapshot()
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var query = new Query<RuntimeQi4Mid, RuntimeQi4Double, RuntimeQi4Small, RuntimeQi4Wide>();
                Init(ref query, ref world);
                var arch = world.GetArchetype(typeof(RuntimeQi4Mid), typeof(RuntimeQi4Double),
                    typeof(RuntimeQi4Small), typeof(RuntimeQi4Wide));
                for (var i = 0; i < 65; i++)
                {
                    var e = arch.CreateEntity();
                    e.Get<RuntimeQi4Mid>().Value = i;
                    e.Get<RuntimeQi4Double>().Value = i + 0.5;
                    e.Get<RuntimeQi4Small>().Value = (byte)i;
                    e.Get<RuntimeQi4Wide>().Right = 1000 + i;
                }
                query.Update(ref world, IntPtr.Zero);
                var compact = query.iter_compact_runtime();
                var count = 0;
                foreach (var (a, b, c, d) in query.GetEnumerator())
                {
                    Assert.IsTrue(compact.MoveNext());
                    var (x, y, z, w) = compact.Current;
                    Assert.AreEqual(x.Read, a.Read); Assert.AreEqual(y.Read, b.Read);
                    Assert.AreEqual(z.Read, c.Read); Assert.AreEqual(w.Read, d.Read);
                    if (++count == 1) arch.CreateEntity();
                }
                Assert.AreEqual(65, count);
                Assert.IsFalse(compact.MoveNext());
            }
            finally { world.Dispose(); }
        }
    }
}
