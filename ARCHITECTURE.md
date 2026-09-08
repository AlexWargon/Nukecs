# Nukecs — Architecture for AI Agents

> Updated against the source on 2026-09-08. This document explains the storage model and the reasons behind design decisions.
> See [README.md](README.md) for the user-facing API and [AGENTS.md](AGENTS.md) for the code reference.
> The new iterator contract is in [RuntimeQuery/README.md](src/Systems/FnSystems/RuntimeQuery/README.md).
> Optimization history and measurements are recorded in HANDOFF_ArchetypeMasks.md.
> Read this file BEFORE modifying the core (src/). These rules come from actual bugs.

## 1. Mental Model (Five Layers)

```
World ─── archetypesList[] ─── ArchetypeUnsafe (LA)     LOGIC: masks + queries + rows
              │                    │ storagePtr
              │                    ▼
              └── storagesList[] ── StorageArchetype    DATA: SoA columns, packedEntities
                                      ▲
              refCount = number of LAs sharing storage   shared by inlineMask
```

**Key idea (variant C, similar to Bevy Table/Archetype):**

- Archetype **identity** = `inlineMask + tagMask + poolMask` (union hash), used for query matching.
- **StorageArchetype** owns the data and is shared by all logical archetypes (LAs) with the same inline component set.
  Tags use zero bytes (a bit in tagMask); pool data lives in GenericPool (a bit in poolMask).
- **Changing a tag/pool component** migrates rows-list membership without copying inline data (O(1)).
- **Changing an inline component** moves the row between storages (memcpy of inline columns).

`LA.count == rows.length`; `storage.count` includes every row of ALL LAs sharing that storage.
`RowsAreDense == (refCount <= 1)` determines whether the iterator uses dense or gather traversal.

## 2. Structural Changes (The `e.Add<T>()` Path)

```
e.Add → ECB.Add (deferred) → world.Update() → Playback
  → ProcessEntityBatch:
      target = GetOrCreateArchetype(union-mask)       // hash + probe
      MoveEntityTo(row, target)                       // same-storage O(1) / cross-storage memcpy
      BatchMigrateQueries(from, to, entity)           // pair-edge cache below
```

**pairEdges** (`ArchetypeUnsafe.pairEdges: HashMap<long, ptr<Edge>>`, key `(to.index<<32)|from.index`):
query removal/addition lists are built once per pair (`FillPairEdge`) and applied linearly.
Invalidation uses `queriesVersion` (incremented on attachment in CheckQuery/PopulateQueries and in Refresh).
Do NOT enumerate queries with a linear Contains check per entity: this creates quadratic work
(a previous bug caused a 3.5× slowdown with 150 queries).

**Manual query creation order matters**: `world.Query().With<T>()` creates a new
query without attaching it to existing LAs. Create manual queries before spawning
and retain them: identical chains are not deduplicated. Storage mode can discover
data through a lazy storage scan. Typed `Query<T...>.Init`, in contrast, calls
`CheckQuery` for existing archetypes; it uses a different registration path.

## 3. Iteration (Three Paths)

| Path | When | Mechanics |
|---|---|---|
| **batch storage-loop** | An eligible single `foreach (var (a,b) in query)` in a [System] method | Generated pointer walk over storages (`base++` up to the `end` sentinel, bodies use `->`), with walkers in separate methods. See AGENTS.md, "Generated Batch Loops - Performance Contract". Managed 1.63 / Burst 0.164 ms (100k×4×float3). |
| **storage-mode** | Queries with inline-only with-filters, runtime `iter()`/iterator factories | Dense traversal through `GetMatchingStorages()`; falls back to LAs when none-tag filters conflict (prefab/dead). None-filter benchmarks: degradation with 10% tagged entities adds 3–4% to total time; gather costs 16% more per entity (16.3→18.7 ns, constant and independent of row distribution); iteration scales with matching entities (50% → 0.93 ms from 1.63). |
| **enumerator** | Manual entity queries / generic iterators | Existing QueryEnumerator2 / QueryIter and new QueryRuntimeIterN: dense traversal or gathering through logical-archetype rows; ranged runtime traversal preserves matchingArchetypes order. |

**Managed iteration cost model (Mono, 100k×4 components)** — established through measurements:

