#if UNITY_EDITOR
namespace Wargon.Nukecs.Editor
{
    public class DebugListItem
    {
        public enum ItemType { Entity, Archetype, Query }

        public ItemType Type;
        public int Id;
        public string DisplayName;

        public DebugListItem(ItemType type, int id, string displayName)
        {
            Type = type;
            Id = id;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}
#endif