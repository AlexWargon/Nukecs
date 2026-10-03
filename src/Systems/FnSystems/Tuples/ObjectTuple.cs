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
        private int* _rows;
        private int _rowIdx;

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
            current++;
            if (_rows != null)
            {
                _rowIdx++;
                _entities += _rows[_rowIdx] - _rows[_rowIdx - 1];
            }
            else
            {
                _entities++;
            }
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
            _rows = archetype.RowsAreDense ? null : archetype.rows.Ptr;
            _rowIdx = range.start;
            if (_rows != null) _entities += _rows[range.start];
            else _entities += range.start;
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
                var data = ComponentTypeMap.GetComponentType(index);
                if (data.category != ComponentCategory.Inline)
                    continue;
                var componentsPtr = System.Runtime.CompilerServices.Unsafe.AsPointer(ref components);
                if (_rows == null)
                {
                    var cptr = ptr + archetype.GetComponentOffset(archetype.GetComponentLocalIndex(index));
                    System.Runtime.CompilerServices.Unsafe
                        .CopyBlock(componentsPtr, cptr, (uint)(entitiesCount * data.size));
                }
                else
                {
                    for (var r = 0; r < entitiesCount; r++)
                    {
                        var src = archetype.GetComponentDataPtr(index, _rows[range.start + r]);
                        if (src == null) continue;
                        System.Runtime.CompilerServices.Unsafe
                            .CopyBlock((byte*)componentsPtr + r * data.size, src, (uint)data.size);
                    }
                }
            }
        }
    }
}