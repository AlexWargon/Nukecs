using System;
using UnityEngine;
using Wargon.Nukecs;
using Object = UnityEngine.Object;

namespace Wargon.Nukecs.Demos.HotReload
{
    public struct RotationSpeed : IComponent
    {
        public float RadiansPerSecond;
    }

    public struct GameObjectView : IComponent, IDisposable
    {
        public ObjectRef<GameObject> val;

        public void Dispose()
        {
            if (val.IsValid() && val != null)
            {
                Object.Destroy(val.Value);
                val.Dispose();
            }
            dbug.log("GO Disposed");
        }
    }
}
