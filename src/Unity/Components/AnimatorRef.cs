using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    public struct AnimatorRef : IComponent
    {
        public ObjectRef<Animator> Value;
    }

    public struct GameObjectRef : IComponent
    {
        public ObjectRef<GameObject> Value;
    }

    public struct ManagedList<T> : IComponent, System.IDisposable
    {
        public ObjectRef<System.Collections.Generic.List<T>> Value;

        public ManagedList(int size)
        {
            Value = new System.Collections.Generic.List<T>(size);
        }
        public void Dispose()
        {
            Value.Dispose();
        }
    }

    public struct ManagedHashMap<TKey, TValue> : IComponent, System.IDisposable
    {
        public ObjectRef<System.Collections.Generic.Dictionary<TKey, TValue>> Value;

        public ManagedHashMap(int size)
        {
            Value = new System.Collections.Generic.Dictionary<TKey, TValue>(size);
        }
        public void Dispose()
        {
            Value.Dispose();
        }
    }
}