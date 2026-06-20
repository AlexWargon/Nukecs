using NUnit.Framework;
using Unity.Burst;
using Wargon.Nukecs.Reactivity;

namespace Wargon.Nukecs.Tests
{
    public struct Speed : IComponent
    {
        public float value;
    }

    public struct ReactiveHealth : IComponent
    {
        public float Value;
        public float MaxValue;
    }

    public struct ReactiveCounter : IComponent
    {
        public int Hits;
        public float LastValue;
    }

    public struct ChangedHitCount : IComponent
    {
        public int Count;
    }

    public struct Mana : IComponent
    {
        public float Value;
    }

    public struct PlayerTag : IComponent { }

    public struct DamageFlash : IComponent
    {
        public float Intensity;
    }

    public static class ChangedQueryTestSystems
    {
        [System, BurstCompile]
        public static void ProcessChangedHealth(ref Query<ReactiveHealth, Changed<ReactiveHealth>> query, ref State state)
        {
            foreach (ref var hp in query)
            {
                hp.Value += 1f;
            }
        }

        [System, BurstCompile]
        public static void CountChangedHealth(
            ref Query<ChangedHitCount, Changed<ReactiveHealth>> query,
            ref State state)
        {
            foreach (ref var count in query)
            {
                count.Count++;
            }
        }

        // Multi-component: reads both Health and Mana for changed Health entities.
        [System, BurstCompile]
        public static void MultiComponentChanged(
            ref Query<ReactiveHealth, Mana, DamageFlash, Changed<ReactiveHealth>> query,
            ref State state)
        {
            foreach (var (hp, mp, flash) in query)
            {
                flash.Get.Intensity = hp.Get.Value + mp.Get.Value;
            }
        }

        // Changed<Mana> — different changed type in same frame.
        [System, BurstCompile]
        public static void CountChangedMana(
            ref Query<ChangedHitCount, Changed<Mana>> query,
            ref State state)
        {
            foreach (ref var count in query)
            {
                count.Count += 100;
            }
        }
    }

    [TestFixture]
    public class ReactivityTests
    {
        [SetUp]
        public void SetUp() => World.DisposeStatic();

        [TearDown]
        public void TearDown() => World.DisposeStatic();

        [Test]
        public void Reactivity_BasicChange_FiresCallback()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 100f;
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChange<ReactiveHealth>(static (in ReactiveHealth h, in Entity ent) =>
            {
                ref var c = ref ent.Get<ReactiveCounter>();
                c.Hits++;
                c.LastValue = h.Value;
            });

            e.Get<ReactiveHealth>().Value = 50f;
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(1, e.Get<ReactiveCounter>().Hits, "callback should fire once");
            Assert.AreEqual(50f, e.Get<ReactiveCounter>().LastValue, "callback should see new value");

            world.Dispose();
        }

        [Test]
        public void Reactivity_NoChange_NoCallback()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 100f;
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChange<ReactiveHealth>(static (in ReactiveHealth h, in Entity ent) =>
            {
                ent.Get<ReactiveCounter>().Hits++;
            });

            // Don't change the value.
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(0, e.Get<ReactiveCounter>().Hits, "no change → no callback");

