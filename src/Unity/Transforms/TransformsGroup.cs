using Unity.Burst;
using Unity.Mathematics;

namespace Wargon.Nukecs.Transforms
{
    public class TransformsGroup : ISystemsGroup
    {
        public void Build(Nukecs.Systems systems, ref World world)
        {
            systems.AddSystems(SystemPath.Update, 
                UpdateTransformOnAddChildSystem, 
                TransformChildSystem, 
                (SyncWithUnityTransformSystem, Threads.Main));
        }
        
        
        [System, BurstCompile]
        public static void UpdateTransformOnAddChildSystem(
            ref Query<ChildOf, Transform, With<OnAddChildWithTransformEvent>>.WithEntity query)
        {
            foreach (var (child,childOfRef, transformRef) in query)
            {
                ref var chilfOf = ref childOfRef.Get;
                ref var childTransform = ref transformRef.Get;
            
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
        [BurstCompile, System]
        public static void TransformChildSystem(
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