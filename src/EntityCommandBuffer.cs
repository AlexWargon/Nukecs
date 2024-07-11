using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;


namespace Wargon.Nukecs {
    public unsafe struct EntityCommandBuffer : IDisposable {
        [NativeDisableUnsafePtrRestriction] private readonly ECBInternal* ecb;
        public int Capacity => ecb->Capacity;
        public int Count => ecb->count;
        public bool IsCreated => ecb != null && ecb->isCreated == 1;
        [NativeSetThreadIndex] internal int ThreadIndex;

        public EntityCommandBuffer(int startSize) {
            ecb = (ECBInternal*) UnsafeUtility.Malloc(sizeof(ECBInternal), UnsafeUtility.AlignOf<ECBInternal>(),
                Allocator.Persistent);
            *ecb = new ECBInternal();
            ThreadIndex = 0;
            //ecb->internalBuffer = UnsafeList<ECBCommand>.Create(startSize, Allocator.Persistent);
            ecb->perThreadBuffer = Chains(startSize);
            ecb->isCreated = 1;
        }

        private UnsafePtrList<UnsafeList<ECBCommand>>* Chains(int startSize) {
            var threads = JobsUtility.JobWorkerCount + 1;
            UnsafePtrList<UnsafeList<ECBCommand>>* ptrList =
                UnsafePtrList<UnsafeList<ECBCommand>>.Create(threads, Allocator.Persistent);
            for (int i = 0; i < threads; i++) {
                var list = UnsafeList<ECBCommand>.Create(startSize, Allocator.Persistent);
                ptrList->Add(list);
            }

            return ptrList;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ECBCommand {
            public void* Component;
            public int Entity;
            public Type EcbCommandType;
            public int ComponentType;
            public int ComponentSize;
            public float3 Position;
            public byte active;

            public enum Type : short {
                AddComponent = 0,
                AddComponentNoData = 1,
                RemoveComponent = 2,
                SetComponent = 3,
                CreateEntity = 4,
                DestroyEntity = 5,
                ChangeTransformRefPosition = 6,
                SetActiveGameObject = 7,
                PlayParticleReference = 8,
                ReuseView = 9,
                SetActiveEntity = 10,
            }
        }

        public sealed partial class ECBCommandType {
            public const byte AddComponent = 0;
            public const byte RemoveComponent = 1;
            public const byte SetComponent = 2;
            public const byte CreateEntity = 3;
            public const byte DestroyEntity = 4;
            public const byte ChangeGOPosition = 5;
            public const byte SetActive = 6;
            public const byte PlayParticle = 7;
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
                    ComponentType = ComponentMeta<T>.Index
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
                var cmd = new ECBCommand {
                    Component = ptr,
                    Entity = entity,
                    EcbCommandType = ECBCommand.Type.AddComponent,
                    ComponentType = ComponentMeta<T>.Index,
                    ComponentSize = size
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
                    ComponentType = ComponentMeta<T>.Index,
                };
                var buffer = perThreadBuffer->ElementAt(thread);
                buffer->Add(cmd);
                count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Destroy(int entity, int thread) {
                // var cmd = new ECBCommand { Entity = entity, EcbCommandType = ECBCommand.Type.DestroyEntity};
                // buffer.AddNoResize(cmd);
                Add<DestroyEntity>(entity, thread);
                count++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Remove<T>(int entity, int thread) where T : unmanaged {
                var cmd = new ECBCommand
                    {Entity = entity, EcbCommandType = ECBCommand.Type.RemoveComponent, ComponentType = ComponentMeta<T>.Index};
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

#if !UNITY_EDITOR
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            [BurstCompile]
            public void Playback(ref World world) {
                //if(internalBuffer->IsEmpty) return;
                for (var i = 0; i < perThreadBuffer->Length; i++) {
                    var buffer = perThreadBuffer->ElementAt(i);
                    if (buffer->IsEmpty) continue;

                    for (var cmdIndex = 0; cmdIndex < buffer->Length; cmdIndex++) {
                        ref var cmd = ref buffer->ElementAt(cmdIndex);
                        switch (cmd.EcbCommandType) {
                            case ECBCommand.Type.AddComponent:
                                ref var e = ref world.GetEntity(cmd.Entity);
                                // ref var pool = ref world._impl->GetUntypedPool(cmd.ComponentType);
                                // pool.SetPtr(e.id, cmd.Component);
                                // UnsafeUtility.Free(cmd.Component, Allocator.Temp);
                                e.archetype->OnEntityChange(ref e, cmd.ComponentType);
                                break;
                            case ECBCommand.Type.AddComponentNoData:
                                e = ref world.GetEntity(cmd.Entity);
                                e.archetype->OnEntityChange(ref e, cmd.ComponentType);
                                //world.GetEntity(cmd.Entity).AddByTypeID(cmd.ComponentType);
                                break;
                            case ECBCommand.Type.RemoveComponent:
                                e = ref world.GetEntity(cmd.Entity);
                                e.archetype->OnEntityChange(ref e, -cmd.ComponentType);
                                //Debug.Log("REMOVED IN ECB");
                                break;
                            case ECBCommand.Type.SetComponent:

                                break;
                            case ECBCommand.Type.CreateEntity:
                                world.CreateEntity();
                                break;
                            case ECBCommand.Type.DestroyEntity:
                                //world.GetEntity(cmd.Entity).Destroy();
                                break;
                            case ECBCommand.Type.ChangeTransformRefPosition:
                                //world.GetEntity(cmd.Entity).Get<TransformRef>().value.position = new Vector3(cmd.Position.x, cmd.Position.y, cmd.Position.z);
                                break;
                            case ECBCommand.Type.SetActiveGameObject:
                                //world.GetEntity(cmd.Entity).Get<Pooled>().SetActive(cmd.active == 1);
                                break;
                            case ECBCommand.Type.SetActiveEntity:
                                //ref var e = ref world.GetEntity(cmd.Entity);
                                //EntityPool.Back(e, e.Get<PooledEntity>());
                                break;
                            case ECBCommand.Type.PlayParticleReference:
                                //if (cmd.active == 1) {
                                //    world.GetEntity(cmd.Entity).Get<Particle>().value.Play();
                                //}
                                //else {
                                //    world.GetEntity(cmd.Entity).Get<Particle>().value.Stop();
                                //}
                                break;
                            case ECBCommand.Type.ReuseView:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                    buffer->Clear();
                }
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
        public void Add<T>(int entity, T component) where T : unmanaged {
            ecb->Add(entity, component, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity) where T : unmanaged {
            ecb->Add<T>(entity, ThreadIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(int entity) where T : unmanaged {
            ecb->Remove<T>(entity, ThreadIndex);
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
        public void PlayParticleReference(int entity, bool value) {
            ecb->PlayParticleReference(entity, value, ThreadIndex);
        }
        
        public void PlaybackParallel(ref World world, int threadIndex) {
            var buffer = ecb->perThreadBuffer->ElementAt(threadIndex);
                if (buffer->IsEmpty) return;

                for (var cmdIndex = 0; cmdIndex < buffer->Length; cmdIndex++) {
                    ref var cmd = ref buffer->ElementAt(cmdIndex);
                    switch (cmd.EcbCommandType) {
                        case ECBCommand.Type.AddComponent:
                            ref var e = ref world.GetEntity(cmd.Entity);
                            // ref var pool = ref world._impl->GetUntypedPool(cmd.ComponentType);
                            // pool.SetPtr(e.id, cmd.Component);
                            // UnsafeUtility.Free(cmd.Component, Allocator.Temp);
                            e.archetype->OnEntityChange(ref e, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentNoData:
                            e = ref world.GetEntity(cmd.Entity);
                            e.archetype->OnEntityChange(ref e, cmd.ComponentType);
                            //world.GetEntity(cmd.Entity).AddByTypeID(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveComponent:
                            e = ref world.GetEntity(cmd.Entity);
                            e.archetype->OnEntityChange(ref e, -cmd.ComponentType);
                            //Debug.Log("REMOVED IN ECB");
                            break;
                        case ECBCommand.Type.SetComponent:

                            break;
                        case ECBCommand.Type.CreateEntity:
                            world.CreateEntity();
                            break;
                        case ECBCommand.Type.DestroyEntity:
                            //world.GetEntity(cmd.Entity).Destroy();
                            break;
                        case ECBCommand.Type.ChangeTransformRefPosition:
                            //world.GetEntity(cmd.Entity).Get<TransformRef>().value.position = new Vector3(cmd.Position.x, cmd.Position.y, cmd.Position.z);
                            break;
                        case ECBCommand.Type.SetActiveGameObject:
                            //world.GetEntity(cmd.Entity).Get<Pooled>().SetActive(cmd.active == 1);
                            break;
                        case ECBCommand.Type.SetActiveEntity:
                            //ref var e = ref world.GetEntity(cmd.Entity);
                            //EntityPool.Back(e, e.Get<PooledEntity>());
                            break;
                        case ECBCommand.Type.PlayParticleReference:
                            //if (cmd.active == 1) {
                            //    world.GetEntity(cmd.Entity).Get<Particle>().value.Play();
                            //}
                            //else {
                            //    world.GetEntity(cmd.Entity).Get<Particle>().value.Stop();
                            //}
                            break;
                        case ECBCommand.Type.ReuseView:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    ecb->count--;
                }

                buffer->Clear();
        }
        public void Playback(ref World world) {
            //ecb->Playback(ref world);

            for (var i = 0; i < ecb->perThreadBuffer->Length; i++) {
                var buffer = ecb->perThreadBuffer->ElementAt(i);
                //if (buffer->IsEmpty) continue;

                for (var cmdIndex = 0; cmdIndex < buffer->Length; cmdIndex++) {
                    ref var cmd = ref buffer->ElementAt(cmdIndex);
                    ref var e = ref world.GetEntity(cmd.Entity);
                    switch (cmd.EcbCommandType) {
                        case ECBCommand.Type.AddComponent:
                            
                            // ref var pool = ref world._impl->GetUntypedPool(cmd.ComponentType);
                            // pool.SetPtr(e.id, cmd.Component);
                            // UnsafeUtility.Free(cmd.Component, Allocator.Temp);
                            e.archetypeRef.OnEntityChange(ref e, cmd.ComponentType);
                            break;
                        case ECBCommand.Type.AddComponentNoData:
                            e.archetypeRef.OnEntityChange(ref e, cmd.ComponentType);
                            //world.GetEntity(cmd.Entity).AddByTypeID(cmd.ComponentType);
                            break;
                        case ECBCommand.Type.RemoveComponent:
                            e.archetypeRef.OnEntityChange(ref e, -cmd.ComponentType);
                            //Debug.Log("REMOVED IN ECB");
                            break;
                        case ECBCommand.Type.SetComponent:

                            break;
                        case ECBCommand.Type.CreateEntity:
                            world.CreateEntity();
                            break;
                        case ECBCommand.Type.DestroyEntity:
                            //world.GetEntity(cmd.Entity).Destroy();
                            break;
                        case ECBCommand.Type.ChangeTransformRefPosition:
                            //world.GetEntity(cmd.Entity).Get<TransformRef>().value.position = new Vector3(cmd.Position.x, cmd.Position.y, cmd.Position.z);
                            break;
                        case ECBCommand.Type.SetActiveGameObject:
                            //world.GetEntity(cmd.Entity).Get<Pooled>().SetActive(cmd.active == 1);
                            break;
                        case ECBCommand.Type.SetActiveEntity:
                            //ref var e = ref world.GetEntity(cmd.Entity);
                            //EntityPool.Back(e, e.Get<PooledEntity>());
                            break;
                        case ECBCommand.Type.PlayParticleReference:
                            //if (cmd.active == 1) {
                            //    world.GetEntity(cmd.Entity).Get<Particle>().value.Play();
                            //}
                            //else {
                            //    world.GetEntity(cmd.Entity).Get<Particle>().value.Stop();
                            //}
                            break;
                        case ECBCommand.Type.ReuseView:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
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