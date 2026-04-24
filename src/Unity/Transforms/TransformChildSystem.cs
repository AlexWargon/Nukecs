namespace Wargon.Nukecs.Transforms
{
    using Unity.Burst;
    using Unity.Mathematics;
    [BurstCompile]
    public struct TransformChildSystem : IEntityJobSystem
    {
        public readonly Threads Mode => Threads.Parallel;

        public Query GetQuery(ref World world) => world.Query()
            .With<ChildOf>()
            .With<Transform>()
            .With<LocalTransform>()
            .None<OnAddChildWithTransformEvent>();

        public void OnUpdate(ref Entity entity, ref State state)
        {
            ref var transform = ref entity.Get<Transform>();
            ref var localTransform = ref entity.Get<LocalTransform>();
            ref readonly var parentTransform =
                ref entity.Get<ChildOf>().Value.Get<Transform>();

            transform.Position = math.mul(parentTransform.Rotation, localTransform.Position * parentTransform.Scale) + parentTransform.Position;
            transform.Rotation = math.mul(parentTransform.Rotation, localTransform.Rotation);
            transform.Scale = localTransform.Scale * parentTransform.Scale;
        }
    }
}
