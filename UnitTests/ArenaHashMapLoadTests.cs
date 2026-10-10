using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct ArenaMapC0 : IComponent { public int V; }
    public struct ArenaMapC1 : IComponent { public int V; }
    public struct ArenaMapC2 : IComponent { public int V; }
    public struct ArenaMapC3 : IComponent { public int V; }
    public struct ArenaMapC4 : IComponent { public int V; }
    public struct ArenaMapC5 : IComponent { public int V; }
    public struct ArenaMapC6 : IComponent { public int V; }

    // Arena hash maps (archetypesMap & co.) that grew before a save used to keep the offsets of
    // their first block: after a load they resolved into freed memory.
    [TestFixture]
    public unsafe class ArenaHashMapLoadTests
    {
        [SetUp]
        public void SetUp() => World.DisposeStatic();

        [TearDown]
        public void TearDown() => World.DisposeStatic();

        private static Entity Spawn(ref World world, int mask, int value)
        {
            var e = world.Entity();
            if ((mask & 1) != 0) e.Add(new ArenaMapC0 { V = value });
            if ((mask & 2) != 0) e.Add(new ArenaMapC1 { V = value });
            if ((mask & 4) != 0) e.Add(new ArenaMapC2 { V = value });
            if ((mask & 8) != 0) e.Add(new ArenaMapC3 { V = value });
            if ((mask & 16) != 0) e.Add(new ArenaMapC4 { V = value });
            if ((mask & 32) != 0) e.Add(new ArenaMapC5 { V = value });
            if ((mask & 64) != 0) e.Add(new ArenaMapC6 { V = value });
            return e;
        }

        private static void AssertMapsResolve(ref World world)
        {
            var w = world.UnsafeWorld;
            ref var allocator = ref w->AllocatorRef;
            var count = 0;
            foreach (var entry in w->archetypesMap)
            {
                Assert.AreEqual((System.IntPtr)entry.Value.ptr.offset.AsPtr<ArchetypeUnsafe>(ref allocator),
                    (System.IntPtr)entry.Value.ptr.Ptr, "Archetype lookup points outside the loaded arena.");
                Assert.AreEqual(entry.Key, entry.Value.ptr.Ref.hashId, "Archetype lookup returns another archetype.");
                Assert.IsTrue(w->archetypesMap.TryGetValue(entry.Key, out _), "Lookup by key fails after load.");
                count++;
            }
            Assert.AreEqual(w->archetypesMap.Count, count);
        }

        private static void AssertValues(Entity e, int mask, int value)
        {
            if ((mask & 1) != 0) Assert.AreEqual(value, e.Get<ArenaMapC0>().V);
            if ((mask & 32) != 0) Assert.AreEqual(value, e.Get<ArenaMapC5>().V);
        }

        private static void AssertArenaIntact(ref World world, string context)
        {
            Assert.IsTrue(world.AllocatorRef.Validate(out var v),
                $"Arena corrupted {context}: {v.Kind} at region {v.Region}, block offset {v.BlockOffset}, " +
                $"data size {v.DataSize}, tag {AllocatorTags.NameOf(v.Tag)}.");
        }

        // Same as below with canaries and freed-block poisoning: reports the overflowing
        // allocation itself (CanaryBroken) or a write into freed memory (FreedBlockWritten).
        [Test]
        public void CreatingManyArchetypes_KeepsArenaIntact_WithArenaGuard()
        {
            var previous = AllocatorDebugState.Mode;
            AllocatorDebugState.Mode = AllocatorDebugMode.All;
            try { CreatingManyArchetypes_KeepsArenaIntact(); }
            finally { AllocatorDebugState.Mode = previous; }
        }

        // Locates the first archetype whose creation damages the arena (no save involved).
        [Test]
        public void CreatingManyArchetypes_KeepsArenaIntact()
        {
            var world = World.Create(WorldConfig.Default1024);
            for (var mask = 1; mask < 128; mask++)
            {
                Spawn(ref world, mask, mask);
                world.Update();
                AssertArenaIntact(ref world, $"after mask {mask} ({world.UnsafeWorld->archetypesMap.Count} archetypes)");
            }
            world.Dispose();
        }

        [Test]
        public void GrownArenaMaps_SurviveSaveLoad_AndGrowAgain()
        {
            var world = World.Create(WorldConfig.Default1024);
            var first = new Entity[64];
            for (var mask = 1; mask < 64; mask++) first[mask] = Spawn(ref world, mask, mask);
            world.Update();
            var archetypesBefore = world.UnsafeWorld->archetypesMap.Count;
            Assert.Greater(archetypesBefore, 32, "The test needs archetypesMap to resize before saving.");
            AssertArenaIntact(ref world, "before save");

            var data = world.Serialize();
            world.Deserialize(data);
            AssertMapsResolve(ref world);
            Assert.AreEqual(archetypesBefore, world.UnsafeWorld->archetypesMap.Count);
            for (var mask = 1; mask < 64; mask++) AssertValues(first[mask], mask, mask);

            // Existing combinations are found, not recreated.
            Spawn(ref world, 63, 1000);
            world.Update();
            Assert.AreEqual(archetypesBefore, world.UnsafeWorld->archetypesMap.Count);

            // New combinations make the loaded maps resize through the restored arena pointer.
            for (var mask = 64; mask < 128; mask++) Spawn(ref world, mask, mask);
            world.Update();
            Assert.Greater(world.UnsafeWorld->archetypesMap.Count, 64);
            AssertMapsResolve(ref world);

            var archetypesAfter = world.UnsafeWorld->archetypesMap.Count;
            // World.Load restores the saved slot, so the source world must be gone first.
            var saved = world.Serialize();
            world.Dispose();
            var reloaded = World.Load(WorldConfig.Default1024, saved);
            AssertMapsResolve(ref reloaded);
            Assert.AreEqual(archetypesAfter, reloaded.UnsafeWorld->archetypesMap.Count);
            reloaded.Dispose();
        }
    }
}
