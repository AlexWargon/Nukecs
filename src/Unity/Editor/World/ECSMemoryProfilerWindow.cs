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
        private float _refreshInterval = 0.1f;
        private double _lastRefreshTime;
        private readonly Dictionary<int, bool> _foldoutStates = new();
        private readonly List<PoolMemoryInfo> _poolInfos = new();
        private readonly List<WorldMemoryInfo> _worldInfos = new();
        private long _totalPoolsAllocated;
        private long _poolsOverhead;

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
            public bool isElementPool;
            public int parentArrayPoolIndex;
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
            public long poolsOverhead;
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
            CollectPoolInfos();
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
                entitiesMemory = CalculateEntitiesMemory(w.UnsafeWorld),
                archetypesMemory = CalculateArchetypesMemory(w.UnsafeWorld),
                queriesMemory = CalculateQueriesMemory(w.UnsafeWorld),
                poolsOverhead = _poolsOverhead
            };

            _worldInfos.Add(info);
        }

        private static long CalculateEntitiesMemory(World.WorldUnsafe* w)
        {
            return w->entities.GetMemorySizeUsed()
                 + w->entitiesArchetypes.GetMemorySizeUsed()
                 + w->reservedEntities.GetMemorySizeUsed()
                 + w->prefabsToSpawn.GetMemorySizeUsed();
        }

        private static long CalculateArchetypesMemory(World.WorldUnsafe* w)
        {
            long total = w->archetypesList.GetMemorySizeUsed();
            total += w->archetypesMap.GetMemorySizeUsed();

            for (int i = 0; i < w->archetypesList.Length; i++)
            {
                var arch = w->archetypesList.Ptr[i].Ptr;
                total += arch->mask.GetMemorySizeUsed();
                total += arch->types.GetMemorySizeUsed();
                total += arch->queries.GetMemorySizeUsed();
                total += arch->transactions.GetMemorySizeUsed();
                total += arch->destroyEdge.addEntity.GetMemorySizeUsed();
                total += arch->destroyEdge.removeEntity.GetMemorySizeUsed();

                foreach (var kv in arch->transactions)
                {
                    ref var edge = ref kv.Value.Ref;
                    total += edge.addEntity.GetMemorySizeUsed();
                    total += edge.removeEntity.GetMemorySizeUsed();
                }
            }
            return total;
        }

        private static long CalculateQueriesMemory(World.WorldUnsafe* w)
        {
            long total = w->queries.GetMemorySizeUsed();

            for (int i = 0; i < w->queries.Length; i++)
            {
                var q = w->queries.Ptr[i].Ptr;
                total += q->with.GetMemorySizeUsed();
                total += q->none.GetMemorySizeUsed();
            }
            return total;
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
            DrawInfoRow("Pools Overhead", FormatBytes(info.poolsOverhead));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Untracked", EditorStyles.boldLabel);
            var known = info.entitiesMemory + info.archetypesMemory + info.queriesMemory + _totalPoolsAllocated + info.poolsOverhead;
            var untracked = info.allocatorUsed - known;
            if (untracked < 0) untracked = 0;
            DrawInfoRow("Untracked Memory", FormatBytes(untracked));

            EditorGUILayout.EndVertical();
        }

        private void DrawPoolsOverview()
        {
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

            for (var i = 0; i < _poolInfos.Count; i++)
            {
                var pool = _poolInfos[i];
                if (!pool.isCreated) continue;

                totalAllocated += pool.allocatedBytes;
                totalUsed += pool.usedBytes;

                var key = pool.typeIndex;
                if (!_foldoutStates.TryGetValue(key, out var open))
                {
                    open = false;
                    _foldoutStates[key] = open;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var foldoutRect = EditorGUILayout.GetControlRect();
                var indent = pool.isElementPool ? 32f : 16f;
                var foRect = new Rect(foldoutRect.x + (pool.isElementPool ? 16f : 0f), foldoutRect.y, 14f, foldoutRect.height);
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
                    : pool.isElementPool
                        ? new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Normal, fontSize = 10 }
                        : EditorStyles.label;

                string label;
                if (pool.isTag)
                    label = $"[T] {pool.typeName}";
                else if (pool.isElementPool)
                    label = $"  └ elements ({pool.typeName})";
                else if (pool.isArray)
                    label = $"[A] {pool.typeName}";
                else
                    label = pool.typeName;

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
                    EditorGUILayout.LabelField("Pool Index", pool.typeIndex.ToString());
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
            var parents = new List<PoolMemoryInfo>();
            var elementMap = new Dictionary<int, PoolMemoryInfo>();
            var skipIndices = new HashSet<int>();

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

                if (skipIndices.Contains(typeIndex)) continue;

                var typeName = "Unknown";
                try
                {
                    var t = ComponentTypeMap.GetType(typeIndex);
                    if (t != null) typeName = t.Name;
                }
                catch { }

                var info = CollectSinglePool(typeIndex, ref pool, typeData, typeName);
                info.isArray = typeData.isArray;
                info.isElementPool = false;
                info.parentArrayPoolIndex = -1;
                parents.Add(info);

                if (!typeData.isArray || typeIndex + 1 >= poolsPtr.Capacity) continue;

                ref var elementPool = ref poolsPtr.Ptr[typeIndex + 1];
                if (!elementPool.IsCreated || elementPool.UnsafeBuffer == null) continue;

                var elementData = elementPool.UnsafeBuffer->componentTypeData;

                var elementTypeName = "Unknown";
                try
                {
                    elementTypeName = elementData.ManagedType.Name;
                }
                catch { }

                var elementInfo = CollectSinglePool(typeIndex + 1, ref elementPool, elementData, elementTypeName, info.entitiesUsing);
                elementInfo.isElementPool = true;
                elementInfo.parentArrayPoolIndex = typeIndex;
                elementMap[typeIndex] = elementInfo;
                skipIndices.Add(typeIndex + 1);
            }

            parents.Sort((a, b) => b.allocatedBytes.CompareTo(a.allocatedBytes));

            foreach (var parent in parents)
            {
                _poolInfos.Add(parent);
                if (elementMap.TryGetValue(parent.typeIndex, out var element))
                    _poolInfos.Add(element);
            }

            _totalPoolsAllocated = 0;
            foreach (var p in _poolInfos)
                _totalPoolsAllocated += p.allocatedBytes;

            _poolsOverhead = CalculatePoolsOverhead();
        }

        private long CalculatePoolsOverhead()
        {
            var w = _world.UnsafeWorld;
            long overhead = w->pools.GetMemorySizeUsed();

            overhead += w->queriesHashToIndex.GetMemorySizeUsed();
            overhead += w->DefaultNoneTypes.GetMemorySizeUsed();
            overhead += w->entitiesDens.GetMemorySizeUsed();

            var poolsPtr = w->pools;
            for (var i = 0; i < _poolInfos.Count; i++)
            {
                var info = _poolInfos[i];
                if (!info.isCreated || info.isTag) continue;
                if (info.typeIndex >= poolsPtr.Capacity) continue;

                ref var pool = ref poolsPtr.Ptr[info.typeIndex];
                if (pool.UnsafeBuffer == null) continue;

                var untyped = pool.UnsafeBuffer;
                overhead += sizeof(ComponentPoolUntyped);
                overhead += untyped->Chunks.GetMemorySizeUsed();
            }

            for (var i = 0; i < w->archetypesList.Length; i++)
            {
                overhead += sizeof(ArchetypeUnsafe);
                var arch = w->archetypesList.Ptr[i].Ptr;
                foreach (var kv in arch->transactions)
                    overhead += sizeof(Edge);
            }

            for (var i = 0; i < w->queries.Length; i++)
                overhead += sizeof(QueryUnsafe);

            return overhead;
        }

        private PoolMemoryInfo CollectSinglePool(int typeIndex, ref GenericPool pool, ComponentTypeData typeData, string typeName, int overrideEntitiesUsing = -1)
        {
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

                entitiesUsing = overrideEntitiesUsing >= 0 ? overrideEntitiesUsing : CountEntitiesUsingPool(typeIndex);
                usedBytes = (long)entitiesUsing * itemSize;
            }

            return new PoolMemoryInfo
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
                isElementPool = false,
                parentArrayPoolIndex = -1,
                isCreated = true
            };
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
