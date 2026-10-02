# POST 1.0 — deferred improvements

Collected during the 1.0 stabilization pass (2026-10-02). Nothing here blocks the
1.0 release; items are ordered roughly by value. None of them should be started
before 1.0 ships.

## API surface
1. **Chunk property naming unification.** Arities 2–4 expose 1-based `C1..C4`,
   arities 5+ expose 0-based `C0..C4..` (`Chunk<T1..T4>.C4` is T4, but
   `Chunk<T1..T5>.C4` is T5 — a silent trap). Rename to one scheme in 2.0
   (breaking).
2. **Iteration API consolidation.** `iter_new()/iter_new_ref()` are temp names
   (arity 5 only); arity-5 `iter_unsafe()` returns `QueryIter5` while other
   arities return `QueryIter<PtrTuple<..>>`; arity-1 has no
   `iter_unsafe()/par_iter_unsafe()`; `iter_refs()`/`iter_chunk2()` are legacy.
   `iter_chunk2()` is `[Obsolete]` since the stabilization pass — delete in 2.0.
3. **Per-world `Res<T>`.** `Res<T>` reads a domain-global `StructSingleton`;
   the per-world `resStorage` stores wrapper copies that resolve to the same
   global. 1.0 contract: resources are domain-scoped, not per-world. A real
   per-world resource param needs its own design (resolution via world id).
4. **Query dedup / lifetime.** Identical fluent chains are never deduplicated
   and queries live until world disposal (`QueryUnsafe.Free` has no callers).
   Add an intern table + explicit `world.RemoveQuery()`.
5. **`GetArchetypeHash` rename.** Returns the archetype index, not a hash —
   rename to `GetArchetypeIndex` in 2.0 (doc comment added).
6. **`ComponentData` uses `BinaryFormatter`** — insecure and deprecated;
   replace with a typed writer or delete the helper if unused.

## Correctness hardening
7. **Enumerator invalidation during structural change.** Enumerators snapshot
   `matchingArchetypes.Ptr`; `MemoryList` growth (new archetype mid-foreach)
   can dangle the snapshot. ECB-deferred changes are safe; inline archetype
   mutations mid-iteration remain caller responsibility — guard by copying the
   matching list per enumerator (allocation cost) or loud doc contract.
8. **`EventsStorage` keys on `Type.GetHashCode()`** — collisions would alias
   event types; switch to `ComponentType`-style registered indices.
9. **`MoveEntityTo` silent early-return** (row out of range) desynchronizes the
   ECB playback path — convert to an assert/throw.
10. **Pool `RemoveAndDispose`** disposes pool slots only if the pooled type is
    disposable (ComponentPoolUntyped.Remove handles dispose+clear, but the
    `RemoveAndDispose` semantics for pool components are not covered by tests).
11b. **Resolved: sparse chunk CopyTo addressing.** The copied pointer stayed at the current row while offsets used the previous row, causing wrong reads (reproduced: iteration sum 25, copy sum 13). Offsets now stay relative to the current row in arities 1, 2, 4-8 (arity 3 already used base pointers). MoveNext no longer advances past the final row in arities 1-8. All three ignored tests re-enabled; 20 targeted chunk checks pass, including nonmonotonic rows [1,5,3], copy windows after advancing, sentinels and ECB row/location backlinks. This does not validate the excluded inline-destroy path.
12b. **Resolved: async load left a stale caller World.** LoadAsync took a struct by value; FastDeserialize can relocate the arena and only refresh the static slot/local copy. A different-size-arena regression reproduced the pointer mismatch without dereferencing freed memory. LoadAsync now returns Task<World>; callers must assign `world = await World.LoadAsync(path, world)`. Package call sites updated, including WorldBaker. Serialization fixture plus relocation regression: 39/39 passed. Legacy instance async-void LoadFromFileAsync still requires a separate API cleanup.
12. **Inline destroy / reserved-id migration accounting (BLOCKING for inline-destroy contract).** `ArchetypeUnsafe.Destroy` does not remove the storage row; the row is reclaimed only by ECB playback. An attempt to remove the row inline (mirror of the ECB branch) passed local invariants but corrupted the arena later in prefab chains. Probes (see git history of this pass): after an inline destroy, a fresh entity taking the recycled id and migrating root→archetype through the ECB grows the archetype `rows` list by 2 while the storage grows by 1 (`rowsList=[0,1,1,2]`, storage=3) — two `AddRow`s for one `AllocateRow` somewhere in the ECB playback/reserved-id chain, pre-existing and masked in the old code because the ghost row kept both counts in lockstep. Rework needed: single-owner destroy path (either always inline with a full rows-list+reserved contract, or drop `EntityDestroyMTSystem`'s inline `arch.Destroy`), plus an invariant assert (rows.length == storage.count) under a debug flag.
11. **par_iter ref deconstruction under Burst (4 components): not reproduced on current HEAD.** Added RuntimeQueryBurstRangeRegressionTests: a native Burst function-pointer probe counts MoveNext and writes float3 components over dense/sparse disjoint ranges; generated Single/Parallel runners each visit 2049 entities exactly once. All four checks pass. The exact original `Wargon.EcsBenches.Nukecs.BenchNukecs.Nukecs_Iteration_4Components_Parallel_Par_Iter_Ref_Burst` (100k entities, 110 updates) also passes in isolation from other benchmarks. No runtime iterator or generator change was needed. Combined targeted run: 64 passed, 0 failed, 0 skipped; XML/log at `../../nukecs-ci/stab-targeted-final.*`. This is Editor/Burst evidence, not IL2CPP validation.
## Packaging / infra
12. **IL2CPP player validation.** This pass validated EditMode + Burst in the
    editor only. Run a Windows/Android IL2CPP build before calling Burst
    support complete.
13. **TriInspector dependency** — runtime asmdef references TriInspector only
    for `WorldInstaller` inspector cosmetics. Drop or isolate behind an editor
    asmdef so the package installs without the git dependency.
14. **Package version string.** No `package.json` / version constant ships with
    the package (`NukEcs.version` is runtime-only). Add one for 1.0.
15. **SourceGen source lives outside the package** (`../../../NUKECSGEN`) — the
    package ships only the analyzer dll. Ship sources or a versioned artifact
    alongside releases.
16. **Repo hygiene before tagging:** untracked `Nukecs*.unitypackage`,
    `dependency_graph*.txt`, `mermaid-*.png`, `UnitTests/Results/`,
    stale `DEPENDENCY_GRAPH_STATUS.md` (claims 2 red DepGraph tests; the
    current baseline is green), stale zips in `SourceGen/`.
17. **`WorldInstaller.Awake()` calls `World.DisposeStatic()`** — instantiating
    any installer tears down every live world and static registry. Make it
    opt-in.
18. **`WorldConfig` off-by-ones** — `Default1024` reserves 1025, `Default16384`
    16385, `Default163840` 163841, `Default256000` 256001,
    `Default_1_000_000` 1_000_001, while `Default6144`/`Default65536` are
    exact. Either document the +1 or make them exact.
