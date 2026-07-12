#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

// ReSharper disable EmptyGeneralCatchClause
// ReSharper disable HeapView.CanAvoidClosure

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    using static Constant;

    public class ComponentCardDrawer
    {
        private static Texture2D _resizeCursorTex;
        private static readonly Dictionary<int, ComponentCardDrawer> Cache = new();
        private static readonly List<ComponentCardDrawer> Active = new();
        private static readonly Texture2D CursorResizeTexture = GetResizeCursorTexture();
        private static readonly Vector2 CursorHotspot = new(12, 12);

        private static readonly StyleCursor ResizeCursor = new(new UnityEngine.UIElements.Cursor
        {
            hotspot = CursorHotspot,
            texture = CursorResizeTexture,
        });

        public readonly VisualElement card;
        private string _compName;
        private readonly DragState[] _dragStates;

        private int _entityId;
        private double _labelDragBaseVal;
        private int _labelDragRow = -1;
        private float _labelDragStartX;
        private int _labelDragSubIdx = -1;

        private readonly FieldRow[] _rows;
        private EcsDebugV2Window _window;

        private ComponentCardDrawer(ComponentInfo template)
        {
            _compName = template.Name;
            var byteSize = template.ByteSize;

            card = EcsDebugV2Theme.CreateGlassCard();
            card.style.marginBottom = 7;

            var compHeader = new VisualElement
            {
                style =
                {
                    height = EcsDebugV2Theme.ComponentHeaderHeight,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 12,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.5f),
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.GlassBorder
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
            var sizeLabel = new Label($"{byteSize} Bytes")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.FieldName,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = 6
                }
            };
            compHeader.Add(sizeLabel);

            var removeBtn = new Button(() => { _window?.RemoveComponent(_entityId, _compName); })
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
                    marginLeft = Length.Auto(),
                    width = 24,
                    height = 24
                }
            };
            removeBtn.SetupBorder(Color.clear, 0);
            removeBtn.RegisterCallback<MouseEnterEvent, Color>((_, color) => removeBtn.style.color = color,
                EcsDebugV2Theme.Red);
            removeBtn.RegisterCallback<MouseLeaveEvent, Color>((_, color) => removeBtn.style.color = color,
                EcsDebugV2Theme.MutedText);
            compHeader.Add(removeBtn);
            card.Add(compHeader);

            var isTag = template.Fields.Count == 1 && template.Fields[0].Key == TAG_LABEL;
            if (isTag)
            {
                sizeLabel.text = TAG_LABEL;
                sizeLabel.style.color = EcsDebugV2Theme.Lime;
                _rows = Array.Empty<FieldRow>();
                _dragStates = Array.Empty<DragState>();
                return;
            }

            var groups = BuildFieldGroups(template);
            _rows = new FieldRow[groups.Count];
            _dragStates = new DragState[groups.Count];

            for (var gi = 0; gi < groups.Count; gi++)
            {
                var group = groups[gi];
                if (group.prefix != null && group.fieldIndices.Count > 1)
                {
                    BuildVectorRow(template, group.prefix, group.fieldIndices, gi);
                }
                else
                {
                    var fi = group.fieldIndices[0];
                    BuildScalarRow(template.Fields[fi].Key, template.Fields[fi].Value, fi, gi);
                }

                card.Add(_rows[gi].row);
            }
        }

        public static int ActiveCount => Active.Count;

        private static Texture2D GetResizeCursorTexture()
        {
            if (_resizeCursorTex != null) return _resizeCursorTex;
            const int s = 32;
            _resizeCursorTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * s];
            var cy = s / 2;
            var white = new Color32(255, 255, 255, 255);
            for (var i = 0; i < 5; i++)
            {
                var x = 4 + i;
                for (var dy = -i; dy <= i; dy++)
                    px[(cy + dy) * s + x] = white;
            }

            for (var i = 0; i < 5; i++)
            {
                var x = 27 - i;
                for (var dy = -i; dy <= i; dy++)
                    px[(cy + dy) * s + x] = white;
            }

            for (var x = 7; x <= 24; x++)
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

        private static void ApplyEwCursor(VisualElement element)
        {
            element.pickingMode = PickingMode.Position;
            element.RegisterCallback<MouseEnterEvent>(_ =>
            {
                element.style.cursor = ResizeCursor;
            });
            element.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                element.style.cursor = StyleKeyword.Null;
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            });
            
        }

        public static ComponentCardDrawer GetOrCreate(ComponentInfo comp)
        {
            var key = comp.TypeIndex >= 0 ? comp.TypeIndex : comp.Name.GetHashCode();
            if (!Cache.TryGetValue(key, out var drawer))
            {
                drawer = new ComponentCardDrawer(comp);
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

        public static ComponentCardDrawer GetActive(int index)
        {
            return Active[index];
        }

        public void Bind(int entityId, string compName, EcsDebugV2Window window, int compIdx, ComponentInfo comp)
        {
            _entityId = entityId;
            _compName = compName;
            _window = window;

            for (var i = 0; i < _rows.Length; i++)
            {
                var r = _rows[i];
                r.changeKey = $"{entityId}:{compName}:{r.fieldKey}";
                r.isHovered = false;

                if (r.isVector)
                {
                    for (var si = 0; si < r.subFieldIndices.Length; si++)
                    {
                        var subIdx = r.subFieldIndices[si];
                        if (subIdx < comp.Fields.Count && r.subTextFields != null &&
                            si < r.subTextFields.Length && r.subTextFields[si] != null)
                            r.subTextFields[si].SetValueWithoutNotify(
                                comp.Fields[subIdx].Value.NumberVal.ToString(GENERAL_NUMBER_FORMAT));
                    }
                }
                else
                {
                    if (r.fieldIndex >= 0 && r.fieldIndex < comp.Fields.Count)
                    {
                        var fv = comp.Fields[r.fieldIndex].Value;
                        r.lastNumberVal = fv.Type == FieldValueType.Number ? fv.NumberVal : 0;
                        r.lastStringVal = fv.Type == FieldValueType.String ? fv.StringVal : null;
                        r.lastBoolVal = fv is { Type: FieldValueType.Bool, BoolVal: true };
                        r.lastEntityRefVal = fv.Type == FieldValueType.EntityRef ? fv.EntityRefVal : 0;
                        r.lastEnumNames = fv.Type == FieldValueType.Enum ? fv.EnumNames : r.lastEnumNames;
                        r.lastEnumRawValues = fv.Type == FieldValueType.Enum ? fv.EnumRawValues : r.lastEnumRawValues;
                        r.lastEnumIndex = fv.Type == FieldValueType.Enum ? fv.EnumSelectedIndex : r.lastEnumIndex;
                        r.lastEnumRawValue = fv.Type == FieldValueType.Enum ? fv.EnumRawValue : r.lastEnumRawValue;
                        r.lastObjectName = fv.Type == FieldValueType.ObjectRef ? fv.ObjectName : r.lastObjectName;
                        r.lastObjectInstanceId = fv.Type == FieldValueType.ObjectRef ? fv.ObjectInstanceId : r.lastObjectInstanceId;
                        r.lastObjectTypeName = fv.Type == FieldValueType.ObjectRef ? fv.ObjectTypeName : r.lastObjectTypeName;
                        r.lastArrayElementTypeName = fv.Type == FieldValueType.ComponentArray ? fv.ArrayElementTypeName : r.lastArrayElementTypeName;
                        r.lastArrayLength = fv.Type == FieldValueType.ComponentArray ? fv.ArrayLength : r.lastArrayLength;
                        r.lastIsUnityObject = fv.Type == FieldValueType.ObjectRef && fv.IsUnityObject;

                        switch (fv.Type)
                        {
                            case FieldValueType.Number:
                                r.mainTextField?.SetValueWithoutNotify(fv.NumberVal.ToString(GENERAL_NUMBER_FORMAT));
                                break;
                            case FieldValueType.String:
                                r.mainTextField?.SetValueWithoutNotify(fv.StringVal);
                                break;
                            case FieldValueType.Bool:
                                SetBoolVisuals(r, fv.BoolVal);
                                break;
                            case FieldValueType.EntityRef:
                                if (r.entityLink != null)
                                    r.entityLink.text = $"#{fv.EntityRefVal}";
                                break;
                            case FieldValueType.Enum:
                                if (r.enumButton != null && fv.EnumNames != null)
                                {
                                    var idx = fv.EnumSelectedIndex >= 0 && fv.EnumSelectedIndex < fv.EnumNames.Length
                                        ? fv.EnumSelectedIndex
                                        : 0;
                                    r.enumButton.text = $"{fv.EnumNames[idx]} \u25BC";
                                }
                                break;
                            case FieldValueType.ObjectRef:
                                break;
                            case FieldValueType.ComponentArray:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }

                _rows[i] = r;
            }
        }

        public void UpdateValues(ComponentInfo comp, long now)
        {
            for (var i = 0; i < _rows.Length; i++)
            {
                var r = _rows[i];

                if (r.isVector)
                {
                    UpdateVectorRow(i, comp, now);
                    continue;
                }

                if (r.fieldIndex < 0 || r.fieldIndex >= comp.Fields.Count)
                    goto Highlight;

                var fv = comp.Fields[r.fieldIndex].Value;

                if (r.mainTextField != null)
                {
                    try
                    {
                        if (r.mainTextField.panel?.focusController?.focusedElement is VisualElement f &&
                            r.mainTextField.Contains(f))
                        {
                            goto Highlight;
                        }
                    }
                    catch
                    {
                    }
                }

                switch (fv.Type)
                {
                    case FieldValueType.Number:
                        if (Math.Abs(fv.NumberVal - r.lastNumberVal) > 0.0001)
                        {
                            r.lastNumberVal = fv.NumberVal;
                            _rows[i] = r;
                            r.mainTextField?.SetValueWithoutNotify(fv.NumberVal.ToString(GENERAL_NUMBER_FORMAT));
                        }

                        break;
                    case FieldValueType.String:
                        if (fv.StringVal != r.lastStringVal)
                        {
                            r.lastStringVal = fv.StringVal;
                            _rows[i] = r;
                            r.mainTextField?.SetValueWithoutNotify(fv.StringVal);
                        }

                        break;
                    case FieldValueType.EntityRef:
                        if (fv.EntityRefVal != r.lastEntityRefVal)
                        {
                            r.lastEntityRefVal = fv.EntityRefVal;
                            _rows[i] = r;
                            if (r.entityLink != null)
                                r.entityLink.text = $"#{fv.EntityRefVal}";
                        }

                        break;
                    case FieldValueType.Bool:
                        if (fv.BoolVal != r.lastBoolVal)
                        {
                            r.lastBoolVal = fv.BoolVal;
                            _rows[i] = r;
                            SetBoolVisuals(r, fv.BoolVal);
                        }

                        break;
                    case FieldValueType.Enum:
                        if (fv.EnumRawValue != r.lastEnumRawValue || fv.EnumSelectedIndex != r.lastEnumIndex)
                        {
                            r.lastEnumRawValue = fv.EnumRawValue;
                            r.lastEnumIndex = fv.EnumSelectedIndex;
                            r.lastEnumNames = fv.EnumNames ?? r.lastEnumNames;
                            r.lastEnumRawValues = fv.EnumRawValues ?? r.lastEnumRawValues;
                            _rows[i] = r;
                            if (r.enumButton != null && r.lastEnumNames != null)
                            {
                                var idx = r.lastEnumIndex >= 0 && r.lastEnumIndex < r.lastEnumNames.Length
                                    ? r.lastEnumIndex
                                    : 0;
                                r.enumButton.text = $"{r.lastEnumNames[idx]} \u25BC";
                            }
                        }

                        break;
                    case FieldValueType.ObjectRef:
                        if (fv.ObjectInstanceId != r.lastObjectInstanceId || fv.ObjectName != r.lastObjectName)
                        {
                            r.lastObjectInstanceId = fv.ObjectInstanceId;
                            r.lastObjectName = fv.ObjectName;
                            r.lastObjectTypeName = fv.ObjectTypeName;
                            _rows[i] = r;
                        }

                        break;
                    case FieldValueType.ComponentArray:
                        if (fv.ArrayLength != r.lastArrayLength)
                        {
                            r.lastArrayLength = fv.ArrayLength;
                            r.lastArrayElementTypeName = fv.ArrayElementTypeName;
                            _rows[i] = r;
                        }

                        break;
                }

                Highlight:
                Color bg;
                if (r.isHovered)
                {
                    bg = EcsDebugV2Theme.SurfaceHover;
                }
                else if (_window != null && _window.changes.TryGetValue(r.changeKey, out var ts))
                {
                    var age = now - ts;
                    bg = age < 1200 ? EcsDebugV2Theme.YellowA015 : Color.clear;
                }
                else
                {
                    bg = Color.clear;
                }

                if (r.lastBgColor != bg)
                {
                    r.lastBgColor = bg;
                    _rows[i] = r;
                    r.row.style.backgroundColor = bg;
                }
            }
        }

        private void UpdateVectorRow(int idx, ComponentInfo comp, long now)
        {
            var r = _rows[idx];
            var anyFocused = false;

            for (var si = 0; si < r.subTextFields.Length; si++)
            {
                var subTf = r.subTextFields[si];
                if (subTf == null) continue;
                try
                {
                    if (subTf.panel?.focusController?.focusedElement
                            is VisualElement f && subTf.Contains(f))
                    {
                        anyFocused = true;
                        break;
                    }
                }
                catch
                {
                }
            }

            if (!anyFocused)
                for (var si = 0; si < r.subFieldIndices.Length; si++)
                {
                    var subIdx = r.subFieldIndices[si];
                    if (subIdx >= comp.Fields.Count) continue;
                    var fv = comp.Fields[subIdx].Value;
                    var subTf = si < r.subTextFields.Length ? r.subTextFields[si] : null;
                    if (subTf == null) continue;
                    var newText = fv.NumberVal.ToString(GENERAL_NUMBER_FORMAT);
                    if (subTf.value != newText)
                        subTf.SetValueWithoutNotify(newText);
                }

            if (r.isHovered)
            {
                if (r.lastBgColor != EcsDebugV2Theme.SurfaceHover)
                {
                    r.lastBgColor = EcsDebugV2Theme.SurfaceHover;
                    _rows[idx] = r;
                    r.row.style.backgroundColor = EcsDebugV2Theme.SurfaceHover;
                }
                return;
            }

            var anyHighlighted = false;
            for (var si = 0; si < r.subFieldIndices.Length; si++)
            {
                var subIdx = r.subFieldIndices[si];
                if (subIdx >= comp.Fields.Count) continue;
                var subKey = comp.Fields[subIdx].Key;
                var subChangeKey = $"{_entityId}:{_compName}:{subKey}";
                if (_window != null &&
                    _window.changes.TryGetValue(subChangeKey, out var ts) &&
                    now - ts < 1200)
                {
                    anyHighlighted = true;
                    break;
                }
            }

            var vecBg = anyHighlighted
                ? EcsDebugV2Theme.YellowA015
                : Color.clear;
            if (r.lastBgColor != vecBg)
            {
                r.lastBgColor = vecBg;
                _rows[idx] = r;
                r.row.style.backgroundColor = vecBg;
            }
        }

        private void SetBoolVisuals(FieldRow r, bool isOn)
        {
            if (r.boolTrack != null)
            {
                r.boolTrack.style.backgroundColor = isOn ? EcsDebugV2Theme.LimeA03 : EcsDebugV2Theme.GlassBorderStrong;
                r.boolTrack.style.borderTopColor =
                    isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong;
                r.boolTrack.style.borderBottomColor =
                    isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong;
                r.boolTrack.style.borderLeftColor =
                    isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong;
                r.boolTrack.style.borderRightColor =
                    isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong;
            }

            if (r.boolThumb != null)
            {
                r.boolThumb.style.marginLeft = isOn ? 20 : 2;
                r.boolThumb.style.backgroundColor = isOn ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
            }

            if (r.boolLabel != null)
            {
                r.boolLabel.text = isOn.ToString();
                r.boolLabel.style.color = isOn ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
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
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = Color.clear,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.GlassBorder,
                    transitionDuration = new List<TimeValue> { new(0.1f, TimeUnit.Second) },
                    transitionProperty = new List<StylePropertyName> { new("background-color") }
                }
            };

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

            VisualElement editor;
            TextField mainTf = null;
            VisualElement boolTrack = null;
            VisualElement boolThumb = null;
            Label boolLabel = null;
            Label entityLink = null;
            Button entityEditBtn = null;
            Button enumButton = null;
            IMGUIContainer objectFieldContainer = null;
            var capturedRowIdx = rowIdx;

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
                        value = value.NumberVal.ToString(GENERAL_NUMBER_FORMAT),
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.TypeNumber,
                            backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.5f),
                            flexGrow = 1,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            unityTextAlign = TextAnchor.MiddleRight
                        }
                    };
                    
                    tf.SetupBorder(Color.clear, 0);
                    tf.Q(TEXT_INPUT).style.backgroundColor = Color.clear;
                    tf.Q(TEXT_INPUT).SetupBorder(Color.clear, 0);
                    tf.Q(TEXT_INPUT).style.paddingLeft = 2;
                    tf.Q(TEXT_INPUT).style.paddingRight = 2;

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
                                { startX = evt.position.x, baseVal = baseVal, active = true };
                            tf.CapturePointer(evt.pointerId);
                            evt.StopPropagation();
                        }
                    });
                    tf.RegisterCallback<PointerMoveEvent>(evt =>
                    {
                        var ds = _dragStates[capturedIdx];
                        if (ds.active)
                        {
                            var delta = evt.position.x - ds.startX;
                            var speed = evt.shiftKey ? 0.01 : evt.ctrlKey ? 10.0 : 0.5;
                            var newVal = ds.baseVal + delta * speed;
                            tf.SetValueWithoutNotify(newVal.ToString(GENERAL_NUMBER_FORMAT));
                            _window.SetFieldValue(_entityId, _compName, fieldKey,
                                FieldValue.FromNumber(newVal));
                        }
                    });
                    tf.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        if (evt.button == 1 && _dragStates[capturedIdx].active)
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
                                : EcsDebugV2Theme.GlassBorderStrong,
                            flexShrink = 0,
                            marginRight = 6,
                            overflow = Overflow.Hidden
                        },
                        name = "bool-track"
                    };
                    track.SetupRadius(9);
                    track.SetupBorder(isOn ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong);
                    track.style.transitionDuration =
                        new List<TimeValue> { new(0.15f, TimeUnit.Second) };
                    track.style.transitionProperty =
                        new List<StylePropertyName> { new("background-color") };
                    track.RegisterCallback<MouseEnterEvent>(_ => track.style.opacity = 0.85f);
                    track.RegisterCallback<MouseLeaveEvent>(_ => track.style.opacity = 1f);

                    var thumb = new VisualElement
                    {
                        style =
                        {
                            width = 14,
                            height = 14,
                            backgroundColor = isOn ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText,
                            marginTop = 1,
                            marginLeft = isOn ? 20 : 2,
                            flexShrink = 0
                        },
                        name = "bool-thumb"
                    };
                    thumb.SetupRadius(7);
                    thumb.style.transitionDuration =
                        new List<TimeValue> { new(0.15f, TimeUnit.Second) };
                    thumb.style.transitionProperty = new List<StylePropertyName>
                    {
                        new("margin-left"),
                        new("background-color")
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
                        var newVal = !r.lastBoolVal;
                        r.lastBoolVal = newVal;
                        _rows[capturedIdx] = r;
                        _window.SetFieldValue(_entityId, _compName, fieldKey,
                            FieldValue.FromBool(newVal));

                        thumb.style.marginLeft = newVal ? 20 : 2;
                        thumb.style.backgroundColor =
                            newVal ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
                        track.style.backgroundColor =
                            newVal ? EcsDebugV2Theme.LimeA03 : EcsDebugV2Theme.GlassBorderStrong;
                        track.style.borderTopColor =
                            newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong;
                        track.style.borderBottomColor =
                            newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong;
                        track.style.borderLeftColor =
                            newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong;
                        track.style.borderRightColor =
                            newVal ? EcsDebugV2Theme.LimeA05 : EcsDebugV2Theme.GlassBorderStrong;
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
                            backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.5f),
                            flexGrow = 1
                        }
                    };
                    tf.SetupBorder(Color.clear, 0);
                    tf.Q(TEXT_INPUT).style.backgroundColor = Color.clear;
                    tf.Q(TEXT_INPUT).SetupBorder(Color.clear, 0);
                    tf.Q(TEXT_INPUT).style.paddingLeft = 2;
                    tf.Q(TEXT_INPUT).style.paddingRight = 2;
                    tf.Q(TEXT_INPUT).style.borderBottomWidth = 1;
                    tf.Q(TEXT_INPUT).style.borderBottomColor = EcsDebugV2Theme.GlassBorder;

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

                    var link = new Label($"#{value.EntityRefVal}    ")
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.TypeEntity,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.5f)
                        }
                    };
                    link.RegisterCallback<MouseEnterEvent>(_ => link.style.opacity = 0.7f);
                    link.RegisterCallback<MouseLeaveEvent>(_ => link.style.opacity = 1f);

                    var capturedIdx = rowIdx;
                    link.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (_window != null && _rows[capturedIdx].lastEntityRefVal > 0)
                            _window.SelectEntity(_rows[capturedIdx].lastEntityRefVal);
                    });
                    container.Add(link);

                    var capturedFieldKey = fieldKey;
                    Button editBtn = default;
                    editBtn = new Button(() =>
                    {
                        var currentId = _rows[capturedIdx].lastEntityRefVal;
                        var popup = new TextField
                        {
                            value = currentId.ToString(),
                            style =
                            {
                                fontSize = EcsDebugV2Theme.Font.FieldName,
                                color = EcsDebugV2Theme.TypeEntity,
                                backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.6f),
                                width = 60
                            }
                        };
                        popup.SetupBorder(EcsDebugV2Theme.TypeEntity);
                        popup.Q(TEXT_INPUT).style.backgroundColor = Color.clear;
                        popup.RegisterValueChangedCallback(evt =>
                        {
                            if (int.TryParse(evt.newValue, out var n))
                            {
                                _window.SetFieldValue(_entityId, _compName, capturedFieldKey,
                                    FieldValue.FromEntityRef(n));
                                link.text = $"#{n}  ";
                            }
                        });
                        popup.RegisterCallback<FocusOutEvent>(_ =>
                        {
                            _rows[capturedIdx].editor.Remove(popup);
                            link.style.display = DisplayStyle.Flex;
                            _rows[capturedIdx].entityEditBtn.style.display = DisplayStyle.Flex;
                        });
                        link.style.display = DisplayStyle.None;
                        // ReSharper disable once AccessToModifiedClosure
                        editBtn!.style.display = DisplayStyle.None;
                        _rows[capturedIdx].editor.Add(popup);
                        popup.Focus();
                    })
                    {
                        text = "\u270E",
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.MutedText,
                            backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.5f),
                            paddingLeft = 4,
                            paddingRight = 4,
                            paddingTop = 1,
                            paddingBottom = 1,
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

                case FieldValueType.Enum:
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

                    var selectedIdx = value.EnumSelectedIndex >= 0 && value.EnumSelectedIndex < value.EnumNames?.Length
                        ? value.EnumSelectedIndex
                        : 0;
                    var displayText = value.EnumNames != null && value.EnumNames.Length > 0
                        ? $"{value.EnumNames[selectedIdx]} \u25BC"
                        : $"{value.EnumRawValue} \u25BC";

                    var btn = new Button
                    {
                        text = displayText,
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.Amber,
                            backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.5f),
                            paddingLeft = 9,
                            paddingRight = 9,
                            paddingTop = 3,
                            paddingBottom = 3,
                            unityTextAlign = TextAnchor.MiddleLeft,
                            flexGrow = 1
                        }
                    };
                    btn.SetupBorder(EcsDebugV2Theme.AmberA03, 1);
                    btn.SetupRadius(EcsDebugV2Theme.BorderRadius);

                    var capturedIdx = rowIdx;
                    var capturedFieldKey = fieldKey;
                    btn.RegisterCallback<ClickEvent>(_ =>
                    {
                        var r = _rows[capturedIdx];
                        if (r.lastEnumNames == null || r.lastEnumNames.Length == 0) return;
                        var menu = new GenericMenu();
                        for (int ei = 0; ei < r.lastEnumNames.Length; ei++)
                        {
                            var name = r.lastEnumNames[ei];
                            var idx = ei;
                            var rawVal = r.lastEnumRawValues != null && idx < r.lastEnumRawValues.Length
                                ? r.lastEnumRawValues[idx]
                                : (long)idx;
                            menu.AddItem(new GUIContent(name), idx == r.lastEnumIndex, () =>
                            {
                                var enumVal = FieldValue.FromEnum(r.lastEnumNames, r.lastEnumRawValues, idx, rawVal);
                                _window.SetFieldValue(_entityId, _compName, capturedFieldKey, enumVal);
                            });
                        }
                        menu.DropDown(btn.worldBound);
                    });

                    container.Add(btn);
                    editor = container;
                    enumButton = btn;
                    break;
                }

                case FieldValueType.ObjectRef:
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

                    if (value.IsUnityObject)
                    {
                        var imgui = new IMGUIContainer(() =>
                        {
                            var rIdx = capturedRowIdx;
                            if (rIdx < 0 || rIdx >= _rows.Length) return;
                            var r = _rows[rIdx];
                            var currentObj = EditorUtility.InstanceIDToObject(r.lastObjectInstanceId);
                            var objTypeResolved = r.objectFieldType ?? typeof(UnityEngine.Object);
                            var newObj = EditorGUILayout.ObjectField(currentObj, objTypeResolved, true,
                                GUILayout.Height(18));
                            if (newObj != currentObj)
                            {
                                var newName = newObj != null ? newObj.name : "null";
                                var newId = newObj != null ? newObj.GetInstanceID() : 0;
                                _window.SetFieldValue(_entityId, _compName, fieldKey,
                                    FieldValue.FromObjectRef(objTypeResolved.Name, newName, newId, true));
                            }
                        })
                        {
                            style =
                            {
                                flexGrow = 1
                            }
                        };

                        container.Add(imgui);
                        editor = container;
                        objectFieldContainer = imgui;
                    }
                    else
                    {
                        var infoLabel = new Label(string.IsNullOrEmpty(value.ObjectName) || value.ObjectName == "null"
                            ? $"{value.ObjectTypeName}: null"
                            : $"{value.ObjectTypeName}: {value.ObjectName}")
                        {
                            name = $"managed-ref-{fieldKey}",
                            style =
                            {
                                fontSize = EcsDebugV2Theme.Font.FieldName,
                                color = EcsDebugV2Theme.MutedText,
                                flexGrow = 1
                            }
                        };
                        container.Add(infoLabel);
                        editor = container;
                    }

                    break;
                }

                case FieldValueType.ComponentArray:
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

                    var elemName = string.IsNullOrEmpty(value.ArrayElementTypeName)
                        ? "?"
                        : value.ArrayElementTypeName;
                    var infoLabel = new Label($"{elemName}[{value.ArrayLength}]")
                    {
                        style =
                        {
                            fontSize = EcsDebugV2Theme.Font.FieldName,
                            color = EcsDebugV2Theme.TypeNumber,
                            unityFontStyleAndWeight = FontStyle.Bold
                        }
                    };
                    container.Add(infoLabel);
                    editor = container;
                    break;
                }

                default:
                    editor = new Label("\u2014") { style = { color = EcsDebugV2Theme.MutedText } };
                    break;
            }

            if (value.Type == FieldValueType.Number && mainTf != null)
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
                        var speed = evt.shiftKey ? 0.01 : evt.ctrlKey ? 10.0 : 0.5;
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
                r.isHovered = true;
                r.lastBgColor = EcsDebugV2Theme.SurfaceHover;
                _rows[ri] = r;
                r.row.style.backgroundColor = EcsDebugV2Theme.SurfaceHover;
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var r = _rows[ri];
                r.isHovered = false;
                _rows[ri] = r;
            });

            _rows[rowIdx] = new FieldRow
            {
                row = row,
                editor = editor,
                mainTextField = mainTf,
                fieldKey = fieldKey,
                fieldIndex = fieldIndex,
                changeKey = "",
                lastNumberVal = value.Type == FieldValueType.Number ? value.NumberVal : 0,
                lastStringVal = value.Type == FieldValueType.String ? value.StringVal : null,
                lastBoolVal = value is { Type: FieldValueType.Bool, BoolVal: true },
                lastEntityRefVal = value.Type == FieldValueType.EntityRef ? value.EntityRefVal : 0,
                isHovered = false,
                valueType = value.Type,
                isVector = false,
                boolTrack = boolTrack,
                boolThumb = boolThumb,
                boolLabel = boolLabel,
                entityLink = entityLink,
                entityEditBtn = entityEditBtn,
                keyLabel = keyLabel,
                lastEnumNames = value.Type == FieldValueType.Enum ? value.EnumNames : null,
                lastEnumRawValues = value.Type == FieldValueType.Enum ? value.EnumRawValues : null,
                lastEnumIndex = value.Type == FieldValueType.Enum ? value.EnumSelectedIndex : 0,
                lastEnumRawValue = value.Type == FieldValueType.Enum ? value.EnumRawValue : 0,
                lastObjectName = value.Type == FieldValueType.ObjectRef ? value.ObjectName : null,
                lastObjectInstanceId = value.Type == FieldValueType.ObjectRef ? value.ObjectInstanceId : 0,
                lastObjectTypeName = value.Type == FieldValueType.ObjectRef ? value.ObjectTypeName : null,
                lastArrayElementTypeName = value.Type == FieldValueType.ComponentArray ? value.ArrayElementTypeName : null,
                lastArrayLength = value.Type == FieldValueType.ComponentArray ? value.ArrayLength : 0,
                lastIsUnityObject = value.Type == FieldValueType.ObjectRef && value.IsUnityObject,
                objectFieldContainer = objectFieldContainer,
                objectFieldType = null,
                enumButton = enumButton
            };
        }

        private void BuildVectorRow(ComponentInfo template, string prefix, List<int> fieldIndices, int rowIdx)
        {
            var row = new VisualElement
            {
                // ReSharper disable once StringLiteralTypo
                name = $"frow-{_compName}-{prefix}",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 3,
                    paddingBottom = 3,
                    backgroundColor = EcsDebugV2Theme.PanelElevated,
                    transitionDuration = new List<TimeValue> { new(0.1f, TimeUnit.Second) },
                    transitionProperty = new List<StylePropertyName> { new("background-color") }
                }
            };

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

            for (var i = 0; i < fieldIndices.Count; i++)
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

            for (var i = 0; i < subFieldNames.Length; i++)
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
                        flexBasis = 0,
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
                        width = 14,
                        flexShrink = 0
                    }
                };
                ApplyEwCursor(subLabel);
                subGroup.Add(subLabel);

                var subTextField = new TextField
                {
                    value = (i < subValues.Length ? subValues[i].NumberVal : 0).ToString(GENERAL_NUMBER_FORMAT),
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = EcsDebugV2Theme.Font.FieldName,
                        color = color,
                        backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.5f),
                        flexGrow = 1,
                        flexShrink = 1,
                        minWidth = 0
                    }
                };
                subTextField.SetupBorder(Color.clear, 0);
                subTextField.Q(TEXT_INPUT).style.backgroundColor = Color.clear;
                subTextField.Q(TEXT_INPUT).SetupBorder(Color.clear, 0);
                subTextField.Q(TEXT_INPUT).style.paddingLeft = 2;
                subTextField.Q(TEXT_INPUT).style.paddingRight = 2;

                var capturedSubKey = subFieldKeys[i];
                subTextField.RegisterValueChangedCallback(evt =>
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
                        _labelDragBaseVal = double.TryParse(subTextField.value, out var v) ? v : 0;
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
                        var speed = evt.shiftKey ? 0.01 : evt.ctrlKey ? 10.0 : 0.5;
                        var newVal = _labelDragBaseVal + delta * speed;
                        subTextField.SetValueWithoutNotify(newVal.ToString("G"));
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

                subGroup.Add(subTextField);
                editor.Add(subGroup);
                subTfs[i] = subTextField;
            }

            editor.name = $"editor-{_compName}-{prefix}";
            row.Add(editor);

            var subIndices = new int[fieldIndices.Count];
            fieldIndices.CopyTo(subIndices, 0);

            var ri = rowIdx;
            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                var r = _rows[ri];
                r.isHovered = true;
                r.lastBgColor = EcsDebugV2Theme.SurfaceHover;
                _rows[ri] = r;
                r.row.style.backgroundColor = EcsDebugV2Theme.SurfaceHover;
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                var r = _rows[ri];
                r.isHovered = false;
                _rows[ri] = r;
            });

            _rows[rowIdx] = new FieldRow
            {
                row = row,
                editor = editor,
                subTextFields = subTfs,
                fieldKey = prefix,
                subFieldKeys = subFieldKeys,
                fieldIndex = -1,
                subFieldIndices = subIndices,
                changeKey = "",
                isHovered = false,
                valueType = FieldValueType.Number,
                isVector = true,
                keyLabel = prefixLabel
            };
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
                    var prefix = key[..dotIdx];
                    var groupFields = new List<int> { i };
                    var j = i + 1;
                    while (j < comp.Fields.Count &&
                           comp.Fields[j].Key.StartsWith(prefix + "."))
                    {
                        groupFields.Add(j);
                        j++;
                    }

                    if (groupFields.Count > 1)
                    {
                        var allNumbers = true;
                        for (var k = 0; k < groupFields.Count; k++)
                            if (comp.Fields[groupFields[k]].Value.Type != FieldValueType.Number)
                            {
                                allNumbers = false;
                                break;
                            }

                        if (allNumbers)
                        {
                            groups.Add(new FieldGroup
                            {
                                prefix = prefix,
                                fieldIndices = groupFields
                            });
                            i = j;
                            continue;
                        }
                    }
                }

                groups.Add(new FieldGroup
                {
                    prefix = null,
                    fieldIndices = new List<int> { i }
                });
                i++;
            }

            return groups;
        }

        private struct FieldRow
        {
            public VisualElement row;
            public VisualElement editor;
            public TextField mainTextField;
            public TextField[] subTextFields;
            public string fieldKey;
            public string[] subFieldKeys;
            public int fieldIndex;
            public int[] subFieldIndices;
            public string changeKey;
            public double lastNumberVal;
            public string lastStringVal;
            public bool lastBoolVal;
            public int lastEntityRefVal;
            public Color lastBgColor;
            public bool isHovered;
            public FieldValueType valueType;
            public bool isVector;
            public VisualElement boolTrack;
            public VisualElement boolThumb;
            public Label boolLabel;
            public Label entityLink;
            public Button entityEditBtn;
            public Label keyLabel;
            public string[] lastEnumNames;
            public long[] lastEnumRawValues;
            public int lastEnumIndex;
            public long lastEnumRawValue;
            public string lastObjectName;
            public int lastObjectInstanceId;
            public string lastObjectTypeName;
            public string lastArrayElementTypeName;
            public int lastArrayLength;
            public bool lastIsUnityObject;
            public IMGUIContainer objectFieldContainer;
            public Type objectFieldType;
            public Button enumButton;
        }

        private struct DragState
        {
            public float startX;
            public double baseVal;
            public bool active;
        }

        private struct FieldGroup
        {
            public string prefix;
            public List<int> fieldIndices;
        }
    }
}
#endif