using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Collision2D
{
    public class Collision2DGroup : SystemsGroup
    {
        public Collision2DGroup(ref World world) : base(ref world){

            this.name = "Collision2D";
            this
                //.Add(new Collision2DOnRectangleOnConvertEntitySystem())
                
            .Add<CollisionsClear>()
            .Add<AddCollision2DDataSystem>()
            .Add<CollisionClearGridCellsSystem>()
            .Add<Collision2DPopulateRectsSystem>()
            .Add<Collision2DPopulateCirclesSystem>()
            .Add<Collision2DSystem>()
            .Add<WriteCollisionsEventsSystem>()
            
            ;
        }
    }
    public struct CollisionEvents
    {
        public UnsafeList<Collision2DData> Events;

        public void Add(in Collision2DData data)
        {
            
        }
    }
    public struct CollisionEventsSystem: IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;

        public Query GetQuery(ref World world)
        {
            return world.Query().WithArray<Collision2DData>().With<CollidedFlag>();
        }

        public void OnUpdate(ref Entity entity, ref State state)
        {
            
        }
    }
}  