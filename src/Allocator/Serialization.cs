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
        // Serialize entire memory to byte array
        public unsafe byte[] Serialize()
        {
            var serializedData = new byte[totalSize];
            fixed (byte* destPtr = serializedData)
            {
                UnsafeUtility.MemCpy(destPtr, basePtr, totalSize);
            }

            return serializedData;
        }

        private static byte[] serializedAllocator = Array.Empty<byte>();
        // Deserialize byte array into allocator memory
        public unsafe void Deserialize(byte[] data)
        {
            var copySize = Math.Min(data.Length, totalSize);
            fixed (byte* srcPtr = data)
            {
                UnsafeUtility.MemCpy(basePtr, srcPtr, copySize);
            }
        }
        public unsafe byte[] FastSerialize()
        {
            byte[] data = new byte[sizeof(long) + sizeof(int) + totalSize + blockCount * sizeof(MemoryBlock)];
    
            fixed (byte* pData = data)
            {
                // Сохранить размер и количество блоков
                *((long*)pData) = totalSize;
                *((int*)(pData + sizeof(long))) = blockCount;

                // Копируем блоки памяти
                UnsafeUtility.MemCpy(pData + sizeof(long) + sizeof(int), blocks, blockCount * sizeof(MemoryBlock));
        
                // Копируем основную память
                UnsafeUtility.MemCpy(
                    pData + sizeof(long) + sizeof(int) + blockCount * sizeof(MemoryBlock), 
                    basePtr, 
                    totalSize);
            }
            return data;
        }
        public unsafe void FastSerialize(ref byte[] data)
        {
            var targetSize = (int)(sizeof(long) + sizeof(int) + totalSize + blockCount * sizeof(MemoryBlock));
            if (targetSize != data.Length)
            {
                Array.Resize(ref data, targetSize);
            }
            
            fixed (byte* pData = data)
            {
                // Сохранить размер и количество блоков
                *((long*)pData) = totalSize;
                *((int*)(pData + sizeof(long))) = blockCount;
                UnsafeUtility.MemCpy(pData + sizeof(long) + sizeof(int), blocks, blockCount * sizeof(MemoryBlock));
                UnsafeUtility.MemCpy(pData + sizeof(long) + sizeof(int) + blockCount * sizeof(MemoryBlock), basePtr, totalSize);
            }
        }
        public unsafe void FastDeserialize(byte[] data)
        {
            spinner.Acquire();
            fixed (byte* pData = data)
            {
                totalSize = *((long*)pData);
                blockCount = *((int*)(pData + sizeof(long)));
                UnsafeUtility.MemCpy(blocks, pData + sizeof(long) + sizeof(int), blockCount * sizeof(MemoryBlock));
                UnsafeUtility.MemCpy(basePtr, pData + sizeof(long) + sizeof(int) + blockCount * sizeof(MemoryBlock), totalSize);
            }
            spinner.Release();
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
            {
                Debug.LogError($"File not found: {filePath}");
            }
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            if (serializedAllocator.Length != (int)fs.Length)
            {
                Array.Resize(ref serializedAllocator, (int)fs.Length);
            }
            
            var data = await fs.ReadAsync(serializedAllocator, 0, serializedAllocator.Length);

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
        
        public unsafe void LoadFromFile(string filePath)
        {
            spinner.Acquire();
    
            if (!File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
            }
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            if (serializedAllocator.Length != (int)fs.Length)
            {
                Array.Resize(ref serializedAllocator, (int)fs.Length);
            }
            
            fs.Read(serializedAllocator, 0, serializedAllocator.Length);

            FastDeserialize(Decompress(serializedAllocator));
            
            spinner.Release();
        }

    }
}