# Nukecs — documentation review

Current pass: `dev` @ `ef5cf83` (2026-10-03). Earlier passes: `Storage-Rework` @ `38b4add`
(2026-09-08) and `dev` @ `089f73a` (2026-10-02). The full text of the `089f73a` findings is
in commit `4442224`; this file now tracks their status plus the remaining and new findings.
Item numbers (§1.12, §4.3, …) are kept from the `089f73a` review so they can be matched.
The author's follow-up notes are kept verbatim below.

## Follow-up status (2026-10-02)

### Current remediation (2026-10-03, working tree)

The lists below the separator describe the historical reviewed revision. This
section records the current implementation; their original numbering/text is
retained for traceability.

- **High:** N1's UnityEditor import is now guarded; the analyzer is excluded from
  Android/iOS runtime loading. The prefab example applies ECB before copying and
  explains OnPrefabSpawn. AGENTS examples now have the timer field, System import,
  qualified UnityEngine.Object and aliases for Transform/Systems (§1.12, §2.3–2.5).
- **Runtime lifecycle:** N2 is fixed: explicit resource registration initializes
  the stored wrapper once, and duplicate registration preserves the original.
  N3 is fixed: public Complete APIs wait for live State dependencies and independent
  scheduled branches. RequestSave/RequestLoad process a detached frame-boundary
  queue; immediate Save/Load synchronize. See WorldIoRequestTests.
- **World ownership:** WorldInstaller no longer calls DisposeStatic in Awake;
  destroying one installer preserves other worlds and reused world slots (§6.6).
  ManagedWorld domain wrappers are freed on disposal/load failure; deserialization
  retains the current world's owned wrapper instead of trusting a saved pointer.
- **Medium documentation:** batch rules, MainRun graph-mode behavior, event-clearing
  order, resource access, compatibility system APIs, ComponentArray restrictions,
  installation, Unity authoring/visible entities, rendering and demo setup are
  documented (§2.7, §3.1, §4.1–4.3, §5.1–5.8, N4–N5). EN/RU guides no longer
  depend on an absent gameplay file, and Main examples no longer imply native Burst.
- **Pool Parallel question (§7.2):** ReviewPriorityTests cover plain foreach both
  with and without Entity, multiple logical archetypes, removed rows and all four
  graph modes, using 2049 initial entities and checking exactly one visit.
- **Low:** corrected enum order, capacities, managed resource/event definitions,
  inspection context, event references, no-op semantics, fixed interval, handle
  examples, hierarchy restrictions, transform list, obsolete links/dates/history
  and misplaced paragraphs. Cube moved to demo code with its namespace preserved;
  stale analyzer archives and orphan comments were removed. Generator patches
  have maintenance documentation. The Transforms.Systems name is retained for
  compatibility; examples use a Systems alias to avoid ambiguity.
- **N13 correction:** ClearTransformsSystem is used by ClientRunner in the host
  game outside this package. It remains a documented main-thread compatibility
  cleanup for explicitly tagged views; it is not automatic Entity.Destroy cleanup.

Remaining release work is broader packaging/validation: a distributable UPM
manifest, bundled/versioned full generator sources, ready-made demo scenes and
an actual player/IL2CPP build. The Assets installation and manual demo setup are
documented; Editor/Burst checks do not claim player compatibility. Historical test
counts below are local run records, not evidence for an already published artifact.
Test-result XMLs and Unity logs remain local verification artifacts and are
not included in the repository commits.
Validation: Unity 6000.0.63f1 EditMode with Burst enabled, 110/110 passed,
0 failed and 0 skipped. Includes native Local registration/lookup probes and
both pool-query forms in the default scheduler and all graph modes. No player
or IL2CPP build was run.

Follow-up (2026-10-03): the byte-array World.Load overload now deserializes the
snapshot, preserves its world slot/handles, rejects an occupied saved slot and
releases temporary allocations on failure. Legacy SystemsGroup Update and Fixed
lists route to their corresponding lifecycle phases. WorldInstaller calls OnStart
after initial ECB playback, flushes startup changes and exposes a virtual Awake.
The unused runtime UnityEditor import was removed. README hierarchy, event clearing,
Transform registration and deferred-copy examples were corrected; only CopyViaECB
remains in the API.
Validation: Unity 6000.0.63f1 EditMode, 122/122 passed with Burst enabled,
including seven new load/lifecycle regressions. Results:
`UnitTests/TestResults_ReviewFollowup.xml`. Player builds were not tested.

