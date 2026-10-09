using NUnit.Framework;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs.Tests
{
    public struct ResourceLifecycleValue : IRes
    {
        public int Creates, Value, WorldId;
        public void OnCreate(ref World world) { Creates++; Value += 10; WorldId = world.Id; }
        public void OnUpdate(ref World world) { }
    }
    public struct ResourceWithBool : IRes
    {
        public bool Flag; // non-blittable field: Res<T> must stay blittable for Burst direct calls
        public int Value;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }
    public struct LocalWithBool : IRes
    {
        public bool Flag;
        public int Value;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }
    public class ManagedLifecycleValue : IRes
    {
        public int Creates, Value, WorldId;
        public void OnCreate(ref World world) { Creates++; Value += 10; WorldId = world.Id; }
        public void OnUpdate(ref World world) { }
    }
    public struct SingletonLifecycleValue : IInit, System.IDisposable
    {
        // SharedStatic: Dispose runs inside the Burst-compiled reset function pointer
        private struct DisposedKey { }
        private static readonly Unity.Burst.SharedStatic<int> disposed =
            Unity.Burst.SharedStatic<int>.GetOrCreate<DisposedKey>();
        public static int Disposed { get => disposed.Data; set => disposed.Data = value; }
        public int Inits, Value;
        public long Padding0, Padding1;
        public void Init() { Inits++; Value = 7; }
        public void Dispose() { disposed.Data++; }
    }
    public static partial class ResourceLifecycleSystems
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

        [Test] public void Singleton_CreatesOnceAndDisposesOnReset()
        {
            SingletonLifecycleValue.Disposed = 0;
            Assert.IsFalse(Singleton<SingletonLifecycleValue>.IsCreated);
            ref var value = ref Singleton<SingletonLifecycleValue>.Instance;
            Assert.AreEqual(1, value.Inits);
            Assert.AreEqual(7, value.Value);
            value.Value = 42;
            Assert.AreEqual(42, Singleton<SingletonLifecycleValue>.Instance.Value, "Instance must return the same storage.");
            Assert.AreEqual(1, Singleton<SingletonLifecycleValue>.Instance.Inits);
            SingletonRegistry.ResetAll();
            Assert.AreEqual(1, SingletonLifecycleValue.Disposed);
            Assert.IsFalse(Singleton<SingletonLifecycleValue>.IsCreated);
            Assert.AreEqual(7, Singleton<SingletonLifecycleValue>.Instance.Value, "Recreated after reset.");
        }

        [Test] public void Singleton_SetValueIsNotInitializedOrDisposed()
        {
            SingletonLifecycleValue.Disposed = 0;
            var supplied = new SingletonLifecycleValue { Value = 3 };
            Singleton<SingletonLifecycleValue>.Set(ref supplied);
            Assert.AreEqual(0, Singleton<SingletonLifecycleValue>.Instance.Inits);
            Assert.AreEqual(3, Singleton<SingletonLifecycleValue>.Instance.Value);
            SingletonRegistry.ResetAll();
            Assert.AreEqual(0, SingletonLifecycleValue.Disposed);
            Assert.IsFalse(Singleton<SingletonLifecycleValue>.IsCreated);
        }

        [Test] public void Res_LiveValueIsKeptAcrossLoad()
        {
            // resources are runtime state (native containers): a load keeps the live values
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new ResourceWithBool { Flag = false, Value = 1 });
            var data = world.Serialize();
            world.GetRes<ResourceWithBool>().Flag = true;
            world.GetRes<ResourceWithBool>().Value = 2;
            world.Deserialize(data);
            Assert.IsTrue(world.GetRes<ResourceWithBool>().Flag);
            Assert.AreEqual(2, world.GetRes<ResourceWithBool>().Value, "The saved value must not replace the live one.");
            var param = world.UnsafeWorld->GetSystemParam2<Res<ResourceWithBool>>();
            Assert.AreEqual(2, param.Ref.Ref.Value, "System param must resolve the restored live block.");
        }

        [Test] public void Res_SavedOnlyResourceIsRecreatedOnRequest()
        {
            var source = World.Create(WorldConfig.Default256);
            source.AddRes(new ResourceLifecycleValue { Value = 5 });
            var data = source.Serialize();
            source.Dispose();
            var world = World.Create(WorldConfig.Default16);
            world.Deserialize(data);
            Assert.IsFalse(world.HasRes<ResourceLifecycleValue>(), "Saved resource values are not restored.");
            var resource = world.UnsafeWorld->GetSystemParam2<Res<ResourceLifecycleValue>>();
            Assert.AreEqual(1, resource.Ref.Ref.Creates);
            Assert.AreEqual(10, resource.Ref.Ref.Value, "Recreated through OnCreate from default.");
        }

        [Test] public void Local_ResolvedByStableKeyAfterSessionReset()
        {
            const ulong owner = 0xC0FFEE123UL;
            HashMap<ulong, int> registrations = default;
            try
            {
                var source = World.Create(WorldConfig.Default256);
                var local = source.UnsafeWorld->CreateLocalSystemParam<Local<LocalWithBool>>(owner, 3, ref registrations, out _);
                local.Ref.Ref.Flag = true;
                local.Ref.Ref.Value = 55;
                var data = source.Serialize();
                source.Dispose();
                registrations.Dispose();
                registrations = default;

                // simulate another session: slot ids restart and are handed out in a different order
                LocalParamSlots.Dispose();
                HashMap<ulong, int> other = default;
                try { source = World.Create(WorldConfig.Default16);
                      source.UnsafeWorld->CreateLocalSystemParam<Local<LocalWithBool>>(owner + 1, 3, ref other, out _);
                      source.Dispose(); }
                finally { if (other.IsCreated) other.Dispose(); }

                var world = World.Create(WorldConfig.Default16);
                world.Deserialize(data);
                var restored = world.UnsafeWorld->CreateLocalSystemParam<Local<LocalWithBool>>(owner, 3, ref registrations, out _);
                Assert.IsTrue(restored.Ref.Ref.Flag);
                Assert.AreEqual(55, restored.Ref.Ref.Value, "Local must be found by its stable key, not the session slot id.");
            }
            finally { if (registrations.IsCreated) registrations.Dispose(); }
        }

        [Test] public void StableTypeHash_IgnoresAssemblyInfo()
        {
            Assert.AreEqual(StableTypeHash<Res<ResourceWithBool>>.Value, StableTypeHash.Compute(typeof(Res<ResourceWithBool>)));
            Assert.AreNotEqual(StableTypeHash<Res<ResourceWithBool>>.Value, StableTypeHash<Res<ResourceLifecycleValue>>.Value);
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
