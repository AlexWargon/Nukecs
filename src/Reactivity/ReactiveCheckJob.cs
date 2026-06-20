using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Скомпилированная с Burst задача проверки. Итерирует все состояния реактивных типов
    /// для мира параллельно (один тип на воркер). Для каждого типа сканирует
    /// список сущностей <c>Alive</c>, находит байтовый указатель (byte*) компонента в архетипе
    /// через неуниверсальный (non-generic) API и сравнивает его с сохраненным старым значением через MemCmp.
    ///
    /// Задача полностью неуниверсальна (старые значения хранятся как сырые байты),
    /// что и делает компиляцию Burst стабильной — специализация универсальных типов (generic specialization) не нужна.
    /// </summary>
    [BurstCompile]
    public unsafe struct ReactiveCheckJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction]
        public World.WorldUnsafe* WorldPtr;

        [NativeDisableUnsafePtrRestriction]
        public ReactiveTypeState* States;

        public void Execute(int stateIdx)
        {
            ref var state = ref States[stateIdx];
            var sz = state.ComponentSize;
            var typeIdx = state.TypeIndex;

            var entityLocationsPtr = WorldPtr->entityLocations.Ptr;
            var archetypesListPtr = WorldPtr->archetypesList.Ptr;
            var entitiesPtr = WorldPtr->entities.Ptr;
            var valuesBase = (byte*)state.Values.GetUnsafePtr();
            var alive = state.Alive;
            var offsets = state.Offsets;
            var changed = state.Changed;
            var pending = state.PendingTriggers;

            // Сканируем подписанные сущности. Каждый воркер обрабатывает один тип
            // состояния, поэтому здесь нет конкуренции (contention).
            for (int i = 0; i < alive.Length; i++)
            {
                var id = alive[i];

                // Проверка валидности: слот пуст, если id==0.
                if (entitiesPtr[id].id == 0) continue;

                var loc = entityLocationsPtr[id];
                var arch = archetypesListPtr[loc.archetypeIndex].Ptr;
                if (!arch->Has(typeIdx)) continue;

                var localIdx = arch->GetComponentLocalIndex(typeIdx);
                var offset = arch->GetComponentOffset(localIdx);
                byte* currentPtr = arch->data.Ptr + offset + loc.row * sz;

                if (offsets.TryGetValue(id, out var oldOffset))
                {
                    byte* oldPtr = valuesBase + oldOffset;
                    if (UnsafeUtility.MemCmp(currentPtr, oldPtr, sz) != 0)
                    {
                        UnsafeUtility.MemCpy(oldPtr, currentPtr, sz);
                        changed.EnqueuePar(id);
                    }
                }
                else
                {
                    // Начальная загрузка (Bootstrap): копируем текущее значение в плоский буфер.
                    var newOffset = state.AppendBytes(currentPtr);
                    offsets.TryAdd(id, newOffset);

                    // Поглощаем отложенный триггер (отложенный TriggerImmediately).
                    if (pending.TryGetValue(id, out var p) && p != 0)
                    {
                        pending.Remove(id);
                        changed.EnqueuePar(id);
                    }
                }
            }
        }
    }
}
