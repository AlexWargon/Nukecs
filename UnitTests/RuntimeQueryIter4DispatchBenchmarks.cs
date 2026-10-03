using NUnit.Framework;
using Unity.PerformanceTesting;
using Wargon.Nukecs.Tests.RuntimeDispatch;

namespace Wargon.Nukecs.Tests.RuntimeDispatch
{
    // These foreach bodies are ordinary C# methods, not source-generated system bodies.
    public static class RuntimeDispatchLoops
    {
        public static void InlineControl(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,RuntimeQi4D> query)
        {
            foreach(var (a,b,c,d) in query.iter_mixed_runtime())
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void InlineRuntime(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,RuntimeQi4D> query)
        {
            foreach(var (a,b,c,d) in query)
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void OneControl(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        {
            foreach(var (a,b,c,d) in query.iter_cached_aaap_runtime())
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void OneRuntime(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        {
            foreach(var (a,b,c,d) in query)
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void MixedControl(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        {
            foreach(var (a,b,c,d) in query.iter_cached_apap_runtime())
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void MixedRuntime(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        {
            foreach(var (a,b,c,d) in query)
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void PoolsControl(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        {
            foreach(var (a,b,c,d) in query.iter_cached_allpool_runtime())
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void PoolsRuntime(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        {
            foreach(var (a,b,c,d) in query)
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
    }
}
namespace Wargon.Nukecs.Tests
{
    public static class RuntimeDispatchHarness
    {
        [System]
        public static void InlineControl(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,RuntimeQi4D> query)
        { RuntimeDispatchLoops.InlineControl(ref query); }
        [System]
        public static void InlineRuntime(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,RuntimeQi4D> query)
        { RuntimeDispatchLoops.InlineRuntime(ref query); }
        [System]
        public static void OneControl(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        { RuntimeDispatchLoops.OneControl(ref query); }
        [System]
        public static void OneRuntime(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        { RuntimeDispatchLoops.OneRuntime(ref query); }
        [System]
        public static void MixedControl(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        { RuntimeDispatchLoops.MixedControl(ref query); }
        [System]
        public static void MixedRuntime(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        { RuntimeDispatchLoops.MixedRuntime(ref query); }
        [System]
        public static void PoolsControl(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        { RuntimeDispatchLoops.PoolsControl(ref query); }
        [System]
        public static void PoolsRuntime(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        { RuntimeDispatchLoops.PoolsRuntime(ref query); }
    }
    [TestFixture]
    public class RuntimeQueryIter4DispatchBenchmarks
    {
        [TestCase(false),TestCase(true),Performance]
        public void CompareDeferredCreation(bool reverse)
        {
            foreach(var variant in reverse?new[]{1,0}:new[]{0,1})
                RuntimeQueryIter4PageCacheBenchmarks.Run<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4>(false,3,systems=>
                {
                    if(variant==0) systems.Add(RuntimeDispatchHarness.PoolsControl,Threads.Main);
                    else systems.Add(RuntimeDispatchHarness.PoolsRuntime,Threads.Main);
                },true,variant==0?"Control":"Runtime");
        }

        [TestCase(0,false,false), TestCase(1,false,false), TestCase(2,false,false), TestCase(4,false,false)]
        [TestCase(0,false,true), TestCase(1,false,true), TestCase(2,false,true), TestCase(4,false,true)]
        [TestCase(0,true,false), TestCase(1,true,false), TestCase(2,true,false), TestCase(4,true,false)]
        [TestCase(0,true,true), TestCase(1,true,true), TestCase(2,true,true), TestCase(4,true,true)]
        [Performance]
        public void Compare(int pools,bool shuffled,bool reverse)
        {
            foreach(var variant in reverse ? new[] {1,0}:new[] {0,1})
            {
                switch(pools)
                {
                    case 0: RuntimeQueryIter4PageCacheBenchmarks.Run<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,RuntimeQi4D>(shuffled,3,systems=>
                    {
                        if(variant==0) systems.Add(RuntimeDispatchHarness.InlineControl,Threads.Main);
                        else systems.Add(RuntimeDispatchHarness.InlineRuntime,Threads.Main);
                    },sampleName:variant==0?"Control":"Runtime"); break;
                    case 1: RuntimeQueryIter4PageCacheBenchmarks.Run<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4>(shuffled,3,systems=>
                    {
                        if(variant==0) systems.Add(RuntimeDispatchHarness.OneControl,Threads.Main);
                        else systems.Add(RuntimeDispatchHarness.OneRuntime,Threads.Main);
                    },sampleName:variant==0?"Control":"Runtime"); break;
                    case 2: RuntimeQueryIter4PageCacheBenchmarks.Run<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4>(shuffled,3,systems=>
                    {
                        if(variant==0) systems.Add(RuntimeDispatchHarness.MixedControl,Threads.Main);
                        else systems.Add(RuntimeDispatchHarness.MixedRuntime,Threads.Main);
                    },sampleName:variant==0?"Control":"Runtime"); break;
                    case 4: RuntimeQueryIter4PageCacheBenchmarks.Run<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4>(shuffled,3,systems=>
                    {
                        if(variant==0) systems.Add(RuntimeDispatchHarness.PoolsControl,Threads.Main);
                        else systems.Add(RuntimeDispatchHarness.PoolsRuntime,Threads.Main);
                    },sampleName:variant==0?"Control":"Runtime"); break;
                }
            }
        }
    }
}
