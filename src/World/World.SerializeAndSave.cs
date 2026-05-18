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
            var id = Id;
            CompleteAllJobs(id);
            var ecb = ECB;
            var allocatorHandler = UnsafeWorldRef.AllocatorHandler;
            var allocatorOld = allocatorHandler.AllocatorWrapper.Allocator;
            allocatorOld.FastDeserialize(data);
            allocatorHandler.AllocatorWrapper.Allocator = allocatorOld;
            CompleteDeserialization(ref allocatorOld, ref allocatorHandler, ecb, id);
        }

        public void LoadFromFile(string path) {
            var id = Id;
            CompleteAllJobs(id);
            var ecb = ECB;
            var allocatorHandler = UnsafeWorldRef.AllocatorHandler;
            var allocator = allocatorHandler.AllocatorWrapper.Allocator;
            allocator.LoadFromFile(path);
            allocatorHandler.AllocatorWrapper.Allocator = allocator;
            CompleteDeserialization(ref allocator, ref allocatorHandler, ecb, id);
        }

        private void CompleteAllJobs(int id) {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            foreach (var systems in WorldSystems.GetAll(id))
                systems.Dependencies.Complete();
        }

        private void CompleteDeserialization(ref MemAllocator allocator, ref UnityAllocatorHandler allocatorHandler, EntityCommandBuffer savedEcb, int id) {
            ComponentTypeMap.ReRegisterFunctionPointers();
            unsafeWorldPtr.OnDeserialize(ref allocator);
            UnsafeWorld->OnDeserialize(ref allocator);
            UnsafeWorld->AllocatorHandler = allocatorHandler;
            UnsafeWorld->AllocatorRef = allocator;
            ECB = savedEcb;
            ECB.FixAfterDeserialize(UnsafeWorld, ref allocator);
            Get(id) = this;
            FixManagedWorld(id);
            ReinitAllSystems();
        }

        private void ReinitAllSystems() {
            var allSystems = WorldSystems.GetAll(Id);
            foreach (var systems in allSystems) {
                systems.OnWorldDeserialize(UnsafeWorld);
            }
        }

        public void SaveToFile(string path) {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.SaveToFile(path);
        }

        public partial struct WorldUnsafe {
            internal void OnDeserialize(ref MemAllocator allocator) {
                selfPtr.OnDeserialize(ref allocator);
                tempMask.OnDeserialize(ref allocator);
#if NUKECS_DEBUG
                entitiesDens.OnDeserialize(ref allocator);
                storyLog.OnDeserialize(ref allocator);
#endif
                entities.OnDeserialize(ref allocator);
                prefabsToSpawn.OnDeserialize(ref allocator);
                reservedEntities.OnDeserialize(ref allocator);
                rootArchetype.ptr.OnDeserialize(ref allocator);
                rootArchetype.ptr.Ref.OnDeserialize(ref allocator, selfPtr.Ptr);
                entitiesArchetypes.OnDeserialize(ref allocator);
                entityLocations.OnDeserialize(ref allocator);

                pools.OnDeserialize(ref allocator);
                foreach (ref var genericPool in pools) {
                    if (genericPool.IsCreated)
                        genericPool.OnDeserialize(ref allocator);
                }
                queriesHashToIndex.OnDeserialize(ref allocator);
                queries.OnDeserialize(ref allocator);
                foreach (ref var query in queries) {
                    query.OnDeserialize(ref allocator);
                    query.Ref.OnDeserialize(ref allocator);
                }

                archetypesList.OnDeserialize(ref allocator);
                foreach (ref var ptr in archetypesList) {
                    ptr.OnDeserialize(ref allocator);
                    ptr.Ref.OnDeserialize(ref allocator, selfPtr.Ptr);
                }
                archetypesMap.OnDeserialize(ref allocator);
                foreach (var kvPair in archetypesMap) {
                    kvPair.Value.ptr.OnDeserialize(ref allocator);
                    //kvPair.Value.ptr.Ref.OnDeserialize(ref allocator, selfPtr.Ptr);
                }
                DefaultNoneTypes.OnDeserialize(ref allocator);
                aspects.OnDeserialize(ref allocator);
                resStorage.OnDeserialize(ref allocator);
            }
        }
    }

    public partial struct World {
        public async void LoadFromFileAsync(string path) {
            var id = Id;
            CompleteAllJobs(id);
            var ecb = ECB;
            var allocatorHandler = UnsafeWorldRef.AllocatorHandler;

            // Async file I/O — no struct mutation, safe across awaits
            if (!File.Exists(path)) Debug.LogError($"File not found: {path}");
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var data = new byte[fs.Length];
            await fs.ReadAsync(data, 0, data.Length);
            var decompressed = await DecompressAsync(data);

            // Synchronous deserialization — FastDeserialize modifies the local copy correctly
            var allocator = allocatorHandler.AllocatorWrapper.Allocator;
            allocator.FastDeserialize(decompressed);
            allocatorHandler.AllocatorWrapper.Allocator = allocator;

            CompleteDeserialization(ref allocator, ref allocatorHandler, ecb, id);
        }

        public async Task SaveToFileAsync(string path) {
            UnsafeWorldRef.systemsUpdateJobDependencies.Complete();
            await UnsafeWorldRef.AllocatorHandler.AllocatorWrapper.Allocator.SaveToFileAsync(path);
        }

        public static async Task LoadAsync(string filePath, World world) {
            var id = world.Id;
            try {
                if (!File.Exists(filePath)) throw new Exception($"File not found: {filePath}");

                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var data = new byte[fs.Length];

                _ = await fs.ReadAsync(data, 0, data.Length);
                var w = world.unsafeWorldPtr;
                var allocatorHandler = w.Ref.AllocatorHandler;
                var a = w.Ref.AllocatorRef;
                w.Ref.systemsUpdateJobDependencies.Complete();
                foreach (var sys in WorldSystems.GetAll(id))
                    sys.Dependencies.Complete();
                var ecb = w.Ref.EntityCommandBuffer;
                var decompressed = await DecompressAsync(data);
                a.FastDeserialize(decompressed);
                ComponentTypeMap.ReRegisterFunctionPointers();
                allocatorHandler.AllocatorWrapper.Allocator = a;
                w.OnDeserialize(ref a);
                w.Ref.OnDeserialize(ref a);
                w.Ref.AllocatorRef = a;
                w.Ref.AllocatorHandler = allocatorHandler;
                w.Ref.EntityCommandBuffer = ecb;
                unsafe
                {
                    w.Ref.EntityCommandBuffer.FixAfterDeserialize(w.Ptr, ref a);
                }


                world.unsafeWorldPtr = w;
                FixManagedWorld(id);
                world.ReinitAllSystems();
            } catch (Exception e) {
                dbug.error(e.Message);
                throw;
            } finally {
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
                foreach (var sys in WorldSystems.GetAll(id))
                    sys.Dependencies.Complete();
                var ecb = w.Ref.EntityCommandBuffer;
                a.FastDeserialize(Decompress(data));
                ComponentTypeMap.ReRegisterFunctionPointers();
                allocatorHandler.AllocatorWrapper.Allocator = a;
                w.OnDeserialize(ref a);
                w.Ref.OnDeserialize(ref a);
                w.Ref.AllocatorRef = a;
                w.Ref.AllocatorHandler = allocatorHandler;
                w.Ref.EntityCommandBuffer = ecb;
                unsafe
                {
                    w.Ref.EntityCommandBuffer.FixAfterDeserialize(w.Ptr, ref a);
                }

                world.unsafeWorldPtr = w;
                FixManagedWorld(id);
                world.ReinitAllSystems();
            } catch (Exception e) {
                dbug.error(e.Message);
                throw;
            } finally {
                Get(id) = world;
            }
        }

        public unsafe void Load(string filePath) {
            if (!File.Exists(filePath)) Debug.LogError($"File not found: {filePath}");
            var id = Id;
            CompleteAllJobs(id);
            var ecb = ECB;
            var allocatorHandler = UnsafeWorldRef.AllocatorHandler;
            var allocator = allocatorHandler.AllocatorWrapper.Allocator;
            allocator.LoadFromFile(filePath);
            allocatorHandler.AllocatorWrapper.Allocator = allocator;
            CompleteDeserialization(ref allocator, ref allocatorHandler, ecb, id);
        }

        private static byte[] Decompress(byte[] inputData) {
            using var input = new MemoryStream(inputData);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();

            gzip.CopyTo(output);
            return output.ToArray();
        }

        private static async Task<byte[]> DecompressAsync(byte[] inputData) {
            await using var input = new MemoryStream(inputData);
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            await using var output = new MemoryStream();
            await gzip.CopyToAsync(output);
            return output.ToArray();
        }
    }
}