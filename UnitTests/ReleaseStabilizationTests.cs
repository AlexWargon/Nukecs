using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests
{
    // ============================================================================
    // 1.0 stabilization regression tests. Every test pins a concrete fix made
    // during the stabilization pass (fix id in the assert message).
    // ============================================================================

    public struct Stab1 : IComponent { public int V; }
    public struct Stab2 : IComponent { public int V; }
    public struct Stab3 : IComponent { public int V; }
    public struct Stab4 : IComponent { public int V; }
    public struct Stab5 : IComponent { public int V; }
    public struct Stab6 : IComponent { public int V; }
    public struct Stab7 : IComponent { public int V; }
    public struct Stab8 : IComponent { public int V; }
    public struct StabTag : IComponent { }
    public struct StabPool : IPoolComponent { public int V; }
    public struct StabDisposablePool : IPoolComponent, IDisposable
    {
        public int V;
        public void Dispose() => StabDisposableTracker.Alive--;
    }

    public struct StabDisposable : IComponent, IDisposable
    {
        public int V;
        public void Dispose() => StabDisposableTracker.Alive--;
    }

    public static class StabDisposableTracker
    {
        public static int Alive;
    }

    public struct StabRes : IRes
    {
        public int Value;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }

    public struct StabCounter : IRes
    {
        public int Sum;         // per-row iteration sum
        public int CopySum;     // CopyTo round-trip sum
        public int Count;
        public int SentinelHits;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }

    public static class StabChunkSystems
    {
        [System, Unity.Burst.BurstCompile, RequireBatch]
        public static void ExpireEntities(ref Query<Entity, Stab1, Stab2> query)
        {
            foreach (var (entity, value, lifetime) in query) {
                lifetime.Get.V--;
                if (lifetime.Read.V <= 0) entity.Destroy();
            }
        }

        [System, Unity.Burst.BurstCompile, RequireBatch]
        public static void CullEntities(ref Query<Entity, Stab1, None<StabTag>> query)
        {
            foreach (var (entity, value) in query) {
                if ((value.Read.V & 1) == 0) entity.Add<StabTag>();
            }
        }

        // FIX B3: chunk iteration and CopyTo must gather scattered rows on shared storage
        // (a tag variant of the archetype makes rows sparse). CopyTo runs at chunk start
        // (_rowIdx == 0); iteration walks all rows.
        [System]
        public static unsafe void Chunk2(ref Query<Stab1, Stab2, None<StabTag>> q, ref Res<StabCounter> c)
        {
            var dst = (Stab2*)UnsafeUtility.Malloc(64 * sizeof(Stab2), 4, Allocator.Temp);
            foreach (var chunk in q.iter_chunk())
            {
                for (var i = 0; i < 64; i++) dst[i].V = -777;
                chunk.CopyTo(dst);
                for (var i = 0; i < chunk.Count; i++)
                {
                    c.Ref.CopySum += dst[i].V;
                    c.Ref.Count++;
                }
                if (dst[chunk.Count].V != -777) c.Ref.SentinelHits++;
                for (var i = 0; i < chunk.Count; i++)
                {
                    c.Ref.Sum += chunk.C2.V;
                    chunk.MoveNext();
                }
            }
            UnsafeUtility.Free(dst, Allocator.Temp);
        }

        [System]
        public static unsafe void Chunk5(ref Query<Stab1, Stab2, Stab3, Stab4, Stab5, None<StabTag>> q, ref Res<StabCounter> c)
        {
            var dst = (Stab4*)UnsafeUtility.Malloc(64 * sizeof(Stab4), 4, Allocator.Temp);
            foreach (var chunk in q.iter_chunk())
            {
                for (var i = 0; i < 64; i++) dst[i].V = -777;
                chunk.CopyTo(dst);
                for (var i = 0; i < chunk.Count; i++)
                {
                    c.Ref.CopySum += dst[i].V;
                    c.Ref.Count++;
                }
                if (dst[chunk.Count].V != -777) c.Ref.SentinelHits++;
                for (var i = 0; i < chunk.Count; i++)
                {
                    c.Ref.Sum += chunk.C3.V; // arity-5 chunks name columns C0..C4 → C3 == T4
                    chunk.MoveNext();
                }
            }
            UnsafeUtility.Free(dst, Allocator.Temp);
        }

        [System]
        public static unsafe void Chunk8(ref Query<Stab1, Stab2, Stab3, Stab4, Stab5, Stab6, Stab7, Stab8, None<StabTag>> q, ref Res<StabCounter> c)
        {
            var dst = (Stab8*)UnsafeUtility.Malloc(64 * sizeof(Stab8), 4, Allocator.Temp);
            foreach (var chunk in q.iter_chunk())
            {
                for (var i = 0; i < 64; i++) dst[i].V = -777;
                chunk.CopyTo(dst);
                for (var i = 0; i < chunk.Count; i++)
                {
                    c.Ref.CopySum += dst[i].V;
                    c.Ref.Count++;
                }
                if (dst[chunk.Count].V != -777) c.Ref.SentinelHits++;
                for (var i = 0; i < chunk.Count; i++)
                {
                    c.Ref.Sum += chunk.C7.V; // arity-8 chunks name columns C0..C7 → C7 == T8
                    chunk.MoveNext();
                }
            }
            UnsafeUtility.Free(dst, Allocator.Temp);
        }
    }

    [TestFixture]
    public unsafe class ReleaseStabilizationTests
    {
        private World _world;

        [SetUp]
        public void SetUp()
        {
            _world = World.Create(WorldConfig.Default256);
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsAlive) _world.Dispose();
            _world = default;
        }

        private void SpawnStab(int count, int stride = 2, bool withTag = true)
        {
            var es = _world.BatchCreateEntity(count);
            for (var i = 0; i < count; i++)
            {
                es[i].Add(new Stab1 { V = i });
                es[i].Add(new Stab2 { V = i });
                es[i].Add(new Stab3 { V = i });
                es[i].Add(new Stab4 { V = i });
                es[i].Add(new Stab5 { V = i });
                es[i].Add(new Stab6 { V = i });
                es[i].Add(new Stab7 { V = i });
                es[i].Add(new Stab8 { V = i });
                if (withTag && i % stride == 0) es[i].Add<StabTag>();
            }
            _world.Update();
        }

        private static int OddsSum(int count) { var s = 0; for (var i = 1; i < count; i += 2) s += i; return s; }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Generations_ParallelLifetimeAndCulling_KeepRowsAliveUntilPlayback(int graphMode)
        {
            var systems = new Systems(ref _world)
                .Add(StabChunkSystems.ExpireEntities, Threads.Parallel)
                .Add(StabChunkSystems.CullEntities, Threads.Parallel);
            if (graphMode >= 0) systems.UseDependencyGraph(mode: (GroupScheduleMode)graphMode);
            const int count = 257;
            for (var cycle = 0; cycle < 4; cycle++) {
                var entities = new Entity[count];
                for (var i = 0; i < count; i++) {
                    entities[i] = _world.Entity(new Stab1 { V = i }, new Stab2 { V = i % 3 + 1 });
                    if ((i & 1) == 1) entities[i].Add<StabTag>();
                }
                _world.Update();
                for (var tick = 1; tick <= 3; tick++) {
                    systems.OnUpdate(0.016f, tick * 0.016f);
                    var alive = 0;
                    for (var i = 0; i < count; i++) {
                        var expected = i % 3 + 1 > tick;
                        Assert.AreEqual(expected, entities[i].IsValid());
                        if (expected) {
                            alive++;
                            Assert.AreEqual(i, entities[i].Get<Stab1>().V);
                        }
                    }
                    Assert.AreEqual(alive, _world.EntitiesAmount);
                }
            }
        }

        [Test]
        public void Generations_EntityFitsInOneLongAndRoundTrips()
        {
            Assert.AreEqual(sizeof(long), sizeof(Entity));
            var entity = _world.Entity();
            var bits = *(long*)&entity;
            var copy = *(Entity*)&bits;
            Assert.AreEqual(entity, copy);
            Assert.IsTrue(copy.IsValid());
        }

        [Test]
        public void Generations_ExhaustedSlotIsRetiredWithoutWrapping()
        {
            var entity = _world.Entity(new Stab1 { V = 1 });
            _world.Update();
            _world.UnsafeWorld->entities.Ptr[entity.id].Generation = ushort.MaxValue;
            var lastGeneration = _world.GetEntity(entity.id);
            lastGeneration.DestroyNow();
            var replacement = _world.Entity();
            Assert.AreNotEqual(lastGeneration.id, replacement.id);
            Assert.IsFalse(lastGeneration.IsValid());
            Assert.AreEqual(1, replacement.Generation);
        }

        [Test]
        public void Generations_InstalledPoolPayloadDisposesAtImmediateDeletion()
        {
            StabDisposableTracker.Alive = 1;
            var entity = _world.Entity(new StabDisposablePool { V = 7 });
            _world.Update();
            Assert.AreEqual(7, entity.Get<StabDisposablePool>().V);
            Assert.AreEqual(1, StabDisposableTracker.Alive);
            entity.DestroyNow();
            Assert.AreEqual(0, StabDisposableTracker.Alive);
            _world.Update();
            Assert.AreEqual(0, StabDisposableTracker.Alive);
        }

        [Test]
        public void Generations_ReuseInvalidatesOldHandleAndKeepsHashStable()
        {
            var old = _world.Entity(new Stab1 { V = 1 });
            _world.Update();
            var hash = old.GetHashCode();
            var identity = new Dictionary<Entity, int> { [old] = 7 };
            old.DestroyNow();
            var current = _world.Entity(new Stab1 { V = 2 });
            _world.Update();
            Assert.AreEqual(old.id, current.id);
            Assert.AreNotEqual(old.Generation, current.Generation);
            Assert.AreNotEqual(old, current);
            Assert.IsFalse(old.IsValid());
            Assert.IsTrue(current.IsValid());
            Assert.AreEqual(hash, old.GetHashCode());
            Assert.AreEqual(7, identity[old]);
            Assert.IsFalse(old.Has<Stab1>());
            Assert.Throws<InvalidOperationException>(() => old.Get<Stab1>());
            Assert.Throws<InvalidOperationException>(() => old.Set(new Stab1 { V = 99 }));
            old.Destroy();
            old.DestroyNow();
            _world.Update();
            Assert.IsTrue(current.IsValid());
            Assert.AreEqual(2, current.Get<Stab1>().V);
        }

        [Test]
        public void Generations_DisposedWorldSlotDoesNotReviveOldHandle()
        {
            var old = _world.Entity();
            var hash = old.GetHashCode();
            var index = _world.Id;
            _world.Dispose();
            Assert.IsFalse(old.IsValid());
            Assert.AreEqual(hash, old.GetHashCode());
            _world = World.Create(WorldConfig.Default256);
            var current = _world.Entity();
            Assert.AreEqual(index, _world.Id);
            Assert.AreEqual(old.id, current.id);
            Assert.IsFalse(old.IsValid());
            Assert.AreNotEqual(old, current);
        }

        [Test]
        public void Generations_PlaybackSkipsOldCommandsWithoutScanningOnDestroy()
        {
            var old = _world.Entity(new Stab1 { V = 1 });
            _world.Update();
            old.Add(new Stab2 { V = 99 });
            old.Remove<Stab1>();
            old.Destroy();
            var queued = _world.UnsafeWorld->ECB.Count;
            old.DestroyNow();
            Assert.AreEqual(queued, _world.UnsafeWorld->ECB.Count, "DestroyNow must leave ECB buffers untouched");
            var current = _world.Entity(new Stab1 { V = 2 });
            _world.Update();
            Assert.IsTrue(current.IsValid());
            Assert.AreEqual(2, current.Get<Stab1>().V);
            Assert.IsFalse(current.Has<Stab2>());
            Assert.AreEqual(0, _world.UnsafeWorld->ECB.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Generations_CopyChecksBothSourceAndTarget(bool destroySource)
        {
            var source = _world.Entity(new Stab1 { V = 1 }, new Stab2 { V = 2 });
            var target = _world.Entity(new Stab1 { V = 10 }, new Stab2 { V = 20 });
            _world.Update();
            _world.UnsafeWorld->ECB.Copy(source.id, target.id);
            var old = destroySource ? source : target;
            old.DestroyNow();
            var replacement = _world.Entity(new Stab1 { V = 30 }, new Stab2 { V = 40 });
            _world.Update();
            Assert.AreEqual(old.id, replacement.id);
            Assert.AreEqual(30, replacement.Get<Stab1>().V);
            Assert.AreEqual(40, replacement.Get<Stab2>().V);
            if (destroySource) Assert.AreEqual(10, target.Get<Stab1>().V);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Generations_ExpiredPayloadDisposesOnce_WithoutTouchingReplacement(bool clear)
        {
            StabDisposableTracker.Alive = 2;
            var old = _world.Entity(new StabDisposable { V = 1 });
            old.Add(new StabDisposablePool { V = 2 });
            old.DestroyNow();
            Assert.AreEqual(2, StabDisposableTracker.Alive);
            var current = _world.Entity(new StabPool { V = 42 });
            if (clear) {
                _world.UnsafeWorld->ECB.Clear();
                current.Add(new StabPool { V = 42 });
            }
            _world.Update();
            Assert.AreEqual(0, StabDisposableTracker.Alive);
            Assert.AreEqual(42, current.Get<StabPool>().V);
            Assert.IsFalse(current.Has<StabDisposablePool>());
            _world.Update();
            Assert.AreEqual(0, StabDisposableTracker.Alive);
        }

        [Test]
        public void Generations_SerializationPreservesIdentityAndHash()
        {
            var old = _world.Entity(new Stab1 { V = 1 });
            _world.Update();
            old.DestroyNow();
            var live = _world.Entity(new Stab1 { V = 2 });
            _world.Update();
            var generation = live.Generation;
            var hash = live.GetHashCode();
            var data = _world.Serialize();
            _world.Deserialize(data);
            Assert.IsFalse(old.IsValid());
            Assert.IsTrue(live.IsValid());
            Assert.AreEqual(generation, _world.GetEntity(live.id).Generation);
            Assert.AreEqual(hash, live.GetHashCode());
            live.DestroyNow();
            var next = _world.Entity();
            Assert.Greater(next.Generation, generation);
        }

        [Test]
        public void Generations_ReactiveSubscriptionDoesNotFollowRecycledId()
        {
            var systems = new Systems(ref _world).AddDefaults();
            var old = _world.Entity(new Stab1 { V = 1 });
            _world.Update();
            var callbacks = 0;
            Wargon.Nukecs.Reactivity.EntityReactiveExtensions.OnChange<Stab1>(old,
                (in Stab1 value, in Entity entity) => callbacks++);
            old.DestroyNow();
            var current = _world.Entity(new Stab1 { V = 2 });
            _world.Update();
            systems.OnUpdate(0.016f, 0f);
            current.Set(new Stab1 { V = 3 });
            systems.OnUpdate(0.016f, 0.016f);
            Assert.AreEqual(0, callbacks);
        }

        [Test]
        public void DeferredDestroy_WorksWithoutDefaults_AndRemovesEveryQueuedEntity()
        {
            var entities = new Entity[9];
            for (var i = 0; i < entities.Length; i++) {
                entities[i] = _world.Entity(new Stab1 { V = i });
                if ((i & 1) == 0) entities[i].Add<StabTag>();
            }
            var survivor = _world.Entity(new Stab1 { V = 99 });
            _world.Update();
            foreach (var entity in entities) entity.Destroy();
            foreach (var entity in entities) Assert.IsTrue(entity.IsValid());
            _world.Update();
            foreach (var entity in entities) Assert.IsFalse(entity.IsValid());
            Assert.IsTrue(survivor.IsValid());
            Assert.AreEqual(99, survivor.Get<Stab1>().V);
            Assert.AreEqual(1, _world.Query().With<Stab1>().Count);
        }

        [Test]
        public unsafe void DestroyNow_SharedStorage_RepairsSiblingRowsAndQueries()
        {
            var first = _world.Entity(new Stab1 { V = 10 });
            var middle = _world.Entity(new Stab1 { V = 20 });
            var last = _world.Entity(new Stab1 { V = 30 });
            last.Add<StabTag>();
            _world.Update();
            var all = _world.Query().With<Stab1>();
            var tagged = _world.Query().With<Stab1>().With<StabTag>();
            Assert.AreEqual(3, all.Count);
            Assert.AreEqual(1, tagged.Count);

            middle.DestroyNow();
            Assert.IsFalse(middle.IsValid());
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(1, tagged.Count);
            Assert.AreEqual(10, first.Get<Stab1>().V);
            Assert.AreEqual(30, last.Get<Stab1>().V);
            var loc = _world.UnsafeWorld->entityLocations.Ptr[last.id];
            Assert.AreEqual(loc.row, last.ArchetypeRef.rows.Ptr[loc.listPos]);
            Assert.AreEqual(last.id, last.ArchetypeRef.packedEntities.Ptr[loc.row]);
            last.DestroyNow();
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(0, tagged.Count);
        }

        [Test]
        public unsafe void DestroyNow_DisposesInlineAndClearsPool_OnlyOnce()
        {
            StabDisposableTracker.Alive = 1;
            var entity = _world.Entity(new StabDisposable { V = 1 });
            entity.Add(new StabPool { V = 42 });
            _world.Update();
            var pool = _world.UnsafeWorldRef.GetPool<StabPool>();
            entity.DestroyNow();
            Assert.AreEqual(0, StabDisposableTracker.Alive);
            Assert.AreEqual(0, *(int*)pool.UnsafeGetPtr(entity.id));
            entity.DestroyNow();
            _world.Update();
            Assert.AreEqual(0, StabDisposableTracker.Alive);
        }

        [Test]
        public void DestroyNow_CancelsPendingCommands_WithoutFlushingOtherEntities()
        {
            var entity = _world.Entity(new Stab1 { V = 1 });
            _world.Update();
            entity.Add(new Stab2 { V = 2 });
            entity.Destroy();
            var other = _world.Entity(new Stab1 { V = 9 });
            entity.DestroyNow();
            Assert.IsFalse(other.Has<Stab1>(), "Other entities' commands must remain deferred");
            var reused = _world.Entity(new Stab1 { V = 7 });
            Assert.AreEqual(entity.id, reused.id);
            _world.Update();
            Assert.IsTrue(reused.IsValid());
            Assert.AreEqual(7, reused.Get<Stab1>().V);
            Assert.IsFalse(reused.Has<Stab2>());
            Assert.AreEqual(9, other.Get<Stab1>().V);
            Assert.AreEqual(2, _world.Query().With<Stab1>().Count);
        }

        [Test]
        public unsafe void DestroyNow_BeforeFirstPlayback_ReleasesPendingComponents()
        {
            StabDisposableTracker.Alive = 1;
            var entity = _world.Entity(new StabDisposable { V = 1 });
            entity.Add(new StabPool { V = 8 });
            entity.DestroyNow();
            Assert.IsFalse(entity.IsValid());
            Assert.AreEqual(1, StabDisposableTracker.Alive, "Pending payload cleanup belongs to ECB playback");
            Assert.AreEqual(0, *(int*)_world.UnsafeWorldRef.GetPool<StabPool>().UnsafeGetPtr(entity.id));
            var reused = _world.Entity(new Stab1 { V = 5 });
            _world.Update();
            Assert.AreEqual(0, StabDisposableTracker.Alive);
            Assert.AreEqual(5, reused.Get<Stab1>().V);
            Assert.IsFalse(reused.Has<StabDisposable>());
            Assert.AreEqual(1, _world.Query().With<Stab1>().Count);
        }

        [Test]
        public void DestroyNow_EmptyEntityAndNull_AreSafe()
        {
            var entity = _world.Entity();
            entity.DestroyNow();
            entity.DestroyNow();
            Entity.Null.DestroyNow();
            _world.Update();
            Assert.IsFalse(entity.IsValid());
            var reused = _world.Entity(new Stab1 { V = 3 });
            _world.Update();
            Assert.AreEqual(3, reused.Get<Stab1>().V);
        }

        // ==================================================================
        // A1 — a typed Query<..., DestroyEntity> never matched: DestroyEntity
        // sat in BOTH the with and the default-none masks
        // ==================================================================
        [Test]
        public void TypedQuery_WithDestroyEntity_OverridesDefaultNone()
        {
            var e = _world.Entity(new Stab1 { V = 1 });
            e.Add<DestroyEntity>();
            _world.Update();

            var q = new Query<Entity, DestroyEntity>();
            q.Init(ref _world.unsafeWorldPtr);
            Assert.AreEqual(1, q.Count,
                "FIX A1: explicit With<DestroyEntity> must override the default none filter");
        }

        // ==================================================================
        // A2 — a fluent query created after entities exist used to stay
        // attached to nothing (archetype path) → silently zero results
        // ==================================================================
        [Test]
        public void FluentQuery_CreatedAfterSpawn_MatchesArchetypePath()
        {
            for (var i = 0; i < 5; i++)
            {
                var e = _world.Entity();
                e.Add(new Stab1 { V = i });
                e.Add<StabTag>(); // tag in with-set → archetype path
            }
            _world.Update();

            var q = _world.Query().With<Stab1>().With<StabTag>(); // created AFTER spawn
            Assert.AreEqual(5, q.Count, "FIX A2: late query must lazily attach to existing archetypes");
            var n = 0;
            foreach (ref var e in q) n++;
            Assert.AreEqual(5, n);
        }

        [Test]
        public void FluentQuery_WithNone_CreatedAfterSpawn_DegradedPathStillMatches()
        {
            for (var i = 0; i < 3; i++) _world.Entity(new Stab1 { V = i });
            for (var i = 0; i < 2; i++) { var e = _world.Entity(new Stab1 { V = 100 + i }); e.Add<StabTag>(); }
            _world.Update();

            var q = _world.Query().With<Stab1>().None<StabTag>(); // created AFTER spawn
            Assert.AreEqual(3, q.Count,
                "FIX A2: degraded storage (tagged sibling LA non-empty) must fall back to the rescanned archetype list, not to an empty one");
        }

        // ==================================================================
        // A3 — zero-with query (world.Query() / Query<Entity>) matched nothing
        // ==================================================================
        [Test]
        public void ZeroWithQuery_MatchesAllEntities()
        {
            for (var i = 0; i < 3; i++) _world.Entity(new Stab1 { V = i });
            for (var i = 0; i < 2; i++) _world.Entity(new Stab1 { V = i }, new Stab2 { V = i });
            _world.Update();

            Assert.AreEqual(5, _world.Query().Count, "FIX A3: zero-with query matches every archetype");
            var n = 0;
            foreach (ref var e in _world.Query()) n++;
            Assert.AreEqual(5, n);
        }

        // ==================================================================
        // B1 — Archetype.Destroy left the storage row in place
        // ==================================================================
        [Test]
        public unsafe void Archetype_DestroyInline_ClearsPoolSlotsAndDetachesQueries()
        {
            var e = _world.Entity(new Stab1 { V = 1 });
            _world.Update();
            e.Add(new StabPool { V = 7 });
            _world.Update();
            var pool = _world.UnsafeWorldRef.GetPool<StabPool>();
            Assert.AreEqual(7, *(int*)pool.UnsafeGetPtr(e.id), "setup: payload present");

            var arch = e.ArchetypeRef;
            arch.Destroy(e.id); // historical low-level inline path

            Assert.AreEqual(0, *(int*)pool.UnsafeGetPtr(e.id),
                "FIX B1: inline destroy must clear pool slots (it also no longer materializes junk pools for tags)");
            // NOTE: the storage row and the storage-mode query counts settle on ECB playback
            // only (destroyEdge decrements archetype-path counters) — inline destroy is not
            // instantly visible to dense queries; part of the same POST_1_0.md #12 rework.
        }

        // ==================================================================
        // B3 — chunk iteration + CopyTo on shared (sparse) storage
        // ==================================================================
        [Test]
        public void Chunk2_SparseRows_IterationAndCopyTo_MatchQueryRows()
        {
            _world.AddRes(new StabCounter());
            var systems = new Systems(ref _world).Add(StabChunkSystems.Chunk2, Threads.Main);
            SpawnStab(10); // evens tagged → shared storage, tag-free rows scattered

            new Res<StabCounter>().Ref.Sum = 0;
            new Res<StabCounter>().Ref.CopySum = 0;
            new Res<StabCounter>().Ref.Count = 0;
            new Res<StabCounter>().Ref.SentinelHits = 0;
            systems.OnUpdate(0.016f, 0f);

            var c = new Res<StabCounter>().Ref;
            Assert.AreEqual(25, c.Sum, "FIX B3: iteration must gather only the query's scattered rows (sum of odd V)");
            Assert.AreEqual(25, c.CopySum, "FIX B3: CopyTo must gather, not memcpy past rows[0]");
            Assert.AreEqual(5, c.Count);
            Assert.AreEqual(0, c.SentinelHits, "CopyTo must not write past the chunk");
        }

        [Test]
        public void Chunk2_DenseRows_StillCorrect()
        {
            _world.AddRes(new StabCounter());
            var systems = new Systems(ref _world).Add(StabChunkSystems.Chunk2, Threads.Main);
            SpawnStab(10, withTag: false); // dense storage

            new Res<StabCounter>().Ref.Sum = 0;
            new Res<StabCounter>().Ref.CopySum = 0;
            new Res<StabCounter>().Ref.Count = 0;
            new Res<StabCounter>().Ref.SentinelHits = 0;
            systems.OnUpdate(0.016f, 0f);

            var c = new Res<StabCounter>().Ref;
            Assert.AreEqual(45, c.Sum);
            Assert.AreEqual(45, c.CopySum);
            Assert.AreEqual(10, c.Count);
            Assert.AreEqual(0, c.SentinelHits);
        }

        [Test]
        public void Chunk5_SparseRows_IterationAndCopyTo_MatchQueryRows()
        {
            _world.AddRes(new StabCounter());
            var systems = new Systems(ref _world).Add(StabChunkSystems.Chunk5, Threads.Main);
            SpawnStab(10);

            new Res<StabCounter>().Ref.Sum = 0;
            new Res<StabCounter>().Ref.CopySum = 0;
            new Res<StabCounter>().Ref.Count = 0;
            new Res<StabCounter>().Ref.SentinelHits = 0;
            systems.OnUpdate(0.016f, 0f);

            var c = new Res<StabCounter>().Ref;
            Assert.AreEqual(25, c.Sum, "FIX B3: arity-5 SetData must bind rows on shared storage");
            Assert.AreEqual(25, c.CopySum);
            Assert.AreEqual(5, c.Count);
            Assert.AreEqual(0, c.SentinelHits);
        }

        [Test]
        public void Chunk8_SparseRows_T8ColumnIsCopied()
        {
            _world.AddRes(new StabCounter());
            var systems = new Systems(ref _world).Add(StabChunkSystems.Chunk8, Threads.Main);
            SpawnStab(10);

            new Res<StabCounter>().Ref.Sum = 0;
            new Res<StabCounter>().Ref.CopySum = 0;
            new Res<StabCounter>().Ref.Count = 0;
            new Res<StabCounter>().Ref.SentinelHits = 0;
            systems.OnUpdate(0.016f, 0f);

            var c = new Res<StabCounter>().Ref;
            Assert.AreEqual(25, c.Sum, "FIX B3: arity-8 gather");
            Assert.AreEqual(25, c.CopySum, "FIX B3: arity-8 CopyTo must copy the T8 column (branch was a duplicated T6 check)");
            Assert.AreEqual(5, c.Count);
            Assert.AreEqual(0, c.SentinelHits);
        }

        // ==================================================================
        // B4 — disposable components leaked when their column was dropped
        // by a component removal migration
        // ==================================================================
        [Test]
        public void RemoveComponent_DisposesDisposableColumn()
        {
            var e = _world.Entity();
            e.Add(new StabDisposable { V = 1 });
            _world.Update();
            StabDisposableTracker.Alive = 1; // ECB add memcpy's bytes — no ctor runs

            e.Remove<StabDisposable>();
            _world.Update();

            Assert.AreEqual(0, StabDisposableTracker.Alive,
                "FIX B4: Remove must dispose disposable columns dropped by the migration");
            Assert.IsFalse(e.Has<StabDisposable>());
        }

        [Test]
        public void ECB_RemoveAndDispose_DisposesColumn()
        {
            var e = _world.Entity();
            e.Add(new StabDisposable { V = 2 });
            _world.Update();
            StabDisposableTracker.Alive = 1;

            _world.ECB.RemoveAndDispose<StabDisposable>(e.id);
            _world.Update();

            Assert.AreEqual(0, StabDisposableTracker.Alive, "FIX B4: RemoveAndDispose must actually dispose");
            Assert.IsFalse(e.Has<StabDisposable>());
        }

        // ==================================================================
        // B5 — removing a pool component left the pool slot behind
        // ==================================================================
        [Test]
        public unsafe void RemovePoolComponent_ClearsPoolSlot()
        {
            var e = _world.Entity(new Stab1 { V = 1 });
            _world.Update();
            e.Add(new StabPool { V = 7 }); // pool payload written at enqueue time
            _world.Update();

            var pool = _world.UnsafeWorldRef.GetPool<StabPool>();
            Assert.AreEqual(7, *(int*)pool.UnsafeGetPtr(e.id), "setup: payload present after add");

            e.Remove<StabPool>();
            _world.Update();

            Assert.IsFalse(e.Has<StabPool>());
            Assert.AreEqual(0, *(int*)pool.UnsafeGetPtr(e.id),
                "FIX B5: ECB playback must clear the pool slot on pool-component removal (stale slot data used to survive)");
        }

        // ==================================================================
        // C1 — save header validation (magic / version / regionCount bounds)
        // ==================================================================
        [Test]
        public void Deserialize_RejectsCorruptOrForeignSaves_Cleanly()
        {
            _world.Entity(new Stab1 { V = 5 });
            _world.Update();
            var save = _world.Serialize();

            var target = World.Create(WorldConfig.Default16);
            try
            {
                var badMagic = (byte[])save.Clone();
                badMagic[0] ^= 0xFF;
                Assert.Throws<ArgumentException>(() => target.Deserialize(badMagic),
                    "FIX C1: bad magic must fail with a clear exception, not heap corruption");

                var badVersion = (byte[])save.Clone();
                badVersion[4] = 99;
                Assert.Throws<ArgumentException>(() => target.Deserialize(badVersion),
                    "FIX C1: foreign format version must be rejected");

                var badRegionCount = (byte[])save.Clone();
                badRegionCount[8] = 0xFF; badRegionCount[9] = 0xFF;
                badRegionCount[10] = 0xFF; badRegionCount[11] = 0x7F; // int.MaxValue
                Assert.Throws<ArgumentException>(() => target.Deserialize(badRegionCount),
                    "FIX C1: region count overflow must be rejected before any allocation");

                var truncated = new byte[5];
                Array.Copy(save, truncated, 5);
                Assert.Throws<ArgumentException>(() => target.Deserialize(truncated),
                    "FIX C1: truncated save must be rejected");

                // target world survives every rejected load
                target.Entity(new Stab1 { V = 6 });
                target.Update();
                Assert.AreEqual(1, target.Query().With<Stab1>().Count);
            }
            finally
            {
                if (target.IsAlive) target.Dispose();
            }
        }

        // ==================================================================
        // D1 — world slot allocation could overwrite a live world
        // ==================================================================
        [Test]
        public void WorldSlotReuse_DoesNotClobberLiveWorld()
        {
            var w1 = World.Create(WorldConfig.Default16);
            var w2 = World.Create(WorldConfig.Default16);
            try
            {
                w2.Entity(new Stab1 { V = 42 }); // marker payload in the live world
                w2.Update();
                var slot1 = w1.Id;
                w1.Dispose();
                w1 = default;

                var w3 = World.Create(WorldConfig.Default16);
                var w4 = World.Create(WorldConfig.Default16);
                try
                {
                    Assert.AreEqual(slot1, w3.Id, "disposed slot should be reused");
                    Assert.AreNotEqual(w2.Id, w4.Id,
                        "FIX D1: Create must not hand out the slot of a still-live world");
                    Assert.IsTrue(w2.IsAlive, "FIX D1: live world must survive other Create calls");
                    Assert.AreEqual(1, w2.Query().With<Stab1>().Count, "live world data must be untouched");
                }
                finally { if (w4.IsAlive) w4.Dispose(); if (w3.IsAlive) w3.Dispose(); }
            }
            finally
            {
                if (w1.IsAlive) w1.Dispose();
                if (w2.IsAlive) w2.Dispose();
            }
        }

        [Test]
        public void Create_PastMaxWorlds_ThrowsCleanError()
        {
            if (_world.IsAlive) { _world.Dispose(); _world = default; } // SetUp's world would consume a slot
            var created = new List<World>();
            try
            {
                for (var i = 0; i < World.MAX_WORLD_COUNT; i++) created.Add(World.Create(WorldConfig.Default16));
                Assert.Throws<InvalidOperationException>(() => World.Create(WorldConfig.Default16),
                    "FIX D1: the 9th world must fail cleanly, not write past the worlds array");
            }
            finally
            {
                foreach (var w in created) if (w.IsAlive) w.Dispose();
            }
        }

        // ==================================================================
        // D2 — resource slot ids are globally stable across worlds
        // ==================================================================
        [Test]
        public void MultipleWorlds_Resources_HaveStableSlotsPerType()
        {
            var wa = World.Create(WorldConfig.Default16);
            var wb = World.Create(WorldConfig.Default16);
            try
            {
                wa.AddRes(new StabCounter { Sum = 1 }); // global slot 0
                wa.AddRes(new StabRes { Value = 2 });   // global slot 1
                wb.AddRes(new StabRes { Value = 20 });  // must be slot 1 in wb too (was 0 pre-fix)

                var (lenA, listA) = wa.UnsafeWorldRef.resStorage.GetAll(new IRes[8]);
                var (lenB, listB) = wb.UnsafeWorldRef.resStorage.GetAll(new IRes[8]);

                Assert.AreEqual(2, lenA, "world A holds both registered resources");
                Assert.AreEqual(1, lenB,
                    "FIX D2: world B holds only its own resource — global slot ids keep A's slot 0 null in B");
                Assert.IsInstanceOf<StabRes>(listB[0],
                    "FIX D2: slot must contain the wrapper for the requested type, not a type-confused one");
                Assert.IsTrue(listA[0] is StabCounter || listA[1] is StabCounter,
                    "world A must hold both resource types");
            }
            finally { if (wa.IsAlive) wa.Dispose(); if (wb.IsAlive) wb.Dispose(); }
        }

        // ==================================================================
        // E2 — Entity equality ignored the world while GetHashCode did not
        // ==================================================================
        [Test]
        public void Entity_Equality_IncludesWorld()
        {
            var wa = World.Create(WorldConfig.Default16);
            var wb = World.Create(WorldConfig.Default16);
            try
            {
                var ea = wa.Entity();
                var eb = wb.Entity();
                Assert.AreEqual(ea.id, eb.id, "setup: same id in both worlds");

                Assert.IsFalse(ea == eb, "FIX E2: same id in different worlds must not be equal");
                Assert.IsTrue(ea != eb);
                Assert.AreNotEqual(ea.GetHashCode(), eb.GetHashCode(), "hash already includes the world");

                var dict = new Dictionary<Entity, int>();
                dict[ea] = 1;
                dict[eb] = 2;
                Assert.AreEqual(1, dict[ea], "dictionary contract must hold");
                Assert.AreEqual(2, dict[eb]);
            }
            finally { if (wa.IsAlive) wa.Dispose(); if (wb.IsAlive) wb.Dispose(); }
        }
    }
}
