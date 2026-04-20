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
            public void Dispose() {
                for (var i = 0; i < perThreadCommands->Length; i++) {
                    UnsafeList<ECBCommand>.Destroy(perThreadCommands->ElementAt(i));
                }
                UnsafePtrList<UnsafeList<ECBCommand>>.Destroy(perThreadCommands);
                isCreated = 0;
            }
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

        public void PlaybackMainThread(ref World world) {
            var commands = ecb->perThreadCommands->ElementAt(0);
            if (commands->IsEmpty) return;

            for (var cmdIndex = 0; cmdIndex < commands->m_length; cmdIndex++) {
                ref var cmd = ref commands->ElementAt(cmdIndex);

                ref var archetype = ref world.UnsafeWorld->GetEntityArchetypePtr(cmd.Entity).Ref;
#if NUKECS_DEBUG
                world.UnsafeWorld->AddComponentChange(new World.ComponentChange {
                    command = cmd.EcbCommandType,
                    entityId = cmd.Entity,
                    componentTypeIndex = cmd.ComponentType,
                    timeStamp = world.UnsafeWorld->timeData.ElapsedTime
                });
#endif
                switch (cmd.EcbCommandType) {
                    case ECBCommand.Type.AddComponent:
                        if (archetype.Has(cmd.ComponentType)) {
                            var typeData = ComponentTypeMap.GetComponentType(cmd.ComponentType);
                            if (typeData.isDisposable)
                                world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                            break;
                        }
                        archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                        break;
                    case ECBCommand.Type.AddComponentNoData:
                        if (archetype.Has(cmd.ComponentType)) break;
                        world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).Set(cmd.Entity);
                        archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                        break;
                    case ECBCommand.Type.RemoveComponent:
                        if (archetype.Has(cmd.ComponentType) == false) break;
                        world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).Remove(cmd.Entity);
                        archetype.OnEntityChangeECB(cmd.Entity, -cmd.ComponentType);
                        break;
                    case ECBCommand.Type.CreateEntity:
                        world.Entity();
                        break;
                    case ECBCommand.Type.DestroyEntity:
                        archetype.Destroy(cmd.Entity);
                        break;
                    case ECBCommand.Type.Copy:
                        archetype.Copy(cmd.Entity, cmd.AdditionalData);
                        break;
                    case ECBCommand.Type.RemoveAndDispose:
                        if (archetype.Has(cmd.ComponentType) == false) break;
                        archetype.OnEntityChangeECB(cmd.Entity, -cmd.ComponentType);
                        world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                        break;
                }
            }
            commands->Clear();
        }

        public void Playback(ref World world) {
            for (var i = 0; i < ecb->perThreadCommands->Length; i++) {
                var commands = ecb->perThreadCommands->ElementAt(i);
                if (commands->IsEmpty) continue;

                for (var cmdIndex = 0; cmdIndex < commands->m_length; cmdIndex++) {
                    ref var cmd = ref commands->ElementAt(cmdIndex);
#if NUKECS_DEBUG
                    world.UnsafeWorld->AddComponentChange(new World.ComponentChange {
                        command = cmd.EcbCommandType,
                        entityId = cmd.Entity,
                        componentTypeIndex = cmd.ComponentType,
                        timeStamp = world.UnsafeWorld->timeData.ElapsedTime
                    });
#endif
                    ref var archetype = ref world.UnsafeWorld->GetEntityArchetypePtr(cmd.Entity).Ref;
                    switch (cmd.EcbCommandType) {
                        case ECBCommand.Type.AddComponent:
                            if (archetype.Has(cmd.ComponentType)) {
                                var typeData = ComponentTypeMap.GetComponentType(cmd.ComponentType);
                                if (typeData.isDisposable)
                                    world.UnsafeWorld->GetUntypedPool(cmd.ComponentType)
                                        .DisposeComponent(cmd.Entity);
                                break;
                            }
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentNoData:
                            if (archetype.Has(cmd.ComponentType)) break;
                            world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).Set(cmd.Entity);
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveComponent:
                            if (archetype.Has(cmd.ComponentType) == false) break;
                            world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).Remove(cmd.Entity);
                            archetype.OnEntityChangeECB(cmd.Entity, -cmd.ComponentType);
                            break;
                        case ECBCommand.Type.CreateEntity:
                            world.Entity();
                            break;
                        case ECBCommand.Type.DestroyEntity:
                            archetype.Destroy(cmd.Entity);
                            break;
                        case ECBCommand.Type.Copy:
                            archetype.Copy(cmd.Entity, cmd.AdditionalData);
                            break;
                        case ECBCommand.Type.RemoveAndDispose:
                            if (!archetype.Has(cmd.ComponentType)) break;
                            archetype.OnEntityChangeECB(cmd.Entity, -cmd.ComponentType);
                            world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                            break;
                    }
                }
                commands->Clear();
            }
        }

        internal void Playback(World.WorldUnsafe* world) {
            for (var i = 0; i < ecb->perThreadCommands->Length; i++) {
                var commands = ecb->perThreadCommands->ElementAt(i);
                if (commands->IsEmpty) continue;

                for (var cmdIndex = 0; cmdIndex < commands->m_length; cmdIndex++) {
                    ref var cmd = ref commands->ElementAt(cmdIndex);

                    ref var archetype = ref world->GetEntityArchetypePtr(cmd.Entity).Ref;
                    switch (cmd.EcbCommandType) {
                        case ECBCommand.Type.AddComponent:
                            if (archetype.Has(cmd.ComponentType)) {
                                var typeData = ComponentTypeMap.GetComponentType(cmd.ComponentType);
                                if (typeData.isDisposable)
                                    world->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                                break;
                            }
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentNoData:
                            if (archetype.Has(cmd.ComponentType)) break;
                            world->GetUntypedPool(cmd.ComponentType).Set(cmd.Entity);
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveComponent:
                            if (!archetype.Has(cmd.ComponentType)) break;
                            world->GetUntypedPool(cmd.ComponentType).Remove(cmd.Entity);
                            archetype.OnEntityChangeECB(cmd.Entity, -cmd.ComponentType);
                            break;
                        case ECBCommand.Type.CreateEntity:
                            world->CreateEntity();
                            break;
                        case ECBCommand.Type.DestroyEntity:
                            archetype.Destroy(cmd.Entity);
                            break;
                        case ECBCommand.Type.Copy:
                            archetype.Copy(cmd.Entity, cmd.AdditionalData);
                            break;
                        case ECBCommand.Type.RemoveAndDispose:
                            if (!archetype.Has(cmd.ComponentType)) break;
                            archetype.OnEntityChangeECB(cmd.Entity, -cmd.ComponentType);
                            world->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                            break;
                    }
                }
                commands->Clear();
            }
        }

        public void Dispose() {
            ecb->Dispose();
            UnsafeUtility.Free(ecb, allocator);
            dbug.log("ECB DISPOSED");
        }
    }
}
