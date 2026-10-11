using System;
using System.Collections.Generic;

namespace Wargon.Nukecs
{
    public static class StaticAllocations{
        private static readonly List<Action> disposables = new List<Action>();
        public static void AddDisposable(Action action)
        {
#if UNITY_EDITOR
            disposables.Add(action);
#endif
        }
#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        static void Initialize()
        {
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            //AddDisposable(World.DisposeStatic);
        }
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                Clear();
                UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }
        static void Clear()
        {
            for (var i = 0; i < disposables.Count; i++)
            {
                disposables[i]?.Invoke();
            }

            disposables.Clear();
        }
#endif
    }
}