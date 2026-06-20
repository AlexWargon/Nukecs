using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Неуниверсальная (non-generic) система проверки. Планирует <see cref="ReactiveCheckJob"/>
    /// один раз за кадр для текущего мира. Задача обрабатывает все состояния реактивных типов
    /// параллельно (один тип на воркер).
    ///
    /// Существует только один экземпляр этой системы на мир (регистрируется автоматически
    /// при первом вызове <c>OnChange&lt;T&gt;</c>). Заменяет N универсальных систем проверки для каждого типа T
    /// одним скомпилированным с помощью Burst конвейером.
    /// </summary>
    public unsafe struct ReactiveCheckSystem : ISystem, IOnCreate
    {
        private World.WorldUnsafe* worldPtr;
        private ReactiveWorldState worldState;

        public void OnCreate(ref World world)
        {
            worldPtr = world.UnsafeWorld;
            worldState = ReactiveWorldRegistry.GetOrCreate(world);
        }

        public void OnUpdate(ref State state)
        {
            var count = worldState.TypeStates.Length;
            if (count == 0) return;

            var statesPtr = (ReactiveTypeState*)worldState.TypeStates.GetUnsafePtr();
            state.Dependencies = new ReactiveCheckJob
            {
                WorldPtr = worldPtr,
                States = statesPtr,
            }.Schedule(count, 1, state.Dependencies);
            // Store handle so dispatch can wait on THIS job only (not all dependencies).
            ReactiveJobSync.SetCheckHandle(worldPtr->Id, state.Dependencies);
        }
    }
}
