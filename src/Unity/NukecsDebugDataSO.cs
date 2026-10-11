using UnityEngine;

namespace Wargon.Nukecs
{
    [CreateAssetMenu]
    public class NukecsDebugDataSO : ScriptableObject
    {
        public NukecsDebugData data;

        public void OnEnable()
        {
            World.OnWorldCreating(() =>
            {
                NukecsDebugData.Instance = data;
            });
        }
    }
}
