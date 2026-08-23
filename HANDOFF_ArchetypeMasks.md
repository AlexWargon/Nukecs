# Nukecs — Refactoring handoff: 3 маски архетипов + storage/logical разделение

> Сессия 2026-08-21. Документ для продолжения работы в новой сессии.
> Статус: **реализовано, тесты зелёные (211/211), Burst компилируется**.

## 1. Задача и принятая архитектура

**Задача**: разделить единую маску архетипа на 3 (inline / tags / pools), чтобы add/remove тега или
пул-компонента не двигал данные. Итерация обязана остаться плотной (без per-entity ветвлений).
Фильтрация — как раньше, per-archetype, в момент ECB playback.

**Реализовано (вариант "C", как в Bevy Table/Archetype):**

```
Архетип (identity)  = inlineMask + tagMask + poolMask → канонический union-hash → матчинг query
StorageArchetype     = inlineMask → владелец data/packedEntities/componentOffsets/offsetMap,
                       шарится между архетипами с одинаковым inline-набором (refCount)
ArchetypeUnsafe (LA) = маски + ptr→storage + rows (row-индексы членства) + queries
EntityLocation       = { archetypeIndex, row, listPos }   // 12 байт

Теги: 0 байт в данных, бит в tagMask. Тег = inline-компонент size==1 БЕЗ полей (GetFields().Length == 0).
      ВАЖНО: struct { bool IsDead } — НЕ тег (поле есть) → inline с данными.
Пулы: данные в GenericPool per-entity, бит в poolMask.
```

- Смена тега/пула = миграция rows-списков, **данные не копируются** (O(1)).
- Смена inline-компонента = move row между storage (memcpy только inline).
- Свап-ремув строки чинит loc.row + la.rows[listPos] перемещённого entity (`FixSwappedEntityLocation`).

## 2. Ключевые файлы

| Файл | Что сделано |
|---|---|
| `src/StorageArchetype.cs` (новый) | владелец данных; layout только по inlineTypes; AllocateRow/RemoveRowSwap/DestroyRowSwap; refCount |
| `src/Components/TagSlotStub.cs` (новый) | стабильный адрес для тег-слотов, **SharedStatic<T>** (Burst!) |
| `src/Archetype.cs` | 3 маски; Has(int) по категории; rows/listPos; MoveEntityTo с быстрым путём same-storage; probe+equality в GetOrCreate* (чинит латентные коллизии hash) |
| `src/World/World.Unsafe.cs` | storagesList/storagesMap; GetOrCreateStorage (probe + refCount); DumpArchetypes() — диагностический дамп |
| `src/Entity/Entity.cs` | Get/TryGet для тегов через TagSlotStub |
| `src/Entity/EntityCommandBuffer.cs` | ProcessEntityBatch через CopyMasksTo; быстрый путь тот же (внутри MoveEntityTo) |
| `src/Systems/FnSystems/Tuples/*.cs` | все 4 семейства (Ptr/EntityPtr/Ref/EntityRef × 9 арностей): AdvanceTo(row) delta-advance; пул-слоты gather из GenericPool; тег-слоты → TagSlotStub; TNIsTag/TNIsPool статики |
| `src/Systems/FnSystems/QueryIterators.cs` | все итераторы: dense-путь (refCount==1, pointer-increment) + gather по rows |
| `src/Systems/FnSystems/QueryIteratorsParallel.cs` | то же для parallel |
| `src/Systems/FnSystems/Chunk.cs` | 8 арностей Chunk — rows-режим |
| `src/Query.cs` | QueryEnumerator/2, GetEntity/GetEntities/First — по rows; MultiArray.AddGathered |
| `src/Components/ComponentTypeMap.cs` | ComponentCategory (Inline/Tag/Pool); категоризация |
| `src/Components/ComponentType.cs` | EnsureRegistered() вызывается из Index/Data геттеров (runtime-саморегистрация) |
| `src/World/World.SerializeAndSave.cs` | сериализация storages (формат сейвов изменился!) |
| `../../NUKECSGEN/SrcGen.*.cs` + `Assets/Nukecs/SourceGen/NUKECSGEN.dll` | генератор: batch-path `_row`-маппинг, категории, enumerator-генерация с gather/пулами |

