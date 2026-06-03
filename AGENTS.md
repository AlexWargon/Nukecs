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
| `src/Systems/FnSystems/Query.cs` | Generic query iterators `Query<T1..T5, TOption>` with `WithEntity` variants |
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
| `src/Allocator/Allocator.cs` | `MemAllocator` — arena allocator |
| `src/Allocator/ptr.cs` | `ptr<T>` — safe pointer wrapper |
| `src/Allocator/Serialization.cs` | Allocator serialization support |
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
