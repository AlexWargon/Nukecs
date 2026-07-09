---
name: ecs-debug-v2
description: EcsDebugV2 editor window for Nukecs — cyberpunk-styled data-driven debugger with provider abstraction (live/mock), entity tables, archetype/query/resource panels, component inspector with reflection-based field editing, theme system. Use when modifying any file in src/Unity/Editor/EcsDebugV2/.
---

# EcsDebugV2 — Skill Reference

## Overview

A Unity Editor window (`EcsDebugV2Window`) providing real-time visualization of an ECS World. Uses a **provider pattern** (`IEcsDataProvider`) to abstract data sources: `LiveDataProvider` reads real `World.UnsafeWorld` data at runtime, `MockDataProvider` generates synthetic data for design-time. Styled as cyberpunk/sci-fi dark tool with swappable JSON themes.

Opened via menu **Nuke.cs > ECS Debug V2**. Requires `NUKECS_DEBUG` scripting define symbol.

## Architecture

```
EcsDebugV2Window (EditorWindow)
├── TopPanel — header bar with world dropdown, stats badges, theme/pause buttons
├── TabBar — Entities | Archetypes | Queries | Resources tabs
├── Left Panel (switches by tab)
│   ├── EntitiesTab — searchable entity list with archetype filter
│   ├── ArchetypesList — archetype cards with component chips
│   ├── QueriesList — query cards with With/Without lists
│   └── ResourcesList — resource cards
├── Inspector Panel (right side)
│   └── InspectorPanel — component cards with editable fields
└── Footer — version, pause state
```

## Data Flow

```
IEcsDataProvider
├── MockDataProvider — synthetic data, SimulateTick mutates fields randomly
└── LiveDataProvider (unsafe) — reads World.UnsafeWorld* pointers
    ├── GetEntities() → entitiesDens.GetAliveEntities() → reflection via ComponentFieldReader
    ├── GetArchetypes() → archetypesList iteration
    ├── GetQueries() → queries iteration, DynamicBitmask.Has() for with/none
    ├── SystemCount → WorldSystems.GetAll(worldId)
    ├── CreateEntity() → world.Entity()
    ├── DestroyEntity() → entity.Destroy()
    ├── AddComponent() → entity.AddIndex(typeIndex) via ECB
    ├── RemoveComponent() → entity.RemoveIndex(typeIndex) via ECB
    └── SetFieldValue() → arch.GetObject() → ComponentFieldWriter → entity.SetObject()
```

## Refresh Strategy (16ms / ~60fps tick loop)

The scheduled callback in `EcsDebugV2Window.CreateGUI()` runs every 16ms with three refresh levels:

1. **Entity list** (`RefreshLeftPanel`): rebuilds only when `Entities.Count` changes. Full DOM clear+rebuild.
2. **Component inspector** (`RefreshInspector`): rebuilds only when selected entity's component hash changes (archetype changed, different entity selected). Full DOM clear+rebuild.
3. **Field values** (`InspectorPanel.UpdateValues`): runs every frame when no structural change detected. Finds existing `TextField`/`Button` elements by name (`editor-{compName}-{fieldKey}`) and updates text/color in-place. Skipped while user is editing a text field.

Each section is wrapped in `try/catch` to prevent one failure from stopping the entire tick loop.

## File Map (all under `src/Unity/Editor/EcsDebugV2/`)

| File | Role |
|------|------|
| `EcsDebugV2Window.cs` | Main `EditorWindow`. Holds all state (Entities, Archetypes, Queries, Resources, selection). `CreateGUI()` builds layout, starts 16ms tick loop. Public methods: `SelectEntity`, `SetTab`, `CreateEntity`, `DestroyEntity`, `AddComponent`, `RemoveComponent`, `SetFieldValue`, `SwitchToWorld`, `TogglePause`. |
| `IEcsDataProvider.cs` | Interface + DTOs: `WorldInfo` (Name, WorldNames, WorldSlots), `IEcsDataProvider` (GetEntities, GetArchetypes, GetQueries, GetResources, CRUD ops, Tick, WorldCount, SetWorld). |
| `MockDataProvider.cs` | Mock implementation. `SimulateTick` mutates random fields. `WorldCount => 1`. |
| `LiveDataProvider.cs` | **unsafe** implementation reading `World.UnsafeWorld*`. Contains `ComponentFieldReader` (reflection: reads public fields from boxed `IComponent` — handles float/int/bool/string/enum/Entity/Vector2/3/4/Quaternion/Color/nested IComponent) and `ComponentFieldWriter` (writes FieldValue back via reflection). |
| `MockData.cs` | DTO types (`FieldValue`, `ComponentInfo`, `EntityInfo`, `ArchetypeInfo`, `QueryInfo`, `ResourceInfo`) + `MockData` static helper (generates mock entities, archetypes, queries, resources). |
| `TopPanel.cs` | Header: glowing pulse dot, world name label (click → GenericMenu dropdown for multi-world), tick counter, stat badges (ENT/ARCH/Q/SYS), "+ New Entity" button, theme switcher dropdown, pause button. `Update()` refreshes tick/pause/world labels. |
| `TabBar.cs` | Tab strip: Entities, Archetypes, Queries, Resources. `TabKey` enum. Active tab highlighted with Lime border. |
| `EntitiesTab.cs` | Entity list: search TextField with placeholder, archetype filter chips, scrollable entity rows (ID, name, archetype badge). `Create()` builds full list, `UpdateValues()` updates count label only. |
| `ArchetypesList.cs` | Archetype cards: component name chips, entity count, occupancy bar. |
| `QueriesList.cs` | Query cards: With/Without component lists, matched entity count. |
| `ResourcesList.cs` | Resource cards (empty for LiveDataProvider — ResStorage has no enumeration API). |
| `InspectorPanel.cs` | Right panel: entity inspector with component cards, editable fields (TextField for numbers/strings, Button toggle for bools, entity ref links). "Add Component" and "Destroy Entity" buttons. `DrawEntityInspector` does full build, `UpdateEntityFieldValues` does fast in-place text update. |
| `Footer.cs` | Bottom bar: version label, pause indicator. |
| `EcsDebugV2Theme.cs` | Static facade: exposes colors (`Background`, `Panel`, `Lime`, `Orange`, `Red`, `MutedText`, etc.), font sizes (`FontBody`, `FontSmall`, `FontMicro`, `FontMini`), border radius constants. Delegates to `EcsDebugV2ThemeData`. |
| `EcsDebugV2ThemeData.cs` | `[Serializable]` theme data. Loads from JSON in `Assets/Nukecs/EcsDebugV2Themes/`. `EnsureBuiltinThemes()` creates Default/Unity themes if missing. `AdaptiveSkin` flag auto-switches colors for pro/light skin. |

