#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Wargon.Nukecs;

public class SystemsViewer : EditorWindow
{
    private Systems targetSystems;
    private Vector2 scroll;

    [MenuItem("Nuke.cs/Simple Systems Viewer")]
    public static void ShowWindow()
    {
        GetWindow<SystemsViewer>("Systems Viewer");
    }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Запусти Play Mode, чтобы увидеть системы.", MessageType.Info);
            return;
        }

        if (World.Get(0).IsAlive == false) return;

        targetSystems = WorldSystems.Get(0, 0);
        if (targetSystems == null)
        {
            EditorGUILayout.LabelField("Системы не найдены.");
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("MainThreadRunners (Update)", EditorStyles.boldLabel);
        DrawRunnerList(targetSystems.mtRunners);

        EditorGUILayout.LabelField("Runners (Update)", EditorStyles.boldLabel);
        DrawRunnerList(targetSystems.runners);

        EditorGUILayout.LabelField("MainThreadFixedRunners (FixedUpdate)", EditorStyles.boldLabel);
        DrawRunnerList(targetSystems.mtFixedRunners);

        EditorGUILayout.LabelField("FixedRunners (FixedUpdate)", EditorStyles.boldLabel);
        DrawRunnerList(targetSystems.fixedRunners);

        EditorGUILayout.EndScrollView();
    }

    void DrawRunnerList(System.Collections.Generic.List<ISystemRunner> runners)
    {
        if (runners == null) return;
        int index = 0;
        foreach (var runner in runners)
        {
            EditorGUILayout.LabelField($"{index++}. {runner.Name}");
        }
        EditorGUILayout.Space();
    }
}
#endif