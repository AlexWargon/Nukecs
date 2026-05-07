#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class InspectorPanel
    {
        private struct FieldRowData
        {
            public string ChangeKey;
            public VisualElement Editor;
            public int CompIndex;
            public int FieldIndex;
            public double LastNumberVal;
            public string LastStringVal;
            public bool LastBoolVal;
            public int LastEntityRefVal;
        }

        private static HashSet<string> _archMatchSet = new HashSet<string>();
        private static HashSet<string> _queryMatchSet = new HashSet<string>();

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
                    backgroundColor = EcsDebugV2Theme.BgA04
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

            switch (window.currentTab)
            {
                case TabKey.Entities:
                    if (window.selectedEntityId.HasValue && window.selectedEntityDetails != null)
                        DrawEntityInspector(panel, window.selectedEntityDetails, window);
                    else
                        DrawEmptyState(panel);
                    break;
                case TabKey.Archetypes:
                    ArchetypeInfo arch;
                    if (window.selectedArchetypeId.HasValue && window.archetypeMap.TryGetValue(window.selectedArchetypeId.Value, out arch))
                        DrawArchetypeInspector(panel, arch, window);
                    else
                        DrawEmptyState(panel);
                    break;
                case TabKey.Queries:
                    QueryInfo query;
                    if (window.selectedQueryId != null && window.queryMap.TryGetValue(window.selectedQueryId, out query))
                        DrawQueryInspector(panel, query, window);
                    else
                        DrawEmptyState(panel);
                    break;
                case TabKey.Resources:
                    ResourceInfo res;
                    if (window.selectedResourceName != null && window.resourceMap.TryGetValue(window.selectedResourceName, out res))
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
            switch (window.currentTab)
            {
                case TabKey.Entities:
                    if (window.selectedEntityId.HasValue && window.selectedEntityDetails != null)
                        UpdateEntityFieldValues(panel, window.selectedEntityDetails, window);
                    break;
            }
        }

        private static void UpdateEntityFieldValues(VisualElement panel, EntityInfo entity, EcsDebugV2Window window)
        {
            var scroll = panel.Q("inspector-scroll");
            if (scroll == null) return;
            if (entity.Components == null) return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            for (int ci = 0; ci < scroll.childCount; ci++)
            {
                var card = scroll[ci];
                if (card.name == "add-comp-section") continue;

                for (int fi = 1; fi < card.childCount; fi++)
                {
                    var row = card[fi];
                    var obj = row.userData;
                    if (!(obj is FieldRowData)) continue;
                    var fd = (FieldRowData)obj;

                    if (fd.CompIndex >= entity.Components.Count) continue;
                    var comp = entity.Components[fd.CompIndex];
                    if (fd.FieldIndex >= comp.Fields.Count) continue;
                    var fv = comp.Fields[fd.FieldIndex].Value;

                    var editor = fd.Editor;
                    if (editor is TextField tf)
                    {
                        try
                        {
                            var focused = tf.panel?.focusController?.focusedElement as VisualElement;
                            if (focused != null && tf.Contains(focused))
                                goto UpdateHighlight;
                        }
                        catch { }

                        switch (fv.Type)
                        {
                            case FieldValueType.Number:
                                if (Math.Abs(fv.NumberVal - fd.LastNumberVal) > 0.0001)
                                {
                                    fd.LastNumberVal = fv.NumberVal;
                                    row.userData = fd;
                                    tf.SetValueWithoutNotify(fv.NumberVal.ToString("G"));
                                }
                                break;
                            case FieldValueType.String:
                                if (fv.StringVal != fd.LastStringVal)
                                {
                                    fd.LastStringVal = fv.StringVal;
                                    row.userData = fd;
                                    tf.SetValueWithoutNotify(fv.StringVal);
                                }
                                break;
                            case FieldValueType.EntityRef:
                                if (fv.EntityRefVal != fd.LastEntityRefVal)
                                {
                                    fd.LastEntityRefVal = fv.EntityRefVal;
                                    row.userData = fd;
                                    tf.SetValueWithoutNotify(fv.EntityRefVal.ToString());
                                }
                                break;
                        }
                    }
                    else if (editor is Button btn && fv.Type == FieldValueType.Bool)
                    {
                        if (fv.BoolVal != fd.LastBoolVal)
                        {
                            fd.LastBoolVal = fv.BoolVal;
                            row.userData = fd;
                            btn.text = fv.BoolVal.ToString();
                            btn.style.color = fv.BoolVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.Red;
                        }
                    }

                    UpdateHighlight:
                    long ts;
                    if (window.changes.TryGetValue(fd.ChangeKey, out ts))
                    {
                        var age = now - ts;
                        row.style.backgroundColor = age < 1200
                            ? EcsDebugV2Theme.YellowA015
                            : (Color)EcsDebugV2Theme.PanelElevated;
                    }
                    else
                    {
                        row.style.backgroundColor = EcsDebugV2Theme.PanelElevated;
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
                EcsDebugV2Theme.OrangeA015, EcsDebugV2Theme.Orange, EcsDebugV2Theme.Font.Mini));

            var meta = new Label($"{(entity.Components?.Count ?? 0)} components")
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

            var compCount = entity.Components?.Count ?? 0;
            for (int ci = 0; ci < compCount; ci++)
                scroll.Add(DrawComponentCard(entity, entity.Components[ci], window, ci));

            scroll.Add(DrawAddComponentSection(entity, window));
            panel.Add(scroll);
        }

        private static VisualElement DrawComponentCard(EntityInfo entity, ComponentInfo comp, EcsDebugV2Window window, int compIdx)
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

            for (int fi = 0; fi < comp.Fields.Count; fi++)
            {
                var kv = comp.Fields[fi];
                var row = DrawFieldRow(entity.Id, comp.Name, kv.Key, kv.Value, window, compIdx, fi);
                card.Add(row);
            }
            return card;
        }

        private static VisualElement DrawFieldRow(int entityId, string compName, string fieldKey, FieldValue value, EcsDebugV2Window window, int compIndex, int fieldIndex)
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
            if (window.changes.TryGetValue(changeKey, out ts))
            {
                var age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ts;
                if (age < 1200)
                    row.style.backgroundColor = EcsDebugV2Theme.YellowA015;
            }

            var editor = CreateFieldEditor(value, (newVal) =>
            {
                window.SetFieldValue(entityId, compName, fieldKey, newVal);
            });
            editor.name = $"editor-{compName}-{fieldKey}";
            editor.style.flexGrow = 1;
            row.Add(editor);

            row.userData = new FieldRowData
            {
                ChangeKey = changeKey,
                Editor = editor,
                CompIndex = compIndex,
                FieldIndex = fieldIndex,
                LastNumberVal = value.Type == FieldValueType.Number ? value.NumberVal : 0,
                LastStringVal = value.Type == FieldValueType.String ? value.StringVal : null,
                LastBoolVal = value.Type == FieldValueType.Bool && value.BoolVal,
                LastEntityRefVal = value.Type == FieldValueType.EntityRef ? value.EntityRefVal : 0
            };
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
            var existing = new HashSet<string>();
            if (entity.Components != null)
                foreach (var c in entity.Components) existing.Add(c.Name);
            var available = new List<string>();
            foreach (var c in window.provider.AvailableComponentTypes)
                if (!existing.Contains(c)) available.Add(c);

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
                        backgroundColor = EcsDebugV2Theme.LimeA01,
                        paddingLeft = 6,
                        paddingRight = 6,
                        paddingTop = 2,
                        paddingBottom = 2,
                        marginRight = 3,
                        marginBottom = 3
                    }
                };
                tag.SetupRadius(EcsDebugV2Theme.BorderRadius);
                tag.SetupBorder(EcsDebugV2Theme.LimeA03);
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

            _archMatchSet.Clear();
            foreach (var c in archetype.Components) _archMatchSet.Add(c);
            foreach (var entityId in archetype.EntityIds)
            {
                EntityInfo e;
                if (!window.entityMap.TryGetValue(entityId, out e)) continue;

                var row = EcsDebugV2Theme.CreateRow();
                row.style.borderBottomColor = EcsDebugV2Theme.PanelBorderA04;
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

            foreach (var arch in window.archetypes)
            {
                _queryMatchSet.Clear();
                foreach (var c in arch.Components) _queryMatchSet.Add(c);
                bool match = true;
                for (int wi = 0; wi < query.With.Count; wi++)
                {
                    if (!_queryMatchSet.Contains(query.With[wi]))
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    for (int wi = 0; wi < query.Without.Count; wi++)
                    {
                        if (_queryMatchSet.Contains(query.Without[wi]))
                        {
                            match = false;
                            break;
                        }
                    }
                }
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
                                ? EcsDebugV2Theme.LimeA01
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
                        ? EcsDebugV2Theme.LimeA03
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
                            borderBottomColor = EcsDebugV2Theme.PanelBorderA04
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
