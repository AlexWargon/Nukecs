# Nukecs ECS Framework — Agent Reference

When writing gameplay code with this framework, follow [NUKECS_AGENTS_GUIDE_EN.md](NUKECS_AGENTS_GUIDE_EN.md) (English) or [NUKECS_AGENTS_GUIDE_RU.md](NUKECS_AGENTS_GUIDE_RU.md) (Russian): method-system registration, Init/Update/OnDestroy lifecycle, Burst/Parallel, event patterns and component memory rules.

Current API reference updated from source on 2026-10-03. Start with
[README.md](README.md) for usage, [ARCHITECTURE.md](ARCHITECTURE.md) for storage
invariants, and the [runtime iterator contract](src/Systems/FnSystems/RuntimeQuery/README.md)
for explicit `iter()` / `par_iter()`. Handoff documents contain historical APIs.

## 1. Project Overview

Nukecs is a **Burst-compiled ECS framework for Unity**. It uses `unsafe` code and raw pointers throughout for maximum performance. The checked-in analyzer is `SourceGen/NUKECSGEN.dll`; in this checkout its source project is at `../../../NUKECSGEN/`. It generates system runners and component type registrations.

- **Runtime**: .NET Framework 4.7.1 (Unity legacy)
- **Dependencies**: Unity.Burst, Unity.Collections, Unity.Jobs, Unity.Mathematics
- **Assembly defs**: `Nukecs.asmdef` (engine-independent runtime core), `Nukecs.Unity.asmdef` (`src/Unity/`: Unity integration, transforms, bakers, debuggers, hot reload), `Nukecs.Tests.asmdef` (tests), `AllocatorEditor.asmdef` (debug). Assemblies using Unity-side types reference both.
- **Core boundary**: nothing outside `src/Unity/` may use the `UnityEngine`/`UnityEditor` namespaces. Log through `dbug`, take host events from `NukecsLifecycle`, and put Unity code in `src/Unity/` (core internals are visible to `Nukecs.Unity`). Types moved from core to `Nukecs.Unity` that can be `[SerializeReference]`-serialized need `[MovedFrom(false, sourceAssembly: "Nukecs")]`. Unity.Collections/Jobs/Burst/Profiling and `AOT` still come from UnityEngine.CoreModule; they are later steps of the engine-independence work.
- **`[BurstCompile]`** used on hot paths

## 2. Architecture

```
World → Archetype[] → Entity (8-byte generational handle)
     → StorageArchetype[] → shared SoA columns (same inline mask)
     → Queries[]     → QueryEnumerator / Query<T1..TN, TOption> / Chunk<T1..T8>
     → Systems        → OnUpdate() dispatch (onStart/onUpdate/onFixedUpdate/onDestroy)
     → ECB            → deferred add/remove/destroy
     → Events<T>      → thread-safe event buffers (spinlock-protected)
     → Res<T>         → singleton resources (unmanaged + managed)
     → Reactivity     → byte snapshots, Burst check job, managed subscriptions
     → DependencyGraph → optional metadata-based system scheduling
     → HotReload      → Roslyn-based runtime system recompilation
```

- **World** — central container: entity storage, archetype management, pools, queries (`src/World/World.cs` safe wrapper, `src/World/World.Unsafe.cs` core). Static management via `World.Static.cs` (`SharedStatic` world list, `ALLOCATOR` struct). Disposal in `World.Free.cs`.
- **Archetype** — logical identity (`inlineMask + tagMask + poolMask`) and storage-row membership (`src/Archetype.cs`). `StorageArchetype` owns shared SoA data (`src/StorageArchetype.cs`); tag/pool changes do not copy inline columns.
- **Entity** — generational handle (8 bytes: `int id`, `ushort Generation`, `ushort WorldToken`); location stored in `World.entityLocations` (archetypeIndex + row)
- **Query** — matches logical masks, with optional dense storage traversal. Explicit `iter()` / `par_iter()` use `QueryRuntimeIter1..9` for 1–8 data components; plain foreach retains the generated batch path when eligible (`src/Systems/FnSystems/RuntimeQuery/`).
- **Systems** — functions with `[System]` attribute; source-gen creates runners; 4 thread modes: Main, MainRun, Parallel, Single (`src/Systems/Systems.cs`). Lifecycle lists: `onStart`, `onUpdate`, `onFixedUpdate`, `onDestroy`. `ISystemRunner` interface for struct/class systems.
- **EntityCommandBuffer (ECB)** — deferred add/remove/destroy; flushed on `world.Update()` (`src/Entity/EntityCommandBuffer.cs`)
- **Component Storage** — two modes: inline (packed in archetype data array) and Pool (separate `GenericPool<T>` with SparseSet) (`src/Components/GenericPool.cs`)
- **Allocator** — custom `MemAllocator` with `ptr<T>` wrapper, `MemoryArray<T>`, `MemoryList<T>` (`src/Allocator/`)
- **ISystemParam** — unified interface for system parameters: Query, Res, State, Events, Local, Chunk. Source generator recognizes these and wires them up.
- **Events** — `Events<TEvent>` thread-safe event buffer; `AddPar` for parallel writes (spinlock); `EventsParallelReader<TEvent>` for parallel reads; `EventsStorage` central registry (`src/Systems/FnSystems/Events.cs`)
- **Resources** — `Res<T>` / `ResManaged<T>` singleton resource accessors; `IRes` interface with `OnCreate`/`OnUpdate`; `ResStorage` unmanaged storage (`src/Systems/FnSystems/Res.cs`, `ResManaged.cs`, `ResStorage.cs`)
- **Chunk Iteration** — `Chunk<T1..T8>` archetype chunk iterators; `IChunk` interface; direct pointer iteration over archetype component arrays (`src/Systems/FnSystems/Chunk.cs`)
- **Reactivity** — `OnChange<T>` / `OffChange<T>` subscriptions, snapshots and main-thread dispatch; `Reactivity.Changed<T>` is a generated-query filter (`src/Reactivity/`). The historical companion/tag implementation was removed.
- **Hot Reload** — `HotReloadSystems` wraps `Systems`; file watching + Roslyn compilation + runner swapping at runtime (`src/Systems/HotReload/`, `src/Unity/Editor/HotReload/`)
- **DynamicBuffer** — `DynamicBuffer<T>` Unity-style dynamic buffer component (`src/Components/DynamicBuffer.cs`)
- **IEntityJobSystem** — per-entity job system interface; `EntityJobSystemRunner<T>` dispatches (`src/Systems/EntityJobSystem.cs`)

