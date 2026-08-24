using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct TestEvent
    {
        public int EntityId;
        public int Value;
    }

    public struct EventScore : IComponent
    {
        public int Value;
        public int EntityId;
    }

    public struct EventHitCount : IComponent
    {
        public int Count;
    }

    public static class EventTestSystems
    {
        [System]
        public static void ProduceEvents(
            ref Query<Entity,EventScore> query,
            ref State state,
            ref Events<TestEvent> events)
        {
            foreach (var (e, score) in query)
            {
                events.Add(new TestEvent { EntityId = e.id, Value = score.Read.Value });
            }
        }

        [System]
        public static void ProduceEventsPar(
            ref Query<EventScore> query,
            ref State state,
            ref Events<TestEvent> events)
        {
            foreach (ref var score in query)
            {
                events.Add(new TestEvent { EntityId = score.EntityId, Value = score.Value });
            }
        }

        [System]
        public static void ProduceEventsParallel(
            ref Query<Entity,EventScore> query,
            ref State state,
            ref Events<TestEvent> events)
        {
            foreach (var (e, score) in query)
            {
                events.AddPar(new TestEvent { EntityId = e.id, Value = score.Read.Value });
            }
        }

        [System]
        public static void ConsumeEventsParallel(
            ref State state,
            ref Events<TestEvent> events)
        {
            foreach (ref var ev in events)
            {
                var e = state.World.GetEntity(ev.EntityId);
                ref var hitCount = ref e.Get<EventHitCount>();
                hitCount.Count++;
            }
        }

        [System]
        public static void ConsumeEventsReadPar(
            ref State state,
            ref Events<TestEvent> events)
        {
            var reader = events.ReadPar();
            for (int i = 0; i < reader.Length; i++)
            {
                ref var ev = ref reader[i];
                var e = state.World.GetEntity(ev.EntityId);
                ref var hitCount = ref e.Get<EventHitCount>();
                hitCount.Count++;
            }
        }

        [System]
        public static void ConsumeEventsGetEnumerator(
            ref State state,
            ref Events<TestEvent> events)
        {
            foreach (ref var ev in events)
            {
                var e = state.World.GetEntity(ev.EntityId);
                ref var hitCount = ref e.Get<EventHitCount>();
                hitCount.Count += ev.Value;
            }
        }

        [System]
        public static void DoubleEventValues(
            ref State state,
            ref Events<TestEvent> events)
        {
            var reader = events.ReadPar();
            for (int i = 0; i < reader.Length; i++)
            {
                ref var ev = ref reader[i];
                ev.Value *= 2;
            }
        }
    }

    [TestFixture]
    public class EventsTests
    {
        [SetUp]
        public void SetUp()
        {
            World.DisposeStatic();
        }

        [TearDown]
        public void TearDown()
        {
            World.DisposeStatic();
        }

        [Test]
        public void Events_ProduceAndConsume_Main()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(EventTestSystems.ProduceEvents, Threads.Main);
            systems.Add(EventTestSystems.ConsumeEventsReadPar, Threads.Single);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            var e1 = arch.CreateEntity();
            e1.Get<EventScore>().Value = 10;
            e1.Get<EventScore>().EntityId = e1.id;
            var e2 = arch.CreateEntity();
            e2.Get<EventScore>().Value = 20;
            e2.Get<EventScore>().EntityId = e2.id;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(1, e1.Get<EventHitCount>().Count, "e1 hit count");
            Assert.AreEqual(1, e2.Get<EventHitCount>().Count, "e2 hit count");

            world.Dispose();
        }

        [Test]
        public void Events_ProduceAndConsumeWithGetEnumerator_Single()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(EventTestSystems.ProduceEvents, Threads.Single);
            systems.Add(EventTestSystems.ConsumeEventsGetEnumerator, Threads.Single);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            var e1 = arch.CreateEntity();
            e1.Get<EventScore>().Value = 5;
            e1.Get<EventScore>().EntityId = e1.id;
            var e2 = arch.CreateEntity();
            e2.Get<EventScore>().Value = 15;
            e2.Get<EventScore>().EntityId = e2.id;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(5, e1.Get<EventHitCount>().Count, "e1: score=5 added");
            Assert.AreEqual(15, e2.Get<EventHitCount>().Count, "e2: score=15 added");

            world.Dispose();
        }
        [Test]
        public void Events_ProduceParallel_ConsumeParallel()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);
            systems.Add(EventTestSystems.ProduceEventsParallel);
            systems.Add(EventTestSystems.ConsumeEventsParallel);
            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            const int count = 100;
            var entities = arch.BatchCreateEntity(count);
            for (int i = 0; i < count; i++)
            {
                entities[i].Get<EventScore>().Value = i + 1;
                entities[i].Get<EventScore>().EntityId = entities[i].id;
            }
            world.Update();

            systems.OnUpdate(1f, 1f);

            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(1, entities[i].Get<EventHitCount>().Count, $"entity {i} hit count");
            }

            world.Dispose();
        }
        [Test]
        public void Events_ProduceParallel_ConsumeSingle()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);
            systems.Add(EventTestSystems.ProduceEventsParallel);
            systems.Add(EventTestSystems.ConsumeEventsParallel, Threads.Single);
            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            const int count = 100;
            var entities = arch.BatchCreateEntity(count);
            for (int i = 0; i < count; i++)
            {
                entities[i].Get<EventScore>().Value = i + 1;
                entities[i].Get<EventScore>().EntityId = entities[i].id;
            }
            world.Update();

            systems.OnUpdate(1f, 1f);

            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(1, entities[i].Get<EventHitCount>().Count, $"entity {i} hit count");
            }

            world.Dispose();
        }
        [Test]
        public void Events_StressManyEvents()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);

            systems.Add(EventTestSystems.ProduceEventsParallel);
            systems.Add(EventTestSystems.ConsumeEventsParallel);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            const int count = 500;
            var entities = arch.BatchCreateEntity(count);
            for (int i = 0; i < count; i++)
            {
                entities[i].Get<EventScore>().Value = 1;
                entities[i].Get<EventScore>().EntityId = entities[i].id;
            }
            
            world.Update();

            systems.OnUpdate(1f, 1f);

            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(1, entities[i].Get<EventHitCount>().Count, $"entity {i}");
            }

            world.Dispose();
        }
        [Test]
        public void Events_ClearBetweenFrames()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();
            systems.Add(EventTestSystems.ProduceEvents, Threads.Main);
            systems.Add(EventTestSystems.ConsumeEventsReadPar, Threads.Single);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            var e1 = arch.CreateEntity();
            e1.Get<EventScore>().Value = 1;
            e1.Get<EventScore>().EntityId = e1.id;
            world.Update();

            systems.OnUpdate(1f, 1f);
            Assert.AreEqual(1, e1.Get<EventHitCount>().Count, "frame 1: 1 hit");

            systems.OnUpdate(1f, 2f);
            Assert.AreEqual(2, e1.Get<EventHitCount>().Count, "frame 2: 2 hits total");

            world.Dispose();
        }

        [Test]
        public void Events_NoEvents_ZeroCount()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();
            systems.Add(EventTestSystems.ConsumeEventsReadPar, Threads.Single);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            var e1 = arch.CreateEntity();
            e1.Get<EventScore>().Value = 1;
            e1.Get<EventScore>().EntityId = e1.id;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(0, e1.Get<EventHitCount>().Count, "no events produced");

            world.Dispose();
        }

        [Test]
        public void Events_MultipleSystemsShareSameEvents()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();
            systems.Add(EventTestSystems.ProduceEvents, Threads.Main);
            systems.Add(EventTestSystems.DoubleEventValues, Threads.Single);
            systems.Add(EventTestSystems.ConsumeEventsGetEnumerator, Threads.Single);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            var e1 = arch.CreateEntity();
            e1.Get<EventScore>().Value = 7;
            e1.Get<EventScore>().EntityId = e1.id;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(14, e1.Get<EventHitCount>().Count, "value doubled: 7*2=14");

            world.Dispose();
        }

        [Test]
        public void Events_UpdateSetsRangeForGetEnumerator()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();
            systems.Add(EventTestSystems.ProduceEvents, Threads.Main);
            systems.Add(EventTestSystems.ConsumeEventsGetEnumerator, Threads.Main);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            var e1 = arch.CreateEntity();
            e1.Get<EventScore>().Value = 3;
            e1.Get<EventScore>().EntityId = e1.id;
            var e2 = arch.CreateEntity();
            e2.Get<EventScore>().Value = 7;
            e2.Get<EventScore>().EntityId = e2.id;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(3, e1.Get<EventHitCount>().Count, "e1: 3");
            Assert.AreEqual(7, e2.Get<EventHitCount>().Count, "e2: 7");

            world.Dispose();
        }

        [Test]
        public void Events_ProduceOnly_NoConsume()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();
            systems.Add(EventTestSystems.ProduceEvents, Threads.Main);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            var e1 = arch.CreateEntity();
            e1.Get<EventScore>().Value = 42;
            e1.Get<EventScore>().EntityId = e1.id;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(0, e1.Get<EventHitCount>().Count, "no consumer");

            world.Dispose();
        }

        [Test]
        public void Events_QueryCountBeforeOnUpdate()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(EventTestSystems.ProduceEvents, Threads.Main);
            systems.Add(EventTestSystems.ConsumeEventsReadPar, Threads.Single);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            const int count = 10;
            var entities = arch.BatchCreateEntity(count);
            for (int i = 0; i < count; i++)
            {
                entities[i].Get<EventScore>().Value = i;
                entities[i].Get<EventScore>().EntityId = entities[i].id;
            }
            world.Update();

            Assert.AreEqual(count, world.EntitiesAmount, "entities created");

            systems.OnUpdate(1f, 1f);

            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(1, entities[i].Get<EventHitCount>().Count, $"entity {i}");
            }

            world.Dispose();
        }

        [Test]
        public void Events_NonWithEntityQuery_Minimal()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(EventTestSystems.ProduceEventsPar, Threads.Main);
            systems.Add(EventTestSystems.ConsumeEventsReadPar, Threads.Single);

            var arch = world.GetArchetype(typeof(EventScore), typeof(EventHitCount));
            var e1 = arch.CreateEntity();
            e1.Get<EventScore>().Value = 10;
            e1.Get<EventScore>().EntityId = e1.id;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(1, e1.Get<EventHitCount>().Count, "e1 hit count via non-WithEntity query");

            world.Dispose();
        }
    }
}
