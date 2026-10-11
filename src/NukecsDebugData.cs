using System;

namespace Wargon.Nukecs
{
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
