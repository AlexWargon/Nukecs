#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public class ECSHistoryConsole : EditorWindow
    {
        private struct LogEntry
        {
            public string text;
            public Color color;
        }

        private ListView _listView;
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private int _lastReadTotalCount = 0;
        private const int MAX_LOG_ENTRIES = 512;
        private Font _font;

        [MenuItem("Nuke.cs/ECS History Console")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<ECSHistoryConsole>("ECS History");
            wnd.minSize = new Vector2(600, 400);
        }

        private void CreateGUI()
        {
            rootVisualElement.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            _font = EditorGUIUtility.Load("Fonts/Consolas.ttf") as Font;
            _listView = new ListView(_logEntries, itemHeight: 20, makeItem: MakeItem, bindItem: BindItem)
            {
                selectionType = SelectionType.None,
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new Color(0, 0, 0, 0),
                }
            };

            rootVisualElement.Add(_listView);

            rootVisualElement.schedule.Execute(RefreshHistory).Every(33);
        }

        private VisualElement MakeItem()
        {
            var label = new Label();
            label.style.unityFont = _font;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 14;
            label.style.whiteSpace = WhiteSpace.PreWrap;
            return label;
        }

        private void BindItem(VisualElement element, int index)
        {
            var label = (Label)element;
            var entry = _logEntries[index];
            label.text = entry.text;
            label.style.color = entry.color;
        }

        private void RefreshHistory()
        {
            var world = World.Get(0);
            if (!world.IsAlive || !EditorApplication.isPlaying)
            {
                _lastReadTotalCount = 0;
                _logEntries.Clear();
                _listView.Rebuild();
                return;
            }

#if NUKECS_DEBUG
            int capacity = world.UnsafeWorldRef.storyLog.Capacity;
            long totalCount = world.UnsafeWorldRef.GetTotalStoryLogCount();

            while (_lastReadTotalCount < totalCount)
            {
                int bufferIndex = (int)(_lastReadTotalCount % capacity);
                var change = world.UnsafeWorldRef.storyLog[bufferIndex];

                var (color, actionStr) = change.command switch
                {
                    EntityCommandBuffer.ECBCommand.Type.AddComponent
                        or EntityCommandBuffer.ECBCommand.Type.AddComponentPtr
                        or EntityCommandBuffer.ECBCommand.Type.AddComponentNoData => (new Color(0.4f, 1f, 0.4f), "Added"),
                    EntityCommandBuffer.ECBCommand.Type.RemoveComponent => (new Color(1f, 0.65f, 0f), "Removed"), // оранжевый
                    EntityCommandBuffer.ECBCommand.Type.Copy => (new Color(0.4f, 0.6f, 1f), "Copied"),           // синий
                    EntityCommandBuffer.ECBCommand.Type.DestroyEntity => (new Color(1f, 0.3f, 0.3f), "Destroyed"), // красный
                    _ => (Color.white, string.Empty)
                };

                if (actionStr != null)
                {
                    var compName = change.componentTypeIndex != 0 ? GetComponentNameByTypeIndex(change.componentTypeIndex) : string.Empty;
                    var timeStr = FormatTimestamp(change.timeStamp);
                    var line = $"[{timeStr}] → {actionStr} {compName} on e:{change.entityId}";

                    _logEntries.Add(new LogEntry { text = line, color = color });

                    if (_logEntries.Count > MAX_LOG_ENTRIES)
                        _logEntries.RemoveAt(0);
                }

                _lastReadTotalCount++;
            }

            _listView.Rebuild();

            // if (_logEntries.Count > 0)
            // {
            //     _listView.ScrollToItem(_logEntries.Count - 1);
            // }
#endif
        }

        private string FormatTimestamp(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }

        private string GetComponentNameByTypeIndex(int typeIndex)
        {
            return ComponentTypeMap.GetType(typeIndex).Name;
        }
    }
}
#endif
