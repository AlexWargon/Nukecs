using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using Wargon.Nukecs.Collections;
// ReSharper disable InconsistentNaming
// ReSharper disable StaticMemberInGenericType

namespace Wargon.Nukecs
{
    public unsafe struct ResStorage
    {
        // Res/ResManaged params keyed by a stable 64-bit type hash (StableTypeHash), Local params
        // keyed by (owner, scope, instance). Both keys are identical in every session, so a world
        // saved in another session resolves its params regardless of first-touch order.
        private struct ResEntry
        {
            public long hash;
            public ptr param;
            // 1 = the param's first field is a ptr to a separate value block (Res<T>)
            public int hasValuePtr;
            public int paramSize;
            public int valueSize;
        }

        // Local<T> params. Every Local holds a ptr to its value block as the first field.
        private struct LocalEntry
        {
            public LocalParamSlots.Key key;
            public ptr param;
        }

        private MemoryList<LocalEntry> _locals;
        private MemoryList<ResEntry> _entries;

        public ResStorage(ref MemAllocator allocator)
        {
            _locals = new MemoryList<LocalEntry>(32, ref allocator);
            _entries = new MemoryList<ResEntry>(16, ref allocator);
        }

        public void OnDeserialize(ref MemAllocator allocator)
        {
            _locals.OnDeserialize(ref allocator);
            for (var i = 0; i < _locals.length; i++)
            {
                ref var local = ref _locals.Ptr[i];
                if (local.param.IsNull) continue;
                local.param.OnDeserialize(ref allocator);
                ((ptr*)local.param.cached)->OnDeserialize(ref allocator);
            }
            _entries.OnDeserialize(ref allocator);
            for (var i = 0; i < _entries.length; i++)
            {
                ref var entry = ref _entries.Ptr[i];
                if (entry.param.IsNull) continue;
                entry.param.OnDeserialize(ref allocator);
                if (entry.hasValuePtr != 0)
                    ((ptr*)entry.param.cached)->OnDeserialize(ref allocator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindEntry(long hash)
        {
            for (var i = 0; i < _entries.length; i++)
                if (_entries.Ptr[i].hash == hash) return i;
            return -1;
        }

        internal void MarkValuePtr(long hash, int valueSize)
        {
            var i = FindEntry(hash);
            if (i < 0) return;
            _entries.Ptr[i].hasValuePtr = 1;
            _entries.Ptr[i].valueSize = valueSize;
        }

        /// <summary>
        /// Resources are runtime state (they commonly own native containers whose pointers and
        /// safety handles are meaningless in another session), so they are not restored from a
        /// save. Capture the live world's resources before a load...
        /// </summary>
        internal LiveResources CaptureLive()
        {
            var live = new LiveResources();
            for (var i = 0; i < _entries.length; i++)
            {
                ref var entry = ref _entries.Ptr[i];
                if (entry.param.IsNull) continue;
                var item = new LiveResources.Item
                {
                    hash = entry.hash,
                    param = Copy(entry.param.cached, entry.paramSize)
                };
                if (entry.hasValuePtr != 0)
                    item.value = Copy(((ptr*)entry.param.cached)->cached, entry.valueSize);
                live.items.Add(item);
            }
            return live;
        }

        /// <summary>
        /// ...and rebuild the resource entries from that capture after it. Resources only present
        /// in the save are dropped and get created again (OnCreate) on first request.
        /// </summary>
        internal void RestoreLive(LiveResources live, World.WorldUnsafe* world)
        {
            _entries.length = 0;
            if (live == null) return;
            ref var allocator = ref world->AllocatorRef;
            foreach (var item in live.items)
            {
                var param = allocator.AllocatePtr<byte>(item.param.Length, AllocatorTags.WorldMisc).UntypedPointer;
                Paste(item.param, param.cached);
                var entry = new ResEntry { hash = item.hash, param = param, paramSize = item.param.Length };
                if (item.value != null)
                {
                    var value = allocator.AllocatePtr<byte>(item.value.Length, AllocatorTags.WorldMisc).UntypedPointer;
                    Paste(item.value, value.cached);
                    *(ptr*)param.cached = value;
                    entry.hasValuePtr = 1;
                    entry.valueSize = item.value.Length;
                }
                _entries.Add(entry, ref allocator);
            }
        }

        private static byte[] Copy(byte* source, int size)
        {
            var bytes = new byte[size];
            fixed (byte* destination = bytes) Buffer.MemoryCopy(source, destination, size, size);
            return bytes;
        }

        private static void Paste(byte[] bytes, byte* destination)
        {
            fixed (byte* source = bytes) Buffer.MemoryCopy(source, destination, bytes.Length, bytes.Length);
        }

        internal ptr<TParam> GetLocal<TParam>(int slot, World.WorldUnsafe* world)
            where TParam : unmanaged, ISystemParam
        {
            if (slot < 0 || default(TParam).MetaType != SystemParamMetaType.Local)
                throw new ArgumentException("A local slot requires a Local system parameter.");
            // slot ids are per session; the stored key is stable across sessions
            if (!LocalParamSlots.TryGetKey(slot, out var key))
                throw new InvalidOperationException("[Nukecs] Unknown local slot: it was not acquired in this session.");
            for (var i = 0; i < _locals.length; i++)
                if (_locals.Ptr[i].key.Equals(key))
                    return _locals.Ptr[i].param.AsTyped<TParam>();
            var value = world->AllocatorRef.AllocatePtr<TParam>();
            value.Ref = default;
            _locals.Add(new LocalEntry { key = key, param = value.UntypedPointer }, ref world->AllocatorRef);
            value.Ref.Init(ref world->selfPtr);
            return value;
        }

        internal (int len, IRes[]) GetAll(IRes[] cache)
        {
            var count = 0;
            if (_entries.length >= cache.Length)
                Array.Resize(ref cache, _entries.length + 1);
            foreach (var type in res_type.RegisteredTypes)
            {
                var data = res_type.data(type);
                var i = FindEntry(data.hash);
                if (i < 0) continue; // this world never added that resource
                cache[count++] = data.getBoxed(_entries.Ptr[i].param.cached);
            }

            return (count, cache);
        }

        internal ptr<T> GetRes<T>() where T : unmanaged
        {
            var i = FindEntry(StableTypeHash<T>.Value);
            if (i < 0)
                throw new InvalidOperationException($"[Nukecs] World does not have resource {typeof(T).Name}");
            res_type.Register<T>(); // entries restored from a save are registered on first typed access
            return _entries.Ptr[i].param.AsTyped<T>();
        }

        public IRes GetRes(Type type)
        {
            var data = res_type.data(type);
            var i = FindEntry(data.hash);
            if (i < 0)
                throw new InvalidOperationException($"[Nukecs] World does not have resource {type.Name}");
            return data.getBoxed(_entries.Ptr[i].param.cached);
        }

        public void SetRes(IRes res)
        {
            var data = res_type.data(res.GetType());
            var i = FindEntry(data.hash);
            if (i < 0)
                throw new InvalidOperationException($"[Nukecs] World does not have resource {res.GetType().Name}");
            data.setBoxed(_entries.Ptr[i].param.cached, res);
        }

        internal bool HasRes<T>() where T : unmanaged
        {
            return FindEntry(StableTypeHash<T>.Value) >= 0;
        }

        internal bool AddRes<T>(in T resource, World.WorldUnsafe* world) where T : unmanaged
        {
            if (HasRes<T>()) return false;
            res_type.Register<T>();
            var ptr = world->_allocate_ptr<T>(1, AllocatorTags.WorldMisc);
            ptr.Ref = resource;
            _entries.Add(new ResEntry
            {
                hash = StableTypeHash<T>.Value,
                param = ptr.UntypedPointer,
                paramSize = sizeof(T)
            }, ref world->AllocatorRef);
            return true;
        }
    }


    internal sealed class LiveResources
    {
        internal struct Item
        {
            public long hash;
            public byte[] param;
            public byte[] value;
        }

        internal readonly List<Item> items = new();
    }

    internal class ReflectionData
    {
        internal GetBoxDelegate getBoxed;
        internal long hash;
        internal SetBoxDelegate setBoxed;

        internal ReflectionData(long hash)
        {
            this.hash = hash;
        }

        internal static unsafe IRes GetRes<T>(byte* ptr)
            where T : struct
        {
            ref var wrapper = ref Unsafe.AsRef<T>(ptr);
            return ((IResourceGetSet)wrapper).GetResource();
        }

        internal static unsafe void SetRes<T>(byte* ptr, IRes val)
            where T : struct
        {
            ref var wrapper = ref Unsafe.AsRef<T>(ptr);
            var boxed = (IResourceGetSet)wrapper;
            boxed.SetResource(val);
            wrapper = (T)boxed;
        }
    }

    internal struct res_type
    {
        // Type -> boxing delegates + stable hash, used by debug tools (IResourceGetSet).
        private static readonly Dictionary<Type, ReflectionData> indexes = new();
        internal static IEnumerable<Type> RegisteredTypes => indexes.Keys;

        internal static ReflectionData data(Type type)
        {
            return indexes[type];
        }

        internal static unsafe void Register<T>() where T : unmanaged
        {
            if (indexes.ContainsKey(typeof(T))) return;
            indexes[typeof(T)] = new ReflectionData(StableTypeHash<T>.Value)
            {
                getBoxed = ReflectionData.GetRes<T>,
                setBoxed = ReflectionData.SetRes<T>
            };
        }
    }

    /// <summary>
    /// 64-bit FNV-1a of a type name without assembly/version info (generic arguments are
    /// expanded recursively), identical in every session. Managed only: do not read from Burst.
    /// </summary>
    internal static class StableTypeHash<T>
    {
        internal static readonly long Value = StableTypeHash.Compute(typeof(T));
    }

    internal static class StableTypeHash
    {
        internal static long Compute(Type type)
        {
            var hash = 14695981039346656037UL;
            foreach (var c in Name(type)) hash = unchecked((hash ^ c) * 1099511628211UL);
            return unchecked((long)hash);
        }

        private static string Name(Type type)
        {
            if (type.IsArray)
                return Name(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (!type.IsGenericType)
                return type.FullName ?? type.Name;
            return type.GetGenericTypeDefinition().FullName
                   + "[" + string.Join(",", type.GetGenericArguments().Select(Name)) + "]";
        }
    }

    internal unsafe delegate void SetBoxDelegate(byte* ptr, IRes val);

    internal unsafe delegate IRes GetBoxDelegate(byte* ptr);
}
