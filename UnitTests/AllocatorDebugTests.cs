using NUnit.Framework;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests
{
    // Arena Guard: runtime-toggleable corruption/leak detection for MemAllocator
    // (canary guards, freed-block poison, per-tag live stats).
    [TestFixture]
    public unsafe class AllocatorDebugTests
    {
        private MemAllocator* _allocator;
        private AllocatorDebugMode _prevMode;

        [SetUp]
        public void SetUp()
        {
            _prevMode = AllocatorDebugState.Mode;
            AllocatorDebugState.Mode = AllocatorDebugMode.None;
            _allocator = MemAllocator.New(256 * 1024);
        }

        [TearDown]
        public void TearDown()
        {
            AllocatorDebugState.Mode = _prevMode;
            if (_allocator != null) MemAllocator.Destroy(_allocator);
        }

        [Test]
        public void CleanArena_WithAllFlags_PassesValidation()
        {
            AllocatorDebugState.Mode = AllocatorDebugMode.All;

            // churn: various sizes, frees, free-list reuse (split path), fresh regions
            void* last = null;
            for (var i = 0; i < 512; i++)
            {
                var size = 16 + (i % 13) * 24;
                var p = _allocator->Allocate(size, AllocatorTags.Archetype);
                Assert.IsFalse(p == null, $"allocation {i} failed");
                if (last != null) _allocator->Free(last);
                last = p;
            }
            _allocator->Free(last);

            Assert.IsTrue(_allocator->Validate(out _), "churned arena with Canary+PoisonFree must validate clean");
        }

        [Test]
        public void Canary_WritePastAllocation_Detected()
        {
            AllocatorDebugState.Mode = AllocatorDebugMode.Canary;

            var p = (long*)_allocator->Allocate(64, AllocatorTags.Archetype);
            Assert.IsTrue(_allocator->Validate(out _), "fresh allocation must be intact");

            // user asked for 64 bytes; guard occupies [64..80) of the 80-byte slot.
            // p[8] = bytes [64..72) — inside the guard
            p[8] = 0x12345678;

            Assert.IsFalse(_allocator->Validate(out var v), "OOB write past the allocation must be detected");
            Assert.AreEqual(AllocatorDebugState.ViolationKind.CanaryBroken, v.Kind);
            Assert.AreEqual(AllocatorTags.Archetype, v.Tag, "violation must carry the allocation tag");
        }

        [Test]
        public void Canary_Off_NoDetection_NoLayoutChange()
        {
            AllocatorDebugState.Mode = AllocatorDebugMode.None;

            var p = (long*)_allocator->Allocate(64, AllocatorTags.Archetype);
            p[8] = 0x12345678; // would hit the guard if canary were on — must NOT be reported

            Assert.IsTrue(_allocator->Validate(out _), "with Canary off the same write must not be flagged");
        }

        [Test]
        public void PoisonFree_WriteIntoFreedBlock_Detected()
        {
            AllocatorDebugState.Mode = AllocatorDebugMode.PoisonFree;

            var p = (byte*)_allocator->Allocate(64, AllocatorTags.Query);
            // keeper: freeing the LAST block rewinds the region cursor (block leaves the
            // chain entirely) — the victim must stay in-chain to be walked by Validate
            var keeper = _allocator->Allocate(64, AllocatorTags.Archetype);
            _allocator->Free(p);
            Assert.IsTrue(_allocator->Validate(out _), "freshly freed block keeps its poison intact");

            p[3] = 0xAB; // use-after-free write

            Assert.IsFalse(_allocator->Validate(out var v), "write into a freed block must be detected");
            Assert.AreEqual(AllocatorDebugState.ViolationKind.FreedBlockWritten, v.Kind);
            // NOTE: no tag assert here — on freed blocks NextFree holds the free-list
            // link, the tag is only preserved on live blocks (canary violations carry it)
        }

        [Test]
        public void TagStats_CountsLiveBlocks_PerTag()
        {
            AllocatorDebugState.Mode = AllocatorDebugMode.TrackTags;

            var a = _allocator->Allocate(32, AllocatorTags.Archetype);
            var b = _allocator->Allocate(48, AllocatorTags.Archetype);
            var c = _allocator->Allocate(64, AllocatorTags.Query);
            var d = _allocator->Allocate(80, 0); // untagged
            _allocator->Free(b);

            var stats = new MemAllocator.TagStats();
            _allocator->GetTagStats(ref stats);

            var archCount = 0L; var archBytes = 0L;
            var queryCount = 0L; var untagged = 0L;
            for (var i = 0; i < stats.Length; i++)
            {
                if (stats.Tags[i] == AllocatorTags.Archetype) { archCount = stats.Counts[i]; archBytes = stats.Bytes[i]; }
                else if (stats.Tags[i] == AllocatorTags.Query) queryCount = stats.Counts[i];
                else if (stats.Tags[i] == AllocatorTags.Untagged) untagged = stats.Counts[i];
            }

            Assert.AreEqual(1, archCount, "freed Archetype block must not be counted");
            Assert.AreEqual(32, archBytes, "live Archetype bytes (user size, guard off)");
            Assert.AreEqual(1, queryCount);
            Assert.AreEqual(1, untagged);

            _allocator->Free(a);
            _allocator->Free(c);
            _allocator->Free(d);
        }

        [Test]
        public void PoisonAllFree_NormalizesBlocksFreedBeforeSwitch()
        {
            var p = (byte*)_allocator->Allocate(64, AllocatorTags.Archetype);
            var keeper = _allocator->Allocate(64, AllocatorTags.Archetype); // keep p in-chain (no cursor rewind)
            _allocator->Free(p); // freed with PoisonFree OFF — arbitrary bytes, no poison

            AllocatorDebugState.Mode = AllocatorDebugMode.PoisonFree;
            _allocator->PoisonAllFree();

            Assert.IsTrue(_allocator->Validate(out _),
                "after PoisonAllFree the pre-switch freed block must validate clean");
        }
    }
}
