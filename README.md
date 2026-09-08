
![logo-no-background](https://github.com/AlexWargon/Nukecs/assets/37613162/827d5e54-82ff-45d5-af2f-bac06fabc2ec)

### <img src="https://github.com/AlexWargon/Nukecs/assets/37613162/553b8223-c304-4429-8def-96e2830d5ca7" width=2% height=2%> NUKECS — Fast C# Entity Component System for Unity

Burst-compiled ECS framework with source-generated systems, custom allocator, and hot reload support.

- **Burst-compiled** systems by default
- **Source-generated** system runners from `[System]` static methods
- **Shared SoA storage** — tag/pool changes preserve inline component data
- **Runtime query iterators** — 1–8 components, inline/pool combinations, Entity access
- **Custom arena allocator** with optional Arena Guard diagnostics
- **Reactive subscriptions** and optional system dependency graph
- **World serialization** — save/load entire world state
- **Hot reload** — edit systems during Play Mode

---

## Minimal Quick Start

Save this as `QuickStart.cs`, attach `QuickStart` to an empty GameObject, and
enter Play Mode. One entity accelerates, moves, and bounces between the bounds
stored in a resource. All three data components implement `IComponent`.

```csharp
using Unity.Burst;
using UnityEngine;
using Wargon.Nukecs;

namespace NukecsQuickStart
{
    public struct Position : IComponent { public float X; }
    public struct Velocity : IComponent { public float X; }
    public struct Acceleration : IComponent { public float X; }

    public struct SimulationConfig : IRes
    {
        public float MinX;
        public float MaxX;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }

    public class QuickStart : WorldInstaller
    {
        protected override void OnWorldCreated(ref World world)
        {
            world.AddRes(new SimulationConfig { MinX = -5f, MaxX = 5f });
            Systems.AddSystems(SystemPath.Update,
                SimulationSystems.Accelerate,
                SimulationSystems.Move,
                SimulationSystems.Bounce);
        }

        protected override void CreateEntities(ref World world)
        {
            var entity = world.Entity();
            entity.Add(new Position { X = 0f });
            entity.Add(new Velocity { X = 1f });
            entity.Add(new Acceleration { X = 0.1f });
        }

        private void Update()
        {
            Systems.OnUpdate(Time.deltaTime, Time.time);
        }
    }

    [BurstCompile]
    public static class SimulationSystems
    {
        [System, BurstCompile]
        public static void Accelerate(ref Query<Velocity, Acceleration> query,
            ref State state)
        {
            foreach (var (velocity, acceleration) in query)
            {
                velocity.Get.X += acceleration.Read.X * state.Time.DeltaTime;
            }
        }

        [System, BurstCompile]
        public static void Move(ref Query<Position, Velocity> query, ref State state)
        {
            foreach (var (position, velocity) in query)
            {
                position.Get.X += velocity.Read.X * state.Time.DeltaTime;
            }
        }

        [System, BurstCompile]
        public static void Bounce(ref Query<Position, Velocity> query,
            ref Res<SimulationConfig> config)
        {
            foreach (var (position, velocity) in query)
            {
                if (position.Read.X > config.Ref.MaxX)
                {
                    position.Get.X = config.Ref.MaxX;
                    velocity.Get.X = -velocity.Read.X;
                }
                else if (position.Read.X < config.Ref.MinX)
                {
                    position.Get.X = config.Ref.MinX;
                    velocity.Get.X = -velocity.Read.X;
                }
            }
        }
    }
}
```

`WorldInstaller` creates and disposes the world and applies the queued component
adds after `CreateEntities`. `AddSystems` registers the three systems in order,
using `Threads.Parallel` by default; the default scheduler chains their jobs.
`Res<SimulationConfig>.Ref` provides access to the registered resource.

Each system contains only one plain `foreach (... in query)` and uses inline
components, making it eligible for generated batch pointer loops. The example
updates ECS data; it does not create a visible GameObject for the entity.

## Documentation

This guide describes the checked-in API as of 2026-09-08.

- [Runtime query contract](src/Systems/FnSystems/RuntimeQuery/README.md): ranges, tuples, Entity deconstruction, migration notes.
- [Architecture](ARCHITECTURE.md): logical archetypes, shared storage, iteration paths and invariants.
- [Agent reference](AGENTS.md): code map and implementation conventions.
- [Archetype history](HANDOFF_ArchetypeMasks.md) and [runtime iterator history](RUNTIME_QUERY_ITER4_HANDOFF.md): historical experiments and test runs; older sections may describe superseded APIs.

## Quick Start

### 1. Create a WorldInstaller

Inherit from `WorldInstaller`, add systems in `OnWorldCreated`, and drive the update loop:

```csharp
using Wargon.Nukecs;
using Wargon.Nukecs.Transforms;
using Unity.Mathematics;
using UnityEngine;

public class GameBootstrap : WorldInstaller
{
    protected override WorldConfig GetConfig() => WorldConfig.Default256;

    protected override void OnWorldCreated(ref World world)
    {
        Systems.Add(MovementSystems.MoveBatched, Threads.Parallel);
    }

    private void Update()
    {
        Systems.OnUpdate(Time.deltaTime, Time.time);
    }
}
```

`WorldInstaller` handles world creation, default systems, and disposal automatically. Override `CreateEntities(ref World)` to spawn initial entities.

### 2. Define Components

```csharp
using Wargon.Nukecs;

public struct Speed : IComponent { public float Value; }
public struct Health : IComponent { public int Value; }
public struct PlayerTag : IComponent { }
public struct InventoryStats : IPoolComponent { public int ItemCount; }
```

Components are unmanaged structs. Empty structs become **tag components** with zero memory cost.
Data components implementing `IComponent` use inline SoA storage by default.
`IPoolComponent` inherits `IComponent` and stores data in a separate pool; it can
be queried alongside inline components, but iterating pool components disables
the generated batch optimization described below.

### 3. Define Systems

```csharp
using Unity.Burst;
using Unity.Mathematics;
using Wargon.Nukecs;
using Wargon.Nukecs.Transforms;

[BurstCompile]
public static class MovementSystems
{
    // Plain foreach: eligible for generated batch pointer loops.
    [System, BurstCompile]
    public static void MoveBatched(
        ref Query<LocalTransform, Speed> query,
        ref State state)
    {
        foreach (var (t, s) in query)
        {
            t.Get.Position += new float3(1, 0, 0)
                * s.Read.Value * state.Time.DeltaTime;
        }
    }

    // Explicit runtime iterator: respects the current job's range.
    [System, BurstCompile]
    public static void Move(
        ref Query<LocalTransform, Speed> query,
        ref State state)
    {
        var dt = state.Time.DeltaTime;
        foreach (var (t, s) in query.par_iter())
        {
            ref var transform = ref t.Get;
            ref readonly var speed = ref s.Read;
            transform.Position += new float3(1, 0, 0) * speed.Value * dt;
        }
    }
}
```

`MoveBatched` and `Move` implement the same movement using different iteration
paths. Register one of them. The current parallel runtime API is `.par_iter()`;
the temporary `.iter_par()` name has been removed.

#### When the Generator Emits Batched Code

For an ordinary component query, the current generator requires:

- A `[System]` method with a block body whose **only top-level statement** is
  `foreach (var (...) in query)`, naming the first typed `Query<...>` parameter
  directly. Execute it through its generated `Systems.Add` runner.
- Exactly one `foreach` in the method, with no nested/additional foreach loops
  or local functions. Statements before or after the loop disable batching —
  even `var dt = state.Time.DeltaTime;`. Local variables inside the loop are allowed.
- Recognizable iteration variables and component types, with **no iterated
  `IPoolComponent` types**. Explicit `.iter()` / `.par_iter()` calls always use
  runtime iterators, including inside generated systems.

For the highest performance on dense inline workloads, use `MoveBatched` with
`[BurstCompile]` and Burst-compatible code. The generated dense storage loop
walks component pointers directly, removing per-entity enumerator and tuple
overhead. Batch generation itself does not require Burst; Burst compiles the
generated job code when enabled and supported by the selected thread mode.

The fastest storage path also requires inline data, no tag tuple slots, and no
runtime storage degradation. Tag slots or filters can select the generated
logical-archetype loop instead, with gather traversal for sparse rows. Even the
default `IsPrefab` / `DestroyEntity` exclusions can cause this fallback when
excluded entities share physical storage with matching entities. `Changed<T>`
uses a separate generated change-detection path. Actual speed and the best
thread mode depend on the workload; see [Architecture](ARCHITECTURE.md) for the
storage model and historical measurements.

### 4. Add Systems to the World

```csharp
Systems
    .Add(MovementSystems.MoveBatched, Threads.Parallel)
    ;
```

The source generator creates the `Systems.Add(delegate, Threads)` extension for each `[System]` method automatically.

### 5. Create Entities

```csharp
protected override void CreateEntities(ref World world)
{
    var e = world.Entity();
    e.Add(new LocalTransform { Position = float3.zero, Scale = new float3(1,1,1) });
    e.Add(new Speed { Value = 5f });
}
```

---

## Components

### IComponent — Inline Archetype Storage (Default)

```csharp
public struct Velocity : IComponent { public float3 Value; }
```

Stored in SoA columns owned by `StorageArchetype` (`StorageType.Archetype`).
Logical archetypes with the same inline components share this storage, even when
their tags or pool components differ. Adding/removing a tag or pool component
changes logical membership without copying the inline columns. Adding/removing
an inline component moves the entity to another storage.

### IPoolComponent — Separate Pool Storage

```csharp
public struct MyPoolData : IPoolComponent { public float3 Value; }
```

Stored in a separate SparseSet pool. Use for components that are sparse (few entities have them) or large.

### IArrayComponent — Dynamic Array Components

```csharp
public struct Child : IArrayComponent { public Entity Value; }
```

Dynamic arrays attached to entities. Accessed via `entity.GetArray<T>()` and `entity.AddArray<T>()`.

### IDisposable Components

```csharp
public struct MyComponent : IComponent, System.IDisposable
{
    public NativeArray<int> Data;
    public void Dispose() { Data.Dispose(); }
}
```

`Dispose()` is called automatically when the component is removed or the entity is destroyed.

### ICopyable\<T\> Components

```csharp
public struct MyCopyable : IComponent, ICopyable<MyCopyable>
{
    public NativeList<int> List;
    public MyCopyable Copy(int to)
    {
        var copy = new NativeList<int>(List.Length, Allocator.Persistent);
        copy.CopyFrom(in List);
        return new MyCopyable { List = copy };
    }
}
```

Called when `entity.Copy()` is used to duplicate an entity.

### Tag Components

```csharp
public struct EnemyTag : IComponent { }
```

Empty structs consume no memory in archetype storage — used only for query filtering.

### Built-in Components

| Component | Description |
|-----------|-------------|
| `DestroyEntity` | Marks entity for deferred destruction |
| `EntityCreated` | Added to newly created entities (cleared each frame) |
| `ChildOf` | Parent reference — `ChildOf { Value = parentEntity }` |
| `Child` | Array component for child references |
| `IsPrefab` | Marks prefab entities |

---

## Entities

### Creation

```csharp
var e = world.Entity();
var e2 = world.Entity<Speed>();                       // with default component
var e3 = world.Entity(new Speed { Value = 5f });     // with initial value
var e4 = world.Entity<Speed, Health>();               // multiple components
```

### Batch Creation

```csharp
var entities = world.BatchCreateEntity(500);
for (int i = 0; i < entities.Length; i++)
{
    ref var e = ref entities[i];
    e.Add(new LocalTransform { Position = new float3(i, 0, 0) });
}
```

### Operations

```csharp
ref var speed = ref entity.Get<Speed>();            // read/write ref
ref readonly var speed = ref entity.Read<Speed>();  // readonly ref
entity.Set(new Speed { Value = 10f });              // overwrite existing
entity.Add(new Speed { Value = 5f });               // add (deferred via ECB)
entity.Remove<Speed>();                             // remove (deferred via ECB)
bool has = entity.Has<Speed>();                     // check existence
ref var speed = ref entity.TryGet<Speed>(out bool exists); // safe access
```

### Destruction

```csharp
entity.Destroy();      // deferred — processed on next world.Update()
entity.DestroyNow();   // currently also queues ECB destruction
```

`entity.Destroy()` queues an ECB destroy command directly. `DestroyNow()`
currently uses the same deferred path despite its name. The `DestroyEntity` tag
is a separate route handled by the built-in destruction system.

### Copying

```csharp
var copy = entity.Copy();         // immediate deep copy
var copy = entity.CopyVieECB();   // deferred copy via ECB
```

### Prefabs

```csharp
var prefab = world.Entity();
prefab.Add(new Speed { Value = 5f });
prefab.Add(new IsPrefab());

var instance = world.SpawnPrefab(prefab);
var instances = world.SpawnPrefabs(prefab, 100);
```

### Hierarchy

```csharp
parent.AddChild(child);
parent.SetParent(childParent);
parent.RemoveChild(child);
ref var child = ref parent.GetChild(0);
ref var root = ref entity.GetRootParent();
```

---

## Systems (FnSystems)

Nukecs uses a **source-generated** approach. Mark static methods with `[System]` — the source generator creates job structs, runner classes, and `Systems.Add()` overloads automatically.

### System Attribute

```csharp
[System]                                    // default: Threads.Parallel
[System(Threads.Main)]                      // explicit thread mode
[System(Threads.MainRun)]
```

### Auto-Injected Parameters

The source generator detects parameter types and injects them automatically:

| Parameter | Description |
|-----------|-------------|
| `ref Query<T1, T2, ...>` | Query iteration over matching entities |
| `ref State` | World, Time, Dependencies |
| `ref Res<T>` | Singleton resource (read/write) |
| `ref ResManaged<T>` | Managed singleton resource |
| `ref Events<TEvent>` | Event stream (send/receive) |
| `ref Local<TData>` | Per-system local state |
| `ref Single<T>` | Singleton entity accessor |

### Thread Modes

```csharp
public enum Threads
{
    Main,       // Main thread
    MainRun,    // Main thread via Job System Run
    Single,     // Single worker thread
    Parallel    // All parallel threads (default)
}
```

### Adding Systems

Register `[System]` methods by method group. The generated `Add` extension
accepts the system, an optional `Threads` mode (default `Threads.Parallel`), and
an optional `path` (default `SystemPath.Update`). Registration returns the same
`Systems` instance, so calls can be chained:

```csharp
Systems
    .Add(MySystems.Spawn, Threads.MainRun)
    .Add(MySystems.Update, Threads.MainRun)
    .Add(MySystems.Render, Threads.Main)
    .Add(MySystems.Physics)              // default: Threads.Parallel
    ;
```

#### Registering a Lifecycle Phase

Use `path:` to select the lifecycle phase while keeping the default thread mode,
or supply both the mode and the phase:

```csharp
// MySystems.Initialize is a static method marked with [System].
systems.Add(MySystems.Initialize, path: SystemPath.Start);

// Alternative for a startup method that uses managed Unity APIs:
systems.Add(MySystems.Initialize, Threads.Main, SystemPath.Start);

// Other phases, with explicit thread modes:
systems
    .Add(MySystems.Physics, Threads.Parallel, SystemPath.FixedUpdate)
    .Add(MySystems.Cleanup, Threads.Main, SystemPath.Destroy);
```

Choose one of the two `Initialize` registrations above. `SystemPath` contains
integer constants; it is separate from the `Threads` enum. The shorthand
`.Add(MySystems.Initialize, SystemPath.Start)` does **not** select the Start phase with the
current generated signature: `Start` is the constant zero, which C# accepts as
`Threads.Main` in the second argument, leaving `path` at Update. Use the named
`path:` argument when omitting the thread mode.

| Path | Execution |
|------|-----------|
| `SystemPath.Start` | Runs when `systems.OnStart()` is called. Call it once after registration and initial world setup. |
| `SystemPath.Update` | Runs through `systems.OnUpdate(deltaTime, time)`; the default registration phase. |
| `SystemPath.FixedUpdate` | Runs through the fixed-step branch inside `systems.OnUpdate(...)`. |
| `SystemPath.Destroy` | Runs through `systems.OnDestroy()`, also invoked during world disposal. |

Adding a Start system only registers it; it does not execute immediately.
`OnUpdate` does not automatically call `OnStart`, and `OnStart` has no once-only
guard. Call it after building the systems and creating initial entities:

```csharp
systems.OnStart(); // after all registrations and initial entity setup
// In the game loop:
systems.OnUpdate(deltaTime, time);
// On shutdown; world disposal also invokes the registered destroy systems:
world.Dispose();
```

#### Registering Multiple Systems with AddSystems

`AddSystems` combines several registrations for the same phase. Each argument
can be a method group (default `Threads.Parallel`) or a `(method, Threads)` tuple
that selects its execution mode. Use `using static Wargon.Nukecs.SystemPath;`
to write `Update`, `Start`, `FixedUpdate` or `Destroy` without the prefix.

For example, group application systems with different thread modes:

```csharp
using Wargon.Nukecs;
using static Wargon.Nukecs.SystemPath;

public class GameplayGroup : ISystemsGroup
{
    public void Build(Systems systems, ref World world)
    {
        systems.AddSystems(Update,
            (MySystems.Spawn, Threads.MainRun),
            MySystems.Move,
            MySystems.UpdateLifetime,
            (MySystems.Render, Threads.Main));
    }
}
```

Here `MySystems` is an application-defined class of static `[System]` methods.
The first argument sets the phase for every system in the call. `Move` and
`UpdateLifetime` use `Threads.Parallel`. Registrations are added in argument order,
just like a chain of `Add` calls; execution still follows the selected scheduler
and dependency rules. Install this group with
`systems.AddGroup(new GameplayGroup())`.

Omit the phase to register all methods for Update, or select another phase:

```csharp
systems.AddSystems(
    (MySystems.Spawn, Threads.MainRun),
    MySystems.Physics,
    (MySystems.Render, Threads.Main));

systems.AddSystems(SystemPath.Start, (MySystems.Initialize, Threads.Main));
```

These overloads are generated from the call sites. Pass recognizable `[System]`
method groups, tuple literals for mode overrides, and a compile-time
`SystemPath` constant when specifying the phase.

### Query Iteration

#### `par_iter()` — The current job's range

Use this inside a parallel system. It visits only the range assigned by the
runner through `Query.Update`. It does not schedule work or synchronize access;
an uninitialized range is empty.

```csharp
foreach (var (t, v) in query.par_iter())
{
    ref var transform = ref t.Get;       // read/write
    ref readonly var vel = ref v.Read;   // readonly
    transform.Position += vel.Value * dt;
}
```

#### `iter_unsafe()` — Raw pointer iteration

```csharp
foreach (var (t, v) in query.iter_unsafe())
{
    t->Position += v->Value * dt;
}
```

#### `par_iter_unsafe()` — Parallel raw pointer iteration

```csharp
foreach (var (t, v) in query.par_iter_unsafe())
{
    t->Position += v->Value * dt;
}
```

#### `iter()` — Sequential ref iteration

```csharp
foreach (var (t, v) in query.iter())
{
    ref var transform = ref t.Get;
    transform.Position += v.Read.Value * dt;
}
```

`iter()` visits the **complete query**, regardless of the current job range.
Do not use it for ordinary per-entity work inside `Threads.Parallel`: each job
would repeat the full traversal. Both explicit methods use the runtime iterator,
including inside generated runners. Plain `foreach (... in query)` remains
eligible for the generator's batch pointer-loop optimization.

#### `iter_chunk()` — Chunk-based iteration

```csharp
foreach (var chunk in query.iter_chunk())
{
    // Process entities in chunks
}
```

Chunks are a separate API from the runtime ref iterators. With shared storage,
logical rows may be sparse. `Chunk<T1,T2,T3>.CopyTo` handles gather rows; do not
assume the other arities' `CopyTo` methods support sparse rows (see
[architecture limitations](ARCHITECTURE.md)).

