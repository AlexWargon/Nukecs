#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public static class DashboardEntityInspector
    {
        public static void DrawInspector(ScrollView container, NukecsDashboardWindow window)
        {
            container.Clear();
            if (!window.SelectedEntityId.HasValue) return;

            var world = window.World;
            if (!world.IsAlive) return;

            var entityId = window.SelectedEntityId.Value;
            var e = world.GetEntity(entityId);
            if (e == Entity.Null) return;

            ref var arch = ref world.UnsafeWorldRef.GetEntityArchetypePtr(entityId).Ref;

            var eName = e.Has<Name>() ? e.Get<Name>().value.Value : "";
            var displayTitle = string.IsNullOrEmpty(eName) ? $"Entity {entityId}" : eName;

            var titleCard = NukecsDashboardWindow.CreateGlowCard(DashboardTheme.Separator, 10);
            titleCard.style.paddingTop = 10;
            titleCard.style.paddingBottom = 10;
            titleCard.style.paddingLeft = 12;
            titleCard.style.paddingRight = 12;
            titleCard.style.marginBottom = 8;
            titleCard.style.flexDirection = FlexDirection.Row;
            titleCard.style.alignItems = Align.Center;
            titleCard.style.flexWrap = Wrap.Wrap;

            var titleLabel = new Label(displayTitle)
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.TitleLarge,
                    color = DashboardTheme.TextPrimary,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            titleCard.Add(titleLabel);

            var idBadge = DashboardStyles.PillBadge($"#:{entityId:D7}",
                DashboardTheme.AccentCyan.WithAlpha(0.2f), DashboardTheme.AccentCyan, DashboardTheme.FontSize.Small);
            idBadge.style.marginLeft = 8;
            titleCard.Add(idBadge);

            var compCount = 0;
            foreach (var _ in arch.types) compCount++;

            var subtitleRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 4
                }
            };

            var subtitleLabel = new Label($"Archetype: {arch.hashId}")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = DashboardTheme.AccentForArchetype(arch.hashId),
                    marginRight = 8
                }
            };
            subtitleRow.Add(subtitleLabel);

            var compBadge = new Label($"{compCount} Components")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = DashboardTheme.TextSecondary
                }
            };
            subtitleRow.Add(compBadge);

            titleCard.Add(subtitleRow);
            container.Add(titleCard);

            var existingTypes = new HashSet<int>();
            foreach (var ti in arch.types) existingTypes.Add(ti);

            foreach (var typeIndex in arch.types)
            {
                var boxedComponent = arch.GetObject(entityId, typeIndex);
                if (boxedComponent == null) continue;

                var typeName = boxedComponent.GetType().Name;
                var accentColor = DashboardTheme.AccentForType(typeName);
                var typeData = ComponentTypeMap.GetComponentType(typeIndex);

                var card = new VisualElement
                {
                    style =
                    {
                        backgroundColor = DashboardTheme.BgCard,
                        borderTopLeftRadius = 8,
                        borderTopRightRadius = 8,
                        borderBottomLeftRadius = 8,
                        borderBottomRightRadius = 8,
                        borderTopWidth = 0,
                        borderBottomWidth = 0,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        overflow = Overflow.Hidden,
                        position = Position.Relative,
                        marginBottom = 6
                    }
                };

                var accentBar = new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = 0, top = 0, bottom = 0,
                        width = 5,
                        backgroundColor = accentColor,
                        borderTopLeftRadius = 8,
                        borderBottomLeftRadius = 8
                    }
                };
                card.Add(accentBar);

                var headerRow = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingLeft = 12,
                        paddingRight = 8,
                        paddingTop = 6,
                        paddingBottom = 2
                    }
                };

                var isExpanded = window.GetFoldoutState(typeName);
                var capturedTypeName = typeName;

                var foldoutBtn = new Button(() =>
                {
                    var current = window.GetFoldoutState(capturedTypeName);
                    window.FoldoutStates[capturedTypeName] = !current;
                    DrawInspector(container, window);
                })
                {
                    text = isExpanded ? "\u25BC" : "\u25BA",
                    style =
                    {
                        fontSize = 9,
                        width = 20,
                        height = 20,
                        borderTopLeftRadius = 6,
                        borderTopRightRadius = 6,
                        borderBottomLeftRadius = 6,
                        borderBottomRightRadius = 6,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        borderTopWidth = 0,
                        borderBottomWidth = 0,
                        backgroundColor = Color.clear,
                        color = DashboardTheme.TextSecondary,
                        paddingTop = 0,
                        paddingBottom = 0,
                        paddingLeft = 0,
                        paddingRight = 0,
                        marginRight = 4
                    }
                };

                headerRow.Add(foldoutBtn);

                var typeLabel = new Label(typeName)
                {
                    style =
                    {
                        fontSize = 13,
                        color = DashboardTheme.TextPrimary,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        flexGrow = 1
                    }
                };
                headerRow.Add(typeLabel);

                if (typeData.isTag)
                {
                    var tagBadge = DashboardStyles.PillBadge("TAG",
                        DashboardTheme.AccentGreen.WithAlpha(0.2f), DashboardTheme.AccentGreen,
                        DashboardTheme.FontSize.Micro);
                    tagBadge.style.paddingTop = 1;
                    tagBadge.style.paddingBottom = 1;
                    tagBadge.style.paddingLeft = 6;
                    tagBadge.style.paddingRight = 6;
                    tagBadge.style.borderTopLeftRadius = 8;
                    tagBadge.style.borderTopRightRadius = 8;
                    tagBadge.style.borderBottomLeftRadius = 8;
                    tagBadge.style.borderBottomRightRadius = 8;
                    headerRow.Add(tagBadge);
                }
                else
                {
                    var removeBtn = new Button(() =>
                    {
                        world.GetEntity(entityId).RemoveIndex(typeIndex);
                        DrawInspector(container, window);
                    })
                    {
                        text = "\u2715",
                        style =
                        {
                            fontSize = 10,
                            width = 20,
                            height = 20,
                            borderTopLeftRadius = 10,
                            borderTopRightRadius = 10,
                            borderBottomLeftRadius = 10,
                            borderBottomRightRadius = 10,
                            borderLeftWidth = 0,
                            borderRightWidth = 0,
                            borderTopWidth = 0,
                            borderBottomWidth = 0,
                            backgroundColor = DashboardTheme.BgCard,
                            color = DashboardTheme.TextSecondary,
                            paddingTop = 0,
                            paddingBottom = 0,
                            paddingLeft = 0,
                            paddingRight = 0
                        }
                    };
                    removeBtn.RegisterCallback<MouseEnterEvent>(_ =>
                    {
                        removeBtn.style.color = DashboardTheme.RemoveBtn;
                        removeBtn.style.backgroundColor = DashboardTheme.RemoveBtnHoverBg;
                    });
                    removeBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                    {
                        removeBtn.style.color = DashboardTheme.TextSecondary;
                        removeBtn.style.backgroundColor = DashboardTheme.BgCard;
                    });
                    headerRow.Add(removeBtn);
                }

                card.Add(headerRow);

                if (isExpanded && !typeData.isTag)
                {
                    var contentArea = new VisualElement
                    {
                        style =
                        {
                            paddingLeft = 14,
                            paddingRight = 8,
                            paddingBottom = 6
                        }
                    };

                    var proxy = window.GetOrCreateProxy(typeIndex);
                    proxy.entity = entityId;
                    proxy.boxedComponent = boxedComponent;
                    proxy.typeIndex = typeIndex;
                    contentArea.Add(proxy.imgui);
                    card.Add(contentArea);
                }

                container.Add(card);
            }

            var addCompBtn = new Button(() =>
            {
                var menu = new GenericMenu();
                foreach (var typeIdx in ComponentTypeMap.TypesIndexes)
                {
                    var t = ComponentTypeMap.GetType(typeIdx);
                    if (t == null) continue;
                    if (existingTypes.Contains(typeIdx)) continue;
                    var idx = typeIdx;
                    menu.AddItem(new GUIContent(t.Name), false, () =>
                    {
                        unsafe
                        {
                            world.GetEntity(entityId).worldPointer->ECB.Add(entityId, idx); 
                        }
                    });
                }
                menu.ShowAsContext();
            })
            {
                text = "+ Add Component",
                style =
                {
                    marginTop = 8,
                    borderTopLeftRadius = 14,
                    borderTopRightRadius = 14,
                    borderBottomLeftRadius = 14,
                    borderBottomRightRadius = 14,
                    borderLeftWidth = 2,
                    borderRightWidth = 2,
                    borderTopWidth = 2,
                    borderBottomWidth = 2,
                    borderTopColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    borderBottomColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    backgroundColor = DashboardTheme.BgCard,
                    color = DashboardTheme.AccentPurple,
                    fontSize = DashboardTheme.FontSize.Body,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingTop = 6,
                    paddingBottom = 6
                }
            };
            addCompBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                addCompBtn.style.backgroundColor = DashboardTheme.AccentPurple.WithAlpha(0.15f);
                addCompBtn.style.borderTopColor = DashboardTheme.AccentPurple.WithAlpha(0.6f);
                addCompBtn.style.borderBottomColor = DashboardTheme.AccentPurple.WithAlpha(0.6f);
                addCompBtn.style.borderLeftColor = DashboardTheme.AccentPurple.WithAlpha(0.6f);
                addCompBtn.style.borderRightColor = DashboardTheme.AccentPurple.WithAlpha(0.6f);
            });
            addCompBtn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                addCompBtn.style.backgroundColor = DashboardTheme.BgCard;
                addCompBtn.style.borderTopColor = DashboardTheme.AccentPurple.WithAlpha(0.3f);
                addCompBtn.style.borderBottomColor = DashboardTheme.AccentPurple.WithAlpha(0.3f);
                addCompBtn.style.borderLeftColor = DashboardTheme.AccentPurple.WithAlpha(0.3f);
                addCompBtn.style.borderRightColor = DashboardTheme.AccentPurple.WithAlpha(0.3f);
            });
            container.Add(addCompBtn);

            var destroyBtn = new Button(() =>
            {
                e.Destroy();
                window.SelectEntity(null);
                window.RefreshAll();
            })
            {
                text = "Destroy Entity",
                style =
                {
                    marginTop = 6,
                    borderTopLeftRadius = 14,
                    borderTopRightRadius = 14,
                    borderBottomLeftRadius = 14,
                    borderBottomRightRadius = 14,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    backgroundColor = DashboardTheme.RemoveBtn.WithAlpha(0.15f),
                    color = DashboardTheme.RemoveBtn,
                    fontSize = DashboardTheme.FontSize.Body,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingTop = 6,
                    paddingBottom = 6
                }
            };
            destroyBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                destroyBtn.style.backgroundColor = DashboardTheme.RemoveBtn.WithAlpha(0.25f);
            });
            destroyBtn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                destroyBtn.style.backgroundColor = DashboardTheme.RemoveBtn.WithAlpha(0.15f);
            });
            container.Add(destroyBtn);
        }

        public static void UpdateInspector(ScrollView container, NukecsDashboardWindow window)
        {
            if (!window.SelectedEntityId.HasValue) return;
            var world = window.World;
            if (!world.IsAlive) return;

            var entityId = window.SelectedEntityId.Value;
            var e = world.GetEntity(entityId);
            if (e == Entity.Null) return;

            ref var arch = ref world.UnsafeWorldRef.GetEntityArchetypePtr(entityId).Ref;

            foreach (var typeIndex in arch.types)
            {
                var boxedComponent = arch.GetObject(entityId, typeIndex);
                if (boxedComponent != null && window.ComponentProxies.TryGetValue(typeIndex, out var proxy))
                {
                    if (!EditorGUIUtility.editingTextField)
                        proxy.boxedComponent = arch.GetObject(entityId, typeIndex);

                    proxy.typeIndex = typeIndex;
                    proxy.entity = entityId;
                    proxy.imgui.MarkDirtyRepaint();
                }
            }
        }
    }
}
#endif
