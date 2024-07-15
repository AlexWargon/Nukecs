using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wargon.Nukecs;

namespace Wargon.Nukecs.Tests {
    public class ComponentEditorWindow : UnityEditor.EditorWindow {
        private SerializedProperty componentDataProperty;
        private string componentName;
        private object component;
        private Type componentType;

        public static void ShowWindow(SerializedProperty componentDataProperty, string componentName) {
            ComponentEditorWindow window = GetWindow<ComponentEditorWindow>("Component Editor");
            window.componentDataProperty = componentDataProperty;
            window.componentName = componentName;
            window.LoadComponent();
        }

        private void LoadComponent() {
            // Find the component type
            componentType = Type.GetType(componentName);
            if (componentType == null) {
                Debug.LogError($"Component type {componentName} not found.");
                return;
            }

            // Deserialize the component
            component = ComponentData.DeserializeComponent(GetByteArrayFromProperty(componentDataProperty),
                componentType);
        }

        private void SaveComponent() {
            // Serialize the component
            byte[] data = ComponentData.SerializeComponent(component);
            SetByteArrayToProperty(componentDataProperty, data);
            componentDataProperty.serializedObject.ApplyModifiedProperties();
        }

        private void OnGUI() {
            if (component == null || componentType == null) {
                EditorGUILayout.LabelField("Component data not loaded.");
                return;
            }

            // Use reflection to draw the fields of the component
            FieldInfo[] fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                object value = field.GetValue(component);
                if (value is int intValue) {
                    int newValue = EditorGUILayout.IntField(field.Name, intValue);
                    if (newValue != intValue) field.SetValue(component, newValue);
                }
                else if (value is float floatValue) {
                    float newValue = EditorGUILayout.FloatField(field.Name, floatValue);
                    if (newValue != floatValue) field.SetValue(component, newValue);
                }
                else if (value is string stringValue) {
                    string newValue = EditorGUILayout.TextField(field.Name, stringValue);
                    if (newValue != stringValue) field.SetValue(component, newValue);
                }
                // Add other field types as needed
            }

            if (GUILayout.Button("Save")) {
                SaveComponent();
                Close();
            }
        }

        private byte[] GetByteArrayFromProperty(SerializedProperty property) {
            byte[] byteArray = new byte[property.arraySize];
            for (int i = 0; i < property.arraySize; i++) {
                byteArray[i] = (byte) property.GetArrayElementAtIndex(i).intValue;
            }

            return byteArray;
        }