### Entity — Access Entity in Iteration

Put `Entity` first in the query signature. Explicit `.iter()` / `.par_iter()`
deconstruction returns an `Entity` **value**, followed by `Ref<T>` components:

```csharp
[System, BurstCompile]
public static void Process(
    ref Query<Entity, LocalTransform, Speed> query,
    ref State state)
{
    foreach (var (e, t, s) in query.par_iter())
    {
        ref var transform = ref t.Get;
        transform.Position += s.Get.Value * state.Time.DeltaTime;
        if (transform.Position.y < 0)
            e.Destroy();
    }
}
```

Use `e.id`, `e.Get<T>()`, `e.Add<T>()` or `e.Destroy()` directly. Do not unwrap
the deconstructed entity through `.Get` / `.Read`. A tuple's `C0` property still
exposes `Ref<Entity>`; the value conversion applies to deconstruction.

### Query Filter Modifiers

Use `None<T>` and `With<T>` as the last type parameter to filter without reading:

```csharp
// None<T> — exclude entities that have component T
ref Query<LocalTransform, Velocity, None<StaticTag>> query

// With<T> — require T without returning its component data
ref Query<LocalTransform, With<CubeStateTag>> query
```

`None<T1, T2>` and `With<T1, T2>` support multiple components. Filter/tag tuple slots contain no
entity payload; omit the trailing filter when deconstructing:

