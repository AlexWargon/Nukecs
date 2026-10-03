# Handoff: оптимизация `query.iter()` для четырёх компонентов

## Текущий API и исправление Burst — 2026-09-07

Последнее уточнение пользователя заменяет прежнее имя `.iter_par()` на
`.par_iter()`. Все девять generic-сигнатур используют runtime iterator:
`.iter()` — полный query, `.par_iter()` — назначенный диапазон job.
Вызовы обновлены в integration tests и BenchNukecs.cs. Старого имени в C# Assets
больше нет. Старый конкретный return type `iter_refs()` сохранён.

В `D:/Unity/NukecsSandbox/NUKECSGEN/SrcGen.ForEachAnalysis.cs` отключён batch
rewrite явного `.iter()`; `.par_iter()` тоже остаётся runtime-вызовом.
Генерация system runners сохранена, обычный foreach по query по-прежнему
допускает batch rewrite. Генератор собран и SourceGen/NUKECSGEN.dll обновлён:
SHA256 `948C7568A2C5739639B14A14CBFFF4055CFF0AB57EC581BBB6F4D59B7F30BA1F`.
Проверка IL реального BenchTestSystems.Iteration4_Iter подтверждает вызовы
Query.iter / QueryRuntimeIter4; OnUpdateBatched вызывает OnUpdate без batch walker.

BC1025/BC1360 были вызваны reflection в static initializer QueryRuntimeSlot<T>.
Проверка IComponent перенесена в ComponentType<T>.EnsureRegistered, помеченный
BurstDiscard. Признак хранится в отдельном SharedStatic<byte>, без изменения
сериализуемого ComponentTypeData. Runtime specialization сохраняет readonly bool,
sizeof-адресацию и прямую деконструкцию выбранного быстрого кандидата.

Финальный прогон `runtime-burst-final-1.xml`: **60 passed, 0 failed, 0 skipped**.
Включает correctness для 1–8 компонентов, pools, tags, ranges, snapshots,
system runners, существующие TagPoolMaskTests, пять проверок машинного кода
Burst (mixed/inline/pools/Entity+tag/eight+filter) и реальное native выполнение.
В общий набор попали семь старых performance tests; их шумные результаты
не использовать как контролируемое сравнение скорости этого исправления.

При проверке обнаружен устаревший кэш сборок Burst: он ссылался на уже удалённый
QueryRuntimeManagedSlot. Временный Editor driver инвалидировал кэш через штатную
команду CompilerCommandDirtyAllAssemblies перед проверкой. После этого исчезли
старые диагностики и заработал native baseline. Это действие только для проверки,
оно не добавлено в runtime или постоянные тесты. Driver и revision удалены.
Артефакты XML, revision и sourcegen IL находятся в
`C:/Users/user/.codex/visualizations/2026/09/05/01a06fa4-56e2-74c2-b543-f3832c93fc87/`.
Предыдущие разделы ниже описывают историю и прежнее имя API.

## Исправление регресса при переносе — 2026-09-07

Пользователь заменил прямой RuntimeDispatch GetEnumerator на `.iter()` в AAAA
и получил avg/median 2.17 ms вместо 1.99 ms. В перенесённом hot path обнаружены
два отличия от выбранного кандидата: `static readonly int Stride` вместо прямого
`sizeof(T)` и деконструкция через N отдельных свойств/Ref-конструкторов.
Исправлены все арности: data-адресация снова использует sizeof-константы,
placeholder корректируется отдельно, Deconstruct записывает указатели напрямую
с одной проверкой представления tuple. Теги, фильтры и `.iter_par()` сохранены.

Проверка `runtime-regression-fixed-all.xml`: 48 correctness passed, native Burst
probe skipped; benchmark AAAA тоже passed, но этот общий прогон шумный
(median 2.2772, avg 2.4921, stddev 0.4623), его скорость не использовать как итог.
Первый точечный fix4: median 2.02695, avg 2.06228, stddev 0.15792.
После завершения компиляции, без изменения сборки: settled median 2.0453,
avg 2.0719, stddev 0.08891.

Итоговое парное сравнение `runtime-regression-paired.xml` в той же сборке:
AAAA `.iter()` median около 2.02 ms, исходный RuntimeDispatch-кандидат через
существующий AAAA_Overload median около 2.03 ms. Средние около 2.05 / 2.03 ms,
stddev около 0.08 / 0.05 ms. Регресс 9% в парном сравнении не воспроизводится;
не утверждать, что каждый запуск гарантированно даст прежние 1.99 ms.
BenchNukecs.cs и исходный кандидат не редактировались. Source-gen сохранён.
Повторные XML имеют revision `runtime-regression-fixed-all`, поскольку запускались
без новой компиляции. Файлы — в прежнем внешнем каталоге артефактов.

## Интеграция runtime API — 2026-09-07

Пользователь остановил эксперименты с производительностью и уточнил API:
однопоточный обход — `.iter()`, диапазон job — `.iter_par()`.
Новые замеры не запускались; пользовательские BenchNukecs.cs и source-gen не менялись.

- `src/Systems/FnSystems/Query.cs`: все девять generic-сигнатур теперь возвращают
  `QueryRuntimeIterN<QueryRuntimeRefs<...>>` из `.iter()`; добавлен `.iter_par()`.
  Поддержаны 1–8 data-компонентов с опциональным фильтром; девятая tuple-family
  нужна для восьми компонентов плюс option/Entity. Старый `par_iter()` сохранён.
- Runtime-код находится в `src/Systems/FnSystems/RuntimeQuery/`, без зависимости
  от UnitTests и без build-time source generator. Это перенос выбранной формы
  с отдельными типами по арности, а не новый эксперимент с производительностью.
- `.iter()` обходит полный query независимо от `_range`. `.iter_par()` соблюдает
  `_range`, установленный Query.Update; не запускает jobs самостоятельно.
  Диапазоны сохраняют порядок matching archetypes, в том числе sparse rows.
- Для inline оставлены relative offsets, для pools — абсолютные указатели и
  кэш страниц через стабильного владельца. Теги/фильтры не читают payload из
  архетипа. Завершающие placeholders имеют stride=0 и не отключают inline path.
  Entity в explicit `.iter()` возвращается как `Ref<Entity>`.
- Сохранены укороченная деконструкция без trailing option, readonly aliases
  `_p1...`, Current snapshots и count snapshots. Конкретный return type изменён;
  старую изменяемую структуру RefTuple новый Current не обещает.
