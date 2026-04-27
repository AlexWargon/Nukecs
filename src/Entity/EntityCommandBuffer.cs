using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs {
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
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
                int total = 0;
                for (int i = 0; i < ecb->perThreadCommands->Length; i++)
                    total += ecb->perThreadCommands->ElementAt(i)->m_length;
                return total;
            }
        }

        public bool IsCreated => ecb != null && ecb->isCreated == 1;
        internal static int ThreadIndex => JobsUtility.ThreadIndex;
        internal readonly Allocator allocator;

        public EntityCommandBuffer(int startSize, Allocator allocator, World.WorldUnsafe* world) {
            this.allocator = allocator;
            ecb = (ECBInternal*)UnsafeUtility.MallocTracked(sizeof(ECBInternal),
                UnsafeUtility.AlignOf<ECBInternal>(), allocator, 0);
            *ecb = new ECBInternal();
            ecb->perThreadCommands = CreateCommandBuffers(startSize, this.allocator);
            ecb->perThreadData = CreateDataBuffers(startSize * 64, this.allocator);
            ecb->world = world;
            ecb->tempMask = new DynamicBitmask(
                Unity.Mathematics.math.max(ComponentAmount.Value.Data, 256), world);
            ecb->isCreated = 1;
        }

        private UnsafePtrList<UnsafeList<ECBCommand>>* CreateCommandBuffers(int startSize, Allocator alloc) {
            var threads = JobsUtility.ThreadIndexCount + 2;
            var ptrList = UnsafePtrList<UnsafeList<ECBCommand>>.Create(threads, alloc);
            for (var i = 0; i < threads; i++) {
                var list = UnsafeList<ECBCommand>.Create(startSize, alloc);
                ptrList->Add(list);
            }
            return ptrList;
        }

        private UnsafePtrList<UnsafeList<byte>>* CreateDataBuffers(int startBytes, Allocator alloc) {
            var threads = JobsUtility.ThreadIndexCount + 2;
            var ptrList = UnsafePtrList<UnsafeList<byte>>.Create(threads, alloc);
            for (var i = 0; i < threads; i++) {
                var buf = UnsafeList<byte>.Create(startBytes, alloc);
                ptrList->Add(buf);
            }
            return ptrList;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ECBCommand {
            public int Entity;
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
                RemoveAndDispose = 10
            }
        }

        internal struct ECBInternal {
            internal byte isCreated;
            [NativeDisableUnsafePtrRestriction]
            internal UnsafePtrList<UnsafeList<ECBCommand>>* perThreadCommands;
            [NativeDisableUnsafePtrRestriction]
            internal UnsafePtrList<UnsafeList<byte>>* perThreadData;
            [NativeDisableUnsafePtrRestriction]
            internal World.WorldUnsafe* world;
            internal DynamicBitmask tempMask;

            public bool IsCreated => isCreated == 1;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear() {
                for (var i = 0; i < perThreadCommands->Length; i++) {
                    perThreadCommands->ElementAt(i)->Clear();
                    perThreadData->ElementAt(i)->Clear();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set<T>(int entity, int thread) where T : unmanaged {
                var ctData = ComponentType<T>.Data;
                ref var loc = ref world->entityLocations.Ptr[entity];
                ref var arch = ref world->archetypesList.Ptr[loc.archetypeIndex].Ref;
                var ptr = arch.GetComponentDataPtr(ctData.index, loc.row);
                if (ptr != null)
                    UnsafeUtility.MemClear(ptr, ctData.size);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, T* componentPtr, int thread) where T : unmanaged {
                ref var data = ref ComponentType<T>.Data;
                var buf = perThreadData->ElementAt(thread);
                var dataOffset = buf->m_length;
                buf->AddRange((byte*)componentPtr, data.size);
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    AdditionalData = dataOffset,
                    isDisposable = data.isDisposable ? (byte)1 : (byte)0,
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, T component, int thread) where T : unmanaged {
                ref var data = ref ComponentType<T>.Data;
                var buf = perThreadData->ElementAt(thread);
                var dataOffset = buf->m_length;
                buf->AddRange((byte*)&component, data.size);
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    AdditionalData = dataOffset,
                    isDisposable = data.isDisposable ? (byte)1 : (byte)0,
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddObject(int entity, IComponent component, ComponentTypeData data) {
                var thread = JobsUtility.ThreadIndex;
                var buf = perThreadData->ElementAt(thread);
                var dataOffset = buf->m_length;
                var size = data.size;
                var newLen = buf->m_length + size;
                if (newLen > buf->Capacity)
                    buf->SetCapacity(Math.Max(buf->Capacity * 2, newLen));
                ComponentHelpers.Write(buf->Ptr + dataOffset, 0, size, data.index, component);
                buf->m_length = newLen;
                
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    AdditionalData = dataOffset,
                    isDisposable = data.isDisposable ? (byte)1 : (byte)0,
                });
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, int thread) where T : unmanaged {
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponentNoData,
                    ComponentType = ComponentType<T>.Index
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(int entity, int thread, int componentType) {
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponentNoData,
                    ComponentType = componentType
                });
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove<T>(int entity, int thread) where T : unmanaged {
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.RemoveComponent,
                    ComponentType = ComponentType<T>.Index
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove(int entity, int component, int thread) {
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.RemoveComponent,
                    ComponentType = component
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveAndDispose<T>(int entity, int thread) where T : unmanaged {
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.RemoveAndDispose,
                    ComponentType = ComponentType<T>.Index
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Destroy(int entity, int thread) {
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand { Entity = entity, EcbCommandType = ECBCommand.Type.DestroyEntity });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnableGameObject(int entity, bool value, int thread) {
                byte v = value ? (byte)1 : (byte)0;
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity, EcbCommandType = ECBCommand.Type.SetActiveGameObject, active = v
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CreateEntity() {
                return 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void PlayParticleReference(int entity, bool value, int thread) {
                var v = value ? (byte)1 : (byte)0;
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity, EcbCommandType = ECBCommand.Type.PlayParticleReference, active = v
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Copy(int entity, int thread) {
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.CreateCopy
                });
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Copy(int from, int to, int thread) {
                perThreadCommands->ElementAt(thread)->Add(new ECBCommand {
                    Entity = from,
                    EcbCommandType = ECBCommand.Type.Copy,
                    AdditionalData = to
                });
            }

            internal void ProcessEntityBatch(ref World world, int entity, ECBCommand* cmds, int count, byte* dataBuffer) {
                var w = world.UnsafeWorld;
                var originalArchIdx = w->entitiesArchetypes.Ptr[entity];
                ref var originalArch = ref w->archetypesList.Ptr[originalArchIdx].Ref;

                tempMask.CopyFrom(ref originalArch.mask);
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
                            w->archetypesList.Ptr[originalArchIdx].Ref.Copy(entity, cmd.AdditionalData);
                            break;
                        case ECBCommand.Type.CreateCopy:
                            break;
                    }
                    if (destroyed) break;
                }

                if (destroyed) {
                    ref var arch = ref w->archetypesList.Ptr[w->entitiesArchetypes.Ptr[entity]].Ref;
                    var loc = w->entityLocations.Ptr[entity];
                    arch.RemoveEntity(loc.row);
                    arch.destroyEdge.Execute(entity);
                    w->OnDestroyEntity(entity);
                    return;
                }

                var targetArch = w->GetOrCreateArchetype(ref tempMask);
                var targetArchIdx = targetArch.impl->index;

                if (targetArchIdx != originalArchIdx) {
                    ref var srcArch = ref w->archetypesList.Ptr[originalArchIdx].Ref;
                    ref var dstArch = ref *targetArch.impl;

                    if (originalArchIdx == 0) {
                        var newRow = dstArch.AllocateEntity(entity);
                        w->entityLocations.Ptr[entity] = new EntityLocation {
                            archetypeIndex = targetArchIdx,
                            row = newRow
                        };
                        w->entitiesArchetypes.Ptr[entity] = targetArchIdx;
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
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteComponentData(byte* dataBuffer, ref ArchetypeUnsafe dstArch, int newRow, ECBCommand* cmds, int count) {
                for (var i = 0; i < count; i++) {
                    ref var cmd = ref cmds[i];
                    if (cmd.EcbCommandType != ECBCommand.Type.AddComponent) continue;
                    var ctData = ComponentTypeMap.GetComponentType(cmd.ComponentType);
                    if (ctData.storageType != StorageType.Archetype) continue;
                    var localIdx = dstArch.GetComponentLocalIndex(cmd.ComponentType);
                    if (localIdx < 0) continue;
                    var off = dstArch.componentOffsets.Ptr[localIdx];
                    if (off < 0) continue;
                    var dst = dstArch.data.Ptr + off + newRow * ctData.size;
                    UnsafeUtility.MemCpy(dst, dataBuffer + cmd.AdditionalData, ctData.size);
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
                for (var i = 0; i < perThreadCommands->Length; i++) {
                    UnsafeList<ECBCommand>.Destroy(perThreadCommands->ElementAt(i));
                    UnsafeList<byte>.Destroy(perThreadData->ElementAt(i));
                }
                UnsafePtrList<UnsafeList<ECBCommand>>.Destroy(perThreadCommands);
                UnsafePtrList<UnsafeList<byte>>.Destroy(perThreadData);
                isCreated = 0;
            }
        }

        public void PlaybackBatched(ref World world) {
            var totalCount = Count;
            if (totalCount == 0) return;

            var flat = UnsafeList<ECBCommand>.Create(totalCount, Allocator.Temp);

            int totalDataBytes = 0;
            for (var i = 0; i < ecb->perThreadData->Length; i++)
                totalDataBytes += ecb->perThreadData->ElementAt(i)->m_length;

            var flatData = UnsafeList<byte>.Create(totalDataBytes, Allocator.Temp);

            for (var i = 0; i < ecb->perThreadCommands->Length; i++) {
                var threadCmds = ecb->perThreadCommands->ElementAt(i);
                if (threadCmds->IsEmpty) continue;

                var threadData = ecb->perThreadData->ElementAt(i);
                var dataBase = flatData->m_length;

                if (threadData->m_length > 0) {
                    UnsafeUtility.MemCpy(flatData->Ptr + dataBase, threadData->Ptr, threadData->m_length);
                    flatData->m_length += threadData->m_length;
                }

                for (int j = 0; j < threadCmds->m_length; j++) {
                    var cmd = threadCmds->Ptr[j];
                    if (cmd.EcbCommandType == ECBCommand.Type.AddComponent)
                        cmd.AdditionalData += dataBase;
                    flat->Add(cmd);
                }
            }

            if (flat->m_length == 0) {
                flat->Dispose();
                flatData->Dispose();
                return;
            }

            ECBInternal.QuickSort(flat->Ptr, 0, flat->m_length - 1);

            var cmdIdx = 0;
            while (cmdIdx < flat->m_length) {
                var entityId = flat->Ptr[cmdIdx].Entity;
                var groupStart = cmdIdx;
                while (cmdIdx < flat->m_length && flat->Ptr[cmdIdx].Entity == entityId)
                    cmdIdx++;

                ecb->ProcessEntityBatch(ref world, entityId, flat->Ptr + groupStart, cmdIdx - groupStart, flatData->Ptr);
            }

            ecb->Clear();
            flat->Dispose();
            flatData->Dispose();
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
            UnsafeUtility.Free(ecb, allocator);
            dbug.log("ECB DISPOSED");
        }
    }
}