## 3. Key Files Map

### Core

| File | Description |
|------|-------------|
| `src/Archetype.cs` | `ArchetypeUnsafe`: entity CRUD, batch ops, archetype edges/transitions, SoA data layout |
| `src/StorageArchetype.cs` | Shared SoA owner, inline mask, column offsets, packed entities and logical-archetype backlinks |
| `src/World/World.Unsafe.cs` | `WorldUnsafe`: entity create/destroy, archetype registry, query management, batch ops |
| `src/World/World.cs` | Safe `World` wrapper |
| `src/World/World.Aspects.cs` | Aspect definitions and operations (old World.Entities.cs / World.Components.cs files removed) |
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
| `src/Components/Component.cs` | Core interfaces IComponent/IArrayComponent/IPoolComponent, Name and built-in tags/hierarchy components; Changed<T> is now in Reactivity/ReactDelegate.cs |
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
| `src/Systems/ECBJob.cs` | ECB processing job |
| `src/Systems/StartFixedECBSystem.cs` | Start/fixed-update ECB processing |
| `src/Systems/JobSystem.cs` | Job system base |
| `src/Systems/IQueryJobSystem.cs` | Query-based job system interface |
| `src/BuiltInSystems.cs` | Built-in system registrations |
| `src/Systems/DependencyGraph/` | Access metadata, conflict detection, system nodes and execution groups |

### Systems / FnSystems

| File | Description |
|------|-------------|
| `src/Systems/FnSystems/Query.cs` | Nine generic query families, runtime iter/par_iter factories, init/update and Entity-first queries |
| `src/Systems/FnSystems/RuntimeQuery/` | QueryRuntimeIter1..9, QueryRuntimeRefs tuples, pool column/page cache and Entity-value deconstruction extensions |
| `src/Systems/FnSystems/QueryGeneric.cs` | Generic query job system runners |
| `src/Systems/FnSystems/QueryIterators.cs` | Query iterator implementations |
| `src/Systems/FnSystems/QueryIteratorsParallel.cs` | Parallel query iterator implementations |
| `src/Systems/FnSystems/Chunk.cs` | `Chunk<T1..T8>` archetype chunk iterators + `IChunk` interface |
| `src/Systems/FnSystems/Events.cs` | `Events<TEvent>` system param, `EventsParallelReader<TEvent>`, `EventsStorage` |
| `src/Systems/FnSystems/Res.cs` | `Res<TRes>` (resource param), `TimeRes` |
| `src/Systems/FnSystems/ResManaged.cs` | `ResManaged<TRes>` for class-type resources |
| `src/Systems/FnSystems/ResStorage.cs` | `ResStorage` unmanaged resource storage |
| `src/Systems/FnSystems/Local.cs` | `Local<TData>`, `IRes`, `IResourceGetSet`, `Data<T>` |
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
| `src/Unity/HotReload/HotReloadSystems.cs` | `HotReloadSystems` class: wraps `Systems`, tracks source files, swaps runners on recompile |

### Reactive

Current API is in `Wargon.Nukecs.Reactivity`; the older files below are historical.

