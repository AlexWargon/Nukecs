using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Collections.LowLevel.Unsafe;
namespace Wargon.Nukecs.Tests
{
    public struct CacheQi4P1 : IPoolComponent { public float3 Value; }
    public struct CacheQi4P2 : IPoolComponent { public float3 Value; }
    public struct CacheQi4P3 : IPoolComponent { public float3 Value; }
    public struct CacheQi4P4 : IPoolComponent { public float3 Value; }
    public static class RuntimeQueryIter4PageCacheHarness
    {
        [System]
        public static void OneSpecialized(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        {
            var count=query.Count;
            foreach (var (a,b,c,d) in query.iter_cached_aaap_runtime())
            {
                a.Get.Value+=b.Read.Value;
                c.Get.Value+=d.Read.Value;
            }
            if (count<0) dbug.log("unreachable");
        }
        [System]
        public static void MixedSpecialized(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        {
            var count=query.Count;
            foreach (var (a,b,c,d) in query.iter_cached_apap_runtime())
            {
                a.Get.Value+=b.Read.Value;
                c.Get.Value+=d.Read.Value;
            }
            if (count<0) dbug.log("unreachable");
        }
        [System]
        public static void OneBaseline(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_paged_pool_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void OneDirect(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_direct_pool_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void OneCached(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_cached_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void OnePlain(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query)
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void MixedBaseline(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_paged_pool_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void MixedDirect(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_direct_pool_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void MixedCached(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_cached_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void MixedPlain(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query)
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void PoolsBaseline(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_paged_pool_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void PoolsDirect(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_direct_pool_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void PoolsCached(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_cached_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void PoolsAllPool(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query.iter_cached_allpool_runtime())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
        [System]
        public static void PoolsPlain(ref Query<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4> query)
        {
            var count = query.Count;
            foreach (var (a,b,c,d) in query)
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
            }
            if (count < 0) dbug.log("unreachable");
        }
    }
    [TestFixture]
    public unsafe class RuntimeQueryIter4PageCacheBenchmarks
    {
        [TestCase(false), TestCase(true), Performance]
        public void CompareDeferredCreation(bool reverse)
        {
            var order=reverse ? new[] {4,3,0} : new[] {0,3,4};
            foreach (var variant in order)
                Run<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4>(false, variant, systems =>
                {
                    switch (variant)
                    {
                        case 0: systems.Add(RuntimeQueryIter4PageCacheHarness.PoolsBaseline, Threads.Main); break;
                        case 3: systems.Add(RuntimeQueryIter4PageCacheHarness.PoolsAllPool, Threads.Main); break;
                        case 4: systems.Add(RuntimeQueryIter4PageCacheHarness.PoolsPlain, Threads.Main); break;
                    }
                }, true);
        }

        [TestCase(1,false,false), TestCase(2,false,false), TestCase(4,false,false)]
        [TestCase(1,true,false), TestCase(2,true,false), TestCase(4,true,false)]
        [TestCase(1,false,true), TestCase(2,false,true), TestCase(4,false,true)]
        [TestCase(1,true,true), TestCase(2,true,true), TestCase(4,true,true)]
        [Performance]
        public void Compare(int pools, bool shuffled, bool reverse)
        {
            var order = reverse ? new[] {4,3,2,1,0} : new[] {0,1,2,3,4};
            foreach (var variant in order)
            {
                switch (pools)
                {
                    case 1: Run<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4>(shuffled, variant, systems =>
                    {
                        switch (variant)
                        {
                            case 0: systems.Add(RuntimeQueryIter4PageCacheHarness.OneBaseline, Threads.Main); break;
                            case 1: systems.Add(RuntimeQueryIter4PageCacheHarness.OneDirect, Threads.Main); break;
                            case 2: systems.Add(RuntimeQueryIter4PageCacheHarness.OneCached, Threads.Main); break;
                            case 3: systems.Add(RuntimeQueryIter4PageCacheHarness.OneSpecialized, Threads.Main); break;
                            case 4: systems.Add(RuntimeQueryIter4PageCacheHarness.OnePlain, Threads.Main); break;
                        }
                    }); break;
                    case 2: Run<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4>(shuffled, variant, systems =>
                    {
                        switch (variant)
                        {
                            case 0: systems.Add(RuntimeQueryIter4PageCacheHarness.MixedBaseline, Threads.Main); break;
                            case 1: systems.Add(RuntimeQueryIter4PageCacheHarness.MixedDirect, Threads.Main); break;
                            case 2: systems.Add(RuntimeQueryIter4PageCacheHarness.MixedCached, Threads.Main); break;
                            case 3: systems.Add(RuntimeQueryIter4PageCacheHarness.MixedSpecialized, Threads.Main); break;
                            case 4: systems.Add(RuntimeQueryIter4PageCacheHarness.MixedPlain, Threads.Main); break;
                        }
                    }); break;
                    case 4: Run<CacheQi4P1,CacheQi4P2,CacheQi4P3,CacheQi4P4>(shuffled, variant, systems =>
                    {
                        switch (variant)
                        {
                            case 0: systems.Add(RuntimeQueryIter4PageCacheHarness.PoolsBaseline, Threads.Main); break;
                            case 1: systems.Add(RuntimeQueryIter4PageCacheHarness.PoolsDirect, Threads.Main); break;
                            case 2: systems.Add(RuntimeQueryIter4PageCacheHarness.PoolsCached, Threads.Main); break;
                            case 3: systems.Add(RuntimeQueryIter4PageCacheHarness.PoolsAllPool, Threads.Main); break;
                            case 4: systems.Add(RuntimeQueryIter4PageCacheHarness.PoolsPlain, Threads.Main); break;
                        }
                    }); break;
                }
            }
        }
        internal static void Run<T1,T2,T3,T4>(bool shuffled, int variant, Action<Systems> configure, bool deferred = false, string sampleName = null)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            const int count = 100000;
            var world = World.Create(deferred
                ? new WorldConfig { StartPoolSize=count+1, StartEntitiesAmount=count+1, StartComponentsAmount=64 }
                : WorldConfig.Default_1_000_000);
            try
            {
                var systems = new Systems(ref world);
                configure(systems);
                var arch = world.GetArchetype(typeof(T1),typeof(T2),typeof(T3),typeof(T4));
                var entities = new Entity[count];
                if (shuffled)
                {
                    for (var i=0;i<count;i++) entities[i] = arch.CreateEntity();
                    var random = new System.Random(431);
                    for (var i=count-1;i>0;i--) { var j=random.Next(i+1); var e=entities[i]; entities[i]=entities[j]; entities[j]=e; }
                    for (var i=0;i<count;i++)
                    {
                        entities[i].DestroyNow();
                        if ((i & 255)==255) world.Update();
                    }
                    world.Update();
                }
                for (var i=0;i<count;i++)
                {
                    var e=deferred ? world.Entity() : arch.CreateEntity(); entities[i]=e;
                    if (deferred)
                    {
                        // Match BenchNukecs's concrete IPoolComponent overloads.
                        // Generic Add<T> constrained only to IComponent selects a different ECB path.
                        e.Add(new CacheQi4P1 { Value=new float3(1,2,3) });
                        e.Add(new CacheQi4P2 { Value=new float3(1,2,3) });
                        e.Add(new CacheQi4P3 { Value=new float3(1,2,3) });
                        e.Add(new CacheQi4P4 { Value=new float3(1,2,3) });
                    }
                    else Value<T1>(e)=Value<T2>(e)=Value<T3>(e)=Value<T4>(e)=new float3(1,2,3);
                    if (shuffled && i%3==0) e.Add<IsPrefab>();
                }
                world.Update();
                if (deferred)
                    for (var i=0;i<count;i++)
                        if (!math.all(Value<T1>(entities[i])==new float3(1,2,3)) ||
                            !math.all(Value<T2>(entities[i])==new float3(1,2,3)) ||
                            !math.all(Value<T3>(entities[i])==new float3(1,2,3)) ||
                            !math.all(Value<T4>(entities[i])==new float3(1,2,3)))
                            Assert.Fail("Deferred creation lost component values before iteration: " + i);
                if (shuffled)
                {
                    var gaps=0;
                    for (var i=1;i<count;i++) if (entities[i].id!=entities[i-1].id+1) gaps++;
                    Assert.Greater(gaps,count/2,"Fixture must shuffle IDs.");
                }
                var names=new[] {"Baseline","Direct","Cached","Specialized","Plain"};
                Measure.Method(() => systems.OnUpdate(0.016f, 1f))
                    .SampleGroup(new SampleGroup(sampleName ?? names[variant],SampleUnit.Millisecond))
                    .WarmupCount(10).MeasurementCount(100).IterationsPerMeasurement(1).Run();
                for (var i=0;i<count;i++)
                {
                    var expected = new float3(1,2,3) * (shuffled && i%3==0 ? 1 : 111);
                    if (!math.all(Value<T1>(entities[i])==expected) || !math.all(Value<T3>(entities[i])==expected) ||
                        !math.all(Value<T2>(entities[i])==new float3(1,2,3)) || !math.all(Value<T4>(entities[i])==new float3(1,2,3)))
                        Assert.Fail("Incorrect values at entity " + i + " in " + names[variant]);
                }
            }
            finally { world.Dispose(); }
        }
        private static ref float3 Value<T>(Entity e) where T : unmanaged,IComponent
        {
            ref var value=ref e.Get<T>();
            return ref *(float3*)UnsafeUtility.AddressOf(ref value);
        }
    }
}