```csharp
// query: Query<Entity, LocalTransform, Speed, None<StaticTag>>
foreach (var (entity, transform, speed) in query.par_iter())
    transform.Get.Position.x += speed.Read.Value * dt;
```

### ISystemsGroup — Organize Systems

```csharp
[BurstCompile]
public class GameSystems : ISystemsGroup
{
    public void Build(Systems systems, ref World world)
    {
        systems
            .Add(Spawn, Threads.MainRun)
            .Add(Move)
            .Add(Render, Threads.Main)
            ;
    }

    [System, BurstCompile]
    public static void Spawn(ref State state, ref Res<Config> config) { }

    [System, BurstCompile]
    public static void Move(ref Query<LocalTransform, Velocity> query, ref State state) { }

    [System]
    public static void Render(ref Query<LocalTransform> query, ref State state) { }
}

// Registration:
Systems.AddGroup(new GameSystems());
```

### BurstCompile

Mark Burst-compatible system methods with `[BurstCompile]`. Systems using
managed Unity APIs should run with `Threads.Main` and stay outside Burst code:

```csharp
[System, BurstCompile]
public static void MySystem(ref Query<Transform> query) { }
```

### Optional Dependency Graph

```csharp
Systems.UseDependencyGraph(); // default: GroupScheduleMode.LegacyGroupComplete
// Return to the sequential scheduling path:
Systems.UseDependencyGraph(false);
```

