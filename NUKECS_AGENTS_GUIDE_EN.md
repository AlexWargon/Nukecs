# Nukecs: agent guide for gameplay code

This guide applies to new gameplay code built with Nukecs. Follow it when
creating the MonoBehaviour that owns the world, components, and systems.
The style follows `Assets/Game/Scripts/EcsTest.cs`, while excluding its legacy
`.Add<T>()` system registrations and application-specific dependencies.

[Russian version](NUKECS_AGENTS_GUIDE_RU.md).

Current API: [AGENTS.md](AGENTS.md). Storage and invariants:
[ARCHITECTURE.md](ARCHITECTURE.md). Explicit iterators:
[runtime iterator contract](src/Systems/FnSystems/RuntimeQuery/README.md).
When an older example differs from the current API, use the current API.

## 1. Lifecycle: Init, Update, OnDestroy

One MonoBehaviour owns the gameplay session. Create the world and systems
in `Init()`. Register systems, initialize resources, create initial entities,
and call `OnStart()` once, before the first `Update()`.

The following is a complete minimal example. `Awake()` calls `Init()`; if the
project initializes gameplay through bootstrap/DI, move that call there and
ensure it runs before the first `Update()`.

```csharp
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs;

namespace Game.Ecs
{
    public struct Position : IComponent { public float3 Value; }
    public struct Velocity : IComponent { public float3 Value; }

    public sealed class GameEcs : MonoBehaviour
    {
        private int worldId = -1;
        private ref World world => ref World.Get(worldId);
        private Systems updateSystems;

        private void Awake() => Init();

        public void Init()
        {
            if (updateSystems != null) return;

            World.DisposeStatic();
            var createdWorld = World.Create(WorldConfig.Default16384);
            worldId = createdWorld.Id;

            updateSystems = new Systems(ref world);
            updateSystems
                .AddDefaults()
                .Add(GameSystems.Initialize, Threads.Main, path: SystemPath.Start)
                .Add(GameSystems.Move, Threads.Parallel);

            // Add resources and application setup here, before OnStart().
            var entity = world.Entity();
            entity.Add(new Position { Value = float3.zero });
            entity.Add(new Velocity { Value = new float3(1f, 0f, 0f) });
            world.Update(); // Setup only: apply the initial ECB commands.

            updateSystems.OnStart();
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            updateSystems.OnUpdate(dt, Time.time);
        }

        private void OnDestroy()
        {
            if (worldId >= 0 && world.IsAlive)
                world.Dispose();
            World.DisposeStatic();
            worldId = -1;
            updateSystems = null;
        }
    }

    public static class GameSystems
    {
        [System, BurstCompile]
        public static void Initialize(ref State state)
        {
            // One-time Burst-compatible gameplay initialization.
        }

        [System, BurstCompile]
        public static void Move(ref Query<Position, Velocity> query, ref State state)
        {
            foreach (var (position, velocity) in query)
            {
                position.Get.Value += velocity.Read.Value * state.Time.DeltaTime;
            }
        }
    }
}
```

Rules for the agent:

- Create the world in a local variable, then assign `worldId`. Do not access
  `World.Get(worldId)` before obtaining a valid ID.
- Implement new systems as static methods marked with `[System]` and register
  them through `.Add(GameSystems.Method, Threads.Parallel)`. Select lifecycle
  with `path: SystemPath.Start / Update / FixedUpdate / Destroy`.
- Do not introduce new `ISystem`/`IEntityJobSystem` implementations registered
  through `.Add<MySystem>()`. This is the legacy style; its presence in
  `EcsTest.cs` is not a pattern to copy.
- `Update()` obtains `dt` and calls `updateSystems.OnUpdate(dt, Time.time)`.
  Put gameplay logic, event handling, input, and synchronization in systems.
  Do not add manual `world.Update()`, additional ECB playback, or entity
  traversal to that method.