`Local<T>` is implemented as per-registration, per-world resource state. Generated
runners initialize it once, update its lifecycle once before dispatch, and restore
its arena-backed value after load. Duplicate registrations and parallel ranges are
covered by `UnitTests/LocalResourceTests.cs`. The README example uses `Ref` and `IRes`.
Unity 6000.0.63f1 EditMode: 81/81 passed with Burst enabled, including 16 Local
cases and the native batch probes. Results: `UnitTests/TestResults_LocalResources.xml`.
Follow-up (2026-10-03): `Single<T>` and `MutRes<T>` were removed from the runtime
and current API documentation. Their unused metadata branch was removed; other
metadata values retain their explicit numeric IDs. `Threads.Single` remains supported.
Removal validation: 57/57 Local and serialization tests passed with Burst enabled
(`UnitTests/TestResults_RemoveSingleMutRes.xml`).
Local registration now uses numeric generated owner keys and the framework's
unmanaged HashMap. Native Burst registration/lookup and regression coverage:
82/82 passed (`UnitTests/TestResults_LocalResourcesHashMapBurst.xml`).

The findings below describe the reviewed revision, not necessarily the current tree.
Thread mode is selected through registration; only `[System]` remains supported.
The obsolete resource-value parameter section and removed creation tag have been
removed from the current usage documentation. `MainRun` is synchronous `job.Run()`
without a dependency argument; waiting for outstanding jobs is the caller's responsibility.
This is its intended contract, not an unresolved generator question.

`DestroyNow` now implements immediate destruction directly in `Entity.cs`: it disposes
components, swap-removes the storage row, detaches queries and recycles the ID.
It never scans pending commands: ECB captures generations and skips expired commands
during normal playback, disposing uninstalled payloads. Pool additions defer their data.
Reserved IDs without a storage row are handled separately. It requires exclusive
world access outside query iteration. The dedicated destruction systems and their
default registration were removed: deferred destruction is handled by ECB playback,
and immediate destruction by `Entity.DestroyNow`. The legacy tag alone no longer
deletes an entity. Regression coverage was added to `ReleaseStabilizationTests`.
Previous immediate-destruction validation: Unity 6000.0.63f1 EditMode, 92/92 passed across stabilization, prefab,
world and serialization fixtures, including five immediate-destruction tests and one ECB regression without defaults.
Results: `UnitTests/TestResults_DestroyNow.xml`. This is Editor validation;
it does not establish player/IL2CPP support.

Generation follow-up: Entity is 8 bytes (`int` ID + `ushort` generation + `ushort`
world token). The token includes the world-slot incarnation, and exhausted entity
generations are retired instead of wrapping. `DestroyNow` leaves ECB buffers
untouched; stale commands and Copy destinations are checked by captured identity.
Generation-aware reactive subscriptions do not follow recycled IDs or unrelated
entities loaded from another saved world; explicit resubscription is required.
Unity 6000.0.63f1 EditMode: 193/193 passed, including 12 generation cases and
pool/query, prefab, world, serialization and reactive-load regressions.
Results: `UnitTests/TestResults_Generations.xml`. Save format is now 2.

---

*Everything below this line was updated for `dev` @ `ef5cf83`; line numbers refer to that
revision.*

**Method.** README.md, AGENTS.md, ARCHITECTURE.md, NUKECS_AGENTS_GUIDE_EN/RU.md, POST_1_0.md
and `src/Systems/FnSystems/RuntimeQuery/README.md` were compared with the source by reading,
and the author's follow-up claims above were checked against the source (§3). Nothing was
compiled or run in Unity. The source generator ships only as `SourceGen/NUKECSGEN.dll`, so
generator behaviour was inferred from the DLL's strings, call sites and tests.

**Severity:** **High** — the example does not compile, produces a wrong result or breaks a
build; **Medium** — misleading or a missing step; **Low** — inaccurate detail or cosmetic.

## 1. Summary of this pass

| Area | `089f73a` | `ef5cf83` |
|---|---|---|
| Core ECS (components, systems, queries, ECB, resources), including the gameplay guide | 8/10 | 8.5/10 |
| README.md accuracy on its own | 6/10 | 7.5/10 |
| ARCHITECTURE.md | 7.5/10 | 7.5/10 |
| Building a complete game from the docs | 5/10 | 5.5/10 |

Status of the 56 findings from the `089f73a` review:

| Status | Count | Items |
|---|---|---|
| Fixed | 19 | §1.1–1.6, §1.8–1.11, §1.13, §1.22; §2.1, §2.2, §2.6; §6.1–6.4 |
| Obsolete (code removed) | 2 | §1.7 (`EntityCreated`), §7.4 (`Single<T>`) |
| Answered | 2 | §7.1 (MainRun contract), §7.3 (`IRes.OnCreate`, see N2) |
| Partly fixed | 4 | §5.2, §5.4, §6.6, §7.2 |
| Still open | 29 | listed in §2 |

What changed:

- **Most High findings are fixed.** The README examples for `[System]`, `Local<T>`,
  hierarchy, events, transforms and deferred copy now match the source.
- **Code behind the review was fixed.** `DestroyNow` is now really immediate and documented
  consistently. `WorldInstaller` runs `OnStart()`. `World.Load(byte[])` works, the legacy
  `SystemsGroup` routing is fixed, and `Single<T>`/`MutRes<T>` were removed.
- **Entity handles carry a generation**, documented in README and ARCHITECTURE.md.

What remains:

- **High:**
  - the README prefab example (§1.12, now confirmed);
  - three non-compiling AGENTS.md §16 examples (§2.3–2.5);
  - a new player-build blocker in `EditorIcons.cs` (N1).
- **Gaps that did not change:** installation, rendering, GameObject linking beyond
  transforms, demo documentation.

## 2. Open findings (current line numbers)

### README.md

- **§1.12 (High, confirmed).** The prefab example `README.md:469-476` produces empty
  instances.
  - `prefab.Add(...)` is deferred through the ECB (`src/Entity/Entity.cs:188-193`), but
    `SpawnPrefab` copies the prefab immediately (`src/World/World.Unsafe.cs:510-514`). The
    copies are made before the prefab has `Speed`/`IsPrefab`.
  - Add `world.Update();` before `SpawnPrefab`, as `EntityPrefabMap` does
    (`src/Unity/EntityPrefabMap.cs:45-48`).
  - Mention that `OnPrefabSpawn` (which strips `IsPrefab` from copies) is registered only
    by `AddDefaults()`.
- **§1.14 (Low). `Threads` enum order.** `README.md:527-533` lists
  `Main, MainRun, Single, Parallel`; the actual order is `Main, MainRun, Parallel, Single`
  (`src/Systems/Systems.cs:866-886`).
- **§1.15 (Low). `WorldConfig` capacity table.** `README.md:1288-1299` shows round
  numbers; several presets reserve one more (`src/World/World.cs:200-247`). Tracked in
  POST_1_0.md #18.
- **§1.16 (Low). `ResManaged` example.** `README.md:985-998` doesn't say the resource must
  be a `class` (`AddResManaged<T> where T : class, IRes`, `src/World/World.cs:166`).
- **§1.17 (Low). Event type example.** `README.md:1043` makes the event an `IComponent`;
  `Events<T>` only needs `unmanaged`.
- **§1.18 (Low). Batch inspection snippet.** `README.md:253` uses an undeclared
  `updateSystems`, and `Systems.Runners` returns only the Update list
  (`src/Systems/Systems.cs:43`).
- **§1.19 (Low). Event iteration copies.** `README.md:1063` uses
  `foreach (var evt in events)`, which copies each event; `foreach (ref var evt in events)`
  is supported.
- **§1.20 (Low). Silent no-ops on live entities are undocumented.**
  - `Add<T>` does nothing if the component is already present (`Entity.cs:191`).
  - `Set<T>` does nothing if it is absent (`Entity.cs:238`).
  - `TryGet` returns a null reference when absent (`Entity.cs:168`).
  - README now documents only the expired-handle behaviour (`README.md:436`).
- **§1.21 (Low). Fixed update is undocumented.** The interval is hardcoded to 16 ms, with
  at most one fixed tick per `OnUpdate` (`src/Systems/Systems.cs:29`, `:142-154`).

### AGENTS.md

- **§2.3 (High). `SpawnSystem` uses a missing field.** `config.Ref.timer`
  (`AGENTS.md:554`), but `ConfigData` (`AGENTS.md:482-488`) has no `timer` field.
- **§2.4 (High). Ambiguous `Transform`.** `Get<Transform>()` / `With<Transform>()`
  (`AGENTS.md:626`, `:630`) are CS0104 with the listed usings (`AGENTS.md:439-445`). Use
  `using Transform = Wargon.Nukecs.Transforms.Transform;`.
