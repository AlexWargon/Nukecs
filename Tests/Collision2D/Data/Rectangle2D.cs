namespace Wargon.Nukecs.Collision2D
{
    public struct Rectangle2D : IComponent {
        public int index;
        public float w;
        public float h;
        public CollisionLayer layer;
        public CollisionLayer collisionWith;
    }
}  