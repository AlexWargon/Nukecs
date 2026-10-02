using System;
using NUnit.Framework;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    public struct ReviewStartValue : IComponent { public int Value; }
    public struct ReviewStarted : IComponent { }
    public static class ReviewStartupSystems
    {
        [System]
        public static void Start(ref Query<Entity, ReviewStartValue> query)
        {
            foreach (var (entity, value) in query) {
                value.Get.Value++;
                entity.Add<ReviewStarted>();
            }
        }
    }
    public class ReviewInstaller : WorldInstaller
    {
        public Entity Created;
        public void Initialize() => base.Awake();
        protected override void OnWorldCreated(ref World world)
            => Systems.Add(ReviewStartupSystems.Start, Threads.Main, SystemPath.Start);
        protected override void CreateEntities(ref World world)
        {
            Created = world.Entity();
            Created.Add(new ReviewStartValue { Value = 10 });
        }
    }
    [TestFixture]
    public class ReviewFollowupTests
    {
        private static int jobUpdates, mainUpdates, jobFixed, mainFixed, destroyed;
        public struct JobUpdate : IEntityJobSystem
        {
            public Threads Mode => Threads.Main;
            public Query GetQuery(ref World world) => world.Query().With<ReviewStartValue>();
            public void OnUpdate(ref Entity entity, ref State state) => jobUpdates++;
        }
        public struct JobFixed : IEntityJobSystem, IFixed
        {
            public Threads Mode => Threads.Main;
            public Query GetQuery(ref World world) => world.Query().With<ReviewStartValue>();
            public void OnUpdate(ref Entity entity, ref State state) => jobFixed++;
        }
        public class MainUpdate : ISystem, IOnDestroy
        {
            public void OnUpdate(ref State state) => mainUpdates++;
            public void OnDestroy(ref World world) => destroyed++;
        }
        public struct MainFixed : ISystem, IFixed
        {
            public void OnUpdate(ref State state) => mainFixed++;
        }
        [SetUp]
        public void SetUp()
        {
            World.DisposeStatic();
            jobUpdates = mainUpdates = jobFixed = mainFixed = destroyed = 0;
        }
        [TearDown] public void TearDown() => World.DisposeStatic();

        [TestCase(false)]
        [TestCase(true)]
        public void ByteLoad_RestoresSavedSlotAndEntityHandles(bool nonzeroSlot)
        {
            if (nonzeroSlot) World.Create(WorldConfig.Default16);
            var source = World.Create(WorldConfig.Default1024);
            var entity = source.Entity(new ReviewStartValue { Value = 123 });
            source.Update();
            var savedId = source.Id;
            var data = source.Serialize();
            World.DisposeStatic();
            var loaded = World.Load(WorldConfig.Default16, data);
            Assert.AreEqual(savedId, loaded.Id);
            Assert.IsTrue(entity.IsValid());
            Assert.AreEqual(123, entity.Get<ReviewStartValue>().Value);
            var added = loaded.Entity(new ReviewStartValue { Value = 456 });
            loaded.Update();
            Assert.AreEqual(456, added.Get<ReviewStartValue>().Value);
            loaded.Dispose();
        }
        [Test]
        public void ByteLoad_OccupiedSavedSlot_DoesNotReplaceLivingWorld()
        {
            var source = World.Create(WorldConfig.Default256);
            var entity = source.Entity(new ReviewStartValue { Value = 12 });
            source.Update();
            var data = source.Serialize();
            Assert.Throws<InvalidOperationException>(() => World.Load(WorldConfig.Default16, data));
            Assert.IsTrue(source.IsAlive);
            Assert.IsTrue(entity.IsValid());
            Assert.AreEqual(12, entity.Get<ReviewStartValue>().Value);
            for (var i = 1; i < World.MAX_WORLD_COUNT; i++) World.Create(WorldConfig.Default16);
        }
        [Test]
        public void ByteLoad_InvalidData_DoesNotConsumeWorldSlot()
        {
            Assert.Throws<ArgumentNullException>(() => World.Load(WorldConfig.Default16, null));
            Assert.Catch(() => World.Load(WorldConfig.Default16, new byte[8]));
            for (var i = 0; i < World.MAX_WORLD_COUNT; i++) World.Create(WorldConfig.Default16);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void LegacyGroup_RunsUpdateAndFixedInTheirPhases(bool graph)
        {
            var world = World.Create(WorldConfig.Default256);
            world.Entity(new ReviewStartValue());
            world.Update();
            var group = new SystemsGroup(ref world).Add<JobUpdate>(false).Add<JobFixed>(false)
                .Add<MainUpdate>((byte)1).Add<MainFixed>(1);
            var systems = new Systems(ref world).Add(group);
            if (graph) systems.UseDependencyGraph();
            systems.OnStart();
            Assert.AreEqual(0, jobUpdates + mainUpdates + jobFixed + mainFixed);
            systems.OnUpdate(0.005f, 0.005f);
            Assert.AreEqual(1, jobUpdates);
            Assert.AreEqual(1, mainUpdates);
            Assert.AreEqual(0, jobFixed + mainFixed);
            systems.OnUpdate(0.02f, 0.025f);
            Assert.AreEqual(2, jobUpdates);
            Assert.AreEqual(2, mainUpdates);
            Assert.AreEqual(1, jobFixed);
            Assert.AreEqual(1, mainFixed);
            world.Dispose();
            Assert.AreEqual(1, destroyed);
            Assert.AreEqual(2, mainUpdates);
            Assert.AreEqual(1, mainFixed);
        }
        [Test]
        public void Installer_StartRunsAfterInitialComponentsAndFlushesItsChanges()
        {
            var go = new GameObject("ReviewInstaller test");
            try {
                var installer = go.AddComponent<ReviewInstaller>();
                installer.Initialize();
                Assert.AreEqual(11, installer.Created.Get<ReviewStartValue>().Value);
                Assert.IsTrue(installer.Created.Has<ReviewStarted>());
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
