using UnityEngine;

namespace Wargon.Nukecs.Tests
{


    public class NukecsDebugUpdater : MonoBehaviour
    {
#if UNITY_EDITOR
        private static NukecsDebugUpdater instance;
        public static NukecsDebugUpdater Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("NukecsDebugUpdater");
                    go.AddComponent<NukecsDebugUpdater>();
                    DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        public event System.Action OnUpdate;

        private void Awake()
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            OnUpdate?.Invoke();
        }

        private void OnDestroy()
        {
            instance = null;
        }
#endif
    }
}