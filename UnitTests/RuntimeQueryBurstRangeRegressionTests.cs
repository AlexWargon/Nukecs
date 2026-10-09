using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Mathematics;

namespace Wargon.Nukecs.Tests
{
    public struct BurstRange1 : IComponent { public float3 Value; }
    public struct BurstRange2 : IComponent { public float3 Value; }
    public struct BurstRange3 : IComponent { public float3 Value; }
    public struct BurstRange4 : IComponent { public float3 Value; }

    [BurstCompile]
    public unsafe class RuntimeQueryBurstRangeRegressionTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RangeProbe(QueryUnsafe* query, int start, int stop, int* result);

        [BurstDiscard]
        private static void MarkManaged(ref bool native) => native = false;

        [BurstCompile(CompileSynchronously = true)]
        private static void Probe(QueryUnsafe* query, int start, int stop, int* result)
        {
            var native = true;
            MarkManaged(ref native);
            var range = new Range(start, stop);
            var iterator = new QueryRuntimeIter4<QueryRuntimeRefs<BurstRange1, BurstRange2, BurstRange3, BurstRange4>>(query, range);
            var count = 0;
            while (iterator.MoveNext())
            {
                var (a, b, c, d) = iterator.Current;
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
                count++;
            }
            *result = native ? count : -1;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NativeBurst_RangedInline4_CountsMoveNextAndWrites(bool sparse)
        {
            var world = World.Create(WorldConfig.Default256);
            try
            {
                var entities = new Entity[37];
                for (var i = 0; i < entities.Length; i++)
                {
                    var e = entities[i] = world.Entity();
                    e.Add(new BurstRange1());
                    e.Add(new BurstRange2 { Value = new float3(1, 2, 3) });
                    e.Add(new BurstRange3());
                    e.Add(new BurstRange4 { Value = new float3(4, 5, 6) });
                    if (sparse && i % 2 == 0) e.Add<StabTag>();
                }
                world.Update();
                var query = new Query<BurstRange1, BurstRange2, BurstRange3, BurstRange4>();
                query.Init(ref world.unsafeWorldPtr);
                var function = BurstCompiler.CompileFunctionPointer<RangeProbe>(Probe);
                var count = 0;
                function.Invoke(query._query.Ptr, 0, 0, &count);
                Assert.AreEqual(0, count, "Probe must execute native Burst (managed returns -1).");
                function.Invoke(query._query.Ptr, 0, 13, &count);
                Assert.AreEqual(13, count);
                function.Invoke(query._query.Ptr, 13, 37, &count);
                Assert.AreEqual(24, count);
                foreach (var entity in entities)
                {
                    Assert.AreEqual(new float3(1, 2, 3), entity.Get<BurstRange1>().Value);
                    Assert.AreEqual(new float3(4, 5, 6), entity.Get<BurstRange3>().Value);
                }
            }
            finally { world.Dispose(); }
        }

        [TestCase(Threads.Single)]
        [TestCase(Threads.Parallel)]
        public void GeneratedRunner_Inline4_ParIter_WritesEveryEntityOnce(Threads mode)
        {
            var world = World.Create(WorldConfig.Default16384);
            try
            {
                var entities = new Entity[2049];
                for (var i = 0; i < entities.Length; i++)
                {
                    var e = entities[i] = world.Entity();
                    e.Add(new BurstRange1());
                    e.Add(new BurstRange2 { Value = new float3(1, 2, 3) });
                    e.Add(new BurstRange3());
                    e.Add(new BurstRange4 { Value = new float3(4, 5, 6) });
                }
                world.Update();
                var systems = new Systems(ref world).Add(BurstRangeSystems.Update, mode);
                systems.OnUpdate(1f, 1f);
                foreach (var entity in entities)
                {
                    Assert.AreEqual(new float3(1, 2, 3), entity.Get<BurstRange1>().Value);
                    Assert.AreEqual(new float3(4, 5, 6), entity.Get<BurstRange3>().Value);
                }
            }
            finally { world.Dispose(); }
        }
    }

    public static partial class BurstRangeSystems
    {
        [System, BurstCompile]
        public static void Update(ref Query<BurstRange1, BurstRange2, BurstRange3, BurstRange4> query)
        {
            foreach (var (a, b, c, d) in query.par_iter())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
        }
    }
}