using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct HealthTest : IComponent
    {
        public int Value;
    }

    public struct VelocityTest : IComponent
    {
        public float X;
        public float Y;
    }

    public struct DamageTest : IComponent
    {
        public int Amount;
    }

    public struct ChildTest : IArrayComponent
    {
        public int ParentId;
    }

    [TestFixture]
    public class WorldTests
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
        public void AddComponent_IsDeferredUntilWorldUpdate()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>();

            var entity = world.Entity();
            entity.Add(new HealthTest { Value = 10 });

            Assert.IsFalse(entity.Has<HealthTest>(), "Health should not be visible before world.Update().");
            Assert.AreEqual(0, query.Count, "Query should not match entity before world.Update().");

            world.Update();

            Assert.IsTrue(entity.Has<HealthTest>(), "Health should be visible after world.Update().");
            Assert.AreEqual(1, query.Count, "Query should match the entity after world.Update().");
            Assert.AreEqual(entity.id, query.First().id);

            world.Dispose();
        }

        [Test]
        public void RemoveComponent_IsDeferredUntilWorldUpdate()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>();

            var entity = world.Entity(new HealthTest { Value = 5 });
            world.Update();

            Assert.IsTrue(entity.Has<HealthTest>(), "Health should exist after initial update.");
            Assert.AreEqual(1, query.Count);

            entity.Remove<HealthTest>();

            Assert.IsTrue(entity.Has<HealthTest>(), "Health should still report existing before world.Update().");
            Assert.AreEqual(1, query.Count, "Query should still include entity before world.Update().");

            world.Update();

            Assert.IsFalse(entity.Has<HealthTest>(), "Health should be removed after world.Update().");
            Assert.AreEqual(0, query.Count, "Query should exclude entity after removal update.");

            world.Dispose();
        }

        [Test]
        public void QueryWithMultipleEntities_ReturnsOnlyMatchingEntities()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>();

            var entityA = world.Entity();
            entityA.Add(new HealthTest { Value = 1 });

            var entityB = world.Entity();
            entityB.Add(new HealthTest { Value = 2 });

            var entityC = world.Entity();

            world.Update();

            Assert.AreEqual(2, query.Count);
            Assert.AreEqual(entityA.id, query.First().id);
            Assert.AreNotEqual(entityC.id, query.First().id);

            world.Dispose();
        }

        [Test]
        public void QueryNone_ExcludesEntitiesWithExcludedComponent()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>().None<VelocityTest>();

            var entityWithOnlyHealth = world.Entity();
            entityWithOnlyHealth.Add(new HealthTest { Value = 10 });

            var entityWithBoth = world.Entity();
            entityWithBoth.Add(new HealthTest { Value = 20 });
            entityWithBoth.Add(new VelocityTest { X = 1f, Y = 2f });

            world.Update();

            Assert.AreEqual(1, query.Count);
            Assert.AreEqual(entityWithOnlyHealth.id, query.First().id);

            world.Dispose();
        }

        [Test]
        public void CreateEntityWithComponent_HasComponentImmediatelyAfterUpdate()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>();

            var entity = world.Entity(new HealthTest { Value = 42 });
            world.Update();

            Assert.IsTrue(entity.Has<HealthTest>());
            Assert.AreEqual(1, query.Count);

            world.Dispose();
        }

        [Test]
        public void GetComponent_ReturnsCorrectValues()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new HealthTest { Value = 100 });
            world.Update();

            ref var health = ref entity.Get<HealthTest>();
            Assert.AreEqual(100, health.Value);

            world.Dispose();
        }

        [Test]
        public void SetComponent_UpdatesExistingComponentValue()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new HealthTest { Value = 50 });
            world.Update();

            entity.Set(new HealthTest { Value = 75 });
            world.Update();

            ref var health = ref entity.Get<HealthTest>();
            Assert.AreEqual(75, health.Value);

            world.Dispose();
        }

        [Test]
        public void MultipleQueries_TrackDifferentComponentSets()
        {
            var world = World.Create(WorldConfig.Default256);
            var healthQuery = world.Query().With<HealthTest>();
            var velocityQuery = world.Query().With<VelocityTest>();
            var bothQuery = world.Query().With<HealthTest>().With<VelocityTest>();

            var entityA = world.Entity(new HealthTest { Value = 10 });
            var entityB = world.Entity(new VelocityTest { X = 1f, Y = 1f });
            var entityC = world.Entity(new HealthTest { Value = 20 }, new VelocityTest { X = 2f, Y = 2f });

            world.Update();

            Assert.AreEqual(2, healthQuery.Count);
            Assert.AreEqual(2, velocityQuery.Count);
            Assert.AreEqual(1, bothQuery.Count);

            Assert.AreEqual(entityC.id, bothQuery.First().id);

            world.Dispose();
        }

        [Test]
        public void EmptyQuery_ReturnsZeroCount()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>();

            world.Update();

            Assert.AreEqual(0, query.Count);
            Assert.IsTrue(query.IsEmpty);

            world.Dispose();
        }

        [Test]
        public void NullEntity_IsNotValid()
        {
            Assert.IsFalse(Entity.Null.IsValid());
        }

        [Test]
        public void QueryEnumeration_IteratesAllMatchingEntities()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>();

            var entityA = world.Entity(new HealthTest { Value = 1 });
            var entityB = world.Entity(new HealthTest { Value = 2 });
            var entityC = world.Entity(new HealthTest { Value = 3 });

            world.Update();

            var count = 0;
            foreach (ref var entity in query)
            {
                count++;
                Assert.IsTrue(entity.Has<HealthTest>());
            }

            Assert.AreEqual(3, count);

            world.Dispose();
        }

        [Test]
        public void AddMultipleComponents_AllBecomeVisibleAfterUpdate()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>().With<VelocityTest>();

            var entity = world.Entity();
            entity.Add(new HealthTest { Value = 5 });
            entity.Add(new VelocityTest { X = 1f, Y = 2f });

            Assert.AreEqual(0, query.Count, "Entity should not match before update.");

            world.Update();

            Assert.AreEqual(1, query.Count);
            Assert.IsTrue(entity.Has<HealthTest>());
            Assert.IsTrue(entity.Has<VelocityTest>());

            world.Dispose();
        }

        [Test]
        public void RemoveOneComponentOfMany_KeepsOtherComponents()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>();

            var entity = world.Entity(new HealthTest { Value = 50 }, new VelocityTest { X = 3f, Y = 4f });
            world.Update();

            Assert.IsTrue(entity.Has<HealthTest>());
            Assert.IsTrue(entity.Has<VelocityTest>());

            entity.Remove<HealthTest>();
            world.Update();

            Assert.IsFalse(entity.Has<HealthTest>());
            Assert.IsTrue(entity.Has<VelocityTest>(), "Velocity should remain after removing Health.");
            Assert.AreEqual(0, query.Count);

            world.Dispose();
        }

        [Test]
        public void SingletonComponent_GetAndSetWorks()
        {
            // var world = World.Create(WorldConfig.Default256);
            //
            // ref var singleton = ref world.GetSingleton<HealthTest>();
            // singleton.Value = 42;
            //
            // ref var retrieved = ref world.GetSingleton<HealthTest>();
            // Assert.AreEqual(42, retrieved.Value);
            //
            // world.Dispose();
        }

        [Test]
        public void ComponentArray_AddAndAccessElements()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();

            entity.AddArray<ChildTest>();
            world.Update();

            Assert.IsTrue(entity.Has<ComponentArray<ChildTest>>());

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();
            children.Add(new ChildTest { ParentId = 123 });

            Assert.AreEqual(1, children.Length);
            Assert.AreEqual(123, children.ElementAt(0).ParentId);

            world.Dispose();
        }

        [Test]
        public void EntityDestroy_RemovesFromQueries()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            var query = world.Query().With<HealthTest>();
            systems.AddDefaults();
            var entity = world.Entity(new HealthTest { Value = 100 });
            systems.OnUpdate(0.16f, 0.16f);
            
            Assert.AreEqual(1, query.Count);

            entity.Destroy();
            systems.OnUpdate(0.16f, 0.16f);
            systems.OnUpdate(0.16f, 0.16f);
            Assert.AreEqual(0, query.Count);
            Assert.IsFalse(entity.IsValid());

            world.Dispose();
        }

        [Test]
        public void QueryUpdates_AfterArchetypeChanges()
        {
            var world = World.Create(WorldConfig.Default256);
            var healthOnlyQuery = world.Query().With<HealthTest>().None<VelocityTest>();
            var bothQuery = world.Query().With<HealthTest>().With<VelocityTest>();

            var entity = world.Entity(new HealthTest { Value = 10 });
            world.Update();

            Assert.AreEqual(1, healthOnlyQuery.Count);
            Assert.AreEqual(0, bothQuery.Count);

            entity.Add(new VelocityTest { X = 1f, Y = 2f });
            world.Update();

            Assert.AreEqual(0, healthOnlyQuery.Count);
            Assert.AreEqual(1, bothQuery.Count);

            world.Dispose();
        }

        [Test]
        public void PrefabSpawn_CreatesCopyWithComponents()
        {
            var world = World.Create(WorldConfig.Default256);

            var prefab = world.Entity(new HealthTest { Value = 50 }, new VelocityTest { X = 5f, Y = 10f });
            world.Update();

            var spawned = world.SpawnPrefab(prefab);
            world.Update();

            Assert.IsTrue(spawned.Has<HealthTest>());
            Assert.IsTrue(spawned.Has<VelocityTest>());
            Assert.AreEqual(50, spawned.Get<HealthTest>().Value);
            Assert.AreEqual(5f, spawned.Get<VelocityTest>().X);

            world.Dispose();
        }

        [Test]
        public void MultipleWorlds_IsolatedEntities()
        {
            var world1 = World.Create(WorldConfig.Default256);
            var world2 = World.Create(WorldConfig.Default256);
            
            var query1 = world1.Query().With<HealthTest>();
            var query2 = world2.Query().With<HealthTest>();
            
            var entity1 = world1.Entity(new HealthTest { Value = 1 });
            var entity2 = world2.Entity(new HealthTest { Value = 2 });

            world1.Update();
            world2.Update();

            Assert.AreEqual(1, query1.Count);
            Assert.AreEqual(1, query2.Count);
            Assert.AreEqual(1, entity1.Get<HealthTest>().Value);
            Assert.AreEqual(2, entity2.Get<HealthTest>().Value);

            world1.Dispose();
            world2.Dispose();
        }

        [Test]
        public void WorldDispose_CleansUpResources()
        {
            var world = World.Create(WorldConfig.Default256);

            var entity = world.Entity(new HealthTest { Value = 1 });
            world.Update();

            Assert.IsTrue(world.IsAlive);

            world.Dispose();

            Assert.IsFalse(world.IsAlive);
        }

        [Test]
        public void EntityCount_ExceedsInitialCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            var query = world.Query().With<HealthTest>();
            const int total = 100;

            for (int i = 0; i < total; i++)
            {
                var entity = world.Entity(new HealthTest { Value = i });
            }

            world.Update();

            Assert.AreEqual(total, query.Count, $"Expected {total} entities but got {query.Count}");

            for (int i = 0; i < total; i++)
            {
                ref var entity = ref world.GetEntity(i + 1);
                Assert.IsTrue(entity.IsValid(), $"Entity {i + 1} should be valid");
                Assert.AreEqual(i, entity.Get<HealthTest>().Value, $"Entity {i + 1} has wrong component value");
            }

            world.Dispose();
        }

        [Test]
        public void EntityCount_DestroyAndCreateBeyondCapacity()
        {
            var world = World.Create(WorldConfig.Default16);
            const int initial = 16;
            var ids = new int[initial];

            for (int i = 0; i < initial; i++)
            {
                var e = world.Entity(new HealthTest { Value = i });
                ids[i] = e.id;
            }
            world.Update();

            for (int i = 0; i < initial; i++)
            {
                world.GetEntity(ids[i]).DestroyNow();
            }
            world.Update();

            const int nextBatch = 50;
            for (int i = 0; i < nextBatch; i++)
            {
                world.Entity(new HealthTest { Value = 100 + i });
            }
            world.Update();

            Assert.AreEqual(nextBatch, world.EntitiesAmount, $"Expected {nextBatch} entities");

            world.Dispose();
        }
    }
}
