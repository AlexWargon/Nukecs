using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Wargon.Nukecs
{
    [Serializable]
    public struct MemoryBlockData
    {
        public long Offset;
        public long Size;
        public bool IsUsed;
    }

    public partial struct MemAllocator
    {
        // Save header: [int magic][int formatVersion][int regionCount]. Validated BEFORE any
        // region memory is freed/reallocated so a corrupt or foreign save fails with a clear
        // exception instead of writing OOB into the fixed regions array.
        internal const int SAVE_MAGIC = 0x4E434B53; // 'NCKS'
        internal const int SAVE_FORMAT_VERSION = NukEcs.version;

        private static unsafe int ValidateSaveHeader(byte* p, int dataLength)
        {
            var headerSize = sizeof(int) * 3;
            if (dataLength < headerSize)
                throw new ArgumentException(
                    $"[Nukecs] Save is truncated: {dataLength} bytes is smaller than the {headerSize}-byte header");
            var magic = *(int*)p;
            if (magic != SAVE_MAGIC)
                throw new ArgumentException(
                    $"[Nukecs] Not a Nukecs save (bad magic 0x{magic:X8})");
            var formatVersion = *(int*)(p + sizeof(int));
            if (formatVersion != SAVE_FORMAT_VERSION)
                throw new ArgumentException(
                    $"[Nukecs] Save format version mismatch: file has {formatVersion}, runtime expects {SAVE_FORMAT_VERSION}");
            var regionCount = *(int*)(p + sizeof(int) * 2);
            if (regionCount < 0 || regionCount > MAX_REGIONS)
                throw new ArgumentException(
                    $"[Nukecs] Save is corrupt: region count {regionCount} is outside [0, {MAX_REGIONS}]");
            return regionCount;
        }

        public unsafe byte[] FastSerialize()
        {
            long headerSize = sizeof(int) * 3;
            long regionHeadersSize = regionCount * sizeof(long) * 2;
            long totalDataSize = 0;
            for (int i = 0; i < regionCount; i++) totalDataSize += regions[i].size;
            var totalBytes = (int)(headerSize + regionHeadersSize + totalDataSize);

            byte[] data = new byte[totalBytes];
            fixed (byte* pData = data)
            {
                byte* p = pData;
                *(int*)p = SAVE_MAGIC; p += sizeof(int);
                *(int*)p = SAVE_FORMAT_VERSION; p += sizeof(int);
                *(int*)p = regionCount; p += sizeof(int);
                for (int i = 0; i < regionCount; i++)
                {
                    *(long*)p = regions[i].size; p += sizeof(long);
                    *(long*)p = regions[i].cursor; p += sizeof(long);
                }
                for (int i = 0; i < regionCount; i++)
                {
                    Mem.MemCpy(p, regions[i].basePtr, regions[i].size);
                    p += regions[i].size;
                }
            }
            return data;
        }

        public unsafe void FastSerialize(ref byte[] data)
        {
            long headerSize = sizeof(int) * 3;
            long regionHeadersSize = regionCount * sizeof(long) * 2;
            long totalDataSize = 0;
            for (int i = 0; i < regionCount; i++) totalDataSize += regions[i].size;
            var targetSize = (int)(headerSize + regionHeadersSize + totalDataSize);
            if (targetSize != data.Length)
                Array.Resize(ref data, targetSize);

            fixed (byte* pData = data)
            {
                byte* p = pData;
                *(int*)p = SAVE_MAGIC; p += sizeof(int);
                *(int*)p = SAVE_FORMAT_VERSION; p += sizeof(int);
                *(int*)p = regionCount; p += sizeof(int);
                for (int i = 0; i < regionCount; i++)
                {
                    *(long*)p = regions[i].size; p += sizeof(long);
                    *(long*)p = regions[i].cursor; p += sizeof(long);
                }
                for (int i = 0; i < regionCount; i++)
                {
                    Mem.MemCpy(p, regions[i].basePtr, regions[i].size);
                    p += regions[i].size;
                }
            }
        }

        public unsafe void FastDeserialize(byte[] data)
        {
            fixed (byte* pData = data)
            {
                // header validated and regionCount bounds-checked BEFORE any memory is
                // touched; p is already positioned past the header — do NOT re-read the
                // count here (that would consume the first region's size field)
                var savedRegionCount = ValidateSaveHeader(pData, data.Length);
                byte* p = pData + sizeof(int) * 3;

                for (int i = 0; i < regionCount; i++)
                {
                    if (regions[i].basePtr != null)
                        UnsafeUtility.Free(regions[i].basePtr, Allocator.Persistent);
                }

                regionCount = savedRegionCount;
                totalCapacity = 0;
                totalAllocated = 0;

                for (int i = 0; i < regionCount; i++)
                {
                    long size = *(long*)p; p += sizeof(long);
                    long cursor = *(long*)p; p += sizeof(long);
                    regions[i].basePtr = (byte*)UnsafeUtility.Malloc(size, ALIGN, Allocator.Persistent);
                    regions[i].size = size;
                    regions[i].cursor = cursor;
                    regions[i].freeHead = NPOS;
                    regions[i].freeCount = 0;
                    totalCapacity += size;
                    totalAllocated += cursor;
                }

                for (int i = 0; i < regionCount; i++)
                {
                    Mem.MemCpy(regions[i].basePtr, p, regions[i].size);
                    p += regions[i].size;
                }

                // The restored bytes carry freed-block headers (negative Size) from the
                // saved session, but the free-list METADATA above was reset — relink the
                // lists from the headers themselves or later Dealloc/Alloc coalesce
                // against ghost blocks and corrupt the arena.
                for (int i = 0; i < regionCount; i++)
                {
                    RebuildFreeList(i);
                }
            }
            // normalize free blocks to the poisoned state when the flag is on — the save
            // may come from a session without PoisonFree and would trip Validate
            if ((AllocatorDebugState.Mode & AllocatorDebugMode.PoisonFree) != 0)
                PoisonAllFree();
            // Arena Guard: a corrupt/incompatible save is reported as a clear validation
            // error here instead of an NRE storm during the pointer-fixup walk
            ValidateAndReport("load");
        }

        public void SaveToFile(string filePath)
        {
            lock_.Acquire();
            // local buffer, not the shared static — concurrent saves from different worlds
            // must not race on one byte[]
            var buffer = Array.Empty<byte>();
            FastSerialize(ref buffer);
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            var data = Compress(buffer);
            fs.Write(data, 0, data.Length);
            lock_.Release();
        }

        public async Task SaveToFileAsync(string filePath)
        {
            lock_.Acquire();
            var buffer = Array.Empty<byte>();
            FastSerialize(ref buffer);
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            var data = await CompressAsync(buffer);
            fs.Write(data, 0, data.Length);
            lock_.Release();
        }

        public async Task LoadFromFileAsync(string filePath)
        {
            lock_.Acquire();
            if (!File.Exists(filePath))
                dbug.error($"File not found: {filePath}");
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[fs.Length];
            var read = await fs.ReadAsync(buffer, 0, buffer.Length);
            if (read != buffer.Length)
            {
                lock_.Release();
                throw new IOException($"[Nukecs] Load failed: read {read} of {buffer.Length} bytes from {filePath}");
            }
            var decompressedData = await DecompressAsync(buffer);
            FastDeserialize(decompressedData);
            lock_.Release();
        }

        private static async Task<byte[]> CompressAsync(byte[] data)
        {
            using var memoryStream = new MemoryStream();
            var gzip = new GZipStream(memoryStream, CompressionLevel.Optimal);
            // write data.Length, not some cached buffer's length (they are not the same array)
            await gzip.WriteAsync(data, 0, data.Length);
            gzip.Close();
            await gzip.DisposeAsync();
            return memoryStream.ToArray();
        }

        private static async Task<byte[]> DecompressAsync(byte[] inputData)
        {
            using var input = new MemoryStream(inputData);
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            await gzip.CopyToAsync(output);
            return output.ToArray();
        }

        public static byte[] Compress(byte[] data)
        {
            using var memoryStream = new MemoryStream();
            using var gzip = new GZipStream(memoryStream, CompressionLevel.Optimal);
            gzip.Write(data, 0, data.Length);
            gzip.Close();
            return memoryStream.ToArray();
        }

        public static byte[] Decompress(byte[] inputData)
        {
            using var input = new MemoryStream(inputData);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        public void LoadFromFile(string filePath)
        {
            lock_.Acquire();
            if (!File.Exists(filePath))
                dbug.error($"File not found: {filePath}");
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[fs.Length];
            var read = fs.Read(buffer, 0, buffer.Length);
            if (read != buffer.Length)
            {
                lock_.Release();
                throw new IOException($"[Nukecs] Load failed: read {read} of {buffer.Length} bytes from {filePath}");
            }
            FastDeserialize(Decompress(buffer));
            lock_.Release();
        }
    }
}
