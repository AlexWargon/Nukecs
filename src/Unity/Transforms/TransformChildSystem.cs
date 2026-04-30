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

    public static partial class Transforms
    {
        [BurstCompile, System]
        public static void ChildSystem(
            ref Query<ChildOf, Transform, LocalTransform, None<OnAddChildWithTransformEvent>> query)
        {
            foreach (var (childOfRef, transformRef, localTransformRef,_) in query)
            {
                ref var transform = ref transformRef.Get;
                ref var localTransform = ref localTransformRef.Get;
                ref readonly var parentTransform =
                    ref childOfRef.Get.Value.Get<Transform>();

                transform.Position = Unity.Mathematics.math.mul(parentTransform.Rotation, localTransform.Position * parentTransform.Scale) + parentTransform.Position;
                transform.Rotation = Unity.Mathematics.math.mul(parentTransform.Rotation, localTransform.Rotation);
                transform.Scale = localTransform.Scale * parentTransform.Scale;
            }
        }
        [System]
        public static void SyncWithUnityTransformSystem(ref Query<Transform, TransformRef, None<NoneSyncTransform>> query)
        {
            foreach (var (tRef,tRefRef) in query)
            {
                var transformRef = tRefRef.Get.Value.Value;
                ref var transform = ref tRef.Get;

                transformRef.position = transform.Position;
                transformRef.rotation = transform.Rotation;
                transformRef.localScale = transform.Scale;
            }
        }
    }
}
