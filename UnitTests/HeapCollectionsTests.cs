using NUnit.Framework;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs.Tests
{
    [TestFixture]
    public unsafe class HeapCollectionsTests
    {
        [SetUp]
        public void SetUp() => World.DisposeStatic();

        [TearDown]
        public void TearDown() => World.DisposeStatic();

        [Test]
        public void HeapList_GrowsAndSharesStateBetweenCopies()
        {
            var list = new HeapList<int>(2, AllocatorHandle.Persistent);
            var copy = list;
            for (var i = 0; i < 100; i++) list.Add(i);
            Assert.AreEqual(100, copy.Length, "copies share the header");
            Assert.GreaterOrEqual(copy.Capacity, 100);
            Assert.AreEqual(57, copy[57]);

            var extra = stackalloc int[3] { 7, 8, 9 };
            copy.AddRange(extra, 3);
            Assert.AreEqual(103, list.Length);
            Assert.AreEqual(9, list[102]);

            list.RemoveAtSwapBack(0);
            Assert.AreEqual(102, list.Length);
            Assert.AreEqual(9, list[0]);

            list.ResizeUninitialized(10);
            Assert.AreEqual(10, copy.Length);
            copy.Clear();
            Assert.IsTrue(list.IsEmpty);
            list.Dispose();
            Assert.IsFalse(list.IsCreated);
        }

        [Test]
        public void HeapHashMap_AddRemoveAndSharesStateBetweenCopies()
        {
            var map = new HeapHashMap<int, long>(4, AllocatorHandle.Persistent);
            var copy = map;
            for (var i = 0; i < 200; i++) Assert.IsTrue(map.TryAdd(i, i * 10L));
            Assert.AreEqual(200, copy.Count, "copies share the map");
            Assert.IsTrue(copy.TryGetValue(150, out var v));
            Assert.AreEqual(1500L, v);
            Assert.IsFalse(map.TryAdd(150, 0));
            copy[150] = 7;
            Assert.AreEqual(7L, map[150]);
            Assert.IsTrue(map.Remove(150));
            Assert.IsFalse(copy.ContainsKey(150));
            var sum = 0L;
            foreach (var kv in map) sum += kv.Key;
            Assert.AreEqual(199 * 200 / 2 - 150, sum);
            map.Dispose();
        }

        // The Changed<T> fetch appends first-seen entities to the old-values buffer, which can
        // reallocate it in the middle of the pass. Archetype B is iterated first and brings 300
        // new entities; A's known entities are compared afterwards and must read the new buffer.
        [Test]
        public void ChangedQuery_OldValuesBufferGrowsMidPass_DetectsExactChanges()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world).AddDefaults();
            var archB = world.GetArchetype(typeof(ReactiveHealth), typeof(ChangedHitCount), typeof(Mana));
            var archA = world.GetArchetype(typeof(ReactiveHealth), typeof(ChangedHitCount));
            var known = new Entity[10];
            for (var i = 0; i < known.Length; i++)
            {
                known[i] = archA.CreateEntity();
                known[i].Get<ReactiveHealth>().Value = i;
            }
            systems.Add(ChangedQueryTestSystems.CountChangedHealth);
            systems.OnUpdate(0.016f, 0.016f); // records the known entities

            for (var i = 0; i < 300; i++) archB.CreateEntity().Get<ReactiveHealth>().Value = 1000 + i;
            for (var i = 0; i < known.Length; i += 2) known[i].Get<ReactiveHealth>().Value += 0.5f;
            systems.OnUpdate(0.016f, 0.032f);

            for (var i = 0; i < known.Length; i++)
                Assert.AreEqual(i % 2 == 0 ? 1 : 0, known[i].Get<ChangedHitCount>().Count, $"known entity {i}");

            systems.OnUpdate(0.016f, 0.048f);
            for (var i = 0; i < known.Length; i++)
                Assert.AreEqual(i % 2 == 0 ? 1 : 0, known[i].Get<ChangedHitCount>().Count, $"known entity {i} after an idle frame");
            world.Dispose();
        }
    }
}
