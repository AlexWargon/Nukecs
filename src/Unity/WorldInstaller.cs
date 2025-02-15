using UnityEngine;

namespace Wargon.Nukecs
{
    public abstract class WorldInstaller : MonoBehaviour
    {
        protected World world;
        protected WorldConfig config = WorldConfig.Default16384;
        public abstract void OnWorldCreated(ref World world);
        private unsafe void Awake()
        {
            world = World.Create(config);
            OnWorldCreated(ref world);
            for (var i = 0; i < world.UnsafeWorld->archetypesList.Length; i++)
            {
                ref var archetype = ref world.UnsafeWorld->archetypesList[i];
                archetype.Ptr->Refresh();
            }
            world.Update();
        }
    }
}