**Деплой генератора**: `dotnet build` в `D:\Unity\NukecsSandbox\NUKECSGEN` →
`cp bin/Debug/netstandard2.0/NUKECSGEN.dll Assets/Nukecs/SourceGen/NUKECSGEN.dll`.

**Сборка для проверки**: `dotnet build Nukecs.csproj / Nukecs.Tests.csproj` в корне проекта
(csproj генерируются Unity; новые .cs добавлялись в csproj вручную — Unity перезапишет, это ок).

## 3. Механика итерации (dense/gather)

```csharp
_rows = arch.RowsAreDense ? null : arch.rows.Ptr;   // refCount <= 1 → null
// dense:  _tuple.Add()                        — pointer-increment (как до рефакторинга)
// gather: _tuple.AdvanceTo(_rows[++_listIdx])  — p += row - _curRow
```
Причина перемешки строк: аллокация в конец storage + swap-remove + миграции тегов не двигают строки.
`AdvanceTo` после `SetData`: `AdvanceTo(_rows[0])` (первая строка может быть ≠ 0).

## 4. Найденные и исправленные баги (важно помнить)

1. **Тег = size==1 БЕЗ полей** (`GetFields().Length == 0`). `ChainDeadFlag { bool IsDead }` — inline.
   Иначе все записи тега идут в одну глобальную заглушку (e1 видел запись e2).
2. **Генератор не регистрировал `: IPoolComponent`** — receiver-фильтр `Contains("IComponent")`
   не матчит "IPoolComponent" (там `lComponent`). Добавлен явный Contains. Вскрыл 100% потерю
   регистрации пул-типов → ComponentType<T>.Index == 0 (бит DestroyEntity).
3. **`ComponentType<T>.Index` теперь саморегистрируется** — EnsureRegistered() в геттерах
   (раньше тип вне GeneratedComponentList жил с Index=0 молча).
4. **QueryParamInfo<T1> никогда не заполняется для слота 1** (кэшируется только trailing TOption)
   → IsPool/IsTag слота-1 считаются без QueryParamInfo (`!T1IsEntity && category == ...`).
5. **Burst-правила** (выучено ценой 10k строк ошибок):
   - `[BurstDiscard]` ТОЛЬКО на void-методах (BC1015 если возвращает значение);
   - чтение non-readonly static полей в Burst запрещено (BC1040) → статик-адреса через SharedStatic<T>;
   - `typeof(X).IsAssignableFrom(...)` в Burst нельзя (BC1025) → `ComponentType<T>.Data.category`;
   - generic-итераторы/сгенерированный код из user-сборок не видят internal → `world`, `rows`,
     `RowsAreDense` на ArchetypeUnsafe сделаны public.
6. **Fluent-query (`world.Query().With<T>()`) видит только архетипы, созданные ПОСЛЕ него**
   (нет ленивого матчинга — пре-существующее поведение, не баг рефакторинга). Тесты обязаны
   создавать query до мутаций.

## 5. Известные ограничения / TODO

- `RefTuple`-семейство: пулы поддержаны; ObjectTuple (дашборд) — gather добавлен, но legacy.
- `Chunk.CopyTo` — memcpy подряд, невалиден в gather-режиме (не используется тестами).
- Сериализованные миры старого формата несовместимы.
- `Nukecs.Collision2D.csproj` сломан ДО рефакторинга (файлы перемещены в 7c54a65) — не наше.
- `Query.Count` инкрементальный через BatchMigrateQueries — кандидат на замену popcount'ом.
- Порядок итерации: LA-порядок; при storage-режиме (идея №1 ниже) станет storage-порядком.

## 6. Идеи оптимизации итерации (обсуждены, НЕ реализованы)

