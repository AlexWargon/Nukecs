using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    [TestFixture]
    public class SerializationTests
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
        public void SerializeDeserialize_EmptyWorld_WorldIsAlive()
        {
            var world = World.Create(WorldConfig.Default256);
            var data = world.Serialize();
            world.Deserialize(data);

            Assert.IsTrue(world.IsAlive);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_MigrationAfterLoad_PairEdgeCacheRebuilt()
        {
            // regression: pairEdges must survive serialization with valid inner lists
            // (ptr<Edge> fixup + Edge lists fixup), and post-load migrations must keep counts sane.
            // The cache is NON-EMPTY at save time (migration happens before Serialize),
            // so the restore path is exercised for real.
            var world = World.Create(WorldConfig.Default256);
            var withHealth = world.Query().With<HealthTest>();
            var withVelocity = world.Query().With<VelocityTest>();

            var e1 = world.Entity(new HealthTest { Value = 10 });
            var e2 = world.Entity(new HealthTest { Value = 20 });
            world.Update();
            Assert.AreEqual(2, withHealth.Count);

            // pre-save migration: fills the pair-edge cache for {Health}->{Health,Velocity}
            e1.Add(new VelocityTest { X = 0.5f, Y = 1f });
            world.Update();
            Assert.AreEqual(1, withVelocity.Count);
            e1.Remove<VelocityTest>();
            world.Update();
            Assert.AreEqual(0, withVelocity.Count);

            var data = world.Serialize();
            world.Deserialize(data);

            // post-load migration over the same (restored) transition
            foreach (ref var e in withHealth)
            {
                e.Add(new VelocityTest { X = 1f, Y = 2f });
            }
            world.Update();

            Assert.AreEqual(2, withVelocity.Count, "Query count must be correct after post-load migration");
            Assert.AreEqual(2, withHealth.Count, "Source query count must survive post-load migration");
            Assert.AreEqual(1f, e1.Get<VelocityTest>().X, "Restored edge must point to valid lists");
            Assert.AreEqual(2f, e2.Get<VelocityTest>().Y);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_EntityCountPreserved()
        {
            var world = World.Create(WorldConfig.Default256);
            const int N = 10;
            for (int i = 0; i < N; i++)
                world.Entity();

            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            Assert.AreEqual(N, world.EntitiesAmount);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_SingleComponent_ValuePreserved()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new HealthTest { Value = 42 });
            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            Assert.IsTrue(entity.Has<HealthTest>());
            Assert.AreEqual(42, entity.Get<HealthTest>().Value);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_MultipleComponents_AllValuesPreserved()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(
                new HealthTest { Value = 100 },
                new VelocityTest { X = 3.5f, Y = 7.2f }
            );
            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            Assert.IsTrue(entity.Has<HealthTest>());
            Assert.IsTrue(entity.Has<VelocityTest>());
            Assert.AreEqual(100, entity.Get<HealthTest>().Value);
            Assert.AreEqual(3.5f, entity.Get<VelocityTest>().X);
            Assert.AreEqual(7.2f, entity.Get<VelocityTest>().Y);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_MultipleEntities_AllPreserved()
        {
            var world = World.Create(WorldConfig.Default256);
            const int N = 10;
            var ids = new int[N];
            for (int i = 0; i < N; i++)
            {
                var entity = world.Entity(new HealthTest { Value = i });
                ids[i] = entity.id;
            }
            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            Assert.AreEqual(N, world.EntitiesAmount);
            for (int i = 0; i < N; i++)
            {
                ref var entity = ref world.GetEntity(ids[i]);
                Assert.IsTrue(entity.IsValid());
                Assert.AreEqual(i, entity.Get<HealthTest>().Value);
            }

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_QueryCountPreserved()
        {
            var world = World.Create(WorldConfig.Default256);
            var query = world.Query().With<HealthTest>();
            var entity = world.Entity(new HealthTest { Value = 10 });
            world.Update();

            
            var data = world.Serialize();
            world.Deserialize(data);

            Assert.AreEqual(1, query.Count);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_QueryIterationWorksAfterRoundtrip()
        {
            var world = World.Create(WorldConfig.Default256);
            const int N = 5;
            var ids = new int[N];
            var query = world.Query().With<HealthTest>();
            for (int i = 0; i < N; i++)
            {
                var entity = world.Entity(new HealthTest { Value = i * 10 });
                ids[i] = entity.id;
            }
            world.Update();

           
            var data = world.Serialize();
            world.Deserialize(data);

            Assert.AreEqual(N, query.Count);

            var foundIds = new System.Collections.Generic.HashSet<int>();
            foreach (ref var entity in query)
            {
                Assert.IsTrue(entity.Has<HealthTest>());
                foundIds.Add(entity.id);
            }
            Assert.AreEqual(N, foundIds.Count);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_NewEntityAfterRoundtrip_Works()
        {
            var world = World.Create(WorldConfig.Default256);
            var data = world.Serialize();
            world.Deserialize(data);

            var entity = world.Entity(new HealthTest { Value = 77 });
            world.Update();

            Assert.IsTrue(entity.Has<HealthTest>());
            Assert.AreEqual(77, entity.Get<HealthTest>().Value);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_RemoveComponentAfterRoundtrip_Works()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(
                new HealthTest { Value = 50 },
                new VelocityTest { X = 1f, Y = 2f }
            );
            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            entity.Remove<HealthTest>();
            world.Update();

            Assert.IsFalse(entity.Has<HealthTest>());
            Assert.IsTrue(entity.Has<VelocityTest>());

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_AddComponentAfterRoundtrip_Works()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new HealthTest { Value = 50 });
            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            entity.Add(new VelocityTest { X = 5f, Y = 10f });
            world.Update();

            Assert.IsTrue(entity.Has<HealthTest>());
            Assert.IsTrue(entity.Has<VelocityTest>());
            Assert.AreEqual(50, entity.Get<HealthTest>().Value);
            Assert.AreEqual(5f, entity.Get<VelocityTest>().X);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_DestroyEntityAfterRoundtrip_Works()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new HealthTest { Value = 50 });
            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            entity.DestroyNow();
            world.Update();
            Assert.IsFalse(entity.IsValid());

            world.Dispose();
        }

        [Test]
        public unsafe void SerializeDeserialize_ArchetypeCountPreserved()
        {
            var world = World.Create(WorldConfig.Default256);
            world.Entity(new HealthTest { Value = 1 });
            world.Entity(new VelocityTest { X = 1f, Y = 2f });
            world.Entity(new HealthTest { Value = 2 }, new VelocityTest { X = 3f, Y = 4f });
            world.Update();

            var archetypesBefore = world.UnsafeWorld->archetypesList.Length;

            var data = world.Serialize();
            world.Deserialize(data);

            var archetypesAfter = world.UnsafeWorld->archetypesList.Length;
            Assert.AreEqual(archetypesBefore, archetypesAfter);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_EntityArchetypeCorrectAfterRoundtrip()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new HealthTest { Value = 42 });
            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            Assert.IsTrue(entity.Has<HealthTest>());

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_SystemsWorkAfterRoundtrip()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();

            var entity = world.Entity(new HealthTest { Value = 100 });
            world.Update();
            systems.OnUpdate(0.016f, 0.016f);

            var data = world.Serialize();
            world.Deserialize(data);

            Assert.IsTrue(entity.Has<HealthTest>());
            Assert.AreEqual(100, entity.Get<HealthTest>().Value);

            systems.OnUpdate(0.016f, 0.032f);
            Assert.IsTrue(entity.Has<HealthTest>());

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_CodeGenSystemWorksAfterRoundtrip()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(TestSystems.Movement2, Threads.Main);

            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f },
                new VelocityTest { X = 1f, Y = 2f }
            );
            world.Update();
            Assert.AreEqual(0f, entity.Get<PositionTest>().X);
            Assert.AreEqual(0f, entity.Get<PositionTest>().Y);
            var data = world.Serialize();
            world.Deserialize(data);

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(1f, entity.Get<PositionTest>().X);
            Assert.AreEqual(2f, entity.Get<PositionTest>().Y);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_ComponentArrayPreserved()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();
            entity.AddArray<ChildTest>();
            world.Update();

            ref var children = ref entity.Get<ComponentArray<ChildTest>>();
            children.Add(new ChildTest { ParentId = 10 });
            children.Add(new ChildTest { ParentId = 20 });
            children.Add(new ChildTest { ParentId = 30 });

            var data = world.Serialize();
            world.Deserialize(data);

            ref var childrenAfter = ref entity.Get<ComponentArray<ChildTest>>();
            Assert.AreEqual(3, childrenAfter.Length);
            Assert.AreEqual(10, childrenAfter.ElementAt(0).ParentId);
            Assert.AreEqual(20, childrenAfter.ElementAt(1).ParentId);
            Assert.AreEqual(30, childrenAfter.ElementAt(2).ParentId);

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_LargeNumberOfEntities()
        {
            var world = World.Create(WorldConfig.Default1024);
            const int N = 500;
            var ids = new int[N];
            var query = world.Query().With<HealthTest>();
            for (int i = 0; i < N; i++)
            {
                var entity = world.Entity(new HealthTest { Value = i });
                ids[i] = entity.id;
            }
            world.Update();

            var data = world.Serialize();
            world.Deserialize(data);

            Assert.AreEqual(N, world.EntitiesAmount);

            
            Assert.AreEqual(N, query.Count);

            for (int i = 0; i < N; i++)
            {
                ref var entity = ref world.GetEntity(ids[i]);
                Assert.IsTrue(entity.IsValid());
                Assert.AreEqual(i, entity.Get<HealthTest>().Value);
            }

            world.Dispose();
        }

        [Test]
        public void SerializeDeserialize_EntityModifiedBeforeSerialize_PreservesLatestState()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new HealthTest { Value = 10 });
            world.Update();

            entity.Set(new HealthTest { Value = 99 });

            var data = world.Serialize();
            world.Deserialize(data);

            Assert.AreEqual(99, entity.Get<HealthTest>().Value);

            world.Dispose();
        }

        private static string GetTempFilePath()
        {
            var dir = Path.Combine(Path.GetTempPath(), "nukecs_tests");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"test_{System.Guid.NewGuid():N}.bin");
        }

        [Test]
        public void SaveToFile_LoadFromFile_EntityAndComponentPreserved()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var entity = world.Entity(new HealthTest { Value = 42 }, new VelocityTest { X = 1.5f, Y = 3.0f });
                world.Update();

                world.SaveToFile(path);

                var entity2 = world.Entity(new DamageTest { Amount = 999 });
                world.Update();
                Assert.AreEqual(2, world.EntitiesAmount, "Second entity added before load.");

                world.LoadFromFile(path);

                Assert.AreEqual(1, world.EntitiesAmount, "Should have only the original entity after LoadFromFile.");
                Assert.IsTrue(entity.Has<HealthTest>());
                Assert.AreEqual(42, entity.Get<HealthTest>().Value);
                Assert.IsTrue(entity.Has<VelocityTest>());
                Assert.AreEqual(1.5f, entity.Get<VelocityTest>().X);
                Assert.AreEqual(3.0f, entity.Get<VelocityTest>().Y);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void SaveToFile_LoadFromFile_MultipleEntitiesPreserved()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                const int N = 10;
                var ids = new int[N];
                for (int i = 0; i < N; i++)
                {
                    var entity = world.Entity(new HealthTest { Value = i * 5 });
                    ids[i] = entity.id;
                }
                world.Update();

                world.SaveToFile(path);
                world.LoadFromFile(path);

                Assert.AreEqual(N, world.EntitiesAmount);
                for (int i = 0; i < N; i++)
                {
                    ref var entity = ref world.GetEntity(ids[i]);
                    Assert.IsTrue(entity.IsValid());
                    Assert.AreEqual(i * 5, entity.Get<HealthTest>().Value);
                }

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void SaveToFile_LoadFromFile_QueryWorksAfterLoad()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var query = world.Query().With<HealthTest>();

                world.Entity(new HealthTest { Value = 10 });
                world.Entity(new HealthTest { Value = 20 });
                world.Entity(new VelocityTest { X = 1f, Y = 2f });
                world.Update();

                world.SaveToFile(path);
                world.LoadFromFile(path);

                Assert.AreEqual(2, query.Count);

                var sum = 0;
                foreach (ref var entity in query)
                {
                    sum += entity.Get<HealthTest>().Value;
                }
                Assert.AreEqual(30, sum);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void SaveToFile_LoadFromFile_SystemsRebuiltAfterLoad()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var systems = new Systems(ref world);
                systems.Add(TestSystems.Movement2, Threads.Main);

                var entity = world.Entity(
                    new PositionTest { X = 0f, Y = 0f },
                    new VelocityTest { X = 2f, Y = 3f }
                );
                world.Update();

                world.SaveToFile(path);
                world.LoadFromFile(path);

                systems.OnUpdate(1f, 1f);

                Assert.AreEqual(2f, entity.Get<PositionTest>().X);
                Assert.AreEqual(3f, entity.Get<PositionTest>().Y);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void SaveToFile_LoadFromFile_EmptyWorldPreservesAlive()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);

                world.SaveToFile(path);
                world.LoadFromFile(path);

                Assert.IsTrue(world.IsAlive);
                Assert.AreEqual(0, world.EntitiesAmount);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void SaveToFile_LoadFromFile_OperationsWorkAfterLoad()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var entity = world.Entity(new HealthTest { Value = 50 });
                world.Update();

                world.SaveToFile(path);
                world.LoadFromFile(path);

                var newEntity = world.Entity(new DamageTest { Amount = 77 });
                world.Update();

                Assert.IsTrue(newEntity.Has<DamageTest>());
                Assert.AreEqual(77, newEntity.Get<DamageTest>().Amount);
                Assert.AreEqual(50, entity.Get<HealthTest>().Value);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public async Task SaveToFileAsync_LoadAsync_EntityAndComponentPreserved()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var entity = world.Entity(
                    new HealthTest { Value = 88 },
                    new VelocityTest { X = 4.5f, Y = 6.5f }
                );
                world.Update();

                await world.SaveToFileAsync(path);
                await World.LoadAsync(path, world);

                Assert.AreEqual(1, world.EntitiesAmount);
                Assert.IsTrue(entity.Has<HealthTest>());
                Assert.AreEqual(88, entity.Get<HealthTest>().Value);
                Assert.IsTrue(entity.Has<VelocityTest>());
                Assert.AreEqual(4.5f, entity.Get<VelocityTest>().X);
                Assert.AreEqual(6.5f, entity.Get<VelocityTest>().Y);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public async Task SaveToFileAsync_LoadAsync_MultipleEntitiesPreserved()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                const int N = 5;
                var ids = new int[N];
                for (int i = 0; i < N; i++)
                {
                    var entity = world.Entity(new HealthTest { Value = i * 10 });
                    ids[i] = entity.id;
                }
                world.Update();

                await world.SaveToFileAsync(path);
                await World.LoadAsync(path, world);

                Assert.AreEqual(N, world.EntitiesAmount);
                for (int i = 0; i < N; i++)
                {
                    var e = world.GetEntity(ids[i]);
                    Assert.IsTrue(e.IsValid());
                    Assert.AreEqual(i * 10, e.Get<HealthTest>().Value);
                }
                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public async Task SaveToFileAsync_LoadAsync_QueryWorksAfterLoad()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var query = world.Query().With<HealthTest>();

                world.Entity(new HealthTest { Value = 15 });
                world.Entity(new HealthTest { Value = 25 });
                world.Update();

                await world.SaveToFileAsync(path);
                await World.LoadAsync(path, world);

                Assert.AreEqual(2, query.Count);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public async Task SaveToFileAsync_LoadAsync_EmptyWorldPreservesAlive()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);

                await world.SaveToFileAsync(path);
                await World.LoadAsync(path, world);

                Assert.IsTrue(world.IsAlive);
                Assert.AreEqual(0, world.EntitiesAmount);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void StaticLoad_SyncRoundtrip_EntityAndComponentPreserved()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var entity = world.Entity(
                    new HealthTest { Value = 55 },
                    new PositionTest { X = 10f, Y = 20f }
                );
                world.Update();

                world.SaveToFile(path);
                World.Load(path, ref world);

                Assert.AreEqual(1, world.EntitiesAmount);
                Assert.IsTrue(entity.Has<HealthTest>());
                Assert.AreEqual(55, entity.Get<HealthTest>().Value);
                Assert.IsTrue(entity.Has<PositionTest>());
                Assert.AreEqual(10f, entity.Get<PositionTest>().X);
                Assert.AreEqual(20f, entity.Get<PositionTest>().Y);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void StaticLoad_SyncRoundtrip_SystemsRebuiltAfterLoad()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var systems = new Systems(ref world);
                systems.Add(TestSystems.Movement2, Threads.Main);

                var entity = world.Entity(
                    new PositionTest { X = 0f, Y = 0f },
                    new VelocityTest { X = 5f, Y = 7f }
                );
                world.Update();

                world.SaveToFile(path);
                World.Load(path, ref world);

                systems.OnUpdate(1f, 1f);

                Assert.AreEqual(5f, entity.Get<PositionTest>().X);
                Assert.AreEqual(7f, entity.Get<PositionTest>().Y);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Serialize_RunSystem_Load_StateRestoredToPreSystemValues()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(TestSystems.Movement2, Threads.Main);
            var entity = world.Entity(
                new PositionTest { X = 10f, Y = 20f },
                new VelocityTest { X = 3f, Y = 4f }
            );
            world.Update();

            var data = world.Serialize();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(13f, entity.Get<PositionTest>().X, "System should have moved X");
            Assert.AreEqual(24f, entity.Get<PositionTest>().Y, "System should have moved Y");

            world.Deserialize(data);

            Assert.AreEqual(10f, entity.Get<PositionTest>().X, "X should be restored to pre-system value");
            Assert.AreEqual(20f, entity.Get<PositionTest>().Y, "Y should be restored to pre-system value");

            world.Dispose();
        }

        [Test]
        public void Serialize_RunSystem_Load_StateRestoredToPreSystemValues_MultipleEntities()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(TestSystems.Movement2, Threads.Main);
            var e1 = world.Entity(new PositionTest { X = 0f, Y = 0f }, new VelocityTest { X = 2f, Y = 3f });
            var e2 = world.Entity(new PositionTest { X = 100f, Y = 200f }, new VelocityTest { X = -1f, Y = -5f });
            var e3 = world.Entity(new HealthTest { Value = 42 });
            world.Update();

            var data = world.Serialize();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(2f, e1.Get<PositionTest>().X);
            Assert.AreEqual(195f, e2.Get<PositionTest>().Y);

            world.Deserialize(data);

            Assert.AreEqual(0f, e1.Get<PositionTest>().X);
            Assert.AreEqual(0f, e1.Get<PositionTest>().Y);
            Assert.AreEqual(100f, e2.Get<PositionTest>().X);
            Assert.AreEqual(200f, e2.Get<PositionTest>().Y);
            Assert.AreEqual(42, e3.Get<HealthTest>().Value, "Non-movement entity should be unaffected");

            world.Dispose();
        }

        [Test]
        public void Save_RunSystem_Load_StateRestoredToPreSystemValues()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var systems = new Systems(ref world);
                systems.Add(TestSystems.Movement2, Threads.Main);
                var entity = world.Entity(
                    new PositionTest { X = 5f, Y = 15f },
                    new VelocityTest { X = 10f, Y = 20f }
                );
                world.Update();

                world.SaveToFile(path);

                systems.OnUpdate(1f, 1f);

                Assert.AreEqual(15f, entity.Get<PositionTest>().X);
                Assert.AreEqual(35f, entity.Get<PositionTest>().Y);

                world.LoadFromFile(path);

                Assert.AreEqual(5f, entity.Get<PositionTest>().X);
                Assert.AreEqual(15f, entity.Get<PositionTest>().Y);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void Save_RunSystem_Load_RunSystem_ResultsIdentical()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(
                new PositionTest { X = 0f, Y = 0f },
                new VelocityTest { X = 5f, Y = 10f }
            );
            world.Update();

            var data = world.Serialize();

            var systems = new Systems(ref world);
            systems.Add(TestSystems.Movement2, Threads.Main);

            systems.OnUpdate(1f, 1f);
            var firstX = entity.Get<PositionTest>().X;
            var firstY = entity.Get<PositionTest>().Y;

            world.Deserialize(data);

            systems.OnUpdate(1f, 1f);
            var secondX = entity.Get<PositionTest>().X;
            var secondY = entity.Get<PositionTest>().Y;

            Assert.AreEqual(firstX, secondX, "X after second run should equal X after first run");
            Assert.AreEqual(firstY, secondY, "Y after second run should equal Y after first run");

            world.Dispose();
        }

        [Test]
        public void Save_RunSystem_Load_RunSystem_ResultsIdentical_MultipleEntities()
        {
            var world = World.Create(WorldConfig.Default256);
            var e1 = world.Entity(new PositionTest { X = 0f, Y = 0f }, new VelocityTest { X = 1f, Y = 2f });
            var e2 = world.Entity(new PositionTest { X = 50f, Y = 50f }, new VelocityTest { X = -3f, Y = 4f });
            var e3 = world.Entity(new PositionTest { X = -10f, Y = 100f }, new VelocityTest { X = 0.5f, Y = -1.5f });
            world.Update();

            var data = world.Serialize();

            var systems = new Systems(ref world);
            systems.Add(TestSystems.Movement2, Threads.Main);

            systems.OnUpdate(2f, 2f);
            var run1 = (
                e1.Get<PositionTest>().X, e1.Get<PositionTest>().Y,
                e2.Get<PositionTest>().X, e2.Get<PositionTest>().Y,
                e3.Get<PositionTest>().X, e3.Get<PositionTest>().Y
            );

            world.Deserialize(data);

            systems.OnUpdate(2f, 2f);
            var run2 = (
                e1.Get<PositionTest>().X, e1.Get<PositionTest>().Y,
                e2.Get<PositionTest>().X, e2.Get<PositionTest>().Y,
                e3.Get<PositionTest>().X, e3.Get<PositionTest>().Y
            );

            Assert.AreEqual(run1.Item1, run2.Item1, "e1.X mismatch");
            Assert.AreEqual(run1.Item2, run2.Item2, "e1.Y mismatch");
            Assert.AreEqual(run1.Item3, run2.Item3, "e2.X mismatch");
            Assert.AreEqual(run1.Item4, run2.Item4, "e2.Y mismatch");
            Assert.AreEqual(run1.Item5, run2.Item5, "e3.X mismatch");
            Assert.AreEqual(run1.Item6, run2.Item6, "e3.Y mismatch");

            world.Dispose();
        }

        [Test]
        public void SaveFile_RunSystem_LoadFile_RunSystem_ResultsIdentical()
        {
            var path = GetTempFilePath();
            try
            {
                var world = World.Create(WorldConfig.Default256);
                var systems = new Systems(ref world);
                systems.Add<Movement2System>();
                var entity = world.Entity(
                    new PositionTest { X = 0f, Y = 0f },
                    new VelocityTest { X = 7f, Y = 3f }
                );
                world.Update();

                world.SaveToFile(path);

                systems.OnUpdate(1f, 1f);
                var firstX = entity.Get<PositionTest>().X;
                var firstY = entity.Get<PositionTest>().Y;

                world.LoadFromFile(path);

                systems.OnUpdate(1f, 1f);
                var secondX = entity.Get<PositionTest>().X;
                var secondY = entity.Get<PositionTest>().Y;

                Assert.AreEqual(firstX, secondX, "X after second run should equal X after first run");
                Assert.AreEqual(firstY, secondY, "Y after second run should equal Y after first run");

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public async Task SaveFileAsync_RunSystem_LoadAsync_RunSystem_ResultsIdentical()
        {
            var path = GetTempFilePath();
            try
            {
                
                var world = World.Create(WorldConfig.Default256);
                var systems = new Systems(ref world);
                systems.Add(TestSystems.Movement2, Threads.Main);
                var entity = world.Entity(
                    new PositionTest { X = 0f, Y = 0f },
                    new VelocityTest { X = 4f, Y = 6f }
                );
                world.Update();

                await world.SaveToFileAsync(path);

                systems.OnUpdate(1f, 1f);
                var firstX = entity.Get<PositionTest>().X;
                var firstY = entity.Get<PositionTest>().Y;

                await World.LoadAsync(path, world);

                systems.OnUpdate(1f, 1f);
                var secondX = entity.Get<PositionTest>().X;
                var secondY = entity.Get<PositionTest>().Y;

                Assert.AreEqual(firstX, secondX);
                Assert.AreEqual(firstY, secondY);

                world.Dispose();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