- **§2.5 (High). `GameObjectView`.** `GameObjectView : IComponent, IDisposable`
  (`AGENTS.md:461`) lacks `using System;`, and adding it makes `Object.Destroy`
  (`AGENTS.md:468`) ambiguous.
- **§2.7 (Medium). Contradicting `Res<T>` advice.** `AGENTS.md:1067` says `new Res<T>().Ref`
  works anywhere, `AGENTS.md:1070` says not to use it from MonoBehaviours, and the pausing
  example at `AGENTS.md:1147` does exactly that.
- **§2.8 (Low). Redeclared `e`.** The entity creation snippet `AGENTS.md:713-742` declares
  `e` several times in one block.
- **§2.9 (Low). Generator source path.** It points outside the repository
  (`AGENTS.md:12`, `:287`).

### ARCHITECTURE.md

- **§3.1 (Medium). Stale batch-generation rules.** `ARCHITECTURE.md:95-101` still says the
  `foreach` must be the only top-level statement and that `var dt = ...` before the loop
  disables batching. This contradicts `README.md:233-239`, `AGENTS.md:327-334` and guide
  EN `:213-220`.

### NUKECS_AGENTS_GUIDE_EN.md / _RU.md (still equivalent)

- **§4.1 (Medium). Missing reference file.** The guide references
  `Assets/Game/Scripts/EcsTest.cs`, which is not in the repository (EN `:5`, `:112`;
  RU `:7`, `:111`).
- **§4.2 (Medium). Burst attribute on Main systems.** `[System, BurstCompile]` is on
  systems registered with `Threads.Main`, where Burst is not used:
  - EN: `Initialize` `:85` (registered `:55`, `:137`, `:164`), `Consume` `:356`
    (registered `:371`).
  - RU: `:85` and `:352`.
  - This contradicts the guide's own §2.
- **§4.3 (Medium). `ComponentArray` pitfalls are missing** (EN `:457-483`, RU `:448-472`).
  - `AddArray` flushes the whole ECB (`src/Entity/EntityArrayExtensions.cs:52`).
  - The API takes `this ref Entity`.
  - `GetArray` throws when missing (`:21`).
  - `DEFAULT_MAX_CAPACITY` is internal (`src/Components/ComponentArray.cs:12`).
  - The 15-vs-16 capacity quirk is still in code (`ComponentArray.cs:104`).
- **§4.4 (Low). RU formatting.** Blank lines are missing before `RU:172` (`## 2.`) and
  `RU:473`.

### Missing documentation

