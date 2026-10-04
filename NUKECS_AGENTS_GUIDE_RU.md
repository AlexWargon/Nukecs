# Nukecs: правила для агента, пишущего игровой код

[English version](NUKECS_AGENTS_GUIDE_EN.md).

Это руководство для нового игрового кода на Nukecs. Следуй ему при создании
MonoBehaviour-владельца мира, компонентов и систем. Примеры ниже самостоятельны и не требуют внешнего игрового проекта.

Актуальный API: [AGENTS.md](AGENTS.md). Хранение и инварианты:
[ARCHITECTURE.md](ARCHITECTURE.md). Явные итераторы:
[runtime iterator contract](src/Systems/FnSystems/RuntimeQuery/README.md).
При расхождении со старым примером используй актуальный API.

## 1. Lifecycle: Init, Update, OnDestroy

Один MonoBehaviour владеет игровой сессией. Мир и системы создаются в `Init()`.
Регистрация, ресурсы, создание начальных сущностей и `OnStart()` выполняются
один раз, до первого `Update()`.

Ниже — полный минимальный пример. `Awake()` вызывает `Init()`; если проект
инициализирует игру через bootstrap/DI, перенеси вызов туда и обеспечь его
выполнение до первого `Update()`.

```csharp
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs;

namespace Game.Ecs
{
    public struct Position : IComponent { public float3 Value; }
    public struct Velocity : IComponent { public float3 Value; }

    public sealed class GameEcs : MonoBehaviour
    {
        private int worldId = -1;
        private ref World world => ref World.Get(worldId);
        private Systems updateSystems;

        private void Awake() => Init();

        public void Init()
        {
            if (updateSystems != null) return;

            World.DisposeStatic();
            var createdWorld = World.Create(WorldConfig.Default16384);
            worldId = createdWorld.Id;

            updateSystems = new Systems(ref world);
            updateSystems
                .AddDefaults()
                .Add(GameSystems.Initialize, Threads.Main, path: SystemPath.Start)
                .Add(GameSystems.Move, Threads.Parallel);

            // AddRes и прикладную настройку выполняй здесь, до OnStart().
            var entity = world.Entity();
            entity.Add(new Position { Value = float3.zero });
            entity.Add(new Velocity { Value = new float3(1f, 0f, 0f) });
            world.Update(); // Только setup: применить начальные ECB-команды.

            updateSystems.OnStart();
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            updateSystems.OnUpdate(dt, Time.time);
        }

        private void OnDestroy()
        {
            if (worldId >= 0 && world.IsAlive)
                world.Dispose();
            World.DisposeStatic();
            worldId = -1;
            updateSystems = null;
        }
    }

    public static class GameSystems
    {
        [System]
        public static void Initialize(ref State state)
        {
            // Однократная настройка игровой логики на главном потоке.
        }

        [System, BurstCompile]
        public static void Move(ref Query<Position, Velocity> query, ref State state)
        {
            foreach (var (position, velocity) in query)
            {
                position.Get.Value += velocity.Read.Value * state.Time.DeltaTime;
            }
        }
    }
}
```

Правила для агента:

- Создавай мир через локальную переменную, затем присваивай `worldId`. Не обращайся
  к `World.Get(worldId)` до получения валидного ID.
- Регистрируй новые системы как статические методы с `[System]` через
  `.Add(GameSystems.Method, Threads.Parallel)`; для lifecycle используй
  `path: SystemPath.Start / Update / FixedUpdate / Destroy`.
- Не создавай новые `ISystem`/`IEntityJobSystem` реализации с регистрацией
  `.Add<MySystem>()`. Это поддерживаемый API существующего кода; новый код использует метод-системы.
- `Update()` содержит получение `dt` и `updateSystems.OnUpdate(dt, Time.time)`.
  Игровая логика, обработка событий, ввод и синхронизация идут в системы.
  Не добавляй туда ручной `world.Update()`, вторую обработку ECB или обход сущностей.
- Освобождай мир через `world.Dispose()`, затем вызывай `World.DisposeStatic()`.
  `world.Dispose()` уже завершает jobs и вызывает destroy lifecycle систем;
  отдельно вызывать `updateSystems.OnDestroy()` перед ним не требуется.

Этот шаблон рассчитан на одного владельца сессии. `World.DisposeStatic()`
сбрасывает domain-global registry, поэтому его не вызывают из каждого
вспомогательного MonoBehaviour. В проекте с несколькими одновременно живыми
мирами общий reset принадлежит bootstrap-владельцу всей сессии.

