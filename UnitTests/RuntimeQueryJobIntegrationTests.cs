using System;
using System.Runtime.InteropServices;
using System.Reflection;
using NUnit.Framework;
using Unity.Burst;
using Unity.Jobs;

namespace Wargon.Nukecs.Tests
{
    [BurstCompile]
    public unsafe class RuntimeQueryJobIntegrationTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BurstProbe(QueryUnsafe* query, int* result);

        [BurstDiscard]
        private static void MarkManaged(ref bool isBurst) => isBurst = false;

        [BurstCompile(CompileSynchronously = true)]
        private static void BaselineProbe(QueryUnsafe* query, int* result)
        {
            var isBurst = true;
            MarkManaged(ref isBurst);
            *result = isBurst ? 1 : -1;
        }

        [BurstCompile(CompileSynchronously = true)]
        private static void Probe(QueryUnsafe* query, int* result)
        {
            var isBurst = true;
            MarkManaged(ref isBurst);
            var count = 0;
            foreach (var tuple in new QueryRuntimeIter2<QueryRuntimeRefs<IntegrationA1, IntegrationP2>>(query))
                count += tuple.C0.Read.Value + tuple.C1.Read.Value;
            *result = isBurst ? count : -1;
        }

        [BurstCompile(CompileSynchronously = true)]
        private static void InlineProbe(ref Query<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4> query, int* result)
        {
            foreach (var (a, b, c, d) in query.iter())
                *result += a.Read.Value + b.Read.Value + c.Read.Value + d.Read.Value;
        }

        [BurstCompile(CompileSynchronously = true)]
        private static void PoolProbe(ref Query<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4> query, int* result)
        {
            foreach (var (a, b, c, d) in query.par_iter())
                *result += a.Read.Value + b.Read.Value + c.Read.Value + d.Read.Value;
        }

        [BurstCompile(CompileSynchronously = true)]
        private static void EntityTagProbe(ref Query<Entity, IntegrationP1, IntegrationExcluded> query, int* result)
        {
            foreach (var tuple in query.iter())
                *result += tuple.C0.Read.id + tuple.C1.Read.Value;
        }

        [BurstCompile(CompileSynchronously = true)]
        private static void EightComponentProbe(ref Query<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5, IntegrationP6, IntegrationA7, IntegrationP8, None<IntegrationExcluded>> query, int* result)
        {
            foreach (var (a, b, c, d, e, f, g, h) in query.par_iter())
                *result += a.Read.Value + b.Read.Value + c.Read.Value + d.Read.Value
                         + e.Read.Value + f.Read.Value + g.Read.Value + h.Read.Value;
        }

        [TestCase(nameof(Probe))]
        [TestCase(nameof(InlineProbe))]
        [TestCase(nameof(PoolProbe))]
        [TestCase(nameof(EntityTagProbe))]
        [TestCase(nameof(EightComponentProbe))]
        public void BurstCompiler_ProducesNativeAssembly_ForRuntimeIterator(string probeName)
        {
            var method = typeof(RuntimeQueryJobIntegrationTests).GetMethod(probeName, BindingFlags.NonPublic | BindingFlags.Static);
            Type service = null;
            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                service = loadedAssembly.GetType("Unity.Burst.LowLevel.BurstCompilerService");
                if (service != null) break;
            }
            Assert.IsNotNull(service, "The Editor must expose the Burst disassembly service.");
            var disassemble = service.GetMethod("GetDisassembly", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(disassemble);
            var assembly = (string)disassemble.Invoke(null, new object[] { method, "--target=X64_SSE2\n--dump=Asm" });
            Assert.IsNotEmpty(assembly);
            StringAssert.DoesNotContain("Burst error", assembly);
            StringAssert.DoesNotContain("Failed to compile", assembly);
            StringAssert.Contains("ret", assembly);
            TestContext.WriteLine("Burst native assembly generated: " + assembly.Length + " characters.");
        }