The graph is opt-in and covers update systems. It groups systems using reported
component, resource, event and ECB access metadata; independent jobs can overlap.
Custom runners can supply `ISystemDependencyInfoProvider` and `IThreadModeProvider`.
Dependencies depend on that metadata, so accesses hidden behind helper methods
or external state need review when enabling graph scheduling.

`GroupScheduleMode` also exposes `ChainedGroupComplete`, `FlattenedSchedule` and
`FlattenedSchedule2`. Inspect the graph in **Nuke.cs → Dependency Graph**.

---

## Queries

### Fluent API (manual queries)

```csharp
var query = world.Query()
    .With<LocalTransform>()
    .With<Speed>()
    .None<StaticTag>();
```

Create and retain manual queries during setup, before spawning entities.
Each `world.Query()` registers a new query; identical fluent chains are not
deduplicated. The manual fluent path does not attach itself to existing logical
archetypes automatically. Typed `Query<T...>.Init` does check existing archetypes.

Queries exclude `IsPrefab` and `DestroyEntity` by default. For a manual query
that includes them, begin with `world.Query(withDefaultNoneTypes: false)`.

### Generic Typed Queries (in systems)

Queries in `[System]` methods are auto-created by the source generator:

```csharp
Query<T1>
Query<T1, TOption>
Query<T1, T2, TOption>
Query<T1, T2, T3, TOption>
Query<T1, T2, T3, T4, TOption>
Query<T1, T2, T3, T4, T5, TOption>
Query<T1, T2, T3, T4, T5, T6, TOption>
Query<T1, T2, T3, T4, T5, T6, T7, TOption>
Query<T1, T2, T3, T4, T5, T6, T7, T8, TOption>
```

