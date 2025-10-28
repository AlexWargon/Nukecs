using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Wargon.Nukecs
{
    public unsafe partial  struct World
    {
        public byte[] Serialize()
        {
            return UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.FastSerialize();
        }

        public void Deserialize(byte[] data)
        {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.FastDeserialize(data);
            for (var index = 0; index < UnsafeWorld->entities.Length; index++)
            {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld;
            }
        }

        public void SaveToFile(string path)
        {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.SaveToFile(path);
        }

        public void LoadFromFile(string path)
        {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.LoadFromFile(path);
            for (var index = 0; index < UnsafeWorld->entities.Length; index++)
            {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld;
            }
        }


        private void UpdateEntitiesWorld()
        {
            for (var index = 0; index < UnsafeWorld->entities.Length; index++)
            {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld->Self;
            }
        }
    }
    public partial struct World
    {
        public async void LoadFromFileAsync(string path)
        {
            UnsafeWorldRef.systemsUpdateJobDependencies.Complete();
            await UnsafeWorldRef.AllocatorHandler.AllocatorWrapper.Allocator.LoadFromFileAsync(path);
            UpdateEntitiesWorld();
        }

        public async void SaveToFileAsync(string path)
        {
            UnsafeWorldRef.systemsUpdateJobDependencies.Complete();
            await UnsafeWorldRef.AllocatorHandler.AllocatorWrapper.Allocator.SaveToFileAsync(path);
            
        }
        public static unsafe void Load(string filePath, ref World world)
        {
            var id = world.Id;
            if (!File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
            }
            dbug.log("create file stream");
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var data = new byte[fs.Length];

            _ = fs.Read(data, 0, data.Length);
            var allocatorHandler = world.unsafeWorldPtr.Ref.AllocatorHandler;
            var ecb = world.unsafeWorldPtr.Ref.EntityCommandBuffer;
            var a = world.unsafeWorldPtr.Ref.AllocatorRef;
            world.unsafeWorldPtr.Ref.systemsUpdateJobDependencies.Complete();

            a.FastDeserialize(Decompress(data));
            allocatorHandler.AllocatorWrapper.Allocator = a;
            world.unsafeWorldPtr.Ref.AllocatorHandler = allocatorHandler;
            world.unsafeWorldPtr.Ref.EntityCommandBuffer = ecb;
            world.unsafeWorldPtr.OnDeserialize(ref a);
            world.unsafeWorldPtr.Ref.OnDeserialize(ref a);
            for (var index = 0; index < world.unsafeWorldPtr.Ref.entities.Length; index++)
            {
                ref var entity = ref world.UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = world.UnsafeWorld;
            }

            Get(id) = world;
            dbug.log("loaded");
        }

        public unsafe void Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
            }
            
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var data = new byte[fs.Length];
            
            fs.Read(data, 0, data.Length);

            var a = unsafeWorldPtr.Ref.AllocatorRef;
            
            unsafeWorldPtr.Ref.systemsUpdateJobDependencies.Complete();
            a.FastDeserialize(MemAllocator.Decompress(data));
            
            unsafeWorldPtr.OnDeserialize(ref a);
            unsafeWorldPtr.Ref.AllocatorRef = a;
            unsafeWorldPtr.Ref.OnDeserialize(ref a);
            for (var index = 0; index < unsafeWorldPtr.Ref.entities.Length; index++)
            {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld;
            }
        }
        private static byte[] Decompress(byte[] inputData)
        {
            using var input = new MemoryStream(inputData);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }

}