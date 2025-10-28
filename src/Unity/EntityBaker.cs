using System.Collections.Generic;
using UnityEngine;

namespace Wargon.Nukecs
{
    public class EntityBaker : MonoBehaviour
    {
        [SerializeReference] private List<IComponent> components = new ();

        public void Bake(ref World world)
        {
            var e = world.Entity();
            foreach (var component in components)
            {
                e.AddObject(component);
            }
        }
    }
}