using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs {
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    using static UnsafeStatic;

    public unsafe struct EntityCommandBuffer : IDisposable {
        [NativeDisableUnsafePtrRestriction] private ECBInternal* ecb;

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (!IsCreated) return 0;
                return ecb->totalCount;
            }
        }

        public bool HasCommands {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsCreated && ecb->totalCount > 0;
        }

        public bool IsCreated => ecb != null && ecb->isCreated == 1;

        internal void FixAfterDeserialize(World.WorldUnsafe* newWorld, ref MemAllocator allocator) {
            if (ecb == null) return;
            ecb->world = newWorld;
            // tempMask is a fixed Bitmask1024 (inline value) — nothing to re-fix
        }

        internal static int ThreadIndex => JobsUtility.ThreadIndex;
        internal readonly AllocatorHandle allocator;

        public EntityCommandBuffer(int startSize, AllocatorHandle allocator, World.WorldUnsafe* world) {
            this.allocator = allocator;
            ecb = (ECBInternal*)Mem.MallocTracked(sizeof(ECBInternal),
                Mem.AlignOf<ECBInternal>(), allocator, 0);
            *ecb = new ECBInternal();
            ecb->perThreadCommands = CreateCommandBuffers(startSize, this.allocator);
            ecb->perThreadData = CreateDataBuffers(startSize * 64, this.allocator);
            ecb->world = world;
            // tempMask: fixed 1024-bit inline value — no world-allocator allocation,
            // no growth, no save/load fixups (was a DynamicBitmask in the arena)
            ecb->isCreated = 1;
        }

        // One list per thread, each in its own allocation (writers never share a header).
        private HeapList<HeapList<ECBCommand>> CreateCommandBuffers(int startSize, AllocatorHandle alloc) {
            var threads = JobsUtility.ThreadIndexCount + 2;
            var lists = new HeapList<HeapList<ECBCommand>>(threads, alloc);
            for (var i = 0; i < threads; i++)
                lists.Add(new HeapList<ECBCommand>(startSize, alloc));
            return lists;
        }

        private HeapList<HeapList<byte>> CreateDataBuffers(int startBytes, AllocatorHandle alloc) {
            var threads = JobsUtility.ThreadIndexCount + 2;
            var lists = new HeapList<HeapList<byte>>(threads, alloc);
            for (var i = 0; i < threads; i++)
                lists.Add(new HeapList<byte>(startBytes, alloc));
            return lists;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ECBCommand {
            public int Entity;
            public ushort Generation;
            public ushort TargetGeneration;
            public ushort WorldToken;
            public int ComponentType;
            public int AdditionalData;
            public Type EcbCommandType;
            public byte active;
            public byte isDisposable;
            public enum Type : short {
                AddComponent = 0,
                AddComponentNoData = 1,
                RemoveComponent = 2,
                SetComponent = 3,
                CreateEntity = 4,
                DestroyEntity = 5,
                SetActiveGameObject = 6,
                PlayParticleReference = 7,
                Copy = 8,
                CreateCopy = 9,
                RemoveAndDispose = 10,
                SpawnPrefab = 11
            }
        }

        internal struct ECBInternal {
            internal byte isCreated;
            internal int totalCount;
            internal HeapList<HeapList<ECBCommand>> perThreadCommands;
            internal HeapList<HeapList<byte>> perThreadData;
            [NativeDisableUnsafePtrRestriction]
            internal World.WorldUnsafe* world;
            /// <summary>Fixed-size scratch mask for batch type-set rebuild — deliberately NOT
            /// a DynamicBitmask: no world-allocator allocation/growth and no save/load fixups.</summary>
            internal Bitmask1024 tempMask;

            public bool IsCreated => isCreated == 1;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear() {
                totalCount = 0;
                for (var i = 0; i < perThreadCommands.Length; i++) {
                    var commands = perThreadCommands.ElementAt(i);
                    var data = perThreadData.ElementAt(i);
                    for (var j = 0; j < commands.Length; j++)
                        DisposePending(ref commands.Ptr[j], data.Ptr);
                    perThreadCommands.ElementAt(i).Clear();
                    perThreadData.ElementAt(i).Clear();
                }
            }

            internal static void DisposePending(ref ECBCommand cmd, byte* data) {
                if (cmd.EcbCommandType != ECBCommand.Type.AddComponent || cmd.isDisposable == 0) return;
                cmd.isDisposable = 0;
                var type = ComponentTypeMap.GetComponentType(cmd.ComponentType);
                type.DisposeFn().Invoke(data + cmd.AdditionalData, 0);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set<T>(int entity, int thread) where T : unmanaged {
                var ctData = ComponentType<T>.Data;
                ref var loc = ref world->entityLocations.Ptr[entity];
                ref var arch = ref world->archetypesList.Ptr[loc.archetypeIndex].Ref;
                var ptr = arch.GetComponentDataPtr(ctData.index, loc.row);
                if (ptr != null)
                    Mem.MemClear(ptr, ctData.size);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, T* componentPtr, int thread) where T : unmanaged {
                ref var data = ref ComponentType<T>.Data;
                var buf = perThreadData.ElementAt(thread);
                var dataOffset = buf.Length;
                buf.AddRange((byte*)componentPtr, data.size);
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    AdditionalData = dataOffset,
                    isDisposable = data.isDisposable ? (byte)1 : (byte)0,
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, T component, int thread) where T : unmanaged {
                ref var data = ref ComponentType<T>.Data;
                var buf = perThreadData.ElementAt(thread);
                var dataOffset = buf.Length;
                buf.AddRange((byte*)&component, data.size);
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    AdditionalData = dataOffset,
                    isDisposable = data.isDisposable ? (byte)1 : (byte)0,
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddObject(int entity, IComponent component, ComponentTypeData data) {
                var thread = JobsUtility.ThreadIndex;
                var buf = perThreadData.ElementAt(thread);
                var dataOffset = buf.Length;
                var size = data.size;
                var newLen = buf.Length + size;
                if (newLen > buf.Capacity)
                    buf.Capacity = Math.Max(buf.Capacity * 2, newLen);
                ComponentHelpers.Write(buf.Ptr + dataOffset, 0, size, data.index, component);
                buf.ResizeUninitialized(newLen);
                
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    AdditionalData = dataOffset,
                    isDisposable = data.isDisposable ? (byte)1 : (byte)0,
                });
                totalCount++;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, int thread) where T : unmanaged {
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.AddComponentNoData,
                    ComponentType = ComponentType<T>.Index
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(int entity, int thread, int componentType) {
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.AddComponentNoData,
                    ComponentType = componentType
                });
                totalCount++;
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove<T>(int entity, int thread) where T : unmanaged {
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.RemoveComponent,
                    ComponentType = ComponentType<T>.Index
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove(int entity, int component, int thread) {
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.RemoveComponent,
                    ComponentType = component
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveAndDispose<T>(int entity, int thread) where T : unmanaged {
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.RemoveAndDispose,
                    ComponentType = ComponentType<T>.Index
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Destroy(int entity, int thread) {
                perThreadCommands.ElementAt(thread).Add(new ECBCommand { Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken, EcbCommandType = ECBCommand.Type.DestroyEntity });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnableGameObject(int entity, bool value, int thread) {
                byte v = value ? (byte)1 : (byte)0;
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken, EcbCommandType = ECBCommand.Type.SetActiveGameObject, active = v
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CreateEntity() {
                return 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void PlayParticleReference(int entity, bool value, int thread) {
                var v = value ? (byte)1 : (byte)0;
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken, EcbCommandType = ECBCommand.Type.PlayParticleReference, active = v
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Copy(int entity, int thread) {
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = entity, Generation = world->entities.Ptr[entity].Generation, WorldToken = world->entityWorldToken,
                    EcbCommandType = ECBCommand.Type.CreateCopy
                });
                totalCount++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Copy(int from, int to, int thread) {
                perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                    Entity = from,
                    Generation = world->entities.Ptr[from].Generation, WorldToken = world->entityWorldToken,
                    TargetGeneration = world->entities.Ptr[to].Generation,
                    EcbCommandType = ECBCommand.Type.Copy,
                    AdditionalData = to
                });
                totalCount++;
            }

            internal void ProcessEntityBatch(ref World world, int entity, ECBCommand* cmds, int count, byte* dataBuffer) {
                var w = world.UnsafeWorld;
                if (cmds[0].WorldToken != w->entityWorldToken || !w->EntityIsValid(entity, cmds[0].Generation)) {
                    for (var i = 0; i < count; i++) DisposePending(ref cmds[i], dataBuffer);
                    return;
                }
                var originalArchIdx = w->entityLocations.Ptr[entity].archetypeIndex;
                ref var originalArch = ref w->archetypesList.Ptr[originalArchIdx].Ref;

                originalArch.CopyMasksTo(ref tempMask);
                var destroyed = false;

                for (var i = 0; i < count; i++) {
                    ref var cmd = ref cmds[i];
                    switch (cmd.EcbCommandType) {
                        case ECBCommand.Type.AddComponent:
                            if (tempMask.Has(cmd.ComponentType)) break;
                            tempMask.Add(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentNoData:
                            if (tempMask.Has(cmd.ComponentType)) break;
                            tempMask.Add(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveComponent:
                            if (!tempMask.Has(cmd.ComponentType)) break;
                            tempMask.Remove(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveAndDispose:
                            if (!tempMask.Has(cmd.ComponentType)) break;
                            tempMask.Remove(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.CreateEntity:
                            world.Entity();
                            break;
                        case ECBCommand.Type.DestroyEntity:
                            destroyed = true;
                            break;
                        case ECBCommand.Type.Copy:
                            if (!w->EntityIsValid(cmd.AdditionalData, cmd.TargetGeneration)) break;
                            w->archetypesList.Ptr[originalArchIdx].Ref.Copy(entity, cmd.AdditionalData);
                            break;
                        case ECBCommand.Type.CreateCopy:
                            break;
                    }
                    if (destroyed) break;
                }

                if (destroyed) {
                    for (var i = 0; i < count; i++) DisposePending(ref cmds[i], dataBuffer);
                    if (!w->EntityIsValid(entity)) return;
                    var loc = w->entityLocations.Ptr[entity];
                    ref var arch = ref w->archetypesList.Ptr[loc.archetypeIndex].Ref;
                    for (var i = 0; i < arch.types.length; i++) {
                        var type = arch.types.Ptr[i];
                        if (ComponentTypeMap.GetComponentType(type).storageType == StorageType.Pool)
                            w->GetUntypedPool(type).Remove(entity);
                    }
                    if (loc.archetypeIndex != 0) {
                        arch.DestroyEntity(loc.row);
                        arch.ExecuteDestroyEdge(entity);
                    }
                    w->OnDestroyEntity(entity);
                    return;
                }

                // Pool-storage removals must also clear the pool slot: a mask-only migration
                // (same storage, pool bit flipped) leaves the stale slot behind otherwise
                for (var i = 0; i < count; i++)
                {
                    ref var cmd = ref cmds[i];
                    if (cmd.EcbCommandType != ECBCommand.Type.RemoveComponent
                        && cmd.EcbCommandType != ECBCommand.Type.RemoveAndDispose) continue;
                    var cmdType = ComponentTypeMap.GetComponentType(cmd.ComponentType);
                    if (cmdType.storageType != StorageType.Pool) continue;
                    if (!originalArch.Has(cmd.ComponentType)) continue;
                    w->GetUntypedPool(cmd.ComponentType).Remove(entity);
                }

                var targetArch = w->GetOrCreateArchetype(ref tempMask);
                var targetArchIdx = targetArch.Unsafe->index;

                if (targetArchIdx != originalArchIdx) {
                    ref var srcArch = ref w->archetypesList.Ptr[originalArchIdx].Ref;
                    ref var dstArch = ref *targetArch.Unsafe;

                    if (originalArchIdx == 0) {
                        var newRow = dstArch.storagePtr.Ref.AllocateRow(entity);
                        var listPos = dstArch.AddRow(newRow);
                        w->entityLocations.Ptr[entity] = new EntityLocation {
                            archetypeIndex = targetArchIdx,
                            row = newRow,
                            listPos = listPos
                        };
                        WriteComponentData(dataBuffer, ref dstArch, newRow, cmds, count);
                        for (var qi = 0; qi < dstArch.queries.length; qi++) {
                            ref var q = ref w->queries.Ptr[dstArch.queries.Ptr[qi]].Ref;
                            q.Add(entity);
                        }
                    } else {
                        var loc = w->entityLocations.Ptr[entity];
                        srcArch.MoveEntityTo(loc.row, ref dstArch);
                        var newRow = w->entityLocations.Ptr[entity].row;
                        WriteComponentData(dataBuffer, ref dstArch, newRow, cmds, count);
                        ArchetypeUnsafe.BatchMigrateQueries(ref srcArch, ref dstArch, entity);
                    }
                }
                // Payloads that were not installed (for example skipped additions) still
                // belong to the command buffer.
                for (var i = 0; i < count; i++) DisposePending(ref cmds[i], dataBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteComponentData(byte* dataBuffer, ref ArchetypeUnsafe dstArch, int newRow, ECBCommand* cmds, int count) {
                for (var i = 0; i < count; i++) {
                    ref var cmd = ref cmds[i];
                    if (cmd.EcbCommandType != ECBCommand.Type.AddComponent) continue;
                    var ctData = ComponentTypeMap.GetComponentType(cmd.ComponentType);
                    if (ctData.storageType == StorageType.Pool) {
                        if (!dstArch.Has(cmd.ComponentType)) continue;
                        world->GetUntypedPool(cmd.ComponentType).WriteBytesUnsafe(
                            dstArch.packedEntities.Ptr[newRow], dataBuffer + cmd.AdditionalData, ctData.size);
                        cmd.isDisposable = 0; // Ownership transferred to the installed component.
                        continue;
                    }
                    if (ctData.category != ComponentCategory.Inline) continue;
                    var localIdx = dstArch.GetComponentLocalIndex(cmd.ComponentType);
                    if (localIdx < 0) continue;
                    var off = dstArch.componentOffsets.Ptr[localIdx];
                    if (off < 0) continue;
                    var dst = dstArch.data.Ptr + off + newRow * ctData.size;
                    Mem.MemCpy(dst, dataBuffer + cmd.AdditionalData, ctData.size);
                    cmd.isDisposable = 0;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static void QuickSort(ECBCommand* arr, int left, int right) {
                while (left < right) {
                    var pivot = arr[(left + right) >> 1].Entity;
                    var i = left - 1;
                    var j = right + 1;
                    while (true) {
                        while (arr[++i].Entity < pivot) { }
                        while (arr[--j].Entity > pivot) { }
                        if (i >= j) break;
                        var tmp = arr[i];
                        arr[i] = arr[j];
                        arr[j] = tmp;
                    }
                    if (j - left < right - j) {
                        QuickSort(arr, left, j);
                        left = j + 1;
                    } else {
                        QuickSort(arr, j + 1, right);
                        right = j;
                    }
                }
            }

            public void Dispose() {
                Clear();
                for (var i = 0; i < perThreadCommands.Length; i++) {
                    perThreadCommands.ElementAt(i).Dispose();
                    perThreadData.ElementAt(i).Dispose();
                }
                perThreadCommands.Dispose();
                perThreadData.Dispose();
                isCreated = 0;
            }
        }

        public void PlaybackBatched(ref World world) {
            var totalCount = Count;
            if (totalCount == 0) return;

            var flat = new HeapList<ECBCommand>(totalCount, AllocatorHandle.Temp);

            int totalDataBytes = 0;
            for (var i = 0; i < ecb->perThreadData.Length; i++)
                totalDataBytes += ecb->perThreadData.ElementAt(i).Length;

            var flatData = new HeapList<byte>(totalDataBytes, AllocatorHandle.Temp);

            for (var i = 0; i < ecb->perThreadCommands.Length; i++) {
                var threadCmds = ecb->perThreadCommands.ElementAt(i);
                if (threadCmds.IsEmpty) continue;

                var threadData = ecb->perThreadData.ElementAt(i);
                var dataBase = flatData.Length;

                if (threadData.Length > 0) {
                    flatData.AddRange(threadData.Ptr, threadData.Length);
                }

                for (int j = 0; j < threadCmds.Length; j++) {
                    var cmd = threadCmds.Ptr[j];
                    if (cmd.WorldToken != ecb->world->entityWorldToken || !ecb->world->EntityIsValid(cmd.Entity, cmd.Generation)
                        || (cmd.EcbCommandType == ECBCommand.Type.Copy
                            && !ecb->world->EntityIsValid(cmd.AdditionalData, cmd.TargetGeneration))) {
                        ECBInternal.DisposePending(ref threadCmds.Ptr[j], threadData.Ptr);
                        continue;
                    }
                    // Live payload ownership moves into the playback array.
                    threadCmds.Ptr[j].isDisposable = 0;
                    if (cmd.EcbCommandType == ECBCommand.Type.AddComponent)
                        cmd.AdditionalData += dataBase;
                    flat.Add(cmd);
                }
            }

            if (flat.Length == 0) {
                ecb->Clear();
                flat.Dispose();
                flatData.Dispose();
                return;
            }

            ECBInternal.QuickSort(flat.Ptr, 0, flat.Length - 1);

            var cmdIdx = 0;
            while (cmdIdx < flat.Length) {
                var entityId = flat.Ptr[cmdIdx].Entity;
                var groupStart = cmdIdx;
                while (cmdIdx < flat.Length && flat.Ptr[cmdIdx].Entity == entityId)
                    cmdIdx++;

                ecb->ProcessEntityBatch(ref world, entityId, flat.Ptr + groupStart, cmdIdx - groupStart, flatData.Ptr);
            }

            ecb->Clear();
            flat.Dispose();
            flatData.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            ecb->Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int entity) where T : unmanaged {
            ecb->Set<T>(entity, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPtr<T>(int entity, T* component) where T : unmanaged {
            ecb->Add(entity, component, JobsUtility.ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity, in T component) where T : unmanaged {
            ecb->Add(entity, component, JobsUtility.ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity) where T : unmanaged {
            ecb->Add<T>(entity, JobsUtility.ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int entity, int component) {
            ecb->Add(entity, JobsUtility.ThreadIndex, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObject(int entity, IComponent component, ComponentTypeData data) {
            ecb->AddObject(entity, component, data);
        }

        internal void AddBytes(int entity, byte* component, int componentType) {
            var type = ComponentTypeMap.GetComponentType(componentType);
            var thread = ThreadIndex;
            var data = ecb->perThreadData.ElementAt(thread);
            var offset = data.Length;
            data.AddRange(component, type.size);
            ecb->perThreadCommands.ElementAt(thread).Add(new ECBCommand {
                Entity = entity,
                Generation = ecb->world->entities.Ptr[entity].Generation,
                WorldToken = ecb->world->entityWorldToken,
                ComponentType = componentType,
                EcbCommandType = ECBCommand.Type.AddComponent,
                AdditionalData = offset,
                isDisposable = type.isDisposable ? (byte)1 : (byte)0
            });
            ecb->totalCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(int entity) where T : unmanaged {
            ecb->Remove<T>(entity, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int entity, int component) {
            ecb->Remove(entity, component, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAndDispose<T>(int entity) where T : unmanaged {
            ecb->RemoveAndDispose<T>(entity, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnableGameObject(int entity, bool value) {
            ecb->EnableGameObject(entity, value, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Destroy(int entity) {
            ecb->Destroy(entity, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int entity) {
            ecb->Copy(entity, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int from, int to) {
            ecb->Copy(from, to, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PlayParticleReference(int entity, bool value) {
            ecb->PlayParticleReference(entity, value, ThreadIndex);
        }

        public void PlaybackMainThread(ref World world) {
            PlaybackBatched(ref world);
        }

        public void Playback(ref World world) {
            PlaybackBatched(ref world);
        }

        internal void Playback(World.WorldUnsafe* world) {
            PlaybackBatched(ref World.Get(world->Id));
        }

        public void Dispose() {
            ecb->Dispose();
            Mem.FreeTracked(ecb, allocator);
        }
    }
}
