using System.Collections.Generic;
using TriInspector;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [CreateAssetMenu]
    public class EntityLinkSO : ScriptableObject {
        [Title("Components")][HideLabel][GUIColor(0.6f, 0.9f, 1.0f)][SerializeReference] protected System.Collections.Generic.List<IComponent> components = new ();
        [Title("Convertors")][HideLabel][GUIColor(1.0f, 1.0f, 0.0f)][SerializeField] protected System.Collections.Generic.List<ICustomConvertor> convertors = new ();
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