- Dispose the world with `world.Dispose()`, then call `World.DisposeStatic()`.
  `world.Dispose()` already completes jobs and invokes the systems' destroy
  lifecycle; do not call `updateSystems.OnDestroy()` separately beforehand.

This template assumes a single session owner. `World.DisposeStatic()` resets
the domain-global registry, so each helper MonoBehaviour must not call it.
With multiple simultaneously live worlds, the shared reset belongs to the
bootstrap owner of the whole session.

## 1.1 Registering multiple systems: AddSystems and AddGroup

`AddSystems` registers several method systems in the specified lifecycle and
order. Specify a mode for each system with a `(method, Threads)` tuple.
This API is source-generated: pass known `[System]` methods directly to the call.

```csharp
// In Init(), instead of individual Add calls for the same systems:
updateSystems
    .AddDefaults()
    .AddSystems(SystemPath.Start,
        (GameSystems.Initialize, Threads.Main))
    .AddSystems(SystemPath.Update,
        (GameSystems.Move, Threads.Parallel),
        (DamageSystems.ApplyDamage, Threads.Parallel));
```

Without a tuple, the method's default registration mode is used. Specify the
mode explicitly when correct data access depends on it.

`AddGroup` adds a group through `ISystemsGroup.Build(Systems, ref World)`.
A group bundles system registrations for a subsystem; the world and
`OnStart()` remain the responsibility of the owning MonoBehaviour.

```csharp
public sealed class GameplayGroup : ISystemsGroup
{
    public void Build(Systems systems, ref World world)
    {
        systems.AddSystems(SystemPath.Update,
            (GameSystems.Move, Threads.Parallel),
            (DamageSystems.ApplyDamage, Threads.Parallel));
    }
}

// Alternative registration in Init():
updateSystems
    .AddDefaults()
    .Add(GameSystems.Initialize, Threads.Main, path: SystemPath.Start)
    .AddGroup(new GameplayGroup());
```

Use `ISystemsGroup`, rather than the legacy `SystemsGroup` with `.Add<T>()`.
`AddGroup` calls `Build`, which adds systems to the existing container.
Systems retain their order within the group, and the group is inserted at
its registration position. Do not also register the same methods with
`Add`/`AddSystems`, or they will execute twice. `GameSystems` and `DamageSystems`
are defined in this guide's examples; keep them and the group in an accessible
namespace.

## 2. Prefer Burst and parallel systems

First try expressing a system through unmanaged components, `Query`, `State`,
`Res<T>`, and `Events<T>`, marking it `[System, BurstCompile]`, and registering
it with `Threads.Parallel`. Parallel execution is appropriate when each worker
writes only its own entities/rows and reads safe shared data.

- Use `Unity.Mathematics`, value types, and system parameters.
- Keep `GameObject`, UnityEngine `Transform`, UI, managed collections, strings,
  LINQ, managed callbacks, and service access out of Burst systems. Synchronize
  UnityEngine objects in a separate `Threads.Main` system without
  `[BurstCompile]`; keep calculations in Burst systems.
- `Threads.MainRun` is analogous to Unity `job.Run()`: synchronous job execution
  on the calling thread (normally the main thread in this lifecycle), without
  enqueueing work for workers. Such a job can execute with Burst when its code
  is compatible and Burst is enabled. Use it for sequential Burst logic that
  must complete immediately; use `Threads.Main` without Burst for managed
  UnityEngine/UI code.
- Use `Threads.Single` for a sequential Burst-compatible pass when correct
  parallel access is not yet possible.
- Do not have multiple workers write the same `Res<T>`, shared counter, or
  arbitrary target entity through `Entity.Get<T>()`. Such ownership requires
  sequential processing, partitioning, reduction, or explicit synchronization.
- Do not mark an entire class `[BurstCompile]` if it contains managed systems.
  Mark individual methods and check that the generator produced runners.
- Do not enable the dependency graph automatically. If the project needs it,
  register it explicitly in `Init()` and account for hidden shared-data access.

