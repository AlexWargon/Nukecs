using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
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
        private static byte[] serializedAllocator = Array.Empty<byte>();

        public unsafe byte[] FastSerialize()
        {
            long headerSize = sizeof(int);
            long regionHeadersSize = regionCount * sizeof(long) * 2;
            long totalDataSize = 0;
            for (int i = 0; i < regionCount; i++) totalDataSize += regions[i].size;
            var totalBytes = (int)(headerSize + regionHeadersSize + totalDataSize);

            byte[] data = new byte[totalBytes];
            fixed (byte* pData = data)
            {
                byte* p = pData;
                *(int*)p = regionCount; p += sizeof(int);
                for (int i = 0; i < regionCount; i++)
                {
                    *(long*)p = regions[i].size; p += sizeof(long);
                    *(long*)p = regions[i].cursor; p += sizeof(long);
                }
                for (int i = 0; i < regionCount; i++)
                {
                    UnsafeUtility.MemCpy(p, regions[i].basePtr, regions[i].size);
                    p += regions[i].size;
                }
            }
            return data;
        }

        public unsafe void FastSerialize(ref byte[] data)
        {
            long headerSize = sizeof(int);
            long regionHeadersSize = regionCount * sizeof(long) * 2;
            long totalDataSize = 0;
            for (int i = 0; i < regionCount; i++) totalDataSize += regions[i].size;
            var targetSize = (int)(headerSize + regionHeadersSize + totalDataSize);
            if (targetSize != data.Length)
                Array.Resize(ref data, targetSize);

            fixed (byte* pData = data)
            {
                byte* p = pData;
                *(int*)p = regionCount; p += sizeof(int);
                for (int i = 0; i < regionCount; i++)
                {
                    *(long*)p = regions[i].size; p += sizeof(long);
                    *(long*)p = regions[i].cursor; p += sizeof(long);
                }
                for (int i = 0; i < regionCount; i++)
                {
                    UnsafeUtility.MemCpy(p, regions[i].basePtr, regions[i].size);
                    p += regions[i].size;
                }
            }
        }

        public unsafe void FastDeserialize(byte[] data)
        {
            fixed (byte* pData = data)
            {
                byte* p = pData;
                int savedRegionCount = *(int*)p; p += sizeof(int);

                for (int i = 0; i < regionCount; i++)
                {
                    if (regions[i].basePtr != null)
                        UnsafeUtility.Free(regions[i].basePtr, Allocator.Persistent);
                }

                while (regionCapacity < savedRegionCount)
                {
                    var newCap = regionCapacity * 2;
                    var newRegions = (Region*)UnsafeUtility.Malloc(sizeof(Region) * newCap, ALIGNMENT, Allocator.Persistent);
                    UnsafeUtility.MemCpy(newRegions, regions, sizeof(Region) * regionCount);
                    UnsafeUtility.MemClear(newRegions + regionCount, sizeof(Region) * (newCap - regionCount));
                    UnsafeUtility.Free(regions, Allocator.Persistent);
                    regions = newRegions;
                    regionCapacity = newCap;
                }

                regionCount = savedRegionCount;
                totalCapacity = 0;
                totalAllocated = 0;

                for (int i = 0; i < regionCount; i++)
                {
                    long size = *(long*)p; p += sizeof(long);
                    long cursor = *(long*)p; p += sizeof(long);
                    regions[i].basePtr = (byte*)UnsafeUtility.Malloc(size, ALIGNMENT, Allocator.Persistent);
                    regions[i].size = size;
                    regions[i].cursor = cursor;
                    totalCapacity += size;
                    totalAllocated += cursor;
                }

                for (int i = 0; i < regionCount; i++)
                {
                    UnsafeUtility.MemCpy(regions[i].basePtr, p, regions[i].size);
                    p += regions[i].size;
                }
            }
        }

        public void SaveToFile(string filePath)
        {
            spinner.Acquire();
            FastSerialize(ref serializedAllocator);
            using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            var data = Compress(serializedAllocator);
            fs.Write(data, 0, data.Length);
            spinner.Release();
        }

        public async Task SaveToFileAsync(string filePath)
        {
            spinner.Acquire();
            FastSerialize(ref serializedAllocator);
            await using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            var data = await CompressAsync(serializedAllocator);
            fs.Write(data, 0, data.Length);
            spinner.Release();
        }

        public async Task LoadFromFileAsync(string filePath)
        {
            spinner.Acquire();
            if (!File.Exists(filePath))
                Debug.LogError($"File not found: {filePath}");
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            if (serializedAllocator.Length != (int)fs.Length)
                Array.Resize(ref serializedAllocator, (int)fs.Length);
            await fs.ReadAsync(serializedAllocator, 0, serializedAllocator.Length);
            var decompressedData = await DecompressAsync(serializedAllocator);
            FastDeserialize(decompressedData);
            spinner.Release();
        }

        private static async Task<byte[]> CompressAsync(byte[] data)
        {
            using var memoryStream = new MemoryStream();
            var gzip = new GZipStream(memoryStream, CompressionLevel.Optimal);
            await gzip.WriteAsync(data, 0, serializedAllocator.Length);
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
            gzip.Write(data, 0, serializedAllocator.Length);
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
            spinner.Acquire();
            if (!File.Exists(filePath))
                Debug.LogError($"File not found: {filePath}");
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            if (serializedAllocator.Length != (int)fs.Length)
                Array.Resize(ref serializedAllocator, (int)fs.Length);
            fs.Read(serializedAllocator, 0, serializedAllocator.Length);
            FastDeserialize(Decompress(serializedAllocator));
            spinner.Release();
        }
    }
}
