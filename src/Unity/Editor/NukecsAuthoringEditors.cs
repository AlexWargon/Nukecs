#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wargon.Nukecs.Editor.EcsDebugV2;
using Theme = Wargon.Nukecs.Editor.EcsDebugV2.EcsDebugV2Theme;

namespace Wargon.Nukecs.Editor
{
    /// <summary>Shared authoring layout, using the same surfaces and controls as ECS Debug v2.</summary>
    public abstract class NukecsAuthoringEditor : UnityEditor.Editor
    {
        protected VisualElement Root;

        public override VisualElement CreateInspectorGUI()
        {
            Root = new VisualElement { name = "nukecs-authoring" };
            Root.style.backgroundColor = Theme.Background;
            Root.style.color = Theme.Foreground;
            Root.style.paddingTop = Root.style.paddingBottom = 8;
            Root.style.paddingLeft = Root.style.paddingRight = 8;
            var card = Card(ObjectNames.NicifyVariableName(target.GetType().Name), Theme.Amber);
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true)) {
                do {
                    if (!ShouldDrawProperty(iterator)) continue;
                    if (iterator.name == "components") {
                        DrawComponents(iterator.Copy());
                        continue;
                    }
                    if (iterator.name == "convertors") {
                        var convertors = Card("Convertors", Theme.TypeEntity);
                        convertors.Add(new PropertyField(iterator.Copy()));
                        continue;
                    }
                    var field = new PropertyField(iterator.Copy()) { name = iterator.name };
                    field.SetEnabled(iterator.name != "m_Script" && iterator.name != "WorldId");
                    card.Add(field);
                } while (iterator.NextVisible(false));
            }
            DrawActions();
            return Root;
        }

        protected virtual void DrawActions() { }
        protected virtual bool ShouldDrawProperty(SerializedProperty property) => true;

        protected VisualElement Card(string title, Color accent)
        {
            var card = Theme.CreateGlassCard();
            card.style.marginBottom = 8;
            var header = Theme.CreateHeaderRow();
            header.Add(new Label(title) { style = { color = accent, fontSize = Theme.FontBody } });
            card.Add(header);
            Root.Add(card);
            return card;
        }

        private void DrawComponents(SerializedProperty property)
        {
            var section = Card("Components", Theme.TypeNumber);
            var items = new VisualElement();
            section.Add(items);
            var cards = new List<(SerializedComponentCardBinding binding, ComponentCardDrawer drawer)>();
            void Refresh()
            {
                serializedObject.Update();
                items.Clear();
                cards.Clear();
                for (var i = 0; i < property.arraySize; i++) {
                    var binding = new SerializedComponentCardBinding(serializedObject, property.propertyPath, i, Refresh);
                    var info = binding.Read();
                    // Authoring owns independent cards; the live debugger's cache is never shared.
                    var drawer = new ComponentCardDrawer(info);
                    drawer.Bind(i, info.Name, binding, i, info);
                    cards.Add((binding, drawer));
                    items.Add(drawer.card);
                }
                items.Bind(serializedObject);
            }
            Refresh();
            Root.schedule.Execute(() => {
                if (!target) return;
                serializedObject.Update();
                if (property.arraySize != cards.Count) { Refresh(); return; }
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                foreach (var (binding, drawer) in cards) drawer.UpdateValues(binding.Read(), now);
            }).Every(100);
            section.Add(Theme.CreateActionBtn("Add component", Theme.Amber, () => {
                var menu = new GenericMenu();
                var types = TypeCache.GetTypesDerivedFrom<IComponent>()
                    .Concat(TypeCache.GetTypesDerivedFrom<IPoolComponent>())
                    .Distinct()
                    .Where(t => !t.IsInterface && !t.IsAbstract && !t.ContainsGenericParameters
                        && !typeof(UnityEngine.Object).IsAssignableFrom(t))
                    .OrderBy(t => t.FullName).ToArray();
                foreach (var type in types) {
                    menu.AddItem(new GUIContent(type.FullName.Replace('.', '/')), false, () => {
                        // Structs have no reflected parameterless constructor; create
                        // their boxed default value before changing the serialized list.
                        var component = type.IsValueType || type.GetConstructor(
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null
                            ? Activator.CreateInstance(type, true)
                            : System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
                        serializedObject.Update();
                        var index = property.arraySize++;
                        property.GetArrayElementAtIndex(index).managedReferenceValue = component;
                        serializedObject.ApplyModifiedProperties();
                        Refresh();
                    });
                }
                if (types.Length == 0) menu.AddDisabledItem(new GUIContent("No component types found"));
                menu.ShowAsContext();
            }));
            // Rebuild after Undo/Redo as managed-reference type changes invalidate child properties.
            Root.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += Refresh);
            Root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Refresh);
        }

    }

    [CustomEditor(typeof(WorldInstaller), true)]
    public sealed class WorldInstallerEditor : NukecsAuthoringEditor { }

    [CustomEditor(typeof(EntityBaker), true)]
    public sealed class EntityBakerEditor : NukecsAuthoringEditor { }

    [CustomEditor(typeof(Tests.EntityLinkSO), true)]
    public sealed class EntityLinkSOEditor : NukecsAuthoringEditor { }

    [CustomEditor(typeof(WorldBaker), true)]
    public sealed class WorldBakerEditor : NukecsAuthoringEditor
    {
        private bool _busy;
        protected override void DrawActions()
        {
            var baker = (WorldBaker)target;
            var card = Card("World", Theme.Amber);
            var buttons = new VisualElement();
            var status = new Label { style = { color = Theme.MutedText, whiteSpace = WhiteSpace.Normal } };
            card.Add(buttons);
            card.Add(status);
            void Refresh()
            {
                buttons.SetEnabled(!_busy);
                buttons.Q<Button>("bake").SetEnabled(!Application.isPlaying);
                buttons.Q<Button>("load").SetEnabled(Application.isPlaying && !baker.HasRuntimeWorld);
                buttons.Q<Button>("load-async").SetEnabled(Application.isPlaying && !baker.HasRuntimeWorld);
                buttons.Q<Button>("save").SetEnabled(Application.isPlaying && baker.IsRuntimeReady);
            }
            async void Execute(Func<Task> action)
            {
                if (_busy) return;
                _busy = true;
                status.text = "Working…";
                Refresh();
                try { await action(); status.text = "Done"; }
                catch (Exception exception) { status.text = exception.Message; Debug.LogException(exception, baker); }
                finally { _busy = false; if (baker) Refresh(); }
            }
            void Add(string name, string text, Func<Task> action)
            {
                var button = Theme.CreateActionBtn(text, Theme.Amber, () => Execute(action));
                button.name = name;
                buttons.Add(button);
            }
            Add("bake", "Bake to file", baker.BakeInternal);
            Add("load", "Load", () => { baker.Load(); return Task.CompletedTask; });
            Add("load-async", "Load async", baker.LoadAsync);
            Add("save", "Save", baker.Save);
            Refresh();
            Root.schedule.Execute(() => { if (baker) Refresh(); }).Every(200);
        }
    }
}
#endif
