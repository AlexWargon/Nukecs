# Nukecs ECS Framework — Agent Reference

## 1. Project Overview

Nukecs is a **Burst-compiled ECS framework for Unity**. It uses `unsafe` code and raw pointers throughout for maximum performance. A source generator at `../../SourseGen/NUKECSGEN/` generates system runners and component type registrations.

- **Runtime**: .NET Framework 4.7.1 (Unity legacy)
- **Dependencies**: Unity.Burst, Unity.Collections, Unity.Jobs, Unity.Mathematics
- **Assembly defs**: `Nukecs.asmdef` (runtime), `Nukecs.Tests.asmdef` (tests), `AllocatorEditor.asmdef` (debug)
- **`[BurstCompile]`** used on hot paths

## 2. Architecture

```
World → Archetype[] → Entity (int ID)
     → Queries[]     → QueryEnumerator / Query<T1..TN, TOption> / Chunk<T1..T8>
     → Systems        → OnUpdate() dispatch (onStart/onUpdate/onFixedUpdate/onDestroy)
     → ECB            → deferred add/remove/destroy
     → Events<T>      → thread-safe event buffers (spinlock-protected)
     → Res<T>         → singleton resources (unmanaged + managed)
     → Reactive<T>    → change detection via memcmp + Changed<T> tags
     → HotReload      → Roslyn-based runtime system recompilation
```

- **World** — central container: entity storage, archetype management, pools, queries (`src/World/World.cs` safe wrapper, `src/World/World.Unsafe.cs` core). Static management via `World.Static.cs` (`SharedStatic` world list, `ALLOCATOR` struct). Disposal in `World.Free.cs`.
- **Archetype** — stores entities with same component mask; SoA data layout (`src/Archetype.cs`)
- **Entity** — lightweight ID (`int`); location stored in `World.entityLocations` (archetypeIndex + row)
- **Query** — matches entities by component mask; iterates via `QueryEnumerator` or generic `Query<T1..TN, TOption>` (`src/Query.cs`, `src/Systems/FnSystems/Query.cs`)
- **Systems** — functions with `[System]` attribute; source-gen creates runners; 3 thread modes: Main, Single, Parallel (`src/Systems/Systems.cs`). Lifecycle lists: `onStart`, `onUpdate`, `onFixedUpdate`, `onDestroy`. `ISystemRunner` interface for struct/class systems.
- **EntityCommandBuffer (ECB)** — deferred add/remove/destroy; flushed on `world.Update()` (`src/Entity/EntityCommandBuffer.cs`)
- **Component Storage** — two modes: inline (packed in archetype data array) and Pool (separate `GenericPool<T>` with SparseSet) (`src/Components/GenericPool.cs`)
- **Allocator** — custom `MemAllocator` with `ptr<T>` wrapper, `MemoryArray<T>`, `MemoryList<T>` (`src/Allocator/`)
- **ISystemParam** — unified interface for system parameters: Query, Res, State, Events, Single, Local, Chunk. Source generator recognizes these and wires them up.
- **Events** — `Events<TEvent>` thread-safe event buffer; `AddPar` for parallel writes (spinlock); `EventsParallelReader<TEvent>` for parallel reads; `EventsStorage` central registry (`src/Systems/FnSystems/Events.cs`)
- **Resources** — `Res<T>` / `ResManaged<T>` singleton resource accessors; `IRes` interface with `OnCreate`/`OnUpdate`; `ResStorage` unmanaged storage (`src/Systems/FnSystems/Res.cs`, `ResManaged.cs`, `ResStorage.cs`)
- **Chunk Iteration** — `Chunk<T1..T8>` archetype chunk iterators; `IChunk` interface; direct pointer iteration over archetype component arrays (`src/Systems/FnSystems/Chunk.cs`)
- **Reactive** — `IReactive` marker + `Reactive<T>` stores old value + `Changed<T>` tag; `ReactiveCheckSystem<T>` detects changes via memcmp; `ReactAndClearSystem<T>` clears tags and fires callbacks (`src/Reactive/`)
- **Hot Reload** — `HotReloadSystems` wraps `Systems`; file watching + Roslyn compilation + runner swapping at runtime (`src/Systems/HotReload/`, `src/Unity/Editor/HotReload/`)
- **DynamicBuffer** — `DynamicBuffer<T>` Unity-style dynamic buffer component (`src/Components/DynamicBuffer.cs`)
- **IEntityJobSystem** — per-entity job system interface; `EntityJobSystemRunner<T>` dispatches (`src/Systems/EntityJobSystem.cs`)

## 3. Key Files Map

### Core

| File | Description |
|------|-------------|
| `src/Archetype.cs` | `ArchetypeUnsafe`: entity CRUD, batch ops, archetype edges/transitions, SoA data layout |
| `src/World/World.Unsafe.cs` | `WorldUnsafe`: entity create/destroy, archetype registry, query management, batch ops |
| `src/World/World.cs` | Safe `World` wrapper |
| `src/World/World.Entities.cs` | (stub — entity helpers moved) |
| `src/World/World.Components.cs` | `AspectType` definitions |
| `src/World/World.Allocation.cs` | World memory allocation helpers |
| `src/World/World.Static.cs` | Static world creation/management, `ALLOCATOR` struct, `SharedStatic` world list |
| `src/World/World.Free.cs` | World/WorldUnsafe disposal logic |
| `src/World/World.StoryLog.cs` | Debug ring-buffer for component changes (`#if NUKECS_DEBUG`) |
| `src/World/nukecs.cs` | Version info struct (`NukEcs { version, name, author }`) |
| `src/Query.cs` | `QueryUnsafe`: archetype matching, entity tracking, `QueryEnumerator` |
| `src/Entity/Entity.cs` | `Entity` struct + extension methods (Get, Set, Add, Remove, Destroy, Copy) |
| `src/Entity/EntityCommandBuffer.cs` | Deferred operations buffer |
| `src/Entity/EntityAspectExtensions.cs` | Aspect-related entity extension methods |
| `src/Entity/EntityArrayExtensions.cs` | Array operation entity extensions |
| `src/Entity/EntityChildrenExtensions.cs` | Parent/child entity hierarchy extensions |

### Components