The trailing slot can be a regular component or a filter such as `None<T>`,
`With<T>`. Runtime iteration supports up to eight data components
within nine total generic slots: eight components plus a filter, or Entity plus
eight components. Entity plus eight components plus a filter exceeds that limit.

Use `var` for iterators and tuples: explicit methods now return
`QueryRuntimeIterN<QueryRuntimeRefs<...>>`, not the old mutable `RefTuple` layout.
Generic helpers that deconstruct a data-first tuple need
`where T : unmanaged, IComponent` on the first type parameter and
`using Wargon.Nukecs` for the deconstruction extensions.

Component references and pointers are valid only while their buffers remain
valid. Do not play back structural changes or resize storage during traversal.
Each block captures its row count on entry; appending rows does not extend that
active block. Full and ranged traversal can visit shared storage in different
orders. See the [runtime contract](src/Systems/FnSystems/RuntimeQuery/README.md).

### Access Patterns

```csharp
ref T val = ref componentRef.Get;       // read/write access
ref readonly T val = ref componentRef.Read;  // readonly access
```

### Query Properties

```csharp
int count = query.Count;
bool empty = query.Count == 0; // works across typed query arities
```

---

## Entity Command Buffer (ECB)

All `Add`, `Remove`, and `Destroy` operations are **deferred** through the Entity Command Buffer:

