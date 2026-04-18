using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct ScoreTest : IComponent
    {
        public int Points;
    }

    public struct NameTest : IComponent
    {
        public int Hash;
    }

    public struct TagOnlyTest : IComponent
    {
        public byte _;
    }

    public struct RuntimeDiscoveredA : IComponent
    {
        public int Alpha;
    }

    public struct RuntimeDiscoveredB : IComponent
    {
        public float Beta;
    }

    public struct RuntimeDiscoveredC : IComponent
    {
        public long Gamma;
        public double Delta;
    }

    [TestFixture]
    public class AddObjectTests
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
        public unsafe void AddObject_BoxedOnlyTypes_NeverReferencedElsewhere()
        {
            var world = World.Create(WorldConfig.Default256);

            var entity = world.Entity();

            IComponent boxedA = new RuntimeDiscoveredA { Alpha = 111 };
            IComponent boxedB = new RuntimeDiscoveredB { Beta = 2.5f };
            IComponent boxedC = new RuntimeDiscoveredC { Gamma = 999, Delta = 3.14 };

            entity.AddObject(boxedA);
            entity.AddObject(boxedB);
            entity.AddObject(boxedC);

            world.Update();

            var idxA = ComponentTypeMap.Index(typeof(RuntimeDiscoveredA));
            var idxB = ComponentTypeMap.Index(typeof(RuntimeDiscoveredB));
            var idxC = ComponentTypeMap.Index(typeof(RuntimeDiscoveredC));

            Assert.IsTrue(entity.Has(idxA),
                "RuntimeDiscoveredA should be present on entity after AddObject + Update.");
            Assert.IsTrue(entity.Has(idxB),
                "RuntimeDiscoveredB should be present on entity after AddObject + Update.");
            Assert.IsTrue(entity.Has(idxC),
                "RuntimeDiscoveredC should be present on entity after AddObject + Update.");

            var poolA = entity.worldPointer->GetUntypedPool(idxA);
            var poolB = entity.worldPointer->GetUntypedPool(idxB);
            var poolC = entity.worldPointer->GetUntypedPool(idxC);

            var readA = (RuntimeDiscoveredA)poolA.GetObject(entity.id);
            var readB = (RuntimeDiscoveredB)poolB.GetObject(entity.id);
            var readC = (RuntimeDiscoveredC)poolC.GetObject(entity.id);
            Assert.AreEqual(111, readA.Alpha,
                "RuntimeDiscoveredA value should round-trip through boxing.");
            Assert.AreEqual(2.5f, readB.Beta,
                "RuntimeDiscoveredB value should round-trip through boxing.");
            Assert.AreEqual(999, readC.Gamma,
                "RuntimeDiscoveredC.Gamma value should round-trip through boxing.");
            Assert.AreEqual(3.14, readC.Delta,
                "RuntimeDiscoveredC.Delta value should round-trip through boxing.");

            world.Dispose();
        }

        [Test]
        public void AddObject_SingleComponent_IsDeferredUntilUpdate()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<ScoreTest>();

            var entity = world.Entity();
            entity.AddObject(new ScoreTest { Points = 42 });

            Assert.IsFalse(entity.Has<ScoreTest>(),
                "Component should not be visible before world.Update().");
            Assert.AreEqual(0, query.Count,
                "Query should not match entity before world.Update().");

            world.Update();

            Assert.IsTrue(entity.Has<ScoreTest>(),
                "Component should be visible after world.Update().");
            Assert.AreEqual(1, query.Count,
                "Query should match the entity after world.Update().");

            world.Dispose();
        }

        [Test]
        public void AddObject_ComponentValue_StoredCorrectly()
        {
            var world = World.Create(WorldConfig.Default256);

            var entity = world.Entity();
            entity.AddObject(new ScoreTest { Points = 99 });
            world.Update();

            ref var score = ref entity.Get<ScoreTest>();
            Assert.AreEqual(99, score.Points,
                "Stored component value should match the value passed to AddObject.");

            world.Dispose();
        }

        [Test]
        public void AddObject_MultipleComponents_AllBecomeVisible()
        {
            var world = World.Create(WorldConfig.Default256);
            var scoreQuery = world.Query().With<ScoreTest>();
            var nameQuery = world.Query().With<NameTest>();
            var bothQuery = world.Query().With<ScoreTest>().With<NameTest>();

            var entity = world.Entity();
            entity.AddObject(new ScoreTest { Points = 10 });
            entity.AddObject(new NameTest { Hash = 123 });

            world.Update();

            Assert.IsTrue(entity.Has<ScoreTest>(), "Entity should have ScoreTest.");
            Assert.IsTrue(entity.Has<NameTest>(), "Entity should have NameTest.");
            Assert.AreEqual(1, scoreQuery.Count, "ScoreTest query should match 1 entity.");
            Assert.AreEqual(1, nameQuery.Count, "NameTest query should match 1 entity.");
            Assert.AreEqual(1, bothQuery.Count, "Both-query should match 1 entity.");

            world.Dispose();
        }

        [Test]
        public void AddObject_DuplicateComponent_IsIgnored()
        {
            var world = World.Create(WorldConfig.Default256);

            var entity = world.Entity();
            entity.AddObject(new ScoreTest { Points = 10 });
            world.Update();

            entity.AddObject(new ScoreTest { Points = 20 });
            world.Update();

            ref var score = ref entity.Get<ScoreTest>();
            Assert.AreEqual(10, score.Points,
                "Duplicate AddObject should be ignored; original value preserved.");

            world.Dispose();
        }

        [Test]
        public void AddObject_OnMultipleEntities_EachEntityIndependent()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<ScoreTest>();

            var entityA = world.Entity();
            var entityB = world.Entity();

            entityA.AddObject(new ScoreTest { Points = 1 });
            entityB.AddObject(new ScoreTest { Points = 2 });

            world.Update();

            Assert.AreEqual(2, query.Count, "Query should match both entities.");

            ref var scoreA = ref entityA.Get<ScoreTest>();
            ref var scoreB = ref entityB.Get<ScoreTest>();
            Assert.AreEqual(1, scoreA.Points, "Entity A should retain its own value.");
            Assert.AreEqual(2, scoreB.Points, "Entity B should retain its own value.");

            world.Dispose();
        }

        [Test]
        public void AddObject_ThenSetObject_UpdatesValue()
        {
            var world = World.Create(WorldConfig.Default256);

            var entity = world.Entity();
            entity.AddObject(new ScoreTest { Points = 5 });
            world.Update();

            entity.SetObject(new ScoreTest { Points = 50 });

            ref var score = ref entity.Get<ScoreTest>();
            Assert.AreEqual(50, score.Points,
                "SetObject should overwrite the component value.");

            world.Dispose();
        }

        [Test]
        public void AddObject_MixedWithGenericAdd_BothVisible()
        {
            var world = World.Create(WorldConfig.Default256);
            var bothQuery = world.Query().With<ScoreTest>().With<VelocityTest>();

            var entity = world.Entity();
            entity.Add(new VelocityTest { X = 3f, Y = 4f });
            entity.AddObject(new ScoreTest { Points = 7 });

            world.Update();

            Assert.IsTrue(entity.Has<ScoreTest>(), "Entity should have ScoreTest (added via AddObject).");
            Assert.IsTrue(entity.Has<VelocityTest>(), "Entity should have VelocityTest (added via Add<T>).");
            Assert.AreEqual(1, bothQuery.Count, "Both-query should match the entity.");

            world.Dispose();
        }

        [Test]
        public void AddObject_TagSizeComponent_RegisteredAndVisible()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<TagOnlyTest>();

            var entity = world.Entity();
            entity.AddObject(new TagOnlyTest());
            world.Update();

            Assert.IsTrue(entity.Has<TagOnlyTest>(),
                "Tag (size=1) component should be visible after AddObject + Update.");
            Assert.AreEqual(1, query.Count);

            world.Dispose();
        }

        [Test]
        public void AddObject_ComponentTypeMap_IndexConsistentWithTypeGeneric()
        {
            var world = World.Create(WorldConfig.Default256);

            var entity = world.Entity();
            entity.AddObject(new ScoreTest { Points = 1 });
            world.Update();

            var indexViaType = ComponentTypeMap.Index(typeof(ScoreTest));
            var indexViaGeneric = ComponentType<ScoreTest>.Index;

            Assert.AreEqual(indexViaGeneric, indexViaType,
                "Index(Type) and ComponentType<T>.Index should return the same value.");

            world.Dispose();
        }

        [Test]
        public void AddObject_ThenRemove_ComponentRemoved()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<ScoreTest>();

            var entity = world.Entity();
            entity.AddObject(new ScoreTest { Points = 10 });
            world.Update();

            Assert.IsTrue(entity.Has<ScoreTest>());
            Assert.AreEqual(1, query.Count);

            entity.Remove<ScoreTest>();
            world.Update();

            Assert.IsFalse(entity.Has<ScoreTest>(),
                "Component should be removed after Remove<T>() + Update.");
            Assert.AreEqual(0, query.Count, "Query should be empty after removal.");

            world.Dispose();
        }

        [Test]
        public unsafe void AddObject_GetObject_RoundTrip()
        {
            var world = World.Create(WorldConfig.Default256);

            var entity = world.Entity();
            entity.AddObject(new ScoreTest { Points = 77 });
            world.Update();
            
            var pool = entity.worldPointer->GetUntypedPool(ComponentType<ScoreTest>.Index);
            var obj = (ScoreTest)pool.GetObject(entity.id);

            Assert.AreEqual(77, obj.Points,
                "GetObject should return the stored component value.");

            world.Dispose();
        }
    }
}
