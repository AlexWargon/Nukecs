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
            // Singleton<T> values live in Malloc blocks referenced from SharedStatic, which
            // survives domain reloads. Reset them while the registered function pointers are
            // still valid, like ordinary statics are reset by the reload.
            AssemblyReloadEvents.beforeAssemblyReload += Tests.SingletonRegistry.ResetAll;
            EditorApplication.quitting += Tests.SingletonRegistry.ResetAll;
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
