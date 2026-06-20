using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Wargon.Nukecs.Reactivity;

namespace Wargon.Nukecs.Tests
{
    public struct BenchSpeed : IComponent { public float Value; }
    public struct BenchMana : IComponent { public float Value; }
    public struct BenchStamina : IComponent { public float Value; }
    public struct BenchShield : IComponent { public float Value; }

    /// <summary>
    /// Измеряет текущий (на основе задач) реактивный конвейер с различным количеством сущностей
    /// и количеством типов компонентов. Служит тестом регрессии — следите за тем, чтобы
    /// median не увеличивался со временем.
    /// </summary>
    [TestFixture]
    public class ReactivityBenchmark
    {
        private const int SmallCount = 100;
        private const int LargeCount = 1000;

        [SetUp]
        public void SetUp() => World.DisposeStatic();

        [TearDown]
        public void TearDown() => World.DisposeStatic();

        // ============ Single type ============

        [Test, Performance]
        public void Reactivity_100E_1T()
        {
            RunBenchHealth(SmallCount);
        }

        [Test, Performance]
        public void Reactivity_1000E_1T()
        {
            RunBenchHealth(LargeCount);
        }

        // ============ Five types per entity (multi-type scan) ============

        [Test, Performance]
        public void Reactivity_100E_5T()
        {
            RunBenchMultiType(SmallCount);
        }

        [Test, Performance]
        public void Reactivity_1000E_5T()
        {
            RunBenchMultiType(LargeCount);
        }

        // ============ Helpers ============

        private static void RunBenchHealth(int entityCount)
        {
            var world = World.Create(WorldConfig.Default16384);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(BenchHealth));
            var entities = new List<Entity>(entityCount);
            for (int i = 0; i < entityCount; i++)
            {
                ref var e = ref arch.CreateEntity();
                e.Get<BenchHealth>().Value = i;
                entities.Add(e);
            }
            systems.OnUpdate(0.016f, 0.016f);

            foreach (var e in entities)
                e.OnChange<BenchHealth>(NoopCallback<BenchHealth>);

            Measure.Method(() =>
                {
                    for (int i = 0; i < entities.Count; i++)
                        entities[i].Get<BenchHealth>().Value += 1f;
                    systems.OnUpdate(0.016f, 0.016f);
                })
                .WarmupCount(5)
                .MeasurementCount(50)
                .IterationsPerMeasurement(1)
                .Run();

            world.Dispose();
        }

        private static void RunBenchMultiType(int entityCount)
        {
            var world = World.Create(WorldConfig.Default16384);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(
                typeof(BenchHealth), typeof(BenchSpeed), typeof(BenchMana), typeof(BenchStamina), typeof(BenchShield));
            var entities = new List<Entity>(entityCount);
            for (int i = 0; i < entityCount; i++)
            {
                ref var e = ref arch.CreateEntity();
                e.Get<BenchHealth>().Value = i;
                e.Get<BenchSpeed>().Value = i;
                e.Get<BenchMana>().Value = i;
                e.Get<BenchStamina>().Value = i;
                e.Get<BenchShield>().Value = i;
                entities.Add(e);
            }
            systems.OnUpdate(0.016f, 0.016f);

            foreach (var e in entities)
            {
                e.OnChange<BenchHealth>(NoopCallback<BenchHealth>);
                e.OnChange<BenchSpeed>(NoopCallback<BenchSpeed>);
                e.OnChange<BenchMana>(NoopCallback<BenchMana>);
                e.OnChange<BenchStamina>(NoopCallback<BenchStamina>);
                e.OnChange<BenchShield>(NoopCallback<BenchShield>);
            }

            Measure.Method(() =>
                {
                    for (int i = 0; i < entities.Count; i++)
                    {
                        var e = entities[i];
                        e.Get<BenchHealth>().Value += 1f;
                        e.Get<BenchSpeed>().Value += 1f;
                        e.Get<BenchMana>().Value += 1f;
                        e.Get<BenchStamina>().Value += 1f;
                        e.Get<BenchShield>().Value += 1f;
                    }
                    systems.OnUpdate(0.016f, 0.016f);
                })
                .WarmupCount(5)
                .MeasurementCount(50)
                .IterationsPerMeasurement(1)
                .Run();

            world.Dispose();
        }

        private static void NoopCallback<T>(in T value, in Entity entity) where T : unmanaged, IComponent { }
    }
}
