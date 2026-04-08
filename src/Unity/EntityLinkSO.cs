using System.Collections.Generic;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [CreateAssetMenu]
    public class EntityLinkSO : ScriptableObject {
        [SerializeReference] protected System.Collections.Generic.List<IComponent> components = new ();
        [SerializeField] protected System.Collections.Generic.List<ICustomConvertor> convertors = new ();
        public Entity Convert(ref World world)
        {
            var e = world.Entity();
            foreach (var component in components)
            {
                e.AddObject(component);
            }
            foreach (var customConvertor in convertors) {
                customConvertor.Convert(ref world, ref e);
            }
            return e;
        }
    }
}