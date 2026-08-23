using System.Diagnostics;
using NUnit.Framework;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests
{
    // Diagnostic: how much do attached queries cost per structural change?
    // This is exactly the bookkeeping a row-bitmap membership would remove.
    // Run single test, read [DIAG-MIG] lines in the log.
    public struct MgPos : IComponent { public Unity.Mathematics.float3 Value; }
    public struct MgVel : IComponent { public Unity.Mathematics.float3 Value; }
    public struct MgHp : IComponent { public float Value; }
    public struct MgT0 : IComponent { } public struct MgT1 : IComponent { }
    public struct MgT2 : IComponent { } public struct MgT3 : IComponent { }
    public struct MgT4 : IComponent { } public struct MgT5 : IComponent { }
    public struct MgT6 : IComponent { } public struct MgT7 : IComponent { }
    public struct MgT8 : IComponent { } public struct MgT9 : IComponent { }

    [TestFixture]
    public class MigrationDiagnosticsTests
    {
        [Test]
        public void Migration_Query_Sensitivity()
        {
            const int n = 10000;
            RunScenario(n, 0, "q=0   ");
            RunScenario(n, 50, "q=50  ");
            RunScenario(n, 150, "q=150 ");
        }

        [Test]
        public void Migration_Current_Vs_Edges_Vs_Bitmap()
        {
            const int n = 10000;
            // 1) micro-benchmark of the bookkeeping alone (exact copy of BatchMigrateQueries logic)
            MicroBookkeeping(n, 50, "q=50  ");
            MicroBookkeeping(n, 150, "q=150 ");

            // 2) real world, structural-only (bookkeeping bypassed — the bitmap floor)
            RunScenarioNoAccount(n, 50, "q=50  noaccount");
            RunScenarioNoAccount(n, 150, "q=150 noaccount");
        }

        /// <summary>
        /// Simulates the three bookkeeping strategies 1:1 (no world):
        /// - current: for each entity — two passes over attached query lists with linear Contains
        /// - edges:   precomputed per-transition remove/add lists, linear application per entity
        /// - bitmap:  nothing per entity; counts recomputed on demand (Q popcounts per read)
        /// Setup mirrors the world test: source LA has Q attached queries, target shares half.
        /// </summary>
        private static unsafe void MicroBookkeeping(int entities, int q, string label)
        {
            const int reps = 20;

            // source: queries 0..Q-1; target: queries Q/4..3Q/4 (half shared)
            var fromQ = new int[q];
            for (var i = 0; i < q; i++) fromQ[i] = i;
            var toQ = new int[q / 2];
            for (var i = 0; i < q / 2; i++) toQ[i] = i + q / 4;

            // edge precomputation (once per transition pair, like CreateTransaction)
            var removeList = new int[q - toQ.Length];
            var ri = 0;
            foreach (var f in fromQ)
                if (System.Array.IndexOf(toQ, f) < 0) removeList[ri++] = f;
            var addList = new int[toQ.Length];
            var ai = 0;
            foreach (var t in toQ)
                if (System.Array.IndexOf(fromQ, t) < 0) addList[ai++] = t;

            var counts = new int[q];

            // inline linear search — mirrors MemoryList<int>.Contains shape (no managed Array.IndexOf)
            bool Contains(int[] arr, int len, int v)
            {
                for (var i = 0; i < len; i++)
                    if (arr[i] == v) return true;
                return false;
            }

            // --- current (square Contains, exact shape of Archetype.BatchMigrateQueries) ---
            long acc0 = 0;
            MeasureLocal(reps, () =>
            {
                for (var e = 0; e < entities; e++)
                {
                    for (var i = 0; i < fromQ.Length; i++)
                        if (!Contains(toQ, toQ.Length, fromQ[i])) { counts[fromQ[i]]--; acc0++; }
                    for (var i = 0; i < toQ.Length; i++)
                        if (!Contains(fromQ, fromQ.Length, toQ[i])) { counts[toQ[i]]++; acc0++; }
                }
            }, out var tCurrent);

            // --- edges (precomputed lists, linear application) ---
            MeasureLocal(reps, () =>
            {
                for (var e = 0; e < entities; e++)
                {
                    for (var i = 0; i < removeList.Length; i++) counts[removeList[i]]--;
                    for (var i = 0; i < addList.Length; i++) counts[addList[i]]++;
                }
            }, out var tEdges);

            // --- bitmap (no per-entity work; counts read as popcount on demand) ---
            MeasureLocal(reps, () =>
            {
                // per cycle: only the "read" — Q counts recomputed from a row mask
                ulong mask = 0;
                for (var e = 0; e < 640; e++) mask |= 1UL << (e & 63); // simulate dense mask words
                for (var i = 0; i < q; i++) counts[i] = Unity.Mathematics.math.countbits(mask);
            }, out var tBitmap);

            dbug.log($"[DIAG-MIG2] {label} current={tCurrent:F4} ms  edges={tEdges:F4} ms  bitmap={tBitmap:F4} ms  (per {entities} entities, {reps} reps, acc={acc0 & 1})");
        }

        private static void RunScenarioNoAccount(int entityCount, int queryCount, string label)
        {
            var world = World.Create(WorldConfig.Default256000);
            AttachQueries(world, queryCount);
            var ids = new int[entityCount];
            for (var i = 0; i < entityCount; i++)
            {
                var e = world.Entity(new MgPos { Value = default });
                ids[i] = e.id;
            }
            world.Update();

            var worldId = world.Id;
            const int reps = 20;
            MigrateOnce(worldId, ids, entityCount);
            MigrateOnce(worldId, ids, entityCount);

            var prev = QueryBookkeepingBypass.Disabled;
            QueryBookkeepingBypass.Disabled = true;
            try
            {
                var sw = Stopwatch.StartNew();
                for (var r = 0; r < reps; r++)
                    MigrateOnce(worldId, ids, entityCount);
                sw.Stop();
                dbug.log($"[DIAG-MIG2] {label} world-structural-only={sw.Elapsed.TotalMilliseconds / reps:F4} ms per cycle ({reps} reps)");
            }
            finally
            {
                QueryBookkeepingBypass.Disabled = prev;
            }
            world.Dispose();
        }

        private static void MeasureLocal(int reps, System.Action action, out double ms)
        {
            action();
            action();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < reps; i++) action();
            sw.Stop();
            ms = sw.Elapsed.TotalMilliseconds / reps;
        }

        private static void RunScenario(int entityCount, int queryCount, string label)
        {
            var world = World.Create(WorldConfig.Default256000);
            // queries FIRST (real-world order: systems init before spawning) —
            // so every archetype created later attaches them via PopulateQueries
            AttachQueries(world, queryCount);
            var ids = new int[entityCount];
            for (var i = 0; i < entityCount; i++)
            {
                var e = world.Entity(new MgPos { Value = default });
                ids[i] = e.id;
            }
            world.Update();

            var worldId = world.Id;
            const int reps = 20;

            // warmup
            MigrateOnce(worldId, ids, entityCount);
            MigrateOnce(worldId, ids, entityCount);

            var sw = Stopwatch.StartNew();
            MigrationStats.Fills = 0; MigrationStats.RemoveLenSum = 0; MigrationStats.AddLenSum = 0;
            MigrationStats.Removes = 0;
            MigrationStats.Adds = 0;
            for (var r = 0; r < reps; r++)
                MigrateOnce(worldId, ids, entityCount);
            sw.Stop();

            dbug.log($"[DIAG-MIG] {label} avg={sw.Elapsed.TotalMilliseconds / reps:F4} ms per add-cycle of {entityCount} entities ({reps} reps) " +
                     $"| fills={MigrationStats.Fills} qRemove={MigrationStats.Removes} qAdd={MigrationStats.Adds} " +
                     $"| avgRemoveLen={(MigrationStats.RemoveLenSum / (double)System.Math.Max(1, MigrationStats.Removes)):F1}");
            world.Dispose();
        }

        /// <summary>Creates unique queries that all attach to the {MgPos} archetype
        /// (None-combinations of tags the entities do not have).</summary>
        private static void AttachQueries(World world, int queryCount)
        {
            if (queryCount <= 0) return;
            int made = 0;
            // single None
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT0>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT1>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT2>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT3>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT4>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT5>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT6>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT7>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT8>(); made++; }
            if (made < queryCount) { world.Query().With<MgPos>().None<MgT9>(); made++; }
            // pairs
            for (var i = 0; i < 10 && made < queryCount; i++)
            for (var j = i + 1; j < 10 && made < queryCount; j++)
            {
                var t0 = TagByIndex(i);
                var t1 = TagByIndex(j);
                var q = world.Query().With<MgPos>().None(t0);
                if (t1 != t0) q.None(t1);
                made++;
            }
            // triples
            for (var i = 0; i < 10 && made < queryCount; i++)
            for (var j = i + 1; j < 10 && made < queryCount; j++)
            for (var k = j + 1; k < 10 && made < queryCount; k++)
            {
                world.Query().With<MgPos>()
                    .None(TagByIndex(i)).None(TagByIndex(j)).None(TagByIndex(k));
                made++;
            }
        }

        private static void MigrateOnce(int worldId, int[] ids, int count)
        {
            ref var world = ref World.Get(worldId);
            for (var i = 0; i < count; i++)
            {
                ref var e = ref world.GetEntity(ids[i]);
                if (i % 2 == 0)
                    e.Add(new MgVel { Value = new Unity.Mathematics.float3(1, 0, 0) });
                else
                    e.Add(new MgHp { Value = 100 });
            }
            world.Update();
            // remove back so the cycle can repeat
            for (var i = 0; i < count; i++)
            {
                ref var e = ref world.GetEntity(ids[i]);
                if (i % 2 == 0)
                    e.Remove<MgVel>();
                else
                    e.Remove<MgHp>();
            }
            world.Update();
        }

        private static int TagByIndex(int i)
        {
            return i switch
            {
                0 => ComponentType<MgT0>.Index,
                1 => ComponentType<MgT1>.Index,
                2 => ComponentType<MgT2>.Index,
                3 => ComponentType<MgT3>.Index,
                4 => ComponentType<MgT4>.Index,
                5 => ComponentType<MgT5>.Index,
                6 => ComponentType<MgT6>.Index,
                7 => ComponentType<MgT7>.Index,
                8 => ComponentType<MgT8>.Index,
                _ => ComponentType<MgT9>.Index,
            };
        }
    }
}
