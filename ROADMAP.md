# Roadmap

Open improvements carried over from the 1.0 stabilization pass. Resolved items
are recorded in AGENTS.md §5 and in git history. Items are ordered roughly by
value within each section.

## API surface

1. **Chunk property naming unification.** Arities 2–4 expose 1-based `C1..C4`,
   arities 5+ expose 0-based `C0..C7` (`Chunk<T1..T4>.C4` is T4, but
   `Chunk<T1..T5>.C4` is T5 — a silent trap). Rename to one scheme in 2.0
   (breaking).
2. **Iteration API consolidation.** `iter_new()/iter_new_ref()` are temporary
   names (arity 5 only); arity-5 `iter_unsafe()` returns `QueryIter5` while
   other arities return `QueryIter<PtrTuple<..>>`; arity-1 has no
   `iter_unsafe()/par_iter_unsafe()`; `iter_refs()`/`iter_chunk2()` are legacy.
   `iter_chunk2()` is `[Obsolete]` — delete in 2.0.
3. ~~**Per-world `Res<T>`.**~~ Done 2026-10-04: the value lives in the
   `Res<T>` param inside the world's `resStorage` slot (no `StructSingleton`);
   outside systems use `world.GetRes<T>()`.
4. **Query dedup / lifetime.** Identical fluent chains are never deduplicated
   and queries live until world disposal (`QueryUnsafe.Free` has no callers).
   Add an intern table + explicit `world.RemoveQuery()`.
5. **`GetArchetypeHash` rename.** Returns the archetype index, not a hash —
   rename to `GetArchetypeIndex` in 2.0.
6. **`BinaryFormatter` usage** (`ComponentData`, `ComponentTypeData`) —
   insecure and deprecated; replace with a typed writer or delete the helpers
   if unused.
7. **Legacy `World.LoadFromFileAsync`** is `async void` and cannot return the
   relocated world. Remove it or replace it with the `Task<World>` pattern used
   by `World.LoadAsync`.
8. **`WorldConfig` off-by-ones.** `Default1024` reserves 1025, `Default16384`
   16385, `Default163840` 163841, `Default256000` 256001,
   `Default_1_000_000` 1_000_001, while `Default6144`/`Default65536` are
   exact. Either document the +1 or make them exact.

## Correctness hardening

9. **Enumerator invalidation during structural change.** Enumerators snapshot
   `matchingArchetypes.Ptr`; `MemoryList` growth (new archetype mid-foreach)
   can dangle the snapshot. ECB-deferred changes are safe; inline archetype
   mutations mid-iteration remain caller responsibility — guard by copying the
   matching list per enumerator (allocation cost) or a loud doc contract.
10. **`EventsStorage` keys on `Type.GetHashCode()`** — collisions would alias
    event types; switch to `ComponentType`-style registered indices.
11. **`MoveEntityTo` silent early-return** (row out of range) desynchronizes the
    ECB playback path — convert to an assert/throw.
12. **Pool `RemoveAndDispose` coverage.** `ComponentPoolUntyped.Remove` handles
    dispose+clear, but `RemoveAndDispose` for pool components has no tests
    (only the inline column case is covered).

## Simplification audit

Noted 2026-10-10 while moving the core off Unity: the API surface is larger than
what the game, tests and demos use, and three long-standing bugs were found in
rarely exercised paths (arena `HashMap` offsets after a resize, Arena Guard on
split free blocks, the `Changed<T>` old-values pointer). Do this before the Jobs
step of the engine-independence work: every system/runner kind is scheduling
code that would otherwise have to be ported.

15. **Audit and consolidate.** Build a table "feature -> users in Game /
    NukecsTests / UnitTests / Demos -> keep / merge / `[Obsolete]` / delete",
    then pick one canonical path per group. Candidates seen so far:
    - Query iteration: plain `foreach`, `iter()`, `par_iter()`, `iter_unsafe()`,
      `par_iter_unsafe()`, `Chunk<T1..T8>`, `iter_chunk2()` (see 1, 2), and five
      tuple families (`Ref`/`Ptr`/`EntityRef`/`EntityPtr`/`Object`) per arity.
    - System kinds: `[System]` functions, `ISystem` struct and class,
      `IEntityJobSystem`, `ISystemsGroup`, delegate runners
      (`DelegateSystem1Runner`, `System1`/`System2` delegates).
    - Dependency graph: four group schedule modes (`LegacyGroupComplete`,
      `ChainedGroupComplete`, `FlattenedSchedule`, `FlattenedSchedule2`).
    - Change tracking: `OnChange` subscriptions and the `Changed<T>` filter keep
      separate snapshots of the same data.
    - Unsafe helpers: `UnsafeStatic`, `NUnsafe` and `Mem` overlap.
    - Dead code: `DelegateSystemsExtensions.cs` (fully commented out),
      `StaticAllocations` (`AddDisposable` has no callers), `ComponentsMapCache`,
      large commented blocks. Already removed: `DynamicBuffer`,
      `EntityFilterBuffer`, the `Serv<T>` service parameter.

## Packaging / infra

16. **Player builds on other platforms.** Windows Mono and IL2CPP player builds
    behave as in the Editor. Android, other IL2CPP targets and consoles have
    not been checked.
17. **SourceGen source lives outside the package** (`../../../NUKECSGEN`) — the
    package ships only the analyzer dll. Ship sources or a versioned artifact
    alongside releases.