- Обычный `foreach (... in query)` и существующие source-gen batch rewrites
  не перенаправлялись. `.iter_par()` работает через обычную system/job обвязку.
  SourceGen DLL SHA256 остался
  `1924AFC425616DFA64EAA9CE914F65723E86124B6116D070909CF5A1D4CF14FA`.

Финальный `runtime-integration-verified.xml`: **48 passed, 0 failed, 1 skipped**.
Проверены 1–8 inline/pool/mixed, диапазоны, sparse фильтры, Entity/tag slots,
все 16 четырёхкомпонентных масок, неодинаковые размеры, lazy pages, page-table
growth, Current/count snapshots, scheduled jobs и runners Main/Single/Parallel,
существующие QueryIter4Tests и TagPoolMaskTests.

Native Burst не подтверждён: независимый baseline function pointer без ECS-кода
тоже исполняется через managed fallback в текущем Editor. Новый native probe
по этой причине пропущен, а не объявлен успешным. Ранние integration-3/final
зафиксировали его ошибку до добавления независимой проверки доступности Burst.
IL2CPP не проверялся. Производительность после переноса на 1–8 не измерялась.
Следующая обязательная проверка перед релизом — native Burst в подходящей сессии
и IL2CPP; старые результаты скорости относятся к изолированному кандидату.

Документация контракта: `src/Systems/FnSystems/RuntimeQuery/README.md`.
Тесты: RuntimeQueryIntegrationTests, RuntimeQueryProductionRegressionTests,
RuntimeQueryJobIntegrationTests. XML/revision stamps — во внешнем каталоге
артефактов прежних запусков. Изолированные compact/dispatch/paged кандидаты сохранены.

## GetEnumerator без source-gen — финальный эксперимент 2026-09-07

Запрос пользователя: попробовать единый вручную написанный GetEnumerator без
source-gen специализации, пока только для четырёх data-компонентов. Сохранён
кандидат в `UnitTests/RuntimeQueryIter4DispatchExperiments.cs`; correctness —
`RuntimeQueryIter4DispatchTests.cs`, измерения — `RuntimeQueryIter4DispatchBenchmarks.cs`.
Предыдущие compact/page-cache кандидаты, runtime/src и source-gen DLL сохранены.
В PageCacheBenchmarks только открыт внутренний Run helper и добавлено необязательное
имя SampleGroup, чтобы переиспользовать одинаковое создание/проверку данных.

### Итоговая форма

- Один `RuntimeDispatchIter<RuntimeDispatchRefs<T1,T2,T3,T4>>` для inline, mixed и
  pool-only. Поддерживает все 16 сочетаний и пятый фильтр; теги не payload.
- Четыре static readonly признака Pool0..Pool3 определяются через ComponentType
  один раз для закрытого tuple-типа. Это неизменная категория типа, а не состояние
  мира. Init каждого iterator сверяет полную runtime-маску с признаками: изменение
  категории после инициализации tuple явно отвергается, а не меняет трактовку памяти.
- Для inline Current хранит базовый указатель + три относительных смещения;
  для mixed/pool — четыре абсолютных указателя. Формат фиксирован на закрытый
  tuple-тип; payload Current остаётся 32 байта на x64 и копируется по значению.
- Inline сохраняет Advance(end) с холодным sparse/archetype переходом. Pool-путь
  использует прямой Gather и кэш component buffers страниц через RuntimePageRows.
- Большого switch и отдельных методов на каждую pool-маску в финальном коде нет.
  Форма на типовых признаках компактнее и пригоднее для будущего переноса на 1–8;
  скорость этого переноса ещё не проверена.
- Обычный `foreach (var (a,b,c,d) in query)` находится в ручных RuntimeDispatchLoops
  без [System]. [System] harness только вызывает эти методы; одинаковая обвязка
  у контроля и кандидата. IL подтверждает вызов ручного GetEnumerator и runtime
  MoveNext, без сгенерированного QueryEnumerator и batch rewrite.

ВАЖНО: foreach binding сейчас изолирован пространством имён
`Wargon.Nukecs.Tests.RuntimeDispatch`. Это доказательство и тестовый кандидат,
не глобальная замена всех GetEnumerator проекта. При production-интеграции надо
согласовать существующие generated extensions/instance methods, TOption и Entity.
Parallel range, Burst/IL2CPP и арности 1–8 здесь не реализовывались/не проверялись.

### Отклонённые промежуточные формы

- `dispatch-switch-1`: большой runtime switch + относительные смещения для всех:
  inline 2.31 против 2.04 ms, PPPP 4.51 против 2.54 ms; 7/7 tests passed, но медленно.
- `dispatch-split-2`: inline восстановлен до 2.04, pool/mixed ещё 4.44–4.77 ms.
- `dispatch-raw-4` / `dispatch-full-5`: четыре абсолютных указателя и короткие
  шаги восстановили pool-скорость, но inline имел небольшой регресс.
- `dispatch-traits-6/7`: различный формат Current по признаку типа и отдельный
  inline hot path убрали этот регресс.
- `dispatch-flags-8`: четыре типовых признака заменили все 15 методов масок;
  это основа финального кода после удаления неиспользуемых методов.
- C#-перегрузка GetEnumerator by-value с IPoolComponent constraints действительно
  выбирает PPPP-итератор без генератора, при общем in-fallback. Проверено Roslyn и
  Unity IL; для mixed не даёт общего решения. Прототип RuntimeOverload удалён из
  Assets как ненужный после успеха единого iterator. `dispatch-overload-3` не имеет
  результата: compile error в namespace forwarding; исправлено перед raw-4.

### Финальные проверки и результаты

Unity 6000.0.63f1, открытый Editor, Threads.Main / Mono. 100000 сущностей, четыре
float3, два сложения, 10 warmups / 100 samples / 1 iteration. Контроль inline —
предыдущий mixed inline путь; pool-контроли — предыдущие AAAP/APAP/PPPP специализации.
Все четыре поля всех сущностей проверяются после каждого замера; отдельный мир на
вариант. Runtime namespace гарантирует отсутствие generated foreach binding.

`dispatch-final-matrix.xml`: **47/47 passed** — 29 correctness + 18 performance.
Проверены все 16 масок, неравные размеры, With/None/bare-tag фильтры, sparse rows,
пустые/исчерпанные iterator, Current/count snapshots, lazy pages, рост таблицы
страниц во время активной итерации. Включены предыдущие PageCache, QueryIter4 и
TagPoolMaskTests, в том числе ранее падавшие pool/tag-тесты.

`dispatch-final-repeat.xml`: **10/10 passed**, повтор sequential и Add+ECB в обоих
порядках с тем же кодом. Последний повтор, прямой / обратный порядок:

