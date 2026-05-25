using NUnit.Framework;
using Unity.Burst;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs.Tests
{
    public struct BenchPosition : IComponent
    {
        public float3 Value;
    }

    public struct BenchVelocity : IComponent
    {
        public float3 Value;
    }

    public struct BenchHealth : IComponent
    {
        public float Value;
    }

    public struct BenchDamage : IComponent
    {
        public int Value;
    }

    public struct BenchTag : IComponent
    {
    }

    public struct EntityCloneArchetype : IComponent
    {
        public Archetype val;
    }

    [BurstCompile]
    public static class BenchTestSystems
    {
        [System]
        [BurstCompile]
        public static void Iteration2(ref Query<BenchPosition, BenchVelocity> query)
        {
            foreach (var (pos, vel) in query) pos.Get.Value += vel.Read.Value;
        }

        [System]
        [BurstCompile]
        public static void Iteration3(ref Query<BenchPosition, BenchVelocity, BenchHealth> query)
        {
            foreach (var (pos, vel, hp) in query) 
                pos.Get.Value += vel.Read.Value;
        }

        [System]
        [BurstCompile]
        public static void Iteration4(ref Query<BenchPosition, BenchVelocity, BenchHealth, BenchDamage> query)
        {
            foreach (var (pos, vel, hp, dmg) in query)
            {
                pos.Get.Value += vel.Read.Value;
                hp.Get.Value += dmg.Read.Value;
            }
        }
        [System]
        [BurstCompile]
        public static void Iteration4_Iter_Chunk(ref Query<BenchPosition, BenchVelocity, BenchHealth, BenchDamage, None<BenchTag>> query)
        {
            foreach (var chunk in query.iter_chunk())
            {
                foreach (var c in chunk)
                {
                    c.C1.Value += c.C2.Value;
                    c.C3.Value += c.C4.Value;
                }
            }
        }
        [System]
        [BurstCompile]
        public static void Iteration4_Iter_Unsafe(ref Query<BenchPosition, BenchVelocity, BenchHealth, BenchDamage> query)
        {
            unsafe
            {
                foreach (var (pos, vel, hp, dmg) in query.iter_unsafe())
                {
                    pos->Value += vel->Value;
                    hp->Value += dmg->Value;
                }
            }
        }
        [System]
        [BurstCompile]
        public static void Iteration4_Iter(ref Query<BenchPosition, BenchVelocity, BenchHealth, BenchDamage> query)
        {
            foreach (var (pos, vel, hp, dmg) in query.iter())
            {
                pos.Get.Value += vel.Read.Value;
                hp.Get.Value += dmg.Read.Value;
            }
        }
        [System]
        [BurstCompile]
        public static void CreateEntitiesBatchSystem(ref Query<EntityCloneArchetype> query)
        {
            foreach (ref var arch in query) arch.val.BatchCreateEntity(EcsBenchmark.EntityCount);
        }

        public struct Iteration4_IEntityJobSystemMain : IEntityJobSystem
        {
            public Threads Mode => Threads.Main;

            public Query GetQuery(ref World world)
            {
                return world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>()
                    .With<BenchDamage>();
            }

            public void OnUpdate(ref Entity entity, ref State state)
            {
                ref var benchPosition = ref entity.Get<BenchPosition>();
                ref var benchVelocity = ref entity.Get<BenchVelocity>();
                ref var benchHealth = ref entity.Get<BenchHealth>();
                ref var benchDamage = ref entity.Get<BenchDamage>();
                benchPosition.Value += benchVelocity.Value;
                benchHealth.Value += benchDamage.Value;
            }
        }

        [BurstCompile]
        public struct Iteration4_IEntityJobSystemMainBurst : IEntityJobSystem
        {
            public Threads Mode => Threads.Main;

            public Query GetQuery(ref World world)
            {
                return world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>()
                    .With<BenchDamage>();
            }

            public void OnUpdate(ref Entity entity, ref State state)
            {
                ref var benchPosition = ref entity.Get<BenchPosition>();
                ref var benchVelocity = ref entity.Get<BenchVelocity>();
                ref var benchHealth = ref entity.Get<BenchHealth>();
                ref var benchDamage = ref entity.Get<BenchDamage>();
                benchPosition.Value += benchVelocity.Value;
                benchHealth.Value += benchDamage.Value;
            }
        }

        public struct Iteration4_IEntityJobSystemSingle : IEntityJobSystem
        {
            public Threads Mode => Threads.Single;

            public Query GetQuery(ref World world)
            {
                return world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>()
                    .With<BenchDamage>();
            }

            public void OnUpdate(ref Entity entity, ref State state)
            {
                ref var benchPosition = ref entity.Get<BenchPosition>();
                ref var benchVelocity = ref entity.Get<BenchVelocity>();
                ref var benchHealth = ref entity.Get<BenchHealth>();
                ref var benchDamage = ref entity.Get<BenchDamage>();
                benchPosition.Value += benchVelocity.Value;
                benchHealth.Value += benchDamage.Value;
            }
        }

        [BurstCompile]
        public struct Iteration4_IEntityJobSystemSingleBurst : IEntityJobSystem
        {
            public Threads Mode => Threads.Single;

            public Query GetQuery(ref World world)
            {
                return world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>()
                    .With<BenchDamage>();
            }

            public void OnUpdate(ref Entity entity, ref State state)
            {
                ref var benchPosition = ref entity.Get<BenchPosition>();
                ref var benchVelocity = ref entity.Get<BenchVelocity>();
                ref var benchHealth = ref entity.Get<BenchHealth>();
                ref var benchDamage = ref entity.Get<BenchDamage>();
                benchPosition.Value += benchVelocity.Value;
                benchHealth.Value += benchDamage.Value;
            }
        }

        public struct Iteration4_IEntityJobSystemParallel : IEntityJobSystem
        {
            public Threads Mode => Threads.Parallel;

            public Query GetQuery(ref World world)
            {
                return world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>()
                    .With<BenchDamage>();
            }

            public void OnUpdate(ref Entity entity, ref State state)
            {
                ref var benchPosition = ref entity.Get<BenchPosition>();
                ref var benchVelocity = ref entity.Get<BenchVelocity>();
                ref var benchHealth = ref entity.Get<BenchHealth>();
                ref var benchDamage = ref entity.Get<BenchDamage>();
                benchPosition.Value += benchVelocity.Value;
                benchHealth.Value += benchDamage.Value;
            }
        }

        [BurstCompile]
        public struct Iteration4_IEntityJobSystemParallelBurst : IEntityJobSystem
        {
            public Threads Mode => Threads.Parallel;

            public Query GetQuery(ref World world)
            {
                return world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>()
                    .With<BenchDamage>();
            }

            public void OnUpdate(ref Entity entity, ref State state)
            {
                ref var benchPosition = ref entity.Get<BenchPosition>();
                ref var benchVelocity = ref entity.Get<BenchVelocity>();
                ref var benchHealth = ref entity.Get<BenchHealth>();
                ref var benchDamage = ref entity.Get<BenchDamage>();
                benchPosition.Value += benchVelocity.Value;
                benchHealth.Value += benchDamage.Value;
            }
        }
    }

    [TestFixture]
    public class EcsBenchmark
    {
        [SetUp]
        public void Setup()
        {
            World.DisposeStatic();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsAlive) _world.Dispose();
            World.DisposeStatic();
        }

        public const int EntityCount = 10000;

        private static readonly WorldConfig BenchConfig = new()
        {
            StartPoolSize = EntityCount + 1,
            StartEntitiesAmount = EntityCount + 1,
            StartComponentsAmount = 64
        };

        private World _world;

        [Test]
        [Performance]
        public void Iteration_2Components_10K()
        {
            _world = World.Create(BenchConfig);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>();
            for (var i = 0; i < EntityCount; i++)
                _world.Entity(new BenchPosition { Value = 0 }, new BenchVelocity { Value = new float3(1, 2, 3) });
            _world.Update();


            Assert.AreEqual(EntityCount, query.Count);
            Measure.Method(() =>
                {
                    foreach (ref var entity in query)
                    {
                        ref var pos = ref entity.Get<BenchPosition>();
                        ref var vel = ref entity.Get<BenchVelocity>();
                        pos.Value += vel.Value;
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_3Components_10K()
        {
            _world = World.Create(BenchConfig);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>();
            for (var i = 0; i < EntityCount; i++)
            {
                var e = _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
            }

            _world.Update();
            Measure.Method(() =>
                {
                    foreach (ref var entity in query)
                    {
                        ref var pos = ref entity.Get<BenchPosition>();
                        ref var vel = ref entity.Get<BenchVelocity>();
                        ref var hp = ref entity.Get<BenchHealth>();
                        pos.Value += vel.Value;
                        hp.Value -= 1;
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();

            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_2Components_Archetype_10K()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration2, Threads.Main);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>();
            var queryNone = _world.Query().With<BenchPosition>().None<BenchVelocity>();
            for (var i = 0; i < EntityCount; i++)
                _world.Entity(new BenchPosition { Value = 0 }, new BenchVelocity { Value = new float3(1, 2, 3) });
            _world.Update();
            Assert.AreEqual(0, queryNone.Count);
            Assert.AreEqual(EntityCount, query.Count);
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_3Components_Archetype_10K()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration3, Threads.Main);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>();
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void Iteration_4Components_Archetype_10K_Main_Iter()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration4_Iter, Threads.Main);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void Iteration_4Components_Archetype_10K_Main_Iter_Chunk()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration4_Iter_Chunk, Threads.Main);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            // foreach (ref var entity in query)
            // {
            //     ref var pos = ref entity.Get<BenchPosition>();
            //     Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            // }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void Iteration_4Components_Archetype_10K_Main_IterUnsafe()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration4_Iter_Unsafe, Threads.Main);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void Iteration_4Components_Archetype_10K_Main()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration4, Threads.Main);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_4Components_Archetype_10K_Single_Burst()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration4, Threads.Single);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();;
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_4Components_Archetype_10K_Parallel_Burst()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration4);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();;
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_4Components_10K_IEntityJobSystem_Main()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add<BenchTestSystems.Iteration4_IEntityJobSystemMain>();
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();;
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_4Components_10K_IEntityJobSystem_Main_Burst()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add<BenchTestSystems.Iteration4_IEntityJobSystemMainBurst>();
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();;
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_4Components_10K_IEntityJobSystem_Single()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add<BenchTestSystems.Iteration4_IEntityJobSystemSingle>();
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();;
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_4Components_10K_IEntityJobSystem_Single_Burst()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add<BenchTestSystems.Iteration4_IEntityJobSystemSingleBurst>();
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();;
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_4Components_10K_IEntityJobSystem_Parallel()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add<BenchTestSystems.Iteration4_IEntityJobSystemParallel>();
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();;
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Iteration_4Components_10K_IEntityJobSystem_Parallel_Burst()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add<BenchTestSystems.Iteration4_IEntityJobSystemParallelBurst>();
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>().With<BenchDamage>();;
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
                e.Add(new BenchDamage { Value = 100 });
            }

            _world.Update();
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110, 220, 330), pos.Value);
            }

            Assert.AreEqual(EntityCount, query.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void EntityCreation_10K()
        {
            _world = World.Create(BenchConfig);
            var worldId = _world.Id;
            Measure.Method(() =>
                {
                    ref var localW = ref World.Get(worldId);

                    for (var i = 0; i < EntityCount; i++)
                    {
                        ref var e = ref localW.Entity();
                        e.Add(new BenchPosition { Value = new float3(i, 0, 0) });
                        e.Add(new BenchVelocity { Value = new float3(1, 0, 0) });
                    }

                    localW.Update();
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(1)
                .Run();
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void EntityCreationBATCH_10K()
        {
            _world = World.Create(BenchConfig);
            var arch = _world.GetArchetype(typeof(BenchPosition), typeof(BenchVelocity));
            Measure.Method(() =>
                {
                    var entities = arch.BatchCreateEntity(EntityCount);
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(1)
                .SetUp(() => { })
                .Run();
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void EntityCreationBATCH_10K_JobSingleBurst()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.CreateEntitiesBatchSystem, Threads.Single);
            var arch = _world.GetArchetype(typeof(BenchPosition), typeof(BenchVelocity));
            var e = _world.Entity();
            e.Add(new EntityCloneArchetype { val = arch });
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(1)
                .SetUp(() => { })
                .Run();
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void EntityCreationBATCH_10K_JobMainBurst()
        {
            _world = World.Create(BenchConfig);
            var systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.CreateEntitiesBatchSystem, Threads.Main);
            var arch = _world.GetArchetype(typeof(BenchPosition), typeof(BenchVelocity));
            var e = _world.Entity();
            e.Add(new EntityCloneArchetype { val = arch });
            Measure.Method(() => { systems.OnUpdate(1f, 1f); })
                .WarmupCount(5)
                .MeasurementCount(20)
                .IterationsPerMeasurement(1)
                .SetUp(() => { })
                .Run();
            _world.Dispose();
        }
        
        [Test]
        [Performance]
        public void ECB_AddComponent_10K()
        {
            _world = World.Create(BenchConfig);
            var query = _world.Query().With<BenchPosition>();
            var query2 = _world.Query().With<BenchPosition>().With<BenchVelocity>();
            var worldId = _world.Id;
            for (var i = 0; i < EntityCount; i++)
                _world.Entity(new BenchPosition { Value = 0 });
            _world.Update();
            Assert.AreEqual(EntityCount, query.Count);
            Measure.Method(() =>
                {
                    foreach (ref var entity in query)
                        entity.Add(new BenchVelocity { Value = new float3(1, 0, 0) });
                    World.Get(worldId).Update();
                })
                .WarmupCount(5)
                .MeasurementCount(50)
                .IterationsPerMeasurement(1)
                .Run();
            Assert.AreEqual(EntityCount, query2.Count);
            _world.Dispose();
        }

        [Test]
        [Performance]
        public void ECB_RemoveComponent_10K()
        {
            var batches = 60;
            var total = EntityCount * batches;
            var config = new WorldConfig
            {
                StartPoolSize = total + 1,
                StartEntitiesAmount = total + 1,
                StartComponentsAmount = 64
            };
            _world = World.Create(config);
            var entityIds = new int[total];
            for (var i = 0; i < total; i++)
            {
                var e = _world.Entity(new BenchPosition { Value = 0 },
                    new BenchVelocity { Value = new float3(1, 0, 0) });
                entityIds[i] = e.id;
            }

            _world.Update();
            var worldId = _world.Id;
            var cursor = 0;

            Measure.Method(() =>
                {
                    ref var w = ref World.Get(worldId);
                    var start = cursor * EntityCount;
                    var end = start + EntityCount;
                    for (var i = start; i < end; i++)
                        w.GetEntity(entityIds[i]).Remove<BenchVelocity>();
                    World.Get(worldId).Update();
                    cursor++;
                })
                .WarmupCount(5)
                .MeasurementCount(50)
                .IterationsPerMeasurement(1)
                .Run();

            _world.Dispose();
        }

        [Test]
        [Performance]
        public void RandomAccess_GetComponent_10K()
        {
            _world = World.Create(BenchConfig);
            var query = _world.Query().With<BenchHealth>();
            for (var i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = new float3(i, 0, 0) });
                e.Add(new BenchVelocity { Value = new float3(1, 0, 0) });
                e.Add(new BenchHealth { Value = i });
            }

            _world.Update();

            float sum = 0;

            Measure.Method(() =>
                {
                    sum = 0;
                    foreach (ref var entity in query)
                    {
                        ref var hp = ref entity.Get<BenchHealth>();
                        sum += hp.Value;
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();

            _world.Dispose();
        }

        [Test]
        [Performance]
        public void Migration_AddRemove_10K()
        {
            _world = World.Create(BenchConfig);
            var entities = new int[EntityCount];
            for (var i = 0; i < EntityCount; i++)
            {
                var e = _world.Entity(new BenchPosition { Value = 0 });
                entities[i] = e.id;
            }

            _world.Update();
            var worldID = _world.Id;

            Measure.Method(() =>
            {
                ref var world = ref World.Get(worldID);
                for (var i = 0; i < EntityCount; i++)
                {
                    ref var e = ref world.GetEntity(entities[i]);
                    if (i % 2 == 0)
                        e.Add(new BenchVelocity { Value = new float3(1, 0, 0) });
                    else
                        e.Add(new BenchHealth { Value = 100 });
                }

                world.Update();
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .IterationsPerMeasurement(1)
            .Run();

            _world.Dispose();
        }

        [Test]
        [Performance]
        public unsafe void DynamicBitMask256()
        {
            _world  = World.Create(BenchConfig);
            var mask = new DynamicBitmask(256, _world.UnsafeWorld);
            dbug.log(mask.Size());
            for (int i = 0; i < 255; i++)
            {
                mask.Add(i);
            }
            Measure.Method(() =>
            {
                for (int j = 0; j < 1000; j++)
                for (int i = 0; i < 255; i++)
                {
                    if (mask.Has(i))
                    {
                        
                    }
                }
            })
            .WarmupCount(5)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1)
            .Run();
            for (int i = 0; i < 255; i++)
            {
                Assert.True(mask.Has(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public unsafe void HierarchicalBitMask256_1024()
        {
            var mask = new Bitmask1024();
            dbug.log(mask.Size());
            for (int i = 0; i < 255; i++)
            {
                mask.Add(i);
            }
            Measure.Method(() =>
            {
                for (int j = 0; j < 1000; j++)
                for (int i = 0; i < 255; i++)
                {
                    if (mask.HasFast(i))
                    {
                        
                    }
                }
            })
            .WarmupCount(5)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1)
            .Run();
            for (int i = 0; i < 255; i++)
            {
                Assert.True(mask.Has(i));
            }
        }
        [Test]
        [Performance]
        public void HierarchicalBitMask256_4096()
        {
            var mask = new Bitmask4096();
            dbug.log(mask.Size());
            for (int i = 0; i < 255; i++)
            {
                mask.Add(i);
            }
            Measure.Method(() =>
            {
                for (var j = 0; j < 1000; j++)
                for (var i = 0; i < 255; i++)
                {
                    if (mask.Has(i))
                    {
                    
                    }
                }
            })
            .WarmupCount(5)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1)
            .Run();
            for (int i = 0; i < 255; i++)
            {
                Assert.True(mask.Has(i));
            }
        }
        [Test]
        [Performance]
        public void GET_FAST_HashMap_1023_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new HashMap<int,int>(1023, ref _world.AllocatorHandler);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.GetMemorySizeUsed())}KB/{map.GetMemorySizeUsed()}B");

            for (int i = 0; i < 1023; i++)
            {
                map.Add(i, i);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 1023; i++)
                        {
                            var d = map[i];
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 1023; i++)
            {
                Assert.True(map.ContainsKey(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void GET_FAST_BitMap1024_64_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new BitMap1024<int>(64, ref _world.AllocatorHandler.AllocatorWrapper.Allocator);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.Size())}KB/{map.Size()}B");
            for (int i = 0; i < 63; i++)
            {
                map.Add(i, i, ref _world.AllocatorHandler.AllocatorWrapper.Allocator);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 63; i++)
                        {
                            var d = map.GetRef(i);
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 63; i++)
            {
                Assert.True(map.Has(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void GET_FAST_ZeroMoveBitMap1024_64_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new ZeroMoveBitMap1024<int>(64, ref _world.AllocatorRef);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.Size())}KB/{map.Size()}B");
            for (int i = 0; i < 63; i++)
            {
                map.Add(i, i);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 63; i++)
                        {
                            var d = map.Get(i);
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 63; i++)
            {
                Assert.True(map.Has(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void GET_FAST_MemoryArray1024_64_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new MemoryArray<int>(1023, ref _world.AllocatorRef);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.GetMemorySizeUsed())}KB/{map.GetMemorySizeUsed()}B");
            for (int i = 0; i < 1023; i++)
            {
                map[i] = i;
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 64; i++)
                        {
                            var d = map[i];
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 64; i++)
            {
                Assert.True(map[i] == i);
            }
            _world.Dispose();
        }
                [Test]
        [Performance]
        public void GET_FAST_HashMap_64_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new HashMap<int,int>(64, ref _world.AllocatorHandler);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.GetMemorySizeUsed())}KB/{map.GetMemorySizeUsed()}B");

            for (int i = 0; i < 63; i++)
            {
                map.Add(i, i);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 63; i++)
                        {
                            var d = map[i];
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 63; i++)
            {
                Assert.True(map.ContainsKey(i));
            }
            _world.Dispose();
        }
        
        
                [Test]
        [Performance]
        public void GET_FAST_BitMap1024_16_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new BitMap1024<int>(16, ref _world.AllocatorHandler.AllocatorWrapper.Allocator);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.Size())}KB/{map.Size()}B");
            for (int i = 0; i < 15; i++)
            {
                map.Add(i, i, ref _world.AllocatorHandler.AllocatorWrapper.Allocator);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            var d = map.GetRef(i);
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 15; i++)
            {
                Assert.True(map.Has(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void GET_FAST_ZeroMoveBitMap1024_16_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new ZeroMoveBitMap1024<int>(16, ref _world.AllocatorRef);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.Size())}KB/{map.Size()}B");
            for (int i = 0; i < 15; i++)
            {
                map.Add(i, i);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            var d = map.Get(i);
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 15; i++)
            {
                Assert.True(map.Has(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void GET_FAST_MemoryArray1024_16_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new MemoryArray<int>(1024, ref _world.AllocatorRef);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.GetMemorySizeUsed())}KB/{map.GetMemorySizeUsed()}B");
            for (int i = 0; i < 1023; i++)
            {
                map[i] = i;
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            var d = map[i];
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 15; i++)
            {
                Assert.True(map[i] == i);
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void GET_FAST_HashMap_16_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new HashMap<int,int>(16, ref _world.AllocatorHandler);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.GetMemorySizeUsed())}KB/{map.GetMemorySizeUsed()}B");

            for (int i = 0; i < 15; i++)
            {
                map.Add(i, i);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            var d = map[i];
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 15; i++)
            {
                Assert.True(map.ContainsKey(i));
            }
            _world.Dispose();
        }
        
        
        
        
        
        
        
        
        
        [Test]
        [Performance]
        public void GET_FAST_BitMap1024_1023_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new BitMap1024<int>(1023, ref _world.AllocatorHandler.AllocatorWrapper.Allocator);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.Size())}KB/{map.Size()}B");
            for (int i = 0; i < 1023; i++)
            {
                map.Add(i, i, ref _world.AllocatorHandler.AllocatorWrapper.Allocator);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 1023; i++)
                        {
                            var d = map.GetRef(i);
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 512; i++)
            {
                Assert.True(map.Has(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void GET_FAST_ZeroMoveBitMap1024_1023_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new ZeroMoveBitMap1024<int>(1023, ref _world.AllocatorRef);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.Size())}KB/{map.Size()}B");
            for (int i = 0; i < 1023; i++)
            {
                map.Add(i, i);
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 1023; i++)
                        {
                            var d = map.Get(i);
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 512; i++)
            {
                Assert.True(map.Has(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void GET_FAST_MemoryArray1024_1023_GET()
        {
            _world  = World.Create(BenchConfig);
            var map = new MemoryArray<int>(1023, ref _world.AllocatorRef);
            dbug.log($"SIZE:{Memory.BytesToKilobytes(map.GetMemorySizeUsed())}KB/{map.GetMemorySizeUsed()}B");
            for (int i = 0; i < 1023; i++)
            {
                map[i] = i;
            }
            
            Measure.Method(() => 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        for (int i = 0; i < 1023; i++)
                        {
                            var d = map[i];
                        }
                    }

                })
                .WarmupCount(5)
                .MeasurementCount(100)
                .IterationsPerMeasurement(1)
                .Run();
            for (int i = 0; i < 512; i++)
            {
                Assert.True(map[i] == i);
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public unsafe void DynamicBitMask4096()
        {
            _world  = World.Create(BenchConfig);
            var mask = new DynamicBitmask(4095, _world.UnsafeWorld);
            dbug.log(mask.Size());
            for (int i = 0; i < 4095; i++)
            {
                mask.Add(i);
            }
            Measure.Method(() => 
            {
                for (int j = 0; j < 1000; j++)
                {
                    for (int i = 0; i < 4095; i++)
                    {
                        if (mask.Has(i))
                        {
                            
                        }
                    }
                }

            })
            .WarmupCount(5)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1)
            .Run();
            for (int i = 0; i < 4095; i++)
            {
                Assert.True(mask.Has(i));
            }
            _world.Dispose();
        }
        [Test]
        [Performance]
        public void HierarchicalBitMask4096()
        {
            var mask = new Bitmask4096();
            dbug.log(mask.Size());
            for (int i = 0; i < 4095; i++)
            {
                mask.Add(i);
            }
            Measure.Method(() =>
            {
                for (int j = 0; j < 1000; j++)
                {
                    for (int i = 0; i < 4095; i++)
                    {
                        if (mask.Has(i))
                        {
                            
                        }
                    }
                }
            })
            .WarmupCount(5)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1)
            .Run();
            for (int i = 0; i < 4095; i++)
            {
                Assert.True(mask.Has(i));
            }
        }
    }
}