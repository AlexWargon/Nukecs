﻿using Unity.Burst;
using UnityEngine;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Tests {
    //[BurstCompile]
    public struct FillRenderDataSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world) {
            return world.Query().With<SpriteRenderData>().With<Transform>().None<Culled>();
        }
        public unsafe void OnUpdate(ref Entity entity, ref State state) {
            var (chunk, transform, data) =
                entity.Read<SpriteChunkReference, Transform, SpriteRenderData>();
            SpriteArchetypesStorage.Singleton.GetSpriteChunkPtr(chunk.achetypeIndex)->AddToFill(in entity, in transform, in data);
        }
    }
}