## 1.1 Регистрация нескольких систем: AddSystems и AddGroup

`AddSystems` регистрирует несколько метод-систем в указанном lifecycle и
порядке. Для каждой системы можно задать режим tuple-парой `(метод, Threads)`.
Это source-generated API: передавай известные методы с `[System]` прямо в вызов.

```csharp
// В Init(), вместо отдельных Add для тех же систем:
updateSystems
    .AddDefaults()
    .AddSystems(SystemPath.Start,
        (GameSystems.Initialize, Threads.Main))
    .AddSystems(SystemPath.Update,
        (GameSystems.Move, Threads.Parallel),
        (DamageSystems.ApplyDamage, Threads.Parallel));
```

Без tuple используется режим регистрации метода по умолчанию; задавай режим
явно, когда от него зависит корректность доступа к данным.

`AddGroup` подключает группу через `ISystemsGroup.Build(Systems, ref World)`.
Группа объединяет регистрацию систем одной подсистемы, а мир и `OnStart()`
по-прежнему принадлежат MonoBehaviour-владельцу.

```csharp
public sealed class GameplayGroup : ISystemsGroup
{
    public void Build(Systems systems, ref World world)
    {
        systems.AddSystems(SystemPath.Update,
            (GameSystems.Move, Threads.Parallel),
            (DamageSystems.ApplyDamage, Threads.Parallel));
    }
}

// Альтернативная регистрация в Init():
updateSystems
    .AddDefaults()
    .Add(GameSystems.Initialize, Threads.Main, path: SystemPath.Start)
    .AddGroup(new GameplayGroup());
```

Используй `ISystemsGroup`, а не старый `SystemsGroup` с `.Add<T>()`.
`Build` вызывается при `AddGroup`; он добавляет системы в существующий контейнер.
Порядок систем внутри группы сохраняется, а группа вставляется в позиции
своего вызова. Не регистрируй те же методы дополнительно через `Add`/`AddSystems`,
иначе они выполнятся повторно. Классы `GameSystems` и `DamageSystems` приведены
в примерах этого руководства; держи их и группу в доступном namespace.

## 2. Burst и параллельные системы — предпочтительный вариант

Сначала пытайся выразить систему через unmanaged-компоненты, `Query`, `State`,
`Res<T>` и `Events<T>`, поставить `[System, BurstCompile]` и зарегистрировать
с `Threads.Parallel`. Параллельность подходит, когда каждый worker пишет только
в свои сущности/строки и читает безопасные общие данные.

- Используй `Unity.Mathematics`, value types и параметры системы.
- Не помещай в Burst-систему `GameObject`, `Transform` UnityEngine, UI,
  managed-коллекции, строки, LINQ, managed callbacks или обращения к сервисам.
  UnityEngine-объекты синхронизируй отдельной системой `Threads.Main`
  без `[BurstCompile]`, а расчёты оставляй в Burst-системах.
- `Threads.MainRun` — аналог Unity `job.Run()`: синхронное выполнение job на
  вызывающем потоке (в обычном lifecycle — main thread), без отправки в очередь
  workers и без передачи зависимостей. Перед доступом к данным ранее запланированных jobs заверши их явно. Такой job может исполняться с Burst при совместимом коде и включённом
  Burst. Используй его для последовательной Burst-логики, которую нужно завершить
  сразу; для managed UnityEngine/UI-кода выбирай `Threads.Main` без Burst.
- `Threads.Single` используй для Burst-совместимого последовательного прохода,
  если параллельный доступ пока невозможно сделать корректным.
- Не записывай из нескольких workers в один `Res<T>`, общий счётчик или
  произвольную целевую сущность через `Entity.Get<T>()`. Такое владение требует
  последовательной обработки, partitioning, reduction или явной синхронизации.
- Не ставь `[BurstCompile]` на весь класс, если в нём есть managed-системы.
  Помечай отдельные методы и проверяй, что генератор создал runners.
- Не включай dependency graph автоматически. Если он нужен проекту,
  регистрируй его явно в `Init()` и учитывай скрытые обращения к общим данным.

### Быстрый проход: foreach непосредственно по query

По `Query` можно проходить без `.iter()` и `.par_iter()`:
`foreach (var (position, velocity) in query)`. Для подходящей системы кодоген
заменяет такой обход прямым циклом по указателям на колонки компонентов,
убирая overhead runtime-итератора и tuple-доступа. Это предпочтительный вариант
для простых массовых вычислений, включая системы с `Threads.Parallel`.

