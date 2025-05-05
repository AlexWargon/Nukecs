namespace Wargon.Nukecs.Collision2D
{
    using System;

    [Flags]
    public enum CollisionLayer {
        None = 0,
        Player = 1 << 0,
        Enemy = 1 << 1,
        PlayerProjectile = 1 << 2,
        EnemyProjectile = 1 << 3,
        Bonus = 1 << 4,
        DoorTrigger = 1 << 5,
        WinCollider = 1 << 6,
        Wall = 1 << 7,
    }
}  