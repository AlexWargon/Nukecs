

using Unity.Collections;
using UnityEngine.Jobs;

namespace Wargon.Nukecs.Transforms {
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Mathematics;
    [Serializable][StructLayout(LayoutKind.Sequential)]
    public struct Transform : IComponent {
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;

        public Transform(float3 pos) {
            Position = pos;
            Rotation = quaternion.identity;
            Scale = new float3(1, 1, 1);
        }
        public float4x4 Matrix
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => float4x4.TRS(Position, Rotation, Scale);
        }
    }

    public struct OnAddChildWithTransformEvent : IComponent { }
    public struct StaticTag : IComponent { }
    public static class TransformsUtility {
        public static void Convert(UnityEngine.Transform transform, ref World world, ref Entity entity) {
            entity.Add(new Transform {
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.lossyScale
            });
            entity.Add(new LocalTransform {
                Position = transform.localPosition,
                Rotation = transform.localRotation,
                Scale = transform.localScale
            });
        }
    }

    public partial struct TransformAspect : IAspect
    {
        public AspectData<Transform> Trasform;
        public AspectData<LocalTransform> LocalTransform;
    }

    // public struct TransformsQuery
    // {
    //     public TransformAccessArray TransformAccess;
    //     public NativeHashMap<int, int> entityToIndex;
    //     public void Add(int entity, int instanceID)
    //     {
    //         if (entityToIndex.ContainsKey(entity)) return; // Уже добавлен
    //
    //         int index = TransformAccess.length;
    //         TransformAccess.Add(instanceID);
    //         entityToIndex[entity] = index;
    //     }
    //     public void Remove(int entity)
    //     {
    //         if (!entityToIndex.TryGetValue(entity, out int index)) return;
    //
    //         int lastIndex = TransformAccess.length - 1;
    //         if (index != lastIndex)
    //         {
    //             TransformAccess[index] = TransformAccess[lastIndex]; // Перемещаем последний элемент
    //             int lastEntity = entityToIndex.FirstOrDefault(e => e.Value == lastIndex).Key;
    //             entityToIndex[lastEntity] = index;
    //         }
    //
    //         TransformAccess.RemoveAtSwapBack(lastIndex);
    //         entityToIndex.Remove(entity);
    //     }
    // }
    public struct TransformRef : IComponent, ICopyable<TransformRef>
    {
        public ObjectRef<UnityEngine.Transform> Value;
        public TransformRef Copy(int to)
        {
            var go = UnityEngine.Object.Instantiate(Value.Value.gameObject);
            return new TransformRef
            {
                Value = go.transform
            };
        }
    }

    public struct ClearTransformsSystem : ISystem, IOnCreate
    {
        private Query query;
        public void OnCreate(ref World world)
        {
            query = world.Query(withDefaultNoneTypes : false).With<DestroyEntity>().With<TransformRef>();
        }

        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in query)
            {
                UnityEngine.Object.Destroy(entity.Get<TransformRef>().Value.Value.gameObject);
            }
        }
    }
}