Генератор переписывает один plain foreach по первому query-параметру,
сохраняя локальные переменные, ранний return до цикла, cleanup после него,
окружающие unsafe-блоки и if-ветки. Обычные захваченные локальные переменные
передаются по ref: изменения доступны после цикла. Для одного компонента
используй `foreach (ref var value in query)`.

В Parallel окружающий код выполняется для каждого рабочего диапазона,
включая запланированный диапазон пустого query. Общие записи должны быть
потокобезопасными; действия ровно один раз за update вынеси в отдельную
main-thread систему. Managed-код перед циклом остаётся managed и может
препятствовать компиляции Burst.

```csharp
[System, BurstCompile]
public static void Move(ref Query<Position, Velocity> query, ref State state)
{
    foreach (var (position, velocity) in query)
    {
        var dt = state.Time.DeltaTime;
        position.Get.Value += velocity.Read.Value * dt;
    }
}

// Регистрация:
// updateSystems.Add(GameSystems.Move, Threads.Parallel);
```

Это быстрый «развёрнутый» проход по указателям: для dense inline-компонентов
генерируются прямые разыменования и последовательное продвижение указателей.
Здесь не подразумевается обязательный loop unrolling с дублированием тела
на несколько сущностей. Для sparse/tag-фильтров генератор выбирает подходящий
archetype-проход; pool-компоненты отключают обычный batch rewrite.

Один foreach — необходимая форма для этой оптимизации, но не гарантия:
тело и типы должны поддерживаться анализатором. Метод должен выполняться
через сгенерированный runner, зарегистрированный в `Systems`; прямой вызов
исходного метода не запускает его batch-версию. Структурные Add/Remove остаются
deferred и не должны инвалидировать данные активного прохода.

Несколько циклов по основному query, вложенный цикл внутри выбранного,
локальные функции и return/break/goto/yield внутри цикла вызывают fallback.
Не поддерживаются захваченные ref-локалы, константы, анонимные типы и имена,
начинающиеся с `_` либо равные `state`/`range`. Явные `.iter()` и `.par_iter()`
сохраняют runtime-итерацию и не переписываются.

Если batching обязателен, используй `[System, BurstCompile, RequireBatch]`:
fallback станет ошибкой компиляции `NUKECS002` с причиной. Сгенерированные
runner реализуют `ISystemCompilationInfoProvider`: смотри `CompilationInfo.Kind`
(`PointerBatch`, `ChangedBatch`, `RuntimeIteration`, `NoQuery`), `FallbackReason`
и `HasSurroundingCode`. Это статус генерации, а не подтверждение native Burst
или реально выбранной dense/sparse ветки. Пример проверки есть в README.

`FallbackDetail` описывает первое препятствие: конкретный компонент, локальную
переменную или неподдерживаемую конструкцию и способ исправления.
`FallbackFile`, `FallbackLine`, `FallbackColumn` указывают место (нумерация с 1).
С `[RequireBatch]` эта подробность попадает в ошибку на проблемном узле;
без атрибута смотри metadata runner. После устранения первого препятствия
может обнаружиться следующее. При успешном batching строки пустые, координаты 0.

Если нужен явный runtime-итератор, в Parallel/Single системе с назначенным
диапазоном используй `query.par_iter()`. `query.iter()` обходит весь запрос,
поэтому внутри Parallel каждый worker может повторно обработать все сущности.
`par_iter()` сам не запускает jobs: их запускает зарегистрированный runner.
Не заменяй им plain foreach без необходимости.

## 3. События: тег, pool payload или Events<T>

Выбирай один из трёх вариантов под смысл события.

| Требование | Представление |
|---|---|
| Сущность помечена для обработки, payload не нужен | Пустой `struct : IComponent` — тег |
| Редкое временное событие принадлежит сущности, нужен payload | `struct : IPoolComponent` |
| Нужен поток отдельных событий, в том числе несколько на одну сущность | `ref Events<T> events` с unmanaged payload |

Не создавай для событий managed event bus и коллекцию в каждой сущности.

### Тег и IPoolComponent

```csharp
public struct Recalculate : IComponent { } // Тег без данных.
public struct Health : IComponent { public float Value; }
public struct DamageRequest : IPoolComponent
{
    public Entity Source;
    public float Amount;
}

public static class DamageSystems
{
    [System, BurstCompile]
    public static void ApplyDamage(ref Query<Entity, Health, DamageRequest> query)
    {
        foreach (var (entity, health, request) in query)
        {
            health.Get.Value -= request.Read.Amount;
            entity.Remove<DamageRequest>();
        }
    }
}

// В Init():
// updateSystems.Add(DamageSystems.ApplyDamage, Threads.Parallel);
```