| File | Description |
|------|-------------|
| `src/Components/ComponentTypeMap.cs` | Static type → index registry |
| `src/Components/ComponentTypeData.cs` | `ComponentTypeData` struct (size, storageType, etc.) |
| `src/Components/ComponentType.cs` | Per-type `SharedStatic<ComponentTypeData>` with lazy registration |
| `src/Components/Component.cs` | Core interfaces: `IComponent`, `IArrayComponent`, `IPoolComponent`, `IReactive`, `Changed<T>`, `Reactive<T>`, `Name`, `DestroyEntity`, `EntityCreated`, `IsPrefab`, `ChildOf` |
| `src/Components/GenericPool.cs` | `GenericPool` for Pool-stored components (SparseSet-based) |
| `src/Components/DynamicBuffer.cs` | `DynamicBuffer<T>` — Unity-style dynamic buffer component |
| `src/Components/ComponentArray.cs` | `ComponentArray<T>` — array-as-component type |
| `src/Components/ComponentData.cs` | Serialization helper (byte[] representation of components) |
| `src/Components/UnsafeStatic.cs` | `UnsafeStatic` utility (memcpy, as_ref, etc.) |
| `src/Components/DisposeRegistryStatic.cs` | Dispose tracking for unmanaged resources |
| `src/Components/TypeExtensions.cs` | Type reflection extensions |
| `src/Components/GeneratedComponentList.cs` | Auto-generated component registration list |

### Systems

| File | Description |
|------|-------------|
| `src/Systems/Systems.cs` | `Systems` container; lifecycle lists (onStart/onUpdate/onFixedUpdate/onDestroy); `ISystemRunner` support; `Add<T>()` overloads |
| `src/Systems/State.cs` | `State` struct (World, TimeData, Dependencies) — system execution context, implements `ISystemParam` |
| `src/Systems/WorldSystems.cs` | Static registry mapping world IDs to `Systems` instances |
| `src/Systems/_systems_internal.cs` | Internal helper for routing system runners to correct lifecycle list |
| `src/Systems/Marker.cs` | `Marker` struct wrapping Unity `ProfilerMarker` |
| `src/Systems/TimeData.cs` | `TimeData` struct (DeltaTime, Time, etc.) |
| `src/Systems/EntityJobSystem.cs` | `IEntityJobSystem` interface + `EntityJobSystemRunner<T>` runner |
| `src/Systems/EntityDestroySystem.cs` | Built-in entity destruction system |
| `src/Systems/ECBJob.cs` | ECB processing job |
| `src/Systems/StartFixedECBSystem.cs` | Start/fixed-update ECB processing |
| `src/Systems/JobSystem.cs` | Job system base |
| `src/Systems/IQueryJobSystem.cs` | Query-based job system interface |
| `src/Systems/BuiltInSystems.cs` | Built-in system registrations |

### Systems / FnSystems

| File | Description |
|------|-------------|
| `src/Systems/FnSystems/Query.cs` | Generic query iterators `Query<T1..T5, TOption>` with `Query<Entity, ...>` for entity+components |
| `src/Systems/FnSystems/QueryGeneric.cs` | Generic query job system runners |
| `src/Systems/FnSystems/QueryIterators.cs` | Query iterator implementations |
| `src/Systems/FnSystems/QueryIteratorsParallel.cs` | Parallel query iterator implementations |
| `src/Systems/FnSystems/Chunk.cs` | `Chunk<T1..T8>` archetype chunk iterators + `IChunk` interface |
| `src/Systems/FnSystems/Events.cs` | `Events<TEvent>` system param, `EventsParallelReader<TEvent>`, `EventsStorage` |
| `src/Systems/FnSystems/Res.cs` | `Res<TRes>` (resource param), `SaveRes<TRes>`, `TimeRes` |
| `src/Systems/FnSystems/ResManaged.cs` | `ResManaged<TRes>` for class-type resources |
| `src/Systems/FnSystems/ResStorage.cs` | `ResStorage` unmanaged resource storage |
| `src/Systems/FnSystems/Single.cs` | `Single<T1>` (singleton entity accessor), `MutRes<TRes>`, `Local<TData>`, `IRes`, `IResourceGetSet` |
| `src/Systems/FnSystems/ManagedResRef.cs` | `ManagedResRef<T>` (GCHandle-like managed reference wrapper) |

### Systems / FnSystems / Tuples

| File | Description |
|------|-------------|
| `src/Systems/FnSystems/Tuples/RefTuple.cs` | Ref-based query tuples |
| `src/Systems/FnSystems/Tuples/PtrTuple.cs` | Pointer-based query tuples |
| `src/Systems/FnSystems/Tuples/EntityRefTuple.cs` | Entity+ref query tuples |
| `src/Systems/FnSystems/Tuples/EntityPtrTuple.cs` | Entity+ptr query tuples |
| `src/Systems/FnSystems/Tuples/ObjectTuple.cs` | Object-based query tuples |

### Systems / Runners

| File | Description |
|------|-------------|
| `src/Systems/Runners/SystemMainThreadRunnerStruct.cs` | Runner for `struct ISystem` (main thread, Burst-friendly) |
| `src/Systems/Runners/SystemMainThreadRunnerClass.cs` | Runner for `class ISystem` (managed) |
| `src/Systems/Runners/SystemDestroyer.cs` | `SystemDestroyer<T>` for `IOnDestroy` unmanaged systems |
| `src/Systems/Runners/SystemClassDestroyer.cs` | Destroyer for class-type systems |

### Systems / HotReload

| File | Description |
|------|-------------|
| `src/Systems/HotReload/HotReloadSystems.cs` | `HotReloadSystems` class: wraps `Systems`, tracks source files, swaps runners on recompile |

### Reactive

| File | Description |
|------|-------------|
| `src/Reactive/ReactiveCheckSystem.cs` | `ReactiveCheckSystem<T>` detects component changes via memcmp; `AddReactive<T>()` extension |
| `src/Reactive/ReactAndClearSystem.cs` | Clears `Changed<T>` tags and fires callbacks |
| `src/Reactive/ComponentChangeEvent.cs` | Static event system for component change notifications |
| `src/Reactive/ReactDelegate.cs` | `ReactDelegate<T>` delegate type |

### Collections

| File | Description |
|------|-------------|
| `src/Collections/MemoryArray.cs` | `MemoryArray<T>` — unmanaged resizable array via allocator |
| `src/Collections/MemoryList.cs` | `MemoryList<T>` — unmanaged list via allocator |
| `src/Collections/DynamicBitmask.cs` | Bitmask for archetype component matching |
| `src/Collections/HashMap.cs` | Custom hash map |
| `src/Collections/AliveEntitiesSet.cs` | Sparse-set based alive entity tracking |
| `src/Collections/Bitmask1024.cs` | Bitmask for 1024 elements |
| `src/Collections/Bitmask4096.cs` | Bitmask for 4096 elements |
| `src/Collections/BitMap1024.cs` | Fast hashmap for 1024 elements |
| `src/Collections/MultiArray.cs` | MultiArray collection |

