namespace Wargon.Nukecs {
    public unsafe struct Query {
        internal unsafe struct QueryImpl {
            internal BitMask with;
            internal BitMask none;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<int> entities;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<int> entitiesMap;
            internal int count;
            internal readonly World.WorldImpl* world;
            internal readonly QueryImpl* self;

            internal static QueryImpl* Create(World.WorldImpl* world) {
                var ptr = Unsafe.Malloc<QueryImpl>(world->allocator);
                *ptr = new QueryImpl(world, ptr);
                return ptr;
            }

            internal QueryImpl(World.WorldImpl* world, QueryImpl* self) {
                this.world = world;
                this.with = default;
                this.none = default;
                this.count = default;
                this.entities = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(world->config.StartEntitiesAmount, world->allocator);
                this.entitiesMap = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(world->config.StartEntitiesAmount, world->allocator);
                this.self = self;
            }
            public ref Entity GetEntity(int index) {
                return ref world->GetEntity(entities[index]);
            }
            internal bool Has(int entity) {
                if (entitiesMap.Length <= entity) return false;
                return entitiesMap[entity] > 0;
            }

            internal void Add(int entity) {
                if (entities.Length - 1 <= count) {
                    entities.Resize(count * 2);
                }
                if (entitiesMap.Length - 1 <= entity) {
                    entitiesMap.Resize(count * 2);
                }
                entities[count++] = entity;
                entitiesMap[entity] = count;
            }
            internal void Remove(int entity) {
                if (!Has(entity)) return;
                var index = entitiesMap[entity] - 1;
                entitiesMap[entity] = 0;
                count--;
                if (count > index) {
                    entities[index] = entities[count];
                    entitiesMap[entities[index]] = index + 1;
                }
            }

            public QueryImpl* With(int type) {
                with.Add(type);
                return self;
            }

            public bool HasWith(int type) {
                return with.Has(type);
            }

            public bool HasNone(int type) {
                return none.Has(type);
            }
            public QueryImpl* None(int type) {
                none.Add(type);
                return self;
            }
        }

        internal readonly QueryImpl* impl;

        internal Query(World.WorldImpl* world) {
            impl = QueryImpl.Create(world);
        }

        internal Query(QueryImpl* impl) {
            this.impl = impl;
        }

        public Query With<T>(){
            impl->With(ComponentMeta<T>.Index);
            return this;
        }

        public Query None<T>() {
            impl->None(ComponentMeta<T>.Index);
            return this;
        }

        public ref Entity GetEntity(int index) {
            return ref impl->GetEntity(index);
        }
    }

    public struct BitMask {
        public long mask;
        public int count;
        const int BitSize = (sizeof(int) * 8) - 1;
        const int ByteSize = 5;  // log_2(BitSize + 1)

        public static BitMask New() {
            BitMask mask;
            mask.count = 0;
            mask.mask = -1;
            return mask;
        }
        public bool Add(int item) {
            if (Has(item)) return false;
            mask |= (long)(1 << item);
            count++;
            return true;
        }

        public bool Has(int item) {
            return (mask & (long)(1 << item)) > 0;
        }
    }

    public struct Bit {
        public int Value;
    }
}