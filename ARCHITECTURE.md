# Nukecs — Architecture for AI Agents

> Этот документ — ментальная модель и причины дизайн-решений. Справочник API — в AGENTS.md.
> История оптимизаций с замерами — в HANDOFF_ArchetypeMasks.md.
> Читай этот файл ПЕРЕД правками ядра (src/). Он написан ценой реальных багов.

## 1. Ментальная модель (5 слоёв)

```
World ─── archetypesList[] ─── ArchetypeUnsafe (LA)     ЛОГИКА: маски + queries + rows
              │                    │ storagePtr
              │                    ▼
              └── storagesList[] ── StorageArchetype    ДАННЫЕ: SoA-колонки, packedEntities
                                      ▲
              refCount = число LA над storage           shared по inlineMask
```

**Ключевая идея (вариант "C", как Bevy Table/Archetype):**

- **Identity** архетипа = `inlineMask + tagMask + poolMask` (union-hash) → матчинг query.
- **StorageArchetype** владеет данными, шарится между всеми LA с одинаковым inline-набором.
  Теги = 0 байт (бит в tagMask); пулы = данные в GenericPool (бит в poolMask).
- **Смена тега/пула** = миграция rows-списка, данные не копируются (O(1)).
- **Смена inline-компонента** = move row между storage (memcpy inline-колонок).

`LA.count == rows.length`; `storage.count` = все строки ВСЕХ LA над ним.
`RowsAreDense == (refCount <= 1)` — итератор dense/gather решает это.

## 2. Структурное изменение (путь `e.Add<T>()`)

```
e.Add → ECB.Add (deferred) → world.Update() → Playback
  → ProcessEntityBatch:
      target = GetOrCreateArchetype(union-маска)      // hash + probe
      MoveEntityTo(row, target)                        // данные: same-storage O(1) / cross-storage memcpy
      BatchMigrateQueries(from, to, entity)            // pair-edge кэш ↓
```

**pairEdges** (`ArchetypeUnsafe.pairEdges: HashMap<long, ptr<Edge>>`, ключ `(to.index<<32)|from.index`):
списки remove/add queries строятся один раз на пару (`FillPairEdge`), применяются линейно.
Инвалидация — `queriesVersion` (бамп при attach в CheckQuery/PopulateQueries и в Refresh).
НЕ перечисляй queries с линейным Contains на entity — это квадрат (был баг: ×3.5 при 150 queries).

**Порядок создания решает**: query, созданный ПОСЛЕ архетипов, к ним НЕ аттачится
(Count растёт, но LA-итерация их не увидит). Правильный порядок — queries до спавна.
Storage-mode queries спасены (сканируют storages лениво).

## 3. Итерация (три пути)

| Путь | Когда | Механика |
|---|---|---|
| **batch storage-loop** | прямой `foreach (var (a,b) in query)` в [System] | генератор: pointer-walk по storages (`base++` до sentinel `end`, тела через `->`), walkers в отдельных методах. Perf-контракт генератора — AGENTS.md §"Generated Batch Loops - Performance Contract". Managed 1.63 / Burst 0.164 ms (100k×4×float3) |
| **storage-mode** | query с inline-only with-фильтрами, итераторы `iter()`/фабрики | dense-проход по `GetMatchingStorages()`; деградация на LA-при конфликте none-тегов (prefab/dead). None-filter бенчи: деградация при 10% помеченных = +3–4% общего времени; gather = +16% per-entity (16.3→18.7 ns, константа, не зависит от перемешанности); итерация масштабируется с матчащими (50% → 0.93 ms от 1.63) |
| **enumerator** | `foreach (ref var e in q)` / generic-итераторы | QueryEnumerator2 / QueryIter: dense (`_rows==null`) или gather (`AdvanceTo(rows[i])`) |

**Cost-model managed-итерации (Mono, 100k×4 компонентов)** — выверено замерами:
- enumerator-протокол ~0.24 ms + tuple-механика ~0.5 ms + тела `.Get/.Read` ~1.7 ms ≈ 2.36 ms.
- batch pointer-walk 1.63 ms = hand-written потолок (indexed+guards давали 1.80).
- **Размер tuple решает**: `Current` копирует всю структуру на каждый entity. +30Б ≈ +1 ms.
  НЕ добавляй поля в Ref/PtrTuple (все 35 сжаты к минимуму после инцидента 3.3ms).
