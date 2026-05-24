
![logo-no-background](https://github.com/AlexWargon/Nukecs/assets/37613162/827d5e54-82ff-45d5-af2f-bac06fabc2ec)

### <img src="https://github.com/AlexWargon/Nukecs/assets/37613162/553b8223-c304-4429-8def-96e2830d5ca7" width=2% height=2%> NUKECS — Fast C# Entity Component System for Unity

Burst-compiled ECS framework with source-generated systems, custom allocator, and hot reload support.

- **Burst-compiled** systems by default
- **Source-generated** system runners from `[System]` static methods
- **Custom arena allocator** — no GC pressure
- **World serialization** — save/load entire world state
- **Hot reload** — edit systems during Play Mode

---

## Quick Start

### 1. Create a WorldInstaller

Inherit from `WorldInstaller`, add systems in `OnWorldCreated`, and drive the update loop:

```csharp
using Wargon.Nukecs;
using Wargon.Nukecs.Transforms;
using Unity.Mathematics;

public class GameBootstrap : WorldInstaller
{
    protected override WorldConfig GetConfig() => WorldConfig.Default256;

    protected override void OnWorldCreated(ref World world)
    {
        Systems
            .AddGroup(new GameSystems())
            ;
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
```

Components are unmanaged structs. Empty structs become **tag components** with zero memory cost.

### 3. Define Systems

```csharp
[BurstCompile]
public static class MovementSystems
{
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

### 4. Add Systems to the World

```csharp
Systems
    .Add(MovementSystems.Move, Threads.MainRun)
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

Stored inline in archetype data arrays. This is the default and most efficient storage.

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
entity.DestroyNow();   // immediate
```

`entity.Destroy()` is equivalent to `entity.Add(new DestroyEntity())`. The built-in `EntityDestroySystem` handles actual cleanup.

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

```csharp
Systems
    .Add(MySystems.Spawn, Threads.MainRun)
    .Add(MySystems.Update, Threads.MainRun)
    .Add(MySystems.Render, Threads.Main)
    .Add(MySystems.Physics)              // default: Threads.Parallel
    ;
```

### Query Iteration

#### `par_iter()` — Parallel-safe ref iteration (recommended)

```csharp
foreach (var (t, v) in query.par_iter())
{
    ref var transform = ref t.Get;       // read/write
    ref readonly var vel = ref v.Read;   // readonly
    transform.Position += vel.Value * dt;
}
```

#### `iter_unsafe()` — Raw pointer iteration (highest performance)

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
    transform.Position += v.Value * dt;
}
```

#### `iter_chunk()` — Chunk-based iteration

```csharp
foreach (var chunk in query.iter_chunk())
{
    // Process entities in chunks
}
```

### WithEntity — Access Entity in Iteration

Append `.WithEntity` to get the `Entity` in the deconstruction:

```csharp
[System, BurstCompile]
public static void Process(
    ref Query<LocalTransform, Speed>.WithEntity query,
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

### Query Filter Modifiers

Use `None<T>` and `With<T>` as the last type parameter to filter without reading:

```csharp
// None<T> — exclude entities that have component T
ref Query<LocalTransform, Velocity, None<StaticTag>> query

// With<T> — include only entities that have component T (readable via .Get)
ref Query<LocalTransform, With<CubeStateTag>> query
```

`None<T1, T2>` and `With<T1, T2>` support multiple components.

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

Always add `[BurstCompile]` to both the containing class (for `ISystemsGroup`) or the static method for maximum performance:

```csharp
[System, BurstCompile]
public static void MySystem(ref Query<Transform> query) { }
```

---

## Queries

### Fluent API (manual queries)

```csharp
var query = world.Query()
    .With<LocalTransform>()
    .With<Speed>()
    .None<StaticTag>();
```

### Generic Typed Queries (in systems)

Queries in `[System]` methods are auto-created by the source generator:

```csharp
Query<T1>
Query<T1, TOption>
Query<T1, T2, TOption>
Query<T1, T2, T3, TOption>
Query<T1, T2, T3, T4, TOption>
Query<T1, T2, T3, T4, T5, TOption>
```

Where `TOption` can be a regular component, `None<T>`, or `With<T>`.

### Access Patterns

```csharp
ref T val = ref componentRef.Get;       // read/write access
ref readonly T val = ref componentRef.Read;  // readonly access
```

### Query Properties

```csharp
int count = query.Count;
bool empty = query.IsEmpty;
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

> **Important:** Changes are not visible until the next `Update()`. If you need immediate access, use `entity.Set<T>()` to modify existing components.

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
    public float ElapsedTime;
    public int TickCount;
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

### SaveRes — Per-World Allocator-Stored Resource

```csharp
[System]
public static void MySystem(ref SaveRes<MyData> data)
{
    ref var d = ref data.Ref;
}
```

`SaveRes<T>` is stored in the world's custom allocator and survives serialization.

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
public static void ApplyDamage(ref Events<DamageEvent> events)
{
    events.Add(new DamageEvent { Amount = 10, Target = target });
}
```

### Receiving Events

```csharp
[System]
public static void ProcessDamage(ref Events<DamageEvent> events)
{
    foreach (var evt in events)
    {
        // Handle event
    }
    events.Clear();
}
```

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
await world.LoadFromFileAsync("path/to/save.dat");
```

### Static Load

```csharp
World.Load("path/to/save.dat", ref world);
```

Serialization captures the entire world state: all entities, components, queries, and archetypes. Function pointers are re-registered on deserialization automatically.

---

## Hot Reload (Editor Only)

`HotReloadSystems` wraps a regular `Systems` instance and swaps system runners when source files change during Play Mode.

### Setup

```csharp
using Wargon.Nukecs.HotReload;

private HotReloadSystems hotReload;

void Awake()
{
    world = World.Create(WorldConfig.Default1024);

    hotReload = new HotReloadSystems(ref world);
    hotReload.Systems.Add(MySystem.Update, Threads.MainRun);
    hotReload.Systems.Add(MySystem.Render, Threads.Main);
    hotReload.StartTracking();
}

void Update()
{
    hotReload.OnUpdate(Time.deltaTime, Time.time);
}

void OnDestroy()
{
    hotReload?.Dispose();
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

Up to **8 worlds** can exist simultaneously. Each `WorldInstaller` manages its own world.

```csharp
var world1 = World.Create(WorldConfig.Default256);
var world2 = World.Create(WorldConfig.Default1024);
```

---

## Editor Tools

- **ECS Debug Window** — inspect entities, archetypes, and components at runtime
- **ECS Dashboard** — cyberpunk-styled visual debugger with entity tables and archetype panels
- **Allocator Debugger** — monitor custom allocator memory usage
- **Memory Profiler** — track memory allocation patterns
