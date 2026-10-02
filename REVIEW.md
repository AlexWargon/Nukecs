# Nukecs — documentation review

Reviewed revision: `dev` @ `089f73a` (2026-10-02). An earlier pass was done on
`Storage-Rework` @ `38b4add` (2026-09-08); items fixed since then are listed in §8.

## Follow-up status (2026-10-02)

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

**Method.** README.md, AGENTS.md, ARCHITECTURE.md, NUKECS_AGENTS_GUIDE_EN/RU.md and
`src/Systems/FnSystems/RuntimeQuery/README.md` were compared with the source by reading.
Nothing was compiled or run in Unity. The source generator ships only as
`SourceGen/NUKECSGEN.dll`, so generator behaviour was inferred from the DLL's strings,
call sites and tests. Items marked **(to verify)** are inferred, not confirmed.

**Severity:** **High** — the example does not compile or describes wrong behaviour;
**Medium** — misleading or a missing step; **Low** — inaccurate detail or cosmetic.

## Summary

| Area | Score |
|---|---|
| Core ECS (components, systems, queries, ECB, resources), including the new gameplay guide | 8/10 |
| README.md accuracy on its own | 6/10 |
| ARCHITECTURE.md | 7.5/10 |
| Building a complete game from the docs | 5/10 |

What works well:

- `NUKECS_AGENTS_GUIDE_EN.md` is the most useful document: a correct owner template
  (`OnStart()`, `Dispose()` → `DisposeStatic()` order), thread-mode guidance, event
  patterns and memory rules. Its examples match the source.
- `POST_1_0.md` openly lists known limitations.
- ARCHITECTURE.md explains the storage model and invariants in depth.
- Most API signatures shown in README exist as documented.

Main problems:

- README.md was barely updated in the 1.0 pass: 15 of 20 issues from the previous
  review remain, and two README snippets no longer compile because the code changed
  (`[System(Threads.X)]`, `SaveRes<T>`).
- AGENTS.md and ARCHITECTURE.md contradict the source or README in places
  (`DestroyNow`, batch-generation rules).
- No installation section; nothing on rendering, GameObject linking or input.

## 1. README.md

### High

1. **`[System(Threads.Main)]` no longer compiles.** `README.md:464-465`, `README.md:1006`.
   `SystemAttribute` (`src/Systems/Systems.cs:1004`) lost its `Threads` constructor in
   the 1.0 pass → CS1729. The thread mode comes only from `Systems.Add(method, Threads)`
   / `AddSystems`. Remove the attribute-argument form and document mode selection via `Add`.
2. **`SaveRes<T>` section documents a deleted type.** `README.md:955-968`. The type was
   removed (`src/Systems/FnSystems/Res.cs:85`). Delete the section.
3. **`Local<T>` example does not compile.** `README.md:976-980` uses `Local<MyState>` and
   `local.Value`. Actual: `Local<TData> where TData : unmanaged, IRes` with field `Ref`
   (`src/Systems/FnSystems/Single.cs:136-138`). See also §7.4.
4. **`GetRootParent` example does not compile.** `README.md:451`
   `ref var root = ref entity.GetRootParent();` — the method returns `Entity` by value
   (`src/Entity/EntityChildrenExtensions.cs:50`) and returns `Entity.Null` when there is
   no parent.
5. **Event lifetime is described wrongly.** `README.md:1020` says events persist until
   explicitly cleared. `AddDefaults()` registers `DefaultSystems.ClearEvents`
   (`src/Systems/Systems.cs:160`, `src/BuiltInSystems.cs:93-95`), which clears every
   event buffer on each update, and `WorldInstaller` always calls `AddDefaults()`
   (`src/Unity/WorldInstaller.cs:25`). The gameplay guide (§3) explains this correctly;
   README should too.
6. **Transform sync is off by default and README doesn't say how to enable it.**
   `README.md:1103` ("Built-in Transform Systems"). The systems are static methods in
   `TransformsGroup` (`src/Unity/Transforms/TransformsGroup.cs:6`) and are registered
   neither by `WorldInstaller` nor by `AddDefaults()`; users must call
   `Systems.AddGroup(new TransformsGroup())`. Sync is one-way and reads only world-space
   `Transform` + `TransformRef` (`TransformsGroup.cs:72`), while both Quick Starts use
   only `LocalTransform`, so nothing visible moves.
7. **`EntityCreated` is never added.** `README.md:374` says it is added to new entities
   and cleared each frame. Nothing adds it (`src/Components/Component.cs:70` only defines
   it), and the clearing system's registration is commented out
   (`src/Systems/Systems.cs:161`).

