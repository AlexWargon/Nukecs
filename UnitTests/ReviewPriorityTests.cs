using NUnit.Framework;
using Unity.Burst;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    public struct ReviewPoolValue : IComponent, IPoolComponent { public int Visits; }
    public struct ReviewInlineValue : IComponent { public int Visits; }
    public struct ReviewSparseTag : IComponent { }
    public static class ReviewPoolSystems
    {
        [System, BurstCompile] public static void Boundary(ref State state) { }
        [System, BurstCompile]
        public static void WithoutEntity(ref Query<ReviewInlineValue, ReviewPoolValue> query)
        {
            foreach (var (inline, pool) in query) { inline.Get.Visits++; pool.Get.Visits++; }
        }
        [System, BurstCompile]
        public static void WithEntity(ref Query<Entity, ReviewInlineValue, ReviewPoolValue> query)
        {
            foreach (var (entity, inline, pool) in query) { inline.Get.Visits++; pool.Get.Visits++; }
        }
    }
    [TestFixture]
    public class ReviewPriorityTests
    {
        [SetUp] public void SetUp() => World.DisposeStatic();
        [TearDown] public void TearDown() => World.DisposeStatic();
        [TestCase(false, -1)] [TestCase(true, -1)]
        [TestCase(false, 0)] [TestCase(true, 0)]
        [TestCase(false, 1)] [TestCase(true, 1)]
        [TestCase(false, 2)] [TestCase(true, 2)]
        [TestCase(false, 3)] [TestCase(true, 3)]
        public void ParallelPlainForeach_WithPool_VisitsEveryEntityOnce(bool withEntity, int graph)
        {
            var world = World.Create(WorldConfig.Default6144);
            var entities = new Entity[2049];
            for (var i = 0; i < entities.Length; i++) {
                entities[i] = world.Entity(new ReviewInlineValue(), new ReviewPoolValue());
                if (i % 3 == 0) entities[i].Add<ReviewSparseTag>();
            }
            world.Update();
            for (var i = 0; i < entities.Length; i += 7) entities[i].Destroy();
            world.Update();
            var systems = new Systems(ref world);
            if (withEntity) systems.Add(ReviewPoolSystems.WithEntity, Threads.Parallel);
            else systems.Add(ReviewPoolSystems.WithoutEntity, Threads.Parallel);
            systems.Add(ReviewPoolSystems.Boundary, Threads.MainRun);
            if (graph >= 0) systems.UseDependencyGraph(mode: (GroupScheduleMode)graph);
            systems.OnUpdate(0.016f, 0);
            foreach (var entity in entities) {
                if (!entity.IsValid()) continue;
                Assert.AreEqual(1, entity.Get<ReviewInlineValue>().Visits);
                Assert.AreEqual(1, entity.Get<ReviewPoolValue>().Visits);
            }
        }
        [Test] public void TwoInstallers_OwnIndependentWorlds()
        {
            var first = new GameObject("First installer");
            var second = new GameObject("Second installer");
            try {
                var a = first.AddComponent<ReviewInstaller>(); a.Initialize();
                var b = second.AddComponent<ReviewInstaller>(); b.Initialize();
                Assert.AreNotEqual(a.WorldId, b.WorldId);
                Assert.IsTrue(a.Created.IsValid()); Assert.IsTrue(b.Created.IsValid());
                Object.DestroyImmediate(second); second = null;
                Assert.IsTrue(a.Created.IsValid());
            }
            finally { if (second != null) Object.DestroyImmediate(second); Object.DestroyImmediate(first); }
        }
        [Test] public unsafe void Load_PreservesOwnedDomainWrapperInsteadOfSavedForeignWrapper()
        {
            var world = World.Create(WorldConfig.Default256);
            var other = World.Create(WorldConfig.Default256);
            var owned = world.UnsafeWorld->ManagedWorld;
            byte[] snapshot;
            try {
                world.UnsafeWorld->ManagedWorld = other.UnsafeWorld->ManagedWorld;
                snapshot = world.Serialize();
            }
            finally { world.UnsafeWorld->ManagedWorld = owned; }
            world.Deserialize(snapshot);
            Assert.AreEqual((System.IntPtr)owned.Ptr, (System.IntPtr)world.UnsafeWorld->ManagedWorld.Ptr);
            Assert.AreEqual(other.Id, other.UnsafeWorld->ManagedWorld.Ref.Id,
                "Restoring a saved domain pointer must not overwrite another world's wrapper.");
        }
        [Test] public void OldInstaller_DoesNotDisposeReusedWorldSlot()
        {
            var go = new GameObject("Old installer");
            try {
                var installer = go.AddComponent<ReviewInstaller>(); installer.Initialize();
                World.Get(installer.WorldId).Dispose();
                var replacement = World.Create(WorldConfig.Default256);
                Object.DestroyImmediate(go); go = null;
                Assert.IsTrue(replacement.IsAlive);
            }
            finally { if (go != null) Object.DestroyImmediate(go); }
        }
    }
}
