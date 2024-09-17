using System;
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
    [Serializable]
    public class NukecsDebugData
    {
        public bool showInitedComponents;

        private static NukecsDebugData instance;
        public static NukecsDebugData Instance
        {
            get
            {
                if (instance == null) instance = new NukecsDebugData();
                return instance;
            }
            set => instance = value;
        }
    }
}
