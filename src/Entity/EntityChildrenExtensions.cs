using System.Runtime.CompilerServices;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs
{
    public static class EntityChildrenExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddChild(this ref Entity entity, Entity child)
        {
            if (child.Has<ChildOf>())
            {
                ref var oldParent = ref child.Get<ChildOf>().Value;
                ref var children = ref oldParent.GetArray<Child>();
                foreach (ref var child1 in children)
                    if (child1.Value == child)
                    {
                        children.RemoveAtSwapBack(in child1);
                        break;
                    }

                child.Get<ChildOf>().Value = entity;
            }
            else
            {
                child.Add(new ChildOf { Value = entity });
            }

            if (entity.Has<ComponentArray<Child>>())
            {
                ref var childrenNew = ref entity.GetArray<Child>();
                childrenNew.Add(new Child { Value = child });
            }
            else
            {
                ref var childrenNew = ref entity.AddArray<Child>();
                childrenNew.Add(new Child { Value = child });
            }

            child.Add(new OnAddChildWithTransformEvent());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetParent(this ref Entity entity, Entity newParent)
        {
            newParent.AddChild(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Entity GetRootParent(this ref Entity entity)
        {
            var current = entity;
            if (current.Has<ChildOf>())
            {
                while (current.Has<ChildOf>())
                {
                    current = current.Get<ChildOf>().Value;
                }

                return current;
            }

            return Entity.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Entity GetChild(this ref Entity entity, int index)
        {
            return ref entity.GetArray<Child>().ElementAt(index).Value;
        }

        public static void RemoveChild(this ref Entity entity, Entity child)
        {
            if (!entity.Has<ComponentArray<Child>>()) return;
            ref var children = ref entity.GetArray<Child>();
            foreach (ref var child1 in children)
                if (child1.Value == child)
                {
                    children.RemoveAtSwapBack(in child1);
                    break;
                }

            child.Remove<ChildOf>();
        }
    }
}