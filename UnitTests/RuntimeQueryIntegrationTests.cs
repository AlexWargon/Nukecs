using System;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Tests
{
    public struct IntegrationA1 : IComponent { public int Value; }
    public struct IntegrationP1 : IPoolComponent { public int Value; }
    public struct IntegrationA2 : IComponent { public int Value; }
    public struct IntegrationP2 : IPoolComponent { public int Value; }
    public struct IntegrationA3 : IComponent { public int Value; }
    public struct IntegrationP3 : IPoolComponent { public int Value; }
    public struct IntegrationA4 : IComponent { public int Value; }
    public struct IntegrationP4 : IPoolComponent { public int Value; }
    public struct IntegrationA5 : IComponent { public int Value; }
    public struct IntegrationP5 : IPoolComponent { public int Value; }
    public struct IntegrationA6 : IComponent { public int Value; }
    public struct IntegrationP6 : IPoolComponent { public int Value; }
    public struct IntegrationA7 : IComponent { public int Value; }
    public struct IntegrationP7 : IPoolComponent { public int Value; }
    public struct IntegrationA8 : IComponent { public int Value; }
    public struct IntegrationP8 : IPoolComponent { public int Value; }
    public struct IntegrationExcluded : IComponent { }
    public unsafe class RuntimeQueryIntegrationTests
    {
        private static void Init<T>(ref T query, ref World world) where T : struct, ISystemParam
        {
            var pointer = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            query.Init(ref pointer);
            query.Update(ref world, IntPtr.Zero);
        }
        private static void Set<T>(Entity entity, int value) where T : unmanaged, IComponent
        {
            ref var c = ref entity.Get<T>();
            *(int*)UnsafeUtility.AddressOf(ref c) = value;
        }
        [Test] public void Arity1_Inline_FullAndRanges() => Check1<IntegrationA1>();
        [Test] public void Arity1_Pools_FullAndRanges() => Check1<IntegrationP1>();
        [Test] public void Arity1_Mixed_FullAndRanges() => Check1<IntegrationA1>();
        private static void Check1<T1>()
            where T1 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    Set<T1>(e, e.id + 10000);
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<T1, None<IntegrationExcluded>>();
                Init(ref query, ref world);
                var expected = new int[query.Count];
                var count = 0;
                foreach (var tuple in query.par_iter())
                {
                    tuple.Deconstruct(out var c1);
                    expected[count++] = *(int*)c1.data - 10000;
                }
                Assert.AreEqual(91, count);
                query._range = new Range(3, 11);
                var fullCount = 0;
                foreach (var tuple in query.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    fullCount++;
                }
                Assert.AreEqual(91, fullCount, "iter must ignore the assigned job range");
                foreach (var range in new[] { new Range(0,0), new Range(0,1), new Range(1,65), new Range(65,91), new Range(91,150), new Range(20,10) })
                {
                    query._range = range;
                    var iterator = query.par_iter();
                    var visited = 0;
                    while (iterator.MoveNext())
                    {
                        var tuple = iterator.Current;
                        var entityId = expected[range.start + visited];
                        Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                        visited++;
                    }
                    Assert.AreEqual(Math.Max(0, Math.Min(range.end, expected.Length) - range.start), visited);
                    Assert.IsFalse(iterator.MoveNext());
                }
                var all = new Query<T1>();
                Init(ref all, ref world);
                var allCount = 0;
                foreach (var tuple in all.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    allCount++;
                }
                Assert.AreEqual(137, allCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void Arity2_Inline_FullAndRanges() => Check2<IntegrationA1, IntegrationA2>();
        [Test] public void Arity2_Pools_FullAndRanges() => Check2<IntegrationP1, IntegrationP2>();
        [Test] public void Arity2_Mixed_FullAndRanges() => Check2<IntegrationA1, IntegrationP2>();
        private static void Check2<T1, T2>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    Set<T1>(e, e.id + 10000);
                    Set<T2>(e, e.id + 20000);
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<T1, T2, None<IntegrationExcluded>>();
                Init(ref query, ref world);
                var expected = new int[query.Count];
                var count = 0;
                foreach (var tuple in query.par_iter())
                {
                    tuple.Deconstruct(out var c1, out var c2);
                    expected[count++] = *(int*)c1.data - 10000;
                }
                Assert.AreEqual(91, count);
                query._range = new Range(3, 11);
                var fullCount = 0;
                foreach (var tuple in query.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    fullCount++;
                }
                Assert.AreEqual(91, fullCount, "iter must ignore the assigned job range");
                foreach (var range in new[] { new Range(0,0), new Range(0,1), new Range(1,65), new Range(65,91), new Range(91,150), new Range(20,10) })
                {
                    query._range = range;
                    var iterator = query.par_iter();
                    var visited = 0;
                    while (iterator.MoveNext())
                    {
                        var tuple = iterator.Current;
                        var entityId = expected[range.start + visited];
                        Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                        Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                        visited++;
                    }
                    Assert.AreEqual(Math.Max(0, Math.Min(range.end, expected.Length) - range.start), visited);
                    Assert.IsFalse(iterator.MoveNext());
                }
                var all = new Query<T1, T2>();
                Init(ref all, ref world);
                var allCount = 0;
                foreach (var tuple in all.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    allCount++;
                }
                Assert.AreEqual(137, allCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void Arity3_Inline_FullAndRanges() => Check3<IntegrationA1, IntegrationA2, IntegrationA3>();
        [Test] public void Arity3_Pools_FullAndRanges() => Check3<IntegrationP1, IntegrationP2, IntegrationP3>();
        [Test] public void Arity3_Mixed_FullAndRanges() => Check3<IntegrationA1, IntegrationP2, IntegrationA3>();
        private static void Check3<T1, T2, T3>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    Set<T1>(e, e.id + 10000);
                    Set<T2>(e, e.id + 20000);
                    Set<T3>(e, e.id + 30000);
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<T1, T2, T3, None<IntegrationExcluded>>();
                Init(ref query, ref world);
                var expected = new int[query.Count];
                var count = 0;
                foreach (var tuple in query.par_iter())
                {
                    tuple.Deconstruct(out var c1, out var c2, out var c3);
                    expected[count++] = *(int*)c1.data - 10000;
                }
                Assert.AreEqual(91, count);
                query._range = new Range(3, 11);
                var fullCount = 0;
                foreach (var tuple in query.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    fullCount++;
                }
                Assert.AreEqual(91, fullCount, "iter must ignore the assigned job range");
                foreach (var range in new[] { new Range(0,0), new Range(0,1), new Range(1,65), new Range(65,91), new Range(91,150), new Range(20,10) })
                {
                    query._range = range;
                    var iterator = query.par_iter();
                    var visited = 0;
                    while (iterator.MoveNext())
                    {
                        var tuple = iterator.Current;
                        var entityId = expected[range.start + visited];
                        Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                        Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                        Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                        visited++;
                    }
                    Assert.AreEqual(Math.Max(0, Math.Min(range.end, expected.Length) - range.start), visited);
                    Assert.IsFalse(iterator.MoveNext());
                }
                var all = new Query<T1, T2, T3>();
                Init(ref all, ref world);
                var allCount = 0;
                foreach (var tuple in all.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    allCount++;
                }
                Assert.AreEqual(137, allCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void Arity4_Inline_FullAndRanges() => Check4<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4>();
        [Test] public void Arity4_Pools_FullAndRanges() => Check4<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4>();
        [Test] public void Arity4_Mixed_FullAndRanges() => Check4<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4>();
        private static void Check4<T1, T2, T3, T4>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    Set<T1>(e, e.id + 10000);
                    Set<T2>(e, e.id + 20000);
                    Set<T3>(e, e.id + 30000);
                    Set<T4>(e, e.id + 40000);
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<T1, T2, T3, T4, None<IntegrationExcluded>>();
                Init(ref query, ref world);
                var expected = new int[query.Count];
                var count = 0;
                foreach (var tuple in query.par_iter())
                {
                    tuple.Deconstruct(out var c1, out var c2, out var c3, out var c4);
                    expected[count++] = *(int*)c1.data - 10000;
                }
                Assert.AreEqual(91, count);
                query._range = new Range(3, 11);
                var fullCount = 0;
                foreach (var tuple in query.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    fullCount++;
                }
                Assert.AreEqual(91, fullCount, "iter must ignore the assigned job range");
                foreach (var range in new[] { new Range(0,0), new Range(0,1), new Range(1,65), new Range(65,91), new Range(91,150), new Range(20,10) })
                {
                    query._range = range;
                    var iterator = query.par_iter();
                    var visited = 0;
                    while (iterator.MoveNext())
                    {
                        var tuple = iterator.Current;
                        var entityId = expected[range.start + visited];
                        Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                        Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                        Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                        Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                        visited++;
                    }
                    Assert.AreEqual(Math.Max(0, Math.Min(range.end, expected.Length) - range.start), visited);
                    Assert.IsFalse(iterator.MoveNext());
                }
                var all = new Query<T1, T2, T3, T4>();
                Init(ref all, ref world);
                var allCount = 0;
                foreach (var tuple in all.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    allCount++;
                }
                Assert.AreEqual(137, allCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void Arity5_Inline_FullAndRanges() => Check5<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4, IntegrationA5>();
        [Test] public void Arity5_Pools_FullAndRanges() => Check5<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4, IntegrationP5>();
        [Test] public void Arity5_Mixed_FullAndRanges() => Check5<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5>();
        private static void Check5<T1, T2, T3, T4, T5>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
            where T5 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    Set<T1>(e, e.id + 10000);
                    Set<T2>(e, e.id + 20000);
                    Set<T3>(e, e.id + 30000);
                    Set<T4>(e, e.id + 40000);
                    Set<T5>(e, e.id + 50000);
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<T1, T2, T3, T4, T5, None<IntegrationExcluded>>();
                Init(ref query, ref world);
                var expected = new int[query.Count];
                var count = 0;
                foreach (var tuple in query.par_iter())
                {
                    tuple.Deconstruct(out var c1, out var c2, out var c3, out var c4, out var c5);
                    expected[count++] = *(int*)c1.data - 10000;
                }
                Assert.AreEqual(91, count);
                query._range = new Range(3, 11);
                var fullCount = 0;
                foreach (var tuple in query.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                    fullCount++;
                }
                Assert.AreEqual(91, fullCount, "iter must ignore the assigned job range");
                foreach (var range in new[] { new Range(0,0), new Range(0,1), new Range(1,65), new Range(65,91), new Range(91,150), new Range(20,10) })
                {
                    query._range = range;
                    var iterator = query.par_iter();
                    var visited = 0;
                    while (iterator.MoveNext())
                    {
                        var tuple = iterator.Current;
                        var entityId = expected[range.start + visited];
                        Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                        Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                        Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                        Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                        Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                        visited++;
                    }
                    Assert.AreEqual(Math.Max(0, Math.Min(range.end, expected.Length) - range.start), visited);
                    Assert.IsFalse(iterator.MoveNext());
                }
                var all = new Query<T1, T2, T3, T4, T5>();
                Init(ref all, ref world);
                var allCount = 0;
                foreach (var tuple in all.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                    allCount++;
                }
                Assert.AreEqual(137, allCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void Arity6_Inline_FullAndRanges() => Check6<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4, IntegrationA5, IntegrationA6>();
        [Test] public void Arity6_Pools_FullAndRanges() => Check6<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4, IntegrationP5, IntegrationP6>();
        [Test] public void Arity6_Mixed_FullAndRanges() => Check6<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5, IntegrationP6>();
        private static void Check6<T1, T2, T3, T4, T5, T6>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
            where T5 : unmanaged, IComponent
            where T6 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    Set<T1>(e, e.id + 10000);
                    Set<T2>(e, e.id + 20000);
                    Set<T3>(e, e.id + 30000);
                    Set<T4>(e, e.id + 40000);
                    Set<T5>(e, e.id + 50000);
                    Set<T6>(e, e.id + 60000);
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<T1, T2, T3, T4, T5, T6, None<IntegrationExcluded>>();
                Init(ref query, ref world);
                var expected = new int[query.Count];
                var count = 0;
                foreach (var tuple in query.par_iter())
                {
                    tuple.Deconstruct(out var c1, out var c2, out var c3, out var c4, out var c5, out var c6);
                    expected[count++] = *(int*)c1.data - 10000;
                }
                Assert.AreEqual(91, count);
                query._range = new Range(3, 11);
                var fullCount = 0;
                foreach (var tuple in query.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                    Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                    fullCount++;
                }
                Assert.AreEqual(91, fullCount, "iter must ignore the assigned job range");
                foreach (var range in new[] { new Range(0,0), new Range(0,1), new Range(1,65), new Range(65,91), new Range(91,150), new Range(20,10) })
                {
                    query._range = range;
                    var iterator = query.par_iter();
                    var visited = 0;
                    while (iterator.MoveNext())
                    {
                        var tuple = iterator.Current;
                        var entityId = expected[range.start + visited];
                        Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                        Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                        Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                        Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                        Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                        Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                        visited++;
                    }
                    Assert.AreEqual(Math.Max(0, Math.Min(range.end, expected.Length) - range.start), visited);
                    Assert.IsFalse(iterator.MoveNext());
                }
                var all = new Query<T1, T2, T3, T4, T5, T6>();
                Init(ref all, ref world);
                var allCount = 0;
                foreach (var tuple in all.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                    Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                    allCount++;
                }
                Assert.AreEqual(137, allCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void Arity7_Inline_FullAndRanges() => Check7<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4, IntegrationA5, IntegrationA6, IntegrationA7>();
        [Test] public void Arity7_Pools_FullAndRanges() => Check7<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4, IntegrationP5, IntegrationP6, IntegrationP7>();
        [Test] public void Arity7_Mixed_FullAndRanges() => Check7<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5, IntegrationP6, IntegrationA7>();
        private static void Check7<T1, T2, T3, T4, T5, T6, T7>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
            where T5 : unmanaged, IComponent
            where T6 : unmanaged, IComponent
            where T7 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    Set<T1>(e, e.id + 10000);
                    Set<T2>(e, e.id + 20000);
                    Set<T3>(e, e.id + 30000);
                    Set<T4>(e, e.id + 40000);
                    Set<T5>(e, e.id + 50000);
                    Set<T6>(e, e.id + 60000);
                    Set<T7>(e, e.id + 70000);
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<T1, T2, T3, T4, T5, T6, T7, None<IntegrationExcluded>>();
                Init(ref query, ref world);
                var expected = new int[query.Count];
                var count = 0;
                foreach (var tuple in query.par_iter())
                {
                    tuple.Deconstruct(out var c1, out var c2, out var c3, out var c4, out var c5, out var c6, out var c7);
                    expected[count++] = *(int*)c1.data - 10000;
                }
                Assert.AreEqual(91, count);
                query._range = new Range(3, 11);
                var fullCount = 0;
                foreach (var tuple in query.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                    Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                    Assert.AreEqual(entityId + 70000, *(int*)tuple.C6.data);
                    fullCount++;
                }
                Assert.AreEqual(91, fullCount, "iter must ignore the assigned job range");
                foreach (var range in new[] { new Range(0,0), new Range(0,1), new Range(1,65), new Range(65,91), new Range(91,150), new Range(20,10) })
                {
                    query._range = range;
                    var iterator = query.par_iter();
                    var visited = 0;
                    while (iterator.MoveNext())
                    {
                        var tuple = iterator.Current;
                        var entityId = expected[range.start + visited];
                        Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                        Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                        Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                        Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                        Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                        Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                        Assert.AreEqual(entityId + 70000, *(int*)tuple.C6.data);
                        visited++;
                    }
                    Assert.AreEqual(Math.Max(0, Math.Min(range.end, expected.Length) - range.start), visited);
                    Assert.IsFalse(iterator.MoveNext());
                }
                var all = new Query<T1, T2, T3, T4, T5, T6, T7>();
                Init(ref all, ref world);
                var allCount = 0;
                foreach (var tuple in all.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                    Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                    Assert.AreEqual(entityId + 70000, *(int*)tuple.C6.data);
                    allCount++;
                }
                Assert.AreEqual(137, allCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void Arity8_Inline_FullAndRanges() => Check8<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4, IntegrationA5, IntegrationA6, IntegrationA7, IntegrationA8>();
        [Test] public void Arity8_Pools_FullAndRanges() => Check8<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4, IntegrationP5, IntegrationP6, IntegrationP7, IntegrationP8>();
        [Test] public void Arity8_Mixed_FullAndRanges() => Check8<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5, IntegrationP6, IntegrationA7, IntegrationP8>();
        private static void Check8<T1, T2, T3, T4, T5, T6, T7, T8>()
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent
            where T4 : unmanaged, IComponent
            where T5 : unmanaged, IComponent
            where T6 : unmanaged, IComponent
            where T7 : unmanaged, IComponent
            where T8 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8));
                for (var i = 0; i < 137; i++)
                {
                    var e = arch.CreateEntity();
                    Set<T1>(e, e.id + 10000);
                    Set<T2>(e, e.id + 20000);
                    Set<T3>(e, e.id + 30000);
                    Set<T4>(e, e.id + 40000);
                    Set<T5>(e, e.id + 50000);
                    Set<T6>(e, e.id + 60000);
                    Set<T7>(e, e.id + 70000);
                    Set<T8>(e, e.id + 80000);
                    if (i % 3 == 0) e.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<T1, T2, T3, T4, T5, T6, T7, T8, None<IntegrationExcluded>>();
                Init(ref query, ref world);
                var expected = new int[query.Count];
                var count = 0;
                foreach (var tuple in query.par_iter())
                {
                    tuple.Deconstruct(out var c1, out var c2, out var c3, out var c4, out var c5, out var c6, out var c7, out var c8);
                    expected[count++] = *(int*)c1.data - 10000;
                }
                Assert.AreEqual(91, count);
                query._range = new Range(3, 11);
                var fullCount = 0;
                foreach (var tuple in query.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                    Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                    Assert.AreEqual(entityId + 70000, *(int*)tuple.C6.data);
                    Assert.AreEqual(entityId + 80000, *(int*)tuple.C7.data);
                    fullCount++;
                }
                Assert.AreEqual(91, fullCount, "iter must ignore the assigned job range");
                foreach (var range in new[] { new Range(0,0), new Range(0,1), new Range(1,65), new Range(65,91), new Range(91,150), new Range(20,10) })
                {
                    query._range = range;
                    var iterator = query.par_iter();
                    var visited = 0;
                    while (iterator.MoveNext())
                    {
                        var tuple = iterator.Current;
                        var entityId = expected[range.start + visited];
                        Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                        Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                        Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                        Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                        Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                        Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                        Assert.AreEqual(entityId + 70000, *(int*)tuple.C6.data);
                        Assert.AreEqual(entityId + 80000, *(int*)tuple.C7.data);
                        visited++;
                    }
                    Assert.AreEqual(Math.Max(0, Math.Min(range.end, expected.Length) - range.start), visited);
                    Assert.IsFalse(iterator.MoveNext());
                }
                var all = new Query<T1, T2, T3, T4, T5, T6, T7, T8>();
                Init(ref all, ref world);
                var allCount = 0;
                foreach (var tuple in all.iter())
                {
                    var entityId = *(int*)tuple.C0.data - 10000;
                    Assert.AreEqual(entityId + 10000, *(int*)tuple.C0.data);
                    Assert.AreEqual(entityId + 20000, *(int*)tuple.C1.data);
                    Assert.AreEqual(entityId + 30000, *(int*)tuple.C2.data);
                    Assert.AreEqual(entityId + 40000, *(int*)tuple.C3.data);
                    Assert.AreEqual(entityId + 50000, *(int*)tuple.C4.data);
                    Assert.AreEqual(entityId + 60000, *(int*)tuple.C5.data);
                    Assert.AreEqual(entityId + 70000, *(int*)tuple.C6.data);
                    Assert.AreEqual(entityId + 80000, *(int*)tuple.C7.data);
                    allCount++;
                }
                Assert.AreEqual(137, allCount);
            }
            finally { world.Dispose(); }
        }
        [Test]
        public void EntityAndTagSlots_UseLogicalRows()
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                for (var i = 0; i < 73; i++)
                {
                    var e = world.GetArchetype(typeof(IntegrationP1), typeof(IntegrationExcluded)).CreateEntity();
                    e.Get<IntegrationP1>().Value = e.id;
                }
                var query = new Query<Entity, IntegrationP1, IntegrationExcluded>();
                Init(ref query, ref world);
                var count = 0;
                foreach (var (entity, pool, tag) in query.iter())
                {
                    Assert.AreEqual(entity.id, pool.Read.Value);
                    Assert.AreEqual((IntPtr)TagSlotStub<IntegrationExcluded>.GetPtr(), (IntPtr)tag.data);
                    count++;
                }
                Assert.AreEqual(73, count);
            }
            finally { world.Dispose(); }
        }
    }
}
