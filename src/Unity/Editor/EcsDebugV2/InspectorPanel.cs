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

        public static void UpdateValues(VisualElement panel, EcsDebugV2Window window, long now)
        {
            switch (window.currentTab)
            {
                case TabKey.Entities:
                    if (window.selectedEntityId.HasValue && window.selectedEntityDetails != null)
                        UpdateEntityFieldValues(window.selectedEntityDetails, window, now);
                    break;
            }
        }

        public static void ClearDrawerCache()
        {
            ComponentCardDrawer.ClearCache();
        }

        private static void UpdateEntityFieldValues(EntityInfo entity, EcsDebugV2Window window, long now)
        {
            if (entity.components == null) return;

            var count = ComponentCardDrawer.ActiveCount;
            for (int i = 0; i < count; i++)
            {
                if (i >= entity.components.Count) break;
                ComponentCardDrawer.GetActive(i).UpdateValues(entity.components[i], now);
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
            var idLabel = new Label($"#{entity.id}")
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
            var nameLabel = new Label(entity.name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Body,
                    color = EcsDebugV2Theme.Foreground,
                    marginRight = 6
                }
            };
            header.Add(nameLabel);
            header.Add(EcsDebugV2Theme.CreateBadge(entity.archetype.ToUpper(),
                EcsDebugV2Theme.OrangeA015, EcsDebugV2Theme.Orange, EcsDebugV2Theme.Font.Mini));

            var meta = new Label($"{(entity.components?.Count ?? 0)} components")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = UnityEngine.UIElements.Length.Auto()
                }
            };
            header.Add(meta);

            var destroyBtn = EcsDebugV2Theme.CreateActionBtn("destroy", EcsDebugV2Theme.Red, () => window.DestroyEntity(entity.id));
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

            ComponentCardDrawer.ResetActive();
            var compCount = entity.components?.Count ?? 0;
            for (int ci = 0; ci < compCount; ci++)
            {
                var comp = entity.components[ci];
                var drawer = ComponentCardDrawer.GetOrCreate(comp);
                drawer.Bind(entity.id, comp.Name, window, ci, comp);
                scroll.Add(drawer.card);
                ComponentCardDrawer.AddActive(drawer);
            }

            scroll.Add(DrawAddComponentSection(entity, window));
            panel.Add(scroll);
        }

        private static VisualElement DrawAddComponentSection(EntityInfo entity, EcsDebugV2Window window)
        {
            var section = new VisualElement
            {
                name = "add-comp-section",
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
            if (entity.components != null)
                foreach (var c in entity.components) existing.Add(c.Name);
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
                    window.AddComponent(entity.id, name);
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
            header.Add(new Label($"archetype #{archetype.id}")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Orange,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 6
                }
            });
            header.Add(new Label(string.Join(" + ", archetype.components))
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Foreground
                }
            });
            header.Add(new Label($"{archetype.entityCount} entities \u00B7 {archetype.chunkCount} chunks")
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
            foreach (var c in archetype.components)
                compTags.Add(EcsDebugV2Theme.CreateFilterTag(c, true));
            compSection.Add(compTags);
            panel.Add(compSection);

            var entLabel = new Label($"Entities ({archetype.entityCount})")
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
            foreach (var c in archetype.components) _archMatchSet.Add(c);
            foreach (var entityId in archetype.entityIds)
            {
                EntityInfo e;
                if (!window.entityMap.TryGetValue(entityId, out e)) continue;

                var row = EcsDebugV2Theme.CreateRow();
                row.style.borderBottomColor = EcsDebugV2Theme.PanelBorderA04;
                row.Add(new Label($"#{e.id}")
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = EcsDebugV2Theme.TypeEntity,
                        width = 70,
                        flexShrink = 0
                    }
                });
                row.Add(new Label(e.name)
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = EcsDebugV2Theme.Foreground,
                        flexGrow = 1
                    }
                });
                var aliveLabel = new Label(e.alive ? "\u25CF alive" : "\u25CF dead")
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = e.alive ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.Red,
                        width = 70,
                        flexShrink = 0
                    }
                };
                row.Add(aliveLabel);
                var capturedId = e.id;
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
            header.Add(new Label(query.name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Lime,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            });
            header.Add(new Label($"{query.matched} entities \u00B7 {query.lastRunMs:F2} ms")
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
            filterSection.Add(CreateFilterRow("With", query.with, true));
            filterSection.Add(CreateFilterRow("Without", query.without, false));
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
                foreach (var c in arch.components) _queryMatchSet.Add(c);
                bool match = true;
                for (int wi = 0; wi < query.with.Count; wi++)
                {
                    if (!_queryMatchSet.Contains(query.with[wi]))
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    for (int wi = 0; wi < query.without.Count; wi++)
                    {
                        if (_queryMatchSet.Contains(query.without[wi]))
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
                topRow.Add(new Label($"#{arch.id}")
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = EcsDebugV2Theme.Orange
                    }
                });
                topRow.Add(new Label($"{arch.entityCount} entities")
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
                foreach (var c in arch.components)
                {
                    bool isWith = query.with.Contains(c);
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
            header.Add(new Label(resource.name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Yellow,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 6
                }
            });
            header.Add(new Label(resource.type)
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

            if (resource.isScalar)
            {
                var valueCard = EcsDebugV2Theme.CreateCard();
                valueCard.style.paddingLeft = 10;
                valueCard.style.paddingRight = 10;
                valueCard.style.paddingTop = 6;
                valueCard.style.paddingBottom = 6;
                valueCard.Add(CreateValueDisplay(resource.scalarValue));
                content.Add(valueCard);
            }
            else
            {
                var valueCard = EcsDebugV2Theme.CreateCard();
                foreach (var kv in resource.value)
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
                case FieldValueType.Enum:
                {
                    var name = value.EnumNames != null && value.EnumSelectedIndex >= 0 &&
                               value.EnumSelectedIndex < value.EnumNames.Length
                        ? value.EnumNames[value.EnumSelectedIndex]
                        : value.EnumRawValue.ToString();
                    return new Label($"{name}")
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.Lime
                        }
                    };
                }
                case FieldValueType.ObjectRef:
                {
                    var display = string.IsNullOrEmpty(value.ObjectName) || value.ObjectName == "null"
                        ? $"{value.ObjectTypeName}: null"
                        : $"{value.ObjectTypeName}: {value.ObjectName}";
                    return new Label(display)
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.MutedText
                        }
                    };
                }
                case FieldValueType.ComponentArray:
                {
                    var elemName = string.IsNullOrEmpty(value.ArrayElementTypeName)
                        ? "?"
                        : value.ArrayElementTypeName;
                    return new Label($"{elemName}[{value.ArrayLength}]")
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.Small,
                            color = EcsDebugV2Theme.Orange
                        }
                    };
                }
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
