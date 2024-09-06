using UnityEngine;
using Transform = Wargon.Nukecs.Transforms.Transform;
namespace Wargon.Nukecs.Tests {
    //[BurstCompile]
    // public struct AddToRenderSystem : IEntityJobSystem {
    //     public SystemMode Mode => SystemMode.Parallel;
    //     public Query GetQuery(ref World world) {
    //         return world.Query()
    //             .With<Transforms.Transform>()
    //             .With<SpriteRenderData>()
    //             .With<EntityCreated>()
    //             .With<SpriteChunkReference>()                
    //             .None<Culled>()
    //             .None<DestroyEntity>()
    //             ;
    //     }
    //     public void OnUpdate(ref Entity entity, float deltaTime) {
    //         entity.Get<SpriteChunkReference>().ChunkRef.Add(in entity, in entity.Get<Transforms.Transform>(), in entity.Get<SpriteRenderData>());
    //     }
    // }

    
}