        private void SetByteArrayToProperty(SerializedProperty property, byte[] byteArray) {
            property.ClearArray();
            property.arraySize = byteArray.Length;
            for (int i = 0; i < byteArray.Length; i++) {
                property.GetArrayElementAtIndex(i).intValue = byteArray[i];
            }
        }
    }

    [UnityEditor.CustomEditor(typeof(EntityLink))]
    public class ComponentHolderEditor : UnityEditor.Editor {
private SerializedProperty componentsProperty;

    private void OnEnable()
    {
        componentsProperty = serializedObject.FindProperty(nameof(EntityLink.components));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        for (int i = 0; i < componentsProperty.arraySize; i++)
        {
            SerializedProperty componentProperty = componentsProperty.GetArrayElementAtIndex(i);
            DrawComponentProperty(componentProperty, i);
        }

        if (GUILayout.Button("Add Component"))
        {
            ComponentSelectionWindow.ShowWindow(type =>
            {
                componentsProperty.arraySize++;
                SerializedProperty newComponentProperty = componentsProperty.GetArrayElementAtIndex(componentsProperty.arraySize - 1);
                newComponentProperty.FindPropertyRelative(nameof(ComponentData.componentName)).stringValue = type.FullName;
                newComponentProperty.FindPropertyRelative(nameof(ComponentData.componentData)).ClearArray();
                serializedObject.ApplyModifiedProperties();
            });
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawComponentProperty(SerializedProperty componentProperty, int index)
    {
        EditorGUILayout.BeginVertical("box");

        SerializedProperty componentNameProperty = componentProperty.FindPropertyRelative("componentName");
        SerializedProperty componentDataProperty = componentProperty.FindPropertyRelative("componentData");

        EditorGUILayout.LabelField(componentNameProperty.stringValue);

        Type componentType = Type.GetType(componentNameProperty.stringValue);
        if (componentType != null)
        {
            object component = ComponentData.DeserializeComponent(GetByteArrayFromProperty(componentDataProperty), componentType);
            DrawComponentFields(component, componentType);
            byte[] data = ComponentData.SerializeComponent(component);
            SetByteArrayToProperty(componentDataProperty, data);
        }

        if (GUILayout.Button("Remove Component"))
        {
            componentsProperty.DeleteArrayElementAtIndex(index);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawComponentFields(object component, Type componentType)
    {
        FieldInfo[] fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (FieldInfo field in fields)
        {
            object value = field.GetValue(component);
            EditorGUI.BeginChangeCheck();
            object newValue = DrawField(field.Name, value, field.FieldType);
            if (EditorGUI.EndChangeCheck())
            {
                field.SetValue(component, newValue);
            }
        }
    }

    private object DrawField(string label, object value, Type type)
    {
        if (type == typeof(int))
        {
            return EditorGUILayout.IntField(label, (int)value);
        }
        else if (type == typeof(float))
        {
            return EditorGUILayout.FloatField(label, (float)value);
        }
        else if (type == typeof(string))
        {
            return EditorGUILayout.TextField(label, (string)value);
        }
        else if (type == typeof(bool))
        {
            return EditorGUILayout.Toggle(label, (bool)value);
        }
        else if (type == typeof(Vector3))
        {
            return EditorGUILayout.Vector3Field(label, (Vector3)value);
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UnityObjectRef<>))
        {
            return DrawUnityObjectRefField(label, value, type);
        }
        else
        {
            EditorGUILayout.LabelField(label, $"Type {type.Name} not supported");
            return value;
        }
    }
    private object DrawUnityObjectRefField(string label, object value, Type type) {
        var v = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        
        var instanceIDField = type.GetField("Id", BindingFlags.NonPublic | BindingFlags.Instance);
        var untypedRef = (UntypedUnityObjectRef)instanceIDField.GetValue(value);
        int instanceID = untypedRef.instanceId;
        UnityEngine.Object currentObject = (UnityEngine.Object)v.GetValue(value);

        UnityEngine.Object newObject = EditorGUILayout.ObjectField(label, currentObject, type.GetGenericArguments()[0], true);
        
        if (newObject != currentObject)
        {
            v.SetValue(value, newObject);
            //instanceIDField.SetValue(value, new UntypedUnityObjectRef { instanceId = newObject != null ? newObject.GetInstanceID() : 0 });
        }
        return value;
    }

    private byte[] GetByteArrayFromProperty(SerializedProperty property)
    {
        byte[] byteArray = new byte[property.arraySize];
        for (int i = 0; i < property.arraySize; i++)
        {
            byteArray[i] = (byte)property.GetArrayElementAtIndex(i).intValue;
        }
        return byteArray;
    }

    private void SetByteArrayToProperty(SerializedProperty property, byte[] byteArray)
    {
        property.ClearArray();
        property.arraySize = byteArray.Length;
        for (int i = 0; i < byteArray.Length; i++)
        {
            property.GetArrayElementAtIndex(i).intValue = byteArray[i];
        }
    }
}
    
    public class ComponentSelectionWindow : EditorWindow {
        private List<Type> componentTypes;
        private Action<Type> onSelectComponent;

        public static void ShowWindow(Action<Type> onSelectComponent) {
            ComponentSelectionWindow window = GetWindow<ComponentSelectionWindow>("Select Component");
            window.onSelectComponent = onSelectComponent;
            window.FindAllComponents();
        }

        private void FindAllComponents() {
            componentTypes = Assembly.GetAssembly(typeof(IComponent))
                .GetTypes()
                .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();
        }

        private void OnGUI() {
            if (componentTypes == null) {
                EditorGUILayout.LabelField("No components found.");
                return;
            }

            foreach (Type type in componentTypes) {
                if (GUILayout.Button(type.Name)) {
                    onSelectComponent?.Invoke(type);
                    Close();
                }
            }
        }
    }
}