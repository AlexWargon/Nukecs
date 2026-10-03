using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs.Tests
{
    [TestFixture]
    public unsafe class WorldIoRequestTests
    {
        private string directory;
        private string SavePath => Path.Combine(directory, "world.dat");
        [SetUp] public void SetUp()
        {
            World.DisposeStatic();
            directory = Path.Combine(Path.GetTempPath(), "nukecs-io-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
        }
        [TearDown] public void TearDown()
        {
            World.DisposeStatic();
            Directory.Delete(directory, true);
        }

        private sealed class ActionRunner : ISystemRunner, IThreadModeProvider
        {
            public Action<World> Action;
            public Threads Mode => Threads.MainRun;
            public string Name => nameof(ActionRunner);
            public JobHandle Schedule(UpdateContext context, ref State state)
            { Action(state.World); return state.Dependencies; }
            public void Run(ref State state) => Action(state.World);
        }
        private static void Add(Systems systems, Action<World> action)
            => _systems_internal.add_on_update(systems, new ActionRunner { Action = action });

        [Test] public void Save_IsDeferredAndCapturesBeforeSystemsRun()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new LoadHealth { Value = 10 });
            world.Update();
            var systems = new Systems(ref world);
            Add(systems, _ => entity.Get<LoadHealth>().Value = 20);
            var request = world.RequestSave(SavePath);
            Assert.IsFalse(request.IsCompleted);
            Assert.IsFalse(File.Exists(SavePath));
            systems.OnUpdate(0.016f, 0);
            Assert.AreEqual(TaskStatus.RanToCompletion, request.Status);
            Assert.AreEqual(20, entity.Get<LoadHealth>().Value);
            world.Load(SavePath);
            Assert.AreEqual(10, entity.Get<LoadHealth>().Value);
        }

        [TestCase(-1)] [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Load_RebindsSystemsBeforeTheirNextPass(int mode)
        {
            var source = World.Create(WorldConfig.Default1024);
            var saved = source.Entity(new LoadHealth { Value = 42 });
            source.Update(); source.Save(SavePath); source.Dispose();
            var world = World.Create(WorldConfig.Default16);
            var id = world.Id;
            var systems = new Systems(ref world).Add<LoadedPlayerLinkSystem>();
            if (mode >= 0) systems.UseDependencyGraph(mode: (GroupScheduleMode)mode);
            var link = ((SystemMainThreadRunnerClass<LoadedPlayerLinkSystem>)systems.Runners[0]).System;
            var request = world.RequestLoad(SavePath);
            Assert.IsFalse(saved.IsValid());
            systems.OnUpdate(0.016f, 0);
            Assert.AreEqual(TaskStatus.RanToCompletion, request.Status);
            Assert.AreEqual(1, link.Loads);
            Assert.AreEqual(42, link.Player.Get<LoadHealth>().Value);
            Assert.IsTrue(systems.World.UnsafeWorld == World.Get(id).UnsafeWorld);
            Assert.IsTrue(request.Result.UnsafeWorld == systems.World.UnsafeWorld);
        }

        [Test] public void RequestIssuedBySystem_WaitsUntilNextPrimaryPass()
        {
            var world = World.Create(WorldConfig.Default256);
            var first = new Systems(ref world);
            var second = new Systems(ref world);
            Task request = null;
            Add(first, w => { if (request == null) request = w.RequestSave(SavePath); });
            first.OnUpdate(0.016f, 0);
            second.OnUpdate(0.016f, 0);
            Assert.IsFalse(request.IsCompleted);
            first.OnUpdate(0.016f, 0.016f);
            Assert.AreEqual(TaskStatus.RanToCompletion, request.Status);
        }

        [Test] public void Requests_RunInOrderAndContinueAfterIoFailure()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new LoadHealth { Value = 10 });
            world.Update(); world.Save(SavePath);
            entity.Get<LoadHealth>().Value = 20;
            var systems = new Systems(ref world);
            var failed = world.RequestLoad(Path.Combine(directory, "missing.dat"));
            var id = world.Id;
            var load = world.RequestLoad(SavePath);
            var copyPath = Path.Combine(directory, "copy.dat");
            var save = world.RequestSave(copyPath);
            systems.OnUpdate(0.016f, 0);
            Assert.IsTrue(failed.IsFaulted);
            Assert.IsInstanceOf<FileNotFoundException>(failed.Exception.InnerException);
            Assert.AreEqual(TaskStatus.RanToCompletion, load.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, save.Status);
            entity.Get<LoadHealth>().Value = 30;
            world = World.Get(id);
            world.Load(copyPath);
            Assert.AreEqual(10, entity.Get<LoadHealth>().Value);
        }

        [Test] public void Queues_ArePerWorldAndCancelOnDispose()
        {
            var a = World.Create(WorldConfig.Default256);
            var b = World.Create(WorldConfig.Default256);
            var sa = new Systems(ref a);
            var sb = new Systems(ref b);
            var saved = a.RequestSave(SavePath);
            var canceled = b.RequestSave(Path.Combine(directory, "b.dat"));
            sa.OnUpdate(0.016f, 0);
            Assert.AreEqual(TaskStatus.RanToCompletion, saved.Status);
            Assert.IsFalse(canceled.IsCompleted);
            b.Dispose();
            Assert.IsTrue(canceled.IsCanceled);
            var replacement = World.Create(WorldConfig.Default256);
            new Systems(ref replacement).OnUpdate(0.016f, 0);
            Assert.IsFalse(File.Exists(Path.Combine(directory, "b.dat")));
        }

        private struct SlowWrite : IJob
        {
            [NativeDisableUnsafePtrRestriction] public LoadHealth* Value;
            public int Delay;
            public void Execute() { Thread.Sleep(Delay); Value->Value = 77; }
        }

        public sealed class CallbackRequest : ISystem, IOnWorldDeserialize
        {
            public string Path;
            public Task Request;
            public void OnUpdate(ref State state) { }
            public void OnWorldDeserialize(ref World world) => Request = world.RequestSave(Path);
        }

        [Test] public void LoadCallbackRequest_IsDeferredUntilNextPass()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world).Add<CallbackRequest>();
            var callback = ((SystemMainThreadRunnerClass<CallbackRequest>)systems.Runners[0]).System;
            callback.Path = Path.Combine(directory, "callback.dat");
            world.Save(SavePath);
            var load = world.RequestLoad(SavePath);
            systems.OnUpdate(0.016f, 0);
            Assert.AreEqual(TaskStatus.RanToCompletion, load.Status);
            Assert.IsFalse(callback.Request.IsCompleted);
            systems.OnUpdate(0.016f, 0.016f);
            Assert.AreEqual(TaskStatus.RanToCompletion, callback.Request.Status);
        }

        [Test] public void StaticDispose_CancelsPendingRequests()
        {
            var world = World.Create(WorldConfig.Default256);
            var request = world.RequestSave(SavePath);
            World.DisposeStatic();
            Assert.IsTrue(request.IsCanceled);
            Assert.IsFalse(File.Exists(SavePath));
        }
        private sealed class Writer : ISystemRunner, IThreadModeProvider
        {
            public LoadHealth* Value;
            public bool Independent;
            public int Delay = 100;
            public Threads Mode => Threads.Single;
            public string Name => nameof(Writer);
            public JobHandle Schedule(UpdateContext context, ref State state)
                => new SlowWrite { Value = Value, Delay = Delay }.Schedule(Independent ? default : state.Dependencies);
            public void Run(ref State state) => new SlowWrite { Value = Value, Delay = Delay }.Execute();
        }

        [Test] public void ImmediateSave_WaitsForIndependentScheduledBranches()
        {
            var world = World.Create(WorldConfig.Default256);
            var first = world.Entity(new LoadHealth { Value = 1 });
            var second = world.Entity(new LoadHealth { Value = 2 });
            world.Update();
            var systems = new Systems(ref world);
            _systems_internal.add_on_update(systems, new Writer { Value = (LoadHealth*)UnsafeUtility.AddressOf(ref first.Get<LoadHealth>()), Delay = 150 });
            _systems_internal.add_on_update(systems, new Writer { Value = (LoadHealth*)UnsafeUtility.AddressOf(ref second.Get<LoadHealth>()), Independent = true, Delay = 0 });
            Add(systems, w => w.Save(SavePath));
            systems.OnUpdate(0.016f, 0);
            first.Get<LoadHealth>().Value = second.Get<LoadHealth>().Value = 0;
            world.Load(SavePath);
            Assert.AreEqual(77, first.Get<LoadHealth>().Value);
            Assert.AreEqual(77, second.Get<LoadHealth>().Value);
        }

        [TestCase(false)] [TestCase(true)]
        public void ImmediateSave_CompletesActualJobInsideMainRun(bool file)
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new LoadHealth { Value = 1 });
            world.Update();
            var systems = new Systems(ref world);
            _systems_internal.add_on_update(systems, new Writer { Value = (LoadHealth*)UnsafeUtility.AddressOf(ref entity.Get<LoadHealth>()) });
            byte[] snapshot = null;
            Add(systems, w => { if (file) w.Save(SavePath); else snapshot = w.Serialize(); });
            systems.OnUpdate(0.016f, 0);
            entity.Get<LoadHealth>().Value = 2;
            if (file) world.Load(SavePath); else world.Deserialize(snapshot);
            Assert.AreEqual(77, entity.Get<LoadHealth>().Value, "Saving inside MainRun must wait for the preceding scheduled writer.");
        }

        [TestCase(false)] [TestCase(true)]
        public void ImmediateLoad_WaitsBeforeOverwritingArena(bool file)
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity(new LoadHealth { Value = 1 });
            world.Update();
            var snapshot = world.Serialize();
            if (file) world.Save(SavePath);
            var systems = new Systems(ref world);
            _systems_internal.add_on_update(systems, new Writer { Value = (LoadHealth*)UnsafeUtility.AddressOf(ref entity.Get<LoadHealth>()) });
            Add(systems, w => { if (file) w.Load(SavePath); else w.Deserialize(snapshot); });
            systems.OnUpdate(0.016f, 0);
            Assert.AreEqual(1, entity.Get<LoadHealth>().Value, "An unfinished writer must not overwrite loaded data.");
        }
    }
}
