# Nukecs API reference

Start with the [README](README.md) for installation and a first system. This
reference covers individual APIs, execution details, and advanced features.
Examples are independent snippets; application types such as `Speed`, `Health`,
`MySystems`, and `Config` stand for types defined in your own project.

- [Installation details](#installation)
- [Components](#components) and [entities](#entities)
- [Systems and lifecycle](#systems-fnsystems)
- [Queries](#queries) and [deferred changes](#entity-command-buffer-ecb)
- [State](#state), [resources](#resources), and [events](#events)
- [Reactivity](#reactivity)
- [Transforms](#transforms) and [Unity integration](#additional-unity-integration)
- [Save and load](#world-serialization)
- [Hot reload](#hot-reload-editor-only)
- [World configuration](#world-configuration)
- [Editor tools](#editor-tools) and [verification](#verification)
- [Generated batch code](#generated-batch-code)

## Installation

For UPM installation, use the Git URL in the [README](README.md#installation).
The distribution contains `src`, `SourceGen`, Markdown documentation, and package
metadata. Dependencies are declared in `package.json`; see [UPM distribution](UPM.md).

For manual installation, copy the full source checkout into `Assets/Nukecs`,
preserving `.meta` files, and install the dependencies yourself. Use either UPM
or the Assets copy, not both. The verified
Editor environment is Unity 6000.0.63f1 with Burst 1.8.29, Collections 2.6.2,
Mathematics 1.3.2 and Unity's Jobs API. These are tested versions, not claimed
minimum versions. Tests require Unity Test Framework (tested 1.6.0).

Unity authoring inspectors are included and reuse ECS Debug v2's theme and cards;
no third-party inspector package is required. They work without `NUKECS_DEBUG`.
GameObject authoring and live Debug v2 use the same `ComponentCardDrawer` renderer
for headers, fields and controls. Only the data binding differs (`SerializedObject`
versus live ECS data); visual changes to the renderer apply to both.
`WorldInstaller` displays a read-only world ID. `WorldBaker` provides Bake in edit
mode and Load/Save in play mode, with operation status and error reporting.
`EntityBaker` and `EntityLinkSO` display component cards. Add Component lists
concrete, non-generic `IComponent` and `IPoolComponent` types, including structs.
`EntityLinkSO` stores converter assets as
Unity object references.

Keep `SourceGen/NUKECSGEN.dll` and its meta file together: retain its
`RoslynAnalyzer` asset label and disable runtime plug-in loading. Generated
`Systems.Add` overloads and component registration depend on that analyzer.

Enable unsafe code in consuming assembly definitions. `NUKECS_DEBUG` is an
optional Scripting Define Symbol for diagnostics/editor tools. Editor tests do
not establish IL2CPP/player support. Physics, input, UI and audio remain
application code using Unity APIs/packages.

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

### IArrayComponent — Small Array Components

```csharp
public struct Child : IArrayComponent { public Entity Value; }
```

Fixed-capacity arrays attached to entities. Access them through `entity.GetArray<T>()`
and `entity.AddArray<T>()`. `AddArray` flushes the ECB, so call it during exclusive
setup, outside jobs and query iteration. See the [gameplay guide](NUKECS_AGENTS_GUIDE_EN.md#small-arrays-componentarrayt)
for capacity and ownership rules.

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
| `DestroyEntity` | Legacy exclusion tag; does not delete an entity |
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
    var e = entities[i];
    e.Add(new LocalTransform { Position = new float3(i, 0, 0) });
}
```

### Operations

```csharp
ref var speed = ref entity.Get<Speed>();            // read/write ref
ref readonly var readSpeed = ref entity.Read<Speed>();  // readonly ref
entity.Set(new Speed { Value = 10f });              // overwrite existing
entity.Add(new Speed { Value = 5f });               // add (deferred via ECB)
entity.Remove<Speed>();                             // remove (deferred via ECB)
bool has = entity.Has<Speed>();                     // check existence
ref var optionalSpeed = ref entity.TryGet<Speed>(out bool exists); // safe access
```

### Destruction

An entity handle occupies 8 bytes: `int id`, `ushort Generation`, `ushort WorldToken`.
WorldToken includes the world slot and its incarnation. Reusing an ID creates
a different generation: a saved copy of the old handle stays invalid. Equality and
hashing include the generation and remain stable after destruction or save/load.
Generation does not wrap: an ID reaching 65535 is retired on deletion.
`IsValid()` also checks that the world is alive. Keep handles by value; a `ref Entity`
into the world's entity array refers to a mutable slot and can change when reused.

`Has` returns false and `TryGet` returns a null reference for an expired handle;
`Destroy` / `DestroyNow` do nothing. Component access and mutation through an expired
handle throw rather than accessing the replacement entity. APIs taking a bare integer
ID, such as `world.GetEntity(id)`, resolve the current entity in that slot.
Reactive subscriptions belong to the full handle identity. Loading an unrelated
saved entity with the same ID requires an explicit new subscription.

On a live entity, `Add<T>` does nothing when T is already installed; `Set<T>`
does nothing when T is absent. `TryGet<T>` also returns a null reference when
the component is absent: check its `exists` output before dereferencing.

```csharp
entity.Destroy();      // deferred — processed on next world.Update()
entity.DestroyNow();   // immediate removal of this entity only
```

`entity.Destroy()` queues an ECB destroy command directly. `DestroyNow()`
immediately disposes installed components and removes the storage row and query
membership. It does not scan ECB buffers. Commands capture the generation and
expired commands are skipped during normal playback; their uninstalled disposable
payloads are freed during playback, `Clear()` or buffer disposal. Pool additions
defer both their payload and mask until playback. Complete outstanding jobs first and call it outside query
iteration. Destruction needs no registered system or `AddDefaults()`: ECB playback
handles `Destroy()`. Adding the legacy `DestroyEntity` tag does not delete entities.

Entity layout changed with generations. Save format is version 2; version 1 saves
are rejected rather than loaded into the new memory layout.

### Copying

```csharp
var copy = entity.Copy();         // immediate deep copy
var deferredCopy = entity.CopyViaECB(); // deferred copy via ECB
```

### Prefabs

```csharp
var prefab = world.Entity();
prefab.Add(new Speed { Value = 5f });
prefab.Add(new IsPrefab());
world.Update(); // Apply deferred prefab components before immediate copying.

var instance = world.SpawnPrefab(prefab);
var instances = world.SpawnPrefabs(prefab, 100);
```

Register `AddDefaults()` before gameplay systems: its `OnPrefabSpawn` removes
`IsPrefab` from spawned copies during the next system pass.

Unity-side `EntityPrefabMap` caches must resolve a current handle after loading.
`GetOrCreatePrefab(source, ref world)` reuses a uniquely named `IsPrefab` entity
from the loaded world; otherwise it converts the source. Keep prefab names unique.
Cached Entity values in MonoBehaviours are not rewritten by arena deserialization.

### Hierarchy

```csharp
child.SetParent(parent);
world.Update();
var firstChild = parent.GetChild(0);
var root = child.GetRootParent(); // Entity.Null when child has no parent
parent.RemoveChild(child);
world.Update();
```

`SetParent` and `AddChild` can
flush the entire ECB through `AddArray`; call them during exclusive setup,
outside jobs and query iteration. Destruction does not cascade to children:
destroy owned children explicitly before their parent.

---

## Systems (FnSystems)

Nukecs uses a **source-generated** approach. Mark static methods with `[System]` — the source generator creates job structs, runner classes, and `Systems.Add()` overloads automatically.

### System Attribute

```csharp
[System] // marks the method for source generation
```

Declare the containing class and every enclosing class `partial`:

```csharp
public static partial class MySystems
{
    const float Speed = 2f;
    static float Scale(float v) => v * Speed;      // private helper

    [System, BurstCompile]
    public static void Update(ref Query<Velocity> query)
    {
        foreach (ref var v in query) v.Value = Scale(v.Value); // short name
    }
}
```

The generator emits the job struct nested in that class (`MySystems.__Update_Job`),
so the body binds exactly as written in the class: short calls to public,
internal and private static members, constants, nested types, and type names
resolved in your namespace before `Wargon.Nukecs`. The `#line` mapping keeps
compile errors and the debugger on your source file. The framework part of the
generated code (job interface, runner, dependency metadata) stays in
`Wargon.Nukecs`; a runner's `Name` is `"MySystems_Update"`.

| Diagnostic | Meaning |
|---|---|
| `NUKECS010` (error) | The system is declared in a generic type. Move it to a non-generic class. |
| `NUKECS011` (error) | A containing type is private or protected; generated runners need at least `internal`. |
| `NUKECS012` (warning) | A containing type is not `partial`. The body compiles in your namespace with `using static` for the class: public static members work by short name, private/internal-only helpers do not. |
| `NUKECS013` (error) | The class already declares the generated `__<Method>_Job` name. Names starting with `__` are reserved. |

Hot reload recompiles a body in a separate assembly. It sees public members by
short name through `using static`, but not private helpers; such an edit fails
to compile and the previous runner stays active.

Thread mode is selected when registering the method:
`systems.Add(MySystems.Update, Threads.MainRun)`. The attribute takes no thread-mode
argument. `MainRun` uses synchronous `job.Run()` on the calling thread, without a
dependency argument; synchronize outstanding jobs before accessing data they use.

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

### Thread Modes

```csharp
public enum Threads
{
    Main,       // Main thread
    MainRun,    // Main thread via Job System Run
    Parallel,   // All parallel threads (default)
    Single      // Single worker thread
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

`WorldInstaller` already calls `OnStart` once after `CreateEntities` and initial
ECB playback; do not call it again from installer hooks. Fixed update uses a
hardcoded 0.016-second interval and runs at most once per `OnUpdate`, without
catch-up ticks.

`AddDefaults()` appends `OnPrefabSpawn` and `ClearEvents`, both MainRun, to the
Update list. Call it before gameplay registrations. `Systems.Default(ref world)`
is shorthand for `new Systems(ref world).AddDefaults()`. Under the default
scheduler, MainRun does not wait for previous jobs. Legacy/Chained graph modes
synchronize execution groups before MainRun; Flattened graph modes run all
Main/MainRun systems before scheduling worker systems.

Method systems are recommended for new gameplay. `ISystem`, `IEntityJobSystem`
and `SystemsGroup` remain supported compatibility APIs; their examples in
AGENTS.md describe existing code, rather than the preferred style for new code.

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
logical rows may be sparse. `Chunk<T1..T8>.CopyTo` gathers sparse rows and copies
dense rows contiguously. The copy begins at the current chunk position; copy
before advancing the iterator.

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

`None<T1, T2>` and `With<T1, T2>` support multiple components.

`Any<T1..T5>` requires **at least one** of its components. Combine filters in a
tuple in the last type parameter; the result is an AND of all groups:

```csharp
// has Health, has Burning or Frozen (or both), and is not Dead
ref Query<Health, (Any<Burning, Frozen>, None<Dead>)> query

// several filters: every With, no None, at least one Any
ref Query<A, B, (With<C, D>, Any<E, F, G>)> query
```

All `Any<>` of one query form a single group (`(Any<A>, Any<B>)` equals
`Any<A, B>`). Any types are filters only: they never appear in the loop tuple and
are not recorded in `SystemDependencyInfo`. The generator reports `NUKECS020` when
a type is in both `Any` and `None` (it can never satisfy the group) and
`NUKECS021` when it is also a queried component or `With` (the group is always
satisfied). Batched systems with `Any` stay `PointerBatch`/`RequireBatch`-compatible.

Iteration cost: an `Any` group of regular inline components is decided per
storage and keeps the dense storage walk. Tag or pool components in `Any` are
decided per logical archetype: a storage whose non-empty rows all match (or all
fail) stays dense; a storage that mixes them switches the query to the
archetype walk until the mix disappears.

Filter/tag tuple slots contain no entity payload; omit the trailing filter when deconstructing:

```csharp
// query: Query<Entity, LocalTransform, Speed, None<StaticTag>>
foreach (var (entity, transform, speed) in query.par_iter())
    transform.Get.Position.x += speed.Read.Value * dt;
```

### ISystemsGroup — Organize Systems

```csharp
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

// at least one of Burning / Frozen
var affected = world.Query().With<Health>().Any<Burning>().Any<Frozen>();
```

`Any<T>()` / `Any(int typeIndex)` add to the query's single Any group. An
explicit `Any` of a default none type (`IsPrefab`, `DestroyEntity`) removes it
from the none mask, like `With`. A type explicitly in both `None` and `Any` logs
a warning and never satisfies the group.

Create and retain manual queries during setup. Each `world.Query()` registers
a new query; identical fluent chains are not deduplicated. Queries created or
modified after entities exist lazily rescan the existing archetypes on the main
thread. Do not create a new query every frame.

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
`With<T>`, `Any<T...>`, or a tuple of filters. Runtime iteration supports up to eight data components
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

`AddRes` and `AddResManaged` call `OnCreate(ref World)` immediately on first
registration in that world. Connecting the resource to additional systems does
not call it again. Repeated registration of an existing resource is ignored and
preserves its initialized value/instance. Resources first requested by a system
are initialized automatically through the same `Init` lifecycle.

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

For resources that reference managed objects (e.g., `Mesh`, `Material`), define
the resource as a `class` implementing `IRes`:

```csharp
world.AddResManaged(new MeshData { Mesh = mesh, Material = material });

[System]
public static void Render(ref ResManaged<MeshData> meshData)
{
    var mesh = meshData.Val.Mesh;
    var material = meshData.Val.Material;
}
```

`Res<T>.Ref` uses static `StructSingleton<T>` storage, so it is not isolated per
world. Choose resource ownership explicitly when working with multiple worlds.

### Local — Per-System Local State

```csharp
public struct MyState : IRes
{
    public int Counter;
    public void OnCreate(ref World world) { Counter = 0; }
    public void OnUpdate(ref World world) { }
}

[System]
public static void MySystem(ref Local<MyState> local)
{
    local.Ref.Counter++;
}
```

`T` must be `unmanaged, IRes`. Every registration gets its own value, including
repeated registrations of the same method. Two `Local<T>` parameters are also
independent. Local values are isolated between worlds and `Systems` containers;
they do not use the global singleton backing `Res<T>`.

The generator emits numeric owner keys. Registration and lookup use the framework's
unmanaged `HashMap` and `SharedStatic` registry, and can execute in Burst.

`OnCreate` runs once when the local value is created. `OnUpdate` runs once on the
main thread before each system invocation, including an empty query. Parallel
work ranges share the same local value: synchronize writes, for example with
`Interlocked`, or register the system as `Single` for ordinary mutable state.

Local values are stored in the world arena and restored by Save/Load, also in
another editor session. A local is identified by its system method and parameter,
the index of its `Systems` container in the world, and the ordinal of repeated
registrations of the same method; the order of unrelated systems does not matter.
A local absent from the snapshot is initialized normally. External native
allocations referenced by a local are not serialized automatically.

---

## Events

```csharp
public struct DamageEvent { public int Amount; public Entity Target; }
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
[System]
public static void ProcessDamage(ref Events<DamageEvent> events)
{
    foreach (ref var evt in events)
    {
        // Handle event
    }
    events.Clear();
}
```

`Add()` is for a single writer; `AddPar(in TEvent)` uses a spinlock and grows
the buffer while holding it. `ReadPar()` provides a pointer/length reader after
producers complete. Do not grow or clear the buffer while readers use it.
`AddDefaults()` registers `ClearEvents`, which clears all event buffers at its
position in the Update list. Register defaults first, as `WorldInstaller` does:
clearing removes previous-frame events before this frame's producers/consumers.
Under the default scheduler, late defaults can race with Parallel writes because
ClearEvents is MainRun. Without defaults, clear once after all consumers finish
and all producer jobs complete.

---

## Reactivity

Use `Wargon.Nukecs.Reactivity` to process changes through system queries or
per-entity callbacks. A regular unmanaged `IComponent` is sufficient; no
`IReactive` marker or `Reactive<T>` companion is needed.

### `Changed<T>` — Query changed components

Use `Changed<T>` to process entities only when a component's value changes.
Add `using Wargon.Nukecs.Reactivity;` to your file, then add this method to
`MySystems`:

```csharp
[System]
public static void HealthChangedSystem(ref Query<Health, Changed<Health>> query)
{
    foreach (ref var health in query)
    {
        UnityEngine.Debug.Log($"Health changed to {health.Value}");
    }
}
```

Register it in `OnWorldCreated`, after systems that update health:

```csharp
Systems.Add(MySystems.HealthChangedSystem, Threads.Main);
```

This example uses the main thread for logging. The first observation records
the initial value; later updates process changed values. Assigning the same
value does not count as a change. A regular `Health : IComponent` is enough;
no `OnChange` subscription is needed.

**Use plain `foreach (... in query)` with `Changed<T>`.** Change filtering relies
on the generated system path. Explicit `iter()` and `par_iter()` do not apply
the changed-only filter. With one data component, `foreach (ref var health in query)`
gives direct component access, so use `health.Value` without `.Get` or `.Read`.

### `OnChange<T>` — Subscribe to callbacks

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
- **UpdateTransformOnAddChildSystem** — updates transforms when a child is attached
- **SyncWithUnityTransformSystem** — syncs ECS transforms to Unity transforms

Register them explicitly; `AddDefaults()` and `WorldInstaller` do not add them:

```csharp
systems.AddGroup(new Wargon.Nukecs.Transforms.TransformsGroup());
```

Sync reads ECS world-space `Transform` and `TransformRef`, then writes the Unity
Transform. `LocalTransform` alone does not move a GameObject. Convert an existing
Unity Transform before updating, then flush the deferred additions:

```csharp
var entity = world.Entity();
Wargon.Nukecs.Transforms.TransformsUtility.Convert(gameObject.transform, ref world, ref entity);
entity.Add(new Wargon.Nukecs.Transforms.TransformRef {
    Value = new ObjectRef<UnityEngine.Transform>(gameObject.transform)
});
world.Update();
```

---

## Additional Unity Integration

### Unity authoring and visible entities

During setup on the main thread, create a visible GameObject and attach it to
an ECS entity using the transform bridge:

```csharp
var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
var visibleEntity = world.Entity();
Wargon.Nukecs.Transforms.TransformsUtility.Convert(view.transform, ref world, ref visibleEntity);
visibleEntity.Add(new Wargon.Nukecs.Transforms.TransformRef { Value = view.transform });
world.Update();
systems.AddGroup(new Wargon.Nukecs.Transforms.TransformsGroup());
// Ensure a camera sees the cube; update its ECS Transform to move it.
```

`TransformRef` and `GameObjectRef` (the latter is currently in
`Wargon.Nukecs.Tests`) are references, not GameObject ownership. Entity destruction
does not automatically destroy those objects. Release owned views on the main
thread in application cleanup. `ClearTransformsSystem` remains a manual
main-thread compatibility system for explicitly tagged `DestroyEntity` views:
it destroys their GameObjects only. Ordinary `Destroy()` does not add that tag.
The rotate-cube demo shows an explicit disposable view owner.

`EntityBaker` stores inspector-configured `IComponent` values and creates an
entity when `Bake(ref world)` is called; it does not bake automatically or
provide a GameObject link. `Wargon.Nukecs.Tests.EntityLinkSO.Convert(ref world)`
creates an entity from a ScriptableObject's components and custom converters.
Both defer component additions; apply the setup ECB before reading/copying them.
`WorldBaker` is an abstract authoring/session component: subclasses implement
`Bake(ref world)` and `AddSystems`, choose a file name and use its inspector bake/
load buttons. These are managed authoring helpers, not Burst gameplay systems.

### Other existing APIs

`entity.AddObject(IComponent)` adds boxed component data through ECB;
`SetObject(IComponent)` writes component data through runtime type metadata.
They are managed authoring/debug APIs, not Burst methods; prefer generic
`Add<T>` / `Set<T>` in gameplay. `SetObject` on pool storage uses the pool setter,
while the inline path does nothing when the component is absent.

`world.GetSingleton<T>()` is a low-level accessor for pool slot 0. It neither
queries for a unique entity nor initializes a missing value; the caller must
ensure that pool slot exists. Use `Res<T>` for resource state. Aspects implement
`IAspect<T>` and `IAspect`; `world.GetAspect<T>(ref entity)` / `entity.GetAspect<T>()`
update a cached per-type aspect for that entity. Do not retain its mutable
reference across another aspect access or share it across parallel work.

Rendering samples and scene setup are in [Demos/README.md](Demos/README.md).

## World Serialization

### Serialize / Deserialize

```csharp
byte[] data = world.Serialize();
world.Deserialize(data);
// Restore a new world after disposing the saved world:
world.Dispose();
world = World.Load(WorldConfig.Default1024, data);
```

The byte-array overload restores the saved world slot, even when it is not slot 0.
That slot must be free; an occupied slot is rejected without replacing its world.

### File I/O

```csharp
world.Save("path/to/save.dat");
world.Load("path/to/save.dat");
```

`Save` / `Load` execute immediately and first complete the world's active jobs,
including jobs already scheduled in the current system pass. `SaveToFile` /
`LoadFromFile` and `Serialize` / `Deserialize` use the same completion policy.
`Threads.MainRun` retains its `Jobs.Run()` semantics; it does not wait for
dependencies automatically. Explicit saving/loading performs that wait.

### Deferred file requests

```csharp
Task save = world.RequestSave("path/to/save.dat");
Task<World> load = world.RequestLoad("path/to/save.dat");
// Requests execute before the next primary systems.OnUpdate(dt, time).
// Observe/await the tasks after that pass to receive completion or I/O errors.
// If retaining a World struct copy, refresh it: world = await load;
```

Requests execute in insertion order before update systems, without an extra
`Complete`: the preceding systems update already completed its jobs. Requests
issued during a pass or by load callbacks wait for the next pass. With several
`Systems` containers for one world, the first registered container owns this
boundary and must be updated first. Complete any externally scheduled jobs
before this boundary; they are outside the framework's dependency tracking.

`RequestLoad` returns `Task<World>` containing the refreshed world, because loading
can relocate its arena. Registered systems are rebound automatically; external
code retaining a `World` struct should assign the returned value.

Queues live outside the saved arena and are independent per world. Disposing
the world cancels pending tasks. A failed request faults its task and leaves
later requests eligible to execute. These file APIs are managed main-thread
adapters; do not call them from Burst jobs or block on a request task inside
the system pass that must process it.

### Async File I/O

```csharp
await world.SaveToFileAsync("path/to/save.dat");
world = await World.LoadAsync("path/to/save.dat", world);
```

`World.LoadAsync` returns `Task<World>`. Assign the result: deserialization may
move the arena, so the old struct copy can hold a stale pointer. The legacy
`LoadFromFileAsync` returns `async void`; use the static awaitable API above.

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

    systems = new Systems(ref world).AddDefaults();
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
| `WorldConfig.Default1024` | 1,025 |
| `WorldConfig.Default6144` | 6,144 |
| `WorldConfig.Default16384` | 16,385 |
| `WorldConfig.Default65536` | 65,536 |
| `WorldConfig.Default163840` | 163,841 |
| `WorldConfig.Default256000` | 256,001 |
| `WorldConfig.Default_1_000_000` | 1,000,001 |

### Multiple Worlds

The static registry supports up to **8 worlds**. Each `WorldInstaller` creates
and disposes its own world. `World.DisposeStatic()` is an explicit session/domain
reset that affects all worlds; do not call it when creating another installer.

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

## Generated Batch Code

For an ordinary component query, the current generator requires:

- A `[System]` method with one plain `foreach (var (...) in query)` naming the
  first typed `Query<...>` parameter directly. For a single data component,
  use `foreach (ref var value in query)`. Execute through the generated runner.
- The generator replaces the selected loop and preserves surrounding statements,
  including locals, early returns before the loop, cleanup after it, and enclosing
  `unsafe` blocks or `if` branches. Captured ordinary locals are forwarded by ref.
  Local functions, nested loops in the selected loop, multiple loops over the
  primary query, and `return`/`break`/`goto`/`yield` inside it cause fallback.
  Ref locals, constants captured from outside the loop, anonymous types, and
  captured names beginning with `_` or named `state`/`range` are unsupported.
- Recognizable iteration variables and component types, with **no iterated
  `IPoolComponent` types**. Explicit `.iter()` / `.par_iter()` calls always use
  runtime iterators, including inside generated systems.

Both `OnUpdateBatched` and `OnUpdateBatchedParallel` preserve the surrounding
code. With `Threads.Parallel`, it executes once per assigned work range, including
an empty query's scheduled range; shared writes must be thread-safe. Use a
separate main-thread system for work that must happen exactly once per update.

Add `[RequireBatch]` to make unsupported traversal an error (`NUKECS002`) rather
than a silent fallback. Inspect a generated runner's compile-time status:

```csharp
// updateSystems is the Systems container created during initialization.
// Runners exposes Update registrations only, not Start/FixedUpdate/Destroy.
foreach (var runner in updateSystems.Runners)
    if (runner is ISystemCompilationInfoProvider provider)
        UnityEngine.Debug.Log($"{runner.Name}: {provider.CompilationInfo.Kind}, " +
            $"fallback: {provider.CompilationInfo.FallbackReason}\n" +
            $"{provider.CompilationInfo.FallbackDetail}\n" +
            $"{provider.CompilationInfo.FallbackFile}:" +
            $"{provider.CompilationInfo.FallbackLine}:{provider.CompilationInfo.FallbackColumn}");
```

`PointerBatch` confirms generated pointer walkers; `ChangedBatch` identifies the
special change-detection path. `RuntimeIteration` includes a fallback reason,
and `NoQuery` means there is no primary query. `HasSurroundingCode` records whether
the selected loop has a surrounding envelope. This metadata describes generated
code, not whether Burst executed natively or which dense/sparse branch ran.

`FallbackDetail` identifies the blocking component, captured local, or syntax
and suggests a correction. `FallbackFile`, `FallbackLine`, and `FallbackColumn`
identify its source location (line and column are one-based). `NUKECS002` reports
the same detail at the offending source node. The first blocker is reported;
fixing it may reveal another. Successful batch generation has empty detail/path
and zero location. Inspect these fields even without `[RequireBatch]`; fallback
does not produce automatic runtime logs or new compiler warnings.

For dense inline workloads, use a plain query loop as in the [quick start](README.md#quick-start), with
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
