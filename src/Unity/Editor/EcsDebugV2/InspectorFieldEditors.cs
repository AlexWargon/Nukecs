#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class InspectorFieldEditors
    {
        const string NAME = "unity-text-input";
        public static VisualElement CreateNumberEditor(FieldValue value, Action<FieldValue> onChange)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1
                }
            };

            var tf = new TextField
            {
                value = value.NumberVal.ToString("G"),
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.TypeNumber,
                    backgroundColor = EcsDebugV2Theme.Background,
                    flexGrow = 1,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleRight
                }
            };
            tf.SetupBorder(Color.clear, 0);
            
            tf.Q(NAME).style.backgroundColor = Color.clear;
            tf.Q(NAME).SetupBorder(Color.clear, 0);
            tf.Q(NAME).style.paddingLeft = 2;
            tf.Q(NAME).style.paddingRight = 2;
            //tf.Q(NAME).style.unityTextAlign = TextAnchor.MiddleRight;

            tf.RegisterValueChangedCallback(evt =>
            {
                if (double.TryParse(evt.newValue, out var n))
                {
                    onChange(FieldValue.FromNumber(n));
                }
            });

            float dragStart = 0;
            double dragBaseVal = 0;
            bool isDragging = false;
            tf.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    dragStart = evt.position.x;
                    dragBaseVal = value.NumberVal;
                    isDragging = true;
                    tf.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });
            tf.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (isDragging)
                {
                    var delta = evt.position.x - dragStart;
                    var speed = evt.shiftKey ? 0.01 : (evt.ctrlKey ? 10.0 : 0.5);
                    var newVal = dragBaseVal + delta * speed;
                    tf.SetValueWithoutNotify(newVal.ToString("G"));
                    onChange(FieldValue.FromNumber(newVal));
                }
            });
            tf.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 1 && isDragging)
                {
                    isDragging = false;
                    tf.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            container.Add(tf);
            return container;
        }

        public static VisualElement CreateBoolEditor(FieldValue value, Action<FieldValue> onChange)
        {
            var isOn = value.BoolVal;
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1
                }
            };

            var track = new VisualElement
            {
                style =
                {
                    width = 36,
                    height = 18,
                    backgroundColor = isOn ? EcsDebugV2Theme.LimeA03 : EcsDebugV2Theme.PanelBorder,
                    flexShrink = 0,
                    marginRight = 6,
                    overflow = Overflow.Hidden
                }
            };
            track.name = "bool-track";
            track.SetupRadius(9);
            track.SetupBorder(isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder);
            track.style.transitionDuration = new List<TimeValue> { new TimeValue(0.15f, TimeUnit.Second) };
            track.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("background-color") };
            track.RegisterCallback<MouseEnterEvent>(_ => track.style.opacity = 0.85f);
            track.RegisterCallback<MouseLeaveEvent>(_ => track.style.opacity = 1f);

            var thumb = new VisualElement
            {
                style =
                {
                    width = 14,
                    height = 14,
                    backgroundColor = isOn ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText,
                    marginTop = 2,
                    marginLeft = isOn ? 20 : 2,
                    flexShrink = 0
                }
            };
            thumb.name = "bool-thumb";
            thumb.SetupRadius(7);
            thumb.style.transitionDuration = new List<TimeValue> { new TimeValue(0.15f, TimeUnit.Second) };
            thumb.style.transitionProperty = new List<StylePropertyName>
            {
                new StylePropertyName("margin-left"),
                new StylePropertyName("background-color")
            };
            track.Add(thumb);

            var label = new Label(isOn.ToString())
            {
                name = "bool-label",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.FieldName,
                    color = isOn ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText
                }
            };
            container.Add(track);
            container.Add(label);

            var currentState = isOn;
            container.RegisterCallback<ClickEvent>(_ =>
            {
                var newVal = !currentState;
                currentState = newVal;
                onChange(FieldValue.FromBool(newVal));

                thumb.style.marginLeft = newVal ? 20 : 2;
                thumb.style.backgroundColor = newVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
                track.style.backgroundColor = newVal ? EcsDebugV2Theme.LimeA03 : EcsDebugV2Theme.PanelBorder;
                track.style.borderTopColor = newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                track.style.borderBottomColor = newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                track.style.borderLeftColor = newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                track.style.borderRightColor = newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                label.text = newVal.ToString();
                label.style.color = newVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
            });

            return container;
        }

        public static VisualElement CreateStringEditor(FieldValue value, Action<FieldValue> onChange)
        {
            var wrapper = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    overflow = Overflow.Hidden
                }
            };

            var tf = new TextField
            {
                value = value.StringVal,
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.TypeString,
                    backgroundColor = EcsDebugV2Theme.Background,
                    flexGrow = 1
                }
            };
            tf.SetupBorder(Color.clear, 0);
            tf.Q(NAME).style.backgroundColor = Color.clear;
            tf.Q(NAME).SetupBorder(Color.clear, 0);
            tf.Q(NAME).style.paddingLeft = 2;
            tf.Q(NAME).style.paddingRight = 2;
            tf.Q(NAME).style.borderBottomWidth = 1;
            tf.Q(NAME).style.borderBottomColor = EcsDebugV2Theme.PanelBorderA04;

            var underline = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = EcsDebugV2Theme.TypeString,
                    marginTop = 0,
                    opacity = 0
                }
            };

            tf.RegisterValueChangedCallback(evt =>
                onChange(FieldValue.FromString(evt.newValue)));

            tf.RegisterCallback<FocusInEvent>(_ => underline.style.opacity = 1);
            tf.RegisterCallback<FocusOutEvent>(_ => underline.style.opacity = 0);

            wrapper.Add(tf);
            wrapper.Add(underline);
            return wrapper;
        }

        public static VisualElement CreateEntityRefEditor(FieldValue value, Action<FieldValue> onChange, EcsDebugV2Window window)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1
                }
            };

            var arrow = new Label("\u2192")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.TypeEntity,
                    marginRight = 4
                }
            };
            container.Add(arrow);
        
            var link = new Label($"#{value.EntityRefVal}")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.TypeEntity,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = EcsDebugV2Theme.Background
                }
            };
            link.RegisterCallback<MouseEnterEvent>(_ => link.style.opacity = 0.7f);
            link.RegisterCallback<MouseLeaveEvent>(_ => link.style.opacity = 1f);
            link.RegisterCallback<ClickEvent>(_ =>
            {
                if (window != null && value.EntityRefVal > 0)
                    window.SelectEntity(value.EntityRefVal);
            });
            container.Add(link);
            Button editBtn = default;
            editBtn = new Button(() =>
            {
                var currentId = value.EntityRefVal;
                var popup = new TextField
                {
                    value = currentId.ToString(),
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = EcsDebugV2Theme.TypeEntity,
                        backgroundColor = EcsDebugV2Theme.Background,
                        width = 60
                    }
                };
                popup.SetupBorder(EcsDebugV2Theme.TypeEntity, 1);
                popup.Q("unity-text-input").style.backgroundColor = Color.clear;
                popup.RegisterValueChangedCallback(evt =>
                {
                    if (int.TryParse(evt.newValue, out var n))
                    {
                        onChange(FieldValue.FromEntityRef(n));
                        link.text = $"#{n}";
                    }
                });
                popup.RegisterCallback<FocusOutEvent>(_ =>
                {
                    container.Remove(popup);
                    link.style.display = DisplayStyle.Flex;
                    editBtn.style.display = DisplayStyle.Flex;
                });
                link.style.display = DisplayStyle.None;
                editBtn.style.display = DisplayStyle.None;
                container.Add(popup);
                popup.Focus();
            })
            {
                text = "\u270E",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.MutedText,
                    backgroundColor = EcsDebugV2Theme.Background,
                    paddingLeft = 2,
                    paddingRight = 2,
                    paddingTop = 0,
                    paddingBottom = 0,
                    marginLeft = 4
                }
            };
            editBtn.SetupBorder(Color.clear, 0);
            container.Add(editBtn);

            return container;
        }

        public static VisualElement CreateVectorEditor(
            string[] subFieldNames, Color[] subFieldColors,
            FieldValue[] subValues, Action<int, FieldValue> onSubChange)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1,
                    flexWrap = Wrap.Wrap
                }
            };

            for (int i = 0; i < subFieldNames.Length; i++)
            {
                var idx = i;
                var color = i < subFieldColors.Length ? subFieldColors[i] : EcsDebugV2Theme.TypeNumber;

                var subLabel = new Label(subFieldNames[i])
                {
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.FieldName,
                        color = color,
                        marginRight = 2,
                        flexShrink = 0
                    }
                };
                container.Add(subLabel);

                var subTf = new TextField
                {
                    value = (i < subValues.Length ? subValues[i].NumberVal : 0).ToString("G"),
                    style =
                    {
                        
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = EcsDebugV2Theme.Font.Small,
                        color = color,
                        backgroundColor = EcsDebugV2Theme.Background,
                        width = 40,
                        marginRight = 4,
                        flexShrink = 0
                    }
                };
                subTf.SetupBorder(Color.clear, 0);
                subTf.Q(NAME).style.backgroundColor = Color.clear;
                subTf.Q(NAME).SetupBorder(Color.clear, 0);
                subTf.Q(NAME).style.paddingLeft = 2;
                subTf.Q(NAME).style.paddingRight = 2;

                subTf.RegisterValueChangedCallback(evt =>
                {
                    if (double.TryParse(evt.newValue, out var n))
                        onSubChange(idx, FieldValue.FromNumber(n));
                });

                container.Add(subTf);
            }

            return container;
        }

    }
}
#endif
