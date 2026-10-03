using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public unsafe class RuntimeQueryEntityDeconstructionTests
    {
        private static void Init<T>(ref T query, ref World world) where T : struct, ISystemParam
        {
            var pointer = new ptr<World.WorldUnsafe>((byte*)world.UnsafeWorld, 0u, true);
            query.Init(ref pointer);
            query.Update(ref world, IntPtr.Zero);
        }
        [Test] public void EntityWith1_Inline_FullAndRanged() => Check1<IntegrationA1>();
        [Test] public void EntityWith1_Pools_FullAndRanged() => Check1<IntegrationP1>();
        [Test] public void EntityWith1_Mixed_FullAndRanged() => Check1<IntegrationA1>();
        private static void Check1<T1>()
            where T1 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1));
                for (var i = 0; i < 24; i++)
                {
                    var entity = arch.CreateEntity();
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T1>()) = entity.id + 0;
                    if (i % 2 == 0) entity.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<Entity, T1>();
                Init(ref query, ref world);
                var ids = new List<int>();
                query._range = new Range(2, 9); // Full iteration must ignore this range.
                foreach (var (entity, c1) in query.iter())
                {
                    ids.Add(entity.id);
                    Assert.AreEqual(entity.id + 0, *(int*)c1.data);
                    *(int*)c1.data += 1;
                }
                Assert.AreEqual(24, ids.Count);
                Assert.AreEqual(24, new HashSet<int>(ids).Count);
                query._range = new Range(2, 9);
                var visited = 0;
                foreach (var (entity, c1) in query.par_iter())
                {
                    Assert.AreEqual(ids[2 + visited], entity.id);
                    visited++;
                    Assert.AreEqual(entity.id + 1, *(int*)c1.data);
                    *(int*)c1.data += 10;
                }
                Assert.AreEqual(7, visited);
                var filtered = new Query<Entity, T1, None<IntegrationExcluded>>();
                Init(ref filtered, ref world);
                var filteredCount = 0;
                foreach (var (entity, c1) in filtered.iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(12, filteredCount);
                filtered._range = new Range(1, 5);
                filteredCount = 0;
                foreach (var (entity, c1) in filtered.par_iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(4, filteredCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void EntityWith2_Inline_FullAndRanged() => Check2<IntegrationA1, IntegrationA2>();
        [Test] public void EntityWith2_Pools_FullAndRanged() => Check2<IntegrationP1, IntegrationP2>();
        [Test] public void EntityWith2_Mixed_FullAndRanged() => Check2<IntegrationA1, IntegrationP2>();
        private static void Check2<T1, T2>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2));
                for (var i = 0; i < 24; i++)
                {
                    var entity = arch.CreateEntity();
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T1>()) = entity.id + 0;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T2>()) = entity.id + 100;
                    if (i % 2 == 0) entity.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<Entity, T1, T2>();
                Init(ref query, ref world);
                var ids = new List<int>();
                query._range = new Range(2, 9); // Full iteration must ignore this range.
                foreach (var (entity, c1, c2) in query.iter())
                {
                    ids.Add(entity.id);
                    Assert.AreEqual(entity.id + 0, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    *(int*)c1.data += 1;
                }
                Assert.AreEqual(24, ids.Count);
                Assert.AreEqual(24, new HashSet<int>(ids).Count);
                query._range = new Range(2, 9);
                var visited = 0;
                foreach (var (entity, c1, c2) in query.par_iter())
                {
                    Assert.AreEqual(ids[2 + visited], entity.id);
                    visited++;
                    Assert.AreEqual(entity.id + 1, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    *(int*)c1.data += 10;
                }
                Assert.AreEqual(7, visited);
                var filtered = new Query<Entity, T1, T2, None<IntegrationExcluded>>();
                Init(ref filtered, ref world);
                var filteredCount = 0;
                foreach (var (entity, c1, c2) in filtered.iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(12, filteredCount);
                filtered._range = new Range(1, 5);
                filteredCount = 0;
                foreach (var (entity, c1, c2) in filtered.par_iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(4, filteredCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void EntityWith3_Inline_FullAndRanged() => Check3<IntegrationA1, IntegrationA2, IntegrationA3>();
        [Test] public void EntityWith3_Pools_FullAndRanged() => Check3<IntegrationP1, IntegrationP2, IntegrationP3>();
        [Test] public void EntityWith3_Mixed_FullAndRanged() => Check3<IntegrationA1, IntegrationP2, IntegrationA3>();
        private static void Check3<T1, T2, T3>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3));
                for (var i = 0; i < 24; i++)
                {
                    var entity = arch.CreateEntity();
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T1>()) = entity.id + 0;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T2>()) = entity.id + 100;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T3>()) = entity.id + 200;
                    if (i % 2 == 0) entity.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<Entity, T1, T2, T3>();
                Init(ref query, ref world);
                var ids = new List<int>();
                query._range = new Range(2, 9); // Full iteration must ignore this range.
                foreach (var (entity, c1, c2, c3) in query.iter())
                {
                    ids.Add(entity.id);
                    Assert.AreEqual(entity.id + 0, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    *(int*)c1.data += 1;
                }
                Assert.AreEqual(24, ids.Count);
                Assert.AreEqual(24, new HashSet<int>(ids).Count);
                query._range = new Range(2, 9);
                var visited = 0;
                foreach (var (entity, c1, c2, c3) in query.par_iter())
                {
                    Assert.AreEqual(ids[2 + visited], entity.id);
                    visited++;
                    Assert.AreEqual(entity.id + 1, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    *(int*)c1.data += 10;
                }
                Assert.AreEqual(7, visited);
                var filtered = new Query<Entity, T1, T2, T3, None<IntegrationExcluded>>();
                Init(ref filtered, ref world);
                var filteredCount = 0;
                foreach (var (entity, c1, c2, c3) in filtered.iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(12, filteredCount);
                filtered._range = new Range(1, 5);
                filteredCount = 0;
                foreach (var (entity, c1, c2, c3) in filtered.par_iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(4, filteredCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void EntityWith4_Inline_FullAndRanged() => Check4<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4>();
        [Test] public void EntityWith4_Pools_FullAndRanged() => Check4<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4>();
        [Test] public void EntityWith4_Mixed_FullAndRanged() => Check4<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4>();
        private static void Check4<T1, T2, T3, T4>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4));
                for (var i = 0; i < 24; i++)
                {
                    var entity = arch.CreateEntity();
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T1>()) = entity.id + 0;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T2>()) = entity.id + 100;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T3>()) = entity.id + 200;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T4>()) = entity.id + 300;
                    if (i % 2 == 0) entity.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<Entity, T1, T2, T3, T4>();
                Init(ref query, ref world);
                var ids = new List<int>();
                query._range = new Range(2, 9); // Full iteration must ignore this range.
                foreach (var (entity, c1, c2, c3, c4) in query.iter())
                {
                    ids.Add(entity.id);
                    Assert.AreEqual(entity.id + 0, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    *(int*)c1.data += 1;
                }
                Assert.AreEqual(24, ids.Count);
                Assert.AreEqual(24, new HashSet<int>(ids).Count);
                query._range = new Range(2, 9);
                var visited = 0;
                foreach (var (entity, c1, c2, c3, c4) in query.par_iter())
                {
                    Assert.AreEqual(ids[2 + visited], entity.id);
                    visited++;
                    Assert.AreEqual(entity.id + 1, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    *(int*)c1.data += 10;
                }
                Assert.AreEqual(7, visited);
                var filtered = new Query<Entity, T1, T2, T3, T4, None<IntegrationExcluded>>();
                Init(ref filtered, ref world);
                var filteredCount = 0;
                foreach (var (entity, c1, c2, c3, c4) in filtered.iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(12, filteredCount);
                filtered._range = new Range(1, 5);
                filteredCount = 0;
                foreach (var (entity, c1, c2, c3, c4) in filtered.par_iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(4, filteredCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void EntityWith5_Inline_FullAndRanged() => Check5<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4, IntegrationA5>();
        [Test] public void EntityWith5_Pools_FullAndRanged() => Check5<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4, IntegrationP5>();
        [Test] public void EntityWith5_Mixed_FullAndRanged() => Check5<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5>();
        private static void Check5<T1, T2, T3, T4, T5>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));
                for (var i = 0; i < 24; i++)
                {
                    var entity = arch.CreateEntity();
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T1>()) = entity.id + 0;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T2>()) = entity.id + 100;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T3>()) = entity.id + 200;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T4>()) = entity.id + 300;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T5>()) = entity.id + 400;
                    if (i % 2 == 0) entity.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<Entity, T1, T2, T3, T4, T5>();
                Init(ref query, ref world);
                var ids = new List<int>();
                query._range = new Range(2, 9); // Full iteration must ignore this range.
                foreach (var (entity, c1, c2, c3, c4, c5) in query.iter())
                {
                    ids.Add(entity.id);
                    Assert.AreEqual(entity.id + 0, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    Assert.AreEqual(entity.id + 400, *(int*)c5.data);
                    *(int*)c1.data += 1;
                }
                Assert.AreEqual(24, ids.Count);
                Assert.AreEqual(24, new HashSet<int>(ids).Count);
                query._range = new Range(2, 9);
                var visited = 0;
                foreach (var (entity, c1, c2, c3, c4, c5) in query.par_iter())
                {
                    Assert.AreEqual(ids[2 + visited], entity.id);
                    visited++;
                    Assert.AreEqual(entity.id + 1, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    Assert.AreEqual(entity.id + 400, *(int*)c5.data);
                    *(int*)c1.data += 10;
                }
                Assert.AreEqual(7, visited);
                var filtered = new Query<Entity, T1, T2, T3, T4, T5, None<IntegrationExcluded>>();
                Init(ref filtered, ref world);
                var filteredCount = 0;
                foreach (var (entity, c1, c2, c3, c4, c5) in filtered.iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(12, filteredCount);
                filtered._range = new Range(1, 5);
                filteredCount = 0;
                foreach (var (entity, c1, c2, c3, c4, c5) in filtered.par_iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(4, filteredCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void EntityWith6_Inline_FullAndRanged() => Check6<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4, IntegrationA5, IntegrationA6>();
        [Test] public void EntityWith6_Pools_FullAndRanged() => Check6<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4, IntegrationP5, IntegrationP6>();
        [Test] public void EntityWith6_Mixed_FullAndRanged() => Check6<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5, IntegrationP6>();
        private static void Check6<T1, T2, T3, T4, T5, T6>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));
                for (var i = 0; i < 24; i++)
                {
                    var entity = arch.CreateEntity();
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T1>()) = entity.id + 0;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T2>()) = entity.id + 100;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T3>()) = entity.id + 200;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T4>()) = entity.id + 300;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T5>()) = entity.id + 400;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T6>()) = entity.id + 500;
                    if (i % 2 == 0) entity.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<Entity, T1, T2, T3, T4, T5, T6>();
                Init(ref query, ref world);
                var ids = new List<int>();
                query._range = new Range(2, 9); // Full iteration must ignore this range.
                foreach (var (entity, c1, c2, c3, c4, c5, c6) in query.iter())
                {
                    ids.Add(entity.id);
                    Assert.AreEqual(entity.id + 0, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    Assert.AreEqual(entity.id + 400, *(int*)c5.data);
                    Assert.AreEqual(entity.id + 500, *(int*)c6.data);
                    *(int*)c1.data += 1;
                }
                Assert.AreEqual(24, ids.Count);
                Assert.AreEqual(24, new HashSet<int>(ids).Count);
                query._range = new Range(2, 9);
                var visited = 0;
                foreach (var (entity, c1, c2, c3, c4, c5, c6) in query.par_iter())
                {
                    Assert.AreEqual(ids[2 + visited], entity.id);
                    visited++;
                    Assert.AreEqual(entity.id + 1, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    Assert.AreEqual(entity.id + 400, *(int*)c5.data);
                    Assert.AreEqual(entity.id + 500, *(int*)c6.data);
                    *(int*)c1.data += 10;
                }
                Assert.AreEqual(7, visited);
                var filtered = new Query<Entity, T1, T2, T3, T4, T5, T6, None<IntegrationExcluded>>();
                Init(ref filtered, ref world);
                var filteredCount = 0;
                foreach (var (entity, c1, c2, c3, c4, c5, c6) in filtered.iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(12, filteredCount);
                filtered._range = new Range(1, 5);
                filteredCount = 0;
                foreach (var (entity, c1, c2, c3, c4, c5, c6) in filtered.par_iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(4, filteredCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void EntityWith7_Inline_FullAndRanged() => Check7<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4, IntegrationA5, IntegrationA6, IntegrationA7>();
        [Test] public void EntityWith7_Pools_FullAndRanged() => Check7<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4, IntegrationP5, IntegrationP6, IntegrationP7>();
        [Test] public void EntityWith7_Mixed_FullAndRanged() => Check7<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5, IntegrationP6, IntegrationA7>();
        private static void Check7<T1, T2, T3, T4, T5, T6, T7>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7));
                for (var i = 0; i < 24; i++)
                {
                    var entity = arch.CreateEntity();
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T1>()) = entity.id + 0;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T2>()) = entity.id + 100;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T3>()) = entity.id + 200;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T4>()) = entity.id + 300;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T5>()) = entity.id + 400;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T6>()) = entity.id + 500;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T7>()) = entity.id + 600;
                    if (i % 2 == 0) entity.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<Entity, T1, T2, T3, T4, T5, T6, T7>();
                Init(ref query, ref world);
                var ids = new List<int>();
                query._range = new Range(2, 9); // Full iteration must ignore this range.
                foreach (var (entity, c1, c2, c3, c4, c5, c6, c7) in query.iter())
                {
                    ids.Add(entity.id);
                    Assert.AreEqual(entity.id + 0, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    Assert.AreEqual(entity.id + 400, *(int*)c5.data);
                    Assert.AreEqual(entity.id + 500, *(int*)c6.data);
                    Assert.AreEqual(entity.id + 600, *(int*)c7.data);
                    *(int*)c1.data += 1;
                }
                Assert.AreEqual(24, ids.Count);
                Assert.AreEqual(24, new HashSet<int>(ids).Count);
                query._range = new Range(2, 9);
                var visited = 0;
                foreach (var (entity, c1, c2, c3, c4, c5, c6, c7) in query.par_iter())
                {
                    Assert.AreEqual(ids[2 + visited], entity.id);
                    visited++;
                    Assert.AreEqual(entity.id + 1, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    Assert.AreEqual(entity.id + 400, *(int*)c5.data);
                    Assert.AreEqual(entity.id + 500, *(int*)c6.data);
                    Assert.AreEqual(entity.id + 600, *(int*)c7.data);
                    *(int*)c1.data += 10;
                }
                Assert.AreEqual(7, visited);
                var filtered = new Query<Entity, T1, T2, T3, T4, T5, T6, T7, None<IntegrationExcluded>>();
                Init(ref filtered, ref world);
                var filteredCount = 0;
                foreach (var (entity, c1, c2, c3, c4, c5, c6, c7) in filtered.iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(12, filteredCount);
                filtered._range = new Range(1, 5);
                filteredCount = 0;
                foreach (var (entity, c1, c2, c3, c4, c5, c6, c7) in filtered.par_iter())
                {
                    Assert.IsFalse(entity.Has<IntegrationExcluded>());
                    filteredCount++;
                }
                Assert.AreEqual(4, filteredCount);
            }
            finally { world.Dispose(); }
        }
        [Test] public void EntityWith8_Inline_FullAndRanged() => Check8<IntegrationA1, IntegrationA2, IntegrationA3, IntegrationA4, IntegrationA5, IntegrationA6, IntegrationA7, IntegrationA8>();
        [Test] public void EntityWith8_Pools_FullAndRanged() => Check8<IntegrationP1, IntegrationP2, IntegrationP3, IntegrationP4, IntegrationP5, IntegrationP6, IntegrationP7, IntegrationP8>();
        [Test] public void EntityWith8_Mixed_FullAndRanged() => Check8<IntegrationA1, IntegrationP2, IntegrationA3, IntegrationP4, IntegrationA5, IntegrationP6, IntegrationA7, IntegrationP8>();
        private static void Check8<T1, T2, T3, T4, T5, T6, T7, T8>()
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where T5 : unmanaged, IComponent where T6 : unmanaged, IComponent where T7 : unmanaged, IComponent where T8 : unmanaged, IComponent
        {
            var world = World.Create(WorldConfig.Default1024);
            try
            {
                var arch = world.GetArchetype(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8));
                for (var i = 0; i < 24; i++)
                {
                    var entity = arch.CreateEntity();
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T1>()) = entity.id + 0;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T2>()) = entity.id + 100;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T3>()) = entity.id + 200;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T4>()) = entity.id + 300;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T5>()) = entity.id + 400;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T6>()) = entity.id + 500;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T7>()) = entity.id + 600;
                    *(int*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref entity.Get<T8>()) = entity.id + 700;
                    if (i % 2 == 0) entity.Add<IntegrationExcluded>();
                }
                world.Update();
                var query = new Query<Entity, T1, T2, T3, T4, T5, T6, T7, T8>();
                Init(ref query, ref world);
                var ids = new List<int>();
                query._range = new Range(2, 9); // Full iteration must ignore this range.
                foreach (var (entity, c1, c2, c3, c4, c5, c6, c7, c8) in query.iter())
                {
                    ids.Add(entity.id);
                    Assert.AreEqual(entity.id + 0, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    Assert.AreEqual(entity.id + 400, *(int*)c5.data);
                    Assert.AreEqual(entity.id + 500, *(int*)c6.data);
                    Assert.AreEqual(entity.id + 600, *(int*)c7.data);
                    Assert.AreEqual(entity.id + 700, *(int*)c8.data);
                    *(int*)c1.data += 1;
                }
                Assert.AreEqual(24, ids.Count);
                Assert.AreEqual(24, new HashSet<int>(ids).Count);
                query._range = new Range(2, 9);
                var visited = 0;
                foreach (var (entity, c1, c2, c3, c4, c5, c6, c7, c8) in query.par_iter())
                {
                    Assert.AreEqual(ids[2 + visited], entity.id);
                    visited++;
                    Assert.AreEqual(entity.id + 1, *(int*)c1.data);
                    Assert.AreEqual(entity.id + 100, *(int*)c2.data);
                    Assert.AreEqual(entity.id + 200, *(int*)c3.data);
                    Assert.AreEqual(entity.id + 300, *(int*)c4.data);
                    Assert.AreEqual(entity.id + 400, *(int*)c5.data);
                    Assert.AreEqual(entity.id + 500, *(int*)c6.data);
                    Assert.AreEqual(entity.id + 600, *(int*)c7.data);
                    Assert.AreEqual(entity.id + 700, *(int*)c8.data);
                    *(int*)c1.data += 10;
                }
                Assert.AreEqual(7, visited);
            }
            finally { world.Dispose(); }
        }
    }
}
