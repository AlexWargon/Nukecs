using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Callback invoked when a reactive component of type <typeparamref name="T"/> changes.
    /// Receives the new value and the owning entity.
    /// </summary>
    public delegate void ReactDelegate<T>(in T value, in Entity entity) where T : unmanaged, IComponent;

    /// <summary>
    /// Predicate filter for reactive subscriptions. Return <c>true</c> to fire
    /// the callback for the given (changed) value, <c>false</c> to skip.
    /// </summary>
    public delegate bool ReactFilter<T>(in T value) where T : unmanaged, IComponent;

    /// <summary>
    /// Query filter marker — only iterate entities whose component <typeparamref name="T"/>
    /// changed this frame. Implemented as <see cref="IFilter"/> (not <see cref="IComponent"/>)
    /// so the source generator treats it as a filter, not data.
    ///
    /// Usage in [System] methods:
    /// <code>
    /// [System, BurstCompile]
    /// static void DamageFlashSystem(ref Query&lt;SpriteColor, Changed&lt;Health&gt;&gt; query) {
    ///     foreach (var (color) in query) { color.Get.R = 255; }
    /// }
    /// </code>
    /// </summary>
    public unsafe struct Changed<T> : IFilter where T : unmanaged, IComponent
    {
        public void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T>.Index);
            // Pre-resolve ChangedQueryStorage — called from non-Burst Query.Init context.
            // Store raw pointers in QueryUnsafe so Burst-compiled OnUpdateBatched can
            // access them without any managed calls.
            var world = World.Get(query->world->Id);
            var storage = ChangedQueryStorageRegistry.GetOrCreate(world, ComponentType<T>.Index, sizeof(T));
            query->ChangedEntitiesPtr = UnsafeUtility.AddressOf(ref storage.ChangedList);
            query->ChangedOffsetsPtr = UnsafeUtility.AddressOf(ref storage.Offsets);
            query->ChangedValuesPtr = UnsafeUtility.AddressOf(ref storage.Values);
            query->ChangedComponentSize = sizeof(T);
        }
    }
}

