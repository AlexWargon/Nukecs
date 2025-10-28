using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe partial struct World {
        public byte[] Serialize() {
            return UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.FastSerialize();
        }

        public void Deserialize(byte[] data) {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.FastDeserialize(data);
            for (var index = 0; index < UnsafeWorld->entities.Length; index++) {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld;
            }
        }

        public void SaveToFile(string path) {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.SaveToFile(path);
        }

        public void LoadFromFile(string path) {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.LoadFromFile(path);
            for (var index = 0; index < UnsafeWorld->entities.Length; index++) {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld;
            }
        }


        private void UpdateEntitiesWorld() {
            for (var index = 0; index < UnsafeWorld->entities.Length; index++) {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld->Self;
            }
        }
    }

    public partial struct World {
        public async void LoadFromFileAsync(string path) {
            UnsafeWorldRef.systemsUpdateJobDependencies.Complete();
            await UnsafeWorldRef.AllocatorHandler.AllocatorWrapper.Allocator.LoadFromFileAsync(path);
            UpdateEntitiesWorld();
        }

        public async Task SaveToFileAsync(string path) {
            UnsafeWorldRef.systemsUpdateJobDependencies.Complete();
            await UnsafeWorldRef.AllocatorHandler.AllocatorWrapper.Allocator.SaveToFileAsync(path);
        }

        private static unsafe void OnWorldLoad(ref WorldUnsafe worldUnsafe) {
            for (var index = 0; index < worldUnsafe.entities.Length; index++) {
                ref var entity = ref worldUnsafe.entities.Ptr[index];
                entity.worldPointer = worldUnsafe.selfPtr.Ptr;
            }
        }

        public static async Task LoadAsync(string filePath, World world) {
            var id = world.Id;
            try {
                if (!File.Exists(filePath)) throw new Exception($"File not found: {filePath}");

                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var data = new byte[fs.Length];

                await fs.ReadAsync(data, 0, data.Length);
                var w = world.unsafeWorldPtr;
                var allocatorHandler = w.Ref.AllocatorHandler;
                var a = w.Ref.AllocatorRef;
                w.Ref.systemsUpdateJobDependencies.Complete();
                var ecb = w.Ref.EntityCommandBuffer;
                a.FastDeserialize(Decompress(data));
                allocatorHandler.AllocatorWrapper.Allocator = a;
                w.OnDeserialize(ref a);
                w.Ref.EntityCommandBuffer = ecb;
                w.Ref.AllocatorHandler = allocatorHandler;
                w.Ref.OnDeserialize(ref a);
                
                OnWorldLoad(ref w.Ref);
                
                world.unsafeWorldPtr = w;
            }
            catch (Exception e) {
                dbug.error(e.Message);
                throw;
            }
            finally {
                Get(id) = world;
            }
        }

        public static void Load(string filePath, ref World world) {
            var id = world.Id;
            try {
                if (!File.Exists(filePath)) throw new Exception($"File not found: {filePath}");

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var data = new byte[fs.Length];

                _ = fs.Read(data, 0, data.Length);
                var w = world.unsafeWorldPtr;
                var allocatorHandler = w.Ref.AllocatorHandler;
                var a = w.Ref.AllocatorRef;
                w.Ref.systemsUpdateJobDependencies.Complete();
                var ecb = w.Ref.EntityCommandBuffer;
                a.FastDeserialize(Decompress(data));
                allocatorHandler.AllocatorWrapper.Allocator = a;
                w.OnDeserialize(ref a);
                w.Ref.EntityCommandBuffer = ecb;
                w.Ref.AllocatorHandler = allocatorHandler;
                w.Ref.OnDeserialize(ref a);
                OnWorldLoad(ref w.Ref);

                world.unsafeWorldPtr = w;
            }
            catch (Exception e) {
                dbug.error(e.Message);
                throw;
            }
            finally {
                Get(id) = world;
            }
        }

        public unsafe void Load(string filePath) {
            if (!File.Exists(filePath)) Debug.LogError($"File not found: {filePath}");

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var data = new byte[fs.Length];

            fs.Read(data, 0, data.Length);

            var a = unsafeWorldPtr.Ref.AllocatorRef;

            unsafeWorldPtr.Ref.systemsUpdateJobDependencies.Complete();
            a.FastDeserialize(MemAllocator.Decompress(data));

            unsafeWorldPtr.OnDeserialize(ref a);
            unsafeWorldPtr.Ref.AllocatorRef = a;
            unsafeWorldPtr.Ref.OnDeserialize(ref a);
            for (var index = 0; index < unsafeWorldPtr.Ref.entities.Length; index++) {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld;
            }
        }

        private static byte[] Decompress(byte[] inputData) {
            using var input = new MemoryStream(inputData);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}