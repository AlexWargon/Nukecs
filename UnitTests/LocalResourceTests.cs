using System.Threading;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs.Tests
{
    public struct LocalCounter : IRes
    {
        public int Value;
        public int Creates;
        public int Updates;
        public void OnCreate(ref World world) { Creates++; Value = 10; }
        public void OnUpdate(ref World world) { Updates++; }
    }
    public struct LocalVisited : IComponent { public int Value; }

    public static partial class LocalSystems
    {
        [System, BurstCompile]
        public static void Increment(ref Local<LocalCounter> local) { local.Ref.Value++; }

        [System, BurstCompile]
        public static void IncrementTen(ref Local<LocalCounter> local) { local.Ref.Value += 10; }

        [System, BurstCompile]
        public static void TwoLocals(ref Local<LocalCounter> left, ref Local<LocalCounter> right)
        {
            left.Ref.Value++;
            right.Ref.Value += 10;
        }

        [System, BurstCompile, RequireBatch]
        public static void Visit(ref Query<Entity, LocalVisited> query, ref Local<LocalCounter> local)
        {
            foreach (var (entity, visited) in query) {
                visited.Get.Value++;
                Interlocked.Increment(ref local.Ref.Value);
            }
        }
    }

    [TestFixture, BurstCompile]
    public unsafe class LocalResourceTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RegisterLocalDelegate(World.WorldUnsafe* world, HashMap<ulong, int>* registrations, int* result);

        [BurstDiscard]
        private static void MarkManaged(ref bool native) => native = false;

        [BurstCompile(CompileSynchronously = true)]
        private static void RegisterLocalNative(World.WorldUnsafe* world, HashMap<ulong, int>* registrations, int* result)
        {
            var native = true;
            MarkManaged(ref native);
            var first = world->CreateLocalSystemParam<Local<LocalCounter>>(0xB0123456789UL, 7, ref *registrations, out var firstSlot);
            var second = world->CreateLocalSystemParam<Local<LocalCounter>>(0xB0123456789UL, 7, ref *registrations, out var secondSlot);
            first.Ref.Ref.Value = 99;
            result[0] = native ? 1 : 0;
            result[1] = firstSlot != secondSlot ? 1 : 0;
            result[2] = second.Ref.Ref.Value;
            result[3] = second.Ref.Ref.Creates;
            result[4] = world->GetLocalSystemParam<Local<LocalCounter>>(firstSlot).Ref.Ref.Value;
        }

        [Test]
        public void RegistrationAndLookup_CompileAndExecuteNativelyInBurst()
        {
            if (!BurstCompiler.Options.IsEnabled) Assert.Ignore("Native Burst is disabled in this Editor session.");
            var world = World.Create(WorldConfig.Default256);
            HashMap<ulong, int> registrations = default;
            var result = stackalloc int[5];
            try {
                BurstCompiler.CompileFunctionPointer<RegisterLocalDelegate>(RegisterLocalNative)
                    .Invoke(world.UnsafeWorld, &registrations, result);
                Assert.AreEqual(1, result[0], "Registration must execute native Burst code.");
                Assert.AreEqual(1, result[1]);
                Assert.AreEqual(10, result[2]);
                Assert.AreEqual(1, result[3]);
                Assert.AreEqual(99, result[4]);
            }
            finally { if (registrations.IsCreated) registrations.Dispose(); }
        }

        [SetUp] public void SetUp() => World.DisposeStatic();
        [TearDown] public void TearDown() => World.DisposeStatic();

        private static LocalCounter Read(ISystemRunner runner, string field = "local")
            => ((ptr<Local<LocalCounter>>)runner.GetType().GetField(field).GetValue(runner)).Ref.Ref;

        [TestCase(Threads.Main)]
        [TestCase(Threads.MainRun)]
        [TestCase(Threads.Single)]
        [TestCase(Threads.Parallel)]
        public void SameType_IsolatedPerMethodAndRegistration(Threads mode)
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).Add(LocalSystems.Increment, mode)
                .Add(LocalSystems.IncrementTen, mode).Add(LocalSystems.Increment, mode);
            systems.OnUpdate(0.01f, 0.01f);
            systems.OnUpdate(0.01f, 0.02f);
            Assert.AreEqual(12, Read(systems.Runners[0]).Value);
            Assert.AreEqual(30, Read(systems.Runners[1]).Value);
            Assert.AreEqual(12, Read(systems.Runners[2]).Value);
            foreach (var runner in systems.Runners) {
                Assert.AreEqual(1, Read(runner).Creates);
                Assert.AreEqual(2, Read(runner).Updates);
            }
        }

        [Test]
        public void TwoParametersOfSameType_HaveSeparateValues()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).Add(LocalSystems.TwoLocals, Threads.MainRun);
            systems.OnUpdate(0.01f, 0.01f);
            Assert.AreEqual(11, Read(systems.Runners[0], "left").Value);
            Assert.AreEqual(20, Read(systems.Runners[0], "right").Value);
        }

        [Test]
        public void RestartAndLoad_RestoresEachRegisteredInstance()
        {
            var source = World.Create(WorldConfig.Default1024);
            var original = new Systems(ref source);
            original.AddSystems((LocalSystems.Increment, Threads.Main), (LocalSystems.IncrementTen, Threads.Main));
            original.OnUpdate(0.01f, 0.01f);
            var saved = source.Serialize();
            World.DisposeStatic();
            var target = World.Create(WorldConfig.Default16);
            var restored = new Systems(ref target);
            restored.AddSystems((LocalSystems.Increment, Threads.Main), (LocalSystems.IncrementTen, Threads.Main));
            target.Deserialize(saved);
            Assert.AreEqual(11, Read(restored.Runners[0]).Value);
            Assert.AreEqual(20, Read(restored.Runners[1]).Value);
            restored.OnUpdate(0.01f, 0.02f);
            Assert.AreEqual(12, Read(restored.Runners[0]).Value);
            Assert.AreEqual(30, Read(restored.Runners[1]).Value);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ParallelQuery_UpdatesLifecycleOnceAndSharesOneValue(int graphMode)
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world).Add(LocalSystems.Visit, Threads.Parallel);
            if (graphMode >= 0) systems.UseDependencyGraph(mode: (GroupScheduleMode)graphMode);
            var entities = new Entity[513];
            for (var i = 0; i < entities.Length; i++) entities[i] = world.Entity<LocalVisited>();
            world.Update();
            systems.OnUpdate(0.01f, 0.01f);
            Assert.AreEqual(10 + entities.Length, Read(systems.Runners[0]).Value);
            Assert.AreEqual(1, Read(systems.Runners[0]).Creates);
            Assert.AreEqual(1, Read(systems.Runners[0]).Updates);
            foreach (var entity in entities) Assert.AreEqual(1, entity.Get<LocalVisited>().Value);
        }

        [Test]
        public void Values_AreIsolatedAcrossWorldsAndSystemsContainers()
        {
            var a = World.Create(WorldConfig.Default256);
            var b = World.Create(WorldConfig.Default256);
            var first = new Systems(ref a).Add(LocalSystems.Increment, Threads.Main);
            var second = new Systems(ref a).Add(LocalSystems.Increment, Threads.Main);
            var otherWorld = new Systems(ref b).Add(LocalSystems.Increment, Threads.Main);
            first.OnUpdate(0.01f, 0.01f);
            Assert.AreEqual(11, Read(first.Runners[0]).Value);
            Assert.AreEqual(10, Read(second.Runners[0]).Value);
            Assert.AreEqual(10, Read(otherWorld.Runners[0]).Value);
        }

        [Test]
        public void EmptyParallelQuery_UpdatesLocalOnce()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).Add(LocalSystems.Visit, Threads.Parallel);
            systems.OnUpdate(0.01f, 0.01f);
            Assert.AreEqual(10, Read(systems.Runners[0]).Value);
            Assert.AreEqual(1, Read(systems.Runners[0]).Updates);
        }

        [Test]
        public void DirectRunnerRun_UpdatesLocalOnce()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).Add(LocalSystems.Increment, Threads.Main);
            var state = new State { World = world };
            systems.Runners[0].Run(ref state);
            Assert.AreEqual(11, Read(systems.Runners[0]).Value);
            Assert.AreEqual(1, Read(systems.Runners[0]).Updates);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_RestoresValueOrInitializesLocalMissingFromSnapshot(bool saveBeforeRegistration)
        {
            var world = World.Create(WorldConfig.Default256);
            var before = world.Serialize();
            var systems = new Systems(ref world).Add(LocalSystems.Increment, Threads.Single);
            systems.OnUpdate(0.01f, 0.01f);
            var saved = saveBeforeRegistration ? before : world.Serialize();
            systems.OnUpdate(0.01f, 0.02f);
            world.Deserialize(saved);
            Assert.AreEqual(saveBeforeRegistration ? 10 : 11, Read(systems.Runners[0]).Value);
            Assert.AreEqual(1, Read(systems.Runners[0]).Creates);
            Assert.AreEqual(saveBeforeRegistration ? 0 : 1, Read(systems.Runners[0]).Updates);
            systems.OnUpdate(0.01f, 0.03f);
            Assert.AreEqual(saveBeforeRegistration ? 11 : 12, Read(systems.Runners[0]).Value);
        }
    }
}