            world.Dispose();
        }

        [Test]
        public void Reactivity_Bootstrap_NoFalsePositiveOnSubscribe()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 100f;
            systems.OnUpdate(0.016f, 0.016f);

            // Subscribe after the entity already has its value.
            e.OnChange<ReactiveHealth>(static (in ReactiveHealth h, in Entity ent) =>
            {
                ent.Get<ReactiveCounter>().Hits++;
            });

            // Next frame: OldValues was bootstrapped, so no false positive.
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(0, e.Get<ReactiveCounter>().Hits, "subscribe should not trigger immediately");

            world.Dispose();
        }

        [Test]
        public void Reactivity_TriggerImmediately_FiresOnSubscribe()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 42f;
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChange<ReactiveHealth>(
                static (in ReactiveHealth h, in Entity ent) => { ent.Get<ReactiveCounter>().Hits++; },
                ReactOptions.TriggerImmediately);

            Assert.AreEqual(1, e.Get<ReactiveCounter>().Hits, "triggerImmediately fires synchronously");

            world.Dispose();
        }

        [Test]
        public void Reactivity_MultipleSubscribers_AllFire()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChange<ReactiveHealth>(static (in ReactiveHealth h, in Entity ent) =>
            {
                ref var c = ref ent.Get<ReactiveCounter>();
                c.Hits += 10;
            });
            e.OnChange<ReactiveHealth>(static (in ReactiveHealth h, in Entity ent) =>
            {
                ref var c = ref ent.Get<ReactiveCounter>();
                c.Hits += 100;
            });

            e.Get<ReactiveHealth>().Value = 5f;
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(110, e.Get<ReactiveCounter>().Hits, "both callbacks should fire");

            world.Dispose();
        }

        [Test]
        public void Reactivity_Unsubscribe_StopsCallbacks()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            long token = e.OnChange<ReactiveHealth>(static (in ReactiveHealth h, in Entity ent) =>
            {
                ent.Get<ReactiveCounter>().Hits++;
            });

            // First change → fires.
            e.Get<ReactiveHealth>().Value = 1f;
            systems.OnUpdate(0.016f, 0.032f);
            Assert.AreEqual(1, e.Get<ReactiveCounter>().Hits, "first change fires");

            // Unsubscribe.
            e.OffChange<ReactiveHealth>(token);

            // Second change → should NOT fire.
            e.Get<ReactiveHealth>().Value = 2f;
            systems.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(1, e.Get<ReactiveCounter>().Hits, "after unsubscribe no callback");

            world.Dispose();
        }

        [Test]
        public void Reactivity_Filter_OnlyMatchingChangesFire()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 50f;
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChange<ReactiveHealth>(
                static (in ReactiveHealth h, in Entity ent) => { ent.Get<ReactiveCounter>().Hits++; },
                static (in ReactiveHealth h) => h.Value <= 0);

            // Change but still positive: filter returns false.
            e.Get<ReactiveHealth>().Value = 10f;
            systems.OnUpdate(0.016f, 0.032f);
            Assert.AreEqual(0, e.Get<ReactiveCounter>().Hits, "filter should block positive value");

            // Change to negative: filter passes.
            e.Get<ReactiveHealth>().Value = -5f;
            systems.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(1, e.Get<ReactiveCounter>().Hits, "filter should pass on negative");

            world.Dispose();
        }

        [Test]
        public void Reactivity_OneShot_AutoUnsubscribes()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChange<ReactiveHealth>(
                static (in ReactiveHealth h, in Entity ent) => { ent.Get<ReactiveCounter>().Hits++; },
                ReactOptions.Once);

            e.Get<ReactiveHealth>().Value = 1f;
            systems.OnUpdate(0.016f, 0.032f);
            Assert.AreEqual(1, e.Get<ReactiveCounter>().Hits, "one-shot fires first time");

            e.Get<ReactiveHealth>().Value = 2f;
            systems.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(1, e.Get<ReactiveCounter>().Hits, "one-shot does not fire again");

            world.Dispose();
        }

        [Test]
        public void Reactivity_AutoCleanup_DeadEntityNoStaleCallback()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            int hits = 0;
            e.OnChange<ReactiveHealth>((in ReactiveHealth h, in Entity ent) => hits++);

            // Destroy the entity.
            e.Destroy();
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(0, hits, "destroyed entity should not fire callback");

            world.Dispose();
        }

        [Test]
        public void Reactivity_MultiWorld_Isolated()
        {
            var worldA = World.Create(WorldConfig.Default256);
            var worldB = World.Create(WorldConfig.Default256);
            var systemsA = new Systems(ref worldA).AddDefaults();
            var systemsB = new Systems(ref worldB).AddDefaults();

            var arch = worldA.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var eA = ref arch.CreateEntity();
            systemsA.OnUpdate(0.016f, 0.016f);
            systemsB.OnUpdate(0.016f, 0.016f);

            int hitsA = 0;
            eA.OnChange<ReactiveHealth>((in ReactiveHealth h, in Entity ent) => hitsA++);

            eA.Get<ReactiveHealth>().Value = 1f;
            // Run only worldB — should not affect worldA subscriptions.
            systemsB.OnUpdate(0.016f, 0.032f);
            Assert.AreEqual(0, hitsA, "world B tick should not fire world A subscriptions");

            systemsA.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(1, hitsA, "world A tick should fire its subscription");

            worldA.Dispose();
            worldB.Dispose();
        }

        [Test]
        public void Reactivity_ExplicitAddReactive_Works()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddReactive<ReactiveHealth>();
            systems.AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 1f;
            systems.OnUpdate(0.016f, 0.016f);

            int hits = 0;
            e.OnChange<ReactiveHealth>((in ReactiveHealth h, in Entity ent) => hits++);

            e.Get<ReactiveHealth>().Value = 2f;
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(1, hits, "AddReactive should register systems that fire callbacks");

            world.Dispose();
        }

        [Test]
        public void Reactivity_RepeatedChanges_AllFire()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChange<ReactiveHealth>(static (in ReactiveHealth h, in Entity ent) =>
            {
                ent.Get<ReactiveCounter>().Hits++;
            });

            for (int i = 0; i < 5; i++)
            {
                e.Get<ReactiveHealth>().Value = i + 1;
                systems.OnUpdate(0.016f, 0.016f * (i + 2));
            }

            Assert.AreEqual(5, e.Get<ReactiveCounter>().Hits, "five distinct changes should fire five times");

            world.Dispose();
        }

        [Test]
        public void Reactivity_TriggerImmediately_DeferredAdd_FiresOnNextFrame()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            // Create an entity WITHOUT ReactiveHealth — we'll add it via deferred ECB.
            var arch = world.GetArchetype(typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            int hits = 0;
            float lastValue = -1f;
            // Subscribe BEFORE the component exists — TriggerImmediately must defer.
            e.OnChange<ReactiveHealth>(
                (in ReactiveHealth h, in Entity ent) => { hits++; lastValue = h.Value; },
                ReactOptions.TriggerImmediately);

            // No sync trigger yet — component not on entity.
            Assert.AreEqual(0, hits, "should NOT fire synchronously when T not on entity");

            // Queue the Add via ECB (deferred).
            e.Add<ReactiveHealth>();

            // Playback ECB by running one world update tick.
            world.Update();
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(1, hits, "deferred TriggerImmediately should fire on next frame after ECB");
            Assert.AreEqual(0f, lastValue, "should fire with the initial (default) value");

            // Subsequent real change also fires.
            e.Get<ReactiveHealth>().Value = 42f;
            systems.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(2, hits, "subsequent change should fire");
            Assert.AreEqual(42f, lastValue, "should see new value");

            world.Dispose();
        }

        // ============ Changed<T> query filter tests ============

        [Test]
        public void Reactivity_ChangedQuery_ProcessesOnlyChanged()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ChangedHitCount));
            ref var e1 = ref arch.CreateEntity();
            e1.Get<ReactiveHealth>().Value = 10f;
            ref var e2 = ref arch.CreateEntity();
            e2.Get<ReactiveHealth>().Value = 20f;

            // First frame: bootstrap (entities created, no changes yet).
            systems.Add(ChangedQueryTestSystems.CountChangedHealth);
            systems.OnUpdate(0.016f, 0.016f);

            // Change ONLY e1.
            e1.Get<ReactiveHealth>().Value = 100f;

            // Second frame: _Fetch scans all Health entities, populates ChangedList.
            // CountChangedHealth processes only changed entities.
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(1, e1.Get<ChangedHitCount>().Count, "e1 changed → Count should be 1");
            Assert.AreEqual(0, e2.Get<ChangedHitCount>().Count, "e2 unchanged → Count should be 0");

            // Third frame: no changes → Count should stay same.
            systems.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(1, e1.Get<ChangedHitCount>().Count, "no change → Count stays 1");
            Assert.AreEqual(0, e2.Get<ChangedHitCount>().Count, "no change → Count stays 0");

            world.Dispose();
        }

        [Test]
        public void Reactivity_ChangedQuery_MultiComponent_ReadsBothComponents()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(Mana), typeof(DamageFlash));
            ref var e1 = ref arch.CreateEntity();
            e1.Get<ReactiveHealth>().Value = 50f;
            e1.Get<Mana>().Value = 30f;
            ref var e2 = ref arch.CreateEntity();
            e2.Get<ReactiveHealth>().Value = 80f;
            e2.Get<Mana>().Value = 20f;

            
            systems.Add(ChangedQueryTestSystems.MultiComponentChanged);
            systems.OnUpdate(0.016f, 0.016f); // bootstrap

            // Change only e1's Health. Mana unchanged.
            e1.Get<ReactiveHealth>().Value = 100f;
            systems.OnUpdate(0.016f, 0.032f);

            // MultiComponentChanged should fire on e1 (Health changed).
            // Intensity = Health.Value + Mana.Value = 100 + 30 = 130.
            Assert.AreEqual(130f, e1.Get<DamageFlash>().Intensity, "e1: flash should be hp+mana");
            // e2 unchanged → Intensity stays 0 (default).
            Assert.AreEqual(0f, e2.Get<DamageFlash>().Intensity, "e2: unchanged → no flash");

            world.Dispose();
        }

        [Test]
        public void Reactivity_ChangedQuery_DifferentChangedTypes_SameFrame()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(Mana), typeof(ChangedHitCount));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 10f;
            e.Get<Mana>().Value = 5f;

            // Two systems with different Changed<T> types.
            systems.Add(ChangedQueryTestSystems.CountChangedHealth);  // Changed<ReactiveHealth>
            systems.Add(ChangedQueryTestSystems.CountChangedMana);    // Changed<Mana>
            systems.OnUpdate(0.016f, 0.016f); // bootstrap

            // Change BOTH Health and Mana.
            e.Get<ReactiveHealth>().Value = 99f;
            e.Get<Mana>().Value = 77f;
            systems.OnUpdate(0.016f, 0.032f);

            // CountChangedHealth → Count += 1
            // CountChangedMana → Count += 100
            // Both systems fire on same entity (different Changed<T> → independent ChangedLists).
            Assert.AreEqual(101, e.Get<ChangedHitCount>().Count, "both changed systems should fire: 1 + 100 = 101");

            // Next frame: no changes → neither fires.
            systems.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(101, e.Get<ChangedHitCount>().Count, "no changes → Count stays 101");

            world.Dispose();
        }

        [Test]
        public void Reactivity_ChangedQuery_MultipleEntities_PartialChange()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ChangedHitCount));
            var entities = new Entity[10];
            for (int i = 0; i < 10; i++)
            {
                ref var e = ref arch.CreateEntity();
                e.Get<ReactiveHealth>().Value = i;
                entities[i] = e;
            }

            systems.Add(ChangedQueryTestSystems.CountChangedHealth);
            systems.OnUpdate(0.016f, 0.016f); // bootstrap

            // Change only odd-indexed entities.
            for (int i = 1; i < 10; i += 2)
                entities[i].Get<ReactiveHealth>().Value = 1000 + i;

            systems.OnUpdate(0.016f, 0.032f);

            for (int i = 0; i < 10; i++)
            {
                if (i % 2 == 1)
                    Assert.AreEqual(1, entities[i].Get<ChangedHitCount>().Count, $"entity {i} changed → Count 1");
                else
                    Assert.AreEqual(0, entities[i].Get<ChangedHitCount>().Count, $"entity {i} unchanged → Count 0");
            }

            world.Dispose();
        }

        [Test]
        public void Reactivity_ChangedQuery_RepeatedChanges_AllDetected()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ChangedHitCount));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 0;

            systems.Add(ChangedQueryTestSystems.CountChangedHealth);
            systems.OnUpdate(0.016f, 0.016f); // bootstrap

            for (int i = 0; i < 5; i++)
            {
                e.Get<ReactiveHealth>().Value = i + 1;
                systems.OnUpdate(0.016f, 0.016f * (i + 2));
            }

            Assert.AreEqual(5, e.Get<ChangedHitCount>().Count, "5 distinct changes → Count 5");

            world.Dispose();
        }

        [Test]
        public void Reactivity_ChangedQuery_OnlyChangedType_TriggersCorrectSystem()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(Mana), typeof(ChangedHitCount));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 10f;
            e.Get<Mana>().Value = 20f;

            systems.Add(ChangedQueryTestSystems.CountChangedHealth);
            systems.Add(ChangedQueryTestSystems.CountChangedMana);
            systems.OnUpdate(0.016f, 0.016f); // bootstrap

            // Change ONLY Health, NOT Mana.
            e.Get<ReactiveHealth>().Value = 50f;
            systems.OnUpdate(0.016f, 0.032f);

            // CountChangedHealth fires (+1), CountChangedMana does NOT (+0).
            Assert.AreEqual(1, e.Get<ChangedHitCount>().Count, "only Health changed → Count 1");

            // Next frame: change ONLY Mana, NOT Health.
            e.Get<Mana>().Value = 99f;
            systems.OnUpdate(0.016f, 0.048f);

            // CountChangedMana fires (+100).
            Assert.AreEqual(101, e.Get<ChangedHitCount>().Count, "only Mana changed → Count 1+100=101");

            world.Dispose();
        }
    }
}
