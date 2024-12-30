namespace Wargon.Nukecs.Collision2D
{
    public class Collision2DGroup : SystemsGroup{
        public Collision2DGroup(ref World world) : base(ref world){

            this.name = "Collision2D";
                this
                //.Add(new Collision2DOnRectangleOnConvertEntitySystem())
                
            .Add<CollisionsClear>()
            .Add<SynchroniseBackSystem>()
            .Add<AddCollision2DDataSystem>()
            //.Add<SetCollisionsSystem>()
            .Add<CollisionClearGridCellsSystem>()
             
            //.Add<CollidersSizeUpdateSystem>()
            .Add<Collision2DPopulateRectsSystem>()
            .Add<Collision2DPopulateCirclesSystem>()
            //.Add<UpdateCirclePositionsSystem>()
             //.Add<CollisionsClear>()

            .Add<Collision2DSystem>()
            
            
            ;
        }
    }
}  