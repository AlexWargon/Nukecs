![Nukecs](https://github.com/AlexWargon/Nukecs/assets/37613162/827d5e54-82ff-45d5-af2f-bac06fabc2ec)

# Nukecs

**An Entity Component System for Unity, with Burst and the C# Job System.**

Keep game data in small structs and write gameplay as ordinary C# methods.
Nukecs generates the code that connects your systems to queries, resources,
and jobs.

- **High performance.** Efficient single-threaded iteration, source-generated loops,
  and parallel execution with Unity Jobs and Burst.
- **Less boilerplate.** Write systems as static `[System]` methods. Nukecs generates
  runners and connects queries, resources, and events. `WorldInstaller` handles
  world setup and cleanup.
- **Flexible component storage.** Combine inline data, tags, and pool components.
  Shared SoA storage avoids copying inline data when tags or pool components change.
- **Hot reload in Play Mode.** Edit system logic without restarting the simulation.
  Hot-reloaded systems run without Burst compilation.
- **Gameplay tools included.** Event streams, reactive subscriptions, shared
  resources, and per-system state.
- **Save and restore ECS state.** Serialize allocator-backed world data, including
  entities and components.
- **Built-in debugging.** Inspect entities, components, and system dependencies.
  Track arena memory usage and detect memory corruption with Arena Guard.

[Installation](#installation) · [Quick start](#quick-start) · [Components](#components) ·
[Entities](#entities) · [Queries](#queries) · [Systems](#systems) ·
[Further reading](#further-reading)

## Installation

1. Install Git, then open Unity's **Package Manager → Add package from git URL**.
2. Paste this URL:

   ```text
   https://github.com/AlexWargon/Nukecs.git#upm
   ```

3. Enable **Allow 'unsafe' Code** in Player Settings, or **Allow Unsafe Code**
   on your gameplay assembly definition. If you use your own assembly definition,
   add references to `Nukecs`, `Unity.Burst`, and the Unity packages your code uses.
4. Let Unity compile, then follow the quick start below.

For manual installation or the demos, copy the full `dev` checkout into
`Assets/Nukecs` and install the dependencies above through Package Manager.
Keep all `.meta` files, including the analyzer metadata in `SourceGen`.
Choose one installation method to avoid duplicate assemblies.
See [UPM distribution](UPM.md) for package contents and updates.

The tested environment is **Unity 6000.0.63f1**, Burst **1.8.29**, Collections
**2.6.2**, and Mathematics **1.3.2**. These are tested versions, not minimum
requirements. Windows player builds with both Mono and IL2CPP have been run and
behaved as in the Editor; other player platforms have not been checked.

## Quick start

This example moves one entity along the X axis at two units per second.
It introduces five ideas:

| Term | Meaning in this example |
|---|---|
| **World** | Owns the entities and their data. |
| **Entity** | Identifies the thing being moved. |
| **Component** | Stores one piece of data: position or velocity. |
| **Query** | Selects entities that have both components. |
| **System** | Updates their positions each frame. |

Create `QuickStart.cs`, paste the code below, and attach `QuickStart` to an empty
GameObject. Then enter Play Mode.

```csharp
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs;

namespace NukecsQuickStart
{
    public struct Position : IComponent { public float3 Value; }
    public struct Velocity : IComponent { public float3 Value; }

    public sealed class QuickStart : WorldInstaller
    {
        protected override void OnWorldCreated(ref World world)
        {
            Systems.Add(MySystems.MoveSystem);
        }

        protected override void CreateEntities(ref World world)
        {
            var entity = world.Entity();
            entity.Add(new Position { Value = float3.zero });
            entity.Add(new Velocity { Value = new float3(2f, 0f, 0f) });
        }

        private void Update()
        {
            Systems.OnUpdate(Time.deltaTime, Time.time);
        }
    }

    public static class MySystems
    {
        [System, BurstCompile]
        public static void MoveSystem(ref Query<Position, Velocity> query, ref State state)
        {
            foreach (var (position, velocity) in query)
            {
                position.Get.Value += velocity.Read.Value * state.Time.DeltaTime;
            }
        }
    }
}
```

**What happens:** `WorldInstaller` creates the world, then calls your two setup
hooks. It applies the queued component additions and runs startup systems.
Each `Update` runs the registered movement system. When the installer is
destroyed, it disposes the world and runs system cleanup.

Inside the loop, `.Get` gives writable access to a component; `.Read` gives
read-only access. `State.Time.DeltaTime` is the frame duration passed to
`Systems.OnUpdate`.

**Check the result:** enable the `NUKECS_DEBUG` Scripting Define Symbol and open
**Nuke.cs → ECS Debug V2** in Play Mode. Inspect the entity's `Position`: its X
value increases by approximately 2 each second of simulation time. This example
updates ECS data only. To see moving GameObjects, follow the
[rotate-cube demo setup](https://github.com/AlexWargon/Nukecs/blob/65218d9e1d891f619cb70efc283a8dcc21dc7f7b/Demos/README.md).

The snippets below build on the same `Position` and `Velocity` components.

## Components

A component is an unmanaged struct that holds data. Keep behavior in systems.

```csharp
public struct Health : IComponent { public int Value; }
public struct Frozen : IComponent { } // An empty component is a tag.
public struct Fire : IComponent { }
```

Use tags to mark an entity for filtering, such as `Frozen`, `Enemy`, or
`Selected`. They have no per-entity data payload.

For rare or large data, use `IPoolComponent`:

```csharp
public struct DamageRequest : IPoolComponent
{
    public int Amount;
}
```

Regular components live together in columns for iteration. Pool components
live in separate storage; adding or removing one preserves the entity's inline
data columns. Both kinds can appear in the same query.

Prefer small values and IDs in frequently used components. Put shared collections
in shared storage and keep a key in the component. The
[gameplay guide](NUKECS_AGENTS_GUIDE_EN.md#4-components-used-by-many-entities)
explains ownership, collection costs, and disposal.

## Entities

Create an entity and add its initial data in `CreateEntities`:

```csharp
var entity = world.Entity();
entity.Add(new Health { Value = 100 });
```

**Adding, removing, and destroying are deferred.** Nukecs queues these operations
in an Entity Command Buffer (ECB) and applies them at a safe point. The installer
applies initial additions after `CreateEntities`; the systems loop handles
playback during normal updates.

Once the component has been added, read or change its value directly:

```csharp
if (entity.Has<Health>())
{
    ref var health = ref entity.Get<Health>();
    health.Value -= 10;                         // Changes the stored value now.
    entity.Set(new Health { Value = 50 });      // Replaces an existing value now.
}

entity.Remove<Health>();                       // Queued.
entity.Destroy();                              // Queued.
```

Use `ref` when you want to edit the stored component; a value copy is independent
of it. `Add` adds a missing component; use `Set` to update an existing one.

Keep entity handles by value and use `entity.IsValid()` before accessing a handle
that may have been destroyed. Queue structural changes inside query loops and
let the scheduler apply them; do not call `world.Update()` during iteration.

## Queries

A typed query selects entities with the requested components. Nukecs initializes
queries passed to `[System]` methods automatically.

| Query | Selects |
|---|---|
| `Query<Position, Velocity>` | Entities with position and velocity. |
| `Query<Position, Velocity, With<Frozen>>` | Only those that also have `Frozen`. |
| `Query<Position, Velocity, None<Frozen>>` | Only those without `Frozen`. |
| `Query<Position, Velocity, (With<Frozen>, None<Fire>)>` | Only those with `Frozen` and without `Fire`. |
| `Query<Entity, Position, Velocity>` | The same data, plus the entity handle. |
| `Query<Health, Changed<Health>>` | Entities whose health changed. See the [reactivity example](API_REFERENCE.md#reactivity). |

To skip frozen entities, change the movement system's query type. The trailing
filter does not add a value to the loop:

```csharp
[System, BurstCompile]
public static void MoveSystem(ref Query<Position, Velocity, None<Frozen>> query,
    ref State state)
{
    foreach (var (position, velocity) in query)
    {
        position.Get.Value += velocity.Read.Value * state.Time.DeltaTime;
    }
}
```

Combine filters in a tuple to apply several conditions at once. This system
stops frozen entities unless they also have `Fire`:

```csharp
[System, BurstCompile]
public static void StopFrozenSystem(
    ref Query<Position, Velocity, (With<Frozen>, None<Fire>)> query)
{
    foreach (var (position, velocity) in query)
    {
        velocity.Get.Value = float3.zero;
    }
}
```

Both conditions must match. The tuple is a filter: the loop still returns only
`position` and `velocity`. Add this method to `MySystems` and register it before
`MoveSystem` with `Threads.Parallel` to stop matching entities before movement.

To remove entities that leave an area, add this method to `MySystems` and
register it after `MoveSystem` with `Threads.Parallel`:

```csharp
[System, BurstCompile]
public static void RemoveOutOfBounds(ref Query<Entity, Position> query)
{
    foreach (var (entity, position) in query.par_iter())
    {
        if (position.Read.Value.x > 100f)
            entity.Destroy();
    }
}
```

Here `entity` is an `Entity` value, so call `entity.Destroy()` directly.

Start with plain `foreach (... in query)` for component processing. Eligible
loops use generated batch code. If you need an explicit iterator,
**`par_iter()` respects the current job's range**; `iter()` visits the entire
query and would repeat that work in each parallel worker. Iterator details and
limits are in the [runtime query guide](src/Systems/FnSystems/RuntimeQuery/README.md).

Queries exclude prefab entities and the legacy `DestroyEntity` tag by default.
If you create manual `world.Query()` queries, retain and reuse them instead of
creating new ones every frame.

## Systems

A system is a static method marked with `[System]`. Add `[BurstCompile]` when
its code is Burst-compatible, then register the method in `OnWorldCreated`.
The generated runner provides its parameters and schedules its work.

### Choose an execution mode

| Mode | Use it for |
|---|---|
| `Threads.Parallel` | Independent per-entity work on worker threads. The default. |
| `Threads.Single` | Sequential work in one scheduled job. |
| `Threads.Main` | GameObjects, UI, input, and other managed Unity APIs. |
| `Threads.MainRun` | Synchronous job execution on the calling thread. |

Parallel workers should write only their own entity data. Shared counters,
resources, and writes to arbitrary target entities need synchronization or
sequential processing. Keep managed Unity API calls in `Threads.Main` systems
without `[BurstCompile]`. `MainRun` does not automatically wait for earlier jobs;
see the [execution reference](API_REFERENCE.md#adding-systems) before mixing it
with worker systems.

### Register systems in order

For several methods, chain `Add` calls or use `AddSystems`:

```csharp
// In OnWorldCreated; replace the single MoveSystem registration from QuickStart.
// RemoveOutOfBounds is the method shown above, added to MySystems.
Systems.AddSystems(SystemPath.Update,
    (MySystems.MoveSystem, Threads.Parallel),
    (MySystems.RemoveOutOfBounds, Threads.Parallel));
```

The default scheduler chains worker jobs in registration order. Registering a
method twice makes it run twice. For larger features, bundle registrations in
an [`ISystemsGroup`](API_REFERENCE.md#isystemsgroup--organize-systems).

### Select a lifecycle phase

Update is the default. Use `path:` for a different phase:

```csharp
// Initialize and Cleanup are your own static [System] methods.
Systems.Add(MySystems.Initialize, Threads.Main, path: SystemPath.Start);
Systems.Add(MySystems.Cleanup, Threads.Main, path: SystemPath.Destroy);
```

With `WorldInstaller`, Start runs once after initial entity setup; Destroy runs
when the world is disposed. You only need to drive `Systems.OnUpdate` from
`Update`. The installer already adds the default systems and calls `OnStart`.

For fixed-step systems or manual world ownership, see the
[lifecycle reference](API_REFERENCE.md#registering-a-lifecycle-phase).

### Use more than component data

Add parameters to a system method as needed:

| Parameter | Purpose |
|---|---|
| `ref State state` | Access the world and simulation time. |
| `ref Res<T> config` | Share a struct resource through `config.Ref`. |
| `ref ResManaged<T> view` | Access managed resource objects on the main thread. |
| `ref Local<T> local` | Keep state for this system registration through `local.Ref`. |
| `ref Events<T> events` | Send or receive a stream of events. |

Resources and locals implement `IRes`, with `OnCreate` and `OnUpdate` hooks.
`Res<T>` values live in the world arena and are isolated per world; read them
outside systems with `world.GetRes<T>()`. `Local<T>` values are isolated per
system registration.

For events, use `AddPar` in parallel producers. Read after producers finish and
clear after the last consumer. `WorldInstaller` also clears previous-frame events
at the start of each update. The [gameplay guide's event examples](NUKECS_AGENTS_GUIDE_EN.md#3-events-tags-pool-payloads-or-eventst)
show how to choose between tags, temporary payload components, and event buffers.

## Further reading

| I want to… | Read |
|---|---|
| Build gameplay with practical patterns | [Gameplay guide](NUKECS_AGENTS_GUIDE_EN.md) |
| Look up an API or an advanced feature | [API reference](API_REFERENCE.md) |
| Connect entities to visible GameObjects | [Transform integration](API_REFERENCE.md#transforms) and [demos](https://github.com/AlexWargon/Nukecs/blob/65218d9e1d891f619cb70efc283a8dcc21dc7f7b/Demos/README.md) |
| React when component values change | [Reactivity](API_REFERENCE.md#reactivity) |
| Save and restore a world | [Serialization](API_REFERENCE.md#world-serialization) |
| Inspect entities or memory in the Editor | [Editor tools](API_REFERENCE.md#editor-tools) |
| Understand iteration and batching | [Runtime queries](src/Systems/FnSystems/RuntimeQuery/README.md) and [batch generation](API_REFERENCE.md#generated-batch-code) |
| Understand the storage implementation | [Architecture](ARCHITECTURE.md) |
| Work on the framework itself | [Agent reference](AGENTS.md) and [test guidance](API_REFERENCE.md#verification) |