### Fast traversal: foreach directly over query

You can traverse a `Query` without `.iter()` or `.par_iter()`:
`foreach (var (position, velocity) in query)`. For eligible systems, the generator
replaces this traversal with a direct pointer loop over component columns,
removing runtime iterator and tuple-access overhead. Prefer this form for simple
calculations across many entities, including systems using `Threads.Parallel`.

The generator rewrites one plain foreach over the first query parameter and
preserves surrounding locals, early returns before the loop, cleanup after it,
and enclosing unsafe blocks or if branches. Ordinary captured locals are
forwarded by ref, so writes remain visible after the loop. For one data
component, use `foreach (ref var value in query)`.

In Parallel, surrounding code executes per assigned work range, even for an
empty query. Shared writes must be thread-safe; actions exactly once per update
belong in a separate main-thread system. Managed code before the loop remains
managed and can prevent Burst compilation.

```csharp
[System, BurstCompile]
public static void Move(ref Query<Position, Velocity> query, ref State state)
{
    foreach (var (position, velocity) in query)
    {
        var dt = state.Time.DeltaTime;
        position.Get.Value += velocity.Read.Value * dt;
    }
}

// Registration:
// updateSystems.Add(GameSystems.Move, Threads.Parallel);
```

This is a fast expanded pointer traversal: dense inline components use direct
dereferences and sequential pointer increments. It does not imply mandatory
loop unrolling that duplicates the body for several entities. For sparse/tag
filters, the generator selects the appropriate archetype traversal; pool
components disable the ordinary batch rewrite.

One foreach is the required shape for this optimization, but not a guarantee:
the analyzer must support the body and types. Execute the method through its
generated runner registered in `Systems`; calling the original method directly
does not invoke its batch version. Structural Add/Remove operations remain
deferred and must not invalidate the active traversal's data.

Multiple loops over the primary query, loops nested inside the selected loop,
local functions, and return/break/goto/yield inside it cause fallback.
Captured ref locals, constants, anonymous types, and names starting with `_`
or named `state`/`range` are unsupported. Explicit `.iter()` and `.par_iter()`
retain runtime iteration and are not rewritten.

Use `[System, BurstCompile, RequireBatch]` when batching is required: fallback
then produces compiler error `NUKECS002` with a reason. Generated runners
implement `ISystemCompilationInfoProvider`; inspect `CompilationInfo.Kind`
(`PointerBatch`, `ChangedBatch`, `RuntimeIteration`, `NoQuery`), `FallbackReason`,
and `HasSurroundingCode`. This reports generation, not native Burst execution
or the actual dense/sparse branch. See README for an inspection example.

`FallbackDetail` explains the first blocker, naming the component/local or
unsupported syntax and suggesting a correction. `FallbackFile`, `FallbackLine`,
and `FallbackColumn` locate it (one-based coordinates). `[RequireBatch]` reports
that detail at the offending node; without it, inspect the runner metadata.
Fixing the first blocker can reveal another. Successful batching has empty
detail/path and zero coordinates.

When an explicit runtime iterator is needed in a Parallel/Single system with
an assigned range, use `query.par_iter()`. `query.iter()` traverses the entire
query, so each Parallel worker may process all entities again. `par_iter()`
does not schedule jobs itself; the registered runner does. Do not replace
plain foreach with it without a reason.

## 3. Events: tags, pool payloads, or Events<T>

Choose one of these three representations based on the event's meaning.

| Requirement | Representation |
|---|---|
| An entity is marked for processing; no payload is needed | Empty `struct : IComponent` tag |
| A rare temporary event belongs to an entity and needs a payload | `struct : IPoolComponent` |
| A stream of distinct events is needed, including several per entity | `ref Events<T> events` with an unmanaged payload |

Do not introduce a managed event bus or a collection on each entity for events.

### Tags and IPoolComponent

