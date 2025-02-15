

namespace Wargon.Nukecs {
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;
    
    public unsafe struct EntityFilterBuffer : IDisposable {
        [NativeDisableUnsafePtrRestriction] private readonly EFBInternal* efbPtr;
        public int Capacity => efbPtr->Capacity;
        public int Count => efbPtr->count;
        public bool IsCreated => efbPtr != null && efbPtr->isCreated == 1;
        [NativeSetThreadIndex] internal int ThreadIndex;

        public EntityFilterBuffer(int startSize) {
            efbPtr = (EFBInternal*) UnsafeUtility.Malloc(sizeof(EFBInternal), UnsafeUtility.AlignOf<EFBInternal>(),
                Allocator.Persistent);
            *efbPtr = new EFBInternal();
            ThreadIndex = 0;
            //ecb->internalBuffer = UnsafeList<ECBCommand>.Create(startSize, Allocator.Persistent);
            efbPtr->perThreadBuffer = Chains(startSize);
            efbPtr->isCreated = 1;
        }

        private static UnsafePtrList<Unity.Collections.LowLevel.Unsafe.UnsafeList<EFBCommand>>* Chains(int startSize) {
            var threads = JobsUtility.JobWorkerCount + 1;
            var ptrList =
                UnsafePtrList<Unity.Collections.LowLevel.Unsafe.UnsafeList<EFBCommand>>.Create(threads, Allocator.Persistent);
            for (var i = 0; i < threads; i++) {
                var list = Unity.Collections.LowLevel.Unsafe.UnsafeList<EFBCommand>.Create(startSize, Allocator.Persistent);
                ptrList->Add(list);
            }

            return ptrList;
        }

        [StructLayout(LayoutKind.Sequential)]
        // ReSharper disable once InconsistentNaming
        public struct EFBCommand {
            internal Edge edge;
            internal int entity;
            public override string ToString() {
                return $"Entity {World.Get(0).GetEntity(entity).ToString()}; move to {edge.ToMovePtr->ToString()}";
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

        internal struct EFBInternal {
            internal byte isCreated;

            //[NativeDisableUnsafePtrRestriction]
            //internal UnsafeList<ECBCommand>* internalBuffer;
            [NativeDisableUnsafePtrRestriction] internal UnsafePtrList<Unity.Collections.LowLevel.Unsafe.UnsafeList<EFBCommand>>* perThreadBuffer;


            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<EFBCommand>* internalBuffer {
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
                        
                    }

                    buffer->Clear();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                for (var i = 0; i < perThreadBuffer->Length; i++) {
                    Unity.Collections.LowLevel.Unsafe.UnsafeList<EFBCommand>.Destroy(perThreadBuffer->ElementAt(i));
                }

                UnsafePtrList<Unity.Collections.LowLevel.Unsafe.UnsafeList<EFBCommand>>.Destroy(perThreadBuffer);
                //UnsafeList<ECBCommand>.Destroy(internalBuffer);
                isCreated = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(int entity, Edge edge) {
            var cmd = new EFBCommand {
                entity = entity,
                edge = edge
            };
            var buffer = efbPtr->perThreadBuffer->ElementAt(ThreadIndex);
            buffer->Add(cmd);
        }
        public void Playback() {
            //ecb->Playback(ref world);
            for (var i = 0; i < efbPtr->perThreadBuffer->Length; i++) {
                var buffer = efbPtr->perThreadBuffer->ElementAt(i);
                if (buffer->IsEmpty) continue;
                for (var cmdIndex = 0; cmdIndex < buffer->Length; cmdIndex++) {
                    ref var cmd = ref buffer->ElementAt(cmdIndex);
                    //cmd.edge.Execute(cmd.entity);
                }
                buffer->Clear();
            }
        }

        public void Dispose() {
            efbPtr->Dispose();
            UnsafeUtility.Free(efbPtr, Allocator.Persistent);
        }
    }
}