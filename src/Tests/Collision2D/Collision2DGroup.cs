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
}  