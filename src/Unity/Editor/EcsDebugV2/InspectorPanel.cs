#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class InspectorPanel
    {
        public static VisualElement Create(EcsDebugV2Window window)
        {
            var panel = new VisualElement
            {
                name = "inspector-panel",
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1,
                    overflow = Overflow.Hidden,
                    backgroundColor = EcsDebugV2Theme.Background.WithAlpha(0.4f)
                }
            };
            Refresh(panel, window);
            return panel;
        }

        public static void Refresh(VisualElement panel, EcsDebugV2Window window)
        {
            var oldScroll = panel.Query<ScrollView>().First();
            var savedOffset = oldScroll != null ? oldScroll.scrollOffset : Vector2.zero;

            panel.Clear();

            switch (window.CurrentTab)
            {
                case TabKey.Entities:
                    var entity = window.Entities.FirstOrDefault(e => e.Id == window.SelectedEntityId);
                    if (entity != null)
                        DrawEntityInspector(panel, entity, window);
                    else
                        DrawEmptyState(panel);
                    break;
                case TabKey.Archetypes:
                    var arch = window.Archetypes.FirstOrDefault(a => a.Id == window.SelectedArchetypeId);
                    if (arch != null)
                        DrawArchetypeInspector(panel, arch, window);
                    else
                        DrawEmptyState(panel);
                    break;
                case TabKey.Queries:
                    var query = window.Queries.FirstOrDefault(q => q.Id == window.SelectedQueryId);
                    if (query != null)
                        DrawQueryInspector(panel, query, window);
                    else
                        DrawEmptyState(panel);
                    break;
                case TabKey.Resources:
                    var res = window.Resources.FirstOrDefault(r => r.Name == window.SelectedResourceName);
                    if (res != null)
                        DrawResourceInspector(panel, res);
                    else
                        DrawEmptyState(panel);
                    break;
            }

            var newScroll = panel.Query<ScrollView>().First();
            if (newScroll != null) newScroll.scrollOffset = savedOffset;
        }

        public static void UpdateValues(VisualElement panel, EcsDebugV2Window window)
        {
            switch (window.CurrentTab)
            {
                case TabKey.Entities:
                    var entity = window.Entities.FirstOrDefault(e => e.Id == window.SelectedEntityId);
                    if (entity != null)
                        UpdateEntityFieldValues(panel, entity, window);
                    break;
            }
        }

        private static void UpdateEntityFieldValues(VisualElement panel, EntityInfo entity, EcsDebugV2Window window)
        {
            foreach (var comp in entity.Components)
            {
                foreach (var field in comp.Fields)
                {
                    var editorEl = panel.Q($"editor-{comp.Name}-{field.Key}");
                    if (editorEl == null) continue;

                    if (editorEl is TextField tf)
                    {
                        try
                        {
                            var focused = tf.panel?.focusController?.focusedElement as VisualElement;
                            if (focused != null && tf.Contains(focused))
                                continue;
                        }
                        catch { }

                        switch (field.Value.Type)
                        {
                            case FieldValueType.Number:
                                tf.SetValueWithoutNotify(field.Value.NumberVal.ToString("G"));
                                break;
                            case FieldValueType.String:
                                tf.SetValueWithoutNotify(field.Value.StringVal);
                                break;
                            case FieldValueType.EntityRef:
                                tf.SetValueWithoutNotify(field.Value.EntityRefVal.ToString());
                                break;
                        }
                    }
                    else if (editorEl is Button btn && field.Value.Type == FieldValueType.Bool)
                    {
                        btn.text = field.Value.BoolVal.ToString();
                        btn.style.color = field.Value.BoolVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.Red;
                    }

                    var rowEl = panel.Q($"frow-{comp.Name}-{field.Key}");
                    if (rowEl != null)
                    {
                        var changeKey = $"{entity.Id}:{comp.Name}:{field.Key}";
                        long ts;
                        if (window.Changes.TryGetValue(changeKey, out ts))
                        {
                            var age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ts;
                            rowEl.style.backgroundColor = age < 1200
                                ? EcsDebugV2Theme.Yellow.WithAlpha(0.15f)
                                : (Color)EcsDebugV2Theme.PanelElevated;
                        }
                        else
                        {
                            rowEl.style.backgroundColor = EcsDebugV2Theme.PanelElevated;
                        }
                    }
                }
            }
        }

        private static void DrawEmptyState(VisualElement panel)
        {
            var empty = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center
                }
            };
            var label = new Label("Select an item on the left to inspect.")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.MutedText
                }
            };
            empty.Add(label);
            panel.Add(empty);
        }

        private static void DrawEntityInspector(VisualElement panel, EntityInfo entity, EcsDebugV2Window window)
        {
            var header = EcsDebugV2Theme.CreateHeaderRow();
            var idLabel = new Label($"#{entity.Id}")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.TypeEntity,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 6
                }
            };
            header.Add(idLabel);
            var nameLabel = new Label(entity.Name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Foreground,
                    marginRight = 6
                }
            };
            header.Add(nameLabel);
            header.Add(EcsDebugV2Theme.CreateBadge(entity.Archetype.ToUpper(),
                EcsDebugV2Theme.Orange.WithAlpha(0.15f), EcsDebugV2Theme.Orange, EcsDebugV2Theme.Font.Mini));

            var meta = new Label($"{entity.Components.Count} components")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = UnityEngine.UIElements.Length.Auto()
                }
            };
            header.Add(meta);

            var destroyBtn = EcsDebugV2Theme.CreateActionBtn("Destroy", EcsDebugV2Theme.Red, () => window.DestroyEntity(entity.Id));
            destroyBtn.style.marginLeft = 8;
            destroyBtn.tooltip = "Destroy entity";
            header.Add(destroyBtn);
            panel.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "inspector-scroll",
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    overflow = Overflow.Hidden
                }
            };

            foreach (var comp in entity.Components)
                scroll.Add(DrawComponentCard(entity, comp, window));

            scroll.Add(DrawAddComponentSection(entity, window));
            panel.Add(scroll);
        }

        private static VisualElement DrawComponentCard(EntityInfo entity, ComponentInfo comp, EcsDebugV2Window window)
        {
            var card = EcsDebugV2Theme.CreateCard();
            card.style.marginBottom = 6;

            var compHeader = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = EcsDebugV2Theme.PanelElevated
                }
            };

            var compName = new Label(comp.Name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Body,
                    color = EcsDebugV2Theme.Foreground,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            compHeader.Add(compName);

            var sizeLabel = new Label($"{comp.ByteSize}B")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = 6
                }
            };
            compHeader.Add(sizeLabel);

            var removeBtn = new Button(() => window.RemoveComponent(entity.Id, comp.Name))
            {
                text = "\u2715",
                tooltip = $"Remove {comp.Name}",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.MutedText,
                    backgroundColor = Color.clear,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginLeft = UnityEngine.UIElements.Length.Auto(),
                    width = 24,
                    height = 24
                }
            };
            removeBtn.SetupBorder(Color.clear, 0);
            removeBtn.RegisterCallback<MouseEnterEvent>(_ => removeBtn.style.color = EcsDebugV2Theme.Red);
            removeBtn.RegisterCallback<MouseLeaveEvent>(_ => removeBtn.style.color = EcsDebugV2Theme.MutedText);
            compHeader.Add(removeBtn);
            card.Add(compHeader);

            foreach (var kv in comp.Fields)
            {
                var row = DrawFieldRow(entity.Id, comp.Name, kv.Key, kv.Value, window);
                card.Add(row);
            }
            return card;
        }

        private static VisualElement DrawFieldRow(int entityId, string compName, string fieldKey, FieldValue value, EcsDebugV2Window window)
        {
            var row = new VisualElement
            {
                name = $"frow-{compName}-{fieldKey}",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 2,
                    paddingBottom = 2,
                    backgroundColor = EcsDebugV2Theme.PanelElevated
                }
            };

            var keyLabel = new Label(fieldKey)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.MutedText,
                    width = 130,
                    flexShrink = 0
                }
            };
            row.Add(keyLabel);

            var changeKey = $"{entityId}:{compName}:{fieldKey}";
            long ts;
            if (window.Changes.TryGetValue(changeKey, out ts))
            {
                var age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ts;
                if (age < 1200)
                    row.style.backgroundColor = EcsDebugV2Theme.Yellow.WithAlpha(0.15f);
            }

            var editor = CreateFieldEditor(value, (newVal) =>
            {
                window.SetFieldValue(entityId, compName, fieldKey, newVal);
            });
            editor.name = $"editor-{compName}-{fieldKey}";
            editor.style.flexGrow = 1;
            row.Add(editor);
            return row;
        }

        private static VisualElement CreateFieldEditor(FieldValue value, Action<FieldValue> onChange)
        {
            switch (value.Type)
            {
                case FieldValueType.Number:
                    var numTf = new TextField
                    {
                        value = value.NumberVal.ToString("G"),
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.TypeNumber,
                            backgroundColor = Color.clear,
                            flexGrow = 1
                        }
                    };
                    numTf.SetupBorder(Color.clear, 0);
                    numTf.Q("unity-text-input").style.backgroundColor = Color.clear;
                    numTf.Q("unity-text-input").SetupBorder(Color.clear, 0);
                    numTf.Q("unity-text-input").style.paddingLeft = 2;
                    numTf.Q("unity-text-input").style.paddingRight = 2;
                    numTf.RegisterValueChangedCallback(evt =>
                    {
                        if (double.TryParse(evt.newValue, out var n))
                            onChange(FieldValue.FromNumber(n));
                    });
                    return numTf;

                case FieldValueType.Bool:
                    var boolBtn = new Button(() => onChange(FieldValue.FromBool(!value.BoolVal)))
                    {
                        text = value.BoolVal.ToString(),
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = value.BoolVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.Red,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            backgroundColor = Color.clear,
                            paddingLeft = 2,
                            paddingRight = 2,
                            paddingTop = 1,
                            paddingBottom = 1
                        }
                    };
                    boolBtn.SetupBorder(Color.clear, 0);
                    return boolBtn;

                case FieldValueType.String:
                    var strTf = new TextField
                    {
                        value = value.StringVal,
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.TypeString,
                            backgroundColor = Color.clear,
                            flexGrow = 1
                        }
                    };
                    strTf.SetupBorder(Color.clear, 0);
                    strTf.Q("unity-text-input").style.backgroundColor = Color.clear;
                    strTf.Q("unity-text-input").SetupBorder(Color.clear, 0);
                    strTf.Q("unity-text-input").style.paddingLeft = 2;
                    strTf.Q("unity-text-input").style.paddingRight = 2;
                    strTf.RegisterValueChangedCallback(evt =>
                        onChange(FieldValue.FromString(evt.newValue)));
                    return strTf;

                case FieldValueType.EntityRef:
                    var refTf = new TextField
                    {
                        value = value.EntityRefVal.ToString(),
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.TypeEntity,
                            backgroundColor = Color.clear,
                            flexGrow = 1
                        }
                    };
                    refTf.SetupBorder(Color.clear, 0);
                    refTf.Q("unity-text-input").style.backgroundColor = Color.clear;
                    refTf.Q("unity-text-input").SetupBorder(Color.clear, 0);
                    refTf.Q("unity-text-input").style.paddingLeft = 2;
                    refTf.Q("unity-text-input").style.paddingRight = 2;
                    refTf.RegisterValueChangedCallback(evt =>
                    {
                        if (int.TryParse(evt.newValue, out var n))
                            onChange(FieldValue.FromEntityRef(n));
                    });
                    return refTf;

                default:
                    return new Label("\u2014") { style = { color = EcsDebugV2Theme.MutedText } };
            }
        }

        private static VisualElement DrawAddComponentSection(EntityInfo entity, EcsDebugV2Window window)
        {
            var section = new VisualElement
            {
                style =
                {
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 6
                }
            };
            section.SetupRadius(EcsDebugV2Theme.CardRadius);
            section.SetupBorder(Color.clear, 0);

            var pickerContainer = new VisualElement
            {
                name = "add-comp-picker",
                style = { display = DisplayStyle.None }
            };
            var existing = new HashSet<string>(entity.Components.Select(c => c.Name));
            var available = window.Provider.AvailableComponentTypes.Where(c => !existing.Contains(c)).ToList();

            var pickerHeader = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.SpaceBetween,
                    marginBottom = 4
                }
            };
            pickerHeader.Add(new Label("Pick component")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1
                }
            });
            var closeBtn = new Button(() => pickerContainer.style.display = DisplayStyle.None)
            {
                text = "\u2715",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText,
                    backgroundColor = Color.clear,
                    paddingLeft = 2,
                    paddingRight = 2,
                    paddingTop = 0,
                    paddingBottom = 0
                }
            };
            closeBtn.SetupBorder(Color.clear, 0);
            pickerHeader.Add(closeBtn);
            pickerContainer.Add(pickerHeader);

            var tagsRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap
                }
            };
            foreach (var compName in available)
            {
                var name = compName;
                var tag = new Button(() =>
                {
                    window.AddComponent(entity.Id, name);
                    pickerContainer.style.display = DisplayStyle.None;
                })
                {
                    text = "+ " + name,
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Micro,
                        color = EcsDebugV2Theme.Lime,
                        backgroundColor = EcsDebugV2Theme.Lime.WithAlpha(0.1f),
                        paddingLeft = 6,
                        paddingRight = 6,
                        paddingTop = 2,
                        paddingBottom = 2,
                        marginRight = 3,
                        marginBottom = 3
                    }
                };
                tag.SetupRadius(EcsDebugV2Theme.BorderRadius);
                tag.SetupBorder(EcsDebugV2Theme.Lime.WithAlpha(0.3f));
                tagsRow.Add(tag);
            }
            pickerContainer.Add(tagsRow);
            section.Add(pickerContainer);

            var addBtn = new Button(() =>
            {
                bool showing = pickerContainer.style.display == DisplayStyle.Flex;
                pickerContainer.style.display = showing ? DisplayStyle.None : DisplayStyle.Flex;
            })
            {
                text = "+ Add Component",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Foreground,
                    backgroundColor = EcsDebugV2Theme.Panel,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 4,
                    paddingBottom = 4,
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            addBtn.SetupBorder(Color.clear, 0);
            if (available.Count == 0) addBtn.SetEnabled(false);
            section.Add(addBtn);
            return section;
        }

        private static void DrawArchetypeInspector(VisualElement panel, ArchetypeInfo archetype, EcsDebugV2Window window)
        {
            var header = EcsDebugV2Theme.CreateHeaderRow();
            header.Add(new Label($"archetype #{archetype.Id}")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Orange,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 6
                }
            });
            header.Add(new Label(string.Join(" + ", archetype.Components))
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Foreground
                }
            });
            header.Add(new Label($"{archetype.EntityCount} entities \u00B7 {archetype.ChunkCount} chunks")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = UnityEngine.UIElements.Length.Auto()
                }
            });
            panel.Add(header);

            var compSection = new VisualElement
            {
                style =
                {
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 8,
                    paddingBottom = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder
                }
            };
            compSection.Add(new Label("Component types")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1,
                    marginBottom = 4
                }
            });
            var compTags = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap }
            };
            foreach (var c in archetype.Components)
                compTags.Add(EcsDebugV2Theme.CreateFilterTag(c, true));
            compSection.Add(compTags);
            panel.Add(compSection);

            var entLabel = new Label($"Entities ({archetype.EntityCount})")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1,
                    paddingLeft = 10,
                    paddingTop = 6,
                    paddingBottom = 4
                }
            };
            panel.Add(entLabel);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "inspector-scroll",
                style =
                {
                    flexGrow = 1,
                    overflow = Overflow.Hidden
                }
            };

            var matchSet = new HashSet<string>(archetype.Components);
            foreach (var e in window.Entities)
            {
                var names = e.Components.Select(c => c.Name).ToList();
                if (names.Count != archetype.Components.Count || !names.All(n => matchSet.Contains(n)))
                    continue;

                var row = EcsDebugV2Theme.CreateRow();
                row.style.borderBottomColor = EcsDebugV2Theme.PanelBorder.WithAlpha(0.4f);
                row.Add(new Label($"#{e.Id}")
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = EcsDebugV2Theme.TypeEntity,
                        width = 70,
                        flexShrink = 0
                    }
                });
                row.Add(new Label(e.Name)
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = EcsDebugV2Theme.Foreground,
                        flexGrow = 1
                    }
                });
                var aliveLabel = new Label(e.Alive ? "\u25CF alive" : "\u25CF dead")
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = e.Alive ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.Red,
                        width = 70,
                        flexShrink = 0
                    }
                };
                row.Add(aliveLabel);
                var capturedId = e.Id;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    window.SelectEntityFromArchetype(capturedId);
                });
                scroll.Add(row);
            }
            panel.Add(scroll);
        }

        private static void DrawQueryInspector(VisualElement panel, QueryInfo query, EcsDebugV2Window window)
        {
            var header = EcsDebugV2Theme.CreateHeaderRow();
            header.Add(new Label(query.Name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Lime,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            });
            header.Add(new Label($"{query.Matched} entities \u00B7 {query.LastRunMs:F2} ms")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = UnityEngine.UIElements.Length.Auto()
                }
            });
            panel.Add(header);

            var filterSection = new VisualElement
            {
                style =
                {
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 8,
                    paddingBottom = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder
                }
            };
            filterSection.Add(CreateFilterRow("With", query.With, true));
            filterSection.Add(CreateFilterRow("Without", query.Without, false));
            panel.Add(filterSection);

            var matchingLabel = new Label("Matching archetypes")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1,
                    paddingLeft = 10,
                    paddingTop = 6,
                    paddingBottom = 4
                }
            };
            panel.Add(matchingLabel);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "inspector-scroll",
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 10,
                    paddingRight = 10,
                    overflow = Overflow.Hidden
                }
            };

            foreach (var arch in window.Archetypes)
            {
                var archSet = new HashSet<string>(arch.Components);
                bool match = query.With.All(w => archSet.Contains(w)) && query.Without.All(w => !archSet.Contains(w));
                if (!match) continue;

                var card = EcsDebugV2Theme.CreateCard();
                card.style.paddingLeft = 8;
                card.style.paddingRight = 8;
                card.style.paddingTop = 6;
                card.style.paddingBottom = 6;
                card.style.marginBottom = 4;

                var topRow = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 4
                    }
                };
                topRow.Add(new Label($"#{arch.Id}")
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = EcsDebugV2Theme.Orange
                    }
                });
                topRow.Add(new Label($"{arch.EntityCount} entities")
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Micro,
                        color = EcsDebugV2Theme.Yellow,
                        marginLeft = UnityEngine.UIElements.Length.Auto()
                    }
                });
                card.Add(topRow);

                var tagRow = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap }
                };
                foreach (var c in arch.Components)
                {
                    bool isWith = query.With.Contains(c);
                    var tag = new Label(c)
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Mini,
                            color = isWith ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText,
                            backgroundColor = isWith
                                ? EcsDebugV2Theme.Lime.WithAlpha(0.1f)
                                : EcsDebugV2Theme.PanelElevated,
                            paddingLeft = 4,
                            paddingRight = 4,
                            paddingTop = 1,
                            paddingBottom = 1,
                            marginRight = 3,
                            marginBottom = 2
                        }
                    };
                    tag.SetupRadius(EcsDebugV2Theme.BorderRadius);
                    tag.SetupBorder(isWith
                        ? EcsDebugV2Theme.Lime.WithAlpha(0.3f)
                        : EcsDebugV2Theme.PanelBorder);
                    tagRow.Add(tag);
                }
                card.Add(tagRow);
                scroll.Add(card);
            }
            panel.Add(scroll);
        }

        private static void DrawResourceInspector(VisualElement panel, ResourceInfo resource)
        {
            var header = EcsDebugV2Theme.CreateHeaderRow();
            header.Add(new Label(resource.Name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Yellow,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 6
                }
            });
            header.Add(new Label(resource.Type)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText
                }
            });
            panel.Add(header);

            var content = new VisualElement
            {
                style =
                {
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 10,
                    paddingBottom = 10
                }
            };

            if (resource.IsScalar)
            {
                var valueCard = EcsDebugV2Theme.CreateCard();
                valueCard.style.paddingLeft = 10;
                valueCard.style.paddingRight = 10;
                valueCard.style.paddingTop = 6;
                valueCard.style.paddingBottom = 6;
                valueCard.Add(CreateValueDisplay(resource.ScalarValue));
                content.Add(valueCard);
            }
            else
            {
                var valueCard = EcsDebugV2Theme.CreateCard();
                foreach (var kv in resource.Value)
                {
                    var row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            paddingLeft = 10,
                            paddingRight = 10,
                            paddingTop = 4,
                            paddingBottom = 4,
                            borderBottomWidth = 1,
                            borderBottomColor = EcsDebugV2Theme.PanelBorder.WithAlpha(0.4f)
                        }
                    };
                    row.Add(new Label(kv.Key)
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.MutedText,
                            width = 150,
                            flexShrink = 0
                        }
                    });
                    row.Add(CreateValueDisplay(kv.Value));
                    valueCard.Add(row);
                }
                content.Add(valueCard);
            }
            panel.Add(content);
        }

        private static VisualElement CreateValueDisplay(FieldValue value)
        {
            switch (value.Type)
            {
                case FieldValueType.Number:
                    return new Label(value.NumberVal.ToString("G"))
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.TypeNumber
                        }
                    };
                case FieldValueType.Bool:
                    return new Label(value.BoolVal.ToString())
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = value.BoolVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.Red,
                            unityFontStyleAndWeight = FontStyle.Bold
                        }
                    };
                case FieldValueType.String:
                    return new Label($"\"{value.StringVal}\"")
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.TypeString
                        }
                    };
                case FieldValueType.EntityRef:
                    return new Label($"\u2192 #{value.EntityRefVal}")
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.TypeEntity
                        }
                    };
                default:
                    return new Label("\u2014")
                    {
                        style = { color = EcsDebugV2Theme.MutedText }
                    };
            }
        }

        private static VisualElement CreateFilterRow(string label, List<string> items, bool positive)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart,
                    marginBottom = 4
                }
            };
            row.Add(new Label(label)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1,
                    width = 60,
                    flexShrink = 0,
                    marginTop = 2
                }
            });
            var tags = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap }
            };
            if (items.Count == 0)
            {
                tags.Add(new Label("\u2014")
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = EcsDebugV2Theme.MutedText
                    }
                });
            }
            foreach (var item in items)
            {
                var prefix = positive ? "+" : "\u2212";
                tags.Add(EcsDebugV2Theme.CreateFilterTag(prefix + item, positive));
            }
            row.Add(tags);
            return row;
        }

    }
}
#endif
