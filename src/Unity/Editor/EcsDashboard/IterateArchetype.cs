#if UNITY_EDITOR && NUKECS_DEBUG
using System;

namespace Wargon.Nukecs.Editor
{
#pragma warning disable CS0618
    public static class IterateArchetype
    {
        private static ObjectTuple _tuple;
        public static unsafe void Iter(int archIndex, ref World world, int start, int end, Action<Entity, IComponent[]> action)
        {
            var iter = new QueryIterObject<ObjectTuple>(archIndex, world.UnsafeWorld, ref _tuple, new Range(start, end));
            foreach (var tuple in iter)
            {
                var components = tuple.GetComponents();
                var e = tuple.GetEntity();
                action?.Invoke(e, components);
            }
        }
    }
}
#endif