using NUnit.Framework;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests
{
    [TestFixture]
    public unsafe class ChunkSparseRegressionTests
    {
        private World _world;

        [SetUp]
        public void SetUp() => _world = World.Create(WorldConfig.Default256);

        [TearDown]
        public void TearDown()
        {
            if (_world.IsAlive) _world.Dispose();
        }

        private Entity[] Spawn(bool sparse)
        {
            var entities = new Entity[7];
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i] = _world.Entity();
                e.Add(new Stab1 { V = 100 + i });
                e.Add(new Stab2 { V = 200 + i });
                e.Add(new Stab3 { V = 300 + i });
                e.Add(new Stab4 { V = 400 + i });
                e.Add(new Stab5 { V = 500 + i });
                e.Add(new Stab6 { V = 600 + i });
                e.Add(new Stab7 { V = 700 + i });
                e.Add(new Stab8 { V = 800 + i });
                if (sparse && i % 2 == 0) e.Add<StabTag>();
            }
            _world.Update();
            if (sparse)
            {
                // Swap-remove the middle member, then append it again: rows [1,5,3].
                entities[3].Add<StabTag>();
                _world.Update();
                entities[3].Remove<StabTag>();
                _world.Update();
                Assert.AreEqual(3, entities[1].ArchetypeRef.count);
                CollectionAssert.AreEqual(new[] { 1, 5, 3 }, new[] {
                    entities[1].ArchetypeRef.rows.Ptr[0],
                    entities[1].ArchetypeRef.rows.Ptr[1],
                    entities[1].ArchetypeRef.rows.Ptr[2] });
            }
            for (var i = 0; i < entities.Length; i++)
            {
                var loc = _world.UnsafeWorldRef.entityLocations.Ptr[entities[i].id];
                ref var arch = ref entities[i].ArchetypeRef;
                Assert.AreEqual(loc.row, arch.rows.Ptr[loc.listPos]);
                Assert.AreEqual(entities[i].id, arch.storagePtr.Ref.packedEntities.Ptr[loc.row]);
                Assert.AreEqual(100 + i, entities[i].Get<Stab1>().V);
            }
            return entities;
        }
        [TestCase(false)]
        [TestCase(true)]
        public void Chunk1_CopyWindowsAndEnd(bool sparse)
        {
            var entities = Spawn(sparse);
            ref var arch = ref entities[1].ArchetypeRef;
            var expected = sparse ? new[] { 101, 105, 103 } : new[] { 100, 101, 102, 103, 104, 105, 106 };
            var chunk = new Chunk<Stab1>();
            chunk.SetData(ref arch);
            Assert.AreEqual(expected.Length, chunk.Count);
            var destination = stackalloc Stab1[8];
            for (var start = 0; start < expected.Length; start++)
            {
                Assert.AreEqual(expected[start], chunk.Get().V);
                for (var j = 0; j < 8; j++) destination[j].V = -777;
                var length = expected.Length - start;
                chunk.CopyTo(destination, length);
                for (var j = 0; j < length; j++)
                    Assert.AreEqual(expected[start + j], destination[j].V, "copy window start=" + start + ", index=" + j);
                Assert.AreEqual(-777, destination[length].V);
                Assert.AreEqual(start + 1 < expected.Length, chunk.MoveNext());
            }
            Assert.IsFalse(chunk.MoveNext());
            Assert.AreEqual(expected[expected.Length - 1], chunk.Get().V, "end must not advance the pointer past the last row");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Chunk2_CopyWindowsAndEnd(bool sparse)
        {
            var entities = Spawn(sparse);
            ref var arch = ref entities[1].ArchetypeRef;
            var expected = sparse ? new[] { 101, 105, 103 } : new[] { 100, 101, 102, 103, 104, 105, 106 };
            var chunk = new Chunk<Stab1, Stab2>();
            chunk.SetData(ref arch);
            Assert.AreEqual(expected.Length, chunk.Count);
            var destination = stackalloc Stab1[8];
            for (var start = 0; start < expected.Length; start++)
            {
                Assert.AreEqual(expected[start], chunk.C1.V);
                for (var j = 0; j < 8; j++) destination[j].V = -777;
                var length = expected.Length - start;
                chunk.CopyTo(destination, length);
                for (var j = 0; j < length; j++)
                    Assert.AreEqual(expected[start + j], destination[j].V, "copy window start=" + start + ", index=" + j);
                Assert.AreEqual(-777, destination[length].V);
                Assert.AreEqual(start + 1 < expected.Length, chunk.MoveNext());
            }
            Assert.IsFalse(chunk.MoveNext());
            Assert.AreEqual(expected[expected.Length - 1], chunk.C1.V, "end must not advance the pointer past the last row");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Chunk3_CopyWindowsAndEnd(bool sparse)
        {
            var entities = Spawn(sparse);
            ref var arch = ref entities[1].ArchetypeRef;
            var expected = sparse ? new[] { 101, 105, 103 } : new[] { 100, 101, 102, 103, 104, 105, 106 };
            var chunk = new Chunk<Stab1, Stab2, Stab3>();
            chunk.SetData(ref arch);
            Assert.AreEqual(expected.Length, chunk.Count);
            var destination = stackalloc Stab1[8];
            for (var start = 0; start < expected.Length; start++)
            {
                Assert.AreEqual(expected[start], chunk.C1.V);
                for (var j = 0; j < 8; j++) destination[j].V = -777;
                var length = expected.Length - start;
                chunk.CopyTo(destination, length);
                for (var j = 0; j < length; j++)
                    Assert.AreEqual(expected[start + j], destination[j].V, "copy window start=" + start + ", index=" + j);
                Assert.AreEqual(-777, destination[length].V);
                Assert.AreEqual(start + 1 < expected.Length, chunk.MoveNext());
            }
            Assert.IsFalse(chunk.MoveNext());
            Assert.AreEqual(expected[expected.Length - 1], chunk.C1.V, "end must not advance the pointer past the last row");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Chunk4_CopyWindowsAndEnd(bool sparse)
        {
            var entities = Spawn(sparse);
            ref var arch = ref entities[1].ArchetypeRef;
            var expected = sparse ? new[] { 101, 105, 103 } : new[] { 100, 101, 102, 103, 104, 105, 106 };
            var chunk = new Chunk<Stab1, Stab2, Stab3, Stab4>();
            chunk.SetData(ref arch);
            Assert.AreEqual(expected.Length, chunk.Count);
            var destination = stackalloc Stab1[8];
            for (var start = 0; start < expected.Length; start++)
            {
                Assert.AreEqual(expected[start], chunk.C1.V);
                for (var j = 0; j < 8; j++) destination[j].V = -777;
                var length = expected.Length - start;
                chunk.CopyTo(destination, length);
                for (var j = 0; j < length; j++)
                    Assert.AreEqual(expected[start + j], destination[j].V, "copy window start=" + start + ", index=" + j);
                Assert.AreEqual(-777, destination[length].V);
                Assert.AreEqual(start + 1 < expected.Length, chunk.MoveNext());
            }
            Assert.IsFalse(chunk.MoveNext());
            Assert.AreEqual(expected[expected.Length - 1], chunk.C1.V, "end must not advance the pointer past the last row");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Chunk5_CopyWindowsAndEnd(bool sparse)
        {
            var entities = Spawn(sparse);
            ref var arch = ref entities[1].ArchetypeRef;
            var expected = sparse ? new[] { 101, 105, 103 } : new[] { 100, 101, 102, 103, 104, 105, 106 };
            var chunk = new Chunk<Stab1, Stab2, Stab3, Stab4, Stab5>();
            chunk.SetData(ref arch);
            Assert.AreEqual(expected.Length, chunk.Count);
            var destination = stackalloc Stab1[8];
            for (var start = 0; start < expected.Length; start++)
            {
                Assert.AreEqual(expected[start], chunk.C0.V);
                for (var j = 0; j < 8; j++) destination[j].V = -777;
                var length = expected.Length - start;
                chunk.CopyTo(destination, length);
                for (var j = 0; j < length; j++)
                    Assert.AreEqual(expected[start + j], destination[j].V, "copy window start=" + start + ", index=" + j);
                Assert.AreEqual(-777, destination[length].V);
                Assert.AreEqual(start + 1 < expected.Length, chunk.MoveNext());
            }
            Assert.IsFalse(chunk.MoveNext());
            Assert.AreEqual(expected[expected.Length - 1], chunk.C0.V, "end must not advance the pointer past the last row");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Chunk6_CopyWindowsAndEnd(bool sparse)
        {
            var entities = Spawn(sparse);
            ref var arch = ref entities[1].ArchetypeRef;
            var expected = sparse ? new[] { 101, 105, 103 } : new[] { 100, 101, 102, 103, 104, 105, 106 };
            var chunk = new Chunk<Stab1, Stab2, Stab3, Stab4, Stab5, Stab6>();
            chunk.SetData(ref arch);
            Assert.AreEqual(expected.Length, chunk.Count);
            var destination = stackalloc Stab1[8];
            for (var start = 0; start < expected.Length; start++)
            {
                Assert.AreEqual(expected[start], chunk.C0.V);
                for (var j = 0; j < 8; j++) destination[j].V = -777;
                var length = expected.Length - start;
                chunk.CopyTo(destination, length);
                for (var j = 0; j < length; j++)
                    Assert.AreEqual(expected[start + j], destination[j].V, "copy window start=" + start + ", index=" + j);
                Assert.AreEqual(-777, destination[length].V);
                Assert.AreEqual(start + 1 < expected.Length, chunk.MoveNext());
            }
            Assert.IsFalse(chunk.MoveNext());
            Assert.AreEqual(expected[expected.Length - 1], chunk.C0.V, "end must not advance the pointer past the last row");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Chunk7_CopyWindowsAndEnd(bool sparse)
        {
            var entities = Spawn(sparse);
            ref var arch = ref entities[1].ArchetypeRef;
            var expected = sparse ? new[] { 101, 105, 103 } : new[] { 100, 101, 102, 103, 104, 105, 106 };
            var chunk = new Chunk<Stab1, Stab2, Stab3, Stab4, Stab5, Stab6, Stab7>();
            chunk.SetData(ref arch);
            Assert.AreEqual(expected.Length, chunk.Count);
            var destination = stackalloc Stab1[8];
            for (var start = 0; start < expected.Length; start++)
            {
                Assert.AreEqual(expected[start], chunk.C0.V);
                for (var j = 0; j < 8; j++) destination[j].V = -777;
                var length = expected.Length - start;
                chunk.CopyTo(destination, length);
                for (var j = 0; j < length; j++)
                    Assert.AreEqual(expected[start + j], destination[j].V, "copy window start=" + start + ", index=" + j);
                Assert.AreEqual(-777, destination[length].V);
                Assert.AreEqual(start + 1 < expected.Length, chunk.MoveNext());
            }
            Assert.IsFalse(chunk.MoveNext());
            Assert.AreEqual(expected[expected.Length - 1], chunk.C0.V, "end must not advance the pointer past the last row");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Chunk8_CopyWindowsAndEnd(bool sparse)
        {
            var entities = Spawn(sparse);
            ref var arch = ref entities[1].ArchetypeRef;
            var expected = sparse ? new[] { 101, 105, 103 } : new[] { 100, 101, 102, 103, 104, 105, 106 };
            var chunk = new Chunk<Stab1, Stab2, Stab3, Stab4, Stab5, Stab6, Stab7, Stab8>();
            chunk.SetData(ref arch);
            Assert.AreEqual(expected.Length, chunk.Count);
            var destination = stackalloc Stab1[8];
            for (var start = 0; start < expected.Length; start++)
            {
                Assert.AreEqual(expected[start], chunk.C0.V);
                for (var j = 0; j < 8; j++) destination[j].V = -777;
                var length = expected.Length - start;
                chunk.CopyTo(destination, length);
                for (var j = 0; j < length; j++)
                    Assert.AreEqual(expected[start + j], destination[j].V, "copy window start=" + start + ", index=" + j);
                Assert.AreEqual(-777, destination[length].V);
                Assert.AreEqual(start + 1 < expected.Length, chunk.MoveNext());
            }
            Assert.IsFalse(chunk.MoveNext());
            Assert.AreEqual(expected[expected.Length - 1], chunk.C0.V, "end must not advance the pointer past the last row");
        }
    }
}
