using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Transforms
{
    public static class Systems
    {
        public struct SyncTransformsSystem : ISystem, IOnCreate
        {
            private Query _query;

            public void OnCreate(ref World world)
            {
                _query = world.Query().With<Transform>().With<TransformRef>().None<NoneSyncTransform>();
            }
            public void OnUpdate(ref State state)
            {
                foreach (ref var entity in _query)
                {
                    var transformRef = entity.Get<TransformRef>().Value.Value;
                    ref var transform = ref entity.Get<Transform>();

                    transformRef.position = transform.Position;
                    transformRef.rotation = transform.Rotation;
                    transformRef.localScale = transform.Scale;
                }
            }

        }
        [System]
        public static void SyncSystem(ref Query<Transform, TransformRef, None<NoneSyncTransform>> query)
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