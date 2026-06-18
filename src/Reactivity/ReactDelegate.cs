namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Managed callback invoked when a reactive component of type <typeparamref name="T"/> changes.
    /// Receives the new value and the owning entity.
    /// </summary>
    public delegate void ReactDelegate<T>(in T value, in Entity entity) where T : unmanaged, IComponent;

    /// <summary>
    /// Managed predicate filter for reactive subscriptions. Return <c>true</c> to fire
    /// the callback for the given (changed) value, <c>false</c> to skip.
    /// </summary>
    public delegate bool ReactFilter<T>(in T value) where T : unmanaged, IComponent;

    /// <summary>
    /// Non-generic Burst callback signature. The callback receives only the entity
    /// and is responsible for reading the component via <c>entity.Get&lt;T&gt;()</c>.
    ///
    /// Why non-generic: <c>FunctionPointer&lt;TDelegate&gt;.Invoke</c> (and the underlying
    /// <c>Marshal.GetDelegateForFunctionPointer</c>) fail on generic delegate types in Mono.
    /// A non-generic delegate works in BOTH managed execution and Burst-compiled code.
    /// </summary>
    public delegate void ReactDelegateBurst(in Entity entity);

    /// <summary>
    /// Non-generic Burst filter signature. The callback receives only the entity
    /// and reads the component itself.
    /// </summary>
    public delegate bool ReactFilterBurst(in Entity entity);
}

