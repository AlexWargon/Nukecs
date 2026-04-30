using System;
using NUnit.Framework;
using Unity.Burst;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    public struct PositionTest : IComponent
    {
        public float X;
        public float Y;
    }
    
    public static class TestSystems
    {
        [System][BurstCompile]
        public static void Movement2(ref Query<PositionTest, VelocityTest> query, ref State state)
        {
            foreach (var (pos, vel) in query)
            {
                pos.Get.X += vel.Read.X * state.Time.DeltaTime;
                pos.Get.Y += vel.Read.Y * state.Time.DeltaTime;
            }
        }
        [System]
        public static void Movement3_2(ref Query<PositionTest, VelocityTest, DamageTest> query, ref State state)
        {
            foreach (var (pos, vel) in query)
            {
                pos.Get.X += vel.Read.X * state.Time.DeltaTime;
                pos.Get.Y += vel.Read.Y * state.Time.DeltaTime;
            }
        }
        [System]
        public static void Movement3_3(ref Query<PositionTest, VelocityTest, DamageTest> query, ref State state)
        {
            foreach (var (pos, vel, dmg) in query)
            {
                pos.Get.X += vel.Read.X * state.Time.DeltaTime;
                pos.Get.Y += vel.Read.Y * state.Time.DeltaTime;
            }
        }
        [System]
        public static void Movement4_4(ref Query<PositionTest, VelocityTest,DamageTest, HealthTest> query, ref State state)
        {
            foreach (var (pos, vel, dmg, hp) in query)
            {
                pos.Get.X += vel.Read.X * state.Time.DeltaTime;
                pos.Get.Y += vel.Read.Y * state.Time.DeltaTime;
            }
        }
        [System]
        public static void AddComponentSystem(ref Query<PositionTest, None<VelocityTest>>.WithEntity query)
        {
            foreach (var (e, _) in query)
            {
                e.Add(new VelocityTest());
            }
        }
        [System]
        public static void RemoveComponentSystem(ref Query<PositionTest, VelocityTest>.WithEntity query)
        {
            foreach (var (e, _, _) in query)
            {
                e.Remove<VelocityTest>();
            }
        }
    }

    public struct MovementSystemTest : ISystem, IOnCreate
    {
        private Query query;

        public void OnCreate(ref World world)
        {
            query = world.Query().With<PositionTest>().With<VelocityTest>();
        }

        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<PositionTest>();
                ref var vel = ref entity.Get<VelocityTest>();

                pos.X += vel.X * state.Time.DeltaTime;
                pos.Y += vel.Y * state.Time.DeltaTime;
            }
        }

    }

    [TestFixture]
    public class AdvancedTests
    {
        [SetUp]
        public void Setup()
        {
            World.DisposeStatic();
        }

        [TearDown]
        public void TearDown()
        {
            World.DisposeStatic();
        }
        [Test]
        public void AddRemoveSystem()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            systems.Add(TestSystems.AddComponentSystem, Threads.Main);
            systems.Add(TestSystems.RemoveComponentSystem, Threads.Main);
            systems.Add(TestSystems.RemoveComponentSystem, Threads.Main);
            
            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f }
            );
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.IsTrue(!entity.Has<VelocityTest>());

            world.Dispose();
        }
        [Test]
        public void SystemExample_ISystemImplementation()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            systems.Add<MovementSystemTest>();

            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f },
                new VelocityTest { X = 1f, Y = 2f }
            );
            world.Update();

            systems.OnUpdate(1f, 1f);

            ref var pos = ref entity.Get<PositionTest>();
            Assert.AreEqual(1f, pos.X, "Position X should be updated by system");
            Assert.AreEqual(2f, pos.Y, "Position Y should be updated by system");

            world.Dispose();
        }

        [Test]
        public void CodeGeneratedSystem()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            systems.Add(TestSystems.Movement2, Threads.Main);

            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f },
                new VelocityTest { X = 1f, Y = 2f }
            );
            world.Update();
            systems.OnUpdate(1f, 1f);

            ref var pos = ref entity.Get<PositionTest>();
            Assert.AreEqual(1f, pos.X, "Position X should be updated by code-generated system");
            Assert.AreEqual(2f, pos.Y, "Position Y should be updated by code-generated system");

            world.Dispose();
        }
        [Test]
        public void CodeGeneratedSystemSingle()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);

            systems.Add(TestSystems.Movement2, Threads.Single);
            var query = world.Query().With<PositionTest>().With<VelocityTest>();

            for (var i = 0; i < 1000; i++)
            {
                var e = world.Entity(
                    new PositionTest { X = 0f, Y = 0f },
                    new VelocityTest { X = 1f, Y = 1f }
                );
            }
            world.Update();

            systems.OnUpdate(1f, 1f);

            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<PositionTest>();
                Assert.AreEqual(1f, pos.X, "All positions X should be updated in parallel");
                Assert.AreEqual(1f, pos.Y, "All positions Y should be updated in parallel");
            }

            world.Dispose();
        }
        [Test]
        public void CodeGeneratedSystemMain()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);
            systems.Add(TestSystems.Movement2, Threads.Main);
            var query = world.Query().With<PositionTest>().With<VelocityTest>();

            for (var i = 0; i < 1000; i++)
            {
                var e = world.Entity(
                    new PositionTest { X = 0f, Y = 0f },
                    new VelocityTest { X = 1f, Y = 1f }
                );
            }
            world.Update();

            systems.OnUpdate(1f, 1f);
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<PositionTest>();
                Assert.AreEqual(1f, pos.X, "All positions X should be updated in parallel");
                Assert.AreEqual(1f, pos.Y, "All positions Y should be updated in parallel");
            }

            world.Dispose();
        }
        [Test]
        public void CodeGeneratedSystemParallel()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);

            systems.Add(TestSystems.Movement2, Threads.Parallel);
            var query = world.Query().With<PositionTest>().With<VelocityTest>();

            for (var i = 0; i < 1000; i++)
            {
                var e = world.Entity(
                    new PositionTest { X = 0f, Y = 0f },
                    new VelocityTest { X = 1f, Y = 1f }
                );
            }
            world.Update();
            systems.OnUpdate(1f, 1f);

            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<PositionTest>();
                Assert.AreEqual(1f, pos.X, "All positions X should be updated in parallel");
                Assert.AreEqual(1f, pos.Y, "All positions Y should be updated in parallel");
            }

            world.Dispose();
        }

        [Test]
        public void ComponentArray_AddMultipleElements()
        {
            var world = World.Create(WorldConfig.Default256);
            ref var entity = ref world.Entity();

            entity.AddArray<ChildTest>();

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();

            children.Add(new ChildTest { ParentId = 1 });
            children.Add(new ChildTest { ParentId = 2 });
            children.Add(new ChildTest { ParentId = 3 });
            world.Update();
            Assert.AreEqual(3, children.Length);
            Assert.AreEqual(1, children.ElementAt(0).ParentId);
            Assert.AreEqual(2, children.ElementAt(1).ParentId);
            Assert.AreEqual(3, children.ElementAt(2).ParentId);

            world.Dispose();
        }

        [Test]
        public void ComponentArray_RemoveAt()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();

            entity.AddArray<ChildTest>();
            world.Update();

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();

            children.Add(new ChildTest { ParentId = 1 });
            children.Add(new ChildTest { ParentId = 2 });
            children.Add(new ChildTest { ParentId = 3 });

            children.RemoveAt(1);

            Assert.AreEqual(2, children.Length);
            Assert.AreEqual(1, children.ElementAt(0).ParentId);
            Assert.AreEqual(3, children.ElementAt(1).ParentId);

            world.Dispose();
        }

        [Test]
        public void ComponentArray_RemoveRange()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();

            entity.AddArray<ChildTest>();
            world.Update();

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();

            children.Add(new ChildTest { ParentId = 1 });
            children.Add(new ChildTest { ParentId = 2 });
            children.Add(new ChildTest { ParentId = 3 });
            children.Add(new ChildTest { ParentId = 4 });

            children.RemoveRange(1, 2);

            Assert.AreEqual(2, children.Length);
            Assert.AreEqual(1, children.ElementAt(0).ParentId);
            Assert.AreEqual(4, children.ElementAt(1).ParentId);

            world.Dispose();
        }

        [Test]
        public void ComponentArray_Clear()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();

            entity.AddArray<ChildTest>();
            world.Update();

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();

            children.Add(new ChildTest { ParentId = 1 });
            children.Add(new ChildTest { ParentId = 2 });
            children.Add(new ChildTest { ParentId = 3 });

            Assert.AreEqual(3, children.Length);

            children.Clear();

            Assert.AreEqual(0, children.Length);

            world.Dispose();
        }

        [Test]
        public void ComponentArray_Enumerator()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();

            entity.AddArray<ChildTest>();
            world.Update();

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();

            children.Add(new ChildTest { ParentId = 10 });
            children.Add(new ChildTest { ParentId = 20 });
            children.Add(new ChildTest { ParentId = 30 });

            int sum = 0;
            foreach (var child in children)
            {
                sum += child.ParentId;
            }

            Assert.AreEqual(60, sum);

            world.Dispose();
        }

        [Test]
        public void ComponentArray_ReadAt()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();

            entity.AddArray<ChildTest>();
            world.Update();

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();

            children.Add(new ChildTest { ParentId = 100 });
            children.Add(new ChildTest { ParentId = 200 });

            var first = children.ReadAt(0);
            var second = children.ReadAt(1);

            Assert.AreEqual(100, first.ParentId);
            Assert.AreEqual(200, second.ParentId);

            world.Dispose();
        }

        [Test]
        public void ComponentArray_IndexOutOfRange_ThrowsException()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();

            entity.AddArray<ChildTest>();
            world.Update();

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();

            children.Add(new ChildTest { ParentId = 1 });

            try
            {
                children.ElementAt(-1);
                Assert.Fail("ElementAt(-1) should throw IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                children.ElementAt(1);
                Assert.Fail("ElementAt(1) should throw IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                children.ReadAt(-1);
                Assert.Fail("ReadAt(-1) should throw IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                children.ReadAt(1);
                Assert.Fail("ReadAt(1) should throw IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                children.RemoveAt(1);
                Assert.Fail("RemoveAt(1) should throw IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            world.Dispose();
        }
        // [Test]
        // public void AddRemoveSystemMain()
        // {
        //     var world = World.Create(WorldConfig.Default256);
        //     var systems = new Systems(ref world);
        //     systems.Add(TestSystems.AddComponentSystem, Threads.Main);
        //     systems.Add(TestSystems.RemoveComponentSystem, Threads.Main);
        //     
        //     var entity = world.Entity();
        //     entity.Add(new PositionTest());
        //     world.Update();
        //     systems.OnUpdate(1f,1f);
        //     Assert.IsTrue(!entity.Has<VelocityTest>());
        //     Assert.IsTrue(entity.Has<PositionTest>());
        // }
        //
        // [Test]
        // public void AddRemoveSystemSingle()
        // {
        //     var world = World.Create(WorldConfig.Default256);
        //     var systems = new Systems(ref world);
        //     systems.Add(TestSystems.AddComponentSystem, Threads.Single);
        //     systems.Add(TestSystems.RemoveComponentSystem, Threads.Single);
        //     
        //     var entity = world.Entity();
        //     entity.Add(new PositionTest());
        //     world.Update();
        //     systems.OnUpdate(1f,1f);
        //     
        //     Assert.IsTrue(!entity.Has<VelocityTest>());
        //     Assert.IsTrue(entity.Has<PositionTest>());
        // }
        // [Test]
        // public void AddRemoveSystemParallel()
        // {
        //     var world = World.Create(WorldConfig.Default256);
        //     var systems = new Systems(ref world);
        //     systems.Add(TestSystems.AddComponentSystem);
        //     systems.Add(TestSystems.RemoveComponentSystem);
        //     
        //     var entity = world.Entity();
        //     entity.Add(new PositionTest());
        //     world.Update();
        //     systems.OnUpdate(1f,1f);
        //     // MUST BE ONLY ONE AND WORK, BUT NOW WORK ONLY WITH TWO. 15 04 2026
        //     systems.OnUpdate(1f,1f);
        //     Assert.IsTrue(!entity.Has<VelocityTest>());
        //     Assert.IsTrue(entity.Has<PositionTest>());
        // }

        public struct TagTest : IComponent { }

        [Test]
        public void QueryT1TOption_WritesThroughArchetypeRef()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(QueryT1TOptionTestSystems.WriteThroughVal, Threads.Main);

            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f },
                new TagTest()
            );
            world.Update();
            systems.OnUpdate(1f, 1f);

            ref var pos = ref entity.Get<PositionTest>();
            Assert.AreEqual(42f, pos.X, "X should be written through inp.Val");
            Assert.AreEqual(99f, pos.Y, "Y should be written through inp.Val");

            world.Dispose();
        }
        [Test]
        public void OneEntityIterateSystem_1()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            systems.Add(TestSystems.Movement2, Threads.Main);

            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f },
                new VelocityTest { X = 1f, Y = 2f }
            );
            world.Update();
            systems.OnUpdate(1f, 1f);

            ref var pos = ref entity.Get<PositionTest>();
            Assert.AreEqual(1f, pos.X, "Position X should be updated by code-generated system");
            Assert.AreEqual(2f, pos.Y, "Position Y should be updated by code-generated system");

            world.Dispose();
        }
        [Test]
        public void OneEntityIterateSystem_2()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            systems.Add(TestSystems.Movement2, Threads.Main);
            var arch = world.GetArchetype(typeof(PositionTest), typeof(VelocityTest));
            var entity = arch.CreateEntity();
            entity.Set(new VelocityTest { X = 1f, Y = 2f });

            world.Update();
            systems.OnUpdate(1f, 1f);

            ref var pos = ref entity.Get<PositionTest>();
            Assert.AreEqual(1f, pos.X, "Position X should be updated by code-generated system");
            Assert.AreEqual(2f, pos.Y, "Position Y should be updated by code-generated system");

            world.Dispose();
        }
        [Test]
        public void TwoEntityIterateSystem_Query4_ForEach4()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            systems.AddDefaults().Add(TestSystems.Movement4_4, Threads.Main);
            var arch = world.GetArchetype(
                typeof(PositionTest), 
                typeof(VelocityTest),
                typeof(DamageTest),
                typeof(HealthTest),
                typeof(IsPrefab));
            var entity = arch.CreateEntity();
            entity.Set(new VelocityTest { X = 1f, Y = 2f });
            var entity2 = arch.CreateEntity();
            entity2.Set(new VelocityTest { X = 1f, Y = 2f });
            world.Update();

            var spawnPrefab = world.SpawnPrefab(entity);

            systems.OnUpdate(1f, 1f);
            systems.OnUpdate(1f, 1f);
            systems.OnUpdate(1f, 1f);
            ref var pos = ref entity.Get<PositionTest>();
            ref var pos2 = ref entity2.Get<PositionTest>();
            ref var spawnedPos = ref spawnPrefab.Get<PositionTest>();
            // Assert.AreEqual(2f, pos.X, "Position X should be updated by code-generated system");
            // Assert.AreEqual(4f, pos.Y, "Position Y should be updated by code-generated system");
            // Assert.AreEqual(2f, pos2.X, "Position X should be updated by code-generated system");
            // Assert.AreEqual(4f, pos2.Y, "Position Y should be updated by code-generated system");
            Assert.AreEqual(2f, spawnedPos.X, "Position X should be updated by code-generated system");
            Assert.AreEqual(4f, spawnedPos.Y, "Position Y should be updated by code-generated system");
            world.Dispose();
        }
    }

    public static class QueryT1TOptionTestSystems
    {
        [System]
        public static void WriteThroughVal(ref Query<PositionTest, AdvancedTests.TagTest> query)
        {
            foreach (var (inp, _) in query)
            {
                ref var pos = ref inp.Val;
                pos.X = 42f;
                pos.Y = 99f;
            }
        }
        
    }
}