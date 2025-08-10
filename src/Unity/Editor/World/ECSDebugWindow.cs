#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    public unsafe class ECSDebugWindow : EditorWindow
    {
        private World world; // Твой ECS World
        private Vector2 entitiesScrollPosition;
        private Vector2 archetypesScrollPosition;
        private Vector2 queriesScrollPosition;
        private string entitySearchQuery = "";
        private string archetypeSearchQuery = "";
        private string querySearchQuery = "";
        private bool showEntities = true;
        private bool showArchetypes = true;
        private bool showQueries = true;
        private Dictionary<int, bool> archetypeFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> queryFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, string> queryIdToName = new Dictionary<int, string>();
        [MenuItem("Window/ECS Debug Window")]
        public static void ShowWindow()
        {
            GetWindow<ECSDebugWindow>("ECS Debug");
        }

        private void OnEnable()
        {
            world = FindWorld();
            if (!world.IsAlive)
            {
                Debug.LogWarning("ECS World not found. Please ensure World is initialized.");
            }

            EditorApplication.playModeStateChanged += Enable;
        }
        
        private void Enable(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.EnteredPlayMode)
            {
                NukecsDebugUpdater.Instance.OnUpdate += OnGameUpdate;
            }
            else
            if (playModeStateChange == PlayModeStateChange.ExitingPlayMode)
            {
                NukecsDebugUpdater.Instance.OnUpdate -= OnGameUpdate;
            }
        }
        private void OnGameUpdate()
        {
            if (EditorApplication.isPlaying && world.IsAlive)
            {
                Repaint();
            }
        }
        private World FindWorld()
        {
            return World.Get(0);
        }

        private void OnGUI()
        {
            world = FindWorld();
            if (!world.IsAlive || !EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField($"No Active World");
                return;
            }

            EditorGUILayout.LabelField($"World Entities: {world.EntitiesAmount}", EditorStyles.boldLabel);
            EditorGUILayout.Space();


            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh")) Repaint();
            GUILayout.Label("Filter:", GUILayout.Width(50));
            entitySearchQuery = EditorGUILayout.TextField(entitySearchQuery);
            EditorGUILayout.EndHorizontal();

            // entities
            showEntities = EditorGUILayout.Foldout(showEntities, $"Entities ({world.UnsafeWorld->entitiesAmount})", true);
            if (showEntities)
            {
                entitiesScrollPosition = EditorGUILayout.BeginScrollView(entitiesScrollPosition, GUILayout.Height(200));
                DisplayEntities();
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();

            // acrhetypes
            showArchetypes = EditorGUILayout.Foldout(showArchetypes, $"Archetypes ({world.UnsafeWorld->archetypesList.Length})", true);
            if (showArchetypes)
            {
                archetypeSearchQuery = EditorGUILayout.TextField("Archetype Filter:", archetypeSearchQuery);
                archetypesScrollPosition = EditorGUILayout.BeginScrollView(archetypesScrollPosition, GUILayout.Height(200));
                DisplayArchetypes();
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();

            // queries
            showQueries = EditorGUILayout.Foldout(showQueries, $"Queries ({world.UnsafeWorld->queries.Length})", true);
            if (showQueries)
            {
                querySearchQuery = EditorGUILayout.TextField("Query Filter:", querySearchQuery);
                queriesScrollPosition = EditorGUILayout.BeginScrollView(queriesScrollPosition, GUILayout.Height(200));
                DisplayQueries();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DisplayEntities()
        {
            var entities = world.UnsafeWorld->entities;
            var archetypes = world.UnsafeWorld->entitiesArchetypes;
            for (int i = 0; i < entities.Capacity; i++)
            {
                var entity = entities.Ptr[i];
                if (!entity.IsValid()) continue;

                string entityName = $"Entity {entity.id}";
                if (!string.IsNullOrEmpty(entitySearchQuery) && !entityName.Contains(entitySearchQuery, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                EditorGUILayout.BeginHorizontal();
                var archetype = archetypes.ElementAt(entity.id);
                var archetypeInfo = archetype.IsCreated ? $"Archetype ({archetype.ptr.Ref.id})" : "No Archetype";
                EditorGUILayout.LabelField(entityName, GUILayout.Width(100));
                EditorGUILayout.LabelField(archetypeInfo, GUILayout.Width(150));

                // Компоненты
                if (archetype.IsCreated)
                {
                    //var types = archetype.ptr.Ref.types;
                    //string components = string.Join(", ", types.AsSpan().Select(t => ComponentTypeMap.GetType(t).Name));
                    //EditorGUILayout.LabelField(components);
                }
                else
                {
                    EditorGUILayout.LabelField("No Components");
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DisplayArchetypes()
        {
            // foreach (var kvp in world.UnsafeWorld->archetypes)
            // {
            //     var archetype = kvp.Value.ptr.Ref;
            //     string archetypeName = $"Archetype {archetype.id}";
            //     if (!string.IsNullOrEmpty(archetypeSearchQuery) && !archetypeName.Contains(archetypeSearchQuery, System.StringComparison.OrdinalIgnoreCase))
            //         continue;
            //
            //     if (!archetypeFoldouts.ContainsKey(archetype.id))
            //         archetypeFoldouts[archetype.id] = false;
            //
            //     archetypeFoldouts[archetype.id] = EditorGUILayout.Foldout(archetypeFoldouts[archetype.id], archetypeName, true);
            //     if (archetypeFoldouts[archetype.id])
            //     {
            //         EditorGUI.indentLevel++;
            //         EditorGUILayout.LabelField($"Components: {string.Join(", ", archetype.types.Select(t => ComponentTypeMap.GetType(t).Name))}");
            //         EditorGUILayout.LabelField($"Queries: {string.Join(", ", archetype.queries.Select(q => $"Query {q}"))}");
            //         EditorGUILayout.LabelField($"Transactions: {archetype.transactions.Count}");
            //         foreach (var trans in archetype.transactions)
            //         {
            //             EditorGUILayout.LabelField($"  Component {ComponentTypeMap.GetType(math.abs(trans.Key)).Name} ({trans.Key}) -> Archetype {trans.Value.Ref.ToMove.ptr.Ref.id}");
            //         }
            //         EditorGUILayout.LabelField($"Entities: {world.UnsafeWorld->entities.Where(e => e.IsValid() && world.UnsafeWorld->entitiesArchetypes.ElementAt(e.id).ptr.Ref.id == archetype.id).Count()}");
            //         EditorGUI.indentLevel--;
            //     }
            // }
        }

        private readonly StringBuilder stringBuilder = new StringBuilder();
        private void DisplayQueries()
        {
            for (int i = 0; i < world.UnsafeWorld->queries.Length; i++)
            {
                var query = world.UnsafeWorld->queries.ElementAt(i).Ref;
                if(!queryIdToName.ContainsKey(query.Id))
                {
                    stringBuilder.Append($"Query(id:{query.Id}, count:{query.count}).");
                    foreach (var with in query.withDebug)
                    {
                        stringBuilder.Append($"With<{ComponentTypeMap.GetType(with).Name},");
                    }
                    stringBuilder.Remove(stringBuilder.Length - 1, 1);
                    stringBuilder.Append(">().");
                    foreach (var none in query.noneDebug)
                    {
                        stringBuilder.Append($"None<{ComponentTypeMap.GetType(none).Name},");
                    }
                    stringBuilder.Remove(stringBuilder.Length - 1, 1);
                    stringBuilder.Append(">()");
                    queryIdToName[query.Id] = stringBuilder.ToString();
                    stringBuilder.Clear();
                }
                var queryName = queryIdToName[query.Id];
                if (!string.IsNullOrEmpty(querySearchQuery) && !queryName.Contains(querySearchQuery, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!queryFoldouts.ContainsKey(query.Id))
                    queryFoldouts[query.Id] = false;

                queryFoldouts[query.Id] = EditorGUILayout.Foldout(queryFoldouts[query.Id], queryName, true);
                if (queryFoldouts[query.Id])
                {
                    EditorGUI.indentLevel++;
                    foreach (var with in query.withDebug)
                    {
                        EditorGUILayout.LabelField($"With: {string.Join(", ", ComponentTypeMap.GetType(with).Name)}");
                    }
                    foreach (var none in query.noneDebug)
                    {
                        EditorGUILayout.LabelField($"None: {string.Join(", ", ComponentTypeMap.GetType(none).Name)}");
                    }
                    
                    EditorGUILayout.LabelField($"Entities: {query.count}");
                    for (var j = 0; j < query.count; j++)
                    {
                        var entityId = query.entities.ElementAt(j);
                        EditorGUILayout.LabelField($"  Entity {entityId}");
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }
    }
}
#endif
