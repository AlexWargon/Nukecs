using NUnit.Framework;
using Unity.Burst;

namespace Wargon.Nukecs.Tests
{
    public struct BatchValue : IComponent { public float Value; }
    public struct BatchExcluded : IComponent { }
    public struct BatchPoolValue : IPoolComponent { public float Value; }
    public struct BatchEnvelopeEvent { public int Phase, Count; public float Sum; }
    public struct BatchEnvelopeStats : IRes
    {
        public int Prefix, Suffix, Count;
        public float Sum;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }

    public static partial class BatchRewriteSystems
    {
        [System, BurstCompile, RequireBatch]
        public static void Envelope(ref Query<Entity, BatchValue, None<BatchExcluded>> query,
            ref Events<BatchEnvelopeEvent> events, ref State state)
        {
            unsafe
            {
                var delta = state.Time.DeltaTime * 3f;
                var processed = 0;
                var sum = 0f;
                events.AddPar(new BatchEnvelopeEvent { Phase = 1 });
                foreach (var (entity, value) in query)
                {
                    value.Get.Value += delta;
                    processed++;
                    sum += value.Read.Value;
                }
                events.AddPar(new BatchEnvelopeEvent { Phase = 2, Count = processed, Sum = sum });
            }
        }

        [System, BurstCompile]
        public static void Collect(ref Events<BatchEnvelopeEvent> events, ref Res<BatchEnvelopeStats> stats)
        {
            foreach (ref var ev in events)
            {
                if (ev.Phase == 1) stats.Ref.Prefix++;
                else
                {
                    stats.Ref.Suffix++;
                    stats.Ref.Count += ev.Count;
                    stats.Ref.Sum += ev.Sum;
                }
            }
            events.Clear();
        }

        [System, BurstCompile, RequireBatch]
        public static void Guarded(ref Query<BatchValue> query, ref State state)
        {
            if (state.Time.DeltaTime <= 0f) return;
            var delta = state.Time.DeltaTime * 5f;
            if (delta > 0f)
            {
                foreach (ref var value in query)
                {
                    value.Value += delta;
                }
            }
        }

        [System, BurstCompile]
        public static void Runtime(ref Query<BatchValue> query)
        {
            foreach (var value in query.iter()) value.C0.Get.Value++;
        }

        [System, BurstCompile]
        public static void Pooled(ref Query<BatchPoolValue> query)
        {
            foreach (ref var value in query) value.Value++;
        }

        [System, BurstCompile]
        public static void LoopReturn(ref Query<BatchValue> query)
        {
            foreach (ref var value in query)
            {
                if (value.Value < 0f) return;
                value.Value++;
            }
        }

        [System, BurstCompile]
        public static void WithoutQuery(ref State state) { }
    }

    [TestFixture]
    [BurstCompile]
    public unsafe class BatchRewriteRegressionTests
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate void GuardProbe(Query<BatchValue>* query, State* state, int parallel, int* nativeResult);

        [BurstDiscard]
        private static void MarkManaged(ref bool native) => native = false;

        [BurstCompile(CompileSynchronously = true)]
        private static void ProbeGuard(Query<BatchValue>* query, State* state, int parallel, int* nativeResult)
        {
            var native = true;
            MarkManaged(ref native);
            var job = new BatchRewriteSystems.__Guarded_Job();
            if (parallel == 0) job.OnUpdateBatched(ref *query, ref *state);
            else job.OnUpdateBatchedParallel(ref *query, ref *state, new Range(0, query->Count));
            *nativeResult = native ? 1 : -1;
        }

