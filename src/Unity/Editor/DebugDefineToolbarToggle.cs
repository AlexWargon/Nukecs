#if UNITY_EDITOR


using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    using UnityEditor;
    using UnityEngine;
    using System.Linq;

    [InitializeOnLoad]
    public static class DebugDefineToolbarToggle
    {
        private const string DEBUG_SYMBOL = "NUKECS_DEBUG";
        private static bool isDebugEnabled;

        static DebugDefineToolbarToggle()
        {
            EditorApplication.update += Update;
            isDebugEnabled = HasDebugSymbol();
        }

        private static void Update()
        {
            SceneView.duringSceneGui -= OnGUI;
            SceneView.duringSceneGui += OnGUI;
            EditorApplication.update -= Update;
        }

        private static void OnGUI(SceneView sceneView)
        {
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(Screen.width - 150, 5, 140, 20));
            bool newValue = GUILayout.Toggle(isDebugEnabled, "DEBUG");
            if (newValue != isDebugEnabled)
            {
                isDebugEnabled = newValue;
                SetDebugSymbol(isDebugEnabled);
            }

            GUILayout.EndArea();

            Handles.EndGUI();
        }

        private static bool HasDebugSymbol()
        {
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            return defines.Split(';').Contains(DEBUG_SYMBOL);
        }

        private static void SetDebugSymbol(bool enable)
        {
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            var parts = defines.Split(';').ToList();

            if (enable)
            {
                if (!parts.Contains(DEBUG_SYMBOL))
                    parts.Add(DEBUG_SYMBOL);
                else
                    return;
            }
            else
            {
                parts.RemoveAll(s => s == DEBUG_SYMBOL);
            }

            string newDefines = string.Join(";", parts);
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);

            Debug.Log($"DEBUG symbol {(enable ? "enabled" : "disabled")}. New defines: {newDefines}");

            AssetDatabase.Refresh();
        }
    }
}
#endif