        [Test]
        public void NativeBurst_CompilesAndExecutesRuntimeIterator()
        {
            if (!BurstCompiler.Options.IsEnabled) Assert.Ignore("Native Burst is unavailable in this Editor session.");
            var baseline = 0;
            BurstCompiler.CompileFunctionPointer<BurstProbe>(BaselineProbe).Invoke(null, &baseline);
            if (baseline != 1) Assert.Ignore("Even the independent baseline function pointer executes managed fallback in this Editor session.");
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var e = world.GetArchetype(typeof(IntegrationA1), typeof(IntegrationP2)).CreateEntity();
                e.Get<IntegrationA1>().Value = 3;
                e.Get<IntegrationP2>().Value = 7;
                var query = new Query<IntegrationA1, IntegrationP2>();
                var pointer = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
                query.Init(ref pointer);
                var result = 0;
                BurstCompiler.CompileFunctionPointer<BurstProbe>(Probe).Invoke(query._query.Ptr, &result);
                Assert.AreEqual(10, result, "The probe must execute native Burst, not managed fallback.");
            }
            finally { world.Dispose(); }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct MixedJob : IJob
        {
            public Query<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4> Query;
            public void Execute()
            {
                foreach (var (a, b, c, d) in Query.par_iter())
                {
                    a.Get.Value += b.Read.Value;
                    c.Get.Value += d.Read.Value;
                }
            }
        }

        [TestCase(Threads.Main)]
        [TestCase(Threads.Single)]
        [TestCase(Threads.Parallel)]
        public void SystemRunner_IterPar_RespectsAssignedRange(Threads mode)
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var arch = world.GetArchetype(typeof(IntegrationA1), typeof(IntegrationP2), typeof(IntegrationA3), typeof(IntegrationP4));
                for (var i = 0; i < 2049; i++)
                {
                    var e = arch.CreateEntity();
                    e.Get<IntegrationP2>().Value = 3;
                    e.Get<IntegrationP4>().Value = 7;
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var systems = new Systems(ref world);
                systems.Add(RuntimeQueryIntegrationSystems.UpdateRange, mode);
                systems.OnUpdate(1f, 1f);
                var query = new Query<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4>();
                var pointer = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
                query.Init(ref pointer);
                var count = 0;
                foreach (var (a, b, c, d) in query.iter())
                {
                    Assert.AreEqual(3, a.Read.Value);
                    Assert.AreEqual(7, c.Read.Value);
                    count++;
                }
                Assert.AreEqual(2049, count);
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void ScheduledJobs_DisjointRanges_WriteEachEntityOnce()
        {
            var world = World.Create(WorldConfig.Default1024);
            JobHandle pending = default;
            try
            {
                var arch = world.GetArchetype(typeof(IntegrationA1), typeof(IntegrationP2), typeof(IntegrationA3), typeof(IntegrationP4));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    e.Get<IntegrationP2>().Value = 3;
                    e.Get<IntegrationP4>().Value = 7;
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4>();
                var pointer = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
                query.Init(ref pointer);
                var leftRange = new Range(0, 61);
                query.Update(ref world, (IntPtr)(&leftRange));
                pending = new MixedJob { Query = query }.Schedule();
                var rightRange = new Range(61, 137);
                query.Update(ref world, (IntPtr)(&rightRange));
                var second = new MixedJob { Query = query }.Schedule();
                pending = JobHandle.CombineDependencies(pending, second);
                pending.Complete();
                var count = 0;
                foreach (var (a, b, c, d) in query.iter())
                {
                    Assert.AreEqual(3, a.Read.Value);
                    Assert.AreEqual(3, b.Read.Value);
                    Assert.AreEqual(7, c.Read.Value);
                    Assert.AreEqual(7, d.Read.Value);
                    count++;
                }
                Assert.AreEqual(137, count);
            }
            finally { pending.Complete(); world.Dispose(); }
        }
    }

    public static class RuntimeQueryIntegrationSystems
    {
        [System, BurstCompile]
        public static void UpdateRange(ref Query<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4> query)
        {
            foreach (var (a, b, c, d) in query.par_iter())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
        }
    }
}