- Форма Add (статик-ветки vs stride vs fast-path) на скорость НЕ влияет. Только размер.
- `[BurstCompile]` на системе с `iter()` ничего не меняет (fallback исполняется managed).
- View-Current (лёгкий 32Б Current) ПРОБОВАН — медленнее tuple на +0.2 ms в A/B. Не повторять.

## 4. Инварианты (нарушение = порча данных)

1. `rows` LA ↔ `entityLocations.listPos` ↔ `storage.packedEntities` — согласованы всегда.
   Swap-remove чинит `FixSwappedEntityLocation` (row + чужие LA rows).
2. `queriesVersion` бампится при КАЖДОМ изменении набора attached queries — pairEdges валидны.
3. `world->version`++ при row alloc/remove — инвалидация storage-mode снапшотов.
4. Десериализация: pairEdges восстанавливается ДВУМЯ фиксапами (`ptr<Edge>` + `Edge.OnDeserialize`
   для внутренних списков). Managed-Query-обёртки чинит `RestoreIfNeed` (теперь и в Count).
5. Burst-пути не читают non-readonly static → diagnostics = `SharedStatic<T>` или `[BurstDiscard]`.
   Plain static в достижимом из Burst коде = silent managed-fallback ВСЕХ затронутых джоб.
6. Счётчики query (`entityCount`) обновляются ТОЛЬКО через pair-edge списки — больше нигде.

## 5. Карта кода (что внутри и что важно)

| Файл | Суть |
|---|---|
| `src/Archetype.cs` | ArchetypeUnsafe: маски, rows, pairEdges, BatchMigrateQueries/FillPairEdge, CheckQuery/PopulateQueries. Edge — списки remove/add + версии |
| `src/StorageArchetype.cs` | Данные: колонки SoA, packedEntities, refCount, logicalArchetypes (backlink LA→storage для storage-mode) |
| `src/Query.cs` | QueryUnsafe: with/none маски, matchingArchetypes (LA-путь), matchingStorages (storage-путь, лениво под spinner), IsStorageMode/UseStorageIteration/TryUseStorageIteration, count-property |
| `src/World/World.Unsafe.cs` | Реестры: archetypesList/storagesList/GetOrCreate* (hash+probe), GetOrCreateStorage (refCount++) |
| `src/Systems/FnSystems/QueryIterators*.cs` | Все итераторы: dense/gather/storage-ветки. Current — readonly, БЕЗ ref-return (Unity Roslyn CS8170) |
| `src/Systems/FnSystems/Tuples/*.cs` | 35 tuple-структур, СЖАТЫХ к минимуму полей — не раздувать (§3) |
| `src/Systems/FnSystems/Chunk.cs` | Chunk-итераторы: CopyTo валиден ТОЛЬКО arity-3 (gather: поэлементно по rows); 1–2, 4–8 — чинить по образцу при первом использовании |
| `src/Entity/EntityCommandBuffer.cs` | Playback: ProcessEntityBatch — миграции + pair-edge учёт |
| `SourceGen/NUKECSGEN.dll` | Генератор: batch storage-loop = pointer-walk (см. perf-контракт в AGENTS.md — indexed доступ/`_rowsPtr`-ветка в теле цикла запрещены, walkers отдельными методами БЕЗ AggressiveInlining), RefreshStorageMode в Schedule |

## 6. Диагностика (инструменты в тестах, НЕ в ядре)

- `UnitTests/IterationDiagnosticsTests.cs` — послойная декомпозиция итерации (`[DIAG]`-лог).
- `UnitTests/MigrationDiagnosticsTests.cs` — q-чувствительность миграций + A/B стратегий.
- `world.DumpArchetypes()` — дамп масок/rows/queries/storage при падении теста.
- Принцип: разовая диагностика не живёт в горячем пути фреймворка (удалены MigrationStats,
  QueryBookkeepingBypass после исследования).

## 7. Правила работы с кодовой базой

- Массовые правки скриптами: только построчные трансформы + ассерты целостности + билд после
  КАЖДОГО прогона. Back-walk по атрибутам — только `[MethodImpl` (поля с `[NativeDisable...]`
  перед методом съедались дважды).
- Сборка для проверки: `dotnet build Nukecs.csproj / Nukecs.Tests.csproj` в корне проекта.
- Бенчи помечены `[Category("Benchmark")]` — Run All в Unity без них через фильтр категорий.
- Замеры между сессиями несопоставимы (±10–20% шум) — A/B только внутри одного прогона.
