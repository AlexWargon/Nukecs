#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System.Collections.Generic;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public struct WorldInfo
    {
        public string name;
        public string[] worldNames;
        public int[] worldSlots;
    }

    public interface IEcsDataProvider
    {
        List<EntityInfo> GetEntityList();
        EntityInfo GetEntityDetails(int entityId);
        List<ArchetypeInfo> GetArchetypes();
        List<QueryInfo> GetQueries();
        List<ResourceInfo> GetResources();
        int SystemCount { get; }
        int Tick { get; set; }
        WorldInfo WorldInfo { get; }
        string[] AvailableComponentTypes { get; }
        int WorldCount { get; }
        EntityInfo CreateEntity();
        void DestroyEntity(int id);
        void AddComponent(int entityId, string compName);
        void RemoveComponent(int entityId, string compName);
        void SetFieldValue(int entityId, string compName, string fieldKey, FieldValue value);
        void SimulateTick(Dictionary<string, long> changes);
        void SetWorld(int worldIndex);
        int GetEntityCount();
        int GetArchetypeCount();
        int GetEntityArchetypeIndex(int id);
    }
}
#endif
