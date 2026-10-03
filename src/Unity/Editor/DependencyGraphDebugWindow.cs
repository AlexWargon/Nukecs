#if UNITY_EDITOR && NUKECS_DEBUG
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wargon.Nukecs.Editor
{
    public class DependencyGraphDebugWindow : EditorWindow
    {
        private int _selectedWorldIndex;
        private int _selectedSystemIndex = -1;
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private Vector2 _bottomScroll;
        private Systems _systems;
        private SystemDependencyGraph _graph;
        private SystemNode[] _nodes;
        private ExecutionGroup[] _groups;
        private readonly List<string> _worldLabels = new();
        private readonly List<int> _worldIds = new();
        private double _lastRefreshTime;
        private const float REFRESH_INTERVAL = 0.2f;

        private static readonly Color ColorMain = new(1f, 0.6f, 0.2f);
        private static readonly Color ColorParallel = new(0.3f, 0.85f, 0.4f);
        private static readonly Color ColorSingle = new(0.3f, 0.6f, 1f);
        private static readonly Color ColorMainRun = new(0.2f, 0.85f, 0.85f);
        private static readonly Color ColorGroupBg = new(0.15f, 0.15f, 0.15f);
        private static readonly Color ColorSelected = new(0.2f, 0.4f, 0.7f);
        private static readonly Color ColorSeparator = new(0.3f, 0.3f, 0.3f);
        private static readonly Color ColorDepEdge = new(0.7f, 0.4f, 0.1f);
        private static readonly Color ColorDepByEdge = new(0.1f, 0.6f, 0.3f);

        [MenuItem("Nuke.cs/Dependency Graph")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<DependencyGraphDebugWindow>();
            wnd.titleContent = new GUIContent("Dependency Graph");
            wnd.minSize = new Vector2(750, 450);
        }

        private void OnGUI()
        {
            RefreshWorldList();

            DrawToolbar();

            if (_systems == null || _graph == null)
            {
                EditorGUILayout.HelpBox(
                    "No dependency graph available. Call .UseDependencyGraph() on your Systems instance, or select a valid world.",
                    MessageType.Info);
                return;
            }

            // Top panels: Systems list + Execution Groups side by side
            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();

            // Bottom panel: System Details
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(150));
            DrawBottomPanel();
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (_worldLabels.Count > 0)
            {
                var newIndex = EditorGUILayout.Popup("World", _selectedWorldIndex, _worldLabels.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(200));
                if (newIndex != _selectedWorldIndex)
                {
                    _selectedWorldIndex = newIndex;
                    _selectedSystemIndex = -1;
                    TryResolveSystems();
                }
            }
            else
            {
                EditorGUILayout.LabelField("World", "No active worlds", EditorStyles.toolbarPopup, GUILayout.Width(200));
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                TryResolveSystems();
            }

            if (_graph != null)
            {
                if (GUILayout.Button("Export Deps", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    ExportDependencyData();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    $"Nodes: {_graph.NodeCount}  Groups: {_graph.GroupCount}  Cyclic: {_graph.HasCyclicDependency()}",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            EditorGUILayout.LabelField("Systems", EditorStyles.boldLabel);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_nodes != null)
            {
                for (int i = 0; i < _nodes.Length; i++)
                {
                    var node = _nodes[i];
                    var modeColor = GetThreadModeColor(node.ThreadMode);
                    var isSelected = i == _selectedSystemIndex;

                    var rect = EditorGUILayout.GetControlRect(false, 20);
                    if (isSelected)
                        EditorGUI.DrawRect(rect, ColorSelected);

                    var nameRect = new Rect(rect.x + 4, rect.y, rect.width - 80, rect.height);
                    var badgeRect = new Rect(rect.xMax - 76, rect.y + 2, 72, rect.height - 4);

                    if (GUI.Button(nameRect, node.Name, EditorStyles.label))
                    {
                        _selectedSystemIndex = i;
                    }

                    var prevColor = GUI.color;
                    GUI.color = modeColor;
                    GUI.Label(badgeRect, node.ThreadMode.ToString(), EditorStyles.miniBoldLabel);
                    GUI.color = prevColor;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Execution Groups", EditorStyles.boldLabel);
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_groups == null || _groups.Length == 0 || _nodes == null)
            {
                EditorGUILayout.HelpBox("No execution groups. Graph may be empty.", MessageType.Info);
            }
            else
            {
                for (int g = 0; g < _groups.Length; g++)
                {
                    ref var group = ref _groups[g];
                    var totalSystems = group.MainIndices.Length + group.ParallelIndices.Length;

                    var groupRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    var headerRect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(headerRect, ColorGroupBg);
                    GUI.Label(headerRect, $"  Group {g}  ({totalSystems} systems)", EditorStyles.boldLabel);

                    // Main systems
                    foreach (var idx in group.MainIndices)
                    {
                        DrawSystemRow(idx, true);
                    }

                    // Parallel systems
                    foreach (var idx in group.ParallelIndices)
                    {
                        DrawSystemRow(idx, false);
                    }

                    EditorGUILayout.EndVertical();

                    // Separator arrow between groups
                    if (g < _groups.Length - 1)
                    {
                        var sepRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                        var center = new Vector2(sepRect.x + sepRect.width * 0.5f, sepRect.y + sepRect.height * 0.5f);
                        var prevColor = GUI.color;
                        GUI.color = ColorSeparator;
                        Handles.color = ColorSeparator;
                        Handles.DrawLine(new Vector3(center.x, sepRect.y + 2, 0), new Vector3(center.x, sepRect.yMax - 2, 0));
                        Handles.DrawAAConvexPolygon(
                            new Vector3(center.x - 4, sepRect.yMax - 6, 0),
                            new Vector3(center.x + 4, sepRect.yMax - 6, 0),
                            new Vector3(center.x, sepRect.yMax, 0));
                        GUI.color = prevColor;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSystemRow(int nodeIndex, bool isMain)
        {
            var node = _nodes[nodeIndex];
            var modeColor = GetThreadModeColor(node.ThreadMode);
            var isSelected = nodeIndex == _selectedSystemIndex;

            var rect = EditorGUILayout.GetControlRect(false, 18);

            if (isSelected)
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.4f, 0.7f, 0.5f));

            // Indent
            var indentRect = new Rect(rect.x + 16, rect.y, rect.width - 16, rect.height);

            // Thread mode dot
            var dotRect = new Rect(indentRect.x, indentRect.y + 5, 8, 8);
            EditorGUI.DrawRect(dotRect, modeColor);

            // Name
            var nameRect = new Rect(indentRect.x + 14, indentRect.y, indentRect.width - 100, indentRect.height);
            if (GUI.Button(nameRect, $"{node.Name}  [{nodeIndex}]", EditorStyles.label))
            {
                _selectedSystemIndex = nodeIndex;
            }

            // Mode label
            var modeRect = new Rect(indentRect.xMax - 82, indentRect.y, 80, indentRect.height);
            var prevColor = GUI.color;
            GUI.color = modeColor;
            GUI.Label(modeRect, isMain ? "Main" : "Parallel", EditorStyles.miniLabel);
            GUI.color = prevColor;
        }

        private void DrawBottomPanel()
        {
            if (_nodes == null || _selectedSystemIndex < 0 || _selectedSystemIndex >= _nodes.Length) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("System Details", EditorStyles.boldLabel);

            var node = _nodes[_selectedSystemIndex];
            var info = node.Info;

            _bottomScroll = EditorGUILayout.BeginScrollView(_bottomScroll, GUILayout.ExpandHeight(true));

            // Header line: Name + Mode + ECB
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name:", EditorStyles.boldLabel, GUILayout.Width(45));
            EditorGUILayout.LabelField(node.Name);
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Mode:", EditorStyles.boldLabel, GUILayout.Width(45));
            var prevColor = GUI.color;
            GUI.color = GetThreadModeColor(node.ThreadMode);
            EditorGUILayout.LabelField(node.ThreadMode.ToString());
            GUI.color = prevColor;
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("ECB:", EditorStyles.boldLabel, GUILayout.Width(35));
            EditorGUILayout.LabelField(info.UsesECB ? "Yes" : "No");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Components
            if (info.Components != null && info.Components.Length > 0)
            {
                EditorGUILayout.LabelField("Components:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                for (int i = 0; i < info.Components.Length; i++)
                {
                    var c = info.Components[i];
                    var typeName = GetComponentName(c.ComponentTypeIndex);
                    var modeStr = c.Mode switch
                    {
                        SystemAccessMode.Read => "R",
                        SystemAccessMode.Write => "W",
                        SystemAccessMode.ReadWrite => "RW",
                        _ => "?"
                    };
                    var modeColor = c.Mode switch
                    {
                        SystemAccessMode.Read => ColorParallel,
                        SystemAccessMode.Write => ColorMain,
                        SystemAccessMode.ReadWrite => ColorSingle,
                        _ => Color.white
                    };
                    EditorGUILayout.BeginHorizontal();
                    prevColor = GUI.color;
                    GUI.color = modeColor;
                    EditorGUILayout.LabelField(modeStr, EditorStyles.miniBoldLabel, GUILayout.Width(26));
                    GUI.color = prevColor;
                    EditorGUILayout.LabelField(typeName);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            // Resources
            if (info.ReadResources != null && info.ReadResources.Length > 0)
            {
                EditorGUILayout.LabelField("Read Resources:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var r in info.ReadResources)
                    EditorGUILayout.LabelField($"idx: {r}");
                EditorGUI.indentLevel--;
            }
            if (info.WriteResources != null && info.WriteResources.Length > 0)
            {
                EditorGUILayout.LabelField("Write Resources:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var r in info.WriteResources)
                    EditorGUILayout.LabelField($"idx: {r}");
                EditorGUI.indentLevel--;
            }

            // Events
            if (info.ReadEvents != null && info.ReadEvents.Length > 0)
            {
                EditorGUILayout.LabelField("Read Events:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var e in info.ReadEvents)
                    EditorGUILayout.LabelField($"idx: {e}");
                EditorGUI.indentLevel--;
            }
            if (info.WriteEvents != null && info.WriteEvents.Length > 0)
            {
                EditorGUILayout.LabelField("Write Events:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var e in info.WriteEvents)
                    EditorGUILayout.LabelField($"idx: {e}");
                EditorGUI.indentLevel--;
            }

            // Find group for selected system
            var selectedGroup = -1;
            if (_groups != null)
            {
                for (int g = 0; g < _groups.Length; g++)
                {
                    foreach (var idx in _groups[g].MainIndices) { if (idx == _selectedSystemIndex) selectedGroup = g; }
                    foreach (var idx in _groups[g].ParallelIndices) { if (idx == _selectedSystemIndex) selectedGroup = g; }
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Dependencies:", EditorStyles.boldLabel);

            // Predecessors: systems this one depends on
            var preds = _graph?.GetPredecessors();
            if (preds != null && _selectedSystemIndex < preds.Length)
            {
                var myPreds = preds[_selectedSystemIndex];
                if (myPreds != null && myPreds.Length > 0)
                {
                    EditorGUI.indentLevel++;
                    for (int p = 0; p < myPreds.Length; p++)
                    {
                        var predNode = _nodes[myPreds[p]];
                        var predGroup = -1;
                        for (int g = 0; g < _groups.Length; g++)
                        {
                            foreach (var idx in _groups[g].MainIndices) { if (idx == myPreds[p]) predGroup = g; }
                            foreach (var idx in _groups[g].ParallelIndices) { if (idx == myPreds[p]) predGroup = g; }
                        }

                        var prevC = GUI.color;
                        GUI.color = ColorDepEdge;
                        EditorGUILayout.LabelField($"◄  {predNode.Name}  [group {predGroup}, {predNode.ThreadMode}]");
                        GUI.color = prevC;
                    }
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField("  (none — root system)");
                }
            }

            // Successors: systems that depend on this one
            var succs = _graph?.GetSuccessors();
            if (succs != null && _selectedSystemIndex < succs.Length)
            {
                var mySuccs = succs[_selectedSystemIndex];
                if (mySuccs != null && mySuccs.Length > 0)
                {
                    EditorGUI.indentLevel++;
                    for (int s = 0; s < mySuccs.Length; s++)
                    {
                        var succNode = _nodes[mySuccs[s]];
                        var succGroup = -1;
                        for (int g = 0; g < _groups.Length; g++)
                        {
                            foreach (var idx in _groups[g].MainIndices) { if (idx == mySuccs[s]) succGroup = g; }
                            foreach (var idx in _groups[g].ParallelIndices) { if (idx == mySuccs[s]) succGroup = g; }
                        }

                        var prevC = GUI.color;
                        GUI.color = ColorDepByEdge;
                        EditorGUILayout.LabelField($"►  {succNode.Name}  [group {succGroup}, {succNode.ThreadMode}]");
                        GUI.color = prevC;
                    }
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField("  (none — leaf system)");
                }
            }

            if (selectedGroup >= 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Group: {selectedGroup}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ExportDependencyData()
        {
            if (_graph == null || _nodes == null) return;

            var path = EditorUtility.SaveFilePanel("Export Dependency Graph", Application.dataPath, "dependency_graph.txt", "txt");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();
            var preds = _graph.GetPredecessors();
            var succs = _graph.GetSuccessors();
            var groups = _graph.GetPrecomputedGroups();

            sb.AppendLine("=== DEPENDENCY GRAPH EXPORT ===");
            sb.AppendLine($"World: {_worldIds[_selectedWorldIndex]}");
            sb.AppendLine($"Nodes: {_graph.NodeCount}");
            sb.AppendLine($"Groups: {_graph.GroupCount}");
            sb.AppendLine($"Schedule Mode: {_systems.GetGroupScheduleMode()}");
            sb.AppendLine($"Cyclic: {_graph.HasCyclicDependency()}");
            sb.AppendLine();

            // Group structure
            sb.AppendLine("--- EXECUTION GROUPS ---");
            sb.AppendLine("(Ordered: lower group = executes first)");
            sb.AppendLine();
            for (int g = 0; g < groups.Length; g++)
            {
                ref var group = ref groups[g];
                sb.AppendLine($"GROUP {g}:");
                if (group.MainIndices.Length > 0)
                    sb.AppendLine($"  Main: {string.Join(", ", System.Array.ConvertAll(group.MainIndices, i => _nodes[i].Name))}");
                if (group.ParallelIndices.Length > 0)
                    sb.AppendLine($"  Parallel: {string.Join(", ", System.Array.ConvertAll(group.ParallelIndices, i => _nodes[i].Name))}");
                sb.AppendLine($"  HasECB: {group.HasECB}");
                sb.AppendLine();
            }

            // Per-system details
            sb.AppendLine("--- SYSTEM DETAILS ---");
            sb.AppendLine();
            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                var info = node.Info;

                var groupIdx = -1;
                for (int g = 0; g < groups.Length; g++)
                {
                    foreach (var idx in groups[g].MainIndices) { if (idx == i) groupIdx = g; }
                    foreach (var idx in groups[g].ParallelIndices) { if (idx == i) groupIdx = g; }
                }

                sb.AppendLine($"  [{i}] {node.Name}");
                sb.AppendLine($"    ThreadMode: {node.ThreadMode}");
                sb.AppendLine($"    Group: {groupIdx}");
                sb.AppendLine($"    UsesECB: {info.UsesECB}");

                if (info.Components != null && info.Components.Length > 0)
                {
                    sb.AppendLine("    Components:");
                    for (int c = 0; c < info.Components.Length; c++)
                    {
                        var comp = info.Components[c];
                        var typeName = GetComponentName(comp.ComponentTypeIndex);
                        sb.AppendLine($"      {comp.Mode}  {typeName}");
                    }
                }

                if (info.ReadResources != null && info.ReadResources.Length > 0)
                    sb.AppendLine($"    ReadResources: [{string.Join(", ", info.ReadResources)}]");
                if (info.WriteResources != null && info.WriteResources.Length > 0)
                    sb.AppendLine($"    WriteResources: [{string.Join(", ", info.WriteResources)}]");
                if (info.ReadEvents != null && info.ReadEvents.Length > 0)
                    sb.AppendLine($"    ReadEvents: [{string.Join(", ", info.ReadEvents)}]");
                if (info.WriteEvents != null && info.WriteEvents.Length > 0)
                    sb.AppendLine($"    WriteEvents: [{string.Join(", ", info.WriteEvents)}]");

                // Predecessors
                if (preds != null && i < preds.Length && preds[i] != null && preds[i].Length > 0)
                {
                    sb.AppendLine("    DependsOn:");
                    foreach (var p in preds[i])
                        sb.AppendLine($"      [{p}] {_nodes[p].Name}");
                }

                // Successors
                if (succs != null && i < succs.Length && succs[i] != null && succs[i].Length > 0)
                {
                    sb.AppendLine("    DependedBy:");
                    foreach (var s in succs[i])
                        sb.AppendLine($"      [{s}] {_nodes[s].Name}");
                }

                sb.AppendLine();
            }

            // Dependency chains (longest paths)
            sb.AppendLine("--- DEPENDENCY CHAINS ---");
            sb.AppendLine("(Longest paths through the graph — these determine total serial time)");
            sb.AppendLine();
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (preds != null && i < preds.Length && (preds[i] == null || preds[i].Length == 0))
                {
                    sb.Append($"  CHAIN: [{i}] {_nodes[i].Name}");
                    TraverseChain(sb, i, succs, new HashSet<int>());
                    sb.AppendLine();
                }
            }

            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"Dependency graph exported to: {path}");
        }

        private void TraverseChain(System.Text.StringBuilder sb, int idx, int[][] succs, HashSet<int> visited)
        {
            if (succs == null || idx >= succs.Length || succs[idx] == null || succs[idx].Length == 0)
                return;

            foreach (var s in succs[idx])
            {
                if (visited.Contains(s)) continue;
                visited.Add(s);
                sb.Append($" → [{s}] {_nodes[s].Name}");
                TraverseChain(sb, s, succs, visited);
            }
        }

        private static string GetComponentName(int index)
        {
            try
            {
                return ComponentTypeMap.GetType(index).Name;
            }
            catch
            {
                return $"Component_{index}";
            }
        }

        private unsafe void RefreshWorldList()
        {
            if (EditorApplication.timeSinceStartup - _lastRefreshTime < REFRESH_INTERVAL) return;
            _lastRefreshTime = EditorApplication.timeSinceStartup;

            _worldLabels.Clear();
            _worldIds.Clear();

            try
            {
                for (int i = 0; i < World.WorldCapacity; i++)
                {
                    ref var w = ref World.Get(i);
                    if (!w.IsAlive) continue;
                    var ptr = w.UnsafeWorld;
                    if (ptr == null) continue;
                    var worldId = ptr->Id;
                    var systemsList = WorldSystems.GetAll(worldId);
                    if (systemsList == null) continue;
                    for (int s = 0; s < systemsList.Count; s++)
                    {
                        _worldLabels.Add($"World {worldId} / Systems {s}");
                        _worldIds.Add(worldId);
                    }
                }
            }
            catch
            {
                // World list may be in an invalid state during domain reload
            }

            if (_worldLabels.Count == 0)
            {
                _systems = null;
                _graph = null;
                _nodes = null;
                _groups = null;
            }
            else if (_selectedWorldIndex >= _worldLabels.Count)
            {
                _selectedWorldIndex = 0;
                TryResolveSystems();
            }
        }

        private void TryResolveSystems()
        {
            _graph = null;
            _nodes = null;
            _groups = null;
            _systems = null;

            if (_selectedWorldIndex < 0 || _selectedWorldIndex >= _worldIds.Count) return;

            var worldId = _worldIds[_selectedWorldIndex];
            var systemsList = WorldSystems.GetAll(worldId);
            if (systemsList == null || systemsList.Count == 0) return;

            _systems = systemsList[0];
            _graph = _systems.DependencyGraph;
            if (_graph == null) return;

            _nodes = _graph.Nodes;
            _groups = _graph.GetPrecomputedGroups();
        }

        private static Color GetThreadModeColor(Threads mode)
        {
            return mode switch
            {
                Threads.Main => ColorMain,
                Threads.MainRun => ColorMainRun,
                Threads.Parallel => ColorParallel,
                Threads.Single => ColorSingle,
                _ => Color.white
            };
        }
    }
}
#endif
