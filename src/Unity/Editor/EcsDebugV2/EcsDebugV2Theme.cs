#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class EcsDebugV2Theme
    {
        public static readonly Color Background = new Color(0x1A / 255f, 0x1C / 255f, 0x24 / 255f);
        public static readonly Color Panel = new Color(0x1F / 255f, 0x21 / 255f, 0x29 / 255f);
        public static readonly Color PanelElevated = new Color(0x26 / 255f, 0x27 / 255f, 0x2E / 255f);
        public static readonly Color PanelBorder = new Color(0x32 / 255f, 0x34 / 255f, 0x3D / 255f);
        public static readonly Color Lime = new Color(0x8F / 255f, 0xD8 / 255f, 0x30 / 255f);
        public static readonly Color Orange = new Color(0xF5 / 255f, 0x80 / 255f, 0x0A / 255f);
        public static readonly Color Red = new Color(0xD9 / 255f, 0x26 / 255f, 0x26 / 255f);
        public static readonly Color Yellow = new Color(0xF5 / 255f, 0xD8 / 255f, 0x04 / 255f);
        public static readonly Color TypeNumber = new Color(0x5C / 255f, 0xC8 / 255f, 0xE6 / 255f);
        public static readonly Color TypeString = new Color(0xF5 / 255f, 0x9E / 255f, 0x38 / 255f);
        public static readonly Color TypeBool = new Color(0xC0 / 255f, 0x5E / 255f, 0xDB / 255f);
        public static readonly Color TypeEntity = new Color(0x8F / 255f, 0xD8 / 255f, 0x30 / 255f);
        public static readonly Color MutedText = new Color(0x8A / 255f, 0x8D / 255f, 0x9A / 255f);
        public static readonly Color Foreground = new Color(0xDD / 255f, 0xDE / 255f, 0xE3 / 255f);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static class Font
        {
            public const int Body = 13;
            public const int Small = 11;
            public const int Micro = 10;
            public const int Mini = 9;
        }

        public static VisualElement CreateSeparator()
        {
            return new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = PanelBorder,
                    flexShrink = 0
                }
            };
        }

        public static Label CreateBadge(string text, Color bg, Color fg, int fontSize = 10)
        {
            return new Label(text)
            {
                style =
                {
                    fontSize = fontSize,
                    color = fg,
                    backgroundColor = bg,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 8,
                    paddingRight = 8,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
        }

        public static VisualElement CreateCard()
        {
            return new VisualElement
            {
                style =
                {
                    backgroundColor = Panel,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = PanelBorder,
                    borderBottomColor = PanelBorder,
                    borderLeftColor = PanelBorder,
                    borderRightColor = PanelBorder,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    overflow = Overflow.Hidden
                }
            };
        }

        public static Label CreateFilterTag(string text, bool positive)
        {
            var bgColor = positive ? Lime.WithAlpha(0.1f) : Red.WithAlpha(0.1f);
            var fgColor = positive ? Lime : Red;
            var borderColor = positive ? Lime.WithAlpha(0.3f) : Red.WithAlpha(0.3f);
            var label = new Label(text)
            {
                style =
                {
                    fontSize = Font.Micro,
                    color = fgColor,
                    backgroundColor = bgColor,
                    paddingTop = 1,
                    paddingBottom = 1,
                    paddingLeft = 6,
                    paddingRight = 6,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = borderColor,
                    borderBottomColor = borderColor,
                    borderLeftColor = borderColor,
                    borderRightColor = borderColor
                }
            };
            return label;
        }

        public static VisualElement CreateGlowDot(Color color, float size)
        {
            return new VisualElement
            {
                style =
                {
                    width = size,
                    height = size,
                    borderTopLeftRadius = size / 2f,
                    borderTopRightRadius = size / 2f,
                    borderBottomLeftRadius = size / 2f,
                    borderBottomRightRadius = size / 2f,
                    backgroundColor = color
                }
            };
        }

        public static TextField CreateSearchField(string placeholder, System.Action<string> onChanged)
        {
            var tf = new TextField
            {
                style =
                {
                    fontSize = Font.Small,
                    color = Foreground,
                    flexGrow = 1,
                    backgroundColor = Panel,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = PanelBorder,
                    borderBottomColor = PanelBorder,
                    borderLeftColor = PanelBorder,
                    borderRightColor = PanelBorder,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    paddingLeft = 6,
                    paddingRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4,
                    maxWidth = 240
                }
            };
            tf.Q("unity-text-input").style.backgroundColor = Color.clear;
            tf.Q("unity-text-input").style.borderLeftWidth = 0;
            tf.Q("unity-text-input").style.borderRightWidth = 0;
            tf.Q("unity-text-input").style.borderTopWidth = 0;
            tf.Q("unity-text-input").style.borderBottomWidth = 0;
            tf.Q("unity-text-input").style.paddingLeft = 0;
            tf.Q("unity-text-input").style.paddingRight = 0;
            tf.Q("unity-text-input").style.marginLeft = 0;
            tf.Q("unity-text-input").style.marginRight = 0;
            tf.SetValueWithoutNotify("");
            tf.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            return tf;
        }
    }
}
#endif
