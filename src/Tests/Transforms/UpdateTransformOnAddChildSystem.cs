namespace Wargon.Nukecs.Transforms {
    
    using Unity.Mathematics;
    
    public struct UpdateTransformOnAddChildSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;

        public Query GetQuery(ref World world)
        {
            return world.Query().With<ChildOf>().With<Transform>().With<OnAddChildWithTransformEvent>();
        }

        public void OnUpdate(ref Entity child, ref State state)
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
}