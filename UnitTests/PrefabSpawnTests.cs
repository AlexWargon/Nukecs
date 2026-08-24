using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct BulletTransform : IComponent
    {
        public float X;
        public float Y;
    }

    public struct BulletVelocity : IComponent
    {
        public float X;
        public float Y;
    }

    public struct BulletLifetime : IComponent
    {
        public float Seconds;
    }

    public struct BulletTag : IComponent
    {

    }

    public struct PrefabRef : IComponent
    {
        public Entity value;
        public float SpawnDelay;
    }
    public static class BulletSystems
    {
        [System]
        public static void SpawnPrefab(ref Query<PrefabRef> query, ref State state)
        {
            foreach (ref var prefab in query)
            {
                prefab.SpawnDelay -= state.Time.DeltaTime;
                if (prefab.SpawnDelay <= 0f)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        state.World.SpawnPrefab(in prefab.value);
                    }
                    prefab.SpawnDelay = 0.1f;
                }
            }
        }
        [System]
        public static void TickLifetime(ref Query<Entity,BulletLifetime> query, ref State state)
        {
            foreach (var (e, life) in query)
            {
                // var remaining = life.Read.Seconds - state.Time.DeltaTime;
                // if (remaining <= 0f)
                //     e.DestroyNow();
                // else
                //     life.Get.Seconds = remaining;
                ref var lifetime = ref life.Get;
                lifetime.Seconds -= state.Time.DeltaTime;
                if (lifetime.Seconds <= 0f) {
                    e.DestroyNow();
                    dbug.log($"Destroyed {e.id} bullet lifetime");
                }
            }
        }

        [System]
        public static void MoveBullets(ref Query<BulletTransform, BulletVelocity> query, ref State state)
        {
            foreach (var (pos, vel) in query)
            {
                pos.Get.X += vel.Read.X * state.Time.DeltaTime;
                pos.Get.Y += vel.Read.Y * state.Time.DeltaTime;
            }
        }
    }

    [TestFixture]
    public class PrefabSpawnTests
    {
        private static Entity CreateBulletPrefab(World world)
        {
            var arch = world.GetArchetype(
                typeof(BulletTransform),
                typeof(BulletVelocity),
                typeof(BulletLifetime),
                typeof(BulletTag),
                typeof(IsPrefab));
            var prefab = arch.CreateEntity();
            prefab.Set(new BulletTransform { X = 0f, Y = 0f });
            prefab.Set(new BulletVelocity { X = 10f, Y = 20f });
            prefab.Set(new BulletLifetime { Seconds = 1f });
            world.Update();
            return prefab;
        }

        private static void AssertPrefabIntact(Entity prefab, string ctx)
        {
            Assert.IsTrue(prefab.Has<BulletTransform>(), $"{ctx} Prefab lost BulletTransform");
            Assert.IsTrue(prefab.Has<BulletVelocity>(), $"{ctx} Prefab lost BulletVelocity");
            Assert.IsTrue(prefab.Has<BulletLifetime>(), $"{ctx} Prefab lost BulletLifetime");
            Assert.IsTrue(prefab.Has<BulletTag>(), $"{ctx} Prefab lost BulletTag");
            Assert.IsTrue(prefab.Has<IsPrefab>(), $"{ctx} Prefab lost IsPrefab");
        }

        private static void AssertBulletComponents(Entity bullet, string ctx)
        {
            Assert.IsTrue(bullet.Has<BulletTransform>(), $"{ctx} Bullet missing BulletTransform");
            Assert.IsTrue(bullet.Has<BulletVelocity>(), $"{ctx} Bullet missing BulletVelocity");
            Assert.IsTrue(bullet.Has<BulletLifetime>(), $"{ctx} Bullet missing BulletLifetime");
            Assert.IsTrue(bullet.Has<BulletTag>(), $"{ctx} Bullet missing BulletTag");
            Assert.IsFalse(bullet.Has<IsPrefab>(), $"{ctx} Bullet should not have IsPrefab");
        }

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
        public void PrefabSpawn_CopyHasAllComponents()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();

            var prefab = CreateBulletPrefab(world);
            AssertPrefabIntact(prefab, "Before spawn");

            var spawned = world.SpawnPrefab(prefab);
            systems.OnUpdate(1f, 1f);
            systems.OnUpdate(1f, 1f);

            AssertBulletComponents(spawned, "After spawn");
            AssertPrefabIntact(prefab, "After spawn");
            Assert.AreEqual(10f, spawned.Get<BulletVelocity>().X, "BulletVelocity.X copied");
            Assert.AreEqual(20f, spawned.Get<BulletVelocity>().Y, "BulletVelocity.Y copied");
            Assert.AreEqual(1f, spawned.Get<BulletLifetime>().Seconds, "BulletLifetime copied");

            world.Dispose();
        }

        [Test]
        public void PrefabSpawn_MultipleSpawns()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();

            var prefab = CreateBulletPrefab(world);

            var spawned = new Entity[5];
            for (int i = 0; i < 5; i++)
                spawned[i] = world.SpawnPrefab(prefab);

            systems.OnUpdate(1f, 1f);
            systems.OnUpdate(1f, 1f);

            AssertPrefabIntact(prefab, "After 5 spawns");
            for (int i = 0; i < 5; i++)
                AssertBulletComponents(spawned[i], $"Bullet {i}");

            world.Dispose();
        }

        [Test]
        public void PrefabSpawn_SpawnDestroySpawn()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();

            var prefab = CreateBulletPrefab(world);

            var bullet1 = world.SpawnPrefab(prefab);
            systems.OnUpdate(1f, 1f);
            systems.OnUpdate(1f, 1f);

            AssertBulletComponents(bullet1, "Bullet1");
            AssertPrefabIntact(prefab, "After first spawn");

            bullet1.Destroy();
            systems.OnUpdate(1f, 2f);
            systems.OnUpdate(1f, 2f);

            AssertPrefabIntact(prefab, "After destroy");

            var bullet2 = world.SpawnPrefab(prefab);
            systems.OnUpdate(1f, 3f);
            systems.OnUpdate(1f, 3f);

            AssertBulletComponents(bullet2, "Bullet2 after respawn");
            AssertPrefabIntact(prefab, "After respawn");

            world.Dispose();
        }

        [Test]
        public void PrefabSpawn_SpawnDestroyLoop_StressTest()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);
            systems.AddDefaults();

            var prefab = CreateBulletPrefab(world);

            for (int iter = 0; iter < 50; iter++)
            {
                var bullets = new Entity[3];
                for (int i = 0; i < 3; i++)
                    bullets[i] = world.SpawnPrefab(prefab);

                systems.OnUpdate(1f, iter);
                systems.OnUpdate(1f, iter);

                AssertPrefabIntact(prefab, $"Iter {iter} post-spawn");

                for (int i = 0; i < 3; i++)
                {
                    Assert.IsTrue(bullets[i].Has<BulletTransform>(),
                        $"Iter {iter} bullet {i} missing BulletTransform before destroy");
                }

                for (int i = 0; i < 3; i++)
                    bullets[i].Destroy();

                systems.OnUpdate(1f, iter);
                systems.OnUpdate(1f, iter);

                AssertPrefabIntact(prefab, $"Iter {iter} post-destroy");
            }

            world.Dispose();
        }

        [Test]
        public void PrefabSpawn_SystemChain_SpawnProcessDestroy()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();
            systems.Add(BulletSystems.MoveBullets);
            systems.Add(BulletSystems.TickLifetime);

            var prefab = CreateBulletPrefab(world);

            for (int frame = 0; frame < 10; frame++)
            {
                if (frame % 3 == 0)
                {
                    var bullet = world.SpawnPrefab(prefab);
                    bullet.Get<BulletLifetime>().Seconds = 3f;
                }

                systems.OnUpdate(1f, frame);
                AssertPrefabIntact(prefab, $"Frame {frame}");
            }

            world.Dispose();
        }

        [Test]
        public void PrefabSpawn_SystemChain_ManyFrames()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);
            systems.AddDefaults();
            systems.Add(BulletSystems.MoveBullets);
            systems.Add(BulletSystems.TickLifetime);

            var prefab = CreateBulletPrefab(world);

            for (int frame = 0; frame < 100; frame++)
            {
                if (frame % 3 == 0)
                {
                    var bullet = world.SpawnPrefab(prefab);
                    bullet.Get<BulletLifetime>().Seconds = 3f;
                }

                systems.OnUpdate(1f, frame);
                AssertPrefabIntact(prefab, $"Frame {frame}");
            }

            world.Dispose();
        }
        [Test]
        public void PrefabSpawnInSystem_SystemChain_ManyFrames()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);
            systems.AddDefaults();
            systems.Add(BulletSystems.SpawnPrefab)
                    .Add(BulletSystems.MoveBullets)
                    .Add(BulletSystems.TickLifetime);

            var prefab = CreateBulletPrefab(world);
            var spawner = world.Entity();
            spawner.Add(new PrefabRef { value = prefab, SpawnDelay = 0.1f });
            for (int frame = 0; frame < 100; frame++)
            {
                systems.OnUpdate(0.16f, frame*0.16f);
                AssertPrefabIntact(prefab, $"Frame {frame}");
            }

            world.Dispose();
        }
        [Test]
        public void PrefabSpawn_ECBRemoveIsPrefab_PreservesPrefabData()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();

            var prefab = CreateBulletPrefab(world);

            var copies = new Entity[3];
            for (int i = 0; i < 3; i++)
                copies[i] = world.SpawnPrefab(prefab);

            systems.OnUpdate(1f, 1f);
            systems.OnUpdate(1f, 1f);

            AssertPrefabIntact(prefab, "After spawn+ECB flush");

            for (int i = 0; i < 3; i++)
                Assert.IsFalse(copies[i].Has<IsPrefab>(), $"Copy {i} should not have IsPrefab");

            Assert.AreEqual(10f, prefab.Get<BulletVelocity>().X, "Prefab velocity X preserved");
            Assert.AreEqual(20f, prefab.Get<BulletVelocity>().Y, "Prefab velocity Y preserved");
            Assert.AreEqual(1f, prefab.Get<BulletLifetime>().Seconds, "Prefab lifetime preserved");

            world.Dispose();
        }

        [Test]
        public void PrefabSpawn_RecycledEntityID_GetsCorrectArchetype()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddDefaults();

            var prefab = CreateBulletPrefab(world);

            var bullet1 = world.SpawnPrefab(prefab);
            systems.OnUpdate(1f, 1f);
            systems.OnUpdate(1f, 1f);

            AssertBulletComponents(bullet1, "Bullet1 initial");

            bullet1.Destroy();
            systems.OnUpdate(1f, 2f);
            systems.OnUpdate(1f, 2f);

            var bullet2 = world.SpawnPrefab(prefab);
            systems.OnUpdate(1f, 3f);
            systems.OnUpdate(1f, 3f);

            AssertBulletComponents(bullet2, "Bullet2 (recycled ID)");
            AssertPrefabIntact(prefab, "After recycle spawn");

            world.Dispose();
        }
    }
}