## Key Conventions

- **Guard**: All files use `#pragma warning disable CS0618` + `#if UNITY_EDITOR && NUKECS_DEBUG`.
- **Namespace**: `Wargon.Nukecs.Editor.EcsDebugV2` for all files.
- **Unsafe**: `LiveDataProvider` is `unsafe class`. Accesses `WorldUnsafe*` pointers, `entitiesDens`, `archetypesList`, `queries`, `entityLocations`.
- **Element naming**: Inspector fields use `editor-{compName}-{fieldKey}` for TextField/Button elements, `frow-{compName}-{fieldKey}` for row containers. `UpdateValues` finds them by name.
- **No IMGUI**: Pure UIElements (VisualElement, Label, Button, TextField, ScrollView).
- **No Border shorthand**: Uses `SetupRadius()` and `SetupBorder()` extension methods from theme.
- **Hover effects**: `RegisterCallback<MouseEnterEvent>` / `<MouseLeaveEvent>`.
- **Same assembly**: All files fall under `Nukecs.asmdef`, so `internal` members like `WorldSystems.GetAll()` are accessible.

## LiveDataProvider Unsafe Access Patterns

| Data | API |
|------|-----|
| Alive entity IDs | `world.UnsafeWorld->entitiesDens.GetAliveEntities()` → `Span<int>` |
| Entity archetype | `world.UnsafeWorld->GetEntityArchetypePtr(entityId).Ref` → `ref ArchetypeUnsafe` |
| Archetype components | `arch.types` (foreach over `MemoryList<int>`) |
| Archetype entity list | `arch.packedEntities.Ptr[i]` for i in 0..`arch.count` |
| Component boxed read | `arch.GetObject(entityId, typeIndex)` → `IComponent` |
| Component type name | `ComponentTypeMap.GetType(typeIndex)?.Name` |
| All type indices | `ComponentTypeMap.TypesIndexes` (iterable `List<int>`) |
| Queries | `world.UnsafeWorld->queries.Ptr[i].Ref` for i in 0..`queries.Length` |
| Query with/none | `q.with.Has(typeIdx)` / `q.none.Has(typeIdx)` (DynamicBitmask) |
| System count | `WorldSystems.GetAll(worldId)` → sum `runners.Count + fixedRunners.Count` |
| World list | Iterate `World.Get(i)` for i in 0..`World.WorldCapacity`, check `w.IsAlive && w.UnsafeWorld != null` |
| Create entity | `world.Entity()` returns `ref Entity` |
| Destroy entity | `entity.Destroy()` (extension method, adds `DestroyEntity` component via ECB) |
| Add component | `entity.AddIndex(typeIndex)` (internal extension, queues ECB) |
| Remove component | `entity.RemoveIndex(typeIndex)` (queues ECB) |
| Set component | `entity.SetObject(boxedComponent)` (immediate write via ComponentHelpers.Write) |
| Entity name | Check `Name` component: `arch.GetObject(id, nameTypeIndex)` → cast to `Name` → `.value.Value` |
| Entity validity | `entity.IsValid()` checks `id != 0 && entities.ElementAt(id).id != 0` |

## World Switching

- `WorldInfo.WorldSlots` tracks actual world slot indices (not array positions) for correct `World.Get(slot)`.
- TopPanel world label click builds `GenericMenu` from `WorldInfo.WorldNames`, passes `WorldSlots[i]` to `SwitchToWorld()`.
- `SwitchToWorld()` calls `ldp.SetWorld(index)` + `InvalidateEntityCache()` to force full refresh.

## Theme System

- `EcsDebugV2ThemeData` is `[Serializable]` with color/font/border fields.
- Themes stored as JSON in `Assets/Nukecs/EcsDebugV2Themes/`.
- `EcsDebugV2Theme` is static facade: `Background`, `Panel`, `PanelElevated`, `PanelBorder`, `Lime`, `Orange`, `Red`, `Yellow`, `MutedText`, `Foreground`, `TypeNumber`, `TypeString`, `TypeBool`, `TypeEntity`.
- Font constants: `FontBody`, `FontSmall`, `FontMicro`, `FontMini`.
- Extension methods: `SetupRadius(element, float)`, `SetupBorder(element, Color)`, `WithAlpha(Color, float)`, `CreateGlowDot(Color, int)`, `CreateHeaderRow()`, `CreateActionBtn(string, Color, Action)`.
- Theme button in TopPanel shows dropdown of `AvailableThemes`, calls `SwitchTheme()` + `CreateGUI()`.
