using System.Diagnostics;
using NUnit.Framework;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests
{
    // Diagnostic: decomposes managed iteration cost by layer.
    // Run this single test and read the [DIAG] lines in the log.
    public struct DbgPos : IComponent { public float X, Y, Z; }
    public struct DbgVel : IComponent { public float X, Y, Z; }
    public struct DbgC1 : IComponent { public Unity.Mathematics.float3 val; }
    public struct DbgC2 : IComponent { public Unity.Mathematics.float3 val; }
    public struct DbgC3 : IComponent { public Unity.Mathematics.float3 val; }
    public struct DbgC4 : IComponent { public Unity.Mathematics.float3 val; }

    public static class DbgIterSystems
    {
        [System]
        public static void ManagedIterSystem(ref Query<DbgPos, DbgVel> query, ref State state)
        {
            foreach (var (p, v) in query.iter())
                p.Get.X += v.Read.X * state.Time.DeltaTime;
        }

        [System, Unity.Burst.BurstCompile]
        public static void BurstIterSystem(ref Query<DbgPos, DbgVel> query, ref State state)
        {
            foreach (var (p, v) in query.iter())
                p.Get.X += v.Read.X * state.Time.DeltaTime;
        }

        // exact bench shape: 4 float3 components, 2 write + 2 read
        [System]
        public static void ManagedIter4System(ref Query<DbgC1, DbgC2, DbgC3, DbgC4> query)
        {
            foreach (var (c1, c2, c3, c4) in query.iter())
            {
                c1.Get.val += c2.Read.val;
                c3.Get.val += c4.Read.val;
            }
        }

        [System, Unity.Burst.BurstCompile]
        public static void BurstIter4System(ref Query<DbgC1, DbgC2, DbgC3, DbgC4> query)
        {
            foreach (var (c1, c2, c3, c4) in query.iter())
            {
                c1.Get.val += c2.Read.val;
                c3.Get.val += c4.Read.val;
            }
        }

        // arity ladder: same components, 2 → 3 slots
        [System]
        public static void ManagedIter2CSystem(ref Query<DbgC1, DbgC2> query)
        {
            foreach (var (c1, c2) in query.iter())
                c1.Get.val += c2.Read.val;
        }

        [System]
        public static void ManagedIter3CSystem(ref Query<DbgC1, DbgC2, DbgC3> query)
        {
            foreach (var (c1, c2, c3) in query.iter())
            {
                c1.Get.val += c2.Read.val;
                c1.Get.val += c3.Read.val;
            }
        }

        [System]
        public static unsafe void ManagedIter4PtrSystem(ref Query<DbgC1, DbgC2, DbgC3, DbgC4> query)
        {
            foreach (var (c1, c2, c3, c4) in query.iter_unsafe())
            {
                c1->val += c2->val;
                c3->val += c4->val;
            }
        }

        [System]
        public static unsafe void ManagedIter2PtrSystem(ref Query<DbgC1, DbgC2> query)
        {
            foreach (var (c1, c2) in query.iter_unsafe())
                c1->val += c2->val;
        }

        // empty bodies — isolate enumerator+tuple cost from body property access
        [System]
        public static void ManagedIter4Empty(ref Query<DbgC1, DbgC2, DbgC3, DbgC4> query, ref State state)
        {
            var acc = 0f;
            foreach (var (c1, c2, c3, c4) in query.iter())
                acc += c1.Get.val.x;
            if (acc == float.MinValue) dbug.log("never");
        }

        [System]
        public static void ManagedIter2CEmpty(ref Query<DbgC1, DbgC2> query, ref State state)
        {
            var acc = 0f;
            foreach (var (c1, c2) in query.iter())
                acc += c1.Get.val.x;
            if (acc == float.MinValue) dbug.log("never");
        }

        // isolate Current-copy (no Deconstruct, direct tuple field access)
        [System]
        public static unsafe void ManagedIter4CurrentOnly(ref Query<DbgC1, DbgC2, DbgC3, DbgC4> query, ref State state)
        {
            var acc = 0f;
            foreach (var t in query.iter())
                acc += t._p1.data->val.x;
            if (acc == float.MinValue) dbug.log("never");
        }

        // isolate Deconstruct (deconstruction, empty body)
        [System]
        public static void ManagedIter4DeconstructOnly(ref Query<DbgC1, DbgC2, DbgC3, DbgC4> query, ref State state)
        {
            var acc = 0f;
            foreach (var (c1, c2, c3, c4) in query.iter())
                acc += c1.Read.val.x;
            if (acc == float.MinValue) dbug.log("never");
        }

        [System]
        public static unsafe void RawStorageLoopSystem(ref Query<DbgPos, DbgVel> query, ref State state)
        {
            query.TryGetQuery(out var qi);
            var w = state.World.UnsafeWorld;
            var storages = qi.Ref.GetMatchingStorages();
            var dt = state.Time.DeltaTime;
            for (var s = 0; s < storages.length; s++)
            {
                ref var st = ref w->storagesList.Ptr[storages.Ptr[s]].Ref;
                var pBase = (DbgPos*)(st.data.Ptr + st.GetComponentOffset(st.GetComponentLocalIndex(ComponentType<DbgPos>.Index)));
                var vBase = (DbgVel*)(st.data.Ptr + st.GetComponentOffset(st.GetComponentLocalIndex(ComponentType<DbgVel>.Index)));
                var count = st.count;
                for (var i = 0; i < count; i++)
                    pBase[i].X += vBase[i].X * dt;
            }
        }
    }

    [TestFixture]
    public class IterationDiagnosticsTests
    {
        [Test]
        public void Decompose_Iteration_Cost()
        {
            const int n = 100000;
            var world = World.Create(WorldConfig.Default_1_000_000);
            var q = world.Query().With<DbgPos>();

            var ents = world.BatchCreateEntity(n);
            for (var i = 0; i < ents.Length; i++)
            {
                ents[i].Add(new DbgPos { X = i });
                ents[i].Add(new DbgVel { X = 1f });
            }
            // 4-component entities — exact bench shape (C1..C4 float3)
            var ents4 = world.BatchCreateEntity(n);
            var v = new Unity.Mathematics.float3(1, 2, 3);
            for (var i = 0; i < ents4.Length; i++)
            {
                ents4[i].Add(new DbgC1 { val = v });
                ents4[i].Add(new DbgC2 { val = v });
                ents4[i].Add(new DbgC3 { val = v });
                ents4[i].Add(new DbgC4 { val = v });
            }
            world.Update();
            Assert.AreEqual(n, q.Count);

            var systemsIter = new Systems(ref world);
            systemsIter.Add(DbgIterSystems.ManagedIterSystem, Threads.Main);
            var systemsBurstIter = new Systems(ref world);
            systemsBurstIter.Add(DbgIterSystems.BurstIterSystem, Threads.Main);
            var systemsIter4 = new Systems(ref world);
            systemsIter4.Add(DbgIterSystems.ManagedIter4System, Threads.Main);
            var systemsBurstIter4 = new Systems(ref world);
            systemsBurstIter4.Add(DbgIterSystems.BurstIter4System, Threads.Main);
            var systemsRaw = new Systems(ref world);
            systemsRaw.Add(DbgIterSystems.RawStorageLoopSystem, Threads.Main);

            Measure(30, () => systemsIter.OnUpdate(0.016f, 0f), "managed iter() system  ");
            Measure(30, () => systemsBurstIter.OnUpdate(0.016f, 0f), "BURST+iter() system    ");
            Measure(30, () => systemsIter4.OnUpdate(0.016f, 0f), "managed iter4() system ");
            Measure(30, () => systemsBurstIter4.OnUpdate(0.016f, 0f), "BURST+iter4() system   ");
            Measure(30, () => systemsRaw.OnUpdate(0.016f, 0f), "raw storage for system ");

            // arity ladder + ptr family on the same DbgC entities
            var systemsIter2C = new Systems(ref world);
            systemsIter2C.Add(DbgIterSystems.ManagedIter2CSystem, Threads.Main);
            var systemsIter3C = new Systems(ref world);
            systemsIter3C.Add(DbgIterSystems.ManagedIter3CSystem, Threads.Main);
            var systemsIter4Ptr = new Systems(ref world);
            systemsIter4Ptr.Add(DbgIterSystems.ManagedIter4PtrSystem, Threads.Main);
            var systemsIter2Ptr = new Systems(ref world);
            systemsIter2Ptr.Add(DbgIterSystems.ManagedIter2PtrSystem, Threads.Main);
            Measure(30, () => systemsIter2C.OnUpdate(0.016f, 0f), "iter2 C1C2 (Ref)       ");
            Measure(30, () => systemsIter3C.OnUpdate(0.016f, 0f), "iter3 C1C2C3 (Ref)     ");
            Measure(30, () => systemsIter2Ptr.OnUpdate(0.016f, 0f), "iter2 C1C2 (Ptr)       ");
            Measure(30, () => systemsIter4Ptr.OnUpdate(0.016f, 0f), "iter4 (Ptr)             ");

            var systemsIter4E = new Systems(ref world);
            systemsIter4E.Add(DbgIterSystems.ManagedIter4Empty, Threads.Main);
            var systemsIter2E = new Systems(ref world);
            systemsIter2E.Add(DbgIterSystems.ManagedIter2CEmpty, Threads.Main);
            Measure(30, () => systemsIter4E.OnUpdate(0.016f, 0f), "iter4 EMPTY+1 read     ");
            Measure(30, () => systemsIter2E.OnUpdate(0.016f, 0f), "iter2 EMPTY+1 read     ");

            var systemsCurrentOnly = new Systems(ref world);
            systemsCurrentOnly.Add(DbgIterSystems.ManagedIter4CurrentOnly, Threads.Main);
            var systemsDeconstructOnly = new Systems(ref world);
            systemsDeconstructOnly.Add(DbgIterSystems.ManagedIter4DeconstructOnly, Threads.Main);
            Measure(30, () => systemsCurrentOnly.OnUpdate(0.016f, 0f), "iter4 CurrentOnly      ");
            Measure(30, () => systemsDeconstructOnly.OnUpdate(0.016f, 0f), "iter4 DeconstructOnly  ");

            var sum = 0;
            Measure(30, () => { foreach (ref var e in q) sum++; }, "fluent enumerator empty");

            world.Dispose();
            Assert.Pass($"sum={sum}");
        }

        private static void Measure(int iterations, System.Action action, string label)
        {
            action();
            action();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++) action();
            sw.Stop();
            dbug.log($"[DIAG] {label} avg={sw.Elapsed.TotalMilliseconds / iterations:F4} ms (n={iterations})");
        }
    }
}