### Allocator

| File | Description |
|------|-------------|
| `src/Allocator/Allocator.cs` | `MemAllocator` — arena allocator (+ Arena Guard: tags, canary, poison, `Validate`, `GetTagStats`) |
| `src/Allocator/AllocatorDebug.cs` | Arena Guard: `AllocatorDebugState.Mode` (SharedStatic runtime flags), `AllocatorTags`, violation kinds/reporting |
| `src/Allocator/ptr.cs` | `ptr<T>` — safe pointer wrapper |
| `src/Allocator/Serialization.cs` | Allocator serialization (+ free-list rebuild after load, post-load validation) |
| `src/Allocator/Spinner.cs` | Spinlock implementation (copy of Unity internal Spinner) |

### Misc Root Files

| File | Description |
|------|-------------|
| `src/dbug.cs` | Debug logging utility |
| `src/NUnsafe.cs` | Additional unsafe utilities |
| `src/Singleton.cs` | `StructSingleton<T>` and `Singleton<T>` implementations |
| `src/SparseSet.cs` | Sparse set data structure |
| `src/StaticAllocations.cs` | Static allocation helpers |
| `src/SystemsGroup.cs` | `SystemsGroup` — named group of system runners |
| `src/rng.cs` | Random number generation utilities |
| `src/EntityFilterBuffer.cs` | Entity filtering buffer |
| `src/QueryFilter.cs` | Query filtering logic |
| `src/Usings.cs` | Global using directives |

### Unity Integration

| File | Description |
|------|-------------|
| `src/Unity/WorldInstaller.cs` | World lifecycle management MonoBehaviour |
| `src/Unity/WorldBaker.cs` | Baker for sub-scene conversion |
| `src/Unity/EntityBaker.cs` | Entity prefab baking |
| `src/Unity/EntityPrefabMap.cs` | Prefab → entity mapping |
| `src/Unity/SyncTransformsSystem.cs` | Transform synchronization system |
| `src/Unity/Transforms/Transform.cs` | Transform component |
| `src/Unity/Transforms/LocalTransform.cs` | Local transform component |
| `src/Unity/Transforms/TransformChildSystem.cs` | Child transform system |
| `src/Unity/Transforms/TransformsGroup.cs` | Transforms system group |
| `src/Unity/Transforms/UpdateTransformOnAddChildSystem.cs` | Transform update on child add |
| `src/Unity/Utils/Reflect.cs` | Reflection utilities |
| `src/Unity/Resoursers/EntityBlueprintEditor.cs` | Entity blueprint editor (UIElements) |

## 4. Data Layout & Conventions

- **Archetype data**: SoA layout — `componentOffsets[i]` = byte offset to start of component `i`'s row array; each row = `componentSize * capacity` apart
- **Entity location**: `World.entityLocations[entityID] = { archetypeIndex, row }`
- **Packed entities**: `ArchetypeUnsafe.packedEntities[row] = entityID`
- **Query iteration**: `QueryEnumerator` walks `matchingArchetypes[]` → archetype → `packedEntities[row]`
- **Component access**: `data.Ptr + componentOffset + row * componentSize`
- **Two component storage types**: `StorageType.Default` (inline in archetype) and `StorageType.Pool` (separate pool)
- **`TOption` in queries**: can be `None<T>`, `Any<T>`, `With<T>`, or a regular component
- **Component type index**: `ComponentType<T>.Index` — per-type `SharedStatic<ComponentTypeData>` with lazy registration
- **Alive entity set**: `AliveEntitiesSet` (SparseSet-based) tracks living entity IDs
- **Events range**: `Events<TEvent>.Range` set per-thread for parallel iteration; `RangeEnumerator` walks `start..end`

## 5. Known Bugs / Pitfalls (resolved)

- `BatchCreateEntity` must call `EnsureCapacity`, fill `packedEntities`, set `entityLocations.row`, memclear component data, increment `count`
- `TOptIsComponent` static field in `Query<T1, TOption>` was stale — use `QueryParamInfo<TOption>.IsComponent` instead
- `QueryEnumerator` needs `_lastArch < 0` guard on first `MoveNext` to avoid null deref
- `SetupTN` methods need `li < 0` guard before `.SetArchetype()` calls
- `MoveNext` in generic queries needs `_archIdx >= matchingArchetypes.length` bounds check
- `Update()` in all `Query<T1..TN, TOption>` and `.WithEntity` variants: the archetype row loop must use the snapshot of `count` taken *before* iteration (not re-read from archetype each tick), otherwise entities added during iteration cause OOB access

## 6. Testing

- Unity Edit mode tests in `UnitTests/`
- Key test files: `SystemChainTests.cs`, `WorldTests.cs`, `AdvancedTests.cs`, `EventsTests.cs`, `SerializationTests.cs`, `PrefabSpawnTests.cs`, `ResizeTests.cs`, `AllocatorTests.cs`, `AddObjectTests.cs`, `SimpleTest.cs`
- Tests run via Unity Editor (not `dotnet test`) — results in `UnitTests/TestResults_*.xml`
- SystemChainTests: 14 tests covering system chains (3+ systems in sequence), thread modes, batch creation
- EventsTests: tests for `Events<TEvent>` add/read/clear and parallel safety

## 7. Source Generator

- Located at `../../SourseGen/NUKECSGEN/` (note: `ComponentTypeGenerator .cs` has a space before `.cs`)
- Generates system runners (`ISystemRunner`), query system job runners, and component type registrations
- Generated output in `NUKECSGEN/` directory at solution root
- `[System]` attribute marks static methods as ECS systems; source gen creates runner classes

### Generated System Structure

