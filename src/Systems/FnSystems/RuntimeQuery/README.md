# Runtime query iteration

`Query<T...>.iter()` enumerates the complete matching query on the calling thread.
`Query<T...>.par_iter()` enumerates only `_range`, assigned by `Query.Update` for
the current job. It does not schedule jobs itself. Initialize/update the query
before calling `par_iter`; a default range is empty.

```csharp
foreach (var (position, velocity) in query.iter())
    position.Get.Value += velocity.Read.Value;

// Inside a system/job with an assigned query range:
foreach (var (position, velocity) in query.par_iter())
    position.Get.Value += velocity.Read.Value;
```

The runtime owns the iterator, traversal, and tuple implementations. These
checked-in C# types need no source generator. Explicit `.iter()` and `.par_iter()`
calls are excluded from source-generator batch rewriting; generated system
runners still call the runtime iterator. Plain `foreach (... in query)` retains
its previous binding and remains eligible for batch rewriting. `par_iter()` now
uses the runtime implementation for all arities; the temporary `iter_par()` name
has been removed.

## Storage and API contract

- Supports 1–8 data components, arbitrary inline/pool combinations, and the
  optional trailing filter. The ninth family accommodates eight components plus
  an option (or an Entity slot).
- Data-only inline tuples use a base address and relative offsets. Pool tuples
  use absolute addresses and cache page buffers. Page-table pointers are not
  cached, so a pool table can grow without invalidating cached page buffers.
- Tags and filter slots have no archetype payload. Compatibility tuple slots
  resolve to stubs. A trailing option may be omitted when deconstructing, as
  with the old `.iter()` tuples. Prefer `With<Tag>` / `None<Tag>` filters.
- Deconstructing `Query<Entity, ...>.iter()` or `.par_iter()` returns the first
  slot as an `Entity` value; use `entity.id`, `entity.Get<T>()`, etc. Component
  slots remain `Ref<T>`. This supports Entity plus 1–8 components, including
  shortened deconstruction that omits a trailing filter. Tuple properties such
  as `C0` still expose the underlying ref slot.
- Deconstruction is selected through extension overloads in `Wargon.Nukecs`.
  A generic first data parameter must have `where T : unmanaged, IComponent`;
  an entity query names `Entity` explicitly as its first parameter.
- `Current` is a value snapshot of addresses. Structural changes can still
  invalidate component buffers; this is not a stable entity handle.
- A block's row count is captured when entering it. Appending rows does not
  extend that active block's iteration.
- Ranged iteration follows matching-archetype order, including sparse logical
  rows. Full inline iteration may use physical storage traversal. Do not assume
  their iteration orders are identical across shared storages.
- Concrete return types are now `QueryRuntimeIterN<QueryRuntimeRefs<...>>`.
  Use `var` for iterators and tuples. Read access through the old `_p1...` names
  is retained; the old mutable tuple layout is not retained.

## Verification

`RuntimeQueryIntegrationTests` covers arities, storage combinations, filters,
empty/clipped ranges, and Entity/tag slots. `RuntimeQueryProductionRegressionTests`
ports the selected candidate's pool-page, snapshot, unequal-size, and all-16-mask
checks to the public `.iter()` API. `RuntimeQueryJobIntegrationTests` covers
scheduled disjoint ranges and Main/Single/Parallel system runners.

Component classification is initialized during managed component registration
and exposed through SharedStatic metadata. Reflection is confined to a
Burst-discarded method, while iterator specialization keeps its readonly flags.

The native Burst probe first verifies an independent baseline function pointer
actually executes Burst. It is skipped if the Editor only supplies managed
fallback. A managed pass is not evidence of native Burst or IL2CPP compatibility.
Six additional probes check native Burst compilation for mixed, inline, pool,
Entity/tag (full and ranged), and eight-component-plus-filter queries through
the Editor's disassembly service. `RuntimeQueryEntityDeconstructionTests` covers
Entity plus 1–8 components and filters. The latest verified correctness run
(`entity-deconstruct-final-1`) passed all 78 tests, including native execution
with Entity copied by value. No performance comparison was made for this change.
