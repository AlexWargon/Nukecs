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
            for (int i = 0; i < world.UnsafeWorld->archetypesList.Length; i++)
            {
                var archetype = world.UnsafeWorld->archetypesList[i];
                archetype->Refresh();
            }
            world.Update();
        }
    }
}