```csharp
public struct Recalculate : IComponent { } // Tag without data.
public struct Health : IComponent { public float Value; }
public struct DamageRequest : IPoolComponent
{
    public Entity Source;
    public float Amount;
}

public static class DamageSystems
{
    [System, BurstCompile]
    public static void ApplyDamage(ref Query<Entity, Health, DamageRequest> query)
    {
        foreach (var (entity, health, request) in query)
        {
            health.Get.Value -= request.Read.Amount;
            entity.Remove<DamageRequest>();
        }
    }
}

// In Init():
// updateSystems.Add(DamageSystems.ApplyDamage, Threads.Parallel);
```

The producer calls `entity.Add<Recalculate>()` or
`entity.Add(new DamageRequest { Source = source, Amount = amount })`.
After processing, the consumer removes the tag/component through `Remove<T>()`.
If several systems read the event, register cleanup after the last reader
instead of inside the first consumer.

Add/Remove become visible after ECB playback. Account for the boundary between
producer, consumer, and cleanup; do not flush inline during traversal. One
component per entity holds one current payload, rather than a queue. For
multiple hits per frame, use `Events<T>` or explicitly designed accumulation;
do not assume repeated `Add` automatically sums damage.

### Events<T> buffers

```csharp
public struct MovedEvent
{
    public Entity Entity;
    public float3 Position;
}

public static class MovementEvents
{
    [System, BurstCompile]
    public static void Produce(
        ref Query<Entity, Position, Velocity> query,
        ref Events<MovedEvent> events,
        ref State state)
    {
        foreach (var (entity, position, velocity) in query.par_iter())
        {
            position.Get.Value += velocity.Read.Value * state.Time.DeltaTime;
            events.AddPar(new MovedEvent
            {
                Entity = entity,
                Position = position.Read.Value
            });
        }
    }

    [System, BurstCompile]
    public static void Consume(ref Events<MovedEvent> events)
    {
        foreach (ref var ev in events)
        {
            // Unmanaged processing of one event.
            // For UnityEngine/UI, use a separate Main consumer without Burst.
        }
        events.Clear(); // This is the last reader.
    }
}

// In Init(), instead of GameSystems.Move (otherwise movement runs twice):
// updateSystems
//     .Add(MovementEvents.Produce, Threads.Parallel)
//     .Add(MovementEvents.Consume, Threads.Main);
```

Use `AddPar`, rather than `Add`, in a Parallel producer. Worker event order is
not guaranteed. Complete producers before reading or clearing. A parallel
consumer is valid when events are processed independently; move the shared
`Clear()` into a separate sequential step after all readers.

`Events<T>` retains events until cleared. The last consumer calls `Clear()`,
or a separate cleanup system is registered after all readers. `AddDefaults()`
also registers `DefaultSystems.ClearEvents` at the position where
`AddDefaults()` is called. In the template, this is the start of the chain:
the previous buffer is cleared before new producers. For current-frame events
and multiple readers, explicitly order producer → consumers → cleanup;
do not insert clearing between readers.

## 4. Components used by many entities

Components used by many entities should be small unmanaged structs: numbers,
`float2/3`, enums, flags, `Entity`, or IDs/indices into shared data. Do not give
each entity its own collection: `List`, arrays, `Dictionary`, `NativeList`,
`MemoryList`, `DynamicBuffer`, `ComponentArray`, or other containers backed by
individual allocations. Even an unmanaged container has capacity, allocation,
and disposal costs.

The project owner's practical rule: if a component is expected on more than
100–200 entities, prefer a separate shared collection/storage and keep a key
or record ID in the component. This is a design guideline, not a framework
limit. Consider payload size and capacity even with fewer owners.

```csharp
public struct InventoryRef : IComponent
{
    public int InventoryId; // Key in a separate InventoryStorage.
}
```

The shared storage owns and disposes collections. Removing `InventoryRef`
does not automatically remove its storage record: the record's owner defines
its lifetime.

- Keep target/neighbour lists, settings, animation tables, and other shared
  reference data centralized. Store IDs, indices, or data ranges in components.
