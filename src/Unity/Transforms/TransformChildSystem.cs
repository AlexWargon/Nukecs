namespace Wargon.Nukecs.Transforms
{
    using Unity.Burst;
    using Unity.Mathematics;
    [BurstCompile]
    public struct TransformChildSystem : IEntityJobSystem
    {
        public readonly SystemMode Mode => SystemMode.Parallel;

        public Query GetQuery(ref World world) => world.Query()
            .With<ChildOf>()
            .With<Transform>()
            .With<LocalTransform>()
            .None<OnAddChildWithTransformEvent>();

        public void OnUpdate(ref Entity entity, ref State state)
        {
            var (cref, tref, ltref) = entity.Get<ChildOf, Transform, LocalTransform>();
            ref var transform = ref tref.Val;
            ref var localTransform = ref ltref.Val;
            ref readonly var parentTransform =
                ref state.World.GetPool<Transform>().GetRef<Transform>(cref.Val.Value.id);

            transform.Position = math.mul(parentTransform.Rotation, localTransform.Position * parentTransform.Scale) + parentTransform.Position;
            transform.Rotation = math.mul(parentTransform.Rotation, localTransform.Rotation);
            transform.Scale = localTransform.Scale * parentTransform.Scale;
        }
    }
}
