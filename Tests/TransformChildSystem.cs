using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    //[BurstCompile]
    public struct UpdateTransformOnAddChildSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;

        public Query GetQuery(ref World world)
        {
            return world.Query().With<ChildOf>().With<Transform>().With<OnAddChildWithTransformEvent>();
        }

        public void OnUpdate(ref Entity child, float deltaTime)
        {
            var (cref, tref) = child.Get<ChildOf, Transform>();
            ref var childTransform = ref tref.Value;
            
            ref readonly var parentTransform = ref cref.Value.Value.Read<Transform>();
            // Get local transform values relevent to parent
            var localPosition = math.mul(math.inverse(parentTransform.Rotation), childTransform.Position - parentTransform.Position) / parentTransform.Scale;
            var localRotation = math.mul(math.inverse(parentTransform.Rotation), childTransform.Rotation);
            var localScale = childTransform.Scale / parentTransform.Scale;

            // Add or update LocalTransform
            if (child.Has<LocalTransform>())
            {
                ref var localTransform = ref child.Get<LocalTransform>();
                localTransform.Position = localPosition;
                localTransform.Rotation = localRotation;
                localTransform.Scale = localScale;
            }
            else
            {
                child.Add(new LocalTransform
                {
                    Position = localPosition,
                    Rotation = localRotation,
                    Scale = localScale
                });
            }

            child.Remove<OnAddChildWithTransformEvent>();
        }
    }
    public static class debug{
        public static void has(){
            Debug.Log("has");
        }
        public static void has_no(){
            Debug.Log("has no");
        }
    } 
    [BurstCompile]
    public struct TransformChildSystem : IEntityJobSystem
    {
        public readonly SystemMode Mode => SystemMode.Parallel;

        public Query GetQuery(ref World world)
        {
            return world.Query().With<ChildOf>().With<Transform>().With<LocalTransform>().None<OnAddChildWithTransformEvent>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime)
        {
            var (cref, tref, ltref) = entity.Get<ChildOf, Transform, LocalTransform>();
            ref var transform = ref tref.Value;
            ref var localTransform = ref ltref.Value;
            ref readonly var parentTransform = ref cref.Value.Value.Read<Transform>();

            transform.Position = math.mul(parentTransform.Rotation, localTransform.Position * parentTransform.Scale) + parentTransform.Position;

            transform.Rotation = math.mul(parentTransform.Rotation, localTransform.Rotation);
            
            transform.Scale = localTransform.Scale * parentTransform.Scale;
        }
    }
}