1. **Storage-режим для query без тегов/пулов в with/none** — итерировать весь storage подряд
   (все строки «свои»): matchingStorages + отдельный путь в итераторах/генераторе.
   Самый большой выигрыш — почти все query станут всегда плотными. ← кандидат №1
2. **Bitmap принадлежности per-LA** (вместо/рядом с rows): миграция = бит, Count = popcount
   мгновенно (выкинуть BatchMigrateQueries), swap = перестановка битов.
3. **Отсортированные rows** — forward-only прыжки, кэш/prefetch; цена O(n) вставка.
4. **Фоновая компакция** storage при пороге фрагментации — LA становятся contiguous-блоками.
5. **Чанкование storage** (как DOTS 16KB) — дешёвый EnsureCapacity, локальность; большая работа.

## 7. Тесты

- `UnitTests/TagPoolMaskTests.cs` (новый, 11 тестов): теги add/remove/churn, сохранность inline/pool
  данных при миграциях, gather-итерация mixed tags, swap-консистентность, None-фильтры,
  BatchCreate, пул в tuple (`iter()`, `iter_unsafe()`, прямой `foreach (ref var pc ...)`).
- Диагностика: `world.DumpArchetypes()` — при падении тест печатает архетипы/маски/rows/queries.
- Прогоны: XML в `UnitTests/Results/`; история: 195 → 204 → 207 → 209 → 209 → 210 → 211 зелёных.
- Транзитный артефакт: двойной `systems.OnUpdate` в тесте — уже исправлен (было 24 vs 12).

## 8. Соответствие с Bevy (для контекста)

## 9. Storage-mode queries (реализовано 2026-08-22, поверх §1)

**Идея №1 из §6 реализована**: query с inline-only `with`-фильтрами итерирует **целые StorageArchetype подряд** (dense, без gather). Все LA одного storage имеют одинаковый inlineMask → inline-фильтры матчат all-or-none.

- `QueryUnsafe.IsStorageMode()` — все with-биты inline (лениво, инвалидация в With/None).
- `GetMatchingStorages()` — лениво перестраиваемый список: `ContainsAll/ContainsNone` + **none-tag/pool биты дисквалифицируют storage при непустых LA** (prefab/dead варианты).
- **Деградация**: если хоть один storage дисквалифицирован — query откатывается на LA-путь (`UseStorageIteration()` → false). CheckQuery-аттачмент НЕ скипается (LA-бухгалтерия всегда жива).
- **Потокобезопасность**: rebuild под `world->spinner`; jobs используют `TryUseStorageIteration()` (только чтение, никогда не rebuild); main — `RefreshStorageMode()` в сгенерированном Schedule перед диспетчем. NRE-гонка parallel ECB лечится именно этим.
- `logicalArchetypes` backlink на StorageArchetype (4 места создания LA); `world->version++` в AllocateRow/RemoveRowSwap (инвалидация).
- Итераторы: storage-ctor + dense-проход у QueryIter/ParIter/WithEntity/T1/5/Ref5; `count`-property (storage-сумма или entityCount); First/GetEntity/GetEntities/Enumerators — storage-ветки.
- Генератор: storage-loop (seq+parallel) с уникальными `_s*`-именами переменных; `varToPtr` тоже `_sp{i}` (CS0841!).
- **Фикс ленивого матчинга**: inline-only query видят storage, созданные ДО запроса (питфолл №6 частично закрыт).
- Тесты: `UnitTests/StorageModeQueryTests.cs` (12). Итог: 222+ зелёных.

## 10. Перформанс-уроки managed-итерации (2026-08-22)

ДИАГНОСТИКА: `UnitTests/IterationDiagnosticsTests.cs` — послойная декомпозиция (managed iter / Burst+iter / raw for / fluent empty / CurrentOnly / DeconstructOnly / арности 2-3-4). Запускать одиночно, читать `[DIAG]` в логе.