Производитель ставит `entity.Add<Recalculate>()` или
`entity.Add(new DamageRequest { Source = source, Amount = amount })`.
После обработки потребитель снимает тег/компонент через `Remove<T>()`.
Если событие читают несколько систем, cleanup регистрируй после последнего
читателя, а не внутри первого потребителя.

Add/Remove становятся видимыми после ECB playback. Учитывай границу между
производителем, потребителем и cleanup; не делай inline flush посреди итерации.
Один компонент на сущности — один текущий payload, а не очередь. Для нескольких
попаданий за кадр используй `Events<T>` или явно спроектированное накопление;
не рассчитывай, что повторный `Add` автоматически суммирует урон.

### Буфер Events<T>

```csharp
public struct MovedEvent
{
    public Entity Entity;
    public float3 Position;
}

public static class MovementEvents
{
    [System, BurstCompile]
    public static void Produce(
        ref Query<Entity, Position, Velocity> query,
        ref Events<MovedEvent> events,
        ref State state)
    {
        foreach (var (entity, position, velocity) in query.par_iter())
        {
            position.Get.Value += velocity.Read.Value * state.Time.DeltaTime;
            events.AddPar(new MovedEvent
            {
                Entity = entity,
                Position = position.Read.Value
            });
        }
    }

    [System]
    public static void Consume(ref Events<MovedEvent> events)
    {
        foreach (ref var ev in events)
        {
            // Unmanaged-обработка одного события.
            // Для UnityEngine/UI выдели отдельного Main-потребителя без Burst.
        }
        events.Clear(); // Здесь это последний читатель.
    }
}

// В Init(), вместо GameSystems.Move (иначе движение выполнится дважды):
// updateSystems
//     .Add(MovementEvents.Produce, Threads.Parallel)
//     .Add(MovementEvents.Consume, Threads.Main);
```

В Parallel-производителе используй `AddPar`, а не `Add`. Порядок событий от
workers не гарантирован. Заверши producers перед чтением/очисткой. Параллельный
consumer допустим при независимой обработке событий; общий `Clear()` вынеси
в отдельный последовательный шаг после всех readers.

`Events<T>` хранит события до очистки. Последний потребитель вызывает `Clear()`
либо после всех readers регистрируется отдельная cleanup-система. `AddDefaults()`
также регистрирует `DefaultSystems.ClearEvents` в позиции вызова `AddDefaults()`.
В шаблоне это начало цепочки: старый буфер чистится перед новыми producers.
Для обработки событий текущего кадра и нескольких readers задавай порядок
producer → consumers → cleanup явно; не вставляй очистку между читателями.

## 4. Компоненты на большом числе сущностей

Массовые компоненты должны быть маленькими unmanaged struct: числа, `float2/3`,
enum, флаги, `Entity`, ID/индекс общих данных. Не добавляй в них коллекции на
каждую сущность: `List`, массивы, `Dictionary`, `NativeList`, `MemoryList`,
`DynamicBuffer`, `ComponentArray` или отдельные allocation-backed containers.
Даже unmanaged контейнер несёт стоимость capacity, allocation и disposal.

Практическое правило владельца проекта: если компонент ожидается более чем на
100–200 сущностях, предпочитай отдельную общую коллекцию/хранилище, а в компоненте
держи ключ или ID записи. Это ориентир для проектирования, а не лимит фреймворка.
Размер payload и capacity учитывай даже при меньшем числе владельцев.

```csharp
public struct InventoryRef : IComponent
{
    public int InventoryId; // Ключ в отдельном InventoryStorage.
}
```

Общий storage владеет коллекциями и освобождает их. Удаление `InventoryRef`
само по себе не удаляет запись из storage: lifetime записи задаёт её владелец.

- Список целей/соседей, настройки, таблицы анимаций и общие справочники храни
  централизованно. В компоненте сохраняй ID, индекс или диапазон данных.
- Переменный набор дочерних данных при необходимости моделируй отдельными
  сущностями с `Owner`/`Parent` и маленькими компонентами; оцени число сущностей
  и стоимость запросов, прежде чем выбирать эту схему.
- Редкий тяжёлый payload выделяй отдельно; `IPoolComponent` помогает не включать
  его в inline-колонки и не мигрировать их при добавлении. Pool не отменяет
  стоимость коллекции внутри payload и сам по себе не делает её дешёвой.