- Enumerator protocol ~0.24 ms + tuple mechanics ~0.5 ms + `.Get/.Read` bodies ~1.7 ms ≈ 2.36 ms.
- The batch pointer walk at 1.63 ms matches the hand-written baseline (indexed access plus guards took 1.80 ms).
- **Tuple size matters**: `Current` copies the whole struct for each entity. An extra 30 bytes cost approximately 1 ms.
  Do NOT add fields to Ref/PtrTuple (all 35 were reduced to minimal layouts after the 3.3 ms incident).
- The shape of Add (static branches vs stride vs fast path) did not affect speed in those measurements; size did.
- `query.iter()` and `query.par_iter()` use runtime iterators even inside `[System]`: batch rewriting of explicit calls is disabled. `iter()` visits the complete query; `par_iter()` visits the assigned job range. Integration tests have verified native Burst.
- The runtime API supports 1–8 data components within nine generic slots total.
  Entity + 8 components is supported; Entity + 8 + filter is not. Deconstruction
  returns the first Entity by value and components as `Ref<T>`; `Current.C0`
  for Entity remains `Ref<Entity>`. Use `var`, not an old concrete tuple type.
- `Current` is a snapshot of addresses, not a copy of components. Count is captured
  on entry to a block; structural changes can invalidate references. `par_iter()`
  neither schedules jobs nor synchronizes access. Account for the range when
  changing iterator methods: `.iter()` inside Parallel repeats the full traversal in every job.
- View-Current (a lightweight 32-byte Current) was TRIED: it was 0.2 ms slower than tuples in A/B tests. Do not repeat that approach without new evidence.

### Batch Generation and the Fastest Storage Path

For ordinary component queries, the current generator requires a block-bodied
`[System]` method whose only top-level statement is a single plain
`foreach (... in query)`. It analyzes the first typed Query parameter; the loop
must name that parameter directly. Additional or nested foreach loops, local
functions, and statements before or after the loop disable the rewrite. Even
`var dt = state.Time.DeltaTime;` before the loop is enough to disable it. Local
variables inside the loop body are allowed.

The generator must recognize the iteration variables and component types, and
none of the iterated component types may implement `IPoolComponent`. Explicit
`.iter()` / `.par_iter()` calls always retain runtime iteration. Execute through
the generated runner registered with `Systems.Add`; a direct call to the user
method does not invoke its generated batch implementation.

The fastest dense path uses inline data components, no tag tuple slots, and a
storage snapshot with `storageDegraded == 0`. Tags or filters can require the
logical-archetype walker, including sparse gather, while retaining generated
batch code. Default none-filters for `IsPrefab` and `DestroyEntity` can trigger
this fallback when excluded logical archetypes in a shared storage are nonempty.
`Changed<T>` uses a separate generated change-detection path.

The dense pointer walk removes per-entity enumerator and tuple overhead. Combine
it with `[BurstCompile]` and Burst-compatible code for the highest performance
on eligible dense inline workloads; actual timings and the best thread mode
still depend on workload size and hardware. See the examples in README.md.

## 4. Invariants (Violations Corrupt Data)

1. LA `rows` ↔ `entityLocations.listPos` ↔ `storage.packedEntities` must always agree.
   Swap-remove uses `FixSwappedEntityLocation` to repair the row and any affected LA rows.
2. Increment `queriesVersion` on EVERY change to the attached query set so pairEdges remain valid.
3. Increment `world->version` on row allocation/removal to invalidate storage-mode snapshots.
4. Deserialization restores pairEdges with TWO fixups (`ptr<Edge>` and `Edge.OnDeserialize`
   for the internal lists). `RestoreIfNeed` repairs managed Query wrappers, including Count access.
5. Burst paths must not read mutable static fields: use `SharedStatic<T>` or `[BurstDiscard]` for diagnostics.
   A plain static reachable from Burst code can silently force ALL affected jobs into managed fallback.
6. Update query counters (`entityCount`) ONLY through pair-edge lists, nowhere else.

## 5. Code Map (Contents and Constraints)