```csharp
entity.Add(new Speed { Value = 5f });   // Queued in ECB
entity.Remove<Speed>();                  // Queued in ECB
entity.Destroy();                        // Queued in ECB
```

ECB playback happens on `world.Update()`:

```csharp
world.Update();   // Plays back all queued ECB commands
```

Changes become visible after ECB playback, which may happen between systems in
the same frame. Use `entity.Set<T>()` or `Get<T>()` to modify existing component
values immediately. Queue structural changes during iteration and let the
scheduler play them back after the jobs that use the current storage finish.

The ECB is **thread-safe** — it uses per-thread command buffers internally.

---

## State

`State` is auto-injected into systems and provides:

```csharp
public struct State
{
    public JobHandle Dependencies;
    public World World;
    public TimeData Time;
}

public struct TimeData
{
    public float DeltaTime;
    public float DeltaTimeFixed;
    public float Time;
    public double ElapsedTime;
    public uint TickCount;
}
```

Usage in systems:

```csharp
[System]
public static void MySystem(ref Query<Speed> query, ref State state)
{
    var dt = state.Time.DeltaTime;
    var world = state.World;
}
```

---

## Resources

### IRes — Unmanaged Resources

```csharp
public struct GameConfig : IRes
{
    public float MoveSpeed;
    public int MaxEntities;

    public void OnCreate(ref World world)
    {
        // Called once on creation. Can use managed types.
    }

    public void OnUpdate(ref World world)
    {
        // Called before each system update. Unmanaged only.
    }
}
```

### Registering Resources

```csharp
world.AddRes(new GameConfig { MoveSpeed = 5f, MaxEntities = 1000 });
```

### Accessing in Systems

```csharp
[System]
public static void Move(
    ref Query<LocalTransform, Speed> query,
    ref State state,
    ref Res<GameConfig> config)
{
    float speed = config.Ref.MoveSpeed;
}
```

### ResManaged — Managed Resources

For resources that reference managed objects (e.g., `Mesh`, `Material`):

```csharp
world.AddResManaged(new MeshData { Mesh = mesh, Material = material });

[System]
public static void Render(ref ResManaged<MeshData> meshData)
{
    var mesh = meshData.Val.Mesh;
    var material = meshData.Val.Material;
}
```

