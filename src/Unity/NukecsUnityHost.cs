using UnityEngine;
using Wargon.Nukecs.Tests;

namespace Wargon.Nukecs
{
    // Connects the engine-independent core to Unity: log output, application quitting and
    // static cleanup of Unity-side registries.
    internal static class NukecsUnityHost
    {
        private static bool installed;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            if (installed) return;
            installed = true;
            dbug.Logger = new UnityNukecsLogger();
            NukecsLifecycle.SetQuittingSource(h => Application.quitting += h, h => Application.quitting -= h);
            NukecsLifecycle.StaticDisposed += EntityPrefabMap.Dispose;
        }
    }

    internal sealed class UnityNukecsLogger : INukecsLogger
    {
        private const string COLOR_FORMAT = "<color={0}>{1}</color>";

        public void Log(object message) => Debug.Log(message);

        public void LogColored(string message, object color)
        {
            var name = color switch
            {
                Color c => "#" + ColorUtility.ToHtmlStringRGB(c),
                string s => s,
                _ => null
            };
            Debug.Log(name == null ? message : string.Format(COLOR_FORMAT, name, message));
        }

        public void Warning(string message) => Debug.LogWarning(message);

        public void Error(string message) => Debug.LogError(message);
    }
}