- **§5.1 Installation.** Still missing: Unity version (the follow-up mentions
  6000.0.63f1), required packages, the TriInspector dependency (`src/Nukecs.asmdef:8`),
  the analyzer DLL setup, and `NUKECS_DEBUG`. There is no `package.json`
  (POST_1_0.md #13–14).
- **§5.2 (partly fixed) GameObject ↔ entity.** `TransformRef`,
  `TransformsUtility.Convert` and the `EntityPrefabMap` cache are now documented.
  `EntityBaker`, `EntityLinkSO`, `WorldBaker` and `GameObjectRef` are still not, and there
  is no "show an entity on screen" walkthrough.
- **§5.3 Rendering.** Nothing is documented; point readers to `RenderMeshInstanced` in
  BoidsDemo and CubeSculptureDemo.
- **§5.4 (partly fixed) `AddDefaults()`.** It is now mentioned, but not what it registers
  (`OnPrefabSpawn`, `ClearEvents` — `src/Systems/Systems.cs:166-172`).
  `Systems.Default(ref world)` (`Systems.cs:161`) is undocumented.
- **§5.5 Status of `ISystem` / `IEntityJobSystem` / `SystemsGroup` is contradictory.**
  AGENTS.md presents them as normal API (`AGENTS.md:617-640`, `:933-941`), the guide calls
  them legacy (EN `:110-111`), and README is silent.
- **§5.6 Other undocumented API.** `GetSingleton<T>`, `IAspect` / `GetAspect<T>`,
  `AddObject` / `SetObject`.
- **§5.7 Demos.** There is no README, no scenes and no run instructions.
- **§5.8 Physics, input, UI, audio.** Not provided, and not stated anywhere. The `Input`
  component (`src/Components/Component.cs:92`) is unused.

### Code and packaging

- **§6.5 (Low) Stray component.** `public struct Cube : IComponent` is in
  `src/Unity/WorldInstaller.cs:65` (RotateCubeDemo depends on it).
- **§6.6 (partly fixed) `WorldInstaller.Awake`.** `Awake` is now `protected virtual`
  (`WorldInstaller.cs:19`) but still calls `World.DisposeStatic()` (`:21`, POST_1_0.md #17).
- **§6.7 (Low) Name clash.** `Wargon.Nukecs.Transforms.Systems`
  (`src/Unity/SyncTransformsSystem.cs:5`) clashes with `Wargon.Nukecs.Systems`.
- **§6.8 (Low) Stale archives.** `SourceGen/NUKECSGEN.zip` and `SourceGen/NUKECSGEN2.zip`
  are still tracked.

### Open question

- **§7.2 (partly answered). Pool-component plain `foreach` under `Threads.Parallel`.**
  - For Entity-first queries the generated fallback enumerator is the range-splitting
    `QueryParIterWithEntity` (DLL strings; `QueryIteratorsParallel.cs:234-340`), so the
    guide's `ApplyDamage` example is probably correct.
  - Non-Entity shapes such as `Query<T1, TPool>` are unconfirmed.
  - No test runs a plain `foreach` over a pool component under Parallel. Adding one would
    close this.

## 3. Verification of the author's follow-up claims

| Claim | Result |
|---|---|
| `World.Load(WorldConfig, byte[])` deserializes, keeps the slot, rejects an occupied slot, cleans up on failure | Confirmed (`src/World/World.Static.cs:193-233`); 7 regressions in `ReviewFollowupTests.cs`. Minor: the `ManagedWorld` pointer allocated in `Create` is not freed on failure (nor on a normal `Dispose`). |
| Legacy `SystemsGroup` lists routed to the right phases | Confirmed (`src/Systems/Systems.cs:283-293`). Within a group, all `runners` still precede all `mainThreadRunners` regardless of registration order. |
| `WorldInstaller` runs `OnStart` after initial playback, flushes, virtual `Awake` | Confirmed (`WorldInstaller.cs:19`, `:33-36`). |
| Runtime `UnityEditor` import removed | Confirmed for `Singleton.cs`; another one remains, see N1. |
| `Single<T>` / `MutRes<T>` removed | Confirmed; no remaining usages in src, tests, demos or docs. |
| Only `CopyViaECB` remains | Confirmed; an orphan doc comment for the removed alias is left at `Entity.cs:482`. |
| `Local<T>` per registration and per world, restored on load | Consistent with `Local.cs`, `LocalParamSlots.cs`, `ResStorage.cs` and `LocalResourceTests.cs`; the generator side is visible only through DLL strings. |
| `DestroyNow` immediate; ECB skips expired commands; destruction systems removed; tag no longer deletes | Confirmed (`Entity.cs:413-443`, `EntityCommandBuffer.cs`, `World.Unsafe.cs:318-333`). No source, test or demo relies on the tag. Leftovers: N9, N13. |
| Entity is 8 bytes with a generation; save format 2 | Confirmed (`Entity.cs:13-20`, `:68-89`; `nukecs.cs:9`; `Serialization.cs:38-41`). The previous struct was also 8 bytes because of padding. |
| Test results 122/122, 81/81, 57/57, 82/82, 92/92, 193/193 | **Not verifiable.** None of the referenced `UnitTests/TestResults_*.xml` files is committed. The only tracked result, `UnitTests/TestResults_20260622_154845.xml`, is stale (12 total, 1 failed). Commit the XMLs or drop the references. |

## 4. New findings at `ef5cf83`

- **N1 (High) Player build blocker.** `src/Unity/Editor/World/EditorIcons.cs:3` has
  `using UnityEditor;` before its `#if UNITY_EDITOR && NUKECS_DEBUG` (`:6`). The folder is
  covered by `src/Nukecs.asmdef`, which has no platform restriction, so player builds fail
  with the same error as the fixed `Singleton.cs`. Move the `using` inside the `#if`.
- **N2 (Medium) `IRes.OnCreate` never runs for registered resources.** `README.md:956`
  says it is "Called once on creation".
  - `Res<T>.Init` (`src/Systems/FnSystems/Res.cs:56-60`) calls it only when a system
    parameter is created for a resource that was not registered beforehand
    (`World.Unsafe.cs`, `GetSystemParam2`).
  - For resources added with `world.AddRes` / `AddResManaged`, the documented pattern, it
    never runs.
  - The `IRes` XML doc (`src/Systems/FnSystems/Local.cs:19-23`) describes `Local`
    semantics.
  - Fix: call `OnCreate` from `AddRes`, or fix the docs.
- **N3 (Medium) No public way to wait for outstanding jobs.** The MainRun contract now
  requires the caller to wait, but:
  - `Systems.Complete()` is internal (`src/Systems/Systems.cs:296`).
  - The public `Systems.Dependencies` (`Systems.cs:25`) and `World.DependenciesUpdate`
    (`src/World/World.cs:81`) are never assigned a real handle.
  - `World.CompleteAllJobs` (`src/World/World.SerializeAndSave.cs:35-39`) completes exactly
    those handles, so save/load called from a system while Parallel jobs are in flight is
    not synchronised.
  - The only workaround is a `Threads.Main` system before the MainRun one. Expose a public
    `Complete()` or document the workaround.
- **N4 (Medium) MainRun contract scope.** "MainRun does not wait" holds for the default
  scheduler only.
  - With `UseDependencyGraph`, the Legacy and Chained modes complete before MainRun
    (`SystemDependencyGraph.cs:102-105`; `Systems.cs:437-439`, `:501-503`).
  - The Flattened modes run Main/MainRun first (`Systems.cs:592`, `:728`).
  - The `Threads` XML doc (`Systems.cs:870`, `:875`: "In feature Main and MainRun will be
    same") contradicts the new contract.
- **N5 (Medium) `ClearEvents` placement.** The default `ClearEvents` is MainRun
  (`Systems.cs:169`). It is safe only because `WorldInstaller` and the guide call
  `AddDefaults()` first. Calling `AddDefaults()` after Parallel producers lets `ClearAll`
  race with `AddPar` writers.
  - `README.md:1074-1077` and `AGENTS.md:1030-1033` ("place consumers before clearing") do
    not describe the `WorldInstaller` flow, where clearing runs first.
  - The guide (EN `:379-385`) is accurate.
- **N6 (Low) Stale `src/Reactive/` references.** The directory was deleted, but it is still
  referenced at `AGENTS.md:46`, `:163-166`, `:398`; `README.md:1116`;
  `ARCHITECTURE.md:168`.
- **N7 (Low) Stale dates.** "Updated 2026-09-08" remains at `AGENTS.md:5`,
  `ARCHITECTURE.md:3` and `src/Systems/FnSystems/RuntimeQuery/README.md:3`.
- **N8 (Low) Outdated AGENTS.md wording.**
  - `AGENTS.md:22` says `Entity (int ID)`.
  - `AGENTS.md:38` says "3 thread modes" (there are four).
  - `AGENTS.md:1069` says `AddManaged`; it should be `AddResManaged`.
- **N9 (Low) Inline-destroy history is out of date.** POST_1_0.md #12 is still marked
  BLOCKING and names the removed `EntityDestroyMTSystem`, and `AGENTS.md:251` says inline
  row removal was reverted. `DestroyNow` now removes the row inline.
- **N10 (Low) Handle-by-value advice contradicted by examples.** `README.md:433` says to
  keep handles by value, while examples keep `ref` handles (`README.md:409`;
  `AGENTS.md:715`, `:721`, `:730`).
- **N11 (Low) README hierarchy snippet** (`README.md:483-492`):
  - It uses an undeclared `entity` (`:489`).
  - It doesn't say that `SetParent` / `AddChild` flush the whole ECB through `AddArray`
    (`src/Entity/EntityArrayExtensions.cs:47-58`), so they are unsafe inside systems or
    iteration.
  - It doesn't say that `Destroy` / `DestroyNow` do not cascade to children; the cascade
    code in `Archetype.cs:1065-1072` has no callers.
- **N12 (Low) Incomplete transform list.** The README system list (`README.md:1161-1163`)
  omits `UpdateTransformOnAddChildSystem` (`TransformsGroup.cs:19`).
- **N13 (Low) Dead code.**
  - `ClearTransformsSystem` (`src/Unity/Transforms/Transform.cs:88-111`) destroys
    GameObjects of entities tagged `DestroyEntity`, but nothing adds the tag or registers
    the system, so GameObjects behind `TransformRef` are never cleaned up automatically.
  - An orphan doc comment remains at `Entity.cs:482`.
  - A commented-out line remains at `Systems.cs:170`.
- **N14 (Low) Misplaced paragraph.** In `README.md:585-592`, the new `WorldInstaller`
  paragraph sits between "Call it … entities:" and its code block.
- **N15 (Low) Undocumented patches.** `SourceGen/Patches~/` (diffs against the external
  generator repository) is undocumented, and the generator source is still outside the
  repository (POST_1_0.md #15).