### SaveRes — Resource Value Parameter

```csharp
[System]
public static void MySystem(ref SaveRes<MyData> data)
{
    ref var d = ref data.Ref;
}
```

`SaveRes<T>` currently contains a `T Ref` field and has empty `Init` / `Update`
methods. It does not itself register persistent world storage or call resource
lifecycle methods. Do not assume it provides automatic save/load support.

`Res<T>.Ref` uses static `StructSingleton<T>` storage, so it is not isolated per
world. Choose resource ownership explicitly when working with multiple worlds.

### Local — Per-System Local State

```csharp
[System]
public static void MySystem(ref Local<MyState> local)
{
    local.Value.counter++;
}
```

Each system gets its own isolated instance.

---

## Events

```csharp
public struct DamageEvent : IComponent { public int Amount; public Entity Target; }
```

### Sending Events

```csharp
[System]
public static void EmitDamage(ref Query<Entity, Health> query, ref Events<DamageEvent> events)
{
    foreach (var (entity, health) in query.par_iter())
        events.AddPar(new DamageEvent { Amount = 10, Target = entity });
}
```

### Receiving Events

```csharp
[System(Threads.Main)]
public static void ProcessDamage(ref Events<DamageEvent> events)
{
    foreach (var evt in events)
    {
        // Handle event
    }
    events.Clear();
}
```

`Add()` is for a single writer; `AddPar(in TEvent)` uses a spinlock and grows
the buffer while holding it. `ReadPar()` provides a pointer/length reader after
producers complete. Do not grow or clear the buffer while readers use it.
Events persist until explicitly cleared; clear once after all consumers finish.

---

## Reactivity

Use `Wargon.Nukecs.Reactivity` for per-entity subscriptions. A regular unmanaged
`IComponent` is sufficient; no `IReactive` marker or `Reactive<T>` companion is
needed for this API.

```csharp
using Wargon.Nukecs.Reactivity;

// After creating Systems for the world and creating the entity:
long token = entity.OnChange<Health>(
    (in Health value, in Entity owner) => UnityEngine.Debug.Log(value.Value));

entity.Get<Health>().Value -= 10;
// Callback runs when the reactive check/dispatch systems process the change.

// When the subscriber is no longer needed:
entity.OffChange<Health>(token);
```

The first subscription registers the check/dispatch systems for existing
`Systems` instances in that world. `systems.AddReactive<Health>()` can register
them explicitly. Detection compares component bytes in a Burst job; dispatch
invokes managed callbacks on the main thread. Notifications are processed at
the reactive systems' update position, not synchronously on every write.

`OnChange` also accepts a `ReactFilter<T>` predicate and `ReactOptions`:
`Once` removes the subscription after dispatch; `TriggerImmediately` invokes it
with the current component on subscription, or defers the initial notification
if the component has not yet been added through ECB.

`Wargon.Nukecs.Reactivity.Changed<T>` is a query filter with its own change
tracking. Use it in generated systems with plain `foreach (... in query)` as
covered by `ReactivityTests`. Explicit runtime `.iter()` / `.par_iter()` do not
apply this change-detection filter; they traverse its required component set.
Files under `src/Reactive/` describe the older implementation; use the public
API under `src/Reactivity/` for new subscriptions and change filters.

---

## Transforms

Nukecs provides built-in transform components:

### Transform (World-Space)

```csharp
public struct Transform : IComponent
{
    public float3 Position;
    public quaternion Rotation;
    public float3 Scale;
    public float4x4 Matrix => float4x4.TRS(Position, Rotation, Scale);
}
```

### LocalTransform (Local-Space)

```csharp
public struct LocalTransform : IComponent
{
    public float3 Position;
    public quaternion Rotation;
    public float3 Scale;
    public float4x4 Matrix => float4x4.TRS(Position, Rotation, Scale);
}
```

### TransformRef — Unity Transform Bridge

```csharp
public struct TransformRef : IComponent
{
    public ObjectRef<UnityEngine.Transform> Value;
}
```

Bridges ECS entities to `UnityEngine.Transform` GameObjects.

### Built-in Transform Systems

- **TransformChildSystem** — manages parent-child transform hierarchies
- **SyncWithUnityTransformSystem** — syncs ECS transforms to Unity transforms

---

## World Serialization

### Serialize / Deserialize

```csharp
byte[] data = world.Serialize();
world.Deserialize(data);
```

### File I/O

```csharp
world.SaveToFile("path/to/save.dat");
world.LoadFromFile("path/to/save.dat");
```

### Async File I/O

```csharp
await world.SaveToFileAsync("path/to/save.dat");
world.LoadFromFileAsync("path/to/save.dat");
```

