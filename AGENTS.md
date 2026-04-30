# Nukecs ECS Framework — Agent Reference

## 1. Project Overview

Nukecs is a **Burst-compiled ECS framework for Unity**. It uses `unsafe` code and raw pointers throughout for maximum performance. A source generator at `../../SourseGen/NUKECSGEN/` generates system runners and component type registrations.

- **Runtime**: .NET Framework 4.7.1 (Unity legacy)
- **Dependencies**: Unity.Burst, Unity.Collections, Unity.Jobs, Unity.Mathematics
- **Assembly defs**: `Nukecs.asmdef` (runtime), `Nukecs.Tests.asmdef` (tests)
- **`[BurstCompile]`** used on hot paths

## 2. Architecture

```
World → Archetype[] → Entity (int ID)
     → Queries[]     → QueryEnumerator / Query<T1..TN, TOption>
     → Systems        → OnUpdate() dispatch
     → ECB            → deferred add/remove/destroy
```

- **World** — central container: entity storage, archetype management, pools, queries (`src/World/World.cs` safe wrapper, `src/World/World.Unsafe.cs` core)
- **Archetype** — stores entities with same component mask; SoA data layout (`src/Archetype.cs`)
- **Entity** — lightweight ID (`int`); location stored in `World.entityLocations` (archetypeIndex + row)
- **Query** — matches entities by component mask; iterates via `QueryEnumerator` or generic `Query<T1..TN, TOption>` (`src/Query.cs`, `src/Systems/FnSystems/Query.cs`)
- **Systems** — functions with `[System]` attribute; source-gen creates runners; 3 thread modes: Main, Single, Parallel (`src/Systems/Systems.cs`)
- **EntityCommandBuffer (ECB)** — deferred add/remove/destroy; flushed on `world.Update()` (`src/Entity/EntityCommandBuffer.cs`)
- **Component Storage** — two modes: inline (packed in archetype data array) and Pool (separate `GenericPool<T>` with SparseSet) (`src/Components/GenericPool.cs`)
- **Allocator** — custom `MemAllocator` with `ptr<T>` wrapper, `MemoryArray<T>`, `MemoryList<T>` (`src/Allocator/`)

## 3. Key Files Map

| File | Description |
|------|-------------|
| `src/Archetype.cs` | `ArchetypeUnsafe`: entity CRUD, batch ops, archetype edges/transitions, SoA data layout |
| `src/World/World.Unsafe.cs` | `WorldUnsafe`: entity create/destroy, archetype registry, query management, batch ops |
| `src/World/World.cs` | Safe `World` wrapper |
| `src/World/World.Entities.cs` | Entity creation, Get/Set/Add/Remove component helpers |
| `src/World/World.Components.cs` | Pool management, typed component access |
| `src/Query.cs` | `QueryUnsafe`: archetype matching, entity tracking, `QueryEnumerator` |
| `src/Systems/FnSystems/Query.cs` | Generic query iterators `Query<T1..T5, TOption>` with `WithEntity` variants |
| `src/Systems/FnSystems/QueryGeneric.cs` | Generic query job system runners |
| `src/Systems/Systems.cs` | `Systems` container, `OnUpdate` dispatch pipeline |
| `src/Entity/Entity.cs` | `Entity` struct + extension methods (Get, Set, Add, Remove, Destroy, Copy) |
| `src/Entity/EntityCommandBuffer.cs` | Deferred operations buffer |
| `src/Components/ComponentTypeMap.cs` | Static type → index registry |
| `src/Components/ComponentTypeData.cs` | `ComponentTypeData` struct (size, storageType, etc.) |
| `src/Components/GenericPool.cs` | `GenericPool` for Pool-stored components (SparseSet-based) |
| `src/Collections/MemoryArray.cs` | `MemoryArray<T>` — unmanaged resizable array via allocator |
| `src/Collections/MemoryList.cs` | `MemoryList<T>` — unmanaged list via allocator |
| `src/Collections/DynamicBitmask.cs` | Bitmask for archetype component matching |
| `src/Allocator/Allocator.cs` | `MemAllocator` — arena allocator |
| `src/Allocator/ptr.cs` | `ptr<T>` — safe pointer wrapper |
| `src/World/World.Allocation.cs` | World memory allocation helpers |

## 4. Data Layout & Conventions

- **Archetype data**: SoA layout — `componentOffsets[i]` = byte offset to start of component `i`'s row array; each row = `componentSize * capacity` apart
- **Entity location**: `World.entityLocations[entityID] = { archetypeIndex, row }`
- **Packed entities**: `ArchetypeUnsafe.packedEntities[row] = entityID`
- **Query iteration**: `QueryEnumerator` walks `matchingArchetypes[]` → archetype → `packedEntities[row]`
- **Component access**: `data.Ptr + componentOffset + row * componentSize`
- **Two component storage types**: `StorageType.Default` (inline in archetype) and `StorageType.Pool` (separate pool)
- **`TOption` in queries**: can be `None<T>`, `Any<T>`, `With<T>`, or a regular component

## 5. Known Bugs / Pitfalls (resolved)

- `BatchCreateEntity` must call `EnsureCapacity`, fill `packedEntities`, set `entityLocations.row`, memclear component data, increment `count`
- `TOptIsComponent` static field in `Query<T1, TOption>` was stale — use `QueryParamInfo<TOption>.IsComponent` instead
- `QueryEnumerator` needs `_lastArch < 0` guard on first `MoveNext` to avoid null deref
- `SetupTN` methods need `li < 0` guard before `.SetArchetype()` calls
- `MoveNext` in generic queries needs `_archIdx >= matchingArchetypes.length` bounds check
- `Update()` in all `Query<T1..TN, TOption>` and `.WithEntity` variants: the archetype row loop must use the snapshot of `count` taken *before* iteration (not re-read from archetype each tick), otherwise entities added during iteration cause OOB access

## 6. Testing

- Unity Edit mode tests in `UnitTests/`
- Key test files: `SystemChainTests.cs`, `WorldTests.cs`, `AdvancedTests.cs`
- Tests run via Unity Editor (not `dotnet test`) — results in `UnitTests/TestResults_*.xml`
- SystemChainTests: 14 tests covering system chains (3+ systems in sequence), thread modes, batch creation

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

## 8. Unity-Specific Notes

- Uses `[BurstCompile]` on hot paths — avoid managed allocations in Burst-compiled code
- `MemAllocator` is a custom arena allocator, not Unity's `Allocator`
- Jobs use `IJobParallelFor` and `IJob` from Unity.Jobs
- `UnityAllocatorHandler.cs` / `UnityAllocatorWrapper.cs` bridge to Unity's allocator for specific use cases
- `World.SerializeAndSave.cs` handles world serialization
- `World.Aspects.cs` provides aspect (group-of-components) support
- `EntityFilterBuffer.cs` and `QueryFilter.cs` handle entity filtering