Найдено (Mono, 100k entities, 4×float3):
- **Размер tuple решает**: копия `Current` = sizeof(tuple) НА КАЖДЫЙ entity. Рост tuple с ~80 до ~112Б (steps/poolActives/флаги) стоил +1.0 ms. Сжатие обратно к исходному набору полей вернуло基准у. Больше НЕ добавлять полей в Ref/PtrTuple.
- Форма Add (статик-ветки vs stride vs fast-path) — НЕ влияет (Mono не инлайнит, но ветки дёшевы; Ref≈Ptr при разных формах). Влиял только размер.
- `[BurstCompile]` на системе с `iter()` — не меняет ничего (fallback исполняется managed).
- `.Get/.Read` (ref-return property) + float3-операторы в телах — ~1.7 ms нативной Mono-стоимости, не лечится без API-ломки.
- `Deconstruct` — дёшев (+0.1 ms).
- Итог бенчей: iter-семейство 2.36–2.38 (база 2.10–2.25), par_iter 0.43 (лучше всех прошлых), batch `_Main` 1.75 (база 1.70, шум), `_Main_Burst__Run` 0.164.
- Кандидат следующего шага: «View-Current» — лёгкий 32Б-вид вместо полного tuple в `Current` (оценка −0.3…−0.5 ms).
- **View-Current — ПРОБОВАН, провалился (2026-08-22)**: итератор с прямыми полями-указателями + 32Б RefView в Current оказался на **+0.2 ms МЕДЛЕННЕЕ** tuple-итератора в честном A/B одной сессии (tuple 2.32 vs view 2.53, тела одинаковые). Даже с полностью безветочным инлайн-hot-path. Mono оптимизирует привычный паттерн tuple+constrained-calls лучше ручного ref-struct с большим числом полей. Откачено; сжатый tuple-итератор — оптимум managed iter. Диагностический слой `iter4 TUPLE direct` в IterationDiagnosticsTests оставлен для будущих A/B.

## 12. Структурные изменения: база и вердикт по bitmap (2026-08-23)

**Новая база структурных бенчей** (EcsBenchmark fixture, зафиксирована — исторической нет):
```
Migration_AddRemove_10K      0.274 ms   ECB_AddComponent_10K        0.344 ms
ECB_RemoveComponent_10K      3.395 ms   EntityCreation_10K          6.101 ms
EntityCreationBATCH_10K      0.403 ms   BATCH_10K_JobMainBurst      0.191 ms
BATCH_10K_JobSingleBurst     0.205 ms   RandomAccess_GetComponent   0.577 ms
```
Аномалия-кандидат на расследование: ECB_Remove 3.4 ms vs ECB_Add 0.34 (×10).

**q-чувствительность миграций** (MigrationDiagnosticsTests, 10k миграций/цикл, аттаченные queries):
```
q=0    7.94 ms
q=50   14.34 ms   (+81%)
q=150  28.29 ms   (+256% — 72% времени = q-бухгалтерия)
```
Линейно ~13.5 ns/query/миграция. Бенч-мир почти без queries (0.27 ms ≈ q≈0);
реальный проект с 50–150 системами платит ×2–3.5 на структурных изменениях.

**Вердикт (финал, 2026-08-23): bitmap НЕ нужен — закрыт pair-edge кэшем.**
`BatchMigrateQueries` переведён на edge-кэш пары (from→to): списки remove/add queries строятся
один раз (`FillPairEdge`), применяются линейно per entity; инвалидация — `queriesVersion`
(бамп при attach в CheckQuery/PopulateQueries и в Refresh). Кэш: `pairEdges: HashMap<long, ptr<Edge>>`
на LA, ключ `(to.index << 32) | from.index`. Ускорились оба пути: ECB playback и SetArchetype.
Результаты (10k миграций/цикл): квадрат 28.3 ms (q=150) → **8.0 ms = структурный пол** при
любом q (списки перехода пусты при полном пересечении; при частичном — линейно: ~13 ms vs 28.3).
Бенч Migration_AddRemove_10K: 0.28 (база 0.274). Микробенч стратегий: current 92.6 / edges 1.9 / bitmap ~0 (Mono, q=150).

