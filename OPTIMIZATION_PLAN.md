# Nukecs — Детальний план оптимізацій

> Дата аналізу: 2026-04-09
> Версія фреймворку: early (pre-release)
> Розмір кодової бази: ~60+ файлів, ~8000+ рядків

---

## Зміст

1. [Огляд архітектури](#1-огляд-архітектури)
2. [Критичні баги](#2-критичні-баги)
3. [Оптимізація алокацій](#3-оптимізація-алокацій)
4. [Оптимізація систем та паралелізму](#4-оптимізація-систем-та-паралелізму)
5. [Оптимізація компонентів та пулів](#5-оптимізація-компонентів-та-пулів)
6. [Якість коду та API](#6-якість-коду-та-api)
7. [Потокобезпечність](#7-потокобезпечність)
8. [Дебаг-інфраструктура](#8-дебаг-інфраструктура)
9. [План імплементації по етапах](#9-план-імплементації-по-етапах)
10. [Порівняння з зрілими ECS-фреймворками](#10-порівняння-з-зрілими-ecs-фреймворками)

---

## 1. Огляд архітектури

### 1.1 Структура проекту

```
src/
├── Allocator/          # Власний arena-алокатор + offset-based вказівники
├── Collections/        # MemoryList, HashMap, DynamicBitmask, AliveEntitiesSet
├── Components/         # Пули компонентів, реєстрація типів, GenericPool
├── Debug/              # (тільки .asset файл, код розкиданий)
├── Entity/             # Entity, EntityCommandBuffer (ECB)
├── Extensions/         # Розширення
├── Reactive/           # Reactive система (change detection)
├── Systems/            # ISystem, IJobSystem, IEntityJobSystem, runners, ECBJob
│   ├── FnSystems/      # Delegate-based системи (function pointers)
│   └── UnsafeJobs/     # Void-pointer системи (IComponentJobSystemUnsafe1-4)
├── Unity/              # Unity інтеграція (WorldInstaller, Transforms, Editor вікна)
├── World/              # World, WorldUnsafe, серіалізація, статичний світовий реєстр
├── Archetype.cs        # Архетипи з графом переходів (edge-based)
├── Query.cs            # Query builder + ітератори
├── QueryFilter.cs      # Фільтри запитів
├── SparseSet.cs        # Sparse set (standalone, не використовується в ядрі)
├── SystemsGroup.cs     # Групи систем
├── Singleton.cs        # Singleton компонент
└── BuiltInSystems.cs   # Вбудовані системи (OnPrefabSpawn, NRandom)
```

### 1.2 Ключові архітектурні рішення

| Рішення | Опис | Переваги | Недоліки |
|---------|------|----------|----------|
| Arena-алокатор | Вся пам'ять World в одному регіоні | Швидка серіалізація, нуль GC | Немає індивідуального звільнення |
| Offset-based вказівники (ptr\<T\>) | Зберігають offset + cached pointer | Працюють після десеріалізації | 2x пам'яті на вказівник |
| Chunked pools (64 ентіті/чанк) | Компоненти по entity ID | Lazy allocation, cache-friendly | Не archetype-linear (погано для ітерації) |
| DynamicBitmask для архетипів | Bitset component membership | O(1) Has/Add/Remove | Не масштабується для >64 типів (використовує ulong[]) |
| ECB з per-thread buffers | Кожен потік пише в свій буфер | Без блокувань при записі | Allocator.Temp ламається між кадрами |
| Граф архетипів з Edge-кешуванням | Кешовані переходи add/remove | Швидкі структурні зміни | PopulateQueries O(Q*T) на новий архетип |

---

## 2. Критичні баги

### BUG-1: ptr\<T\>.this[index] ігнорує індекс

**Файл:** `src/Allocator/ptr.cs:162`
**Серйозність:** CRITICAL
**Опис:** Індексатор завжди повертає `ref *cached`, ігноруючи параметр `index`.

```csharp
// ПОТОЧНИЙ КОД (баг):
public ref T this[int index] {
    get => ref *cached; // index проігноровано!
}

// ВИПРАВЛЕННЯ:
public ref T this[int index] {
    get => ref cached[index];
}
```

**Вплив:** Будь-який код, що використовує `ptr<T>[i]` для доступу до масиву, завжди отримує перший елемент. Це може бути причиною мовчазних баг даних.

---

### BUG-2: MemoryList.PtrOffset не оновлюється після Resize

**Файл:** `src/Collections/MemoryList.cs:180-203`
**Серйозність:** HIGH
**Опис:** Після resize, `Ptr` встановлюється на нову адресу, але `PtrOffset` продовжує вказувати на старий блок. Після десеріалізації `cached` буде обчислено з застарілого offset.

```csharp
// ПОТОЧНИЙ КОД (проблема в ResizeExact):
Ptr = newPointer;        // оновлено
// PtrOffset НЕ оновлено! ← баг

// ВИПРАВЛЕННЯ:
Ptr = newPointer;
PtrOffset = newPtrOffset; // додати
```

**Вплив:** Серіалізація/десеріалізація ламається після будь-якого resize MemoryList.

---

### BUG-3: DynamicBitmask.HasRange — оманливе форматування

**Файл:** `src/Collections/DynamicBitmask.cs:82-94`
**Серйозність:** MEDIUM
**Опис:** Внутрішній блок `{ if (matches == range) return true; }` виконується незалежно від умови `if (Has(...))`. Крім того, метод рахує кількість співпадінь, але не перевіряє, що ВСІ елементи buffer присутні — дублікати можуть дати false positive.

```csharp
// ПОТОЧНИЙ КОД:
public bool HasRange(int* buffer, int range) {
    var matches = 0;
    for (var i = 0; i < range; i++) {
        if (Has(buffer[i])) matches++;
        {  // цей блок не пов'язаний з if вище!
            if (matches == range) return true;
        }
    }
    return false;
}

// ВИПРАВЛЕННЯ (проста та правильна версія):
public bool HasRange(int* buffer, int range) {
    for (var i = 0; i < range; i++) {
        if (!Has(buffer[i])) return false;
    }
    return true;
}
```

---

### BUG-4: SystemDestroyer — use-after-scope

**Файл:** `src/Systems/SystemDestroyer.cs:9-16`
**Серйозність:** HIGH
**Опис:** Зберігає вказівник на стекову змінну `system`, яка виходить з області видимості після завершення конструктора.

```csharp
// ПОТОЧНИЙ КОД:
public SystemDestroyer(ref T system) {
    fixed (T* ptr = &system) {
        this.system = ptr;           // вказівник на стек!
        gcHandle = GCHandle.Alloc(system);
    }
}

// ВИПРАВЛЕННЯ:
private T systemCopy;  // зберігати значення, не вказівник
public SystemDestroyer(ref T system) {
    systemCopy = system;
}
public void Destroy(ref World world) {
    systemCopy.OnDestroy(ref world);
}
```

---

### BUG-5: World.Free() — закоментоване звільнення ресурсів

**Файл:** `src/World/World.Free.cs:11-103`
**Серйозність:** CRITICAL
**Опис:** Весь код звільнення (пули, архетипи, запити, сутності) закоментований. IDisposable компоненти ніколи не викликають Dispose(). GCHandle-ресурси (ObjectRef) не звільняються.

```csharp
// ПОТОЧНИЙ КОД:
public void Free() {
    ecb.Dispose(ref *this);
    WorldSystems.CompleteAll(id);
    // рядки 14-101 закоментовані!
}

// ПОТРІБНО:
// 1. Ітерувати всі пули → викликати DisposeFn для кожного елемента
// 2. Звільнити GCHandle для SystemClassDestroyer / SystemDestroyer
// 3. Звільнити StaticObjectRefStorage об'єкти цього World
// 4. Тільки потім знищити arena allocator
```

---

### BUG-6: Entity.Get\<T\>() має побічний ефект

**Файл:** `src/Entity/Entity.cs:133-145`
**Серйозність:** MEDIUM (API design)
**Опис:** `Get<T>()` тихо додає компонент з default значенням + ECB команду, якщо компонент відсутній. Це порушує Principle of Least Surprise — "get" не повинен мутувати стан.

**Пропозиція:** Розділити на два методи:
- `Get<T>()` — тільки читання, throw/assert якщо відсутній
- `GetOrAdd<T>()` — поточна поведінка
- `TryGet<T>(out T value)` — безпечний варіант

---

### BUG-7: RNG seed — неініціалізована пам'ять

**Файл:** `src/rng.cs:12-15`, `src/Unsafe.cs:194-231`
**Серйозність:** MEDIUM
**Опис:** `malloc_t<uint>(Allocator.Temp)` не ініціалізує пам'ять — seed є undefined behavior. Крім того, XorShift32 не потокобезпечний через мутацію SharedStatic без атоміків.

```csharp
// ПОТОЧНИЙ КОД:
var seed = malloc_t<uint>(Allocator.Temp);  // UB: неініціалізоване значення
random.Data = new random(*seed);

// ВИПРАВЛЕННЯ:
var seed = (uint)System.DateTime.UtcNow.Ticks;
random.Data = new random(seed);
```

---

### BUG-8: MemAllocator.memoryUsed ніколи не зменшується

**Файл:** `src/Allocator/Allocator.cs:353` vs `lines 263-318`
**Серйозність:** LOW
**Опис:** `memoryUsed` інкрементується в `InsertBlock` але ніколи не декрементується в `Free`. Метод `GetMemoryInfo` обчислює використану пам'ять незалежно, тому `memoryUsed` стає застарілим.

---

### BUG-9: ComponentPoolUntyped.GetChunk — off-by-one

**Файл:** `src/Components/GenericPool.cs:321`
**Серйозність:** MEDIUM
**Опис:** Перевірка `chunkIndex > Chunks.capacity` має бути `>=`.

```csharp
// ПОТОЧНИЙ КОД:
if (chunkIndex > Chunks.capacity) // пропускає останній індекс

// ВИПРАВЛЕННЯ:
if (chunkIndex >= Chunks.capacity)
```

---

## 3. Оптимізація алокацій

### ALLOC-1: Прибрати DeFragment з гарячого шляху

**Файл:** `src/Allocator/Allocator.cs:113, 139, 170, 195, 219`
**Пріоритет:** P0 (найвищий)
**Вплив:** 2-10x прискорення алокацій

**Проблема:** Кожен виклик `AllocateRaw` викликає `DeFragment()` — лінійний scan до 1M блоків, під спінлоком.

**Рішення:**
```
1. Видалити DeFragment() з AllocateRaw/Allocate/AllocatePtr
2. Викликати DeFragment() лише коли:
   - Першого first-fit scan не знайшов вільний блок
   - Явний виклик користувача
3. Додати needsDefrag bool-флаг, що встановлюється в Free()
```

**Оцінка трудовитрат:** 2 години
**Ризик:** Низький — дефрагментація все ще викликається при нестачі пам'яті

---

### ALLOC-2: Замінити лінійний scan на free-list

**Файл:** `src/Allocator/Allocator.cs:114-127`
**Пріоритет:** P1
**Вплив:** O(n) → O(1) або O(log n) для алокацій

**Проблема:** First-fit linear scan через всі блоки для кожної алокації.

**Рішення:**
```
Варіант A: Segregated fits (рекомендовано)
- Розділити блоки на size classes: 16, 32, 64, 128, ..., 4096, >4096
- Кожен size class має свій linked list вільних блоків
- Алокація: O(1) — взяти з відповідного size class
- Звільнення: O(1) — додати до відповідного size class

Варіант B: Red-black tree за розміром
- Блоки відсортовані за розміром
- O(log n) алокація та звільнення
```

**Оцінка трудовитрат:** 8-16 годин
**Ризик:** Середній — потребує ретельного тестування fragmentation patterns

---

### ALLOC-3: Консолідувати дубльовані методи алокації

**Файл:** `src/Allocator/Allocator.cs:109-237`
**Пріоритет:** P2
**Вплив:** Підтримуваність

**Проблема:** `AllocateRaw`, `Allocate`, `AllocatePtr<T>`, `AllocatePtr` — 4 методи з однаковою логікою first-fit + split (~130 рядків дублювання).

**Рішення:**
```
1. Створити приватний AllocateInternal(long size, out int blockIndex)
2. Всі публічні методи викликають його
3. Зменшити код з ~130 до ~30 рядків
```

**Оцінка трудовитрат:** 2 години

---

### ALLOC-4: Зменшити MAX_BLOCKS або зробити конфігурованим

**Файл:** `src/Allocator/Allocator.cs:29`
**Пріоритет:** P2
**Вплив:** 16MB економії пам'яті для малих світів

**Проблема:** `MAX_BLOCKS = 1,048,576` → метадані ~16MB завжди.

**Рішення:** Зробити параметром конструктора з дефолтом 65536 (64K блоків = ~1MB метаданих).

**Оцінка трудовитрат:** 1 година

---

### ALLOC-5: Пул алокацій для ECB компонентних даних

**Файл:** `src/Entity/EntityCommandBuffer.cs:122-124`
**Пріоритет:** P1
**Вплив:** Зменшення allocation pressure в 10-100x для spawning сценаріїв

**Проблема:** Кожен `Add<T>` виділяє `UnsafeUtility.Malloc(..., Allocator.Temp)`.

**Рішення:**
```
1. Pre-allocate linear buffer на Creation ECB (напр. 64KB per thread)
2. Виділяти з нього bump-алокатором
3. Скинути offset при Clear() замість individual free
```

**Оцінка трудовитрат:** 4 години

---

### ALLOC-6: MemoryList.RemoveAt — оптимізація

**Файл:** `src/Collections/MemoryList.cs:206-215`
**Пріоритет:** P2
**Вплив:** O(n) → O(1) для невпорядкованих списків

**Проблема:** RemoveAt робить shift-стиль видалення.

**Рішення:**
```
1. Додати RemoveAtSwapBack(int index) для невпорядкованих списків
2. Використати в reservedEntities (порядок не важливий)
3. Замінити byte-by-byte loop на UnsafeUtility.MemMove для звичайного RemoveAt
```

**Оцінка трудовитрат:** 1 година

---

### ALLOC-7: Виправити MemoryBlock.Size тип

**Файл:** `src/Allocator/Allocator.cs:37`
**Пріоритет:** P2

**Проблема:** `Size` — `int`, але `totalSize` — `long`. Обрізання при size > 2GB.

**Рішення:** Змінити на `long Size`.

---

## 4. Оптимізація систем та паралелізму

### SYS-1: Граф залежностей між системами

**Файл:** `src/Systems/Systems.cs:55-58`, `src/Systems/Systems.cs:632-687`
**Пріоритет:** P0
**Вплив:** Найбільший — дозволяє паралельне виконання систем

**Проблема:** Лінійний ланцюг `System0 → ECB0 → System1 → ECB1 → ...` повністю серіалізує виконання. `SystemsDependencies` інфраструктура існує але не використовується.

**Рішення — 3 етапи:**

#### Етап 1: Розділити scheduling та execution
```csharp
// Замість:
for (var i = 0; i < runners.Count; i++)
    _state.Dependencies = runners[i].Schedule(...);

// Зробити:
// Фаза 1: Schedule всі job-системи (non-blocking)
for (var i = 0; i < runners.Count; i++)
    handles[i] = runners[i].Schedule(...);

// Фаза 2: Execute main-thread системи (паралельно з jobs)
for (var i = 0; i < mtRunners.Count; i++)
    mtRunners[i].Schedule(...);

// Фаза 3: Об'єднати всі handles
_state.Dependencies = JobHandle.CombineDependencies(handles);
```

#### Етап 2: Component access analysis
```
1. Для кожної системи зберігати:
   - ReadComponents: HashSet<int> типів, що читаються
   - WriteComponents: HashSet<int> типів, що пишуться
2. Правило: System A та B можуть виконуватись паралельно якщо:
   - ReadComponents(A) ∩ WriteComponents(B) = ∅
   - WriteComponents(A) ∩ ReadComponents(B) = ∅
   - WriteComponents(A) ∩ WriteComponents(B) = ∅
3. Побудувати dependency graph при реєстрації систем
```

#### Етап 3: Множинні ECB playback
```
1. Замість одного ECB → per-system або per-batch ECB
2. Playback після кожного dependency level
3. Або: barrier-системи, що форсують playback
```

**Оцінка трудовитрат:** 20-40 годин (весь етап)

---

### SYS-2: Виправити IQueryJobSystem.Parallel

**Файл:** `src/Systems/IQueryJobSystem.cs:73`
**Пріоритет:** P1

**Проблема:** `ScheduleParallelFor(ref scheduleParams, 1, 1)` — одна ітерація, не розподіляє роботу.

**Рішення:**
```csharp
case SystemMode.Parallel:
    var workers = JobsUtility.JobWorkerCount;
    var batch = query.Count > workers ? query.Count / workers : 1;
    return JobsUtilities.ScheduleParallelFor(ref scheduleParams, query.Count, batch);
```

---

### SYS-3: Виправити batch size для IEntityJobSystem

**Файл:** `src/Systems/EntityJobSystem.cs:139`
**Пріоритет:** P1

**Проблема:** Batch count = 1 — занадто дрібне розбиття.

**Рішення:** Брати з IDelegateJobSystem (вже правильно):
```csharp
var batchCount = query->count > workers ? query->count / workers : 1;
```

---

### SYS-4: Виправити Fixed Timestep

**Файл:** `src/Systems/Systems.cs:27, 60-68`
**Пріоритет:** P1

**Проблема:**
1. Hardcoded 16ms
2. `_timeSinceLastFixedUpdate = 0` втрачає накопичений час
3. Немає multi-step catch-up

**Рішення:**
```csharp
// 1. Конфігурований інтервал (в WorldConfig або Systems)
public float FixedUpdateInterval { get; set; } = 1f / 60f;

// 2. Накопичення з відніманням
_timeSinceLastFixedUpdate += dt;
while (_timeSinceLastFixedUpdate >= FixedUpdateInterval) {
    // execute fixed systems
    _timeSinceLastFixedUpdate -= FixedUpdateInterval;
}

// 3. Обмеження max iterations (щоб не зависнути)
var maxSteps = 5;
while (_timeSinceLastFixedUpdate >= FixedUpdateInterval && maxSteps-- > 0) { ... }
```

---

### SYS-5: Виправити ECB Context Dispatch

**Файл:** `src/World/World.cs:44-47`
**Пріоритет:** P1

**Проблема:** `GetEcbVieContext` ігнорує `context` параметр — Update та FixedUpdate використовують один ECB.

**Рішення:**
```csharp
// WorldUnsafe має два ECB:
public EntityCommandBuffer UpdateECB;
public EntityCommandBuffer FixedUpdateECB;

internal ref EntityCommandBuffer GetEcbByContext(UpdateContext context) {
    return ref (context == UpdateContext.FixedUpdate
        ? ref UnsafeWorld->FixedUpdateECB
        : ref UnsafeWorld->UpdateECB);
}
```

---

### SYS-6: Видалити debug логування з production шляхів

**Файл:** `src/World/World.Unsafe.cs:497,504`, `src/Reactive/ReactiveCheckSystem.cs:59`
**Пріоритет:** P0

**Проблема:** `dbug.log()` та string interpolation виконуються в production білдах.

**Рішення:** Додати `[System.Diagnostics.Conditional("NUKECS_DEBUG")]` до всіх методів `dbug`.

---

### SYS-7: Додати [BurstDiscard] до dbug.log(string, Color)

**Файл:** `src/dbug.cs:17`
**Пріоритет:** P0

**Проблема:** Без `[BurstDiscard]` — Burst компіляція впаде при виклику цього методу.

---

## 5. Оптимізація компонентів та пулів

### COMP-1: Pre-allocate chunk pools

**Файл:** `src/Components/GenericPool.cs:317-343`
**Пріоритет:** P1
**Вплив:** Усунення lazy allocation branches на гарячому шляху

**Проблема:** `GetChunk()` перевіряє `isCreated == 0` і алокає при першому доступі.

**Рішення:** При `CreatePools()` виділити всі чанки одразу на основі `WorldConfig.StartEntitiesAmount`:
```csharp
var chunksNeeded = (maxEntities + Chunk.MAX_CHUNK_SIZE - 1) / Chunk.MAX_CHUNK_SIZE;
for (int i = 0; i < chunksNeeded; i++) {
    Chunks[i] = new Chunk { buffer = AllocateChunk() };
}
```

---

### COMP-2: Entity Generations (версіонування)

**Файл:** `src/Entity/Entity.cs:11-15`
**Пріоритет:** P0
**Вплив:** Безпека — усуває dangling entity references

**Проблема:** Немає generation counter — перероблений entity ID може посилатись на іншу сутність.

**Рішення:**
```csharp
public struct Entity {
    public int id;
    public ushort generation;       // додано
    public World.WorldUnsafe* worldPointer;

    // + масив generations в WorldUnsafe:
    // MemoryList<ushort> entityGenerations;
    // + при CreateEntity: generation = entityGenerations[id]++
    // + при доступі: перевірити entity.generation == entityGenerations[entity.id]
}
```

**Оцінка трудовитрат:** 8 годин

---

### COMP-3: Консолідувати ECB Playback

**Файл:** `src/Entity/EntityCommandBuffer.cs:353-573`
**Пріоритет:** P0
**Вплив:** Підтримуваність + correctness (вже розійшлися між 3 копіями)

**Рішення:**
```csharp
private void ProcessCommand(ref ECBCommand cmd, World.WorldUnsafe* world) {
    switch (cmd.Type) {
        case ECBCommandType.AddComponent: ...
        case ECBCommandType.RemoveComponent: ...
        // одна копія switch
    }
}

public void PlaybackMainThread(ref World world) {
    // ітерувати commands → ProcessCommand
}

public void Playback(ref World world) {
    // ітерувати thread buffers → ProcessCommand
}
```

---

### COMP-4: Reactive — Dirty-flag замість повного MemCmp

**Файл:** `src/Reactive/ReactiveCheckSystem.cs:71-80`
**Пріоритет:** P1
**Вплив:** Для 10K entity з 5 змінами/кадр: ~2000x зменшення роботи

**Проблема:** MemCmp для КОЖНОГО reactive компонента КОЖЕН кадр, навіть якщо не змінювався.

**Рішення — Dirty-flag підхід:**
```csharp
// 1. Додати [Reactive] атрибут для компонентів
// 2. Генерувати Dirty<T> tag при записі в компонент
// 3. Перевіряти лише Dirty<T> сутності:

public Query GetQuery(ref World world) {
    return world.Query().With<T>().With<Dirty<T>>();
}

public void OnUpdate(ref Entity entity, ref State state) {
    ref var c = ref entity.Get<T>();
    entity.Remove<Dirty<T>>();
    ComponentChangeEvent<T>.Invoke(ref c, ref entity);
}
```

**Альтернатива (менш інвазивна):** Використати version counter per chunk (як Unity DOTS).

---

### COMP-5: Видалити мертвий код EntityFilterBuffer

**Файл:** `src/EntityFilterBuffer.cs:98-101, 133-136`
**Пріоритет:** P3

**Опис:** Обидва методи `Playback()` мають порожні inner loops — код не реалізований. Або завершити, або видалити.

---

## 6. Якість коду та API

### API-1: Замінити dummy-parameter dispatch на named methods

**Файл:** `src/Systems/Systems.cs:102-282`, `src/SystemsGroup.cs:23-105`
**Пріоритет:** P1

**Проблема:** `Add<T>(int dymmy)`, `Add<T>(byte dymmy)`, `Add<T>(bool dymmy)` — крихка диспетчеризація.

**Рішення — Явні методи:**
```csharp
public Systems AddJob<T>() where T : unmanaged, IEntityJobSystem { ... }
public Systems AddEntityJob<T>() where T : unmanaged, IEntityJobSystem { ... }
public Systems AddMainThread<T>() where T : unmanaged, ISystem { ... }
public Systems AddMainThreadClass<T>() where T : class, ISystem { ... }
public Systems AddQueryJob<T>() where T : unmanaged, IQueryJobSystem { ... }

// Зберегти Add<T>() як generic з type inference для зручності:
public Systems Add<T>() {
    // runtime перевірка інтерфейсів
}
```

---

### API-2: Виправлення API naming

| Поточне | Виправлення | Файл |
|---------|-------------|------|
| `dymmy` | `dummy` або видалити | SystemsGroup.cs, Systems.cs |
| `CopyVieECB` | `CopyViaECB` | Entity.cs:426 |
| `GetEcbVieContext` | `GetEcbViaContext` | World.cs:44 |
| `error_no_componnet` | `error_no_component` | dbug.cs:37 |
| `massage` | `message` | dbug.cs (4 місця) |
| `dbug` (class) | `Debug` або `NukecsDebug` | dbug.cs |

---

### API-3: Query.With\<T\>(ReadWrite) — реалізувати або видалити

**Файл:** `src/Query.cs:42`
**Пріоритет:** P2

**Проблема:** Параметр `ReadWrite readWrite` приймається але ніколи не використовується.

**Рішення:** 
- Короткостроково: видалити параметр
- Довгостроково: використовувати для automatic dependency resolution (SYS-1)

---

### API-4: Розділити Editor assembly

**Файл:** `src/Nukecs.asmdef`
**Пріоритет:** P2

**Проблема:** Editor-код (ECSDebugWindow, MemoryAllocatorVisualizer, SystemsViewerWindow) компілюється з runtime.

**Рішення:**
```
1. Створити src/Editor/Nukecs.Editor.asmdef
2. includePlatforms: ["Editor"]
3. Перенести всі #if UNITY_EDITOR файли туди
4. Видалити TriInspector з runtime references (лишити в Editor)
```

---

### API-5: Виправити MemoryList.AsMemoryList\<T2\>

**Файл:** `src/Collections/MemoryList.cs:250`
**Пріоритет:** P2

**Проблема:** `length = this.length / sizeof(T2)` — неправильно для sizeof(T) != sizeof(T2).

**Рішення:** `length = (this.length * sizeof(T)) / sizeof(T2)` або заборонити для різних sizeof.

---

## 7. Потокобезпечність

### THREAD-1: CreateEntity без синхронізації

**Файл:** `src/World/World.Unsafe.cs:169-197`
**Пріоритет:** P0

**Проблема:** `lastEntityIndex++` та `reservedEntities` доступ без lock.

**Рішення:**
```
1. CreateEntity має викликатись тільки з main thread (через ECB)
2. Або: додати Interlocked.Increment для lastEntityIndex
3. Або: pre-allocate entity IDs per thread
```

---

### THREAD-2: QueryUnsafe.Add/Remove не потокобезпечні

**Файл:** `src/Query.cs:191-213`
**Пріоритет:** P1

**Проблема:** `count++` та запис в `entities[]` без синхронізації.

**Рішення:** ECB Playback (який викликає Add/Remove) має виконуватись на одному потоці. Перевірити та задокументувати це обмеження.

---

### THREAD-3: Double-check locking без volatile

**Файл:** `src/World/World.Unsafe.cs:289-298`
**Пріоритет:** P1

**Проблема:** `pool.IsCreated` читається без volatile/memory barrier.

**Рішення:** Використати `Volatile.Read` або `Interlocked.CompareExchange`.

---

### THREAD-4: WorldLock — мертвий код

**Файл:** `src/World/World.cs:226-250`
**Пріоритет:** P2

**Проблема:** `WorldLock.Lock/Unlock` ніколи не викликається. Крім того, `locks` не volatile.

**Рішення:** Або використати, або видалити.

---

### THREAD-5: StaticObjectRefStorage — глобальний не-потокобезпечний

**Файл:** `src/UnityObjectsStorage.cs:206-224`
**Пріоритет:** P1

**Проблема:** Спільний `AutoArray<object>` для всіх світів. `Array.Resize` не потокобезпечний.

**Рішення:**
```
1. Зробити per-World: додати AutoArray<object> в WorldUnsafe
2. Або: використати ConcurrentDictionary замість AutoArray
```

---

## 8. Дебаг-інфраструктура

### DBG-1: Conditional compilation для dbug

**Файл:** `src/dbug.cs`
**Пріоритет:** P0

**Рішення:**
```csharp
[System.Diagnostics.Conditional("NUKECS_DEBUG")]
[BurstDiscard]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void log(string message) {
    UnityEngine.Debug.Log(message);
}
```

Це повністю усуває виклики з non-debug білдів на рівні компілятора.

---

### DBG-2: Thread-safe hexColor в dbug

**Файл:** `src/dbug.cs:9`
**Пріоритет:** P3

**Рішення:** Замінити static field на inline:
```csharp
public static void log(string message, Color color) {
    Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>");
}
```

---

### DBG-3: NukecsDebugData.Instance — guard з #if UNITY_EDITOR

**Файл:** `src/Components/Component.cs:200`
**Пріоритет:** P1

**Рішення:**
```csharp
#if UNITY_EDITOR
if (NukecsDebugData.Instance.showInitedComponents) {
    Debug.Log(typeData.ToString());
}
#endif
```

---

### DBG-4: Додати Unity.Profiling до asmdef

**Файл:** `src/Nukecs.asmdef`
**Пріоритет:** P2

```json
"references": [
    "Unity.Mathematics",
    "Unity.Collections",
    "Unity.Burst",
    "Unity.Profiling",
    "TriInspector"
]
```

---

## 9. План імплементації по етапах

### Етап 1: Критичні виправлення (1-2 дні)

| ID | Задача | Години | Файли |
|----|--------|--------|-------|
| BUG-1 | Виправити ptr\<T\>[index] | 0.5 | `Allocator/ptr.cs` |
| BUG-3 | Виправити HasRange | 0.5 | `Collections/DynamicBitmask.cs` |
| BUG-4 | Виправити SystemDestroyer | 1 | `Systems/SystemDestroyer.cs` |
| BUG-9 | Виправити GetChunk off-by-one | 0.5 | `Components/GenericPool.cs` |
| SYS-6 | Conditional на dbug методи | 1 | `dbug.cs` |
| SYS-7 | BurstDiscard на colored log | 0.5 | `dbug.cs` |
| ALLOC-3 | Консолідувати allocator methods | 2 | `Allocator/Allocator.cs` |
| **Разом** | | **6 годин** | |

---

### Етап 2: Безпека та коректність (3-5 днів)

| ID | Задача | Години | Файли |
|----|--------|--------|-------|
| COMP-2 | Entity generations | 8 | Entity.cs, World.Unsafe.cs, Query.cs |
| BUG-2 | MemoryList.PtrOffset fix | 2 | Collections/MemoryList.cs |
| BUG-5 | Відновити World.Free() | 8 | World/World.Free.cs, Components/ |
| COMP-3 | Консолідувати ECB Playback | 4 | Entity/EntityCommandBuffer.cs |
| THREAD-1 | CreateEntity thread safety | 4 | World/World.Unsafe.cs |
| THREAD-3 | Volatile fix double-check locking | 1 | World/World.Unsafe.cs |
| BUG-7 | RNG seed fix | 0.5 | rng.cs |
| **Разом** | | **~28 годин** | |

---

### Етап 3: Продуктивність (5-7 днів)

| ID | Задача | Години | Файли |
|----|--------|--------|-------|
| ALLOC-1 | Прибрати DeFragment з hot path | 2 | Allocator/Allocator.cs |
| ALLOC-2 | Free-list алокатор | 16 | Allocator/Allocator.cs (rewrite) |
| ALLOC-5 | ECB pooled allocations | 4 | Entity/EntityCommandBuffer.cs |
| SYS-1 (partial) | Розділити scheduling/execution | 8 | Systems/Systems.cs |
| SYS-2 | Fix IQueryJobSystem.Parallel | 1 | Systems/IQueryJobSystem.cs |
| SYS-3 | Fix IEntityJobSystem batch size | 1 | Systems/EntityJobSystem.cs |
| SYS-4 | Fix Fixed Timestep | 2 | Systems/Systems.cs |
| SYS-5 | ECB Context Dispatch | 4 | World/World.cs, World.Unsafe.cs |
| ALLOC-6 | MemoryList.RemoveAtSwapBack | 1 | Collections/MemoryList.cs |
| COMP-1 | Pre-allocate chunk pools | 4 | Components/GenericPool.cs |
| **Разом** | | **~43 години** | |

---

### Етап 4: API та якість (3-5 днів)

| ID | Задача | Години | Файли |
|----|--------|--------|-------|
| API-1 | Named system registration methods | 8 | Systems/Systems.cs, SystemsGroup.cs |
| API-2 | Naming fixes (typos) | 2 | Багато файлів |
| API-4 | Editor assembly separation | 4 | Новий .Editor.asmdef, переміщення файлів |
| THREAD-5 | Per-World ObjectRef storage | 4 | UnityObjectsStorage.cs, World.Unsafe.cs |
| COMP-4 | Dirty-flag reactive system | 8 | Reactive/ (rewrite) |
| DBG-1..4 | Debug cleanup | 2 | dbug.cs, Component.cs |
| API-5 | Fix AsMemoryList | 1 | Collections/MemoryList.cs |
| **Разом** | | **~29 годин** | |

---

### Етап 5: Розширений паралелізм (7-10 днів)

| ID | Задача | Години | Файли |
|----|--------|--------|-------|
| SYS-1 (full) | Dependency graph + CombineDependencies | 24 | Systems/, Query.cs, нові файли |
| — | ReadOnly/ReadWrite query access | 8 | Query.cs, нові атрибути |
| — | Source-generated system registration | 8 | SourceGen/ |
| — | Archetype-based component storage (опціонально) | 40+ | Components/, Archetype.cs (major rewrite) |
| **Разом** | | **40-80 годин** | |

---

## 10. Порівняння з зрілими ECS-фреймворками

### Чого не вистачає (пріоритезовано)

| # | Фіча | Зрілість | Складність імплементації |
|---|-------|----------|--------------------------|
| 1 | Entity versioning | Unity ECS, Arch, Flecs | Низька (2-8 годин) |
| 2 | Automatic system dependency resolution | Unity ECS | Висока (20-40 годин) |
| 3 | Archetype-linear component storage | Unity ECS, Flecs | Дуже висока (40-80+ годин) |
| 4 | Chunk-level change tracking | Unity ECS | Середня (8-16 годин) |
| 5 | Enableable components | Unity ECS | Середня (8-16 годин) |
| 6 | Shared components | Unity ECS | Висока (16-32 години) |
| 7 | System ordering attributes | Unity ECS | Низька (4-8 годин) |
| 8 | One-frame components (автоочищення) | LeoECS | Низька (2-4 години) |
| 9 | Multi-world lifecycle | Arch | Середня (8-16 годин) |
| 10 | Source-generated system dispatch | Unity ECS 2.x | Середня (8-16 годин) |

### Метрики для порівняння (рекомендовано виміряти)

Після оптимізацій варто додати benchmark suite:

```
1. Entity spawn: 100K entities with 3 components
2. Entity destroy: 100K entities via ECB
3. Query iteration: 100K entities, read 2 components
4. Component add/remove: 10K entities, add+remove component
5. World serialization/deserialization roundtrip
6. Parallel system execution: 4 systems on 100K entities
7. Memory usage: 100K entities world
```

---

## Додаток A: Повний список файлів з проблемами

| Файл | Кількість проблем | Критичних |
|------|-------------------|-----------|
| Allocator/Allocator.cs | 7 | 2 |
| Allocator/ptr.cs | 2 | 1 |
| Collections/MemoryList.cs | 4 | 1 |
| Collections/DynamicBitmask.cs | 2 | 1 |
| Components/GenericPool.cs | 3 | 1 |
| Entity/Entity.cs | 3 | 0 |
| Entity/EntityCommandBuffer.cs | 4 | 1 |
| Systems/Systems.cs | 5 | 1 |
| Systems/SystemDestroyer.cs | 1 | 1 |
| Systems/IQueryJobSystem.cs | 1 | 0 |
| Systems/EntityJobSystem.cs | 1 | 0 |
| World/World.Unsafe.cs | 5 | 2 |
| World/World.Free.cs | 1 | 1 |
| World/World.cs | 3 | 0 |
| Reactive/ReactiveCheckSystem.cs | 2 | 0 |
| rng.cs + Unsafe.cs | 2 | 0 |
| dbug.cs | 4 | 1 |
| UnityObjectsStorage.cs | 1 | 0 |

---

## Додаток B: Алокації в garbage-collected heap (managed)

Список місць, де відбуваються managed алокації в runtime:

| Місце | Тип | Частота | Оптимізація |
|-------|-----|---------|-------------|
| DynamicBitmask.AsArray() | `ulong[]` | Debug only | Замінити на native copy |
| DynamicBitmask.ToString() | `StringBuilder` | Debug only | Guard з #if |
| ArchetypeUnsafe.ToString() | `StringBuilder` | Debug only | Guard з #if |
| Serialization.GetBytes() | `byte[]` | На серіалізацію | Reuse buffer |
| ECB Add\<T\> | `Malloc Temp` | На кожен Add | Pool/linear buffer |
| ComponentChangeEvent delegates | `MulticastDelegate` | На підписку | FunctionPointer |
| ComponentsMapCache | `Dictionary<Type,int>` | Ініціалізація | Acceptable |
| dbug.log() | `string` (interpolation) | Кожен виклик | [Conditional] |

---

*Кінець документа. Загальний обсяг робіт: ~150-200 годин для всіх етапів.*