`LoadFromFileAsync` currently returns `async void`, so it cannot be awaited.
Use `LoadFromFile` when the caller must know loading has finished before continuing.

### Static Load

```csharp
World.Load("path/to/save.dat", ref world);
```

Serialization captures allocator-backed world state, including entities,
components, queries, logical archetypes and shared storages. Deserialization
restores allocator pointers, migration caches and query bindings, and
re-registers component function pointers. Managed callbacks, external Unity
objects and static resources are not an automatic portable save format.

---

## Hot Reload (Editor Only)

`HotReloadSystems` wraps a regular `Systems` instance and swaps system runners when source files change during Play Mode.

### Setup

```csharp
using Wargon.Nukecs.HotReload;

private Systems systems;

void Awake()
{
    world = World.Create(WorldConfig.Default1024);

    systems = new Systems(ref world);
    systems
        .Add(MySystem.Update, Threads.MainRun)
        .Add(MySystem.Render, Threads.Main)
        .AddHotReload();
}

void Update()
{
    systems.OnUpdate(Time.deltaTime, Time.time);
}

void OnDestroy()
{
    world.Dispose();
}
```

### How It Works

1. `StartTracking()` resolves each system runner to its source `.cs` file
2. A file watcher monitors changes during Play Mode
3. On change, the system is recompiled via Roslyn/csc
4. The new runner replaces the old one, **preserving query state**

---

## World Configuration

```csharp
public struct WorldConfig
{
    public int StartPoolSize;
    public int StartEntitiesAmount;
    public int StartComponentsAmount;
}
```

### Presets

| Preset | Capacity |
|--------|----------|
| `WorldConfig.Default16` | 16 |
| `WorldConfig.Default` | 64 |
| `WorldConfig.Default256` | 256 |
| `WorldConfig.Default1024` | 1,024 |
| `WorldConfig.Default6144` | 6,144 |
| `WorldConfig.Default16384` | 16,384 |
| `WorldConfig.Default65536` | 65,536 |
| `WorldConfig.Default163840` | 163,840 |
| `WorldConfig.Default256000` | 256,000 |
| `WorldConfig.Default_1_000_000` | 1,000,000 |

### Multiple Worlds

The static registry supports up to **8 worlds**. Create them explicitly when
running multiple worlds: `WorldInstaller.Awake()` calls `World.DisposeStatic()`
before creating its world, so multiple installers do not provide independent
world ownership automatically.

```csharp
var world1 = World.Create(WorldConfig.Default256);
var world2 = World.Create(WorldConfig.Default1024);
```

---

## Editor Tools

- **Nuke.cs → ECS Debug V2** (`NUKECS_DEBUG`) — inspect entities, logical archetypes, queries and resources; edit component fields and themes.
- **Scene View entity gizmos** (`NUKECS_DEBUG`) — select an entity in Debug V2, then use Move/Rotate/Scale/Transform tools on its world-space `Wargon.Nukecs.Transforms.Transform`. Changes write directly to the component; Undo is not supported.
- **Nuke.cs → Dependency Graph** — inspect system dependencies and execution groups.
- **Nuke.cs → Allocator Debug** — memory usage, allocation tags and Arena Guard controls; available without `NUKECS_DEBUG`.

### Arena Guard

`AllocatorDebugState.Mode` defaults to `AllocatorDebugMode.None`. The allocator
window can enable canaries, freed-memory poisoning and tag tracking, run
**Validate now**, and schedule periodic validation.

- `Canary`: guarded allocations reserve 16 trailing bytes; only allocations
  made while enabled receive guards. Writes within alignment padding may escape detection.
- `PoisonFree`: freed memory begins with `0xDD`; validation detects later writes
  to that region. When enabling through code, call `PoisonAllFree()` on the
  allocator to normalize existing free blocks (the UI does this automatically).
- Allocation tags and `GetTagStats` show live counts/bytes by source. `Validate`
  checks headers, block chains and enabled guards. Disposal and allocator load
  also validate the arena.

## Verification

Run correctness tests with the Unity Editor's **Edit Mode** Test Runner, not
`dotnet test`. Relevant suites include `RuntimeQueryIntegrationTests`,
`RuntimeQueryProductionRegressionTests`, `RuntimeQueryJobIntegrationTests`,
`RuntimeQueryEntityDeconstructionTests`, `StorageModeQueryTests`,
`TagPoolMaskTests`, `AllocatorDebugTests`, `ReactivityTests` and
`DependencyGraphTests`. Run benchmarks separately from correctness tests.

Native Burst execution/compilation probes live in the runtime query suites.
A managed pass alone does not verify native Burst or IL2CPP. Historical run
results are recorded in the runtime contract and handoff; they are not a new
verification of every backend or graph scheduling mode.