**Важный подтверждённый pitfall**: fluent-query, созданный ПОСЛЕ существующих архетипов,
к ним НЕ аттачится (Count растёт инкрементально, но LA-итерация их не увидит) — pitfall №6,
теперь виден в счётчиках MigrationStats. Правильный порядок: queries до спавна entities.
Storage-mode queries от этого спасены (GetMatchingStorages сканирует лениво).

Diagnostics: тесты `MigrationDiagnosticsTests` (q-чувствительность мира + current-vs-edges-vs-bitmap
микробенч стратегий). Временные инструменты из кора удалены после исследования (MigrationStats-
счётчики, QueryBookkeepingBypass-выключатель): разовая диагностика не живёт в горячем пути
фреймворка.

**Десериализация pairEdges**: восстанавливается честно (не пересоздаётся) — по образцу старых
transactions: ДВА фиксапа на entry: `ptr<Edge>.OnDeserialize` + `Edge.OnDeserialize(alloc, world)`
(внутренние списки addEntity/removeEntity + их ptr<QueryUnsafe> — один фиксап ptr<Edge> НЕдостаточен).
Тест MigrationAfterLoad сериализует МИГРАЦИЮ (кэш непуст) и проверяет пост-лоад миграцию.

**Pitfall managed-Query-обёрток после Deserialize** (диагностировано через этот тест): обёртка
держит старый queryUnsafe* (арена переехала на новые адреса). `RestoreIfNeed()` чинил только
GetEnumerator — Count/IsEmpty читали freed-память → флейки («первый раз упал, повтор прошёл» —
freed-память иногда содержит старое значение, иногда мусор). Теперь RestoreIfNeed вызывается
в Count/IsEmpty/CountMulti (быстрый путь — int-сравнение version).

## 11. Прочее из этой сессии

- `GetComponentLocalIndex/GetComponentOffset` на StorageArchetype — **public** (генерированный код в user-сборках, питфолл №5).
- `ObjectTuple` — не трогать (legacy). `Chunk.CopyTo` — валиден ТОЛЬКО для арности 3 (исправлен
  2026-08-23: gather-режим копирует поэлементно по rows через _base-указатели; было memcpy подряд
  → мусорный рендер в FillRenderDataSystem при >~100 спрайтов, когда rows становились несмежными
  из-за Culled-миграций). Арности 1–2, 4–8: CopyTo ВСЁ ЕЩЁ сломан в gather-режиме — чинить по
  образцу 3-арной при первом использовании.
- Генератор: `fullData.{queryParam}->_query.Ref.RefreshStorageMode()` в Schedule (обёртка → QueryUnsafe).
- Скриптовые массовые правки tuple'ов ДВАЖДЫ портили файлы — правило: back-walk по атрибутам только `[MethodImpl` (поля с `[NativeDisable...]` перед методом съедались); обязательны ассерты целостности полей и билд-верификация после каждого прогона.
- **Burst-правило для diagnostics-инструменталки** (выучено дважды за сессию: QueryBookkeepingBypass, затем MigrationStats): любые переключатели/счётчики, достижимые из Burst-джоб (ECB playback! Edge.Execute! Systems) — только `SharedStatic<T>` (флаги, читаемые/пишемые из Burst) или `[BurstDiscard]`-методы (счётчики, исчезающие в Burst) — С ПЕРВОГО ДНЯ. Plain static поле ломает Burst-компиляцию ВСЕХ джоб, до него дотягивающихся (молчаливый managed-fallback → все последующие замеры втихую становятся Mono).

## 8a. Bevy-контекст (продолжение)

Table=StorageArchetype, Archetype=LA (маски), ZST-теги не в таблице, SparseSet≈GenericPool/IPoolComponent,
archetype.entities()+entity_rows≈rows (у нас сразу row-индексы, Bevy — entity-id + мапа в таблице).
Разница: нам надо чинить rows чужих LA при swap-remove (FixSwappedEntityLocation), Bevy мапу не чинит.