| File | Description |
|------|-------------|
| `src/Reactivity/EntityReactiveExtensions.cs` | OnChange/OffChange, optional predicate and immediate trigger |
| `src/Reactivity/SystemsReactiveExtensions.cs` | Auto-registration and explicit AddReactive<T> |
| `src/Reactivity/ReactiveCheckJob.cs` | Burst change detection against snapshots |
| `src/Reactivity/ReactDispatchSystem.cs` | Main-thread callback dispatch and subscription cleanup |
| `src/Reactivity/ReactDelegate.cs` | ReactDelegate, ReactFilter and Changed<T> query filter |
| `src/Reactivity/ReactiveStorage.cs` | Per-world/type subscription and snapshot storage |

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
| `src/dbug.cs` | Logging facade over a replaceable `INukecsLogger` (console by default; Nukecs.Unity installs the `UnityEngine.Debug` one) |
| `src/NukecsLifecycle.cs` | Host signals for the core: `Quitting` (forwarded to the host's event once connected) and `StaticDisposed` |
| `src/ObjectRef.cs` | `ObjectRef<T>` managed-object handle and its static storage |
| `src/NukecsDebugData.cs` | Debug settings; the ScriptableObject wrapper is `src/Unity/NukecsDebugDataSO.cs` |
| `src/NUnsafe.cs` | Additional unsafe utilities |
| `src/Singleton.cs` | `Singleton<T>` (Burst-compatible; SharedStatic holds only pointer+size, value in a Malloc block, reset before assembly reload) and `SingletonRegistry` |
| `src/SparseSet.cs` | Sparse set data structure |
| `src/SystemsGroup.cs` | `SystemsGroup` — named group of system runners |
| `src/rng.cs` | Random number generation utilities |
| `src/EntityFilterBuffer.cs` | Entity filtering buffer |
| `src/QueryFilter.cs` | Query filtering logic |
| `src/Usings.cs` | Global using directives |

### Unity Integration

Everything under `src/Unity/` compiles into `Nukecs.Unity.asmdef` (editor tools there stay
behind `#if UNITY_EDITOR`, except `Editor/Allocator/` with its own asmdef).

| File | Description |
|------|-------------|
| `src/Unity/NukecsUnityHost.cs` | Installs the Unity logger, `Application.quitting` source and `EntityPrefabMap` cleanup into the core |
| `src/Unity/UnityObjectRef.cs` | `UnityObjectRef<T>` instance-id references for baking |
| `src/Unity/Convertor.cs` | `Convertor` ScriptableObject base for `ICustomConvertor` |
| `src/Unity/StaticAllocations.cs` | Editor play-mode exit cleanup list |
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

- **Storage data**: SoA layout — `StorageArchetype.componentOffsets[i]` is the column base offset; consecutive rows are `componentSize` bytes apart, each column reserves `componentSize * capacity` bytes.
- **Entity location**: `World.entityLocations[entityID] = { archetypeIndex, row, listPos }`; row is physical storage index, listPos is the position in the logical archetype's rows list.
- **Packed entities**: `StorageArchetype.packedEntities[row] = entityID`; ArchetypeUnsafe accessors forward to storage.
- **Query iteration**: dense storage traversal or matching logical archetypes with gather via `rows[listPos]`; `LA.count == rows.length`, storage.count includes all its logical archetypes.
- **Component access**: `data.Ptr + componentOffset + row * componentSize`
- **Storage types/categories**: `StorageType.Archetype` / `StorageType.Pool`; `ComponentCategory.Inline` / `Tag` / `Pool`. Tags have no payload, only a mask bit.
- **`TOption` in queries**: `None<T>`, `With<T>`, `Any<T1..T5>`, a regular component, a supported `IFilter`, or a tuple of filters (`(With<C>, Any<D, E>, None<F>)`). Filters are always the last type parameter. `Reactivity.Changed<T>` needs the generated batch path, not explicit runtime iter/par_iter.
- **`Any` group**: `QueryUnsafe.any` mask; every `Any<>`/`Any(int)` of one query joins ONE group (at least one bit present). Matching: `Archetype.CheckQuery`/`PopulateQueries` call `AnySatisfiedBy(types)` after the none check, including zero-with queries. Pair edges, destroy edges and serialization follow the attached-query lists, so they need no Any logic. Storage mode: an inline Any bit in `inlineMask` satisfies the whole storage; tag/pool Any bits are checked per non-empty logical archetype (`AnyStateOfLogicalArchetypes`): all/none satisfied → dense, mixed → `storageDegraded = 1`. Any types are never iterated and never recorded as accesses. Generator errors: `NUKECS020` (Any∩None), `NUKECS021` (Any∩required).
- **Component type index**: `ComponentType<T>.Index` — per-type `SharedStatic<ComponentTypeData>` with lazy registration
- **Alive entity set**: `AliveEntitiesSet` (SparseSet-based) tracks living entity IDs
- **Events range**: `Events<TEvent>.Range` set per-thread for parallel iteration; `RangeEnumerator` walks `start..end`

## 5. Known Bugs / Pitfalls (resolved)

- `BatchCreateEntity` must call `EnsureCapacity`, fill `packedEntities`, set `entityLocations.row`, memclear component data, increment `count`
- `TOptIsComponent` static field in `Query<T1, TOption>` was stale — use `QueryParamInfo<TOption>.IsComponent` instead
- `QueryEnumerator` needs `_lastArch < 0` guard on first `MoveNext` to avoid null deref
- `SetupTN` methods need `li < 0` guard before `.SetArchetype()` calls
- `MoveNext` in generic queries needs `_archIdx >= matchingArchetypes.length` bounds check
- Iterators capture block count before visiting rows; never re-read it as the loop bound while appending entities. Structural mutations can still invalidate pointers.
- Entity access uses `Query<Entity, ...>`; the old nested `.WithEntity` signature is no longer in Query.cs. Explicit runtime deconstruction yields Entity by value.

Fixed in the 1.0 stabilization pass (2026-10-02, regression tests in `UnitTests/ReleaseStabilizationTests.cs`):
- Typed `Query<..., DestroyEntity>` never matched — `DestroyEntity` sat in BOTH with and default-none masks. Fix: explicit `With` overrides a default none of the same type (`QueryUnsafe.With`).
- Fluent queries created/mutated after entities existed stayed attached to nothing (silent zero results). Fix: lazy archetype rescan (`archetypeMasksDirty` + `EnsureArchetypesMatched`), dup-attach guard in `CheckQuery`.
- Zero-with queries (`world.Query()`) matched nothing. Fix: they match every archetype minus none bits.
- Immediate destruction is implemented by Entity.DestroyNow, including row removal, pool disposal and generation-aware ID recycling. Deferred Destroy uses ECB playback without a destruction system; reserved IDs and stale commands are handled explicitly.
- Disposable components leaked when their column was dropped by a Remove migration. Fix: `MoveEntityTo` disposes dropped disposable columns (surviving columns are NOT disposed — the destination row still references them). `ECB RemoveAndDispose` therefore works.
- Removing a pool component left the pool slot behind. Fix: ECB playback clears pool slots for pool-storage removals.
- `Chunk<T>`/`Chunk<T1,T2>` CopyTo memcpy'd on shared (sparse) storage; arities 4–8 never bound `_rows` (iteration AND CopyTo read foreign rows); arity-8 CopyTo had a duplicated T6 branch and no T8 branch. All fixed; `iter_chunk2` (raw block) is `[Obsolete]`.
- Static `World.Load`/`LoadAsync` NRE'd in `FixManagedWorld` (slot not published before the read-back).
- Corrupt saves corrupted the heap: `FastDeserialize` trusted `savedRegionCount`. Fix: save header (magic + int version + regionCount) validated before any allocation; `NukEcs.version` is `const int`.
- `Compress/CompressAsync` wrote the wrong byte length; `SaveToFile` didn't truncate; loads ignored short reads; `serializedAllocator` static buffer raced between worlds. All fixed.
- `World.Create` could overwrite a live world (naive `lastFreeSlot++`); the 9th world wrote past the worlds array. Fix: aliveness-checked slot acquisition + clean error at `MAX_WORLD_COUNT`.
- `res_type<T>.index` was derived from the per-world list length → cross-world type-confused resource reads. Fix (2026-10-07): resources are keyed by stable type hash; session-order slot ids broke loading a save from another session once values moved into the world.
- `Entity ==/Equals` ignored the world while `GetHashCode` included it. Fix: equality and hashing include `WorldToken` (slot + incarnation) and `Generation`; hashes remain stable after removal and relocation.
- Async `World.LoadAsync` returns `Task<World>`: assign `world = await World.LoadAsync(path, world)` because loading can relocate the arena. A forced-relocation regression pins the former stale-pointer/ECB-disposal failure.
- Reactive check/dispatch systems retain a world ID and resolve `World.Get(id)` instead of caching an arena pointer or World copy across loads. Deserialization also refreshes the active `Systems.State.World` (load can occur inside a main-thread update), and struct runner deserialization callbacks are unboxed back into the stored system. `ReactiveLoadRegressionTests` covers relocated loads inside updates with all four graph modes and without a graph.
- Sparse chunk `CopyTo` computes offsets from the current row; `MoveNext` stops before reading rows[count]. All three previously ignored chunk regressions are enabled (arities 1-8 covered by ChunkSparseRegressionTests).
- Dead `src/Reactive/` duplicate deleted; `CopyViaECB` is the deferred copy API.
- Follow-up: `Entity.DestroyNow` implements immediate destruction directly in Entity.cs and handles reserved IDs without storage rows. It never scans ECB: commands capture generations and normal playback drops expired commands, releasing pending disposable payloads. Destroy edges refresh against `queriesVersion` for late queries. The destruction systems were removed: `Destroy()` is processed by ECB playback and `DestroyNow()` removes immediately. Neither needs `AddDefaults()`. The legacy `DestroyEntity` tag remains an exclusion filter, without automatic deletion. This does not change the historical low-level `ArchetypeUnsafe.Destroy` limitation above.

## 5.1 Multi-world contract (1.0)

- Up to `World.MAX_WORLD_COUNT` (8) simultaneous live worlds; exceeding it throws.
- Entity-level state (entities, archetypes, queries, events, ECB, reactive registries) is fully per world.
- `ComponentType` registry and session Local slot ids are domain-global; `Res<T>` VALUES live in each world's arena (see §10), keyed by a stable type hash, and are isolated per world.
- `Save/Load` round-trips one world's arena; save files are not portable across different component registration orders (type indices are first-touch assigned) and carry a magic + int format version header.

## 6. Testing

- Unity Edit mode tests in `UnitTests/`
- Key test files: `SystemChainTests.cs`, `WorldTests.cs`, `AdvancedTests.cs`, `EventsTests.cs`, `SerializationTests.cs`, `PrefabSpawnTests.cs`, `ResizeTests.cs`, `AllocatorTests.cs`, `AddObjectTests.cs`, `SimpleTest.cs`
- Tests run via Unity Editor (not `dotnet test`) — results in `UnitTests/TestResults_*.xml`
- SystemChainTests: 14 tests covering system chains (3+ systems in sequence), thread modes, batch creation
- EventsTests: tests for `Events<TEvent>` add/read/clear and parallel safety
- RuntimeQueryIntegrationTests / RuntimeQueryProductionRegressionTests: arities 1–8, inline/pool combinations, tags, sparse rows, page growth and snapshots.
- RuntimeQueryJobIntegrationTests / RuntimeQueryEntityDeconstructionTests: job ranges, generated runners, Entity-value deconstruction and Burst probes.
- StorageModeQueryTests / TagPoolMaskTests / AllocatorDebugTests / ReactivityTests / DependencyGraphTests cover the other recent subsystems. Test artifacts may also be stored outside UnitTests; handoffs identify historical runs. Do not infer IL2CPP support from a managed or Editor Burst pass.

## 7. Source Generator

- Analyzer: `SourceGen/NUKECSGEN.dll`. Source project in this checkout: `../../../NUKECSGEN/` (outside this package; locate it rather than assuming the old SourseGen path).
- Generates system runners (`ISystemRunner`), query system job runners, and component type registrations
- Generated output in `NUKECSGEN/` directory at solution root
- `[System]` attribute marks static methods as ECS systems; source gen creates runner classes
- The declaring class (and all enclosing types) should be `partial`: the job struct is emitted NESTED in it (`Class.__Method_Job`, file `Class_Method.Job.g.cs`) with the source file's usings and namespace declarations, so the body binds like class code (short/private helpers, constants, nested types, user namespace first). Everything the generator writes there is `global::`-qualified. Non-partial → warning `NUKECS012` and fallback job `__Class_Method_Job` in the user's namespace with `using static`. Errors: `NUKECS010` generic declaring type, `NUKECS011` private/protected declaring type, `NUKECS013` `__` name conflict. External (other-assembly) systems resolve the job via `ResolveExternalJobTypeName`.
- Runner `Name` is the stable `"Class_Method"` string (hot reload maps runners to methods with it), not the job type name.

### Generated System Structure

For each `[System]` method, the generator produces:
- `IXxxSystemJob` interface with `OnUpdate` + `OnUpdateBatched`
- `IXxxSystemJobExtensions` static class with `QuerySystemJobWrapper<TJob>` struct (Execute dispatch)
- `IXxxQuerySystemJobRunner<TJob>` runner class (Schedule/Run for Main/Single/Parallel modes)
- `Class.__Method_Job` struct implementing `IXxxSystemJob` (copies user's method body; nested in the partial class), and `Xxx_Generated` with `GetDependencyInfo()` and the `SystemActionXxx` delegate in `Wargon.Nukecs`

### OnUpdateBatched Design

`OnUpdateBatched` is **always `void`** and **always has `ref State state`** in its parameters (added automatically if the user's `OnUpdate` doesn't have it). This guarantees `state.World.UnsafeWorld` is available for the batch archetype loop path.

**Fallback is internal** — call sites (Execute Single, Runner Main) just call `OnUpdateBatched(...)` directly, no `bool` checks.

Three generated body variants:
1. **Batchable + hasQuery** — inline queries use separate storage/archetype walkers, with runtime degradation checks; Changed<T> uses its dedicated detection/iteration path. Pool queries are demoted to regular iteration during analysis.
2. **Non-batchable + hasQuery** — `Update` + `OnUpdate` (regular foreach path)
3. **No query** — `OnUpdate` only

### Key Variables in Code Generation

- `hasStateParam` — whether user's `OnUpdate` has a `State` parameter
- `onUpdateCallArgs` — args for calling `OnUpdate` from inside `OnUpdateBatched` (e.g., `ref q, ref cp`) — no `state` prefix, no `fullData.` prefix
- `onUpdateBatchedParams` — method signature params for `OnUpdateBatched`; appends `, ref State state` if `!hasStateParam`
- `onUpdateBatchedCallExecute` / `onUpdateBatchedCallRunner` — call-site args that always include state

### Batch Optimization (ForEachAnalysis)

For eligible plain `foreach (var (a, b) in query)` bodies, the generator emits
direct pointer walkers with `->` dereferences and pointer increments, split into
dense storage and logical-archetype paths. Tags use stubs; pools prevent the
ordinary batch rewrite. Explicit `query.iter()` and `query.par_iter()` always
retain runtime traversal. See the performance contract in §17 before editing
walkers; the old `_p0[_i]` loop shape is not the current implementation.

The selected foreach can have surrounding code and be enclosed in unsafe
blocks or if branches. Both full and Parallel range methods replace only that
loop, preserving its envelope and forwarding ordinary captured locals by ref.
Parallel envelopes execute per work range, including an empty query's scheduled
range. Shared side effects require thread-safe operations. Multiple primary-query
loops, nested loops in the selected body, local functions and loop-local
return/break/goto/yield fall back conservatively. Captured ref locals, constants,
anonymous types, and captured names starting with `_` are unsupported. Generated
State/range parameters are named `__state`/`__range` (or the user's State parameter
name), so locals named `state`/`range` are fine. Every copy of user code carries
`#line` mapping: the contextual envelope keeps the original body layout (the
dispatch call is padded with the loop's line count) and each rewritten loop-body
statement in the walkers gets its source line, so errors and debugger steps land in
the user's file. Keep that layout when changing body emission.

Generated runners implement `ISystemCompilationInfoProvider`: `CompilationInfo`
exposes `Kind`, `FallbackReason`, and `HasSurroundingCode`. `[RequireBatch]` turns
fallback into compiler error `NUKECS002`. Metadata identifies generated
PointerBatch/ChangedBatch code; it does not prove native Burst or dense traversal.
`FallbackDetail` explains the first blocker and suggests a correction;
`FallbackFile`/`FallbackLine`/`FallbackColumn` locate it (one-based coordinates).
The RequireBatch diagnostic points at that node and carries the same detail.
Ordinary fallback exposes this metadata without automatic warning/log spam.
Regression coverage: `BatchRewriteRegressionTests` and `SourceGen/Tests~/`.

## 8. System Parameters (ISystemParam)

All system parameters implement `ISystemParam` with `Init(ref ptr<World.WorldUnsafe>)`, `Update(ref World, IntPtr)`, and `MetaType` property. The source generator auto-recognizes these types in `[System]` method signatures.

| Param | MetaType | Description |
|-------|----------|-------------|
| `State` | `State` | Execution context: World, TimeData, Dependencies |
| `Query<T1..TN, TOption>` | `Query` | Component query with foreach iteration |
| `Res<TRes>` | `Resource` | Read/write access to the world's unmanaged singleton resource |
| `ResManaged<TRes>` | `Resource` | Read/write access to managed (class) singleton resource |
| `Events<TEvent>` | `Events` | Thread-safe event buffer; `AddPar` for parallel writes |
| `Local<TData>` | `Local` | Per-system local data |
| `Chunk<T1..T8>` | — | Direct archetype chunk pointer iteration |

## 9. Events System

- `Events<TEvent>` — `ISystemParam` backed by `MemoryList<TEvent>` via `MemAllocator`
- **Thread-safe writes**: `AddPar(in TEvent)` acquires `Spinner` spinlock before list push; auto-resizes under lock
- **Parallel reads**: `ReadPar()` returns `EventsParallelReader<TEvent>` (readonly ptr + length); per-thread `Range` set by `Update(ref World, IntPtr data)` where `data` points to a `Range` struct
- **Iteration**: `RangeEnumerator` walks `Range.start..Range.end` of the event list
- **Storage**: `EventsStorage` — `HashMap<int, ptr>` keyed by type hash; `Get<TEvents>(ref ptr<World.WorldUnsafe>)` lazily creates on first access; `ClearAll()` iterates all event types

## 10. Resource System

- `Local<T> where T : unmanaged, IRes` stores one arena-backed value per system
  registration and parameter, isolated across worlds and Systems containers.
  Generated runners call OnCreate once and OnUpdate once before each invocation,
  then restore the value after Save/Load. Parallel ranges share the local; writes
  require synchronization. Use `local.Ref`. Like `Res<T>`, `Local<T>` holds only a
  `ptr<TData>` to an arena value block (blittable for Burst direct calls). Worlds store
  locals by the stable key (generator owner hash, Systems scope, registration ordinal);
  session slot ids from `LocalParamSlots` are translated to that key, so loads from
  another session work as long as the same systems are registered.

- `Res<TRes>` where `TRes : unmanaged, IRes` — holds only a `ptr<TRes>` (first field) to a value block in the world arena; `Ref` dereferences it. Res stays blittable, so `[System, BurstCompile]` direct calls accept resources with `bool`/`char` fields. Resource values are runtime state and are NOT restored from a save (they commonly own native containers): a load keeps the live world's resources (`ResStorage.CaptureLive`/`RestoreLive`), and resources only present in the save are recreated via `OnCreate` on first request. A detached `new Res<T>()` has no storage. No `SharedStatic`, so changing the resource layout needs no editor restart. `IRes` has `OnCreate(ref World)` and `OnUpdate(ref World)`
- `ResManaged<TRes>` — for class-type resources; uses `ManagedResRef<T>` (GCHandle-like wrapper)
- `IResourceGetSet` — boxing/unboxing interface for reflection-based access (used by debug tools)
- `ResStorage` — per-world storage. `Res<T>`/`ResManaged<T>` params are entries keyed by `StableTypeHash<TParam>` (own FNV-1a over the type name without assembly info, stable across sessions, managed-only), `Local<T>` entries are keyed by `LocalParamSlots.Key` and ARE saved/loaded with the arena (a save from another session resolves them by key). Resources are read outside systems via `world.GetRes<T>()` / `world.HasRes<T>()`.

## 11. Chunk Iteration

- `Chunk<T1..T8>` — direct archetype chunk iterators implementing `IChunk`
- `SetData(ref ArchetypeUnsafe)` — resolves component pointers via `GetComponentLocalIndex` + `GetComponentOffset`
- Iterator pattern: `_remaining` countdown; each `MoveNext()` decrements and advances all component pointers
- Access via component ref properties. NAMING TRAP: arities 2-4 use 1-based names (C1..C4), arities 5+ use 0-based names (C0..C7) — arity-4 `C4` is T4 but arity-5 `C4` is T5. Unify in 2.0 (see ROADMAP.md #1). Arity-1 exposes `Get()` only.
- `CopyTo<TU>(TU* dest, int len)` — component-selective copy of `len` rows from the CURRENT chunk position. All arities gather per-row on sparse (shared) storage and memcpy when dense. The len window is rows[_rowIdx .. _rowIdx+len) — copy before advancing the iterator.

## 12. Reactivity

- Namespace: `Wargon.Nukecs.Reactivity`, files in `src/Reactivity/`. The new API requires only `unmanaged, IComponent`, with no IReactive marker or companion component.
- `entity.OnChange<T>(ReactDelegate<T>, ReactOptions)` returns a long token; another overload accepts `ReactFilter<T>`. Callback signature: `(in T value, in Entity entity)`.
- `OffChange<T>(token)` removes one subscription; `OffChange<T>()` removes all of that type on the entity.
- First subscription auto-registers `ReactiveCheckSystem` and `ReactDispatchSystem<T>` in existing Systems instances for the world. `systems.AddReactive<T>()` explicitly ensures registration.
- A Burst check job compares bytes against snapshots; dispatch completes that job and invokes callbacks on the main thread at the reactive systems' update position.
- `ReactOptions.Once` removes a subscription after dispatch. `TriggerImmediately` fires synchronously if the component exists, otherwise defers the initial trigger until observation after ECB playback.
- `Changed<T> : IFilter` has separate query snapshots. Generated batch code detects changes and iterates the changed list; runtime iter/par_iter do not implement that filtering. Use an eligible plain foreach in a generated system, as in ReactivityTests.
- The historical companion/tag reactive implementation was deleted.

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
using System;
using Wargon.Nukecs;
using Wargon.Nukecs.Transforms;
using Transform = Wargon.Nukecs.Transforms.Transform;
using Systems = Wargon.Nukecs.Systems;
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
            UnityEngine.Object.Destroy(val.Value);
            val.Dispose();
        }
    }
}

// Built-in tag components: DestroyEntity, IsPrefab, ChildOf, Name
// Interfaces: IComponent, IArrayComponent, IPoolComponent
```

### Resource Definition

```csharp
// Unmanaged resource (struct IRes)
public struct ConfigData : IRes
{
    public int TargetCount;
    public float CubeScale;
    public float Timer;
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
// partial: the system body compiles inside this class (short/private helper calls)
public partial class GameSystems
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
        config.Ref.Timer -= state.Time.DeltaTime;
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
var entity = world.Entity();

// With components
var initialEntity = world.Entity(new Health { Value = 100 }, new Position { X = 0, Y = 0 });

// Create then add (deferred via ECB)
var addedEntity = world.Entity();
addedEntity.Add(new Health { Value = 100 });
addedEntity.Add<EnemyTag>();
addedEntity.Add(new Name("Player"));

// Batch creation
var entities = world.BatchCreateEntity(count);
for (int i = 0; i < entities.Length; i++)
{
    var e = entities[i];
    e.Add(new Position { X = i * 1.5f });
    e.Add<Velocity>();
}

// From archetype
var arch = world.GetArchetype(typeof(Position), typeof(Velocity));
var archetypeEntity = arch.CreateEntity();
archetypeEntity.Get<Position>().X = 5;

// Batch from archetype
var archetypeEntities = arch.BatchCreateEntity(count);
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
entity.DestroyNow();                                // immediately removes this entity only; use Destroy() when iterating
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
// Plain foreach: eligible for source-generator batching
foreach (var (pos, vel) in query)
{
    pos.Get.X += vel.Read.X;
}

// Pointer-based (unsafe, Burst-friendly)
foreach (var (pos, vel) in query.iter_unsafe())
{
    pos->X += vel->X;
}

// Runtime full query (use outside parallel per-entity work)
foreach (var (pos, vel) in query.iter())
{
    pos.Get.X += vel.Read.X;
}

// Runtime ref traversal of the current job's assigned range
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
    events.Clear(); // after all consumers have finished
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
public partial class GameSystemsGroup : ISystemsGroup
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

Entity API (`Get`, `Set`, `Add`, `Remove`, `Has`, `Destroy`) **works in ALL thread modes** including `Threads.Parallel` and Burst-compiled systems. Only **Unity managed API** (`GameObject`, `Camera`, `Debug.Log`, `UnityEngine.Object.Destroy`, `UnityEngine.Transform`) requires `Threads.Main`.

| Mode | Entity API | Unity API | Burst |
|------|-----------|-----------|-------|
| `Threads.Main` | Yes | Yes | No |
| `Threads.MainRun` | Yes | **No** | Yes |
| `Threads.Parallel` | Yes | **No** | Yes |
| `Threads.Single` | Yes | **No** | Yes |

`DestroyNow()` requires exclusive world access: complete outstanding jobs and call it outside query iteration. `MainRun` is synchronous `job.Run()`, without a dependency argument; it does not itself wait for earlier scheduled jobs.

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
| `Res<T>.Ref` / `world.GetRes<T>()` | Immediate (world arena) | Yes |
| `entity.Add<T>()` | **Deferred** (ECB) | After playback, possibly in the same frame |
| `entity.Remove<T>()` | **Deferred** (ECB) | After playback, possibly in the same frame |
| `entity.Destroy()` | **Deferred** (ECB) | After playback, possibly in the same frame |
| `entity.DestroyNow()` | **Immediate** | Disposes components, removes storage/query membership, invalidates this generation; pending payloads are cleaned up on ECB playback/clear; no scan or playback for other entities |

**Pattern for same-frame death events**: When an entity dies, use sentinel values instead of relying on deferred `DeadTag`:

```csharp
// DON'T: relies on deferred DeadTag being visible before playback
if (hp.Current <= 0)
    target.Add(new DeadTag()); // deferred — not visible until ECB playback

// DO: use sentinel + immediate event
if (hp.Current <= 0)
{
    deathEvents.Add(new DeathEvent { XPReward = xp }); // immediate
    hp.Current = -999f; // sentinel — visible immediately
    target.Add(new DeadTag()); // for cleanup after playback
}
```

### Events — Frame-start Clearing and Explicit Cleanup

`Events<T>` are immediate buffers. `AddDefaults()` registers `ClearEvents`, which
clears all buffers at its position in the Update list. Register defaults first,
as `WorldInstaller` does: clearing removes the previous frame's events before
this frame's producers/consumers. Under the default scheduler, late MainRun
clearing can race with earlier Parallel writers. Without defaults, clear
explicitly after all consumers and producer jobs finish.

```csharp
[System]
public static void XPSystem(
    ref Events<DeathEvent> deathEvents,
    ...)
{
    foreach (ref var ev in deathEvents) { /* process */ }
    deathEvents.Clear(); // explicit cleanup when no default/later clearing is registered
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

- `Res<T>` where `T : unmanaged, IRes` — per-world arena storage. Outside systems use `world.GetRes<T>()` (ref return); it throws if the world neither added the resource nor registered a system that requests it. `new Res<T>().Ref` is just a detached default copy.
- `ResManaged<T>` where `T : class, IRes` — for resources containing managed types (arrays, GameObjects, etc.). Registered via `world.AddResManaged()`.
- Resources with managed types (arrays, lists) **must** be classes registered with `AddResManaged`.
- Prefer injected parameters in systems. A MonoBehaviour may read an already registered resource through `world.GetRes<T>()` on the main thread after jobs finish, as in the pause example; this does not initialize it. For UI, a Main system can publish a snapshot.

### Query Caching

`world.Query().With<T>()` registers a new Query on each call. Store it once during
setup before entity creation and reuse:

```csharp
private Query enemies;

void Start()
{
    enemies = world.Query().With<Transform>().With<Health>().With<EnemyTag>();
}
```

Identical fluent chains are not deduplicated: CreateQueryPtr appends to the
world's query list (queries live until world disposal — do not build fluent
queries per frame). Since the 1.0 stabilization pass queries attach LAZILY:
every query starts "archetype-dirty" and the first `Count` / `iter*` /
`iter_chunk` / `GetEnumerator` re-runs `CheckQuery` over existing archetypes
(dup-attach guarded), so a query created or mutated (With/None) after spawning
still matches existing entities. Zero-with queries (`world.Query()`,
`Query<Entity>`) match EVERY archetype (minus none bits). Explicit `With<T>`
overrides a default none of the same type (so `Query<Entity, DestroyEntity>`
works — typed Init cannot opt out of default nones). Job paths
(`par_iter`, `TryUseStorageIteration`) never rescan — the generated runners
refresh via `RefreshStorageMode` on the main thread before dispatch.

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
    if (World.GetRes<GameState>().Value == GameStateType.Playing)
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
| Historical generated batch loop from `query.iter()` syntax (rewrite now disabled) | ~1.63 ms |
| Old generated loop (indexed + per-row branch + guards) | ~1.80 ms |
| Runtime/non-batchable `foreach` over `query.iter()` (protocol tax ceiling) | ~2.36 ms |

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
   the fundamental gap between runtime iteration and batched loops under Mono).
7. **Batch analysis currently accepts only plain `query` foreach sources.**
   Explicit `query.iter()` and `query.par_iter()` keep the runtime iterator,
   including inside generated system runners. `iter()` scans the complete query;
   `par_iter()` respects the assigned job range. Deconstruction exposes the first
   Entity slot by value, while data components remain `Ref<T>`.

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

## 19. Dependency Graph Scheduling

- Opt-in: `systems.UseDependencyGraph()`; disable with `UseDependencyGraph(false)`.
  The graph is built from onUpdate runners and invalidated when systems change.
- Generated component access is resolved from symbols (`SrcGen.QueryComponentAccess`):
  every data component of every Query parameter is recorded; loop variables of
  `foreach` over `query`, `iter()`, `par_iter()` and `*_unsafe()` are classified, and any
  use not provably a read (assignment, `++`, `->` write, `ref`/`out`, method call,
  escaping `Ref<T>`/pointer) is a write. A query used any other way (passed on,
  `iter_chunk`) marks all its data components ReadWrite; tags are always Read.
  `entity.Get<T>()`/`Set`/`Has` on any entity are recorded too. Any/None/With are filters,
  not accesses.
- `ISystemDependencyInfoProvider.DependencyInfo` reports component, resource,
  event and ECB accesses; `IThreadModeProvider.Mode` reports the execution mode.
  Conflicting nodes are ordered by registration index; independent nodes can
  share execution groups. Metadata does not automatically capture arbitrary
  hidden accesses in helper methods or external state.
- Default mode: `GroupScheduleMode.LegacyGroupComplete`. Other implemented
  modes: ChainedGroupComplete, FlattenedSchedule, FlattenedSchedule2. Read their
  execution paths in Systems.cs before changing completion or ECB boundaries.
- `State.SkipECBSchedule` lets graph scheduling own ECB playback boundaries.
  Do not insert playback while concurrent readers still hold component buffers.
- Inspect `Systems.DependencyGraph`, `UseDependencyGraphEnabled`,
  `GetGroupScheduleMode()` or the `Nuke.cs/Dependency Graph` window.
- Tests: `UnitTests/DependencyGraphTests.cs`. Historical status reports are not
  evidence that the current revision and every mode have passed a fresh run.

Generator sources are not bundled: ../../../NUKECSGEN/ is this checkout's sibling repository, not a portable install path. See SourceGen/Patches~/README.md for maintenance patches and SourceGen/Tests~/README.md for generator checks.