| Сочетание | Control median ms | Runtime median ms | Runtime avg ms |
|---|---:|---:|---:|
| AAAA | 2.0607 / 2.0537 | 2.0557 / 2.0398 | 2.0570 / 2.0484 |
| AAAP | 2.3741 / 2.3782 | 2.2262 / 2.2165 | 2.2283 / 2.2210 |
| APAP | 2.4184 / 2.4188 | 2.2412 / 2.2343 | 2.2490 / 2.2383 |
| PPPP | 2.4847 / 2.4860 | 2.3717 / 2.3666 | 2.3823 / 2.3734 |
| PPPP, Add+ECB | 2.4904 / 2.4978 | 2.3675 / 2.3816 | 2.3800 / 2.3876 |

В первом финальном matrix-прогоне AAAA runtime median 2.0624 / 2.0742, AAAP
2.2300 / 2.2320, APAP 2.2628 / 2.3465, PPPP 2.6000 / 2.6050. PPPP-контроль там
тоже выше: 2.7345 / 2.7435. Add+ECB runtime avg 2.4288 / 2.4300. Поэтому сравнивать
парные варианты внутри прогона, не выдавать минимальное время за общую гарантию.

Shuffled+filter (66666 обработанных): PPPP примерно на уровне прежней специализации,
APAP немного быстрее, AAAP имеет шумный/неустойчивый результат. Это не доказательство
выигрыша во всех sparse-ID сценариях. Inline sparse быстрее старого mixed-контроля.

XML, revision stamps, снимки исходников и IL — в прежнем внешнем каталоге артефактов.
Финальные snapshot/hash файлы имеют префикс `dispatch-final-`. Временные editor
driver/revision и их meta удалены, pending run request отсутствует.
В рабочем дереве также есть отдельный пользовательский diff `src/Entity/Entity.cs`
в Set<T> (pool-aware Set, файл изменён 04:40:13): эта работа его не редактировала
и не откатывала. Он сохранён вместе со snapshot исходников финального прогона.

## Кэш страниц и специализация четырёх компонентов — 2026-09-07

Текущий этап выполнен как отдельный runtime-эксперимент в UnitTests. Сейчас
оптимизируются только четыре data-компонента; перенос на 1–8 и обычный
`Query + foreach` выполняется после выбора финального решения. Production API,
inline `iter_compact_runtime`, предыдущие mixed/paged-контроли и source-gen DLL
не менялись. Новые файлы: `RuntimeQueryIter4PageCacheExperiments.cs`,
`RuntimeQueryIter4PageCacheBenchmarks.cs`, `RuntimeQueryIter4PageCacheTests.cs`.

### Что изменено в кандидате

- Current — четыре типизированных указателя (32 байта), возвращается по значению;
  сохранённый Current остаётся снимком адресов.
- Gather встраивается; арифметика указателей использует sizeof конкретных типов.
- Индекс страницы и слот вычисляются один раз на сущность. Адреса четырёх
  component buffers обновляются только при смене страницы; таблица страниц
  читается через стабильный ComponentPoolUntyped*. Сырой указатель на таблицу
  не кешируется. Рост таблицы во время активной итерации проверен тестом.
- LoadPage остаётся холодным NoInlining-методом с capacity/isCreated и прежним
  ленивым GetPtr fallback. При непоследовательных ID он может вызываться часто.
- Для известных сочетаний AAAP, APAP и PPPP написаны прототипы той формы,
  которую в будущем сможет выдавать генератор: никаких pool/inline проверок
  колонок в Gather. A = inline, P = pool. Валидация сочетания — в Init.
- Обход сохраняет точные logical archetype rows, фильтры и snapshot count.
  Это по-прежнему runtime foreach, тело системы не переписывается в batch loop.

Диагностические API:

| API | Назначение |
|---|---|
| `iter_direct_pool_runtime()` | контроль: typed pointers + inline Gather, без кэша страниц |
| `iter_cached_runtime()` | общий кэш; все 16 сочетаний четырёх inline/pool слотов |
| `iter_cached_aaap_runtime()` | специализация: три inline, последний pool |
| `iter_cached_apap_runtime()` | специализация: pool во втором и четвёртом слотах |
| `iter_cached_allpool_runtime()` | самый быстрый проверенный PPPP-кандидат |

Общий и all-pool варианты имеют перегрузку с пятым фильтром. AAAP/APAP пока
только четырёхпараметрические прототипы; дополнительные формы потребуются при
интеграции. Entity, parallel range и 1–8 production dispatch здесь не добавлялись.
Общий кэш без специализации не стоит автоматически выбирать для всех запросов:
в некоторых mixed-сценариях он медленнее обычного сгенерированного foreach.

### Производительность и проверки

Unity 6000.0.63f1, открытый пользовательский Editor, Threads.Main / Mono.
Четыре float3, два сложения, 100000 сущностей, 10 warmups / 100 samples / 1 iteration.
Все четыре компонента всех сущностей проверяются после измерения. Отдельный мир
на каждый вариант, finally Dispose. Сравнение включает обычный Query + foreach
(`Plain`), прежний iter_paged_pool_runtime (`Baseline`) и новые кандидаты.

`cache-final-1.xml`: **35/35 passed** (32 correctness + 3 performance tests).
В одном запуске измерен оригинальный Dragon main benchmark: median **2.9196 ms**,
avg **2.9237 ms**, stddev **0.0175 ms**.

PPPP, создание через archetype, прямой / обратный порядок измерения:

| Вариант | Median ms | Avg ms | Stddev ms |
|---|---:|---:|---:|
| Baseline | 5.7387 / 5.6944 | 5.7734 / 5.7130 | 0.1861 / 0.0717 |
| Direct | 3.9332 / 3.9254 | 3.9363 / 3.9427 | 0.0357 / 0.0606 |
| Cached | 3.1663 / 3.1534 | 3.1830 / 3.1560 | 0.0624 / 0.0152 |
| Specialized PPPP | **2.5795 / 2.5839** | **2.5827 / 2.5928** | 0.0398 / 0.0426 |
| Plain | 4.3089 / 4.1740 | 4.3116 / 4.1816 | 0.1169 / 0.0318 |

`cache-specialized-1.xml`: 12/12 performance cases, sequential и shuffled+filter,
по 1, 2, 4 pool, оба порядка. Sequential AAAP: specialized median 2.3930 / 2.4235
против Plain 2.8485 / 2.8478. APAP: 2.4716 / 2.5152 против 3.2570 / 3.2566.
Shuffled+filter реально перемешивает ID через recycling мелкими пакетами, пропускает
каждую третью сущность через IsPrefab (обработано 66666). Выигрыш менее устойчив:
AAAP около паритета, APAP зависит от прогона; PPPP в обратном порядке 3.4953 против
Plain 4.2345. Есть шумные хвосты — не объявлять универсальный выигрыш на sparse ID.