### Medium

8. **`SetParent` direction is reversed.** `README.md:448` `parent.SetParent(childParent)`.
   In `SetParent(this ref Entity entity, Entity newParent)` the receiver is the child
   (`src/Entity/EntityChildrenExtensions.cs:44`). It should read `child.SetParent(parent)`.
9. **Start systems under `WorldInstaller` never run.** `WorldInstaller` does not call
   `Systems.OnStart()` (`src/Unity/WorldInstaller.cs:19-35`). README explains `OnStart`
   in the lifecycle section (`README.md:537-547`), but neither Quick Start mentions it,
   and Quick Start #1 says the installer handles "default systems". Either call
   `OnStart()` in the installer or say so in the Quick Start.
10. **The hot-reload sample skips `AddDefaults()`.** `README.md:1166`
    `new Systems(ref world)` — so no `OnPrefabSpawn` or
    `ClearEvents`. The demos follow the same pattern.
11. **Deferred copy name.** README used the obsolete misspelled alias. Use
    `CopyViaECB`; the alias has now been removed.
12. **Prefab example order (to verify).** `README.md:436-441` adds `IsPrefab` (deferred
    through the ECB) and immediately calls `SpawnPrefab`, which copies at once
    (`src/World/World.Unsafe.cs:503`). `EntityPrefabMap` calls `world.Update()` before
    spawning. Without a playback the prefab may not yet have its components.
    `OnPrefabSpawn`, which strips `IsPrefab` from copies, exists only with `AddDefaults()`.
13. **Stale date.** `README.md:126` says the API is described "as of 2026-09-08", before
    the 1.0 stabilization changes.

### Low

14. **`Threads` enum order.** `README.md:487-490` lists `Main, MainRun, Single, Parallel`;
    the actual order is `Main, MainRun, Parallel, Single` (`src/Systems/Systems.cs:873`).
15. **`WorldConfig` capacity table.** `README.md:1206-1217` shows round numbers. Actual
    values: `Default1024` = 1025, `Default16384` = 16385, `Default163840` = 163841,
    `Default256000` = 256001, `Default_1_000_000` = 1,000,001. Tracked in POST_1_0.md #18.
16. **`ResManaged` example.** `README.md:945` doesn't say the resource must be a `class`
    (`AddResManaged<T> where T : class, IRes`).
17. **Event type example.** `README.md:989` implements `IComponent`; `Events<T>` only
    requires `unmanaged`.
18. **Batch inspection snippet.** `README.md:252` uses an undeclared `updateSystems`, and
    `Systems.Runners` returns only the Update list (`src/Systems/Systems.cs:43`).
19. **Event iteration copies.** `foreach (var evt in events)` copies each event;
    `foreach (ref var evt in events)` is supported and avoids the copy.
20. **Silent no-ops are undocumented.** `entity.Set<T>` does nothing if the component is
    absent, `entity.Add<T>` does nothing if it is already present (`src/Entity/Entity.cs:177`),
    and `TryGet` returns a null reference when the component is absent. One line each
    would help.
21. **Fixed update is undocumented.** The interval is hardcoded to 16 ms, with at most
    one fixed tick per `OnUpdate` (`src/Systems/Systems.cs:28`, `:131-142`).
22. **`Entity` equality.** Equality now includes `worldIndex`; this is not mentioned.

## 2. AGENTS.md

### High