For each `[System]` method, the generator produces:
- `IXxxSystemJob` interface with `OnUpdate` + `OnUpdateBatched`
- `IXxxSystemJobExtensions` static class with `QuerySystemJobWrapper<TJob>` struct (Execute dispatch)
- `IXxxQuerySystemJobRunner<TJob>` runner class (Schedule/Run for Main/Single/Parallel modes)
- `XxxJob` struct implementing `IXxxSystemJob` (copies user's method body)

### OnUpdateBatched Design

`OnUpdateBatched` is **always `void`** and **always has `ref State state`** in its parameters (added automatically if the user's `OnUpdate` doesn't have it). This guarantees `state.World.UnsafeWorld` is available for the batch archetype loop path.

**Fallback is internal** — call sites (Execute Single, Runner Main) just call `OnUpdateBatched(...)` directly, no `bool` checks.

Three generated body variants:
1. **Batchable + hasQuery** — storage check; if not all archetypes, fallback to `Update` + `OnUpdate` + `return`; else archetype pointer loop (rewritten foreach)
2. **Non-batchable + hasQuery** — `Update` + `OnUpdate` (regular foreach path)
3. **No query** — `OnUpdate` only

### Key Variables in Code Generation

- `hasStateParam` — whether user's `OnUpdate` has a `State` parameter
- `onUpdateCallArgs` — args for calling `OnUpdate` from inside `OnUpdateBatched` (e.g., `ref q, ref cp`) — no `state` prefix, no `fullData.` prefix
- `onUpdateBatchedParams` — method signature params for `OnUpdateBatched`; appends `, ref State state` if `!hasStateParam`
- `onUpdateBatchedCallExecute` / `onUpdateBatchedCallRunner` — call-site args that always include state

### Batch Optimization (ForEachAnalysis)

When a system's body is a single `foreach (var (a, b) in query)` with all components inline (`StorageType.Archetype`), the generator rewrites it to a direct archetype pointer loop bypassing `QueryEnumerator`. `ForeachBodyRewriter` replaces `a.Val` / `a.Get` with `_p0[_i]` array access.

## 8. System Parameters (ISystemParam)

All system parameters implement `ISystemParam` with `Init(ref ptr<World.WorldUnsafe>)`, `Update(ref World, IntPtr)`, and `MetaType` property. The source generator auto-recognizes these types in `[System]` method signatures.

| Param | MetaType | Description |
|-------|----------|-------------|
| `State` | `State` | Execution context: World, TimeData, Dependencies |
| `Query<T1..TN, TOption>` | `Query` | Component query with foreach iteration |
| `Res<TRes>` | `Resource` | Read/write access to unmanaged singleton resource |
| `ResManaged<TRes>` | `Resource` | Read/write access to managed (class) singleton resource |
| `Events<TEvent>` | `Events` | Thread-safe event buffer; `AddPar` for parallel writes |
| `Single<T1>` | `Single` | Access singleton entity with component T1 |
| `Local<TData>` | `Local` | Per-system local data |
| `Chunk<T1..T8>` | — | Direct archetype chunk pointer iteration |

## 9. Events System

- `Events<TEvent>` — `ISystemParam` backed by `MemoryList<TEvent>` via `MemAllocator`
- **Thread-safe writes**: `AddPar(in TEvent)` acquires `Spinner` spinlock before list push; auto-resizes under lock
- **Parallel reads**: `ReadPar()` returns `EventsParallelReader<TEvent>` (readonly ptr + length); per-thread `Range` set by `Update(ref World, IntPtr data)` where `data` points to a `Range` struct
- **Iteration**: `RangeEnumerator` walks `Range.start..Range.end` of the event list
- **Storage**: `EventsStorage` — `HashMap<int, ptr>` keyed by type hash; `Get<TEvents>(ref ptr<World.WorldUnsafe>)` lazily creates on first access; `ClearAll()` iterates all event types

## 10. Resource System

- `Res<TRes>` where `TRes : struct, IRes` — wraps `StructSingleton<TRes>` (static storage); `IRes` has `OnCreate(ref ptr<World>)` and `OnUpdate(ref World)`
- `ResManaged<TRes>` — for class-type resources; uses `ManagedResRef<T>` (GCHandle-like wrapper)
- `IResourceGetSet` — boxing/unboxing interface for reflection-based access (used by debug tools)
- `ResStorage` — unmanaged storage registry for resources
- `SaveRes<TRes>` — save-only resource accessor (no auto-update)

## 11. Chunk Iteration

- `Chunk<T1..T8>` — direct archetype chunk iterators implementing `IChunk`
- `SetData(ref ArchetypeUnsafe)` — resolves component pointers via `GetComponentLocalIndex` + `GetComponentOffset`
- Iterator pattern: `_remaining` countdown; each `MoveNext()` decrements and advances all component pointers
- Access via `C0`..`C7` properties (ref returns) or `Get()` for single-type chunks
- `CopyTo<TU>(TU* dest, int len)` — selective memcpy by matching type index

## 12. Reactive System

- `IReactive` — marker interface on components that should be tracked for changes
- `Reactive<T>` — stores `oldValue` of type T; added alongside reactive component
- `Changed<T>` — tag component added by `ReactiveCheckSystem<T>` when value differs from stored old value
- `ReactiveCheckSystem<T> : IEntityJobSystem` — per-entity memcmp between current and `Reactive<T>.oldValue`; adds `Changed<T>` on mismatch
- `ReactAndClearSystem<T>` — clears `Changed<T>` tags and fires registered callbacks
- Registration: `systems.AddReactive<T>()` adds both check and clear systems

## 13. Hot Reload

- `HotReloadSystems` — wraps `Systems`; tracks source files of registered system runners
- **Flow**: `StartTracking()` → `TrackRunnerList()` resolves `[System]` methods → `HotReloadWatcher.Watch(path)` monitors files → on change `HotReloadCompiler` (Roslyn) recompiles → `OnSystemsCompiled` callback swaps runners in-place
- `SystemsHotReloadExtensions.AddHotReload(this Systems)` — convenience extension; registers `onWorldDispose` cleanup
- Editor-only (`#if UNITY_EDITOR`); `HotReloadCompiler.PrewarmCache()` called in constructor
- Source files tracked in `SystemEntry` struct (filePath, methodName, declaringTypeName, threadMode, runnerIndex)

## 14. World Lifecycle

- **World creation**: `World.Static.cs` — `ALLOCATOR` struct provides domain/per-world allocators via `SharedStatic`; world instances stored in `SharedStatic` array
- **Lifecycle lists** (in `Systems`): `onStart`, `onUpdate`, `onFixedUpdate`, `onDestroy` — `List<ISystemRunner>` per phase
- **Disposal**: `World.Free.cs` — `WorldUnsafe.Free()` releases all allocator memory; `Systems` `onWorldDispose` callback fires
- **StoryLog**: `World.StoryLog.cs` — debug ring-buffer recording component add/remove/set operations (behind `#if NUKECS_DEBUG`)
- **Deserialization hook**: `IOnWorldDeserialize` interface for post-deserialization fixup

## 15. Unity-Specific Notes

- Uses `[BurstCompile]` on hot paths — avoid managed allocations in Burst-compiled code
- `MemAllocator` is a custom arena allocator, not Unity's `Allocator`
- Jobs use `IJobParallelFor` and `IJob` from Unity.Jobs
- `UnityAllocatorHandler.cs` / `UnityAllocatorWrapper.cs` bridge to Unity's allocator for specific use cases
- `World.SerializeAndSave.cs` handles world serialization
- `World.Aspects.cs` provides aspect (group-of-components) support
- `EntityFilterBuffer.cs` and `QueryFilter.cs` handle entity filtering
- `src/Unity/Transforms/` — Transform hierarchy: `Transform`, `LocalTransform` components + child/parent systems
- `src/Unity/Utils/Reflect.cs` — Reflection utilities for editor tooling
- `src/Unity/Editor/HotReload/` — Editor-side hot reload: `HotReloadCompiler`, `HotReloadRoslynCompiler`, `HotReloadWatcher`
- `src/Unity/Editor/EcsDebugV2/` — Cyberpunk-styled data-driven debugger (see `ecs-debug-v2` skill)
- `src/Unity/Editor/EcsDashboard/` — Dashboard debugger (see `ecs-dashboard` skill)
- `src/Unity/Editor/World/` — Legacy debug windows (ECSDebugWindow, ECSMemoryProfilerWindow, etc.)
- `src/Unity/Editor/Allocator/` — Allocator debug windows (separate `AllocatorEditor.asmdef`)
- `Demos/` — Example projects: `RotateCubeDemo`, `BoidsDemo`, `CubeSculptureDemo`

## 16. Usage Patterns (Quick Reference)

### Namespaces

```csharp
using Wargon.Nukecs;
using Wargon.Nukecs.Transforms;
using Wargon.Nukecs.HotReload;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
```

### Component Definition

```csharp
// Data component
public struct Health : IComponent
{
    public float Value;
}

// Tag component (no data)
public struct EnemyTag : IComponent { }

// Component with IDisposable (cleanup on entity destroy)
public struct GameObjectView : IComponent, IDisposable
{
    public ObjectRef<GameObject> val;
    public void Dispose()
    {
        if (val.IsValid() && val != null)
        {
            Object.Destroy(val.Value);
            val.Dispose();
        }
    }
}

// Built-in tag components: DestroyEntity, EntityCreated, IsPrefab, ChildOf, Name
// Interfaces: IComponent, IArrayComponent, IPoolComponent, IReactive
```

### Resource Definition

```csharp
// Unmanaged resource (struct IRes)
public struct ConfigData : IRes
{
    public int TargetCount;
    public float CubeScale;
    public void OnCreate(ref World world) { }
    public void OnUpdate(ref World world) { }
}

// Managed resource (class IRes)
public class MeshData : IRes
{
    public Mesh Mesh;
    public Material Material;
    public void OnCreate(ref World world) { }
    public void OnUpdate(ref World world) { }
}
```

### System Definition ([System] static method — source-generated)

```csharp
// Thread modes: Main, MainRun, Single, Parallel (default)
public class GameSystems
{
    // Query + State
    [System, BurstCompile]
    public static void MovementSystem(
        ref Query<Position, Velocity> query,
        ref State state)
    {
        var dt = state.Time.DeltaTime;
        foreach (var (pos, vel) in query)
        {
            pos.Get.X += vel.Read.X * dt;
            pos.Get.Y += vel.Read.Y * dt;
        }
    }

    // Unsafe pointer iteration (for Burst)
    [System, BurstCompile]
    public static unsafe void FastMovement(
        ref Query<Position, Velocity, With<EnemyTag>> query,
        ref State state)
    {
        var dt = state.Time.DeltaTime;
        foreach (var (pos, vel) in query.iter_unsafe())
        {
            pos->X += vel->X * dt;
            pos->Y += vel->Y * dt;
        }
    }

    // Parallel unsafe iteration
    [System, BurstCompile]
    public static unsafe void ParallelPhysics(
        ref Query<Position, Velocity> query,
        ref State state)
    {
        var dt = state.Time.DeltaTime;
        foreach (var (pos, vel) in query.par_iter_unsafe())
        {
            pos->X += vel->X * dt;
        }
    }

    // Query + Res + State
    [System, BurstCompile]
    public static void SpawnSystem(
        ref State state,
        ref Res<ConfigData> config)
    {
        var count = config.Ref.TargetCount;
        config.Ref.timer -= state.Time.DeltaTime;
    }

    // Query + multiple Res + State
    [System, BurstCompile]
    public static void AISystem(
        ref Query<Position, EnemyTag> query,
        ref State state,
        ref Res<ConfigData> config,
        ref Res<GameWorldData> worldData)
    {
    }

    // With Entity (get entity + components) — Entity is always first type param
    [System]
    public static void DamageSystem(
        ref Query<Entity, Health> query,
        ref State state,
        ref Events<DamageEvent> events)
    {
        foreach (var (e, hp) in query)
        {
            if (hp.Read.Value <= 0)
                e.Destroy();
        }
    }

    // None<T> filter (exclude entities with component) + Entity
    [System]
    public static void AddVelocitySystem(
        ref Query<Entity, Position, None<Velocity>> query)
    {
        foreach (var (e, _) in query)
        {
            e.Add(new Velocity { X = 0, Y = 0 });
        }
    }

    // State only (no query)
    [System, BurstCompile]
    public static void TimerSystem(ref State state)
    {
    }

    // ResManaged<T> for class resources
    [System]
    public static unsafe void RenderSystem(
        ref Query<LocalTransform, EnemyTag> query,
        ref ResManaged<MeshData> meshData)
    {
        var param = new RenderParams(meshData.Val.Material);
    }

    // 5 components + TOption
    [System, BurstCompile]
    public static void ComplexSystem(
        ref Query<Position, Velocity, Health, EnemyTag, Weapon> query,
        ref State state)
    {
    }
}
```

### IEntityJobSystem (struct-based per-entity system)

```csharp
[BurstCompile]
public struct RotateSystem : IEntityJobSystem
{
    public Threads Mode => Threads.Parallel;
    public Query GetQuery(ref World world)
    {
        return world.Query().With<Transform>().With<RotationSpeed>();
    }
    public void OnUpdate(ref Entity entity, ref State state)
    {
        ref var transform = ref entity.Get<Transform>();
        ref var speed = ref entity.Get<RotationSpeed>();
        transform.Rotation = math.mul(
            transform.Rotation,
            quaternion.AxisAngle(math.up(), speed.RadiansPerSecond * state.Time.DeltaTime)
        );
    }
}
```

### ISystem struct (manual query management)

```csharp
public struct CustomSystem : ISystem, IOnCreate
{
    private Query query;

    public void OnCreate(ref World world)
    {
        query = world.Query().With<Position>().With<Velocity>();
    }

    public void OnUpdate(ref State state)
    {
        foreach (ref var e in query)
        {
            ref var pos = ref e.Get<Position>();
            ref var vel = ref e.Get<Velocity>();
            pos.X += vel.X * state.Time.DeltaTime;
        }
    }
}
```

### World Setup

```csharp
// Create world
var world = World.Create(WorldConfig.Default256);

// Create systems
var systems = new Systems(ref world)
    .AddDefaults()
    .Add(GameSystems.MovementSystem, Threads.Main)
    .Add(GameSystems.FastMovement)
    .Add(GameSystems.SpawnSystem, Threads.MainRun)
    .Add<RotateSystem>()                         // IEntityJobSystem struct
    .AddGroup(new TransformsGroup());             // ISystemsGroup

// Register resources
world.AddRes(new ConfigData { TargetCount = 100 });
world.AddResManaged(new MeshData { Mesh = myMesh, Material = myMat });

// Game loop
void Update()
{
    systems.OnUpdate(Time.deltaTime, Time.time);
}

// Cleanup
void OnDestroy()
{
    world.Dispose();
}
```

### WorldConfig presets

```csharp
WorldConfig.Default16          // 16 entities
WorldConfig.Default            // 64 entities
WorldConfig.Default256         // 256 entities
WorldConfig.Default1024        // 1024 entities
WorldConfig.Default6144
WorldConfig.Default16384
WorldConfig.Default65536
WorldConfig.Default163840
WorldConfig.Default256000
WorldConfig.Default_1_000_000
```

### Entity Creation

```csharp
// Empty entity
ref var entity = ref world.Entity();

// With components
var e = world.Entity(new Health { Value = 100 }, new Position { X = 0, Y = 0 });

// Create then add (deferred via ECB)
ref var e = ref world.Entity();
e.Add(new Health { Value = 100 });
e.Add<EnemyTag>();
e.Add(new Name("Player"));

// Batch creation
var entities = world.BatchCreateEntity(count);
for (int i = 0; i < entities.Length; i++)
{
    ref var e = ref entities[i];
    e.Add(new Position { X = i * 1.5f });
    e.Add<Velocity>();
}

// From archetype
var arch = world.GetArchetype(typeof(Position), typeof(Velocity));
var e = arch.CreateEntity();
e.Get<Position>().X = 5;

// Batch from archetype
var entities = arch.BatchCreateEntity(count);
```

### Entity Component Access

```csharp
ref var hp = ref entity.Get<Health>();             // Read
entity.Set(new Health { Value = 75 });              // Write
bool hasHp = entity.Has<Health>();                  // Check
entity.Add(new Health { Value = 10 });              // Add (deferred via ECB)
entity.Add<TagComponent>();                         // Add tag (deferred)
entity.Remove<Health>();                            // Remove (deferred via ECB)
ref var pos = ref entity.TryGet<Position>(out bool exist); // TryGet
entity.Destroy();                                   // Deferred destroy
entity.DestroyNow();                                // Immediate destroy
```

### Query API

```csharp
// Build queries via fluent API
var q1 = world.Query().With<Health>();
var q2 = world.Query().With<Health>().With<Velocity>();
var q3 = world.Query().With<Health>().None<EnemyTag>();

int count = q1.Count;
bool empty = q1.IsEmpty;
Entity first = q1.First();

// Iterate non-generic query
foreach (ref var entity in q1)
{
    ref var hp = ref entity.Get<Health>();
}
```

### Query Iteration Modes

```csharp
// Ref-based (safe)
foreach (var (pos, vel) in query)
{
    pos.Get.X += vel.Read.X;
}

// Pointer-based (unsafe, Burst-friendly)
foreach (var (pos, vel) in query.iter_unsafe())
{
    pos->X += vel->X;
}

// Parallel ref
foreach (var (pos, vel) in query.par_iter())
{
    pos.Get.X += vel.Read.X;
}

// Parallel pointer
foreach (var (pos, vel) in query.par_iter_unsafe())
{
    pos->X += vel->X;
}
```

### Events

```csharp
// Define event (plain struct, no interface)
public struct DamageEvent
{
    public int EntityId;
    public float Amount;
}

// Produce events (single-thread)
[System]
public static void ProduceDamage(
    ref Query<Entity, Health> query,
    ref Events<DamageEvent> events)
{
    foreach (var (e, hp) in query)
    {
        events.Add(new DamageEvent { EntityId = e.id, Amount = 10 });
    }
}

// Produce events (parallel-safe)
[System]
public static void ProduceDamageParallel(
    ref Query<Entity, Health> query,
    ref Events<DamageEvent> events)
{
    foreach (var (e, hp) in query)
    {
        events.AddPar(new DamageEvent { EntityId = e.id, Amount = 10 });
    }
}

// Consume events
[System]
public static void ApplyDamage(
    ref State state,
    ref Events<DamageEvent> events)
{
    foreach (ref var ev in events)
    {
        var e = state.World.GetEntity(ev.EntityId);
        ref var hp = ref e.Get<Health>();
        hp.Value -= ev.Amount;
    }
}

// Consume events via parallel reader
var reader = events.ReadPar();
for (int i = 0; i < reader.Length; i++)
{
    ref var ev = ref reader[i];
}
```

### ISystemsGroup (organize systems into a class)

```csharp
[BurstCompile]
public class GameSystemsGroup : ISystemsGroup
{
    public void Build(Systems systems, ref World world)
    {
        systems
            .Add(MovementSystem, Threads.Main)
            .Add(FastPhysics)
            .Add(SpawnSystem, Threads.MainRun)
            .Add(RenderSystem, Threads.Main);
    }

    [System, BurstCompile]
    public static void MovementSystem(/* ... */) { }

    [System, BurstCompile]
    public static void FastPhysics(/* ... */) { }
}

// Registration:
systems.AddGroup(new GameSystemsGroup());
```

### WorldInstaller (Unity MonoBehaviour integration)

```csharp
public class GameBootstrap : WorldInstaller
{
    [SerializeField] int entityCount = 100;

    protected override WorldConfig GetConfig() => WorldConfig.Default256;

    protected override void OnWorldCreated(ref World world)
    {
        world.AddRes(new ConfigData { TargetCount = entityCount });
        Systems.AddGroup(new GameSystemsGroup());
    }

    protected override void CreateEntities(ref World world)
    {
        var entities = world.BatchCreateEntity(entityCount);
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i].Add(new Position { X = i });
            entities[i].Add<Velocity>();
        }
    }

    void Update()
    {
        Systems.OnUpdate(Time.deltaTime, Time.time);
    }
}
```

### Systems.Add() Overloads

```csharp
// Function-based (source-generated runner)
systems.Add(ClassName.MethodName, Threads.Main);
systems.Add(ClassName.MethodName);             // Default = Parallel

// IEntityJobSystem struct
systems.Add<MyJobSystem>();
systems.Add<MyJobSystem>(Threads.Parallel);

// ISystem struct
systems.Add<MyStructSystem>();

// ISystem class (managed)
systems.Add<MyClassSystem>();

// ISystemsGroup
systems.AddGroup(new MySystemsGroup());

// Batch with tuples
systems.AddSystems(SystemPath.Update,
    Method1,                              // default Parallel
    (Method2, Threads.Main),
    (Method3, Threads.MainRun));

// Built-in defaults (entity destroy, clear events)
systems.AddDefaults();
```

### Important: ECB is Deferred

```csharp
// Component changes via Add/Remove/Destroy are DEFERRED (queued in ECB)
var e = world.Entity();
e.Add(new Health { Value = 10 });

e.Has<Health>();        // FALSE — not visible yet
query.Count;            // 0 — query doesn't match yet

world.Update();         // <-- ECB playback happens here

e.Has<Health>();        // TRUE
query.Count;            // 1
```

## 17. Practical Lessons Learned

### Thread Modes — What Works Where

Entity API (`Get`, `Set`, `Add`, `Remove`, `Has`, `Destroy`, `DestroyNow`) **works in ALL thread modes** including `Threads.Parallel` and Burst-compiled systems. Only **Unity managed API** (`GameObject`, `Camera`, `Debug.Log`, `UnityEngine.Object.Destroy`, `UnityEngine.Transform`) requires `Threads.Main`.

| Mode | Entity API | Unity API | Burst |
|------|-----------|-----------|-------|
| `Threads.Main` | Yes | Yes | No |
| `Threads.MainRun` | Yes | **No** | Yes |
| `Threads.Parallel` | Yes | **No** | Yes |
| `Threads.Single` | Yes | **No** | Yes |

### Multiple Queries Per System

A `[System]` method can accept **multiple `Query<>` parameters**. This is useful for cross-query lookups (e.g., collision: projectiles vs enemies).

```csharp
[System]
public static void CollisionSystem(
    ref Query<Entity, Transform, Projectile, With<ProjectileTag>> projectiles,
    ref Query<Entity, Transform, Health, With<EnemyTag>> enemies,
    ref State state,
    ref Events<DamageEvent> damageEvents)
```

### Immediate vs Deferred — Critical Distinction

| Operation | Timing | Visible same frame? |
|-----------|--------|-------------------|
| `entity.Get<T>()` / `entity.Set()` | Immediate | Yes |
| `Events<T>.Add()` / `.Clear()` | Immediate | Yes |
| `Res<T>.Ref` | Immediate (static) | Yes |
| `entity.Add<T>()` | **Deferred** (ECB) | **No** — next frame |
| `entity.Remove<T>()` | **Deferred** (ECB) | **No** — next frame |
| `entity.Destroy()` | **Deferred** (ECB) | **No** — next frame |

**Pattern for same-frame death events**: When an entity dies, use sentinel values instead of relying on deferred `DeadTag`:

```csharp
// DON'T: relies on deferred DeadTag being visible same frame
if (hp.Current <= 0)
    target.Add(new DeadTag()); // deferred — won't be visible until next frame

// DO: use sentinel + immediate event
if (hp.Current <= 0)
{
    deathEvents.Add(new DeathEvent { XPReward = xp }); // immediate
    hp.Current = -999f; // sentinel — visible immediately
    target.Add(new DeadTag()); // for next-frame cleanup
}
```

### Events — Must Clear Manually

`Events<T>` are **immediate buffers** that persist across frames until explicitly cleared. The consuming system MUST call `.Clear()`. Otherwise events accumulate and are re-processed every frame.

```csharp
[System]
public static void XPSystem(
    ref Events<DeathEvent> deathEvents,
    ...)
{
    foreach (ref var ev in deathEvents) { /* process */ }
    deathEvents.Clear(); // REQUIRED — or events accumulate forever
}
```

Guard pattern for systems that reset state — check `Count` before resetting:

```csharp
// DON'T: unconditionally resets every frame
upgradeState.Ref.SelectionPending = false;
gameState.Ref.Value = GameStateType.Playing;

// DO: only reset when events are present
if (upgradeEvents.Count == 0) return;
// ... process events ...
upgradeState.Ref.SelectionPending = false;
```

### Source Generator Pitfalls

- `[BurstCompile]` on a class + `using UnityEngine;` + any managed API call = silent compilation failure. The source generator won't produce runners, and ALL systems in that class disappear with a cryptic "does not contain a definition" error.
- **Fix**: Remove `[BurstCompile]` from the class, or remove all managed API usage.
- C# version is 9.0 — no struct field initializers, no parameterless struct constructors.

### Resources — Managed vs Unmanaged

- `Res<T>` where `T : struct, IRes` — unmanaged, static storage. Access via `new Res<T>().Ref` works anywhere.
- `ResManaged<T>` where `T : class, IRes` — for resources containing managed types (arrays, GameObjects, etc.). Registered via `world.AddResManaged()`.
- Resources with managed types (arrays, lists) **must** be classes registered with `AddManaged`.
- `Res<T>` / `ResManaged<T>` should only be accessed inside `[System]` methods or from `World` context. Don't access from arbitrary MonoBehaviours — use static fields on the system class instead.

### Query Caching

`world.Query().With<T>()` returns a `Query` struct that is internally cached by component mask. Store the query once in `OnCreate` / `Start` and reuse:

```csharp
private Query enemies;

void Start()
{
    enemies = world.Query().With<Transform>().With<Health>().With<EnemyTag>();
}
```

Creating the same query repeatedly each frame is wasteful. The `world.Query().With<T>()` chain registers the query in the world's internal storage by component mask, so repeated calls with the same mask return the same registered query — but still incurs lookup overhead.

### ECS → MonoBehaviour Communication

To pass data from ECS systems to Unity MonoBehaviour components (UI rendering, etc.), use **static fields on the system class**:

```csharp
public class HealthBarSystems
{
    public static int Count;
    public static readonly HealthBarEntry[] Entries = new HealthBarEntry[64];

    [System]
    public static void CollectHealthBars(ref Query<...> query)
    {
        Count = 0;
        // fill Entries...
    }
}

// MonoBehaviour reads static data
public class HealthBarRenderer : MonoBehaviour
{
    void OnGUI()
    {
        for (int i = 0; i < HealthBarSystems.Count; i++) { /* draw */ }
    }
}
```

### WorldInstaller Update Order

In `WorldInstaller.Update()`, call `Systems.OnUpdate()` **before** reading world data for UI:

```csharp
void Update()
{
    Systems.OnUpdate(Time.deltaTime, Time.time); // systems run first
    ReadWorldData(); // then read updated state
    UpdateUI();      // display current data
}
```

### Pausing Gameplay

To pause, gate `Systems.OnUpdate()` on game state:

```csharp
void Update()
{
    if (new Res<GameState>().Ref.Value == GameStateType.Playing)
        Systems.OnUpdate(Time.deltaTime, Time.time);
}
```

### Unity UI Requirements

For clickable UI buttons (Canvas-based), the scene needs:
1. `EventSystem` component
2. `InputSystemUIInputModule` (if using Unity Input System)
3. `GraphicRaycaster` on the Canvas

Without these, buttons render but don't respond to clicks.

### Generated Batch Loops - Performance Contract (SrcGen.BatchCodeGen)

Benchmarked 100k entities / 4 float3 components, Threads.Main (Mono, no Burst):

| Shape | Avg |
|---|---|
| Hand-written dense pointer walk | ~1.63 ms |
| Generated batch loop (current) | ~1.63 ms |
| Old generated loop (indexed + per-row branch + guards) | ~1.80 ms |
| `foreach` over `query.iter()` (protocol tax ceiling) | ~2.36 ms |

Rules the generator must keep:

1. **Dense path = sequential pointer walk with `->` deref bodies.** Never emit
   per-row indexed access `_pN[_row]` on the hot path and never emit a
   `_rowsPtr != null ? rows[_i] : _i` branch inside the entity loop - split
   dense/sparse into separate loops and hoist the check.
2. **No dead temporaries across hot loops.** Emitting `_liN =
   GetComponentLocalIndex(...)` as a statement keeps 4 dead values alive for
   Mono's JIT: registers spill to stack and the loop loses ~5% (0.08 ms /
   100k). Inline the call into the offset expression instead.
