using NUnit.Framework;
using Unity.Burst;
using Wargon.Nukecs.Reactivity;

namespace Wargon.Nukecs.Tests
{
    public struct ReactiveHealth : IComponent
    {
        public float Value;
    }

    public struct ReactiveCounter : IComponent
    {
        public int Hits;
        public float LastValue;
    }

    [TestFixture]
    public class ReactivityTests
    {
        // Static state for Burst callbacks (they cannot capture closures).
        // Reset in SetUp so each test starts clean.
        private static int s_burstHits;
        private static float s_burstLastValue;
        private static int s_burstFilterHits;
        private static int s_worldBurstHits;

        [SetUp]
        public void SetUp()
        {
            World.DisposeStatic();
            s_burstHits = 0;
            s_burstLastValue = -1f;
            s_burstFilterHits = 0;
            s_worldBurstHits = 0;
        }

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

            // Run one frame so systems exist & component is committed.
            systems.OnUpdate(0.016f, 0.016f);

            long token = e.OnChange<ReactiveHealth>(static (in ReactiveHealth h, in Entity ent) =>
            {
                ref var c = ref ent.Get<ReactiveCounter>();
                c.Hits++;
                c.LastValue = h.Value;
            });

            // Change the value.
            e.Get<ReactiveHealth>().Value = 50f;

            // Next frame: check detects → dispatch fires.
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
        public void Reactivity_WorldLevel_FiresForAnyEntity()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e1 = ref arch.CreateEntity();
            ref var e2 = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            int worldHits = 0;
            world.OnChange<ReactiveHealth>((in ReactiveHealth h, in Entity ent) => worldHits++);

            e1.Get<ReactiveHealth>().Value = 1f;
            e2.Get<ReactiveHealth>().Value = 2f;
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(2, worldHits, "world-level callback should fire for both entities");

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
            e.OnChange((in ReactiveHealth h, in Entity ent) => hits++);

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

        // ============ Burst callback tests ============
        //
        // Burst callbacks must be static methods with [BurstCompile] and
        // [AOT.MonoPInvokeCallback(typeof(ReactDelegate<T>))]. State is shared
        // through static fields (closures are not allowed).

        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(ReactDelegateBurst))]
        public static void BurstHealthCallback(in Entity ent)
        {
            ref var h = ref ent.Get<ReactiveHealth>();
            s_burstHits++;
            s_burstLastValue = h.Value;
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(ReactFilterBurst))]
        public static bool BurstHealthFilter(in Entity ent)
        {
            // Pass only when value drops below 10.
            return ent.Get<ReactiveHealth>().Value < 10f;
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(ReactDelegateBurst))]
        public static void BurstWorldHealthCallback(in Entity ent)
        {
            s_worldBurstHits++;
        }

        [Test]
        public void Reactivity_BurstCallback_FiresOnChange()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 100f;
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChangeBurst<ReactiveHealth>((in Entity ent) =>
            {
                ref var h = ref ent.Get<ReactiveHealth>();
                s_burstHits++;
                s_burstLastValue = h.Value;
            });

            e.Get<ReactiveHealth>().Value = 25f;
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(1, s_burstHits, "burst callback should fire once");
            Assert.AreEqual(25f, s_burstLastValue, "burst callback should see new value");

            world.Dispose();
        }

        [Test]
        public void Reactivity_BurstCallback_NoChange_NoFire()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 100f;
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChangeBurst<ReactiveHealth>(BurstHealthCallback);

            // Don't change.
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(0, s_burstHits, "no change → no burst callback");

            world.Dispose();
        }

        [Test]
        public void Reactivity_BurstCallback_Filter_OnlyMatchingChangesFire()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 100f;
            systems.OnUpdate(0.016f, 0.016f);

            // Filter passes only when Value < 10.
            e.OnChangeBurst<ReactiveHealth>(BurstHealthCallback, BurstHealthFilter);

            // Change but still above 10 — filter blocks.
            e.Get<ReactiveHealth>().Value = 50f;
            systems.OnUpdate(0.016f, 0.032f);
            Assert.AreEqual(0, s_burstHits, "filter should block change above 10");

            // Change below 10 — filter passes.
            e.Get<ReactiveHealth>().Value = 5f;
            systems.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(1, s_burstHits, "filter should pass change below 10");
            Assert.AreEqual(5f, s_burstLastValue, "burst callback should see new value");

            world.Dispose();
        }

        [Test]
        public void Reactivity_BurstCallback_TriggerImmediately_FiresSync()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth));
            ref var e = ref arch.CreateEntity();
            e.Get<ReactiveHealth>().Value = 77f;
            systems.OnUpdate(0.016f, 0.016f);

            e.OnChangeBurst<ReactiveHealth>(BurstHealthCallback, ReactOptions.TriggerImmediately);

            // Sync trigger should fire immediately with current value.
            Assert.AreEqual(1, s_burstHits, "burst TriggerImmediately should fire synchronously");
            Assert.AreEqual(77f, s_burstLastValue, "should see current value at subscribe time");

            world.Dispose();
        }

        [Test]
        public void Reactivity_BurstCallback_OffChange_StopsCallbacks()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth));
            ref var e = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            long token = e.OnChangeBurst<ReactiveHealth>(BurstHealthCallback);

            e.Get<ReactiveHealth>().Value = 1f;
            systems.OnUpdate(0.016f, 0.032f);
            Assert.AreEqual(1, s_burstHits, "first change fires burst callback");

            e.OffChange<ReactiveHealth>(token);

            e.Get<ReactiveHealth>().Value = 2f;
            systems.OnUpdate(0.016f, 0.048f);
            Assert.AreEqual(1, s_burstHits, "after OffChange no burst callback");

            world.Dispose();
        }

        [Test]
        public void Reactivity_WorldLevel_BurstCallback_FiresForAnyEntity()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth));
            ref var e1 = ref arch.CreateEntity();
            ref var e2 = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            world.OnChangeBurst<ReactiveHealth>(BurstWorldHealthCallback);

            e1.Get<ReactiveHealth>().Value = 1f;
            e2.Get<ReactiveHealth>().Value = 2f;
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(2, s_worldBurstHits, "world-level burst callback should fire for both entities");

            world.Dispose();
        }

        [Test]
        public void Reactivity_MixedManagedAndBurst_BothFire()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).AddDefaults();

            var arch = world.GetArchetype(typeof(ReactiveHealth), typeof(ReactiveCounter));
            ref var e = ref arch.CreateEntity();
            systems.OnUpdate(0.016f, 0.016f);

            int managedHits = 0;
            // Per-entity managed
            e.OnChange<ReactiveHealth>((in ReactiveHealth h, in Entity ent) => managedHits++);
            // Per-entity burst — uses static counter
            e.OnChangeBurst<ReactiveHealth>(BurstHealthCallback);

            e.Get<ReactiveHealth>().Value = 9f;
            systems.OnUpdate(0.016f, 0.032f);

            Assert.AreEqual(1, managedHits, "managed callback should fire");
            Assert.AreEqual(1, s_burstHits, "burst callback should fire on same change");

            world.Dispose();
        }
    }
}