1. **`DestroyNow` is described as immediate.** `AGENTS.md:747`, `AGENTS.md:1000`. The
   source (`src/Entity/Entity.cs:415`) only enqueues `ECB.Destroy`, which is identical to
   `Destroy()`; its own XML doc says so, and inline destroy is postponed
   (POST_1_0.md #12). README (`README.md:419-424`) is correct, so AGENTS.md contradicts
   both.
2. **`[System(Threads.Main)]` in the Events section no longer compiles.** `AGENTS.md:814`,
   `AGENTS.md:838` — see §1.1.
3. **§16 `SpawnSystem` uses a missing field.** It uses `config.Ref.timer`
   (`AGENTS.md:546`), but `ConfigData` has no `timer` field.
4. **§16 `RotateSystem` has an ambiguous `Transform`.** `Get<Transform>()`
   (`AGENTS.md:622`), with the listed usings (`Wargon.Nukecs.Transforms` and
   `UnityEngine`), is CS0104. Use `using Transform = Wargon.Nukecs.Transforms.Transform;`
   as `src/Unity/WorldInstaller.cs:6` does.
5. **§16 `GameObjectView` is missing `using System;`.** `GameObjectView : IComponent,
   IDisposable` (`AGENTS.md:453`) needs it, but adding it makes `Object.Destroy`
   (`AGENTS.md:460`) ambiguous; use `UnityEngine.Object.Destroy`.

### Medium

6. **"Events — Must Clear Manually"** (`AGENTS.md:1018-1029`). With `AddDefaults()` the
   buffers are cleared every update anyway; reconcile this section with guide §3.
7. **Contradicting `Res<T>` advice.** `AGENTS.md:1057` says not to access `Res<T>` from
   MonoBehaviours, while the pausing example at `AGENTS.md:1134` does exactly that.
8. **Entity creation snippet redeclares `e`.** `AGENTS.md:707-729` declares `e` several
   times in one block (CS0128 if pasted as is).
9. **Generator source path is outside the repository.** `../../../NUKECSGEN/`
   (`AGENTS.md:12`, `AGENTS.md:287`), so contributors cannot find it (POST_1_0.md #15).

## 3. ARCHITECTURE.md

1. **Stale batch-generation rules (Medium).** `ARCHITECTURE.md:81-93` says the `foreach`
   must be the only top-level statement and that even `var dt = ...` before the loop
   disables batching. Since the 1.0 pass the generator preserves surrounding code
   (`README.md:229-246`, AGENTS.md §7, guide §2,
   `UnitTests/BatchRewriteRegressionTests.cs`). The guide sends readers to
   ARCHITECTURE.md, so they get conflicting rules.

## 4. NUKECS_AGENTS_GUIDE_EN.md / NUKECS_AGENTS_GUIDE_RU.md

The EN and RU versions are equivalent (code blocks are identical; only comments are
translated), so each item applies to both.

1. **Missing reference file (Medium).** The guide references
   `Assets/Game/Scripts/EcsTest.cs` (EN `:5`, `:112`), which is not in this repository.
2. **Burst attribute on Main systems (Medium).** `Initialize` (EN `:85`) and
   `MovementEvents.Consume` (EN `:349`) are `[System, BurstCompile]` but registered with
   `Threads.Main` (EN `:55`, `:137`, `:364`), where Burst is not used. This contradicts
   the guide's own §2 (Burst → MainRun/Single/Parallel; Main → managed code without
   Burst). Drop the attribute or register them with `MainRun`.
3. **`ComponentArray` pitfalls are missing (Medium).**
   - `AddArray` flushes the whole ECB (`src/Entity/EntityArrayExtensions.cs:52`), so it
     must not be called inside systems or during iteration.
   - `AddArray`, `GetArray` and `RemoveArray` take `this ref Entity`, so they cannot be
     called on a `foreach` iteration variable.
   - `GetArray` throws when the array is missing (`EntityArrayExtensions.cs:21`).
   - `DEFAULT_MAX_CAPACITY` is `internal` (`src/Components/ComponentArray.cs:12`), so
     gameplay code cannot read it.
   - The 15-vs-16 capacity quirk (`ComponentArray.cs:104`) is better fixed in code than
     documented.
4. **RU formatting (Low).** Blank lines are missing before `## 2.` and before the
   "Fluent query" paragraph.

## 5. Missing documentation

1. **Installation.** Unity version; required packages (Burst, Collections, Mathematics,
   Jobs); the TriInspector dependency (`src/Nukecs.asmdef:8`); how to add the analyzer
   DLL; how to enable `NUKECS_DEBUG`. There is no `package.json` (POST_1_0.md #13–14).
2. **GameObject ↔ entity.** `EntityBaker` (`src/Unity/EntityBaker.cs`), `EntityLinkSO`
   (`src/Unity/EntityLinkSO.cs`), `EntityPrefabMap`, `WorldBaker`, the
   `Convert(UnityEngine.Transform, ...)` helper (`src/Unity/Transforms/Transform.cs:27`),
   `TransformRef` and `GameObjectRef` are undocumented. `EntityLink` and
   `WorldInstaller.ConvertEntities` are commented out (`src/Unity/WorldInstaller.cs:32`,
   `:48-56`). A short "how to show an entity on screen" section is the biggest gap for
   game developers.
3. **Rendering.** Nothing is built in. The demos use `Graphics.RenderMeshInstanced`
   (BoidsDemo, CubeSculptureDemo); point readers to them.
4. **`AddDefaults()` / `Systems.Default(ref world)`** (`src/Systems/Systems.cs:151-160`)
   are not mentioned in README.
5. **`ISystem`, `IEntityJobSystem`, legacy `SystemsGroup`.** README is silent, while the
   guide calls them legacy. State their status in README (supported or deprecated).
6. **Other undocumented API.** `world.GetSingleton<T>()` (`src/World/World.cs:157`),
   aspects (`IAspect`, `GetAspect<T>`), `entity.AddObject` / `SetObject`,
   `world.GetEntity(id)`.
7. **Demos.** There is no README and no scenes; explain how to run each demo.
8. **Physics, input, UI, audio.** None are provided; say so explicitly so users don't
   search for them. The `Input` component (`src/Components/Component.cs:93`) is unused.

## 6. Code and packaging issues noticed during the review

1. **(High) Player builds may fail.** `src/Singleton.cs:8` has an unguarded
   `using UnityEditor;` in the runtime assembly (`src/Nukecs.asmdef`, no platform
   restriction). Not tested in a build.
2. **(High) `World.Load` ignores its data.** `World.Load(WorldConfig config, byte[] data)`
   (`src/World/World.Static.cs:171`) never reads `data` and just creates an empty world.
3. **(Medium) Unimplemented system parameters.** `Single<T1>` and `MutRes<T>` methods
   throw `NotImplementedException` (`src/Systems/FnSystems/Single.cs:27-37`, `:72-82`),
   yet `Single<T>` is documented as a system parameter (README, AGENTS.md §8).
4. **(Medium, to verify) `SystemsGroup` list mapping looks scrambled.**
   `Systems.Add<T>(T group) where T : SystemsGroup` (`src/Systems/Systems.cs:291-297`)
   maps `runners` → `onStart`, `fixedRunners` → `onUpdate`, `mainThreadRunners` →
   `onFixedUpdate`, `mainThreadFixedRunners` → `onDestroy`.
5. **(Low) Stray component.** `public struct Cube : IComponent` sits in
   `src/Unity/WorldInstaller.cs:63`; RotateCubeDemo depends on it.
6. **(Low) `WorldInstaller.Awake` is private and non-virtual** (`WorldInstaller.cs:19`).
   A subclass that defines its own `Awake` silently hides it. It also calls
   `World.DisposeStatic()` (POST_1_0.md #17).
7. **(Low) Name clash.** The class `Wargon.Nukecs.Transforms.Systems`
   (`src/Unity/SyncTransformsSystem.cs:5`) clashes with `Wargon.Nukecs.Systems` when both
   namespaces are imported.
8. **(Low) Stale archives.** `SourceGen/NUKECSGEN.zip` and `SourceGen/NUKECSGEN2.zip`
   contain old DLL builds (POST_1_0.md #16).

## 7. Open questions (to verify)

1. **Clarified: `Threads.MainRun` after `Threads.Parallel` writers.** `ExecuteSequentialUpdate`
   (`src/Systems/Systems.cs:685-710`) completes previous jobs only before `Threads.Main`
   runners. MainRun deliberately runs synchronously without a dependency argument.
   Complete prior jobs explicitly before accessing data they use.
2. **Pool-component `foreach` under `Threads.Parallel`.** With an `IPoolComponent` the
   batch rewrite is disabled. Does the fallback enumerator respect the job range? If not,
   every worker processes all entities; this affects the guide's `ApplyDamage` example.
3. **`IRes.OnCreate` timing.** It appears to be called from `Res<T>.Init` (once per
   consuming system), not from `AddRes`, so it may run several times or never.
4. **`Local<T>` / `Single<T>` generator support.** Neither is used anywhere in src, tests
   or demos.

## 8. Fixed since the previous review (`38b4add` → `089f73a`)

- **README async load.** `world = await World.LoadAsync(path, world)` is documented and
  matches `src/World/World.SerializeAndSave.cs`.
- **README `DestroyNow`.** The description matches the source; AGENTS.md does not (§2.1).
- **Sparse chunk `CopyTo`** works for all arities, and ARCHITECTURE.md is updated.
- **Removals.** `SaveRes` was removed from AGENTS.md, and the duplicate `src/Reactive/`
  was removed.
- **Generator diagnostics.** `[RequireBatch]`, `NUKECS002` and
  `ISystemCompilationInfoProvider` are documented accurately.
- **New documents.** The gameplay guide and POST_1_0.md were added. AGENTS.md lists the
  stabilization fixes; 11 were spot-checked and all are confirmed in the source.
