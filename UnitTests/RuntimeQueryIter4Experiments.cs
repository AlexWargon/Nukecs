using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs.Tests
{
    // Test-assembly-only candidate. The public query.iter() API is deliberately unchanged.
    public static unsafe class RuntimeQueryIter4DiagnosticApi
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static RuntimeStorageIter<RuntimeDenseRefs<T1, T2, T3, T4>> iter_compact_runtime<T1, T2, T3, T4>(
            this in Query<T1, T2, T3, T4> query)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            if (ComponentType<T1>.Data.category != ComponentCategory.Inline ||
                ComponentType<T2>.Data.category != ComponentCategory.Inline ||
                ComponentType<T3>.Data.category != ComponentCategory.Inline ||
                ComponentType<T4>.Data.category != ComponentCategory.Inline)
                throw new System.InvalidOperationException("Dense diagnostic requires four inline components.");
            query.TryGetQuery(out var raw);
            if (!raw.Ref.TryUseStorageIteration()) throw new System.InvalidOperationException("Dense diagnostic requires storage iteration.");
            return new RuntimeStorageIter<RuntimeDenseRefs<T1, T2, T3, T4>>(raw.Ptr);
        }
    }


    public unsafe interface IRuntimeDenseTuple
    {
        void SetStorage(ref StorageArchetype storage);
        bool Advance(byte* end);
        byte* GetEnd(int count);
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeDenseRefs<T1, T2, T3, T4> : IRuntimeDenseTuple
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
    {
        private byte* a;
        // 64-bit offsets avoid the mixed 32/64-bit tuple copy regression in x64 Mono.
        // A value copy retains all four addresses, including across block transitions.
        private long b;
        private long c;
        private long d;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetEnd(int count) => a + count * sizeof(T1);

        public void SetStorage(ref StorageArchetype storage)
        {
            var data = storage.data.Ptr;
            var firstOffset = storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T1>.Index));
            a = data + firstOffset;
            b = storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T2>.Index)) - firstOffset;
            c = storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T3>.Index)) - firstOffset;
            d = storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T4>.Index)) - firstOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Advance(byte* end)
        {
            var next = a + sizeof(T1);
            a = next;
            // Mono folds these guards away for equal-size components.
            if (sizeof(T2) != sizeof(T1)) b += sizeof(T2) - sizeof(T1);
            if (sizeof(T3) != sizeof(T1)) c += sizeof(T3) - sizeof(T1);
            if (sizeof(T4) != sizeof(T1)) d += sizeof(T4) - sizeof(T1);
            return next < end;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> p1, out Ref<T2> p2, out Ref<T3> p3, out Ref<T4> p4)
        { p1.data = (T1*)a; p2.data = (T2*)(a + b); p3.data = (T3*)(a + c); p4.data = (T4*)(a + d); }
    }

    // Diagnostic only: callers must verify four inline components and storage eligibility.
    public unsafe ref struct RuntimeStorageIter<TTuple> where TTuple : unmanaged, IRuntimeDenseTuple
    {
        private TTuple current;
        private readonly World.WorldUnsafe* world;
        private readonly int* storages;
        private readonly int storageCount;
        private int block;
        private byte* end;

        public RuntimeStorageIter(QueryUnsafe* query)
        {
            current = default;
            world = query->world;
            var matches = query->GetMatchingStorages();
            storages = matches.Ptr;
            storageCount = matches.Length;
            block = -1;
            end = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RuntimeStorageIter<TTuple> GetEnumerator() => this;
        public readonly TTuple Current { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => current; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            // Advance performs address arithmetic only. At the initial/block boundary,
            // NextStorage replaces those addresses before Current can be consumed.
            return current.Advance(end) || NextStorage();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool NextStorage()
        {
            while (++block < storageCount)
            {
                ref var storage = ref world->storagesList.Ptr[storages[block]].Ref;
                var count = storage.count;
                if (count <= 0) continue;
                current.SetStorage(ref storage);
                // Snapshot count once: rows appended while iterating do not extend this block.
                end = current.GetEnd(count);
                return true;
            }
            return false;
        }
    }
}
