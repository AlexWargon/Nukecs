using Unity.Burst;

namespace Wargon.Nukecs.Transforms {
    
    using Unity.Mathematics;
    [BurstCompile]
    public struct UpdateTransformOnAddChildSystem : IEntityJobSystem
    {
        public Threads Mode => Threads.Parallel;

        public Query GetQuery(ref World world)
        {
            return world.Query().With<ChildOf>().With<Transform>().With<OnAddChildWithTransformEvent>();
        }

        public void OnUpdate(ref Entity child, ref State state)
        {
            ref var chilfOf = ref child.Get<ChildOf>();
            ref var childTransform = ref child.Get<Transform>();
            
            ref readonly var parentTransform = ref chilfOf.Value.Get<Transform>();
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
}