`cache-deferred-fixed.xml`: **12/12 passed** (9 correctness + 3 performance tests).
Дополнительный `CompareDeferredCreation` повторяет concrete Add<Pool>(value) + ECB
и WorldConfig пользовательского BenchNukecs (100001 entities/pool, 64 components).
PPPP, прямой / обратный порядок:
- Baseline median 5.8600 / 5.9061, avg 6.0196 / 6.0093 ms (шумный контроль).
- Specialized median **2.7269 / 2.7288**, avg **2.7037 / 2.6978 ms**.
- Plain median 4.3195 / 4.3733, avg 4.3169 / 4.4774 ms.
- Неизменённый inline compact: median 2.0634, avg 2.0734 ms.

Correctness покрывает все 16 storage masks общего варианта, специализированные
AAAP/APAP/PPPP, разные размеры, sparse logical rows, теги/None/With, пустые и
исчерпанные итераторы, Current/count snapshots, lazy pages и page-table growth.
Старые QueryIter4, Mixed, Paged, TagPoolMaskTests проходят, включая прежние пять
pool/tag падений. IL 15 generated jobs проверен: runtime MoveNext, без batch walkers.
В Gather трёх специализаций отсутствуют проверки Pool и вызовы GetPtr/GetChunk.
Burst/IL2CPP-производительность не измерялась.

Отброшенный `cache-deferred-final.xml`: 2 benchmark failures вызваны generic
fixture Add<T> с ограничением IComponent, которое выбирает другой ECB overload,
чем concrete IPoolComponent Add в пользовательском benchmark. Это отдельная
проблема generic Add; runtime iterator не менялся для её обхода. Стенд исправлен
на конкретные pool-типы и теперь проверяет исходные значения ДО замера. Не
использовать результаты этого неудачного прогона. Generic Add требует отдельного
регрессионного теста/исправления перед будущей production-интеграцией.

Артефакты XML/IL/снимки исходников находятся в прежнем внешнем каталоге ниже.
Метки скомпилированной тестовой сборки сверены с ID запросов. Source-gen DLL SHA256
остаётся `1924AFC425616DFA64EAA9CE914F65723E86124B6116D070909CF5A1D4CF14FA`.

## Требования к финальному варианту и пользовательский замер — 2026-09-07

- Финальный вариант должен сохранять обычный `Query + foreach` API и поддерживать
  итерацию по 1–8 data-компонентам в любых сочетаниях inline IComponent и
  IPoolComponent, включая полностью inline и полностью pool запросы.
- Теги служат только фильтрами, без payload. Запросы без pool должны сохранять
  отдельный максимально быстрый путь. Текущие эксперименты с четырьмя компонентами
  — измерительные прототипы, а не ограничение будущего API.
- Пользователь сообщил Avg **5.40 ms** для
  `Nukecs_Iteration_4Components_Main_Iter_WithPool_New_06_09_2026_2`:
  `iter_paged_pool_runtime()`, четыре CPool с float3, 100000 сущностей, Threads.Main.
  В исходнике подтверждён вызов этого итератора; сам замер здесь не повторялся.
- Пользовательский Dragon benchmark: Avg **2.86 ms**, четыре float3-компонента,
  100000 сущностей, main thread. Отношение сообщённых средних ~1.89; это ориентир
  для одинакового повторного прогона, не доказательство причины разницы.
  Прежние int-замеры ниже не подменяют этот float3-контроль.
- При обобщении проверять все арности и расположения pool-слотов, фильтрацию,
  sparse rows, перемешанные ID, рост пулов и snapshot-семантику. Оптимизация
  четырёхкомпонентного последовательного случая сама по себе недостаточна.

## Paged runtime, шаги 1–3 — 2026-09-06

Текущая работа завершена как эксперимент в тестовой сборке. Runtime/src,
`iter_compact_runtime()`, `iter_mixed_runtime()` и source-gen не менялись.
Новые файлы: `RuntimeQueryIter4PagedExperiments.cs`, `RuntimeQueryIter4PagedTests.cs`,
`RuntimeQueryIter4PagedBenchmarks.cs` в UnitTests. В существующий стенд добавлен
PagedPointerIteration с тем же float3-телом и Threads.Main.

### Два новых API

- `iter_paged_runtime()` — универсальный контроль с прямым доступом к страницам;
  плотный inline-шаг сохраняет форму `Advance(end) || MoveNextGathered()`.
- `iter_paged_pool_runtime()` — лучший вариант для mixed/pool-only: требует хотя бы
  один pool среди четырёх data-компонентов (проверяется при создании). Его MoveNext
  не делает inline Advance и не проверяет режим хранения; переход между archetype
  вынесен в отдельный NoInlining NextArchetype.
- Оба поддерживают Query с четырьмя data-компонентами и дополнительным пятым
  фильтром. Теги не являются payload, Entity/другие арности не добавлялись.

`Gather` вычисляет страницу и слот один раз на сущность; все pool-колонки используют
эти индексы. `At` читает готовую страницу напрямую через стабильный
ComponentPoolUntyped*, проверяя capacity и isCreated. GetChunk отсутствует в обычном
пути. Сохраняется холодный MissingPage -> GetPtr для старых Add<Pool>()/ECB-путей,
которые ещё могут публиковать membership без выделенной страницы. Убирать этот
fallback без исправления всех таких путей нельзя. Таблица страниц не кешируется
сырым указателем, поэтому её resize не оставляет висячий кеш.

Попытка объединить короткий pool-шаг с inline в одном MoveNext добавляла проверку
режима и давала ~6–16% регресс на минимальном int-цикле. Раздельный pool-итератор
сохраняет выигрыш без изменения inline-контроля. Production dispatch не заменялся.

### Измерения

Unity 6000.0.63f1, batchmode, Mono (`--burst-disable-compilation`), 100000 сущностей,
10 warmups / 100 samples, одинаковые два чтения и две записи int-полей.
На каждый вариант создаётся отдельный мир, проверяются все значения после замера.
Shuffled+filter: ID перемешаны через recycling небольших пакетов, None<Tag>
исключает каждую третью сущность (66666 обработанных). DestroyNow здесь откладывает
удаление в ECB; стенд явно делает playback, иначе shuffle не происходил.

Медианы, ms; контроль — iter_mixed_runtime, кандидат — iter_paged_pool_runtime:

| Сценарий | Контроль / кандидат, прямой порядок | Контроль / кандидат, обратный порядок |
|---|---:|---:|
| 1 pool, последовательные ID | 3.8212 / 3.2193 | 3.8114 / 3.2324 |
| 2 pool, последовательные ID | 4.2970 / 3.7227 | 4.3558 / 3.7659 |
| 4 pool, последовательные ID | 5.2497 / 3.8482 | 5.3309 / 4.0344 |
| 1 pool, shuffled+filter | 2.8814 / 2.6700 | 2.9252 / 2.7297 |
| 2 pool, shuffled+filter | 3.4613 / 3.0663 | 3.6056 / 3.1635 |
| 4 pool, shuffled+filter | 4.5258 / 3.4931 | 5.3615 / 3.6787 |

Выигрыш последовательных pool-сценариев ~13–27%. Shuffled также выигрывает,
но 4-pool имеет заметный шум: в обратном прогоне stddev контроля 0.630 ms,
кандидата 0.228 ms; не трактовать максимальный процент как стабильную гарантию.
Плотный inline int-контроль: ~0.481 ms у обоих универсальных вариантов.
Исходный float3 runner в обратном прогоне: compact 2.0664 ms, mixed 2.0787 ms,
paged 2.0615 ms — заметного регресса нет. Без pool, но со sparse фильтрацией,
универсальный paged выигрыша не дал (~1–1.5% медленнее mixed).

### Проверки и артефакты

`paged-final-reverse.xml`: **28/28 passed** (17 correctness + 11 performance cases).
Включены старые Mixed/Candidate/QueryIter4 тесты, новые проверки ленивых страниц,
переходов страниц, всех pool-слотов, фильтров, snapshot Current/count, пустого
итератора и отказа pool-only API для inline-only запроса. Прямой финальный порядок:
`paged-bench-4.xml`, 16/16. Обратный порядок задаётся переменной процесса
`NUKECS_PAGED_REVERSE=1`, не меняющей настройки Unity проекта.
IL подтверждает три runtime-цикла без batch rewrite, отсутствие Advance в
pool MoveNext, один shr и один and в Gather.

Первый запуск с включённым Burst получил BC0102/InvalidCastException в существующем
`Assets/ECS Bench/Nukecs/BenchNukecs.cs:1831`, Raw_Ptr generated job. Это сообщение
попало в первый correctness-тест. Оно не подавлялось через LogAssert; последующие
прогоны проверяют именно Mono с отключённой Burst-компиляцией на время процесса.
Burst-производительность нового кандидата не измерена и не подтверждена.
`paged-bench-1` содержит четыре провалившихся проверки самого shuffle-стенда;
для shuffled использовать только последующие XML.

XML, логи, снимки исходников, hashes и IL находятся в прежнем внешнем каталоге
артефактов. Временный editor bridge удалён; все запущенные batch-процессы завершились.
Source-gen DLL SHA256 по-прежнему
`1924AFC425616DFA64EAA9CE914F65723E86124B6116D070909CF5A1D4CF14FA`.

## Mixed runtime и исправления pool — 2026-09-05

Актуальное продолжение: `iter_compact_runtime()` сохранён без изменений.
Отдельный кандидат `iter_mixed_runtime()` находится в
`UnitTests/RuntimeQueryIter4MixedExperiments.cs`. Поддерживает четыре data-компонента
Inline/Pool в любой комбинации и необязательный пятый параметр-фильтр
(`With<Tag>`, `None<Tag>`, bare tag и другие фильтры Query). Теги не имеют tuple-слота;
tag среди первых четырёх параметров явно отклоняется. Entity и другие арности
не входят в этот эксперимент. Production `iter()` и source-gen DLL не менялись.

Плотный inline-шаг сохраняет `Advance(end) || Next()` и 32-байтный Current.
Pool/sparse запросы обходят matchingArchetypes и их физические rows; pool ищется
по `packedEntities[row]`. Реальный GenericPool здесь использует страницы по ID
сущности, а не плотно упакованный массив в порядке строк storage.
Колонки держат `ComponentPoolUntyped*`, устойчивый к расширению таблицы GenericPool.
Плотные логические блоки без pool тоже используют короткий шаг.

### Исправлены пять сообщённых падений

- `ArchetypeUnsafe.CreateEntity()` теперь выделяет и обнуляет слоты pool-компонентов,
  как batch-путь. Раньше немедленный `Get<Pool>()` возвращал ссылку на null-страницу.
- `EntityExtensions.Get<T>()` с ограничением IComponent теперь учитывает Pool.
  Generic helper теста выбирал именно эту перегрузку, которая раньше искала inline-offset.
- В `WorldUnsafe.GetPool<T>()` восстановлена проверка вместимости pools; добавлена
  также в `GetUntypedPool(int)`. Высокие индексы типов раньше записывались за пределами
  таблицы, затем её resize терял записанные данные. Это объясняло 0 вместо 12
  в обоих старых TagPoolMaskTests и зависимость результата от предварительного обхода.
- Временная диагностика из TagPoolMaskTests удалена; оставлены две проверки значений
  pool непосредственно перед runner. Дополнительного прогрева/обхода в тесте нет.

### Проверки

`pool-regression-final.xml`: **62/62 passed** — MixedTests, CandidateTests,
QueryIter4Tests, TagPoolMaskTests, WorldTests, ResizeTests. Включены все пять
сообщённых падений. Метка скомпилированной сборки сверена с запросом.
Mixed-тесты покрывают каждый pool-слот, несколько и все pool-компоненты,
storage без inline-колонок, sparse rows, фильтры, разные размеры, snapshot Current
и snapshot count при добавлении строк без перевыделения активных буферов.

100000 сущностей, Threads.Main/Mono, 10 warmups / 100 samples, проверка значений
после измерения. IL всех трёх generated job подтверждает runtime MoveNext,
без BatchStorageWalk/BatchArchetypeWalk.

| Прогон | Compact median | Mixed inline median | Обычный iter median |
|---|---:|---:|---:|
| mixed-bench-fixed-1 | 2.08230 ms | 2.07000 ms | 2.45885 ms |
| mixed-bench-fixed-2 | 2.07250 ms | 2.09045 ms | 2.44625 ms |

Заметного регресса медианы inline-пути не обнаружено (разница в пределах 1%).
Второй mixed-прогон имеет шумный хвост, stddev 0.32850 ms; это не доказательство
строгой эквивалентности производительности. Порог <=2.00 ms по-прежнему не достигнут.
Производительность mixed/pool отдельно пока не измерялась; корректность проверена.
Кандидат остаётся в тестовой сборке.

