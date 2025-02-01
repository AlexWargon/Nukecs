using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs
{
    [Serializable]
    public struct MemoryBlockData
    {
        public long Offset;
        public long Size;
        public bool IsUsed;
    }

    public unsafe partial struct SerializableMemoryAllocator
    {
        // Serialize entire memory to byte array
        public byte[] Serialize()
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
        public void Deserialize(byte[] data)
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
                dbug.log("data resized");
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
            fixed (byte* pData = data)
            {
                totalSize = *((long*)pData);
                blockCount = *((int*)(pData + sizeof(long)));
                UnsafeUtility.MemCpy(blocks, pData + sizeof(long) + sizeof(int), blockCount * sizeof(MemoryBlock));
                UnsafeUtility.MemCpy(basePtr, pData + sizeof(long) + sizeof(int) + blockCount * sizeof(MemoryBlock), totalSize);
            }
        }
        
        // public void SaveToFile(string path)
        // {
        //     var data = FastSerialize();
        //     using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
        //     fs.Write(data, 0, data.Length);
        // }
        //
        // public void LoadFromFile(string path)
        // {
        //     if (!File.Exists(path))
        //     {
        //         UnityEngine.Debug.LogError($"File not found: {path}");
        //     }
        //
        //     using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        //     var data = new byte[fs.Length];
        //     fs.Read(data, 0, data.Length);
        //     FastDeserialize(data);
        // }
        public void SaveToFile(string filePath)
        {
            spinner.Acquire();
            FastSerialize(ref serializedAllocator);
            using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            fs.Write(serializedAllocator, 0, serializedAllocator.Length);
            spinner.Release();
        }

        public void LoadFromFile(string filePath)
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
            FastDeserialize(serializedAllocator);
            
            spinner.Release();
            return;
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            // Загружаем метаданные
            totalSize = reader.ReadInt64();
            blockCount = reader.ReadInt32();

            // Освобождаем существующую память (если есть) и выделяем новую
            Dispose();
            basePtr = (byte*)UnsafeUtility.MallocTracked(totalSize, ALIGNMENT, Allocator.Persistent, 0);
            blocks = (MemoryBlock*)UnsafeUtility.MallocTracked(sizeof(MemoryBlock) * MAX_BLOCKS, ALIGNMENT, Allocator.Persistent, 0);

            // Загружаем блоки памяти
            for (int i = 0; i < blockCount; i++)
            {
                blocks[i].Size = reader.ReadInt32();
                blocks[i].IsUsed = reader.ReadBoolean();
            }

            // Загружаем данные памяти блоками с учётом выравнивания
            var buffer = new byte[ALIGNMENT];
            for (long i = 0; i < totalSize; i += ALIGNMENT)
            {
                int sizeToRead = (int)Math.Min(ALIGNMENT, totalSize - i);

                reader.Read(buffer, 0, sizeToRead);

                // Копируем данные из буфера вручную
                for (int j = 0; j < sizeToRead; j++)
                {
                    basePtr[i + j] = buffer[j];
                }
            }
            spinner.Release();
        }

    }
}