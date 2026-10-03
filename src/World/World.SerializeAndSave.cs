using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Wargon.Nukecs {
    public unsafe partial struct World {
        public byte[] Serialize() {
            CompleteAllJobs(Id);
            return UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.FastSerialize();
        }

        public void Deserialize(byte[] data) {
            DeserializeCore(data, true);
        }

        private void DeserializeCore(byte[] data, bool completeJobs) {
            var id = Id;
            if (completeJobs) CompleteAllJobs(id);
            var ecb = ECB;
            var allocatorHandler = UnsafeWorldRef.AllocatorHandler;
            var allocatorOld = allocatorHandler.AllocatorWrapper.Allocator;
            allocatorOld.FastDeserialize(data);
            allocatorHandler.AllocatorWrapper.Allocator = allocatorOld;
            CompleteDeserialization(ref allocatorOld, ref allocatorHandler, ecb, id);
        }

        public void LoadFromFile(string path) {
            LoadFromFileCore(path, true);
        }

        internal void LoadFromFileCore(string path, bool completeJobs) {
            if (completeJobs) CompleteAllJobs(Id);
            DeserializeCore(Decompress(File.ReadAllBytes(path)), false);
        }

        /// <summary>Complete active jobs in all Systems containers belonging to this world.</summary>
        public void CompleteAllJobs() => CompleteAllJobs(Id);

        private void CompleteAllJobs(int id) {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->systemsFixedUpdateJobDependencies.Complete();
            foreach (var systems in WorldSystems.GetAll(id)) systems.Complete();
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
            SaveToFileCore(path, true);
        }

        internal void SaveToFileCore(string path, bool completeJobs) {
            if (completeJobs) CompleteAllJobs(Id);
            var snapshot = UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.FastSerialize();
            File.WriteAllBytes(path, MemAllocator.Compress(snapshot));
        }

        public partial struct WorldUnsafe {
            internal void OnDeserialize(ref MemAllocator allocator) {
                selfPtr.OnDeserialize(ref allocator);
                // tempMask is a fixed inline Bitmask1024 — no pointer fixup needed
#if NUKECS_DEBUG
                entitiesDens.OnDeserialize(ref allocator);
                storyLog.OnDeserialize(ref allocator);
#endif
                entities.OnDeserialize(ref allocator);
                World.ObserveEntityWorldToken(entityWorldToken);
                prefabsToSpawn.OnDeserialize(ref allocator);
                reservedEntities.OnDeserialize(ref allocator);
                rootArchetype.ptr.OnDeserialize(ref allocator);
                rootArchetype.ptr.Ref.OnDeserialize(ref allocator, selfPtr.Ptr);
                entityLocations.OnDeserialize(ref allocator);

                storagesList.OnDeserialize(ref allocator);
                foreach (ref var storagePtr in storagesList) {
                    storagePtr.OnDeserialize(ref allocator);
                    storagePtr.Ref.OnDeserialize(ref allocator, selfPtr.Ptr);
                }
                storagesMap.OnDeserialize(ref allocator);
                foreach (var entry in storagesMap)
                    entry.Value.OnDeserialize(ref allocator);

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
                eventsStorage.OnDeserialize(ref allocator);
            }
        }
    }

    public partial struct World {
        public async void LoadFromFileAsync(string path) {
            await LoadAsync(path, this);
        }

        public async Task SaveToFileAsync(string path) {
            // Capture the entire snapshot before the first await. File I/O never retains
            // arena pointers, so a later load/dispose cannot invalidate an in-flight save.
            var snapshot = Serialize();
            await using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
            await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
            await gzip.WriteAsync(snapshot, 0, snapshot.Length);
        }

        /// <summary>Read asynchronously, then complete current jobs and apply the snapshot.
        /// Assign the result: world = await World.LoadAsync(path, world).</summary>
        public static async Task<World> LoadAsync(string filePath, World world) {
            var id = world.Id;
            var lifetime = WorldIoRequests.Lifetime(id);
            world.CompleteAllJobs(id);
            byte[] compressed;
            await using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer);
                compressed = buffer.ToArray();
            }
            var snapshot = await DecompressAsync(compressed);
            // The arena can relocate during awaits; resolve the current world only when
            // applying, and never apply an old request to a recreated world slot.
            world = Get(id);
            if (!world.IsAlive || WorldIoRequests.Lifetime(id) != lifetime)
                throw new ObjectDisposedException(nameof(World));
            world.DeserializeCore(snapshot, true);
            return world;
        }

        public static void Load(string filePath, ref World world) {
            world.LoadFromFile(filePath);
        }

        public void Load(string filePath) {
            LoadFromFile(filePath);
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
