using System.Collections.Generic;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public class EntityLink : MonoBehaviour, ICustomConvertor {
        [SerializeField] private int world;
        public List<IComponent> components;
        private void Start() {
            //Convert(ref World.Get(world));
        }

        public void Convert(ref World world, ref Entity entity) {
            foreach (var component in components)
            {
                entity.AddObject(component);
            }

            var childred = transform.GetComponentsInChildren<GameObject>();
            foreach (var o in childred) {
                
            }
        }
    }
}

// #if UNITY_EDITOR
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
// #endif
//}