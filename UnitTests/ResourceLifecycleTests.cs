using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct ResourceLifecycleValue : IRes
    {
        public int Creates, Value, WorldId;
        public void OnCreate(ref World world) { Creates++; Value += 10; WorldId = world.Id; }
        public void OnUpdate(ref World world) { }
    }
    public class ManagedLifecycleValue : IRes
    {
        public int Creates, Value, WorldId;
        public void OnCreate(ref World world) { Creates++; Value += 10; WorldId = world.Id; }
        public void OnUpdate(ref World world) { }
    }
    public static class ResourceLifecycleSystems
    {
        [System] public static void Value(ref Res<ResourceLifecycleValue> resource) { }
        [System] public static void Managed(ref ResManaged<ManagedLifecycleValue> resource) { }
    }

    [TestFixture]
    public unsafe class ResourceLifecycleTests
    {
        [SetUp] public void SetUp() => World.DisposeStatic();
        [TearDown] public void TearDown() => World.DisposeStatic();

        [Test] public void AddRes_InitializesSuppliedValueOnceBeforeSystemRegistration()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new ResourceLifecycleValue { Value = 5 });
            var resource = world.UnsafeWorld->GetSystemParam2<Res<ResourceLifecycleValue>>();
            Assert.AreEqual(1, resource.Ref.Ref.Creates);
            Assert.AreEqual(15, resource.Ref.Ref.Value);
            Assert.AreEqual(world.Id, resource.Ref.Ref.WorldId);
            world.AddRes(new ResourceLifecycleValue { Value = 999 });
            var systems = new Systems(ref world);
            systems.Add(ResourceLifecycleSystems.Value, Threads.MainRun);
            systems.Add(ResourceLifecycleSystems.Value, Threads.MainRun);
            systems.OnUpdate(0.016f, 0);
            Assert.AreEqual(1, resource.Ref.Ref.Creates);
            Assert.AreEqual(15, resource.Ref.Ref.Value, "Repeated registration must not replace the initialized value.");
        }

        [Test] public void AddResManaged_InitializesSuppliedInstanceOnceBeforeSystemRegistration()
        {
            var world = World.Create(WorldConfig.Default256);
            var value = new ManagedLifecycleValue { Value = 5 };
            world.AddResManaged(value);
            Assert.AreEqual(1, value.Creates);
            Assert.AreEqual(15, value.Value);
            Assert.AreEqual(world.Id, value.WorldId);
            var rejected = new ManagedLifecycleValue { Value = 999 };
            world.AddResManaged(rejected);
            var systems = new Systems(ref world);
            systems.Add(ResourceLifecycleSystems.Managed, Threads.MainRun);
            systems.Add(ResourceLifecycleSystems.Managed, Threads.MainRun);
            systems.OnUpdate(0.016f, 0);
            var resource = world.UnsafeWorld->GetSystemParam2<ResManaged<ManagedLifecycleValue>>();
            Assert.AreSame(value, resource.Ref.Val);
            Assert.AreEqual(1, value.Creates);
            Assert.AreEqual(0, rejected.Creates);
        }

        [Test] public void Res_ValuesAreIsolatedPerWorld()
        {
            var a = World.Create(WorldConfig.Default256);
            var b = World.Create(WorldConfig.Default256);
            a.AddRes(new ResourceLifecycleValue { Value = 1 });
            b.AddRes(new ResourceLifecycleValue { Value = 2 });
            Assert.AreEqual(11, a.GetRes<ResourceLifecycleValue>().Value);
            Assert.AreEqual(12, b.GetRes<ResourceLifecycleValue>().Value);
            Assert.AreEqual(a.Id, a.GetRes<ResourceLifecycleValue>().WorldId);
            Assert.AreEqual(b.Id, b.GetRes<ResourceLifecycleValue>().WorldId);
            a.GetRes<ResourceLifecycleValue>().Value = 100;
            Assert.AreEqual(12, b.GetRes<ResourceLifecycleValue>().Value, "Writing world A must not touch world B.");
        }

        [Test] public void GetRes_SharesSlotWithSystemParam()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ResourceLifecycleSystems.Value, Threads.MainRun);
            var resource = world.UnsafeWorld->GetSystemParam2<Res<ResourceLifecycleValue>>();
            Assert.IsTrue(world.HasRes<ResourceLifecycleValue>());
            world.GetRes<ResourceLifecycleValue>().Value = 42;
            Assert.AreEqual(42, resource.Ref.Ref.Value);
        }

        [Test] public void GetRes_ThrowsWhenWorldHasNoResource()
        {
            var a = World.Create(WorldConfig.Default256);
            var b = World.Create(WorldConfig.Default256);
            a.AddRes(new ResourceLifecycleValue());
            Assert.IsFalse(b.HasRes<ResourceLifecycleValue>());
            Assert.Throws<System.InvalidOperationException>(() => b.GetRes<ResourceLifecycleValue>());
        }

        [Test] public void AutomaticRegistration_StillInitializesOnce()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ResourceLifecycleSystems.Managed, Threads.MainRun);
            systems.Add(ResourceLifecycleSystems.Managed, Threads.MainRun);
            var resource = world.UnsafeWorld->GetSystemParam2<ResManaged<ManagedLifecycleValue>>();
            Assert.AreEqual(1, resource.Ref.Val.Creates);
            Assert.AreEqual(10, resource.Ref.Val.Value);
            var rejected = new ManagedLifecycleValue();
            world.AddResManaged(rejected);
            Assert.AreSame(resource.Ref.Val, world.UnsafeWorld->GetSystemParam2<ResManaged<ManagedLifecycleValue>>().Ref.Val);
            Assert.AreEqual(0, rejected.Creates);
        }
    }
}
