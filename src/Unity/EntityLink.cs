﻿using System.Collections.Generic;
using TriInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Wargon.Nukecs;
using Wargon.Nukecs.Transforms;
using Transform = UnityEngine.Transform;

namespace Wargon.Nukecs.Tests {
    public class EntityLink : MonoBehaviour, ICustomConvertor {
        [SerializeField] private EntityLinkOption Option;
        [SerializeField] private int worldId;
        [Title("Components")][HideLabel][GUIColor(0.6f, 0.9f, 1.0f)][SerializeReference] public List<IComponent> components;
        [Title("Convertors")][HideLabel][GUIColor(1.0f, 1.0f, 0.0f)][SerializeField] protected List<Convertor> convertors = new ();
        private bool converted;
        private void Start() {
            // if(converted) return;
            // if (!EntityPrefabMap.TryGet(GetInstanceID(), out Entity entity)) {
            //     ref var w = ref World.Get(worldId);
            //     var e = w.Entity();
            //     Convert(ref w, ref e);
            // }
        }

        public void Convert(ref World world, ref Entity entity) {
            if(converted) return;
            foreach (var component in components)
            {
                entity.AddObject(component);
            }
            foreach (var customConvertor in convertors) {
                customConvertor.Convert(ref world,ref entity);
            }
            switch (Option) {
                case EntityLinkOption.Pure:
                    ConvertComponent<SpriteRenderer>(transform, ref world, ref entity);
                    var children = transform.GetComponentsInChildren<Transform>();
                    foreach (var childT in children) {
                        if(childT == transform) continue;
                        var childE = world.Entity();
                        ConvertComponent<SpriteRenderer>(childT, ref world, ref childE);
                        entity.AddChild(childE);
                        Debug.Log(childT.name);
                    }
                    converted = true;
                    EntityPrefabMap.GetOrCreatePrefab(this, ref world);
                    Destroy(gameObject);
                    break;
                case EntityLinkOption.Hybrid:
                    break;
            }
            
            world.SpawnPrefab(in entity);
        }

        private static void ConvertComponent<T>(Transform transform, ref World world, ref Entity entity) where T : UnityEngine.Component {
            TransformsUtility.Convert(transform, ref world, ref entity);
            var c = transform.GetComponent<T>();
            if (c == null) return;
            switch (c) {
                case SpriteRenderer spriteRenderer:
                    SpriteData.Convert(spriteRenderer, ref world, ref entity);
                    break;
            }
        }
    }

    public enum EntityLinkOption {
        Pure,
        Hybrid,
    }
}

#if UNITY_EDITOR
//
//     [UnityEditor.CustomPropertyDrawer(typeof(ComponentData))]
//     public class ComponentDataDrawer : UnityEditor.PropertyDrawer {
//         public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label) {
//             UnityEditor.EditorGUI.BeginProperty(position, label, property);
//
//             UnityEditor.SerializedProperty componentNameProperty = property.FindPropertyRelative("componentName");
//             UnityEditor.SerializedProperty componentDataProperty = property.FindPropertyRelative("componentData");
//
//             // Draw the component name
//             Rect componentNameRect = new Rect(position.x, position.y, position.width,
//                 UnityEditor.EditorGUIUtility.singleLineHeight);
//             UnityEditor.EditorGUI.PropertyField(componentNameRect, componentNameProperty);
//
//             // Draw the component data (not directly editable)
//             Rect componentDataRect = new Rect(position.x,
//                 position.y + UnityEditor.EditorGUIUtility.singleLineHeight + 2, position.width,
//                 UnityEditor.EditorGUIUtility.singleLineHeight);
//             if (GUI.Button(componentDataRect, "Edit Component Data")) {
//                 // Handle the component data editing in the custom editor
//                 ComponentEditorWindow.ShowWindow(componentDataProperty, componentNameProperty.stringValue);
//             }
//
//             UnityEditor.EditorGUI.EndProperty();
//         }
//
//         public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label) {
//             return UnityEditor.EditorGUIUtility.singleLineHeight * 2 + 2;
//         }
//     }
//
[CustomPropertyDrawer(typeof(Entity))]
public class EntityLinkPropertyDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        // Create property container element.
        var container = new VisualElement();

        // Create property fields.
        var amountField = new UnityEditor.UIElements.PropertyField(property.FindPropertyRelative("amount"));
        var unitField = new UnityEditor.UIElements.PropertyField(property.FindPropertyRelative("unit"));
        var nameField = new UnityEditor.UIElements.PropertyField(property.FindPropertyRelative("name"), "Fancy Name");

        // Add fields to the container.
        container.Add(amountField);
        container.Add(unitField);
        container.Add(nameField);

        return container;
    }
}
#endif
//}