Артефакты находятся в том же внешнем каталоге, который указан ниже. В этой сессии
для проверки использовался открытый Editor через временный bridge, без управления UI.
Временные bridge/revision и их meta удалены; Editor пересобрал тестовую DLL без bridge.
Source-gen DLL SHA256 остался
`1924AFC425616DFA64EAA9CE914F65723E86124B6116D070909CF5A1D4CF14FA`.
Ниже сохранено предыдущее состояние до mixed-эксперимента.

## Обновление 2026-09-05 — актуальная точка продолжения

Этот раздел уточняет историческое состояние и результаты ниже.

### Очистка выполнена

- Удалены все перечисленные ниже неудачные runtime-типы и `iter_dense_runtime()`.
- Удалены зависимые `RuntimeQueryIter4DenseFloorSystem` и `DenseInline_RuntimeForeachFloor`.
- Содержательный diff `Query.cs`, `QueryIterators.cs`, `RefTuple.cs` относительно HEAD отсутствует.
- Source-gen анализатор и `SourceGen/NUKECSGEN.dll` сохранены. SHA256 DLL:
  `1924AFC425616DFA64EAA9CE914F65723E86124B6116D070909CF5A1D4CF14FA`.
- `AGENTS.md`, `ARCHITECTURE.md` и исходный `Assets/ECS Bench/Nukecs/BenchNukecs.cs`
  сохранили исходные SHA256. Посторонние изменения не откатывались.

### Сохранён новый диагностический кандидат, production API не менялся

`UnitTests/RuntimeQueryIter4Experiments.cs` содержит только текущий вариант:

- `RuntimeStorageIter<TTuple>` — один generic-параметр tuple-типа;
- `RuntimeDenseRefs<T1,T2,T3,T4>` — базовый указатель и три 64-битных относительных смещения;
- `Advance(end)` сдвигает базовый указатель и проверяет конец блока;
  при разных размерах компонентов корректирует смещения;
- для одинаковых размеров Mono устраняет три корректировки;
- `Current` возвращается по значению; сохранённый tuple остаётся снимком адресов;
- `NextStorage` вынесен в отдельный NoInlining-метод, конец блока вычисляется
  по снимку count;
- `iter_compact_runtime()` находится только в тестовой сборке и явно отклоняет
  tag/pool-компоненты и неготовый/degraded storage snapshot.

Кандидат НЕ является заменой production `iter()`: общий fallback для фильтров,
Entity, tag/pool и sparse rows не реализован. Эти запросы продолжают использовать
неизменённый production iterator. Перед ручным вызовом диагностического API
требуется `query.Update(ref world, IntPtr.Zero)`, как перед вызовом системы в runner.

Benchmark: `RuntimeQueryIter4Tests.DenseInline_CompactPointerWithSystemRunner`.
Контроль: прежний `DenseInline_RuntimeIterationWithSystemRunner`.
Оба используют одинаковое тело системы, Threads.Main, 100000 сущностей,
10 warmups / 100 samples / 1 iteration. После измерений проверяются все четыре
компонента каждой сущности; измерительный world освобождается через finally.
Проверка IL обоих сгенерированных OnUpdate/OnUpdateBatched подтвердила вызов
runtime MoveNext и отсутствие BatchStorageWalk/BatchArchetypeWalk.

### Измерения и критерий принятия

Unity 6000.0.63f1 был уже открыт пользователем. Замеры выполнялись через TestRunnerApi
в этом Editor, НЕ в batchmode. Поэтому их абсолютные значения нельзя напрямую
приравнивать к прежним batchmode-результатам.

| Прогон (XML) | Candidate median | Control median | Candidate stddev |
|---|---:|---:|---:|
| `verified-advance-1.xml` | 2.06815 ms | 2.41815 ms | 0.03161 ms |
| `candidate-settled-1.xml` | 2.08740 ms | 2.51585 ms | 0.06702 ms |
| `candidate-settled-2.xml` | 2.08990 ms | 2.40810 ms | 0.41580 ms |

Последние два прогона выполнены последовательно с одним кодом кандидата и паузой
5 секунд после reload перед запуском теста. Медианы близки, но хвост второго
прогона нестабилен. Не считать это выполнением критерия двух стабильных запусков.
Цель median <= 2.00 ms НЕ достигнута; production оптимизация НЕ принята.
Кандидат сохранён как воспроизводимый шаг вперёд, примерно 2.07–2.09 ms
против контроля около 2.4 ms, без source-gen rewrite.

Новые неудачные варианты удалены из проекта: index-based Current, косвенный
Current через указатель на mutable state, узкие 32-битные относительные смещения.
Их исходники/замеры сохранены только снаружи Assets. 32-битные смещения дали
2.67 ms; 64-битные устранили этот регресс. Ref-return Current заметного выигрыша
не дал и в кандидате не используется.

### Проверки корректности

`correctness-final.xml`: **10 / 10 passed**:

- пять новых тестов `RuntimeQueryIter4CandidateTests`: неодинаковые размеры,
  несколько storages и логических archetypes, снимок Current, пустой/исчерпанный
  iterator, append при обходе блока, отказ для tag/pool и degraded storage;
- три существующих `QueryIter4Tests` (inline, sparse None, pool/tag);
- два ручных runtime-теста из `RuntimeQueryIter4Tests` (inline и None).

### Артефакты и защита от устаревшей сборки

Каталог:
`C:\Users\user\.codex\visualizations\2026\09\05\01a06fa4-56e2-74c2-b543-f3832c93fc87`.

Там лежат XML, SHA256 исходников, снимки C# кандидата/стенда для каждого
верифицированного эксперимента, IL и native disassembly.

Обнаружена ловушка TestRunnerApi: при ошибке компиляции он может выполнить тест
из старой DLL и сообщить Passed. Ошибка была в ограничениях generic factory
(T2/T3 должны реализовывать IComponent); она исправлена. `compact-factory-1.xml`
и `verified-bootstrap.xml` относятся к предыдущей сборке и НЕ доказывают работу
factory. Остальные ранние результаты без метки сборки считать предварительными.

Начиная с `verified-factory-1`, метка AssemblyMetadata сверялась с запросом;
начиная с `verified-predecrement-1`, она также сохранена в
`*.xml.revision.txt`. `run-bench.ps1` отказывается принимать несовпадающую метку.
Временные EditorBridge и Revision удалены из Assets после проверки; шаблон bridge
сохранён в каталоге артефактов. Для повторения через открытый Editor нужно вернуть
этот шаблон в UnitTests, импортировать его, затем запускать run-bench.ps1 из корня
runtime-репозитория. По завершении снова удалить временные bridge/revision и их meta.
При свободном проекте предпочтительнее прежний batchmode-запуск из этого handoff.

