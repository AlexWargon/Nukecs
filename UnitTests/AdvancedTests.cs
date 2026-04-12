using System;
using NUnit.Framework;
using Unity.Jobs;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests
{
    public struct PositionTest : IComponent
    {
        public float X;
        public float Y;
    }

    public static class TestSystems
    {
        [System]
        public static void Movement(ref Query<PositionTest, VelocityTest> query, ref State state)
        {
            foreach (var (pos, vel) in query)
            {
                pos.Get.X += vel.Read.X * state.Time.DeltaTime;
                pos.Get.Y += vel.Read.Y * state.Time.DeltaTime;
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
        public void SystemExample_ISystemImplementation()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            // Добавляем систему, реализующую ISystem
            systems.Add<MovementSystemTest>();

            // Создаем сущность с компонентами
            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f },
                new VelocityTest { X = 1f, Y = 2f }
            );
            world.Update();

            // Запускаем системы
            systems.OnUpdate(1f, 1f);

            // Проверяем результат
            ref var pos = ref entity.Get<PositionTest>();
            Assert.AreEqual(1f, pos.X, "Position X should be updated by system");
            Assert.AreEqual(2f, pos.Y, "Position Y should be updated by system");

            world.Dispose();
        }

        [Test]
        public void SystemExample_CodeGeneratedSystem()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            // Добавляем кодогенерируемую систему
            systems.Add(TestSystems.Movement, Threads.Main);

            // Создаем сущность с компонентами
            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f },
                new VelocityTest { X = 1f, Y = 2f }
            );
            world.Update();

            // Запускаем системы
            systems.OnUpdate(1f, 1f);

            // Проверяем результат
            ref var pos = ref entity.Get<PositionTest>();
            Assert.AreEqual(1f, pos.X, "Position X should be updated by code-generated system");
            Assert.AreEqual(2f, pos.Y, "Position Y should be updated by code-generated system");

            world.Dispose();
        }

        [Test]
        public void SystemExample_CodeGeneratedSystemParallel()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            // Добавляем кодогенерируемую систему в параллельном режиме
            systems.Add(TestSystems.Movement, Threads.Parallel);

            // Создаем несколько сущностей
            for (int i = 0; i < 10; i++)
            {
                world.Entity(
                    new PositionTest { X = 0f, Y = 0f },
                    new VelocityTest { X = 1f, Y = 1f }
                );
            }
            // Запускаем системы
            systems.OnUpdate(1f, 1f);

            // Проверяем все сущности
            var query = world.Query().With<PositionTest>().With<VelocityTest>();
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

            // Добавляем несколько элементов
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

            // Удаляем элемент по индексу 1
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

            // Удаляем диапазон с индекса 1, 2 элемента
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

            // Проверяем выход за границы с помощью try-catch
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
    }
}