| File | Purpose |
|---|---|
| `src/Archetype.cs` | ArchetypeUnsafe: masks, rows, pairEdges, BatchMigrateQueries/FillPairEdge, CheckQuery/PopulateQueries. Edge holds removal/addition lists and versions. |
| `src/StorageArchetype.cs` | Data: SoA columns, packedEntities, refCount, logicalArchetypes (LA/storage backlinks for storage mode). |
| `src/Query.cs` | QueryUnsafe: with/none masks, matchingArchetypes (LA path), matchingStorages (storage path, built lazily under a spinner), IsStorageMode/UseStorageIteration/TryUseStorageIteration, count property. |
| `src/World/World.Unsafe.cs` | Registries: archetypesList/storagesList/GetOrCreate* (hash + probe), GetOrCreateStorage (refCount++). |
| `src/Systems/FnSystems/QueryIterators*.cs` | Existing plain/ref/pointer/chunk paths with dense/gather/storage branches. |
| `src/Systems/FnSystems/RuntimeQuery/` | Explicit iter/par_iter: QueryRuntimeIter1..9, QueryRuntimeRefs, pool page cache, QueryRuntimeDeconstruction (Entity by value); Current has no ref return. |
| `src/Systems/FnSystems/Tuples/*.cs` | 35 tuple structs reduced to minimal field layouts — do not enlarge them (§3). |
| `src/Systems/FnSystems/Chunk.cs` | Chunk iterators: sparse CopyTo is supported ONLY at arity 3 (element-by-element through rows); fix arities 1–2 and 4–8 using that implementation as a reference before using them for sparse rows. |
| `src/Entity/EntityCommandBuffer.cs` | Playback: ProcessEntityBatch handles migrations and pair-edge accounting. |
| `src/Reactivity/` | OnChange/OffChange, byte snapshots, Burst check job, main-thread dispatch; Changed<T> is an IFilter for the generated batch path. |
| `src/Systems/DependencyGraph/` | Conflict metadata, graph and execution groups; enabled through Systems.UseDependencyGraph. |
| `src/Allocator/AllocatorDebug.cs` | SharedStatic Arena Guard flags, allocation tags and violation descriptions; Validate is implemented in Allocator.cs. |
| `SourceGen/NUKECSGEN.dll` | Generator: batch storage-loop uses a pointer walk (see the performance contract in AGENTS.md: no indexed access or `_rowsPtr` branch inside the dense hot loop; walkers stay in separate methods WITHOUT AggressiveInlining), RefreshStorageMode in Schedule. |

## 6. Diagnostics (Investigation Tools Belong in Tests, Not the Core)

Permanent allocator diagnostics use the separate Arena Guard mechanism: canaries,
freed-memory poisoning, Validate and tag statistics. Toggle it in
`Nuke.cs/Allocator Debug`; it does not require `NUKECS_DEBUG`. Validation also
runs during world disposal and allocator load. Debug V2 and Scene View gizmos
require `NUKECS_DEBUG`. Gizmos edit only the world-space Transform, without Undo.

- `UnitTests/IterationDiagnosticsTests.cs` — breakdown of iteration costs by layer (`[DIAG]` logs).
- `UnitTests/MigrationDiagnosticsTests.cs` — sensitivity of migration cost to query count, plus A/B comparisons of strategies.
- `world.DumpArchetypes()` — dumps masks/rows/queries/storage when a test fails.
- `RuntimeQuery*IntegrationTests`, `RuntimeQueryProductionRegressionTests`,
  `RuntimeQueryEntityDeconstructionTests` — runtime API, arities/ranges,
  Entity-by-value access and Burst probes. `AllocatorDebugTests` covers Arena Guard.
- New reactivity lives in `src/Reactivity/`, without adding Reactive<T> to an
  entity. The generated batch path handles `Changed<T>`; explicit runtime
  iter/par_iter do not perform change detection. Subscriptions and Changed
  queries have separate snapshots; do not confuse them with historical `src/Reactive/` files.
- Principle: one-off diagnostics must not remain in the framework's hot path
  (MigrationStats and QueryBookkeepingBypass were removed after investigation).

## 7. Working with the Codebase

- For bulk source edits with scripts, use line-by-line transformations, integrity
  assertions and a build after EVERY pass. Walk backward over attributes only
  for `[MethodImpl` (fields with `[NativeDisable...]` before a method were accidentally removed twice).
- Build verification: run `dotnet build Nukecs.csproj` and `dotnet build Nukecs.Tests.csproj` from the Unity project root.
- Run correctness tests through the Unity Edit Mode Test Runner, not `dotnet test`.
  A C# build does not verify native Burst or IL2CPP. The historical benchmarks
  above describe their original runs, not measurements of the current version.
- Benchmarks use `[Category("Benchmark")]`; exclude them from Unity Run All through the category filter.
- Measurements from separate sessions are not directly comparable (±10–20% noise); run A/B comparisons within the same session.
