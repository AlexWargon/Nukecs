using System.Collections.Generic;
using Unity.Collections;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Реестр всех состояний реактивных типов для каждого мира. Содержится в
    /// <see cref="ReactiveWorldRegistry"/> и адресуется неуниверсальной системой
    /// <see cref="ReactiveCheckSystem"/> при планировании задачи проверки.
    ///
    /// Это КЛАСС (а не структура) намеренно — современная Unity.Collections
    /// хранит <see cref="NativeList{T}.Length"/> как встроенное поле структуры NativeList,
    /// поэтому структура, содержащая NativeList, скопировала бы поле Length при присваивании,
    /// и мутации, сделанные через копию, не были бы видны другим владельцам оригинала.
    /// </summary>
    public sealed class ReactiveWorldState : System.IDisposable
    {
        // Плоский список состояний типов — задача Burst итерирует его через сырой указатель.
        public NativeList<ReactiveTypeState> TypeStates;
        // typeIndex → индекс в TypeStates.
        public NativeHashMap<int, int> TypeIndexToStateIdx;

        public bool IsCreated => TypeStates.IsCreated;

        public void Initialize()
        {
            TypeStates = new NativeList<ReactiveTypeState>(4, Allocator.Persistent);
            TypeIndexToStateIdx = new NativeHashMap<int, int>(4, Allocator.Persistent);
        }

        public ref ReactiveTypeState GetOrCreate(int typeIndex, int componentSize)
        {
            if (TypeIndexToStateIdx.TryGetValue(typeIndex, out var idx))
                return ref TypeStates.ElementAt(idx);

            idx = TypeStates.Length;
            // Увеличиваем TypeStates — примечание: это может переместить базовый буфер. Существующие
            // указатели, полученные через GetUnsafePtr(), становятся недействительными; вызывающие стороны должны
            // получать их заново. Мы никогда не кэшируем указатель между мутациями.
            TypeStates.ResizeUninitialized(idx + 1);
            ref var state = ref TypeStates.ElementAt(idx);
            state.Initialize(typeIndex, componentSize);
            TypeIndexToStateIdx.TryAdd(typeIndex, idx);
            return ref state;
        }

        public bool TryGet(int typeIndex, out int stateIdx)
        {
            return TypeIndexToStateIdx.TryGetValue(typeIndex, out stateIdx);
        }

        public void Dispose()
        {
            if (TypeStates.IsCreated)
            {
                for (int i = 0; i < TypeStates.Length; i++)
                    TypeStates.ElementAt(i).Dispose();
                TypeStates.Dispose();
            }
            if (TypeIndexToStateIdx.IsCreated) TypeIndexToStateIdx.Dispose();
        }
    }
}