        [TestCase(0)]
        [TestCase(1)]
        public void NativeBurst_ContextualWalker_PreservesEarlyReturnAndGuard(int parallel)
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var entity = world.Entity(new BatchValue { Value = 2 });
                world.Update();
                var query = new Query<BatchValue>();
                query.Init(ref world.unsafeWorldPtr);
                var state = new State { World = world };
                var probe = BurstCompiler.CompileFunctionPointer<GuardProbe>(ProbeGuard);
                var native = 0;
                probe.Invoke(&query, &state, parallel, &native);
                Assert.AreEqual(1, native, "Managed fallback returns -1.");
                Assert.AreEqual(2, entity.Get<BatchValue>().Value);
                state.Time.DeltaTime = 1;
                probe.Invoke(&query, &state, parallel, &native);
                Assert.AreEqual(1, native);
                Assert.AreEqual(7, entity.Get<BatchValue>().Value);
            }
            finally { world.Dispose(); }
        }

        [TestCase(Threads.Main, false, 37)]
        [TestCase(Threads.MainRun, false, 37)]
        [TestCase(Threads.Single, false, 37)]
        [TestCase(Threads.Parallel, false, 2049)]
        [TestCase(Threads.Main, true, 37)]
        [TestCase(Threads.MainRun, true, 37)]
        [TestCase(Threads.Single, true, 37)]
        [TestCase(Threads.Parallel, true, 2049)]
        [TestCase(Threads.Main, false, 0)]
        [TestCase(Threads.MainRun, false, 0)]
        [TestCase(Threads.Single, false, 0)]
        [TestCase(Threads.Parallel, false, 0)]
        public void Envelope_PreservesScopeWritesAndSuffix(Threads mode, bool sparse, int count)
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                world.AddRes(new BatchEnvelopeStats());
                var systems = new Systems(ref world)
                    .Add(BatchRewriteSystems.Envelope, mode)
                    .Add(BatchRewriteSystems.Collect, Threads.Main);
                var info = ((ISystemCompilationInfoProvider)systems.Runners[0]).CompilationInfo;
                Assert.AreEqual(SystemCompilationKind.PointerBatch, info.Kind);
                Assert.AreEqual(BatchFallbackReason.None, info.FallbackReason);
                Assert.IsTrue(info.HasSurroundingCode);
                var entities = new Entity[count];
                var expectedCount = 0;
                var expectedSum = 0f;
                for (var i = 0; i < count; i++)
                {
                    var e = entities[i] = world.Entity(new BatchValue { Value = i });
                    if (sparse && i % 3 == 0) e.Add<BatchExcluded>();
                    else { expectedCount++; expectedSum += i + 3f; }
                }
                world.Update();
                systems.OnUpdate(1f, 1f);
                var stats = world.GetRes<BatchEnvelopeStats>();
                Assert.Greater(stats.Prefix, 0, "Prefix must run even for an empty query.");
                Assert.AreEqual(stats.Prefix, stats.Suffix, "Dispatcher must not return past the suffix.");
                Assert.AreEqual(expectedCount, stats.Count, "Captured locals must propagate walker writes.");
                Assert.AreEqual(expectedSum, stats.Sum);
                for (var i = 0; i < count; i++)
                    Assert.AreEqual(i + (sparse && i % 3 == 0 ? 0f : 3f), entities[i].Get<BatchValue>().Value);
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void Metadata_DistinguishesFallbackReasons()
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var systems = new Systems(ref world)
                    .Add(BatchRewriteSystems.Runtime, Threads.Main)
                    .Add(BatchRewriteSystems.Pooled, Threads.Main)
                    .Add(BatchRewriteSystems.LoopReturn, Threads.Main)
                    .Add(BatchRewriteSystems.WithoutQuery, Threads.Main);
                var reasons = new[] { BatchFallbackReason.ExplicitRuntimeIterator, BatchFallbackReason.PoolComponent,
                    BatchFallbackReason.UnsupportedControlFlow, BatchFallbackReason.NoQuery };
                for (var i = 0; i < reasons.Length; i++)
                {
                    var info = ((ISystemCompilationInfoProvider)systems.Runners[i]).CompilationInfo;
                    Assert.AreEqual(reasons[i], info.FallbackReason);
                    Assert.AreEqual(i == 3 ? SystemCompilationKind.NoQuery : SystemCompilationKind.RuntimeIteration, info.Kind);
                    Assert.IsNotEmpty(info.FallbackDetail);
                    StringAssert.EndsWith("BatchRewriteRegressionTests.cs", info.FallbackFile);
                    Assert.Greater(info.FallbackLine, 0);
                    Assert.Greater(info.FallbackColumn, 0);
                }
                StringAssert.Contains("iter()", ((ISystemCompilationInfoProvider)systems.Runners[0]).CompilationInfo.FallbackDetail);
                StringAssert.Contains("BatchPoolValue", ((ISystemCompilationInfoProvider)systems.Runners[1]).CompilationInfo.FallbackDetail);
                StringAssert.Contains("return", ((ISystemCompilationInfoProvider)systems.Runners[2]).CompilationInfo.FallbackDetail);
            }
            finally { world.Dispose(); }
        }
    }
}