- Коллекцию допускай только при реальной необходимости и ограниченном числе
  владельцев, с понятной capacity, временем жизни и освобождением.
- Managed общие данные доступны через `ResManaged<T>`/Main-систему;
  Burst-данные — через unmanaged ресурсы/общие буферы с явным владельцем.
  `Res<T>` хранит значение в арене своего мира; вне систем — `world.GetRes<T>()`.

### Коллекция внутри редкого компонента: IDisposable

Поддержка освобождения коллекций при удалении компонента предусмотрена
фреймворком. Точное имя интерфейса в текущем API — `System.IDisposable`
(не `IDispose`). Если компонент владеет контейнером, реализуй `Dispose()`
и освободи контейнер там. Например, для редкого владельца:

```csharp
public struct RarePathBuffer : IComponent, System.IDisposable
{
    public Unity.Collections.NativeList<float3> Points;

    public void Dispose()
    {
        if (Points.IsCreated) Points.Dispose();
    }
}
```

Инициализируй контейнер до использования. При `entity.Remove<RarePathBuffer>()`
удаление deferred: после ECB playback dropped disposable-компонент вызывает
`Dispose()`. Не освобождай тот же контейнер вручную перед Remove, иначе получишь
повторный disposal. Если компонент только ссылается на общую коллекцию, он
не должен освобождать её: освобождение принадлежит storage. Копирование struct
с контейнером не создаёт независимый буфер; не раздавай копии как разных владельцев.

`IDisposable` решает cleanup, но не снижает стоимость отдельной коллекции
для каждой сущности. Для массового компонента сохраняй предпочтение ключа/ID.

### Небольшие массивы: ComponentArray<T>

Для небольшого набора элементов на сущности есть `ComponentArray<T>`, где
`T : unmanaged, IArrayComponent`. Используй entity API `AddArray<T>()`,
`GetArray<T>()`, `RemoveArray<T>()`; не создавай default-массив как готовый буфер.

Методы принимают `this ref Entity`: используй изменяемую локальную копию Entity. `AddArray` применяет весь ECB мира; вызывай его только при эксклюзивной настройке вне jobs и обхода query. `GetArray` бросает исключение при отсутствии компонента. Константа ёмкости internal и недоступна игровому коду.

```csharp
public struct InventorySlot : IArrayComponent
{
    public int ItemId;
}

// Во время setup, когда для сущности действительно нужен маленький массив:
ref var slots = ref entity.AddArray<InventorySlot>();
slots.Add(new InventorySlot { ItemId = 10 });
```

В текущем исходнике `DEFAULT_MAX_CAPACITY = 16`: это фиксированный слот,
автоматического роста нет. Есть нюанс API: обычный `Add()` сейчас останавливается
при `length >= capacity - 1`, то есть позволяет добавить 15 элементов;
`AddNoResize()` проверяет `length < capacity` и позволяет заполнить все 16.
При достижении лимита эти методы молча не добавляют элемент. Проектируй размер
заранее и проверяй `Length`, если потеря элемента недопустима. Не используй
`ComponentArray` для неограниченных списков и не обходи через него правило
100–200 массовых компонентов.
Fluent query создавай один раз в setup и сохраняй. Не строй `world.Query()`
каждый кадр: запрос регистрируется и живёт до disposal мира.

## 5. Что проверить перед сдачей игрового кода

- `Init()` создаёт мир, регистрирует метод-системы, задаёт ресурсы/сущности и
  вызывает `OnStart()`; `Update()` только передаёт время в `OnUpdate()`.
- В `OnDestroy()` есть `world.Dispose()` и общий reset владельца сессии.
- Нет новых регистраций `.Add<MySystem>()` и managed-кода внутри Burst-систем.
- Parallel-системы соблюдают владение данными; явный обход использует `par_iter()`.
- У событий есть потребитель и cleanup; повторные события не теряются из-за
  выбора одного payload-компонента вместо потока.
- При числе владельцев более 100–200 компонент хранит ключ/ID общей коллекции.
- Редкий компонент с собственным контейнером реализует `IDisposable`;
  `ComponentArray<T>` используется с учётом фиксированной capacity.
- При async load сохраняется возвращённый мир:
  `world = await World.LoadAsync(path, world)`; старую struct-копию не используют.

Для нового поведения запускай связанные Unity EditMode regression-тесты.
Сам `[BurstCompile]` не доказательство native Burst: важный hot path проверяй
Burst-пробником/сгенерированным job. Editor-проход не доказывает IL2CPP.
