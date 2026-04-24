using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    public struct BenchPosition : IComponent { public float3 Value; }
    public struct BenchVelocity : IComponent { public float3 Value; }
    public struct BenchHealth : IComponent { public float Value; }
    public struct BenchTag : IComponent { }

    [BurstCompile]
    public static class BenchTestSystems
    {
        [System, BurstCompile]
        public static void Iteration2(ref Query<BenchPosition, BenchVelocity> query)
        {
            foreach (var (pos, vel) in query)
            {
                pos.Get.Value += vel.Read.Value;
            }
        }
    }
    [TestFixture]
    public class EcsBenchmark
    {
        private const int EntityCount = 10000;
        private static readonly WorldConfig BenchConfig = new()
        {
            StartPoolSize = EntityCount + 1,
            StartEntitiesAmount = EntityCount + 1,
            StartComponentsAmount = 64
        };

        private World _world;

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

        [Test, Performance]
        public void Iteration_2Components_10K()
        {
            _world = World.Create(BenchConfig);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>();
            for (int i = 0; i < EntityCount; i++)
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
                Assert.AreEqual(new float3(110,220,330),pos.Value);
            }
            _world.Dispose();
        }

        [Test, Performance]
        public void Iteration_3Components_10K()
        {
            _world = World.Create(BenchConfig);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>();
            for (int i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
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

        [Test, Performance]
        public void Iteration_2Components_Archetype_10K()
        {
            _world = World.Create(BenchConfig);
            Systems systems = new Systems(ref _world);
            systems.Add(BenchTestSystems.Iteration2, Threads.Main);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>();
            var queryNone = _world.Query().With<BenchPosition>().None<BenchVelocity>();
            for (int i = 0; i < EntityCount; i++)
                _world.Entity(new BenchPosition { Value = 0 }, new BenchVelocity { Value = new float3(1, 2, 3) });
            _world.Update();
            Assert.AreEqual(0, queryNone.Count);
            Assert.AreEqual(EntityCount, query.Count);
            Measure.Method(() =>
            {
                systems.OnUpdate(1f,1f);
            })
            .WarmupCount(10)
            .MeasurementCount(100)
            .IterationsPerMeasurement(1)
            .Run();
            foreach (ref var entity in query)
            {
                ref var pos = ref entity.Get<BenchPosition>();
                Assert.AreEqual(new float3(110,220,330),pos.Value);
            }
            _world.Dispose();
        }

        [Test, Performance]
        public void Iteration_3Components_Archetype_10K()
        {
            _world = World.Create(BenchConfig);
            var query = _world.Query().With<BenchPosition>().With<BenchVelocity>().With<BenchHealth>();
            for (int i = 0; i < EntityCount; i++)
            {
                ref var e = ref _world.Entity();
                e.Add(new BenchPosition { Value = 0 });
                e.Add(new BenchVelocity { Value = new float3(1, 2, 3) });
                e.Add(new BenchHealth { Value = 100 });
            }
            _world.Update();
            Measure.Method(() =>
            {
                foreach (ref var e in query)
                {
                    ref var pos = ref e.Get<BenchPosition>();
                    ref var vel = ref e.Get<BenchVelocity>();
                    ref var hp = ref e.Get<BenchHealth>();
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
        
        [Test, Performance]
        public void EntityCreation_10K()
        {
            _world = World.Create(BenchConfig);
            var worldId = _world.Id;
            Measure.Method(() =>
                {
                    ref var localW = ref World.Get(worldId);

                    for (int i = 0; i < EntityCount; i++)
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
        [Test, Performance]
        public void EntityCreationBATCH_10K()
        {
            _world = World.Create(BenchConfig);
            var arch = _world.GetArchetype(typeof(BenchPosition), typeof(BenchVelocity));
            Measure.Method(() =>
            {
                var entities = arch.BatchCreateEntity(EntityCount);

                for (int i = 0; i < EntityCount; i++)
                {
                    ref var e = ref entities[i];
                    e.Set(new BenchPosition { Value = new float3(i, 0, 0) });
                    e.Set(new BenchVelocity { Value = new float3(1, 0, 0) });
                }
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .IterationsPerMeasurement(1)
            .Run();
            _world.Dispose();
        }

        [Test, Performance]
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

        [Test, Performance]
        public void ECB_RemoveComponent_10K()
        {
            int batches = 60;
            int total = EntityCount * batches;
            var config = new WorldConfig
            {
                StartPoolSize = total + 1,
                StartEntitiesAmount = total + 1,
                StartComponentsAmount = 64
            };
            _world = World.Create(config);
            var entityIds = new int[total];
            for (int i = 0; i < total; i++)
            {
                var e = _world.Entity(new BenchPosition { Value = 0 }, new BenchVelocity { Value = new float3(1, 0, 0) });
                entityIds[i] = e.id;
            }
            _world.Update();
            var worldId = _world.Id;
            int cursor = 0;

            Measure.Method(() =>
                {
                    ref var w = ref World.Get(worldId);
                    var start = cursor * EntityCount;
                    var end = start + EntityCount;
                    for (int i = start; i < end; i++)
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

        [Test, Performance]
        public void RandomAccess_GetComponent_10K()
        {
            _world = World.Create(BenchConfig);
            var query = _world.Query().With<BenchHealth>();
            for (int i = 0; i < EntityCount; i++)
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

        [Test, Performance]
        public void Migration_AddRemove_10K()
        {
            _world = World.Create(BenchConfig);
            var entities = new int[EntityCount];
            for (int i = 0; i < EntityCount; i++)
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
    }
}
