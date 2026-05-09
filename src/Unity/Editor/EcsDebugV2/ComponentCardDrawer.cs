#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public class ComponentCardDrawer
    {
        public readonly VisualElement Card;

        private int _entityId;
        private string _compName;
        private EcsDebugV2Window _window;
        private readonly int _byteSize;

        private struct FieldRow
        {
            public VisualElement Row;
            public VisualElement Editor;
            public TextField MainTextField;
            public TextField[] SubTextFields;
            public string FieldKey;
            public string[] SubFieldKeys;
            public int FieldIndex;
            public int[] SubFieldIndices;
            public string ChangeKey;
            public double LastNumberVal;
            public string LastStringVal;
            public bool LastBoolVal;
            public int LastEntityRefVal;
            public bool IsHovered;
            public FieldValueType ValueType;
            public bool IsVector;
            public VisualElement BoolTrack;
            public VisualElement BoolThumb;
            public Label BoolLabel;
            public Label EntityLink;
            public Button EntityEditBtn;
            public Label KeyLabel;
        }

        private struct DragState
        {
            public float StartX;
            public double BaseVal;
            public bool Active;
        }

        private FieldRow[] _rows;
        private DragState[] _dragStates;
        private int _labelDragRow = -1;
        private int _labelDragSubIdx = -1;
        private float _labelDragStartX;
        private double _labelDragBaseVal;

        private const string TI = "unity-text-input";

        private static Texture2D _resizeCursorTex;

        private static Texture2D GetResizeCursorTexture()
        {
            if (_resizeCursorTex != null) return _resizeCursorTex;
            const int s = 32;
            _resizeCursorTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * s];
            int cy = s / 2;
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < 5; i++)
            {
                int x = 4 + i;
                for (int dy = -i; dy <= i; dy++)
                    px[(cy + dy) * s + x] = white;
            }
            for (int i = 0; i < 5; i++)
            {
                int x = 27 - i;
                for (int dy = -i; dy <= i; dy++)
                    px[(cy + dy) * s + x] = white;
            }
            for (int x = 7; x <= 24; x++)
            {
                px[(cy - 1) * s + x] = white;
                px[cy * s + x] = white;
                px[(cy + 1) * s + x] = white;
            }
            _resizeCursorTex.SetPixels32(px);
            _resizeCursorTex.Apply();
            _resizeCursorTex.hideFlags = HideFlags.HideAndDontSave;
            return _resizeCursorTex;
        }

        private static void ApplyEwCursor(VisualElement el)
        {
            var tex = GetResizeCursorTexture();
            var hotspot = new Vector2(16, 16);
            el.pickingMode = PickingMode.Position;
            bool hovering = false;
            el.RegisterCallback<MouseEnterEvent>(_ =>
            {
                hovering = true;
                UnityEngine.Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
            });
            el.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                hovering = false;
                UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            });
            el.schedule.Execute(() =>
            {
                if (hovering && el.panel != null)
                    UnityEngine.Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
            }).Every(16);
        }

        private static readonly Dictionary<int, ComponentCardDrawer> Cache = new();
        private static readonly List<ComponentCardDrawer> Active = new();

        public static ComponentCardDrawer GetOrCreate(ComponentInfo comp)
        {
            var key = comp.TypeIndex >= 0 ? comp.TypeIndex : comp.Name.GetHashCode();
            if (!Cache.TryGetValue(key, out var drawer))
            {
                drawer = new ComponentCardDrawer(comp, key);
                Cache[key] = drawer;
            }
            return drawer;
        }

        public static void ResetActive()
        {
            Active.Clear();
        }

        public static void AddActive(ComponentCardDrawer drawer)
        {
            Active.Add(drawer);
        }

        public static void ClearCache()
        {
            Cache.Clear();
            Active.Clear();
        }

        public static int ActiveCount => Active.Count;

        public static ComponentCardDrawer GetActive(int index)
        {
            return Active[index];
        }

        private ComponentCardDrawer(ComponentInfo template, int typeIndex)
        {
            _compName = template.Name;
            _byteSize = template.ByteSize;

            Card = EcsDebugV2Theme.CreateCard();
            Card.style.marginBottom = 6;

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
                    backgroundColor = EcsDebugV2Theme.PanelElevated,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.PanelBorderA04
                }
            };
            var compNameLabel = new Label(template.Name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Body,
                    color = EcsDebugV2Theme.Foreground,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            compHeader.Add(compNameLabel);
            var sizeLabel = new Label($"size:{_byteSize}B")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = 6,
                    
                }
            };
            compHeader.Add(sizeLabel);

            var removeBtn = new Button(() =>
            {
                if (_window != null)
                    _window.RemoveComponent(_entityId, _compName);
            })
            {
                text = "\u2715",
                tooltip = $"Remove {template.Name}",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.FieldName,
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
            Card.Add(compHeader);

            bool isTag = template.Fields.Count == 1 && template.Fields[0].Key == "#tag";
            if (isTag)
            {
                sizeLabel.text = "#tag";
                sizeLabel.style.color = EcsDebugV2Theme.Lime;
                _rows = Array.Empty<FieldRow>();
                _dragStates = Array.Empty<DragState>();
                return;
            }

            var groups = BuildFieldGroups(template);
            _rows = new FieldRow[groups.Count];
            _dragStates = new DragState[groups.Count];

            for (int gi = 0; gi < groups.Count; gi++)
            {
                var group = groups[gi];
                if (group.Prefix != null && group.FieldIndices.Count > 1)
                    BuildVectorRow(template, group.Prefix, group.FieldIndices, gi);
                else
                {
                    var fi = group.FieldIndices[0];
                    BuildScalarRow(template.Fields[fi].Key, template.Fields[fi].Value, fi, gi);
                }
                Card.Add(_rows[gi].Row);
            }
        }

        public void Bind(int entityId, string compName, EcsDebugV2Window window, int compIdx, ComponentInfo comp)
        {
            _entityId = entityId;
            _compName = compName;
            _window = window;

            for (int i = 0; i < _rows.Length; i++)
            {
                var r = _rows[i];
                r.ChangeKey = $"{entityId}:{compName}:{r.FieldKey}";
                r.IsHovered = false;

                if (r.IsVector)
                {
                    for (int si = 0; si < r.SubFieldIndices.Length; si++)
                    {
                        var subIdx = r.SubFieldIndices[si];
                        if (subIdx < comp.Fields.Count && r.SubTextFields != null &&
                            si < r.SubTextFields.Length && r.SubTextFields[si] != null)
                            r.SubTextFields[si].SetValueWithoutNotify(
                                comp.Fields[subIdx].Value.NumberVal.ToString("G"));
                    }
                }
                else
                {
                    if (r.FieldIndex >= 0 && r.FieldIndex < comp.Fields.Count)
                    {
                        var fv = comp.Fields[r.FieldIndex].Value;
                        r.LastNumberVal = fv.Type == FieldValueType.Number ? fv.NumberVal : 0;
                        r.LastStringVal = fv.Type == FieldValueType.String ? fv.StringVal : null;
                        r.LastBoolVal = fv.Type == FieldValueType.Bool && fv.BoolVal;
                        r.LastEntityRefVal = fv.Type == FieldValueType.EntityRef ? fv.EntityRefVal : 0;

                        switch (fv.Type)
                        {
                            case FieldValueType.Number:
                                if (r.MainTextField != null)
                                    r.MainTextField.SetValueWithoutNotify(fv.NumberVal.ToString("G"));
                                break;
                            case FieldValueType.String:
                                if (r.MainTextField != null)
                                    r.MainTextField.SetValueWithoutNotify(fv.StringVal);
                                break;
                            case FieldValueType.Bool:
                                SetBoolVisuals(r, fv.BoolVal);
                                break;
                            case FieldValueType.EntityRef:
                                if (r.EntityLink != null)
                                    r.EntityLink.text = $"#{fv.EntityRefVal}";
                                break;
                        }
                    }
                }

                _rows[i] = r;
            }
        }

        public void UpdateValues(ComponentInfo comp, long now)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                var r = _rows[i];

                if (r.IsVector)
                {
                    UpdateVectorRow(i, comp, now);
                    continue;
                }

                if (r.FieldIndex < 0 || r.FieldIndex >= comp.Fields.Count)
                    goto Highlight;

                var fv = comp.Fields[r.FieldIndex].Value;

                if (r.MainTextField != null)
                {
                    try
                    {
                        var f = r.MainTextField.panel?.focusController?.focusedElement as VisualElement;
                        if (f != null && r.MainTextField.Contains(f))
                            goto Highlight;
                    }
                    catch { }
                }

                switch (fv.Type)
                {
                    case FieldValueType.Number:
                        if (Math.Abs(fv.NumberVal - r.LastNumberVal) > 0.0001)
                        {
                            r.LastNumberVal = fv.NumberVal;
                            _rows[i] = r;
                            r.MainTextField.SetValueWithoutNotify(fv.NumberVal.ToString("G"));
                        }
                        break;
                    case FieldValueType.String:
                        if (fv.StringVal != r.LastStringVal)
                        {
                            r.LastStringVal = fv.StringVal;
                            _rows[i] = r;
                            r.MainTextField.SetValueWithoutNotify(fv.StringVal);
                        }
                        break;
                    case FieldValueType.EntityRef:
                        if (fv.EntityRefVal != r.LastEntityRefVal)
                        {
                            r.LastEntityRefVal = fv.EntityRefVal;
                            _rows[i] = r;
                            if (r.EntityLink != null)
                                r.EntityLink.text = $"#{fv.EntityRefVal}";
                        }
                        break;
                    case FieldValueType.Bool:
                        if (fv.BoolVal != r.LastBoolVal)
                        {
                            r.LastBoolVal = fv.BoolVal;
                            _rows[i] = r;
                            SetBoolVisuals(r, fv.BoolVal);
                        }
                        break;
                }

                Highlight:
                if (r.IsHovered)
                {
                    r.Row.style.backgroundColor = EcsDebugV2Theme.PanelElevatedA04;
                }
                else
                {
                    long ts;
                    if (_window != null && _window.changes.TryGetValue(r.ChangeKey, out ts))
                    {
                        var age = now - ts;
                        r.Row.style.backgroundColor = age < 1200
                            ? EcsDebugV2Theme.YellowA015
                            : EcsDebugV2Theme.PanelElevated;
                    }
                    else
                    {
                        r.Row.style.backgroundColor = EcsDebugV2Theme.PanelElevated;
                    }
                }
            }
        }

        private void UpdateVectorRow(int idx, ComponentInfo comp, long now)
        {
            var r = _rows[idx];
            bool anyFocused = false;

            for (int si = 0; si < r.SubTextFields.Length; si++)
            {
                var subTf = r.SubTextFields[si];
                if (subTf == null) continue;
                try
                {
                    var f = subTf.panel?.focusController?.focusedElement as VisualElement;
                    if (f != null && subTf.Contains(f))
                    {
                        anyFocused = true;
                        break;
                    }
                }
                catch { }
            }

            if (!anyFocused)
            {
                for (int si = 0; si < r.SubFieldIndices.Length; si++)
                {
                    var subIdx = r.SubFieldIndices[si];
                    if (subIdx >= comp.Fields.Count) continue;
                    var fv = comp.Fields[subIdx].Value;
                    var subTf = si < r.SubTextFields.Length ? r.SubTextFields[si] : null;
                    if (subTf == null) continue;
                    var newText = fv.NumberVal.ToString("G");
                    if (subTf.value != newText)
                        subTf.SetValueWithoutNotify(newText);
                }
            }

            if (r.IsHovered)
            {
                r.Row.style.backgroundColor = EcsDebugV2Theme.PanelElevatedA04;
                return;
            }

            bool anyHighlighted = false;
            for (int si = 0; si < r.SubFieldIndices.Length; si++)
            {
                var subIdx = r.SubFieldIndices[si];
                if (subIdx >= comp.Fields.Count) continue;
                var subKey = comp.Fields[subIdx].Key;
                var subChangeKey = $"{_entityId}:{_compName}:{subKey}";
                long ts;
                if (_window != null && _window.changes.TryGetValue(subChangeKey, out ts) && (now - ts) < 1200)
                {
                    anyHighlighted = true;
                    break;
                }
            }

            r.Row.style.backgroundColor = anyHighlighted
                ? EcsDebugV2Theme.YellowA015
                : EcsDebugV2Theme.PanelElevated;
        }

        private void SetBoolVisuals(FieldRow r, bool isOn)
        {
            if (r.BoolTrack != null)
            {
                r.BoolTrack.style.backgroundColor = isOn ? EcsDebugV2Theme.LimeA03 : EcsDebugV2Theme.PanelBorder;
                r.BoolTrack.style.borderTopColor =
                    isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                r.BoolTrack.style.borderBottomColor =
                    isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                r.BoolTrack.style.borderLeftColor =
                    isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                r.BoolTrack.style.borderRightColor =
                    isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
            }

            if (r.BoolThumb != null)
            {
                r.BoolThumb.style.marginLeft = isOn ? 20 : 2;
                r.BoolThumb.style.backgroundColor = isOn ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
            }

            if (r.BoolLabel != null)
            {
                r.BoolLabel.text = isOn.ToString();
                r.BoolLabel.style.color = isOn ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
            }
        }

        private void BuildScalarRow(string fieldKey, FieldValue value, int fieldIndex, int rowIdx)
        {
            var row = new VisualElement
            {
                name = $"frow-{_compName}-{fieldKey}",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 3,
                    paddingBottom = 3,
                    backgroundColor = EcsDebugV2Theme.PanelElevated
                }
            };
            row.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f, TimeUnit.Second) };
            row.style.transitionProperty =
                new List<StylePropertyName> { new StylePropertyName("background-color") };

            var keyLabel = new Label(fieldKey)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.FieldName,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 0.5f,
                    width = 130,
                    flexShrink = 0
                }
            };
            row.Add(keyLabel);

            VisualElement editor = null;
            TextField mainTf = null;
            VisualElement boolTrack = null;
            VisualElement boolThumb = null;
            Label boolLabel = null;
            Label entityLink = null;
            Button entityEditBtn = null;

            switch (value.Type)
            {
                case FieldValueType.Number:
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
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.TypeNumber,
                            backgroundColor = EcsDebugV2Theme.Background,
                            flexGrow = 1,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            unityTextAlign = TextAnchor.MiddleRight
                        }
                    };
                    tf.SetupBorder(Color.clear, 0);
                    tf.Q(TI).style.backgroundColor = Color.clear;
                    tf.Q(TI).SetupBorder(Color.clear, 0);
                    tf.Q(TI).style.paddingLeft = 2;
                    tf.Q(TI).style.paddingRight = 2;

                    var capturedIdx = rowIdx;
                    tf.RegisterValueChangedCallback(evt =>
                    {
                        if (double.TryParse(evt.newValue, out var n))
                            _window.SetFieldValue(_entityId, _compName, fieldKey, FieldValue.FromNumber(n));
                    });
                    tf.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button == 1)
                        {
                            var baseVal = double.TryParse(tf.value, out var v) ? v : 0;
                            _dragStates[capturedIdx] = new DragState
                                { StartX = evt.position.x, BaseVal = baseVal, Active = true };
                            tf.CapturePointer(evt.pointerId);
                            evt.StopPropagation();
                        }
                    });
                    tf.RegisterCallback<PointerMoveEvent>(evt =>
                    {
                        var ds = _dragStates[capturedIdx];
                        if (ds.Active)
                        {
                            var delta = evt.position.x - ds.StartX;
                            var speed = evt.shiftKey ? 0.01 : (evt.ctrlKey ? 10.0 : 0.5);
                            var newVal = ds.BaseVal + delta * speed;
                            tf.SetValueWithoutNotify(newVal.ToString("G"));
                            _window.SetFieldValue(_entityId, _compName, fieldKey,
                                FieldValue.FromNumber(newVal));
                        }
                    });
                    tf.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        if (evt.button == 1 && _dragStates[capturedIdx].Active)
                        {
                            _dragStates[capturedIdx] = default;
                            tf.ReleasePointer(evt.pointerId);
                            evt.StopPropagation();
                        }
                    });

                    container.Add(tf);
                    editor = container;
                    mainTf = tf;
                    break;
                }

                case FieldValueType.Bool:
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
                            backgroundColor = isOn
                                ? EcsDebugV2Theme.LimeA03
                                : EcsDebugV2Theme.PanelBorder,
                            flexShrink = 0,
                            marginRight = 6,
                            overflow = Overflow.Hidden
                        }
                    };
                    track.name = "bool-track";
                    track.SetupRadius(9);
                    track.SetupBorder(isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder);
                    track.style.transitionDuration =
                        new List<TimeValue> { new TimeValue(0.15f, TimeUnit.Second) };
                    track.style.transitionProperty =
                        new List<StylePropertyName> { new StylePropertyName("background-color") };
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
                    thumb.style.transitionDuration =
                        new List<TimeValue> { new TimeValue(0.15f, TimeUnit.Second) };
                    thumb.style.transitionProperty = new List<StylePropertyName>
                    {
                        new StylePropertyName("margin-left"),
                        new StylePropertyName("background-color")
                    };
                    track.Add(thumb);

                    var lbl = new Label(isOn.ToString())
                    {
                        name = "bool-label",
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = isOn ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText
                        }
                    };
                    container.Add(track);
                    container.Add(lbl);

                    var capturedIdx = rowIdx;
                    container.RegisterCallback<ClickEvent>(_ =>
                    {
                        var r = _rows[capturedIdx];
                        var newVal = !r.LastBoolVal;
                        r.LastBoolVal = newVal;
                        _rows[capturedIdx] = r;
                        _window.SetFieldValue(_entityId, _compName, fieldKey,
                            FieldValue.FromBool(newVal));

                        thumb.style.marginLeft = newVal ? 20 : 2;
                        thumb.style.backgroundColor =
                            newVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
                        track.style.backgroundColor =
                            newVal ? EcsDebugV2Theme.LimeA03 : EcsDebugV2Theme.PanelBorder;
                        track.style.borderTopColor =
                            newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                        track.style.borderBottomColor =
                            newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                        track.style.borderLeftColor =
                            newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                        track.style.borderRightColor =
                            newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.PanelBorder;
                        lbl.text = newVal.ToString();
                        lbl.style.color = newVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
                    });

                    editor = container;
                    boolTrack = track;
                    boolThumb = thumb;
                    boolLabel = lbl;
                    break;
                }

                case FieldValueType.String:
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
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.TypeString,
                            backgroundColor = EcsDebugV2Theme.Background,
                            flexGrow = 1
                        }
                    };
                    tf.SetupBorder(Color.clear, 0);
                    tf.Q(TI).style.backgroundColor = Color.clear;
                    tf.Q(TI).SetupBorder(Color.clear, 0);
                    tf.Q(TI).style.paddingLeft = 2;
                    tf.Q(TI).style.paddingRight = 2;
                    tf.Q(TI).style.borderBottomWidth = 1;
                    tf.Q(TI).style.borderBottomColor = EcsDebugV2Theme.PanelBorderA04;

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
                        _window.SetFieldValue(_entityId, _compName, fieldKey,
                            FieldValue.FromString(evt.newValue)));
                    tf.RegisterCallback<FocusInEvent>(_ => underline.style.opacity = 1);
                    tf.RegisterCallback<FocusOutEvent>(_ => underline.style.opacity = 0);

                    wrapper.Add(tf);
                    wrapper.Add(underline);
                    editor = wrapper;
                    mainTf = tf;
                    break;
                }

                case FieldValueType.EntityRef:
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

                    container.Add(new Label("\u2192")
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.TypeEntity,
                            marginRight = 4
                        }
                    });

                    var link = new Label($"#{value.EntityRefVal}")
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.TypeEntity,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            backgroundColor = EcsDebugV2Theme.Background
                        }
                    };
                    link.RegisterCallback<MouseEnterEvent>(_ => link.style.opacity = 0.7f);
                    link.RegisterCallback<MouseLeaveEvent>(_ => link.style.opacity = 1f);

                    var capturedIdx = rowIdx;
                    link.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (_window != null && _rows[capturedIdx].LastEntityRefVal > 0)
                            _window.SelectEntity(_rows[capturedIdx].LastEntityRefVal);
                    });
                    container.Add(link);

                    var capturedFieldKey = fieldKey;
                    Button editBtn = null;
                    editBtn = new Button(() =>
                    {
                        var currentId = _rows[capturedIdx].LastEntityRefVal;
                        var popup = new TextField
                        {
                            value = currentId.ToString(),
                            style =
                            {
                                fontSize = EcsDebugV2Theme.Font.FieldName,
                                color = EcsDebugV2Theme.TypeEntity,
                                backgroundColor = EcsDebugV2Theme.Background,
                                width = 60
                            }
                        };
                        popup.SetupBorder(EcsDebugV2Theme.TypeEntity, 1);
                        popup.Q(TI).style.backgroundColor = Color.clear;
                        popup.RegisterValueChangedCallback(evt =>
                        {
                            if (int.TryParse(evt.newValue, out var n))
                            {
                                _window.SetFieldValue(_entityId, _compName, capturedFieldKey,
                                    FieldValue.FromEntityRef(n));
                                link.text = $"#{n}";
                            }
                        });
                        popup.RegisterCallback<FocusOutEvent>(_ =>
                        {
                            _rows[capturedIdx].Editor.Remove(popup);
                            link.style.display = DisplayStyle.Flex;
                            _rows[capturedIdx].EntityEditBtn.style.display = DisplayStyle.Flex;
                        });
                        link.style.display = DisplayStyle.None;
                        editBtn.style.display = DisplayStyle.None;
                        _rows[capturedIdx].Editor.Add(popup);
                        popup.Focus();
                    })
                    {
                        text = "\u270E",
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
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

                    editor = container;
                    entityLink = link;
                    entityEditBtn = editBtn;
                    break;
                }

                default:
                    editor = new Label("\u2014") { style = { color = EcsDebugV2Theme.MutedText } };
                    break;
            }

            if (value.Type == FieldValueType.Number && keyLabel != null && mainTf != null)
            {
                ApplyEwCursor(keyLabel);
                
                var capturedIdx = rowIdx;
                var capturedTf = mainTf;
                var capturedFieldKey = fieldKey;
                keyLabel.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0)
                    {
                        _labelDragStartX = evt.position.x;
                        _labelDragBaseVal = double.TryParse(capturedTf.value, out var v) ? v : 0;
                        _labelDragRow = capturedIdx;
                        keyLabel.CapturePointer(evt.pointerId);
                        keyLabel.style.color = EcsDebugV2Theme.Lime;
                        evt.StopPropagation();
                    }
                });
                keyLabel.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (_labelDragRow == capturedIdx)
                    {
                        var delta = evt.position.x - _labelDragStartX;
                        var speed = evt.shiftKey ? 0.01 : (evt.ctrlKey ? 10.0 : 0.5);
                        var newVal = _labelDragBaseVal + delta * speed;
                        capturedTf.SetValueWithoutNotify(newVal.ToString("G"));
                        _window.SetFieldValue(_entityId, _compName, capturedFieldKey,
                            FieldValue.FromNumber(newVal));
                    }
                });
                keyLabel.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button == 0 && _labelDragRow == capturedIdx)
                    {
                        _labelDragRow = -1;
                        keyLabel.ReleasePointer(evt.pointerId);
                        keyLabel.style.color = EcsDebugV2Theme.MutedText;
                        evt.StopPropagation();
                    }
                });
            }

            editor.name = $"editor-{_compName}-{fieldKey}";
            row.Add(editor);

            var ri = rowIdx;
            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                var r = _rows[ri];
                r.IsHovered = true;
                _rows[ri] = r;
                r.Row.style.backgroundColor = EcsDebugV2Theme.PanelElevatedA04;
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var r = _rows[ri];
                r.IsHovered = false;
                _rows[ri] = r;
            });

            _rows[rowIdx] = new FieldRow
            {
                Row = row,
                Editor = editor,
                MainTextField = mainTf,
                FieldKey = fieldKey,
                FieldIndex = fieldIndex,
                ChangeKey = "",
                LastNumberVal = value.Type == FieldValueType.Number ? value.NumberVal : 0,
                LastStringVal = value.Type == FieldValueType.String ? value.StringVal : null,
                LastBoolVal = value.Type == FieldValueType.Bool && value.BoolVal,
                LastEntityRefVal = value.Type == FieldValueType.EntityRef ? value.EntityRefVal : 0,
                IsHovered = false,
                ValueType = value.Type,
                IsVector = false,
                BoolTrack = boolTrack,
                BoolThumb = boolThumb,
                BoolLabel = boolLabel,
                EntityLink = entityLink,
                EntityEditBtn = entityEditBtn,
                KeyLabel = keyLabel,
            };
        }

        private void BuildVectorRow(ComponentInfo template, string prefix, List<int> fieldIndices, int rowIdx)
        {
            var row = new VisualElement
            {
                name = $"frow-{_compName}-{prefix}",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 3,
                    paddingBottom = 3,
                    backgroundColor = EcsDebugV2Theme.PanelElevated
                }
            };
            row.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f, TimeUnit.Second) };
            row.style.transitionProperty =
                new List<StylePropertyName> { new StylePropertyName("background-color") };

            var prefixLabel = new Label(prefix)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.FieldName,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 0.5f,
                    width = 130,
                    flexShrink = 0
                }
            };
            row.Add(prefixLabel);

            var subFieldNames = new string[fieldIndices.Count];
            var subFieldColors = new Color[fieldIndices.Count];
            var subValues = new FieldValue[fieldIndices.Count];
            var subFieldKeys = new string[fieldIndices.Count];
            var subColors = new[]
                { EcsDebugV2Theme.TypeNumber, EcsDebugV2Theme.Lime, EcsDebugV2Theme.Yellow };

            for (int i = 0; i < fieldIndices.Count; i++)
            {
                var fi = fieldIndices[i];
                var key = template.Fields[fi].Key;
                var dotIdx = key.IndexOf('.');
                subFieldNames[i] = dotIdx >= 0 ? key.Substring(dotIdx + 1) : key;
                subFieldColors[i] = i < subColors.Length ? subColors[i] : EcsDebugV2Theme.TypeNumber;
                subValues[i] = template.Fields[fi].Value;
                subFieldKeys[i] = key;
            }

            var editor = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1
                }
            };

            var subTfs = new TextField[fieldIndices.Count];

            for (int i = 0; i < subFieldNames.Length; i++)
            {
                var color = i < subFieldColors.Length ? subFieldColors[i] : EcsDebugV2Theme.TypeNumber;
                var isLast = i == subFieldNames.Length - 1;

                var subGroup = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        flexGrow = 1,
                        marginRight = isLast ? 0 : 4
                    }
                };

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
                ApplyEwCursor(subLabel);
                subGroup.Add(subLabel);

                var subTf = new TextField
                {
                    value = (i < subValues.Length ? subValues[i].NumberVal : 0).ToString("G"),
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = EcsDebugV2Theme.Font.FieldName,
                        color = color,
                        backgroundColor = EcsDebugV2Theme.Background,
                        width = 40,
                        flexShrink = 0
                    }
                };
                subTf.SetupBorder(Color.clear, 0);
                subTf.Q(TI).style.backgroundColor = Color.clear;
                subTf.Q(TI).SetupBorder(Color.clear, 0);
                subTf.Q(TI).style.paddingLeft = 2;
                subTf.Q(TI).style.paddingRight = 2;

                var capturedSubKey = subFieldKeys[i];
                subTf.RegisterValueChangedCallback(evt =>
                {
                    if (double.TryParse(evt.newValue, out var n))
                        _window.SetFieldValue(_entityId, _compName, capturedSubKey,
                            FieldValue.FromNumber(n));
                });

                var capturedRowIdx = rowIdx;
                var capturedSubIdx = i;
                var capturedColor = color;
                subLabel.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0)
                    {
                        _labelDragStartX = evt.position.x;
                        _labelDragBaseVal = double.TryParse(subTf.value, out var v) ? v : 0;
                        _labelDragRow = capturedRowIdx;
                        _labelDragSubIdx = capturedSubIdx;
                        subLabel.CapturePointer(evt.pointerId);
                        subLabel.style.color = EcsDebugV2Theme.Lime;
                        evt.StopPropagation();
                    }
                });
                subLabel.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (_labelDragRow == capturedRowIdx && _labelDragSubIdx == capturedSubIdx)
                    {
                        var delta = evt.position.x - _labelDragStartX;
                        var speed = evt.shiftKey ? 0.01 : (evt.ctrlKey ? 10.0 : 0.5);
                        var newVal = _labelDragBaseVal + delta * speed;
                        subTf.SetValueWithoutNotify(newVal.ToString("G"));
                        _window.SetFieldValue(_entityId, _compName, capturedSubKey,
                            FieldValue.FromNumber(newVal));
                    }
                });
                subLabel.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button == 0 && _labelDragRow == capturedRowIdx && _labelDragSubIdx == capturedSubIdx)
                    {
                        _labelDragRow = -1;
                        _labelDragSubIdx = -1;
                        subLabel.ReleasePointer(evt.pointerId);
                        subLabel.style.color = capturedColor;
                        evt.StopPropagation();
                    }
                });

                subGroup.Add(subTf);
                editor.Add(subGroup);
                subTfs[i] = subTf;
            }

            editor.name = $"editor-{_compName}-{prefix}";
            row.Add(editor);

            var subIndices = new int[fieldIndices.Count];
            fieldIndices.CopyTo(subIndices, 0);

            var ri = rowIdx;
            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                var r = _rows[ri];
                r.IsHovered = true;
                _rows[ri] = r;
                r.Row.style.backgroundColor = EcsDebugV2Theme.PanelElevatedA04;
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var r = _rows[ri];
                r.IsHovered = false;
                _rows[ri] = r;
            });

            _rows[rowIdx] = new FieldRow
            {
                Row = row,
                Editor = editor,
                SubTextFields = subTfs,
                FieldKey = prefix,
                SubFieldKeys = subFieldKeys,
                FieldIndex = -1,
                SubFieldIndices = subIndices,
                ChangeKey = "",
                IsHovered = false,
                ValueType = FieldValueType.Number,
                IsVector = true,
                KeyLabel = prefixLabel,
            };
        }

        private struct FieldGroup
        {
            public string Prefix;
            public List<int> FieldIndices;
        }

        private static List<FieldGroup> BuildFieldGroups(ComponentInfo comp)
        {
            var groups = new List<FieldGroup>();
            var i = 0;
            while (i < comp.Fields.Count)
            {
                var key = comp.Fields[i].Key;
                var dotIdx = key.IndexOf('.');
                if (dotIdx > 0 && i + 1 < comp.Fields.Count)
                {
                    var prefix = key.Substring(0, dotIdx);
                    var groupFields = new List<int> { i };
                    int j = i + 1;
                    while (j < comp.Fields.Count && comp.Fields[j].Key.StartsWith(prefix + "."))
                    {
                        groupFields.Add(j);
                        j++;
                    }

                    if (groupFields.Count > 1)
                    {
                        bool allNumbers = true;
                        for (int k = 0; k < groupFields.Count; k++)
                        {
                            if (comp.Fields[groupFields[k]].Value.Type != FieldValueType.Number)
                            {
                                allNumbers = false;
                                break;
                            }
                        }

                        if (allNumbers)
                        {
                            groups.Add(new FieldGroup { Prefix = prefix, FieldIndices = groupFields });
                            i = j;
                            continue;
                        }
                    }
                }

                groups.Add(new FieldGroup { Prefix = null, FieldIndices = new List<int> { i } });
                i++;
            }

            return groups;
        }
    }
}
#endif
