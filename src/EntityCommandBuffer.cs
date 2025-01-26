using UnityEngine;

namespace Wargon.Nukecs {
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Mathematics;
    using Wargon.Nukecs.Transforms;
    using Wargon.Nukecs.Tests;
    public unsafe struct EntityCommandBuffer : IDisposable {
        [NativeDisableUnsafePtrRestriction] private readonly ECBInternal* ecb;
        public int Capacity => ecb->Capacity;
        public int Count => ecb->count;
        public bool IsCreated => ecb != null && ecb->isCreated == 1;
        internal int ThreadIndex => JobsUtility.ThreadIndex;

        public EntityCommandBuffer(int startSize) {
            ecb = (ECBInternal*) UnsafeUtility.Malloc(sizeof(ECBInternal), UnsafeUtility.AlignOf<ECBInternal>(),
                Allocator.Persistent);
            *ecb = new ECBInternal();
            //ecb->internalBuffer = UnsafeList<ECBCommand>.Create(startSize, Allocator.Persistent);
            ecb->perThreadBuffer = Chains(startSize, Allocator.Persistent);
            ecb->isCreated = 1;
        }
        internal EntityCommandBuffer(int startSize, World.WorldUnsafe* world) {
            ecb = world->_allocate<ECBInternal>();
            *ecb = new ECBInternal();
            //ecb->internalBuffer = UnsafeList<ECBCommand>.Create(startSize, Allocator.Persistent);
            ecb->perThreadBuffer = Chains(startSize, world->Allocator);
            ecb->isCreated = 1;
        }
        private UnsafePtrList<UnsafeList<ECBCommand>>* Chains(int startSize, Allocator allocator) {
            var threads = JobsUtility.ThreadIndexCount + 2;
            UnsafePtrList<UnsafeList<ECBCommand>>* ptrList =
                UnsafePtrList<UnsafeList<ECBCommand>>.Create(threads, allocator);
            for (int i = 0; i < threads; i++) {
                var list = UnsafeList<ECBCommand>.Create(startSize, allocator);
                ptrList->Add(list);
            }
            return ptrList;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ECBCommand {
            public byte* Component;
            public int Entity;
            public Type EcbCommandType;
            public int ComponentType;
            public int AdditionalData;
            public float3 Position;
            public byte active;
            public bool IsDisposable;

            public enum Type : short {
                AddComponent = 0,
                AddComponentPtr = 1,
                AddComponentNoData = 2,
                RemoveComponent = 3,
                SetComponent = 4,
                CreateEntity = 5,
                DestroyEntity = 6,
                ChangeTransformRefPosition = 7,
                SetActiveGameObject = 8,
                PlayParticleReference = 9,
                Copy = 10,
                CreateCopy = 11,
                RemoveAndDispose = 12
            }
        }

        internal struct ECBInternal {
            internal byte isCreated;

            //[NativeDisableUnsafePtrRestriction]
            //internal UnsafeList<ECBCommand>* internalBuffer;
            [NativeDisableUnsafePtrRestriction] internal UnsafePtrList<UnsafeList<ECBCommand>>* perThreadBuffer;


            internal UnsafeList<ECBCommand>* internalBuffer {
                get => perThreadBuffer->ElementAt(1);
            }

            public int Capacity => internalBuffer->Capacity;
            public bool IsCreated => isCreated == 1;
            internal int count;
            public void ResizeAndClear(int newSize) {
                internalBuffer->Resize(newSize);
                internalBuffer->Clear();
            }

            public void Clear() {
                internalBuffer->Clear();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set<T>(int entity, int thread) where T : unmanaged {
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.SetComponent,
                    ComponentType = ComponentType<T>.Index
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }

            public void Add<T>(int entity, T* componentPtr, int thread) where T : unmanaged
            {
                ref var data = ref ComponentType<T>.Data;
                var cmd = new ECBCommand {
                    Component = (byte*)componentPtr,
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponentPtr,
                    ComponentType = data.index,
                    AdditionalData = sizeof(T),
                    IsDisposable = data.isDisposable
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, T component, int thread) where T : unmanaged {
                //if(IsCreated== false) return;
                var size = UnsafeUtility.SizeOf<T>();
                var ptr = (T*) UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<T>(), Allocator.Temp);
                *ptr = component;
                ref var data = ref ComponentType<T>.Data;
                var cmd = new ECBCommand {
                    Component = (byte*)ptr,
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = data.index,
                    AdditionalData = size,
                    IsDisposable = data.isDisposable
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(int entity, int thread) where T : unmanaged {
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponentNoData,
                    ComponentType = ComponentType<T>.Index
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(int entity, int thread, int componentType) {
                var cmd = new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponentNoData,
                    ComponentType = componentType
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove<T>(int entity, int thread) where T : unmanaged {
                var cmd = new ECBCommand {
                    Entity = entity, 
                    EcbCommandType = ECBCommand.Type.RemoveComponent, 
                    ComponentType = ComponentType<T>.Index
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove(int entity, int component, int thread)
            {
                var cmd = new ECBCommand {
                    Entity = entity, 
                    EcbCommandType = ECBCommand.Type.RemoveComponent, 
                    ComponentType = component
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveAndDispose<T>(int entity, int thread) where T : unmanaged {
                var cmd = new ECBCommand {
                    Entity = entity, 
                    EcbCommandType = ECBCommand.Type.RemoveAndDispose, 
                    ComponentType = ComponentType<T>.Index
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Destroy(int entity, int thread) {
                var cmd = new ECBCommand { Entity = entity, EcbCommandType = ECBCommand.Type.DestroyEntity};
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetViewPosition(int entity, float3 pos, int thread) {
                var cmd = new ECBCommand
                    {Entity = entity, EcbCommandType = ECBCommand.Type.ChangeTransformRefPosition, Position = pos};
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnableGameObject(int entity, bool value, int thread) {
                byte v = value ? (byte) 1 : (byte) 0;
                var cmd = new ECBCommand
                    {Entity = entity, EcbCommandType = ECBCommand.Type.SetActiveGameObject, active = v};
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnableEntity(int entity, bool value, int thread) {
                byte v = value ? (byte) 1 : (byte) 0;
                var cmd = new ECBCommand
                    {Entity = entity, EcbCommandType = ECBCommand.Type.SetActiveGameObject, active = v};
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int CreateEntity() {
                return 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void PlayParticleReference(int entity, bool value, int thread) {
                var v = value ? (byte) 1 : (byte) 0;
                var cmd = new ECBCommand
                    {Entity = entity, EcbCommandType = ECBCommand.Type.PlayParticleReference, active = v};
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }

            public void Copy(int entity, int thread) {
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(new ECBCommand {
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.CreateCopy
                });
                count++;
            }

            public void Copy(int from, int to, int thread) {
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(new ECBCommand {
                    Entity = from,
                    EcbCommandType = ECBCommand.Type.Copy,
                    AdditionalData = to
                });
                count++;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                for (var i = 0; i < perThreadBuffer->Length; i++) {
                    UnsafeList<ECBCommand>.Destroy(perThreadBuffer->ElementAt(i));
                }

                UnsafePtrList<UnsafeList<ECBCommand>>.Destroy(perThreadBuffer);
                //UnsafeList<ECBCommand>.Destroy(internalBuffer);
                isCreated = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResizeAndClear(int newSize) {
            ecb->ResizeAndClear(newSize);
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

        public void Add(int entity, int component) {
            ecb->Add(entity, JobsUtility.ThreadIndex, component);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(int entity) where T : unmanaged {
            ecb->Remove<T>(entity, JobsUtility.ThreadIndex);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int entity, int component)
        {
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

        public void Playback(ref World world) {
            //ecb->Playback(ref world);

            for (var i = 0; i < ecb->perThreadBuffer->Length; i++) {
                var buffer = ecb->perThreadBuffer->ElementAt(i);
                if (buffer->IsEmpty) continue;

                for (var cmdIndex = 0; cmdIndex < buffer->m_length; cmdIndex++) {
                    ref var cmd = ref buffer->ElementAt(cmdIndex);

                    ref var archetype = ref *world.UnsafeWorld->entitiesArchetypes.ElementAt(cmd.Entity).impl;
                    switch (cmd.EcbCommandType) {
                        case ECBCommand.Type.AddComponent:
                            if (archetype.Has(cmd.ComponentType))
                            {
                                if (cmd.IsDisposable)
                                {
                                    world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                                }
                                UnsafeUtility.Free(cmd.Component, Allocator.Temp);
                                break;
                            }
                            ref var pool = ref world.UnsafeWorld->GetUntypedPool(cmd.ComponentType);
                            pool.SetPtr(cmd.Entity, cmd.Component);
                            UnsafeUtility.Free(cmd.Component, Allocator.Temp);
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentPtr:
                            if (archetype.Has(cmd.ComponentType))
                            {
                                if (cmd.IsDisposable)
                                {
                                    world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                                }
                                break;
                            }
                            pool = ref world.UnsafeWorld->GetUntypedPool(cmd.ComponentType);
                            pool.SetPtr(cmd.Entity, cmd.Component);
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentNoData:
                            if(archetype.Has(cmd.ComponentType)) break;
                            world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).Set(cmd.Entity);
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveComponent:
                            if(archetype.Has(cmd.ComponentType) == false) break;
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
                            if(archetype.Has(cmd.ComponentType) == false) break;
                            archetype.OnEntityChangeECB(cmd.Entity, -cmd.ComponentType);
                            world.UnsafeWorld->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                            break;
                    }
                    ecb->count--;
                }
                buffer->Clear();
            }
        }
        internal void Playback(World.WorldUnsafe* world) {
            //ecb->Playback(ref world);

            for (var i = 0; i < ecb->perThreadBuffer->Length; i++) {
                var buffer = ecb->perThreadBuffer->ElementAt(i);
                if (buffer->IsEmpty) continue;

                for (var cmdIndex = 0; cmdIndex < buffer->m_length; cmdIndex++) {
                    ref var cmd = ref buffer->ElementAt(cmdIndex);

                    ref var archetype = ref *world->entitiesArchetypes.ElementAt(cmd.Entity).impl;
                    switch (cmd.EcbCommandType) {
                        case ECBCommand.Type.AddComponent:
                            if (archetype.Has(cmd.ComponentType))
                            {
                                if (cmd.IsDisposable)
                                {
                                    world->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                                }
                                UnsafeUtility.Free(cmd.Component, Allocator.Temp);
                                break;
                            }
                            ref var pool = ref world->GetUntypedPool(cmd.ComponentType);
                            pool.SetPtr(cmd.Entity, cmd.Component);
                            UnsafeUtility.Free(cmd.Component, Allocator.Temp);
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentPtr:
                            if (archetype.Has(cmd.ComponentType))
                            {
                                if (cmd.IsDisposable)
                                {
                                    world->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                                }
                                break;
                            }
                            pool = ref world->GetUntypedPool(cmd.ComponentType);
                            pool.SetPtr(cmd.Entity, cmd.Component);
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentNoData:
                            if(archetype.Has(cmd.ComponentType)) break;
                            world->GetUntypedPool(cmd.ComponentType).Set(cmd.Entity);
                            archetype.OnEntityChangeECB(cmd.Entity, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveComponent:
                            if(archetype.Has(cmd.ComponentType) == false) break;
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
                            if(archetype.Has(cmd.ComponentType) == false) break;
                            archetype.OnEntityChangeECB(cmd.Entity, -cmd.ComponentType);
                            world->GetUntypedPool(cmd.ComponentType).DisposeComponent(cmd.Entity);
                            break;
                    }
                    ecb->count--;
                }
                buffer->Clear();
            }
        }
        public void Dispose() {
            ecb->Dispose();
            UnsafeUtility.Free(ecb, Allocator.Persistent);
        }
    }
}