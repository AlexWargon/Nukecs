﻿#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor {
    public unsafe class WorldDebug : EditorWindow {
        [MenuItem("Nuke.cs/World Debug")]
        public static void OpenWindow() {
            var w = GetWindow<WorldDebug>();
        }

        private Dictionary<int, QueryInfo> QueryLabels = new();
        private Dictionary<int, ArchetypeInfo> ArchetypeLabels = new();
        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            ScrollView scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            root.Add(scrollView);
            // VisualElements objects can contain other VisualElement following a tree hierarchy
            //Label label = new Label("Hello World!");
            ref var w = ref World.Get(0);
            if(w.IsAlive == false) return;
            for (var index = 0; index < w.UnsafeWorld->queries.Length; index++) {
                var ptr = w.UnsafeWorld->queries[index];
                var queryLabel = QueryInfo(ptr.Ptr, index);
                scrollView.Add(queryLabel);
            }
            foreach (var kvPair in w.UnsafeWorld->archetypesMap) {
                var label = ArchetypeInfo(kvPair.Value, kvPair.Key);
                scrollView.Add(label);
            }
            // root.Add(label);
        }
        // void Update()
        // {
        //     label.schedule.Execute(() =>
        //     {
        //
        //         label.text = $"Mouse over: {mouseOver}";
        //     }).Every(10);
        // }
        private Label QueryInfo(QueryUnsafe* queryImpl, int index) {
            StringBuilder builder = new StringBuilder();
            builder.Append("Query");
            foreach (var typesIndex in ComponentTypeMap.TypesIndexes) {
                if (queryImpl->HasWith(typesIndex)) {
                    builder.Append($".With<{ComponentTypeMap.GetType(typesIndex).Name}>()");
                }

                if (queryImpl->HasNone(typesIndex)) {
                    builder.Append($".None<{ComponentTypeMap.GetType(typesIndex).Name}>()");
                }
            }

            builder.Append($".Count =");
            var info = builder.ToString();
            
            builder.Clear();
            var label = new Label(info);
            label.name = info;
            label.schedule.Execute(() => {
                var inf = QueryLabels[index];
                if (inf.Query != null && inf.Query->world != null)
                {
                    label.text = $"{inf.Label.name}{inf.Query->count}";
                }
            }).Every(60);
            QueryLabels[index] = new QueryInfo {
                Label = label,
                Query = queryImpl
            };
            return label;
        }

        private Label ArchetypeInfo(Archetype archetype, int id) {
            var label = new Label(archetype.impl->ToString());
            label.schedule.Execute(() => {
                var inf = ArchetypeLabels[id];
                if(inf.Archetype->IsCreated)
                    label.text = inf.Archetype->ToString();
            }).Every(100);
            ArchetypeLabels[id] = new ArchetypeInfo {
                Label = label,
                Archetype = archetype.impl
            };
            
            return label;
        }
    }

    internal unsafe class QueryInfo {
        internal Label Label;
        internal QueryUnsafe* Query;
    }

    internal unsafe class ArchetypeInfo {
        internal Label Label;
        internal ArchetypeUnsafe* Archetype;
    }
}
#endif