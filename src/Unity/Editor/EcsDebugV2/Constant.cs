#if UNITY_EDITOR && NUKECS_DEBUG
namespace Wargon.Nukecs.Editor.EcsDebugV2
{
#pragma warning disable CS0618
    internal static class Constant
    {
        public const string TAG_LABEL = "#tag";
        public const string GENERAL_NUMBER_FORMAT = "G";
        public const string TEXT_INPUT = "unity-text-input";
        public const int INSPECTOR_FIELD_REFRESH_MS = 16;
        public const int UI_LOW_PRIORITY_MS = 100;
        public const int LEFT_PANEL_REFRESH_MS = 100;
        public const int ENTITY_LIST_ITEM_HEIGHT = 20;
    }
}
#endif