using NUnit.Framework;
using Unity.Burst;
using Wargon.Nukecs;

// Deliberately outside Wargon.Nukecs: generated job bodies used to be compiled inside
// namespace Wargon.Nukecs, where 'Range' silently bound to Wargon.Nukecs.Range.
namespace NukecsScopeProbe
{
    public struct ScopeValue : IComponent { public float Value; }

    /// <summary>Same short name as Wargon.Nukecs.Range; system bodies must bind to this one.</summary>
    public struct Range { public float Lo; }

    public static partial class ScopeSystems
    {
        public const float Step = 2f;

        public struct Scale { public float K; }

        public static float PublicHelper(float v) => v + Step;

        private static float PrivateHelper(float v) => v * 2f;

        internal static float InternalHelper(float v) => v - 1f;

        // value -> (value + 2) * 4 + 4 - 1 = 4 * value + 11
        [System, BurstCompile, RequireBatch]
        public static void Apply(ref Query<ScopeValue> query, ref State state)
        {
            var bounds = new Range { Lo = Step };
            var scale = new Scale { K = PrivateHelper(bounds.Lo) };
            foreach (ref var value in query)
                value.Value = InternalHelper(PublicHelper(value.Value) * scale.K + PrivateHelper(Step));
        }

        public static partial class Nested
        {
            private const float Bonus = 3f;

            // value -> value + 3 + 2
            [System, BurstCompile, RequireBatch]
            public static void AddBonus(ref Query<ScopeValue> query)
            {
                foreach (ref var value in query) value.Value += Bonus + Step;
            }
        }
    }

    // Not partial: NUKECS012 warning, using static fallback for public members.
#pragma warning disable NUKECS012
    public static class LegacyScopeSystems
    {
        public static float Twice(float v) => v * 2f;

        [System, BurstCompile, RequireBatch]
        public static void Double(ref Query<ScopeValue> query)
        {
            foreach (ref var value in query) value.Value = Twice(value.Value);
        }
    }
#pragma warning restore NUKECS012

    [TestFixture]
    [BurstCompile]
    public unsafe class SystemClassScopeTests
    {
        private static Entity[] Spawn(ref World world, int count)
        {
            var entities = new Entity[count];
            for (var i = 0; i < count; i++) entities[i] = world.Entity(new ScopeValue { Value = i });
            world.Update();
            return entities;
        }

        private static void AssertBatch(Systems systems)
        {
            var info = ((ISystemCompilationInfoProvider)systems.Runners[0]).CompilationInfo;
            Assert.AreEqual(SystemCompilationKind.PointerBatch, info.Kind);
            Assert.AreEqual(BatchFallbackReason.None, info.FallbackReason);
        }

        [TestCase(Threads.Main)]
        [TestCase(Threads.MainRun)]
        [TestCase(Threads.Single)]
        [TestCase(Threads.Parallel)]
        public void Body_UsesClassScope_ShortHelpersConstantsNestedTypesAndOwnRange(Threads mode)
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var systems = new Systems(ref world).Add(ScopeSystems.Apply, mode);
                AssertBatch(systems);
                Assert.AreEqual("ScopeSystems_Apply", systems.Runners[0].Name);
                var entities = Spawn(ref world, 1200);
                systems.OnUpdate(0.016f, 0.016f);
                systems.Complete();
                for (var i = 0; i < entities.Length; i++)
                    Assert.AreEqual(4f * i + 11f, entities[i].Get<ScopeValue>().Value);
            }
            finally { world.Dispose(); }
        }

        [TestCase(Threads.Main)]
        [TestCase(Threads.Parallel)]
        public void NestedClassSystem_SeesOuterAndOwnPrivateMembers(Threads mode)
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var systems = new Systems(ref world).Add(ScopeSystems.Nested.AddBonus, mode);
                AssertBatch(systems);
                var entities = Spawn(ref world, 700);
                systems.OnUpdate(0.016f, 0.016f);
                systems.Complete();
                for (var i = 0; i < entities.Length; i++)
                    Assert.AreEqual(i + 5f, entities[i].Get<ScopeValue>().Value);
            }
            finally { world.Dispose(); }
        }

        [TestCase(Threads.Main)]
        [TestCase(Threads.Parallel)]
        public void NonPartialFallback_ResolvesPublicHelpersByShortName(Threads mode)
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var systems = new Systems(ref world).Add(LegacyScopeSystems.Double, mode);
                AssertBatch(systems);
                var entities = Spawn(ref world, 300);
                systems.OnUpdate(0.016f, 0.016f);
                systems.Complete();
                for (var i = 0; i < entities.Length; i++)
                    Assert.AreEqual(i * 2f, entities[i].Get<ScopeValue>().Value);
            }
            finally { world.Dispose(); }
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate void ApplyProbe(Query<ScopeValue>* query, State* state, int parallel, int* nativeResult);

        [BurstDiscard]
        private static void MarkManaged(ref bool native) => native = false;

        [BurstCompile(CompileSynchronously = true)]
        private static void ProbeApply(Query<ScopeValue>* query, State* state, int parallel, int* nativeResult)
        {
            var native = true;
            MarkManaged(ref native);
            var job = new ScopeSystems.__Apply_Job();
            if (parallel == 0) job.OnUpdateBatched(ref *query, ref *state);
            else job.OnUpdateBatchedParallel(ref *query, ref *state, new Wargon.Nukecs.Range(0, query->Count));
            *nativeResult = native ? 1 : -1;
        }

        [TestCase(0)]
        [TestCase(1)]
        public void NativeBurst_CompilesNestedJobWithPrivateHelpers(int parallel)
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var entity = world.Entity(new ScopeValue { Value = 1 });
                world.Update();
                var query = new Query<ScopeValue>();
                query.Init(ref world.unsafeWorldPtr);
                var state = new State { World = world };
                var probe = BurstCompiler.CompileFunctionPointer<ApplyProbe>(ProbeApply);
                var native = 0;
                probe.Invoke(&query, &state, parallel, &native);
                Assert.AreEqual(1, native, "Managed fallback returns -1.");
                Assert.AreEqual(15f, entity.Get<ScopeValue>().Value);
            }
            finally { world.Dispose(); }
        }
    }
}