3. **Path selection is split between compile time and runtime:**
   - Pool component in query args -> batch not generated at all (compile-time
     demotion via IPoolComponent check; falls back to plain foreach).
   - Tag component in query args -> archetype loop chosen at compile time.
     Tags are filters only: pin pointer to `TagSlotStub<T>.GetPtr()` once,
     never advance or index it.
   - All inline -> storage pointer-walk guarded by runtime degradation check.
4. **The runtime degradation check is NOT removable at compile time.** Worlds
   carry implicit default none-types (`World.DefaultNoneTypes` = IsPrefab,
   DestroyEntity) injected into every query. A storage degrades when any of
   its logical archetypes holding those bits is non-empty - depends on live
   world state. Degraded -> fall back to the archetype loop (exact for any
   filters). Skipping this check silently iterates zero entities.
5. **Keep walker paths in separate small methods** (`BatchStorageWalk`,
   `BatchArchetypeWalk`) rather than one giant `OnUpdateBatched`. Forward all
   non-query params (`Events<>`, `Res<>`, ...) into walkers - user bodies may
   reference them. Do NOT add `[MethodImpl(AggressiveInlining)]` on walkers:
   same average (1.63) but unstable tail - occasional +0.07 ms frames when JIT
   context lands the combined body on a spilled register variant. Split
   methods give deterministic per-frame timing (StdDev 0.01 vs 0.02+).
