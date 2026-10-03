// Managed Unity lifecycle adapter; registration and lookup live in the Burst core.
#if UNITY_EDITOR
using UnityEditor;
namespace Wargon.Nukecs
{
    internal static class LocalRegistryLifetime
    {
        [InitializeOnLoadMethod]
        private static void RegisterCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += LocalParamSlots.Dispose;
            EditorApplication.quitting += LocalParamSlots.Dispose;
        }
    }
}
#else
using UnityEngine;
namespace Wargon.Nukecs
{
    internal static class LocalRegistryLifetime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterCleanup() => Application.quitting += LocalParamSlots.Dispose;
    }
}
#endif
