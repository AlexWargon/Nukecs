using UnityEditor;
using UnityEngine;

namespace Wargon.Nukecs
{
    public unsafe class MemoryAllocatorDebugWindow : EditorWindow
    {
        private SerializableMemoryAllocator* allocator;

        // Configurable column widths
        private readonly float[] columnWidths = { 50f, 100f, 100f, 50f };
        private Color freeColor = new(1f, 0.8f, 0.8f);
        private Vector2 mouseStartPosition;
        private readonly float resizeHandleWidth = 5f;

        // State for resizing columns
        private int resizingColumnIndex = -1;

        private Vector2 scrollPosition;

        // Background colors
        private Color usedColor = new(0.8f, 1f, 0.8f);

        private void OnEnable()
        {
            // Load preferences
            for (var i = 0; i < columnWidths.Length; i++)
                columnWidths[i] = EditorPrefs.GetFloat($"AllocatorDebug_ColumnWidth_{i}", columnWidths[i]);

            usedColor = LoadColor("AllocatorDebug_UsedColor", usedColor);
            freeColor = LoadColor("AllocatorDebug_FreeColor", freeColor);
        }

        private void OnDisable()
        {
            // Save preferences
            for (var i = 0; i < columnWidths.Length; i++)
                EditorPrefs.SetFloat($"AllocatorDebug_ColumnWidth_{i}", columnWidths[i]);

            SaveColor("AllocatorDebug_UsedColor", usedColor);
            SaveColor("AllocatorDebug_FreeColor", freeColor);
        }

        private void OnGUI()
        {
            if (!World.HasActiveWorlds()) return;
            ref var world = ref World.Default;

            if (allocator == null)
            {
                EditorGUILayout.HelpBox("No allocator found. Please assign one to debug.", MessageType.Warning);
                if (GUILayout.Button("Initialize Allocator (Test)"))
                    allocator = world.UnsafeWorld->AllocatorHandler.AllocatorWrapper.GetAllocatorPtr();
                return;
            }

            // Display memory overview
            EditorGUILayout.LabelField("Memory Allocator Debug View", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Blocks: {allocator->BlockCount}");

            // Background color configuration
            EditorGUILayout.LabelField("Background Colors", EditorStyles.boldLabel);
            usedColor = EditorGUILayout.ColorField("Used Block Color", usedColor);
            freeColor = EditorGUILayout.ColorField("Free Block Color", freeColor);

            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Display table header
            EditorGUILayout.BeginHorizontal();

            for (var i = 0; i < columnWidths.Length; i++)
            {
                GUILayout.Label(GetColumnTitle(i), GUILayout.Width(columnWidths[i]));

                // Draw the resize handle (visual separator)
                if (i < columnWidths.Length - 1) // No separator for the last column
                    DrawResizeHandle(i);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); // Separator

            // Display table rows
            for (var i = 0; i < allocator->BlockCount; i++)
            {
                var block = allocator->Blocks[i];
                var backgroundColor = block.IsUsed ? usedColor : freeColor;

                // Set background color for the row
                var originalBackgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = backgroundColor;

                EditorGUILayout.BeginHorizontal("box");

                for (var j = 0; j < columnWidths.Length; j++)
                {
                    DrawColumnContent(i, j, block);
                    if (j < columnWidths.Length - 1)
                        DrawSeparator(); // Visual separator between columns
                }

                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = originalBackgroundColor;
            }

            EditorGUILayout.EndScrollView();

            HandleColumnResizing();
        }

        private void DrawResizeHandle(int columnIndex)
        {
            var rect = GUILayoutUtility.GetRect(resizeHandleWidth, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(resizeHandleWidth));

            EditorGUI.DrawRect(rect, Color.gray); // Visualize the separator

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                resizingColumnIndex = columnIndex;
                mouseStartPosition = Event.current.mousePosition;
                Event.current.Use();
            }
        }

        private void DrawColumnContent(int rowIndex, int columnIndex, SerializableMemoryAllocator.MemoryBlock block)
        {
            switch (columnIndex)
            {
                case 0:
                    EditorGUILayout.LabelField(rowIndex.ToString(), GUILayout.Width(columnWidths[columnIndex]));
                    break;
                case 1:
                    EditorGUILayout.LabelField($"0x{(int)(block.Pointer + allocator->BasePtr):X}",
                        GUILayout.Width(columnWidths[columnIndex]));
                    break;
                case 2:
                    EditorGUILayout.LabelField(block.Size.ToString(), GUILayout.Width(columnWidths[columnIndex]));
                    break;
                case 3:
                    EditorGUILayout.LabelField(block.IsUsed ? "Used" : "Free",
                        GUILayout.Width(columnWidths[columnIndex]));
                    break;
            }
        }

        private void HandleColumnResizing()
        {
            if (resizingColumnIndex >= 0)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    var delta = Event.current.mousePosition.x - mouseStartPosition.x;
                    columnWidths[resizingColumnIndex] = Mathf.Max(30f, columnWidths[resizingColumnIndex] + delta);
                    mouseStartPosition = Event.current.mousePosition;
                    Repaint();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    resizingColumnIndex = -1;
                }
            }
        }

        private string GetColumnTitle(int columnIndex)
        {
            switch (columnIndex)
            {
                case 0: return "Index";
                case 1: return "Pointer";
                case 2: return "Size (bytes)";
                case 3: return "Status";
                default: return "";
            }
        }

        private void DrawSeparator()
        {
            GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));
        }

        [MenuItem("Tools/Memory Allocator Debug")]
        public static void ShowWindow()
        {
            var window = GetWindow<MemoryAllocatorDebugWindow>("Memory Allocator Debug");
            window.Show();
        }

        // Helper to save/load colors from EditorPrefs
        private void SaveColor(string key, Color color)
        {
            EditorPrefs.SetFloat($"{key}_R", color.r);
            EditorPrefs.SetFloat($"{key}_G", color.g);
            EditorPrefs.SetFloat($"{key}_B", color.b);
            EditorPrefs.SetFloat($"{key}_A", color.a);
        }

        private Color LoadColor(string key, Color defaultColor)
        {
            return new Color(
                EditorPrefs.GetFloat($"{key}_R", defaultColor.r),
                EditorPrefs.GetFloat($"{key}_G", defaultColor.g),
                EditorPrefs.GetFloat($"{key}_B", defaultColor.b),
                EditorPrefs.GetFloat($"{key}_A", defaultColor.a)
            );
        }
    }
}