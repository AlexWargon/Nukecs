using System;
using System.IO;
using NUnit.Framework;
using Wargon.Nukecs.Reactivity;

namespace Wargon.Nukecs.Tests
{
    public struct LoadHealth : IComponent { public int Value; }

    public class LoadDuringUpdateSystem : ISystem
    {
        public string Path;
        public bool Loaded;
        public unsafe void OnUpdate(ref State state)
        {
            if (Loaded) return;
            var id = state.World.Id;
            state.World.LoadFromFile(Path);
            Loaded = true;
            Assert.IsTrue(state.World.UnsafeWorld == World.Get(id).UnsafeWorld,
                "The current update must use the relocated arena before ECB playback.");
        }
    }

    public struct DeserializeCallbackProbe : ISystem, IOnWorldDeserialize
    {
        public int Loads;
        public void OnUpdate(ref State state) { }
        public void OnWorldDeserialize(ref World world) { Loads++; }
    }

    public class LoadedPlayerLinkSystem : ISystem, IOnWorldDeserialize
    {
        public Entity Player;
        public int Loads;
        public void OnUpdate(ref State state) { }
        public void OnWorldDeserialize(ref World world)
        {
            var players = world.Query().With<LoadHealth>();
            Player = players.IsEmpty ? Entity.Null : players.First();
            Loads++;
        }
    }

    [TestFixture]
    public unsafe class ReactiveLoadRegressionTests
    {
        [SetUp]
        public void SetUp() => World.DisposeStatic();
        [TearDown]
        public void TearDown() => World.DisposeStatic();

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RestartThenLoadInsideUpdate_RebindsExternalEntityLink(int mode)
        {
            var path = Path.Combine(Path.GetTempPath(), "nukecs-player-link-" + Guid.NewGuid() + ".dat");
            try {
                var source = World.Create(WorldConfig.Default1024);
                source.Entity(new LoadHealth { Value = 42 });
                source.Update();
                source.SaveToFile(path);
                World.DisposeStatic();

                var target = World.Create(WorldConfig.Default16);
                var newSessionPlayer = target.Entity(new LoadHealth { Value = 100 });
                target.Update();
                var systems = new Systems(ref target).Add<LoadDuringUpdateSystem>().Add<LoadedPlayerLinkSystem>();
                if (mode >= 0) systems.UseDependencyGraph(mode: (GroupScheduleMode)mode);
                var loader = ((SystemMainThreadRunnerClass<LoadDuringUpdateSystem>)systems.Runners[0]).System;
                var link = ((SystemMainThreadRunnerClass<LoadedPlayerLinkSystem>)systems.Runners[1]).System;
                loader.Path = path;
                link.Player = newSessionPlayer;
                systems.OnUpdate(0.016f, 0.016f);

                Assert.IsFalse(newSessionPlayer.IsValid());
                Assert.IsTrue(link.Player.IsValid());
                Assert.AreEqual(42, link.Player.Get<LoadHealth>().Value);
                Assert.AreEqual(1, link.Loads);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [TestCase(-1)]
        [TestCase((int)GroupScheduleMode.LegacyGroupComplete)]
        [TestCase((int)GroupScheduleMode.ChainedGroupComplete)]
        [TestCase((int)GroupScheduleMode.FlattenedSchedule)]
        [TestCase((int)GroupScheduleMode.FlattenedSchedule2)]
        public void LoadInsideUpdate_ReactivityResolvesCurrentWorldById(int mode)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nukecs-reactive-load-" + Guid.NewGuid() + ".dat");
            var source = World.Create(WorldConfig.Default1024);
            var worldId = source.Id;
            try
            {
                var savedEntity = source.Entity(new LoadHealth { Value = 42 });
                source.Update();
                var savedEntityId = savedEntity.id;
                source.SaveToFile(path);
                source.Dispose();

                var target = World.Create(WorldConfig.Default16);
                Assert.AreEqual(worldId, target.Id);
                var entity = target.Entity(new LoadHealth { Value = 100 });
                target.Update();
                Assert.AreEqual(savedEntityId, entity.id);
                var oldAddress = (IntPtr)target.UnsafeWorld;
                var systems = new Systems(ref target)
                    .Add<LoadDuringUpdateSystem>()
                    .Add<DeserializeCallbackProbe>();
                if (mode >= 0) systems.UseDependencyGraph(mode: (GroupScheduleMode)mode);
                var loader = ((SystemMainThreadRunnerClass<LoadDuringUpdateSystem>)systems.Runners[0]).System;
                loader.Path = path;
                var calls = 0;
                var observed = 0;
                ReactDelegate<LoadHealth> callback = (in LoadHealth health, in Entity changedEntity) =>
                {
                    calls++;
                    observed = health.Value;
                    Assert.AreEqual(worldId, changedEntity.world.Id);
                };
                entity.OnChange<LoadHealth>(callback);

                systems.OnUpdate(0.001f, 0.001f);
                ref var loaded = ref World.Get(worldId);
                Assert.AreNotEqual(oldAddress, (IntPtr)loaded.UnsafeWorld,
                    "Different arena sizes must exercise pointer relocation.");
                Assert.AreEqual(1, ((SystemMainThreadRunnerStruct<DeserializeCallbackProbe>)systems.Runners[1]).System.Loads,
                    "Mutations made through the boxed struct callback must be retained.");
                Assert.IsFalse(entity.IsValid(), "Loading a different saved world must expire the former target handle.");
                Assert.AreEqual(0, calls, "Subscriptions must not follow an unrelated saved entity with the same ID.");
                loaded.GetEntity(savedEntityId).OnChange<LoadHealth>(callback, ReactOptions.TriggerImmediately);
                Assert.AreEqual(1, calls);
                Assert.AreEqual(42, observed);
                loaded.GetEntity(savedEntityId).Get<LoadHealth>().Value = 55;
                systems.OnUpdate(0.001f, 0.002f);
                Assert.AreEqual(2, calls);
                Assert.AreEqual(55, observed);
                Assert.IsTrue(systems.World.UnsafeWorld == loaded.UnsafeWorld);
                loaded.Dispose();
            }
            finally
            {
                if (World.Get(worldId).IsAlive) World.Get(worldId).Dispose();
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
