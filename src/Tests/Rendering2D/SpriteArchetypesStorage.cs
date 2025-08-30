namespace Wargon.Nukecs.Tests {
    using System;
    using Unity.Mathematics;
    using UnityEngine;

    public static class ShaderNames {
        public const string Sprites = "Custom/SpriteShaderInstanced";
        public const string SpritesWithShadow = "Custom/SpriteShaderInstancedWithShadowURP";
        public const string ShadowShader = "URP2D/SpriteShadowInstancedURP";
    }
    public class SpriteArchetypesStorage : SingletonBase<SpriteArchetypesStorage> {
        internal SpriteArchetype[] archetypes = new SpriteArchetype[6];
        internal int count;
        
        public void OnUpdate() {
            for (var i = 0; i < count; i++) {
                archetypes[i].OnUpdate();
                archetypes[i].Clear();
            }
        }

        public void Clear() {
            for (var i = 0; i < count; i++) {
                archetypes[i].Clear();
            }
        }

        // public unsafe SpriteChunk* GetSpriteChunkPtr(int archetypeIndex)
        // {
        //     return archetypes[archetypeIndex].Chunk;
        // }
        public ref SpriteArchetype Add(Texture2D atlas, Shader shader, ref World world, bool renderShadows = false) {
            Resize();
            var shaderID = shader.GetInstanceID();
            var instanceID = atlas.GetInstanceID();
            var h = Has(instanceID, shaderID);
            if (h.has) {
                return ref archetypes[h.index];
            };
            dbug.log("NOT HAS CREATE NEW", Color.red);
            var material = new Material(shader) {
                mainTexture = atlas
            };
            var arch = new SpriteArchetype {
                material = material,
                mesh = CreateQuadMesh(),
                instanceID = instanceID,
                shaderID = shaderID,
                chunk = SpriteChunk.Create(world.Config.StartEntitiesAmount, ref world.AllocatorHandler.AllocatorWrapper),
                camera = Camera.main,
                index = count,
                renderShadow = renderShadows,
                shadowMaterial = renderShadows ? new Material(Shader.Find(ShaderNames.ShadowShader))
                {
                    mainTexture = atlas
                } : null
            };
            dbug.log($" added shader: {shaderID}, atlas: {(instanceID, atlas.name)}, shadow: {renderShadows}", Color.yellow);
            archetypes[count] = arch;
            count++;
            return ref archetypes[count-1];
        }
        
        public unsafe ref SpriteArchetype Add(Texture2D atlas, ref World world) {
            Resize();
            var shader = Shader.Find(ShaderNames.SpritesWithShadow);
            var shaderID = shader.GetInstanceID();
            var instanceID = atlas.GetInstanceID();
            var h = Has(instanceID, shaderID);
            if (h.has) {
                return ref archetypes[h.index];
            };
            var material = new Material(shader) {
                mainTexture = atlas
            };
            var arch = new SpriteArchetype {
                material = material,
                mesh = CreateQuadMesh(),
                instanceID = instanceID,
                shaderID = shaderID,
                chunk = SpriteChunk.Create(world.Config.StartEntitiesAmount, ref world.AllocatorHandler.AllocatorWrapper),
                camera = Camera.main,
                index = count
            };
            archetypes[count] = arch;
            count++;
            return ref archetypes[count-1];
        }

        private (int index, bool has) Has(int id, int shader) {
            for (var i = count; i >= 0; i--)
            {
                ref readonly var  arch = ref archetypes[i];
                if (arch.instanceID == id && arch.shaderID == shader) return (i, true);
            }
            return (0, false);
        }
        
        private void Resize() {
            if (count >= archetypes.Length) {
                Array.Resize(ref archetypes, archetypes.Length * 2);
            }
        }
        
        public void Dispose() {
            for (var i = 0; i < count; i++) {
                archetypes[i].Dispose();
                archetypes[i] = default;
            }

            count = 0;
        }
        
        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh {
                vertices = new Vector3[] {
                    new (0, 0, 0),
                    new (1, 0, 0),
                    new (1, 1, 0),
                    new (0, 1, 0)
                },
                uv = new Vector2[] {
                    new (0, 0),
                    new (1, 0),
                    new (1, 1),
                    new (0, 1)
                },
                triangles = new [] { 0, 2, 1, 0, 3, 2 }
            };
            return mesh;
        }
    }

    // public struct CullingSystem : ISystem, IOnCreate {
    //     public void OnCreate(ref World world) {
    //         culled = world.Query()
    //             .With<SpriteRenderData>()
    //             .With<SpriteChunkReference>()
    //             .With<Culled>();
    //             
    //         unculled = world.Query()
    //             .With<SpriteRenderData>()
    //             .With<SpriteChunkReference>()
    //             .None<Culled>();
    //     }
    //
    //     private Query unculled;
    //     private Query culled;
    //     public void OnUpdate(ref World world, float deltaTime) {
    //         var data = CullingData.instance;
    //         var transforms = world.GetPool<Transform>().AsComponentPool<Transform>();
    //         world.DependenciesUpdate = new CullJob {
    //             xMax = data.xMax,
    //             xMin = data.xMin,
    //             yMax = data.yMax,
    //             yMin = data.yMin,
    //             transforms = transforms,
    //             query = unculled,
    //         }.Schedule(unculled.Count, 1, world.DependenciesUpdate);
    //         world.DependenciesUpdate = new UnCullJob {
    //             xMax = data.xMax,
    //             xMin = data.xMin,
    //             yMax = data.yMax,
    //             yMin = data.yMin,
    //             transforms = transforms,
    //             query = culled,
    //         }.Schedule(culled.Count, 1, world.DependenciesUpdate);
    //     }
    //     [BurstCompile]
    //     private struct CullJob : IJobParallelFor {
    //         public ComponentPool<Transform> transforms;
    //         public Query query;
    //
    //         public float xMax;
    //         public float yMax;
    //         public float xMin;
    //         public float yMin;
    //         public void Execute(int index) {
    //             ref var entity = ref query.GetEntity(index);
    //             ref var transform = ref transforms.Get(entity.id);
    //             if (!(transform.Position.x < xMax && 
    //                   transform.Position.x > xMin &&
    //                   transform.Position.y < yMax && 
    //                   transform.Position.y > yMin)) {
    //                 entity.Cull();
    //             }
    //         }
    //     }
    //     
    //     [BurstCompile]
    //     private struct UnCullJob : IJobParallelFor {
    //         public ComponentPool<Transform> transforms;
    //         public Query query;
    //
    //         public float xMax;
    //         public float yMax;
    //         public float xMin;
    //         public float yMin;
    //         public void Execute(int index) {
    //             ref var entity = ref query.GetEntity(index);
    //             ref var transform = ref transforms.Get(entity.id);
    //             if (transform.Position.x < xMax && transform.Position.x > xMin && transform.Position.y < yMax &&
    //                 transform.Position.y > yMin) {
    //                 entity.UnCull();
    //             }
    //         }
    //     }
    // }

    public struct CullingData : IInit , IDisposable{
        public static ref CullingData instance => ref Singleton<CullingData>.Instance;
        public float2 CameraPositions;
        public float Width;
        public float Height;
        public float xMax;
        public float yMax;
        public float xMin;
        public float yMin;
        public void Init(){}
        public void Update(Camera camera) {
            Height = camera.orthographicSize * 2f + 1f;
            Width = Height * camera.aspect;
            CameraPositions = new float2(camera.transform.position.x, camera.transform.position.y);
            xMax = CameraPositions.x + Width / 2;
            yMax = CameraPositions.y + Height / 2;
            xMin = CameraPositions.x - Width / 2;
            yMin = CameraPositions.y - Height / 2;
        }

        public void Dispose()
        {
            
        }
    }

    public struct ClearRenderOnEntityDestroySystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.Query().With<DestroyEntity>().With<SpriteChunkReference>().None<Culled>();
        }

        public void OnUpdate(ref Entity entity, ref State state) {
            entity.Get<SpriteChunkReference>().ChunkRef.Remove(in entity);
        }
    }
}