- When needed, model variable child data as separate entities with `Owner`/`Parent`
  and small components. Estimate entity counts and query costs before choosing
  this representation.
- Separate rare heavy payloads. `IPoolComponent` keeps them out of inline columns
  and avoids migrating those columns when added. Pool storage does not eliminate
  a payload collection's cost or make it inexpensive by itself.
- Allow a collection only when needed and with a limited number of owners,
  explicit capacity, lifetime, and disposal.
- Access shared managed data through `ResManaged<T>`/Main systems. Use unmanaged
  resources/shared buffers with explicit ownership for Burst data. `Res<T>`
  values in 1.0 are domain-global, rather than isolated per world.

### Collections inside rare components: IDisposable

The framework supports collection cleanup when removing components. The exact
interface in the current API is `System.IDisposable`, not `IDispose`. If a
component owns a container, implement `Dispose()` and release the container
there. For example, for a rare owner:

```csharp
public struct RarePathBuffer : IComponent, System.IDisposable
{
    public Unity.Collections.NativeList<float3> Points;

    public void Dispose()
    {
        if (Points.IsCreated) Points.Dispose();
    }
}
```

Initialize the container before use. `entity.Remove<RarePathBuffer>()` is
deferred: after ECB playback, the dropped disposable component invokes
`Dispose()`. Do not manually release the same container before Remove, or it
will be disposed twice. A component that only references a shared collection
must not release it; disposal belongs to the storage. Copying a struct with a
container does not create an independent buffer; do not treat copies as
separate owners.

`IDisposable` provides cleanup, but does not reduce the cost of a separate
collection per entity. Continue to prefer keys/IDs for components used by
many entities.

### Small arrays: ComponentArray<T>

For a small set of elements on an entity, use `ComponentArray<T>`, where
`T : unmanaged, IArrayComponent`. Use the entity API `AddArray<T>()`,
`GetArray<T>()`, and `RemoveArray<T>()`; a default array is not a ready-to-use
buffer.

```csharp
public struct InventorySlot : IArrayComponent
{
    public int ItemId;
}

// During setup, when this entity needs a small array:
ref var slots = ref entity.AddArray<InventorySlot>();
slots.Add(new InventorySlot { ItemId = 10 });
```

The current source sets `DEFAULT_MAX_CAPACITY = 16`: the slot has a fixed
capacity and does not grow automatically. Note the API detail: ordinary `Add()`
currently stops at `length >= capacity - 1`, allowing 15 elements;
`AddNoResize()` checks `length < capacity`, allowing all 16. These methods
silently decline to add an element at the limit. Plan the size in advance
and check `Length` when dropping an element is unacceptable. Do not use
`ComponentArray` for unbounded lists or to bypass the 100–200-owner guideline.

Create and cache fluent queries once during setup. Do not build `world.Query()`
every frame: each query is registered and lives until world disposal.

## 5. Checks before delivering gameplay code

- `Init()` creates the world, registers method systems, initializes resources/
  entities, and calls `OnStart()`; `Update()` only passes time to `OnUpdate()`.
- `OnDestroy()` calls `world.Dispose()` and performs the session owner's shared
  reset.
- No new `.Add<MySystem>()` registrations or managed code inside Burst systems.
- Parallel systems respect data ownership; explicit traversal uses `par_iter()`.
- Events have consumers and cleanup; repeated events are not lost by choosing
  a single payload component instead of an event stream.
- With more than 100–200 owners, the component stores a key/ID into a shared
  collection.
- A rare component owning a container implements `IDisposable`;
  `ComponentArray<T>` respects its fixed capacity.
- Async load keeps the returned world:
  `world = await World.LoadAsync(path, world)`; do not use the old struct copy.

Run relevant Unity EditMode regression tests for new behavior. `[BurstCompile]`
alone does not prove native Burst execution: verify important hot paths with
a Burst probe/generated job. An Editor pass does not prove IL2CPP support.
