using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs {
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;

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
            internal World.WorldUnsafe* world;
            internal DynamicBitmask tempMask;

            public bool IsCreated => isCreated == 1;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear() {
                for (var i = 0; i < perThreadCommands->Length; i++) {
                    perThreadCommands->ElementAt(i)->Clear();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set<T>(int entity, int thread) where T : unmanaged {
                ref var pool = ref world->GetUntypedPool(ComponentType<T>.Index);
                var ptr = pool.UnsafeBuffer->GetPtr(entity);
                UnsafeUtility.MemClear(ptr, UnsafeUtility.SizeOf<T>());
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.SetComponent,
                    ComponentType = ComponentType<T>.Index
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, T* componentPtr, int thread) where T : unmanaged {
                ref var data = ref ComponentType<T>.Data;
                ref var pool = ref world->GetUntypedPool(data.index);
                pool.WriteData(entity, (byte*)componentPtr, UnsafeUtility.SizeOf<T>());
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    isDisposable = data.isDisposable ? (byte)1 : (byte)0,
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, T component, int thread) where T : unmanaged
            {
                ref var data = ref ComponentType<T>.Data;
                ref var pool = ref world->GetUntypedPool(data.index);
                pool.UnsafeBuffer->Add(entity, component);

                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    isDisposable = data.isDisposable ? (byte)1 :(byte)0
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, int thread) where T : unmanaged {
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponentNoData,
                    ComponentType = ComponentType<T>.Index
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(int entity, int thread, int componentType) {
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponentNoData,
                    ComponentType = componentType
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove<T>(int entity, int thread) where T : unmanaged {
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.RemoveComponent,
                    ComponentType = ComponentType<T>.Index
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove(int entity, int component, int thread) {
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.RemoveComponent,
                    ComponentType = component
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveAndDispose<T>(int entity, int thread) where T : unmanaged {
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.RemoveAndDispose,
                    ComponentType = ComponentType<T>.Index
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Destroy(int entity, int thread) {
                var cmd = new ECBCommand { Entity = entity, EcbCommandType = ECBCommand.Type.DestroyEntity };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnableGameObject(int entity, bool value, int thread) {
                byte v = value ? (byte)1 : (byte)0;
                var cmd = new ECBCommand {
                    Entity = entity, EcbCommandType = ECBCommand.Type.SetActiveGameObject, active = v
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CreateEntity() {
                return 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void PlayParticleReference(int entity, bool value, int thread) {
                var v = value ? (byte)1 : (byte)0;
                var cmd = new ECBCommand {
                    Entity = entity, EcbCommandType = ECBCommand.Type.PlayParticleReference, active = v
                };
                perThreadCommands->ElementAt(thread)->Add(cmd);
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ProcessEntityBatch(ref World world, int entity, ECBCommand* cmds, int count) {
                var w = world.UnsafeWorld;
                var originalArchIdx = w->entitiesArchetypes.Ptr[entity];
                ref var originalArch = ref w->archetypesList.Ptr[originalArchIdx].Ref;

                tempMask.CopyFrom(ref originalArch.mask);
                var destroyed = false;

                for (var i = 0; i < count; i++) {
                    ref var cmd = ref cmds[i];
                    switch (cmd.EcbCommandType) {
                        case ECBCommand.Type.AddComponent:
                            if (tempMask.Has(cmd.ComponentType)) {
                                if (cmd.isDisposable != 0)
                                    w->GetUntypedPool(cmd.ComponentType).DisposeComponent(entity);
                                break;
                            }
                            tempMask.Add(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentNoData:
                            if (tempMask.Has(cmd.ComponentType)) break;
                            w->GetUntypedPool(cmd.ComponentType).Set(entity);
                            tempMask.Add(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveComponent:
                            if (!tempMask.Has(cmd.ComponentType)) break;
                            w->GetUntypedPool(cmd.ComponentType).Remove(entity);
                            tempMask.Remove(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveAndDispose:
                            if (!tempMask.Has(cmd.ComponentType)) break;
                            w->GetUntypedPool(cmd.ComponentType).DisposeComponent(entity);
                            tempMask.Remove(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.CreateEntity:
                            world.Entity();
                            break;
                        case ECBCommand.Type.DestroyEntity:
                            originalArch.Destroy(entity);
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

                if (destroyed) return;

                var targetArch = w->GetOrCreateArchetype(ref tempMask);
                w->entitiesArchetypes.Ptr[entity] = targetArch.impl->index;

                if (targetArch.impl->index != originalArchIdx) {
                    ArchetypeUnsafe.BatchMigrateQueries(ref originalArch, ref *targetArch.impl, entity);
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
                    }
                    UnsafePtrList<UnsafeList<ECBCommand>>.Destroy(perThreadCommands);
                    isCreated = 0;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PlaybackBatched(ref World world) {
            var totalCount = Count;
            if (totalCount == 0) return;

            var flat = UnsafeList<ECBCommand>.Create(totalCount, Allocator.Temp);
            for (var i = 0; i < ecb->perThreadCommands->Length; i++) {
                var threadCmds = ecb->perThreadCommands->ElementAt(i);
                if (threadCmds->IsEmpty) continue;
                flat->AddRange(threadCmds->Ptr, threadCmds->m_length);
            }

            if (flat->m_length == 0) {
                flat->Dispose();
                return;
            }

            ECBInternal.QuickSort(flat->Ptr, 0, flat->m_length - 1);

            var cmdIdx = 0;
            while (cmdIdx < flat->m_length) {
                var entityId = flat->Ptr[cmdIdx].Entity;
                var groupStart = cmdIdx;
                while (cmdIdx < flat->m_length && flat->Ptr[cmdIdx].Entity == entityId)
                    cmdIdx++;

                ecb->ProcessEntityBatch(ref world, entityId, flat->Ptr + groupStart, cmdIdx - groupStart);
            }

            ecb->Clear();
            flat->Dispose();
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
        public void Remove<T>(int entity) where T : unmanaged {
            ecb->Remove<T>(entity, JobsUtility.ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int entity, int component) {
            ecb->Remove(entity, component, JobsUtility.ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAndDispose<T>(int entity) where T : unmanaged {
            ecb->RemoveAndDispose<T>(entity, JobsUtility.ThreadIndex);
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

        public void PlaybackMainThread(ref World world)
        {
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