### Следующая работа

1. Проверить сохранённый кандидат и контроль в одинаковом спокойном окружении;
   по возможности в batchmode, когда пользователь освободит проект.
2. До production-интеграции довести median до <= 2.00 ms и подтвердить два
   последовательных стабильных запуска. Сохранение одного лучшего min недостаточно.
3. Только после этого проектировать общий fallback и проверять полный контракт
   `Query<T1,T2,T3,TOption>.iter()`, включая фильтры и Entity.
4. Не возвращать удалённые эксперименты и не менять source-gen/исходный benchmark.

## Цель

Оптимизировать runtime-итерацию без source-gen переписывания тела системы, сохранив существующий API:

```csharp
foreach (var (a, b, c, d) in query.iter())
{
    a.Get.Value += b.Read.Value;
    c.Get.Value += d.Read.Value;
}
```

Цель по производительности: медиана не выше `2.00 ms` на 100 000 сущностях с четырьмя inline-компонентами `float3`. Исходный пользовательский результат runtime-пути — примерно `2.35 ms`. Работу с source-gen необходимо сохранить, но runtime-бенчмарк обязан исключать его batch rewrite.

## Репозитории и основные файлы

- Runtime Nukecs: `D:\Unity\NukecsSandbox\NukecsSandbox\Assets\Nukecs`
- Source generator: `D:\Unity\NukecsSandbox\NUKECSGEN`
- Исходный пользовательский benchmark: `D:\Unity\NukecsSandbox\NukecsSandbox\Assets\ECS Bench\Nukecs\BenchNukecs.cs`
- Runtime API запроса: `src/Systems/FnSystems/Query.cs`
- Enumerators: `src/Systems/FnSystems/QueryIterators.cs`
- Ref tuples: `src/Systems/FnSystems/Tuples/RefTuple.cs`
- Изолированный runtime-стенд: `UnitTests/RuntimeQueryIter4Tests.cs`
- Correctness-тесты итерации: `UnitTests/QueryIter4Tests.cs`
- Сохранённый лучший runtime-результат: `UnitTests/Results/BenchIter4Optimized.xml`

Не изменять исходный `Assets/ECS Bench/Nukecs/BenchNukecs.cs` ради новых экспериментов. Новые варианты измерять в `UnitTests/RuntimeQueryIter4Tests.cs`.

## Архитектура runtime-итерации

Для четырёх параметров используется тип:

```csharp
Query<T1, T2, T3, TOption>
```

`TOption` может быть четвёртым компонентом либо фильтром (`None<T>`, `Any<T>`, `With<T>`). Поэтому оптимизация не должна считать четвёртый generic-параметр компонентом без проверки `QueryParamInfo<TOption>.IsComponent`.

Штатный `Query<T1,T2,T3,TOption>.iter()` возвращает:

```csharp
QueryIter<RefTuple<T1, T2, T3, TOption>>
```

Перед созданием enumerator вызывается `QueryUnsafe.TryUseStorageIteration()`:

- storage mode: перебираются `QueryUnsafe.GetMatchingStorages()`, строки физически плотные;
- archetype mode: перебираются `matchingArchetypes`, а при неплотном логическом archetype используется `archetype.rows` и `RefTuple.AdvanceTo(row)`.

`QueryIter<TTuple>.MoveNext()` держит:

- указатели/длину archetype или storage списка;
- индекс текущего блока;
- `_remaining` строк;
- `_rows` и `_listIdx` для gather-пути;
- полный `TTuple` с текущими компонентными указателями.

`RefTuple<T1,T2,T3,T4>` содержит четыре `Ref<T>`, текущую строку, четыре возможных `GenericPool*` и `packedEntities`. Его `Add()` на каждой строке проверяет tag/pool категории, увеличивает inline-указатели и при необходимости получает pooled-компоненты по entity id.

Поддерживаемые семантики, которые нельзя сломать:

- четыре inline-компонента;
- tag-компоненты;
- pool-компоненты и смешанные inline/pool запросы;
- `None<T>` и другие фильтры;
- несколько storages/archetypes;
- неплотные `rows`;
- `Query<Entity, ...>`;
- пустые запросы.

## Архитектура source-gen (оставить)

В `D:\Unity\NukecsSandbox\NUKECSGEN\SrcGen.ForEachAnalysis.cs` сделано намеренное изменение:

- batch analyzer распознаёт не только `foreach (... in query)`, но и `foreach (... in query.iter())`;
- принимается только вызов `iter()` без аргументов на параметре запроса;
- количество деконструируемых значений сверяется с количеством компонентов;
- `query.iter()` с `Entity` пока намеренно не переписывается из-за различия `Ref<Entity>` и `Entity`;
- система должна состоять ровно из одного `foreach`, иначе используется обычный runtime-путь.

Собранный `SourceGen/NUKECSGEN.dll` в runtime-репозитории обновлён. Source-gen вариант показывает около `1.63 ms`. Эту работу не удалять при очистке runtime-экспериментов.

## Как runtime-стенд гарантированно исключает batch rewrite

`RuntimeQueryIter4GeneratedHarness.RuntimeIteration` помечен `[System]`, чтобы сохранить обычное связывание параметров и runner-контекст, но содержит оператор до и проверку после `foreach`:

```csharp
var countSnapshot = query.Count;
foreach (var (a, b, c, d) in query.iter()) { ... }
if (countSnapshot < 0) dbug.log("unreachable");
```

Из-за нескольких statements source-gen analyzer отвергает batch rewrite. Поэтому `DenseInline_RuntimeIterationWithSystemRunner` является основным apples-to-apples runtime-бенчмарком.

Ручные `ISystem`-стенды полезны только диагностически: Mono генерирует для них иной код, и абсолютное время может сильно отличаться.

## Подтверждённые результаты

| Вариант | Median | Avg | Вывод |
|---|---:|---:|---|
| Source-gen batch rewrite | ~1.63 ms | ~1.63 ms | Эталон, сохранить |
| Лучший специализированный runtime-прогон | 2.211 ms | 2.214 ms | Лучший сохранённый `query.iter()` результат |
| Штатный универсальный runtime control | 2.37 ms | 2.41 ms | Надёжная текущая база |
| Ручной raw pointer storage loop | 1.65 ms | 1.85 ms | Доказывает, что `<2.00 ms` достижимо без generated loop, но это не `query.iter()` API |

