using System;
using System.Collections.Generic;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Управляемое хранилище по (мир, тип) для диспетчеризации и моста подписок.
    /// Содержит списки диспетчеризации (управляемые делегаты) и синхронизирует
    /// вызовы Subscribe/Unsubscribe с неуправляемым <see cref="ReactiveTypeState"/>,
    /// из которого читает скомпилированная с Burst задача <see cref="ReactiveCheckJob"/>.
    /// </summary>
    internal sealed class ReactiveStorage<T> : IDisposable where T : unmanaged, IComponent
    {
        public readonly int WorldId;
        public readonly int TypeIndex;
        public readonly int ComponentSize;

        // Подписки по сущностям для диспетчеризации.
        public Dictionary<int, List<Subscription<T>>> ManagedPerEntity = new();
        private readonly Dictionary<long, Subscription<T>> _byToken = new();
        private long _nextToken = 1;

        public ReactiveStorage(World world)
        {
            WorldId = world.Id;
            TypeIndex = ComponentType<T>.Index;
            ComponentSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();

            // Убеждаемся, что неуправляемый ReactiveTypeState существует в этом мире.
            var worldState = ReactiveWorldRegistry.GetOrCreate(world);
            worldState.GetOrCreate(TypeIndex, ComponentSize);
        }

        public ref ReactiveTypeState TypeStateRef
        {
            get
            {
                ReactiveWorldRegistry.TryGet(WorldId, out var worldState);
                if (!worldState.TypeIndexToStateIdx.TryGetValue(TypeIndex, out var idx)
                    || idx >= worldState.TypeStates.Length)
                {
                    worldState.GetOrCreate(TypeIndex, ComponentSize);
                    worldState.TypeIndexToStateIdx.TryGetValue(TypeIndex, out idx);
                }
                return ref worldState.TypeStates.ElementAt(idx);
            }
        }

        public long AddEntitySubscription(int entityId, Subscription<T> sub)
        {
            sub.Token = _nextToken++;
            sub.EntityId = entityId;
            _byToken[sub.Token] = sub;

            if (!ManagedPerEntity.TryGetValue(entityId, out var list))
            {
                list = new List<Subscription<T>>(2);
                ManagedPerEntity[entityId] = list;
            }
            list.Add(sub);

            // Отражаем (Mirror) в неуправляемом состоянии — добавляем entityId в Alive, если его там нет.
            ref var ts = ref TypeStateRef;
            var alive = ts.Alive;
            for (int i = 0; i < alive.Length; i++)
                if (alive[i] == entityId) return sub.Token;
            alive.Add(entityId);
            return sub.Token;
        }

        public bool Remove(long token)
        {
            if (!_byToken.TryGetValue(token, out var sub)) return false;
            _byToken.Remove(token);

            if (sub.EntityId >= 0 && ManagedPerEntity.TryGetValue(sub.EntityId, out var list))
            {
                list.Remove(sub);
                if (list.Count == 0) ManagedPerEntity.Remove(sub.EntityId);
            }

            sub.Dispose();
            return true;
        }

        public void RemoveAllForEntity(int entityId)
        {
            if (ManagedPerEntity.TryGetValue(entityId, out var list))
            {
                foreach (var s in list) { _byToken.Remove(s.Token); s.Dispose(); }
                list.Clear();
                ManagedPerEntity.Remove(entityId);
            }

            ref var ts = ref TypeStateRef;
            for (int i = 0; i < ts.Alive.Length; i++)
            {
                if (ts.Alive[i] == entityId)
                {
                    ts.Alive.RemoveAtSwapBack(i);
                    break;
                }
            }
            ts.Offsets.Remove(entityId);
        }

        public void Dispose()
        {
            foreach (var kv in ManagedPerEntity)
                foreach (var s in kv.Value) s.Dispose();
            ManagedPerEntity.Clear();
            _byToken.Clear();
        }
    }

    /// <summary>
    /// Реестр <see cref="ReactiveStorage{T}"/> по (мир, тип). Статический конструктор каждого
    /// закрытого универсального типа регистрирует свой DisposeAll в <see cref="ReactiveStorageAll"/>,
    /// так что StaticCleanup может очистить все хранилища без перечисления закрытых универсальных типов.
    /// </summary>
    internal static class ReactiveStorageRegistry<T> where T : unmanaged, IComponent
    {
        private static readonly Dictionary<int, ReactiveStorage<T>> ByWorldId = new();
        private static readonly object Lock = new();

        static ReactiveStorageRegistry()
        {
            ReactiveStorageAll.Register(DisposeAll);
        }

        public static ReactiveStorage<T> GetOrCreate(World world)
        {
            lock (Lock)
            {
                if (!ByWorldId.TryGetValue(world.Id, out var s))
                {
                    s = new ReactiveStorage<T>(world);
                    ByWorldId[world.Id] = s;
                }
                return s;
            }
        }

        public static bool TryGet(int worldId, out ReactiveStorage<T> storage)
        {
            lock (Lock) return ByWorldId.TryGetValue(worldId, out storage);
        }

        public static void DisposeAll()
        {
            List<ReactiveStorage<T>> snapshot;
            lock (Lock)
            {
                snapshot = new List<ReactiveStorage<T>>(ByWorldId.Values);
                ByWorldId.Clear();
            }
            foreach (var s in snapshot) s.Dispose();
        }
    }

    /// <summary>
    /// Реестр кросс-типовых callback-функций DisposeAll.
    /// </summary>
    internal static class ReactiveStorageAll
    {
        private static readonly List<Action> DisposeCallbacks = new();
        private static readonly object Lock = new();

        public static void Register(Action dispose)
        {
            lock (Lock)
            {
                if (!DisposeCallbacks.Contains(dispose))
                    DisposeCallbacks.Add(dispose);
            }
        }

        public static void DisposeAll()
        {
            List<Action> snapshot;
            lock (Lock) snapshot = new List<Action>(DisposeCallbacks);
            foreach (var cb in snapshot) cb();
        }
    }
}
