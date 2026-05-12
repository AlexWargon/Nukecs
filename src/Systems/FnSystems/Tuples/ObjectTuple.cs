using System;

namespace Wargon.Nukecs
{
    public unsafe struct ObjectTuple : IComponentEntityTupleRanged
    {
        private IComponent[][] componentsArray;
        private IComponent[] returnArray;
        private int* componentIndexes;
        private int componentCount;
        private int current;
        private int* _entities;
        
        private Entity* _allEntities;
        private Entity _entity;
        private Range _range;
        public Entity GetEntity() => _entity;
        public IComponent[] GetComponents()
        {
            return returnArray;
        }
        public void Add()
        {
            _entities++;
            current++;
            _entity = _allEntities[*_entities];
            for (var i = 0; i < componentCount; i++)
            {
                var index = componentIndexes[i];
                var components = componentsArray[index];
                returnArray[index] = components[current];
            }
        }

        public void SetData(ref ArchetypeUnsafe archetype, int* localEntities, Entity* globalEntities, Range range)
        {
            _range = range;
            var entitiesCount = _range.end - range.start + 1;
            _entities = localEntities;
            _allEntities = globalEntities;
            componentIndexes = archetype.types.Ptr;
            componentCount = archetype.types.length;
            byte* ptr = archetype.data.Ptr;
            if (componentsArray == null)
            {
                componentsArray = new IComponent[componentCount][];
            }
            for (var i = 0; i < componentCount; i++)
            {
                int index = componentIndexes[i];
                ref IComponent[] components = ref componentsArray[index];
                if (components == null)
                {
                    components = new IComponent[entitiesCount];
                }

                if (components.Length != entitiesCount)
                {
                    Array.Resize(ref components, entitiesCount);
                }
                var cptr = ptr + archetype.GetComponentOffset(index);
                var componentsPtr = System.Runtime.CompilerServices.Unsafe.AsPointer(ref components);
                var data = ComponentTypeMap.GetComponentType(index);
                System.Runtime.CompilerServices.Unsafe
                    .CopyBlock(componentsPtr, cptr, (uint)(entitiesCount * data.size));
            }
        }
    }
}