Лучший результат хранится в `UnitTests/Results/BenchIter4Optimized.xml`: min `2.1821`, median `2.21125`, avg `2.21398`, stddev `0.02267`, 100 samples. В момент этого запуска использовался специализированный storage iterator в стиле `QueryRefIter4`; точная версия не была зафиксирована коммитом и позднее была заменена экспериментами. Нельзя утверждать, что любой из текущих классов `QueryRefIter4*` воспроизводит эти цифры — сначала требуется реконструкция и повторное измерение.

## Неудачные эксперименты

Результаты сохранены преимущественно в:

`C:\Users\user\.codex\visualizations\2026\09\04\01a06e30-bbb7-77d0-b32f-09e05c45edb9`

| Эксперимент | Median | Причина отказа/наблюдение |
|---|---:|---|
| `RefTuple.Add()` с дополнительным fast flag | 3.31 ms | Изменение layout/ABI ухудшило Mono-код |
| Статическая быстрая ветка `Add` | 2.44 ms | Не лучше базы |
| ref-return `Current` | 2.72 ms | Alias/ref-return не помог |
| `iter_unsafe()` pointer tuple | 2.83 ms | Сам pointer tuple не решает overhead enumerator |
| row-marker fast mode | 3.71 ms | Дополнительная ветка ухудшила код |
| Безусловный `RefTuple4.Add` | 2.87 ms | Не дал ожидаемого выигрыша |
| Dense iterator с `ValueTuple` | 4.68 ms | Большой регресс |
| `QueryIterDense<TTuple>` | 2.86 ms; в runner около 2.40 ms | Практически база, выигрыша нет |
| Прямой четырёх-generic iterator | 2.94 ms | Mono плохо оптимизирует этот shape |
| Прямой iterator с hot/cold split | 2.58 ms | Лучше прямого, но хуже базы |
| Отдельный compact `Current` iterator | 4.66 ms | Специализация по четырём generic-параметрам дала плохой JIT-код |
| Сжатие pool-state в `RefTuple` | 3.16 ms | Более сложные generic static обращения ухудшили `Add()` |
| `Add()` с одной mode-проверкой и cold fallback | 3.14 ms | Mono не встроил ожидаемым образом |

Главный вывод: микрооптимизация, логичная для CoreCLR, часто ухудшает Unity Mono. Принимать изменения можно только после реального Unity EditMode benchmark.

## Текущее состояние рабочего дерева

Runtime API `Query<T1,T2,T3,TOption>.iter()` уже возвращён к штатному `QueryIter<RefTuple<...>>`. Однако в рабочем дереве остались экспериментальные объявления, которые не должны попасть в финал:

- `QueryIterDense<TTuple>`;
- `IDenseComponentTuple`;
- `Ref4View<T1,T2,T3,T4>`;
- `QueryIterRefTuple4<T1,T2,T3,T4>`;
- `QueryRefIter4<T1,T2,T3,T4>`;
- `QueryDenseRefIter4<T1,T2,T3,T4>`;
- `QueryRefIter4Direct<T1,T2,T3,T4>`;
- диагностический метод `iter_dense_runtime()` в `Query.cs`.

`RefTuple.cs` фактически возвращён к исходной логике; его diff сейчас только из-за финального newline.

Не использовать общий `git reset`/`checkout`: рабочее дерево грязное и содержит пользовательские/другие изменения. Удалять нужно только перечисленные runtime-эксперименты точечным patch. Сохранить изменения source-gen, `AGENTS.md`, `ARCHITECTURE.md` и не относящиеся к задаче файлы.

## Рекомендуемая точка продолжения

1. Точечно удалить перечисленные неудачные runtime-типы и `iter_dense_runtime()`.
2. Проверить, что diff `Query.cs`, `QueryIterators.cs`, `RefTuple.cs` относительно базы отсутствует или содержит только намеренный новый вариант.
3. Запустить `DenseInline_RuntimeIterationWithSystemRunner` и подтвердить базу примерно `2.35–2.40 ms`.
4. Реконструировать минимальный специализированный storage iterator, давший `2.21 ms`, сначала только в отдельном диагностическом API/стенде.
5. Не менять production `iter()` до двух стабильных прогонов нового варианта.
6. Следующее перспективное направление: iterator с одним generic-параметром tuple-типа, компактным публичным tuple из четырёх `Ref<T>` и отдельным traversal state внутри enumerator. Не использовать iterator, обобщённый напрямую по четырём компонентам: измерения показывают плохую генерацию кода Unity Mono.
7. Альтернативно проверить index-based hot loop: `index++ / index < count`, а адреса четырёх компонентов собирать в `Current`. Сравнить с pointer-increment вариантом в одинаковом runner-стенде.
8. После достижения выигрыша добавить correctness-тесты для inline, filter, tag, pool, sparse rows, Entity и нескольких storage.
9. Критерий принятия: минимум два последовательных запуска по 100 samples; целевой median `<=2.00 ms`, отсутствие регрессий API и correctness-тестов.

## Запуск Unity-тестов

Unity Editor:

`D:\Unity\Unity\6000.0.63f1\Editor\Unity.exe`

Основной фильтр:

`Wargon.Nukecs.Tests.RuntimeQueryIter4Tests.DenseInline_RuntimeIterationWithSystemRunner`

Важно:

- не добавлять `-quit`: после domain reload Unity иногда выходит до выполнения теста;
- писать XML/log за пределами `Assets`, иначе AssetDatabase начинает повторные импорты;
- после появления XML завершать только точный PID запущенного batch Unity;
- benchmark: warmup 10, measurement count 100, iterations per measurement 1;
- сравнивать в первую очередь median и stddev, а не единичный min.

## Готовый промпт для нового чата

```text
Продолжи оптимизацию runtime `query.iter()` для четырёх компонентов в Nukecs. Сначала полностью прочитай `D:\Unity\NukecsSandbox\NukecsSandbox\Assets\Nukecs\RUNTIME_QUERY_ITER4_HANDOFF.md` и корневой `AGENTS.md`. Source-gen оптимизацию и обновлённый `SourceGen/NUKECSGEN.dll` сохрани. Исходный benchmark `Assets/ECS Bench/Nukecs/BenchNukecs.cs` не изменяй. Начни с точечной очистки только перечисленных неудачных runtime-экспериментов, восстанови и измерь стабильную базу через `RuntimeQueryIter4Tests.DenseInline_RuntimeIterationWithSystemRunner`, затем реконструируй вариант около 2.21 ms и продолжай к цели <=2.00 ms. API должен остаться `foreach (var (...) in query.iter())`, а тестовый цикл не должен переписываться source-gen.
```
