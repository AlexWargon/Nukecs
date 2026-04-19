#if UNITY_EDITOR && NUKECS_DEBUG
namespace Wargon.Nukecs.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public unsafe class ECSMemoryProfilerWindow : EditorWindow
    {
        private const float KB = 1024f;
        private const float MB = 1024f * 1024f;

        private byte _worldId;
        private World _world;
        private Vector2 _scrollPos;
        private int _lastPoolCount = -1;
        private bool _autoRefresh = true;
        private float _refreshInterval = 0.02f;
        private double _lastRefreshTime;
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private readonly List<PoolMemoryInfo> _poolInfos = new();
        private readonly List<WorldMemoryInfo> _worldInfos = new();

        private struct PoolMemoryInfo
        {
            public int typeIndex;
            public string typeName;
            public int componentSize;
            public int chunksCount;
            public int chunksCapacity;
            public long allocatedBytes;
            public long usedBytes;
            public int entitiesUsing;
            public bool isTag;
            public bool isArray;
            public bool isCreated;
        }

        private struct WorldMemoryInfo
        {
            public byte worldId;
            public long allocatorTotal;
            public long allocatorUsed;
            public long allocatorFree;
            public int entitiesCount;
            public int archetypesCount;
            public int queriesCount;
            public int poolsCreated;
            public long entitiesMemory;
            public long archetypesMemory;
            public long queriesMemory;
        }

        private bool _editorUpdateRegistered;

        [MenuItem("Nuke.cs/Memory Profiler")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<ECSMemoryProfilerWindow>();
            wnd.titleContent = new GUIContent("ECS Memory Profiler");
            wnd.minSize = new Vector2(420, 400);
            wnd.autoRepaintOnSceneChange = true;
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            _editorUpdateRegistered = true;
        }

        private void OnDisable()
        {
            if (_editorUpdateRegistered)
            {
                EditorApplication.update -= OnEditorUpdate;
                _editorUpdateRegistered = false;
            }
        }

        private void OnEditorUpdate()
        {
            if (!_autoRefresh || !Application.isPlaying) return;
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastRefreshTime >= _refreshInterval)
            {
                _lastRefreshTime = now;
                _lastPoolCount = -1;
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see memory stats.", MessageType.Info);
                return;
            }

            _world = World.Get(_worldId);
            if (!_world.IsAlive)
            {
                EditorGUILayout.HelpBox($"World {_worldId} is not alive.", MessageType.Warning);
                return;
            }

            DrawToolbar();
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            CollectWorldInfo();
            DrawWorldOverview();

            EditorGUILayout.Space(4);
            DrawPoolsOverview();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("World:", GUILayout.Width(45));
            var newId = (byte)EditorGUILayout.IntField(_worldId, GUILayout.Width(30));
            if (newId != _worldId)
            {
                _worldId = newId;
                _lastPoolCount = -1;
            }

            _autoRefresh = EditorGUILayout.ToggleLeft("Auto", _autoRefresh, GUILayout.Width(60));

            if (!_autoRefresh)
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    _lastPoolCount = -1;
                    Repaint();
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Force GC", EditorStyles.toolbarButton, GUILayout.Width(65)))
            {
                GC.Collect();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CollectWorldInfo()
        {
            _worldInfos.Clear();

            var w = World.Get(_worldId);
            if (!w.IsAlive) return;

            var info = new WorldMemoryInfo
            {
                worldId = _worldId,
                allocatorTotal = w.UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.TotalSize,
                allocatorUsed = w.UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.MemoryUsed,
                allocatorFree = w.UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.MemoryLeft,
                entitiesCount = w.UnsafeWorld->entitiesAmount,
                archetypesCount = w.UnsafeWorld->archetypesList.Length,
                queriesCount = w.UnsafeWorld->queries.Length,
                poolsCreated = w.UnsafeWorld->poolsCount,
                entitiesMemory = (long)w.UnsafeWorld->entities.Length * sizeof(Entity)
                                 + (long)w.UnsafeWorld->entitiesArchetypes.Length * sizeof(int),
                archetypesMemory = (long)w.UnsafeWorld->archetypesList.Length * System.Runtime.InteropServices.Marshal.SizeOf<ptr<ArchetypeUnsafe>>(),
                queriesMemory = (long)w.UnsafeWorld->queries.Length * System.Runtime.InteropServices.Marshal.SizeOf<ptr<QueryUnsafe>>()
            };

            _worldInfos.Add(info);
        }

        private void DrawWorldOverview()
        {
            if (_worldInfos.Count == 0) return;
            var info = _worldInfos[0];

            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.LabelField("World Overview", EditorStyles.boldLabel);

            DrawMemoryBar("Allocator", info.allocatorUsed, info.allocatorTotal);

            DrawInfoRow("Total", FormatBytes(info.allocatorTotal));
            DrawInfoRow("Used", FormatBytes(info.allocatorUsed));
            DrawInfoRow("Free", FormatBytes(info.allocatorFree));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Entities", EditorStyles.boldLabel);
            DrawInfoRow("Alive Entities", info.entitiesCount.ToString());
            DrawInfoRow("Entities Memory", FormatBytes(info.entitiesMemory));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Archetypes / Queries", EditorStyles.boldLabel);
            DrawInfoRow("Archetypes", info.archetypesCount.ToString());
            DrawInfoRow("Archetypes Memory", FormatBytes(info.archetypesMemory));
            DrawInfoRow("Queries", info.queriesCount.ToString());
            DrawInfoRow("Queries Memory", FormatBytes(info.queriesMemory));
            DrawInfoRow("Pools Created", info.poolsCreated.ToString());

            EditorGUILayout.EndVertical();
        }

        private void DrawPoolsOverview()
        {
            CollectPoolInfos();

            EditorGUILayout.BeginVertical("HelpBox");

            var headerRect = EditorGUILayout.GetControlRect();
            headerRect.x += 16f;
            headerRect.width -= 16f;
            var totalWidth = headerRect.width;
            var typeCol = new Rect(headerRect.x, headerRect.y, totalWidth * 0.30f, headerRect.height);
            var sizeCol = new Rect(typeCol.xMax + 2, headerRect.y, totalWidth * 0.12f, headerRect.height);
            var allocCol = new Rect(sizeCol.xMax + 2, headerRect.y, totalWidth * 0.14f, headerRect.height);
            var usedCol = new Rect(allocCol.xMax + 2, headerRect.y, totalWidth * 0.14f, headerRect.height);
            var chunksCol = new Rect(usedCol.xMax + 2, headerRect.y, totalWidth * 0.14f, headerRect.height);
            var barCol = new Rect(chunksCol.xMax + 2, headerRect.y, totalWidth * 0.14f, headerRect.height);

            EditorGUI.LabelField(typeCol, "Type", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(sizeCol, "Size", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(allocCol, "Allocated", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(usedCol, "Used", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(chunksCol, "Chunks", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(barCol, "Fill", EditorStyles.miniBoldLabel);

            long totalAllocated = 0;
            long totalUsed = 0;

            foreach (var pool in _poolInfos)
            {
                if (!pool.isCreated) continue;

                totalAllocated += pool.allocatedBytes;
                totalUsed += pool.usedBytes;

                var key = pool.typeName;
                if (!_foldoutStates.TryGetValue(key, out var open))
                {
                    open = false;
                    _foldoutStates[key] = open;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var foldoutRect = EditorGUILayout.GetControlRect();
                var indent = 16f;
                var foRect = new Rect(foldoutRect.x, foldoutRect.y, 14f, foldoutRect.height);
                var rowRect = new Rect(foldoutRect.x + indent, foldoutRect.y, foldoutRect.width - indent, foldoutRect.height);
                var totalW = rowRect.width;

                open = EditorGUI.Foldout(foRect, open, GUIContent.none);
                _foldoutStates[key] = open;

                var rType = new Rect(rowRect.x, rowRect.y, totalW * 0.30f, rowRect.height);
                var rSize = new Rect(rType.xMax + 2, rowRect.y, totalW * 0.12f, rowRect.height);
                var rAlloc = new Rect(rSize.xMax + 2, rowRect.y, totalW * 0.14f, rowRect.height);
                var rUsed = new Rect(rAlloc.xMax + 2, rowRect.y, totalW * 0.14f, rowRect.height);
                var rChunks = new Rect(rUsed.xMax + 2, rowRect.y, totalW * 0.14f, rowRect.height);
                var rBar = new Rect(rChunks.xMax + 2, rowRect.y, totalW * 0.14f, rowRect.height);

                var typeStyle = pool.isTag
                    ? new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Italic }
                    : EditorStyles.label;

                var label = pool.isTag
                    ? $"[T] {pool.typeName}"
                    : pool.isArray
                        ? $"[A] {pool.typeName}"
                        : pool.typeName;

                EditorGUI.LabelField(rType, label, typeStyle);

                if (pool.isTag)
                {
                    EditorGUI.LabelField(rSize, "tag");
                    EditorGUI.LabelField(rAlloc, "-");
                    EditorGUI.LabelField(rUsed, "-");
                    EditorGUI.LabelField(rChunks, "-");
                }
                else
                {
                    EditorGUI.LabelField(rSize, FormatBytes(pool.componentSize));
                    EditorGUI.LabelField(rAlloc, FormatBytes(pool.allocatedBytes));
                    EditorGUI.LabelField(rUsed, FormatBytes(pool.usedBytes));
                    EditorGUI.LabelField(rChunks, $"{pool.chunksCount}");

                    if (pool.allocatedBytes > 0)
                    {
                        var fill = (float)pool.usedBytes / pool.allocatedBytes;
                        DrawMiniBar(rBar, fill);
                    }
                }

                if (open && !pool.isTag)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Component Size", $"{pool.componentSize} bytes");
                    EditorGUILayout.LabelField("Chunk Count", pool.chunksCount.ToString());
                    EditorGUILayout.LabelField("Chunk Capacity", pool.chunksCapacity.ToString());
                    EditorGUILayout.LabelField("Allocated", FormatBytes(pool.allocatedBytes));
                    EditorGUILayout.LabelField("Used", FormatBytes(pool.usedBytes));
                    EditorGUILayout.LabelField("Free (reserved)", FormatBytes(pool.allocatedBytes - pool.usedBytes));

                    if (pool.componentSize > 0)
                    {
                        var maxEntities = pool.chunksCapacity * Chunk.MAX_CHUNK_SIZE;
                        var fillPct = maxEntities > 0 ? (float)pool.entitiesUsing / maxEntities * 100 : 0;
                        EditorGUILayout.LabelField("Max Entities", maxEntities.ToString());
                        EditorGUILayout.LabelField("Entities Using", pool.entitiesUsing.ToString());
                        EditorGUILayout.LabelField("Slot Fill", $"{fillPct:F1}%");
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Total Pools Memory:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Allocated: {FormatBytes(totalAllocated)}  |  Used: {FormatBytes(totalUsed)}  |  Free (reserved): {FormatBytes(totalAllocated - totalUsed)}");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CollectPoolInfos()
        {
            var newCount = 0;
            try
            {
                newCount = _world.UnsafeWorld->poolsCount;
            }
            catch
            {
                return;
            }

            if (newCount == _lastPoolCount && _poolInfos.Count > 0)
                return;

            _lastPoolCount = newCount;
            _poolInfos.Clear();

            var indexes = ComponentTypeMap.TypesIndexes;
            if (indexes == null) return;

            var poolsPtr = _world.UnsafeWorld->pools;

            foreach (var typeIndex in indexes)
            {
                if (typeIndex >= poolsPtr.Capacity) continue;

                ref var pool = ref poolsPtr.Ptr[typeIndex];
                if (!pool.IsCreated) continue;

                ComponentTypeData typeData;
                try
                {
                    typeData = ComponentTypeMap.GetComponentType(typeIndex);
                }
                catch
                {
                    continue;
                }

                var typeName = "Unknown";
                try
                {
                    var t = ComponentTypeMap.GetType(typeIndex);
                    if (t != null) typeName = t.Name;
                }
                catch { }

                var chunksCount = 0;
                var chunksCapacity = 0;
                long allocatedBytes = 0;
                long usedBytes = 0;
                var entitiesUsing = 0;

                if (!typeData.isTag && pool.UnsafeBuffer != null)
                {
                    var untyped = pool.UnsafeBuffer;
                    chunksCapacity = untyped->Chunks.Capacity;
                    var itemSize = typeData.size;

                    if (typeData.IsArrayElement)
                    {
                        itemSize *= ComponentArray.DEFAULT_MAX_CAPACITY;
                    }

                    for (var i = 0; i < untyped->Chunks.Capacity; i++)
                    {
                        ref var chunk = ref untyped->Chunks.ElementAt(i);
                        if (chunk.IsCreated)
                        {
                            chunksCount++;
                            var chunkByteSize = Chunk.MAX_CHUNK_SIZE * itemSize;
                            allocatedBytes += chunkByteSize;
                        }
                    }

                    entitiesUsing = CountEntitiesUsingPool(typeIndex);
                    usedBytes = (long)entitiesUsing * itemSize;
                }

                _poolInfos.Add(new PoolMemoryInfo
                {
                    typeIndex = typeIndex,
                    typeName = typeName,
                    componentSize = typeData.size,
                    chunksCount = chunksCount,
                    chunksCapacity = chunksCapacity,
                    allocatedBytes = allocatedBytes,
                    usedBytes = usedBytes,
                    entitiesUsing = entitiesUsing,
                    isTag = typeData.isTag,
                    isArray = typeData.isArray,
                    isCreated = true
                });
            }

            _poolInfos.Sort((a, b) => b.allocatedBytes.CompareTo(a.allocatedBytes));
        }

        private int CountEntitiesUsingPool(int typeIndex)
        {
            var count = 0;
            var archetypesList = _world.UnsafeWorld->archetypesList;
            for (var i = 0; i < archetypesList.Length; i++)
            {
                ref var archPtr = ref archetypesList.Ptr[i];
                ref var arch = ref archPtr.Ref;
                if (!arch.Has(typeIndex)) continue;

                var entitiesList = _world.UnsafeWorld->entities;
                var entitiesArchetypes = _world.UnsafeWorld->entitiesArchetypes;

                for (var e = 1; e < _world.UnsafeWorld->lastEntityIndex; e++)
                {
                    if (entitiesArchetypes.Ptr[e] == arch.index)
                        count++;
                }
            }
            return count;
        }

        private static void DrawInfoRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(140));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawMemoryBar(string label, long used, long total)
        {
            var rect = EditorGUILayout.GetControlRect(false, 22f);
            var fill = total > 0 ? (float)used / total : 0f;
            fill = Mathf.Clamp01(fill);

            var labelRect = new Rect(rect.x, rect.y, 80, rect.height);
            var barRect = new Rect(rect.x + 82, rect.y + 2, rect.width - 82 - 80, rect.height - 4);
            var valueRect = new Rect(barRect.xMax + 4, rect.y, 76, rect.height);

            EditorGUI.LabelField(labelRect, label);

            var bgColor = fill > 0.9f
                ? new Color(0.8f, 0.2f, 0.2f)
                : fill > 0.7f
                    ? new Color(0.8f, 0.6f, 0.1f)
                    : new Color(0.2f, 0.6f, 0.3f);

            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
            var fillRect = new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height);
            EditorGUI.DrawRect(fillRect, bgColor);
            EditorGUI.LabelField(valueRect, $"{fill * 100:F1}%");
        }

        private static void DrawMiniBar(Rect rect, float fill)
        {
            fill = Mathf.Clamp01(fill);
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            var fillRect = new Rect(rect.x, rect.y, rect.width * fill, rect.height);

            var color = fill > 0.9f
                ? new Color(0.8f, 0.2f, 0.2f)
                : fill > 0.7f
                    ? new Color(0.8f, 0.6f, 0.1f)
                    : new Color(0.2f, 0.6f, 0.3f);

            EditorGUI.DrawRect(fillRect, color);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "0 B";
            if (bytes >= MB) return $"{bytes / MB:F2} MB";
            if (bytes >= KB) return $"{bytes / KB:F1} KB";
            return $"{bytes} B";
        }
    }
}
#endif