6. **Mono gotcha:** `ref`-returning properties over own struct fields compile
   in VS-Roslyn but fail CS8170 under Unity's Mono-Roslyn. Value-returning
   `Current` costs nothing extra here because foreach materializes by-value
   locals anyway (~0.5 ms / 100k for MoveNext+Current+Deconstruct protocol -
   the fundamental gap between `foreach (var x in q.iter())` and batched
   loops under Mono).

### Debugging Source-Gen Perf Regressions

1. Set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` +
   `<CompilerGeneratedFilesOutputPath>...</...>` in the consuming csproj to
   inspect emitted code. Note: Unity regenerates csproj files and wipes the
   patch.
2. MSBuild single-line chains (`cmd & echo %ERRORLEVEL%`) report stale exit
   codes - capture exit codes per command or read the log for `error CS`.
3. When a perf diff appears between two builds, hand-write a "twin" test that
   mirrors the generated structure verbatim (plain `[System]`, no batching).
   Twin == fast means generator emits something different; twin == slow means
   the structure itself is the cost.

## 18. Arena Guard — corruption/leak detection (runtime-toggleable)

Arena lives on `Allocator.Persistent` = the SAME Unity DynamicHeapAllocator as editor
blocks — any ECS OOB write corrupts unrelated editor memory and crashes the editor
MINUTES LATER (delayed symptom, victim e.g. TextCore). Arena Guard catches it at the
source. Born from the 2026-08-25 crash-hunt (phantom types via CopyUnion OOB).

- **Flags**: `AllocatorDebugState.Mode` — `SharedStatic<AllocatorDebugMode>`
  (`Canary | PoisonFree | TrackTags`). SharedStatic is MANDATORY: Burst jobs
  (ECB playback) read it; a plain static silently breaks Burst of every touching job.
  Default `None` → cost is one predictable branch in Alloc/Dealloc.
- **Tags (always on, free)**: allocation tag stored in the unused `NextFree` header
  field of live blocks (bit 32 = has-guard marker). `_allocate_ptr<T>(items, tag)` /
  `Allocate(size, tag)`. Key sources tagged (`AllocatorTags.*`); rest = `Untagged`.
  `GetTagStats` → per-tag live count/bytes → runaway-growth leaks visible in UI.
- **Canary**: +16 guard bytes at the end of the aligned slot (flag on at alloc time
  only). Detects writes past allocation (overflows smaller than A16 padding slip).
- **PoisonFree**: freed user area's first 16 bytes = `0xDD`; `Validate` flags writes
  into freed blocks (UAF). When toggling ON mid-session call `PoisonAllFree()`
  (the UI toggle does this; FastDeserialize normalizes too).
- **`Validate(out Violation)`**: walks block chains — header sanity (A16 size, chain
  within cursor), canaries, poison. Burst-safe (no managed); report via
  `[BurstDiscard]` `AllocatorDebugState.Report`.
- **Auto-runs (always, cold paths)**: `WorldUnsafe.Free()` ("world N dispose") and
  end of `FastDeserialize` ("load"). Corruption → clear LogError at the boundary
  instead of a delayed editor crash / NRE storm.
- **UI**: `Nuke.cs/Allocator Debug` window (AllocatorEditor.asmdef, always compiled):
  flag toggles, "Validate now", auto-interval, per-tag card.
- **Tests**: `UnitTests/AllocatorDebugTests.cs` (canary OOB, poison UAF, clean churn,
  tag stats, PoisonAllFree normalization).
