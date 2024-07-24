using Unity.Burst;
using Unity.Mathematics;

namespace Wargon.Nukecs.Tests.Sprites {

    public struct SpriteAnimationSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Main;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<SpriteAnimation>().With<SpriteRenderData>().With<Transform>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            
            ref var anim = ref entity.Get<SpriteAnimation>();
            ref var renderData = ref entity.Get<SpriteRenderData>();
            var position = entity.Read<Transform>().position;
            anim.elapsedTime += deltaTime;
            if (anim.elapsedTime >= anim.frameTime)
            {
                anim.currentFrame = (anim.currentFrame + 1) % anim.frameCount;
                anim.elapsedTime -= anim.frameTime;
            }

            int row = anim.currentFrame / anim.framesPerRow;
            int col = anim.currentFrame % anim.framesPerRow;
            float2 currentUV = new float2(
                anim.startUV.x + col * anim.frameSize.x,
                anim.startUV.y + row * anim.frameSize.y
            );
            
            renderData.position = position;
            renderData.uv = new float4(currentUV.x, currentUV.y, anim.frameSize.x, anim.frameSize.y);
            renderData.atlasIndex = anim.atlasIndex;
            
            SpriteRender.Singleton.Add(renderData);
        }

    }
    
    public struct SpriteViewSystem : IQueryJobSystem{
        public SystemMode Mode => SystemMode.Main;

        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Transform>().With<SpriteRenderData>();
        }

        public void OnUpdate(ref Query query, float deltaTime) {
            SpriteRender.Singleton.OnUpdate(ref query);
        }
    }
    public struct SpriteRenderData : IComponent
    {
        public float3 position;
        public float4 uv;
        public int atlasIndex;
    }
    public struct SpriteAnimation : IComponent
    {
        public int atlasIndex;    // Индекс атласа в массиве атласов
        public int frameCount;    // Общее количество кадров в анимации
        public int currentFrame;  // Текущий кадр анимации
        public float frameTime;   // Время отображения одного кадра
        public float elapsedTime; // Прошедшее время с начала текущего кадра
        public float2 frameSize;  // Размер одного кадра в атласе (в UV координатах)
        public float2 startUV;    // Начальные UV координаты первого кадра в атласе
        public int framesPerRow;  // Количество кадров в одном ряду атласа
    }
}