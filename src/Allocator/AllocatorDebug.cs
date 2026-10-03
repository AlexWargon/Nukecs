using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Arena Guard: runtime-toggleable corruption/leak detection for <see cref="MemAllocator"/>.
    /// Flags live in a SharedStatic so Burst-compiled code (ECB playback, systems) reads them
    /// without recompiles; reporting goes through [BurstDiscard] void methods only
    /// (plain static fields would silently break Burst compilation of every touching job —
    /// see HANDOFF "Burst-правило для diagnostics-инструменталки").
    /// </summary>
    [System.Flags]
    public enum AllocatorDebugMode : int
    {
        None = 0,
        /// <summary>Guard 16 bytes after each allocation's aligned data slot; checked by Validate.</summary>
        Canary = 1,
        /// <summary>Fill freed user memory with 0xDD; Validate flags writes into freed blocks (UAF).</summary>
        PoisonFree = 2,
        /// <summary>Collect per-tag live allocation stats (GetTagStats) for the debug window.</summary>
        TrackTags = 4,
        All = Canary | PoisonFree | TrackTags
    }

    /// <summary>Well-known allocation tag ids. Values are stored in the block header (int).</summary>
    public static class AllocatorTags
    {
        public const int Untagged = 0;
        public const int Archetype = 1;
        public const int ArchetypeMask = 2;
        public const int Storage = 3;
        public const int Query = 4;
        public const int MemoryList = 5;
        public const int MemoryArray = 6;
        public const int HashMap = 7;
        public const int Events = 8;
        public const int Pool = 9;
        public const int Ecb = 10;
        public const int WorldMisc = 11;

        /// <summary>Managed-only name table for UI. Not touched by Burst code.</summary>
        [BurstDiscard]
        public static string NameOf(int tag)
        {
            switch (tag)
            {
                case Untagged: return "Untagged";
                case Archetype: return "Archetype";
                case ArchetypeMask: return "Archetype.Masks";
                case Storage: return "Storage";
                case Query: return "Query";
                case MemoryList: return "MemoryList";
                case MemoryArray: return "MemoryArray";
                case HashMap: return "HashMap";
                case Events: return "Events";
                case Pool: return "Pool";
                case Ecb: return "ECB";
                case WorldMisc: return "World";
                default: return $"Tag_{tag}";
            }
        }
    }

    public static class AllocatorDebugState
    {
        private struct SharedStaticKey { }

        private static readonly SharedStatic<AllocatorDebugMode> mode =
            SharedStatic<AllocatorDebugMode>.GetOrCreate<SharedStaticKey>();

        /// <summary>Burst-readable/writable. Default None → zero-cost paths.</summary>
        public static ref AllocatorDebugMode Mode => ref mode.Data;

        public static bool Has(AllocatorDebugMode flag) => (mode.Data & flag) != 0;

        /// <summary>Kind of the first arena violation found by Validate. No managed types — Burst-safe payload.</summary>
        public struct Violation
        {
            public int Region;
            /// <summary>Block header offset inside the region.</summary>
            public long BlockOffset;
            /// <summary>Allocated data size (absolute value of the header Size).</summary>
            public long DataSize;
            public int Tag;
            /// <summary>Meaningful for live-block violations (canary). Freed blocks reuse
            /// NextFree for the free-list link, so their tag reads -1.</summary>
            public ViolationKind Kind;
            public bool IsValid => Kind != ViolationKind.None;
        }

        public enum ViolationKind : byte
        {
            None = 0,
            BadHeaderSize = 1,      // size < ALIGN or not A16-aligned — chain is broken
            ChainOutOfCursor = 2,   // block chain stepped past the region cursor
            CanaryBroken = 3,       // write past the end of a live allocation
            FreedBlockWritten = 4   // write into a freed (poisoned) block — use-after-free
        }

        [BurstDiscard]
        internal static void Report(in Violation v, string context)
        {
            UnityEngine.Debug.LogError(
                $"[ArenaGuard] CORRUPTED ARENA ({context}): {v.Kind} at region {v.Region}, " +
                $"block offset {v.BlockOffset}, data size {v.DataSize}, tag {AllocatorTags.NameOf(v.Tag)}. " +
                "Nearby native writes are the suspect (OOB or use-after-free).");
        }

        [BurstDiscard]
        internal static void ReportClean(string context, long usedBlocks, long usedBytes)
        {
            UnityEngine.Debug.Log($"[ArenaGuard] {context}: arena OK ({usedBlocks} live blocks, {usedBytes} bytes).");
        }
    }
}
