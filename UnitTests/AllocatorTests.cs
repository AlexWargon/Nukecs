using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Tests
{
    [TestFixture]
    public unsafe class AllocatorTests
    {
        private MemAllocator* alloc;

        [SetUp]
        public void Setup()
        {
            alloc = MemAllocator.New(1024 * 64);
        }

        [TearDown]
        public void Teardown()
        {
            if (alloc != null && alloc->IsActive)
                MemAllocator.Destroy(alloc);
        }

        [Test]
        public void Allocate_Basic_ReturnsValidPointer()
        {
            var p = alloc->Allocate(64);
            Assert.IsTrue(p != null);
            Assert.IsTrue(alloc->MemoryUsed > 0);
        }

        [Test]
        public void AllocateRaw_Basic_ReturnsValidOffset()
        {
            var off = alloc->AllocateRaw(32);
            Assert.IsFalse(off.IsNull);
            Assert.AreEqual(0u, off.BlockIndex);
        }

        [Test]
        public void AllocatePtr_Generic_Works()
        {
            var p = alloc->AllocatePtr<int>();
            Assert.IsFalse(p.IsNull);
            *p.Ptr = 42;
            Assert.AreEqual(42, *p.Ptr);
        }

        [Test]
        public void Free_PtrOffset_RecoversMemory()
        {
            var usedBefore = alloc->MemoryUsed;
            var off = alloc->AllocateRaw(128);
            Assert.IsTrue(alloc->MemoryUsed > usedBefore);
            alloc->Free(off);
        }

        [Test]
        public void Free_VoidPtr_RecoversMemory()
        {
            void* p = alloc->Allocate(64);
            Assert.IsTrue(p != null);
            alloc->Free(p);
        }

        [Test]
        public void Free_BytePtr_RecoversMemory()
        {
            byte* p = (byte*)alloc->Allocate(64);
            Assert.IsTrue(p != null);
            alloc->Free(p);
        }

        [Test]
        public void Free_NullPtr_DoesNotCrash()
        {
            Assert.DoesNotThrow(() =>
            {
                alloc->Free((void*)null);
                alloc->Free((byte*)null);
                alloc->Free(ptr_offset.NULL);
                alloc->Free(ptr.NULL);
            });
        }

        [Test]
        public void Free_TypedPtr_Works()
        {
            var p = alloc->AllocatePtr<float>();
            alloc->Free(p);
        }

        [Test]
        public void Allocate_ManySmallBlocks_AllSucceed()
        {
            var offsets = new List<ptr_offset>();
            for (int i = 0; i < 100; i++)
            {
                var off = alloc->AllocateRaw(16);
                Assert.IsFalse(off.IsNull, $"Failed at iteration {i}");
                offsets.Add(off);
            }
            foreach (var off in offsets)
                alloc->Free(off);
        }

        [Test]
        public void Free_DoubleFree_DoesNotCorrupt()
        {
            var off = alloc->AllocateRaw(64);
            alloc->Free(off);
            Assert.DoesNotThrow(() => alloc->Free(off));
        }

        [Test]
        public void Free_AllMemory_RegionCursorResets()
        {
            var alloc2 = MemAllocator.New(4096);
            var cursorBefore = alloc2->GetRegion(0).cursor;
            var off1 = alloc2->AllocateRaw(64);
            var off2 = alloc2->AllocateRaw(64);
            Assert.IsTrue(alloc2->GetRegion(0).cursor > cursorBefore);

            alloc2->Free(off2);
            alloc2->Free(off1);

            Assert.AreEqual(cursorBefore, alloc2->GetRegion(0).cursor,
                "Cursor should rewind after freeing last blocks in LIFO order");
            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Coalescing_Forward_MergesWithNextFree()
        {
            var alloc2 = MemAllocator.New(4096);
            var off1 = alloc2->AllocateRaw(64);
            var off2 = alloc2->AllocateRaw(64);
            var off3 = alloc2->AllocateRaw(64);
            var off4 = alloc2->AllocateRaw(64);

            alloc2->Free(off3);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks, "off3 should be in free list");

            alloc2->Free(off2);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks,
                "Forward coalescing should merge off2+off3 into 1 free block");

            alloc2->Free(off1);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks,
                "Backward coalescing should merge off1+off2+off3");

            alloc2->Free(off4);
            Assert.AreEqual(0, alloc2->TotalFreeBlocks,
                "Full rewind after freeing last block");
            Assert.AreEqual(0, alloc2->GetRegion(0).cursor);

            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Coalescing_Backward_MergesWithPrevFree()
        {
            var alloc2 = MemAllocator.New(4096);
            var off1 = alloc2->AllocateRaw(64);
            var off2 = alloc2->AllocateRaw(64);
            var off3 = alloc2->AllocateRaw(64);
            var off4 = alloc2->AllocateRaw(64);

            alloc2->Free(off1);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks);

            alloc2->Free(off2);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks,
                "Backward coalescing should merge with previous free block");

            alloc2->Free(off3);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks);

            alloc2->Free(off4);
            Assert.AreEqual(0, alloc2->TotalFreeBlocks);

            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Coalescing_BothDirections_MergesAll()
        {
            var alloc2 = MemAllocator.New(4096);
            var off1 = alloc2->AllocateRaw(64);
            var off2 = alloc2->AllocateRaw(64);
            var off3 = alloc2->AllocateRaw(64);
            var off4 = alloc2->AllocateRaw(64);
            var off5 = alloc2->AllocateRaw(64);

            alloc2->Free(off1);
            alloc2->Free(off3);

            Assert.AreEqual(2, alloc2->TotalFreeBlocks);

            alloc2->Free(off2);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks,
                "Freeing between two free blocks should coalesce all three");

            alloc2->Free(off4);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks,
                "Forward merge with off5 still held");

            alloc2->Free(off5);
            Assert.AreEqual(0, alloc2->TotalFreeBlocks);
            Assert.AreEqual(0, alloc2->GetRegion(0).cursor);

            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Reuse_FreedBlock_IsReused()
        {
            var alloc2 = MemAllocator.New(4096);
            var off1 = alloc2->AllocateRaw(64);
            var off2 = alloc2->AllocateRaw(64);

            alloc2->Free(off1);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks, "off1 should be in free list (off2 holds cursor)");

            var off3 = alloc2->AllocateRaw(64);
            Assert.AreEqual(off1.BlockIndex, off3.BlockIndex,
                "Should reuse freed block from same region");
            Assert.AreEqual(off1.Offset, off3.Offset,
                "Should reuse exact same offset from free list");

            alloc2->Free(off3);
            alloc2->Free(off2);
            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Splitting_LargeBlock_ReturnsRemainderToFreeList()
        {
            var alloc2 = MemAllocator.New(4096);
            var off1 = alloc2->AllocateRaw(256);
            var stopper = alloc2->AllocateRaw(64);

            alloc2->Free(off1);
            Assert.AreEqual(1, alloc2->TotalFreeBlocks, "256-byte block should be in free list");

            var off2 = alloc2->AllocateRaw(64);
            Assert.AreEqual(off1.Offset, off2.Offset,
                "Should reuse start of freed 256-byte block for 64-byte request");

            Assert.AreEqual(1, alloc2->TotalFreeBlocks,
                "Remainder should be in free list after split");

            alloc2->Free(off2);
            alloc2->Free(stopper);
            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Rewind_RestoresState()
        {
            var alloc2 = MemAllocator.New(4096);
            var marker = alloc2->GetMarker();

            alloc2->AllocateRaw(64);
            alloc2->AllocateRaw(128);
            alloc2->AllocateRaw(256);

            Assert.IsTrue(alloc2->MemoryUsed > 0);

            alloc2->Rewind(marker);

            Assert.AreEqual(0, alloc2->GetRegion(0).cursor,
                "Cursor should reset after rewind");
            Assert.AreEqual(0, alloc2->TotalFreeBlocks,
                "Free list should be empty after rewind");

            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void MultipleRegions_AddsWhenFull()
        {
            var alloc2 = MemAllocator.New(4096);
            Assert.AreEqual(1, alloc2->RegionCount);

            var offsets = new ptr_offset[100];
            for (int i = 0; i < 100; i++)
                offsets[i] = alloc2->AllocateRaw(64);

            Assert.IsTrue(alloc2->RegionCount >= 2,
                "Should add a new region when first is full");

            foreach (var off in offsets)
                alloc2->Free(off);
            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Stress_AllocateFreeCycle()
        {
            var alloc2 = MemAllocator.New(1024 * 64);
            var offsets = new ptr_offset[500];

            for (int cycle = 0; cycle < 10; cycle++)
            {
                for (int i = 0; i < 500; i++)
                    offsets[i] = alloc2->AllocateRaw(16 + (i % 8) * 16);

                for (int i = 0; i < 500; i++)
                    alloc2->Free(offsets[i]);
            }

            Assert.IsTrue(alloc2->MemoryUsed <= alloc2->TotalSize);
            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Stress_LargeAllocations()
        {
            var alloc2 = MemAllocator.New(1024 * 256);
            var offsets = new ptr_offset[50];

            for (int cycle = 0; cycle < 5; cycle++)
            {
                for (int i = 0; i < 50; i++)
                    offsets[i] = alloc2->AllocateRaw(4096 + (i % 4) * 1024);

                for (int i = 0; i < 50; i++)
                    alloc2->Free(offsets[i]);
            }

            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Stress_RandomSizes()
        {
            var alloc2 = MemAllocator.New(1024 * 128);
            var rng = new Random(42);
            var active = new List<ptr_offset>();

            for (int i = 0; i < 1000; i++)
            {
                if (active.Count > 0 && rng.NextDouble() < 0.5)
                {
                    int idx = rng.Next(active.Count);
                    alloc2->Free(active[idx]);
                    active.RemoveAt(idx);
                }
                else
                {
                    int size = 16 + rng.Next(256);
                    var off = alloc2->AllocateRaw(size);
                    Assert.IsFalse(off.IsNull, $"Failed to allocate {size} bytes at iteration {i}");
                    active.Add(off);
                }
            }

            foreach (var off in active)
                alloc2->Free(off);

            Assert.AreEqual(0, alloc2->TotalFreeBlocks,
                "All freed: free list should be empty after full rewind");
            Assert.AreEqual(0, alloc2->GetRegion(0).cursor,
                "Region 0 cursor should be 0 after freeing everything in order");

            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Validate_FreeListIntegrity()
        {
            var alloc2 = MemAllocator.New(1024 * 16);
            var offsets = new List<ptr_offset>();

            for (int i = 0; i < 50; i++)
                offsets.Add(alloc2->AllocateRaw(16 + (i % 4) * 32));

            for (int i = 0; i < offsets.Count; i += 2)
            {
                alloc2->Free(offsets[i]);
                offsets[i] = ptr_offset.NULL;
            }

            Assert.IsTrue(ValidateFreeLists(alloc2),
                "Free list integrity check failed");
            MemAllocator.Destroy(alloc2);
        }

        [Test]
        public void Validate_CoalescingIntegrity()
        {
            var alloc2 = MemAllocator.New(4096);
            var offsets = new ptr_offset[10];
            for (int i = 0; i < 10; i++)
                offsets[i] = alloc2->AllocateRaw(64);

            alloc2->Free(offsets[2]);
            alloc2->Free(offsets[4]);
            alloc2->Free(offsets[6]);

            Assert.IsTrue(ValidateFreeLists(alloc2), "Before merge");

            alloc2->Free(offsets[3]);
            Assert.IsTrue(ValidateFreeLists(alloc2), "After forward+backward merge");

            alloc2->Free(offsets[5]);
            Assert.IsTrue(ValidateFreeLists(alloc2), "After mega merge");

            MemAllocator.Destroy(alloc2);
        }

        private static bool ValidateFreeLists(MemAllocator* a)
        {
            int rc = a->RegionCount;
            for (int i = 0; i < rc; i++)
            {
                ref var region = ref a->GetRegion(i);
                var visited = new HashSet<long>();

                for (int b = 0; b < 16; b++)
                {
                    long cur = region.buckets[b];
                    int count = 0;
                    while (cur != -1)
                    {
                        if (cur < 0 || cur >= region.cursor) return false;
                        if (!visited.Add(cur)) return false;
                        count++;
                        if (count > 10000) return false;

                        var node = (long*)(region.basePtr + cur + 16);
                        cur = node[1];
                    }
                }

                long lCur = region.largeFreeHead;
                int lc = 0;
                while (lCur != -1)
                {
                    if (lCur < 0 || lCur >= region.cursor) return false;
                    if (!visited.Add(lCur)) return false;
                    lc++;
                    if (lc > 10000) return false;

                    var node = (long*)(region.basePtr + lCur + 16);
                    lCur = node[1];
                }
            }
            return true;
        }
    }
}
