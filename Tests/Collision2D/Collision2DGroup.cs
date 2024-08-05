namespace Wargon.Nukecs.Collision2D
{
    public class Collision2DGroup : SystemsGroup{
        public Collision2DGroup(ref World world) : base(ref world){

            this.name = "Collision2D";
                //.Add(new Collision2DOnRectangleOnConvertEntitySystem())
             Add<CollisionClearGridCellsSystem>()
             .Add<Velocity2DSystem>()
            //.Add<CollidersSizeUpdateSystem>()
            .Add<Collision2DPopulateRectsSystem>()
            .Add<Collision2DPopulateCirclesSystem>()
            //.Add<UpdateCirclePositionsSystem>()
            .Add<Collision2DSystem>()
             
            ;
        }
    }
}  