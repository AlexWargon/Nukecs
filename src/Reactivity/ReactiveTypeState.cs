using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// По-(мир, тип) неуправляемое состояние для скомпилированного с Burst конвейера проверки.
    /// Все поля являются blittable — структура может находиться в <see cref="NativeList{T}"/>
    /// и адресоваться через сырой указатель из задачи Burst.
    ///
    /// Старые значения компонентов хранятся в плоском байтовом буфере <see cref="Values"/>,
    /// индексируемом через <see cref="Offsets"/> (entityId → смещение в байтах). Это позволяет
    /// задаче проверки оставаться неуниверсальной (non-generic): ей не нужно знать тип T, только его размер.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ReactiveTypeState : IDisposable
    {
        public int TypeIndex;
        public int ComponentSize;

        // entityId → байтовое смещение в Values, где хранится снимок старого значения.
        public NativeHashMap<int, int> Offsets;
        // Плоский байтовый буфер старых значений, плотно упакованный по ComponentSize.
        public NativeList<byte> Values;
        // Идентификаторы сущностей (EntityIds), у которых есть хотя бы одна подписка на сущность. Сканируются задачей.
        public NativeList<int> Alive;
        // Очередь с блокировкой (spinlock), заполняемая задачей проверки (потокобезопасной) и очищаемая при диспетчеризации.
        public ChangedQueue<int> Changed;
        // Читаемый Burst аналог TriggerPending (отложенный TriggerImmediately).
        public NativeHashMap<int, byte> PendingTriggers;

        public bool IsCreated => Values.IsCreated;

        public void Initialize(int typeIndex, int componentSize, int initialCapacity = 16)
        {
            TypeIndex = typeIndex;
            ComponentSize = componentSize;
            Offsets = new NativeHashMap<int, int>(initialCapacity, Allocator.Persistent);
            Values = new NativeList<byte>(initialCapacity * componentSize, Allocator.Persistent);
            Alive = new NativeList<int>(initialCapacity, Allocator.Persistent);
            Changed = new ChangedQueue<int>(initialCapacity, Allocator.Persistent);
            PendingTriggers = new NativeHashMap<int, byte>(4, Allocator.Persistent);
        }

        /// <summary>Добавляет блок необработанных байтов в <see cref="Values"/> и возвращает его смещение.</summary>
        public int AppendBytes(byte* src)
        {
            int start = Values.Length;
            int newLen = start + ComponentSize;
            if (newLen > Values.Capacity)
            {
                int newCap = Values.Capacity > 0 ? Values.Capacity : 16;
                while (newCap < newLen) newCap *= 2;
                Values.Capacity = newCap;
            }
            Values.ResizeUninitialized(newLen);
            UnsafeUtility.MemCpy((byte*)Values.GetUnsafePtr() + start, src, ComponentSize);
            return start;
        }

        public void Dispose()
        {
            if (Offsets.IsCreated) Offsets.Dispose();
            if (Values.IsCreated) Values.Dispose();
            if (Alive.IsCreated) Alive.Dispose();
            Changed.Dispose();
            if (PendingTriggers.IsCreated) PendingTriggers.Dispose();
        }
    }
}
