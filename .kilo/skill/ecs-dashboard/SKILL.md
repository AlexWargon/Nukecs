---
name: ecs-dashboard
description: ECS Dashboard editor window for Nukecs — cyberpunk-styled visual debugger with entity tables, archetype panels, component inspector, and theme system. Use when modifying any file in src/Unity/Editor/EcsDashboard/.
---

# ECS Dashboard — Skill Reference

## Overview

A Unity Editor window (`NukecsDashboardWindow`) that provides real-time visualization of an ECS World: entities, archetypes, components, systems, and memory. Styled as a cyberpunk/sci-fi dark tool.

## File Map (all under `src/Unity/Editor/EcsDashboard/`)

| File | Role |
|------|------|
| `DashboardTheme.cs` | `[Serializable] DashboardThemeData` class (colors, loaded from JSON at `Assets/Nukecs/DashboardTheme.json`). Static facade `DashboardTheme` exposes properties (`BgDark`, `AccentCyan`, etc.) delegating to the data instance. `DashboardStyles` provides factory methods: `SectionTitle`, `PillBadge`, `NeonSeparator`, `CreateSearchField`, `ShineLine`, `GlowDot`, `CreateGradientLine`. |
| `NukecsDashboardWindow.cs` | Main `EditorWindow`. Layout: top gradient bar → top bar → main area (left sidebar \| center (archetypes + entity table) \| right inspector) → bottom bar. Holds all selection state, proxy cache, foldout states. `CreateGlowCard()` factory used by archetype panel and inspector. 250ms data refresh, 33ms inspector refresh via `schedule.Execute`. |
| `DashboardTopBar.cs` | LIVE dot (`GlowDot`), pause button, time, world selector dropdown, entity/system count badges, "Reload Theme" button, NUKECS branding. `unsafe void Update()` refreshes counters. |
| `DashboardLeftSidebar.cs` | Group cards (archetype groups + "All"). `CreateGroupCard()` builds each. Accent bar animates width on hover (3→5px). "All" card has gradient overlay. Progress bar per group. |
| `DashboardArchetypePanel.cs` | Horizontal archetype cards. 200×120, radius 12, `ShineLine()` at top. Selected card has outer glow (2px larger wrapper). Component chips (radius 6). Occupancy bar with bright tip. |
| `DashboardEntityTable.cs` | Two view modes: **Archetype view** (archetype selected) → fixed columns per component type showing first field value or `#tag`. **All view** → Entity, Name, Archetype columns + fixed-width component name cells in a single row. Custom search field (replaces `ToolbarSearchField`). Uses `MakeFixedCell()` with `flexShrink=0` for alignment. |
| `DashboardEntityInspector.cs` | Right panel component inspector. Title card with entity ID pill badge. Component cards: borderless (`VisualElement` with `BgCard` bg), 5px left accent bar, foldout, TAG pill badge, remove button (circle, red on hover). `DrawInspector()` does full rebuild, `UpdateInspector()` does proxy-only refresh (respects `EditorGUIUtility.editingTextField`). |
| `DashboardBottomPanel.cs` | Memory bar (10px), stat badges (radius 10), system chips with `GlowDot`. `unsafe` blocks for memory access. |

## Theme System

- `DashboardThemeData` is `[Serializable]` with all colors as public `Color` fields.
- Auto-loads from `Assets/Nukecs/DashboardTheme.json` on first access.
- Creates default JSON if file doesn't exist.
- `DashboardTheme.Reload()` re-reads JSON; `DashboardTheme.Save()` writes current values.
- Top bar "Reload Theme" button calls `Reload()` + `window.CreateGUI()`.
- `DashboardTheme` is a static facade: `BgDark`, `BgPanel`, `BgCard`, `AccentPurple`, `TextPrimary`, etc. are properties, not fields.
- `WithAlpha(this Color, float)` extension method lives in `DashboardTheme`.
- `FontSize` constants: `TitleLarge=16`, `TitleMedium=14`, `Body=12`, `Small=10`, `Micro=9`.
- `AccentForType(string)` / `AccentForArchetype(int)` generate deterministic colors from hash.

## Key Patterns & Conventions

- **Wrapper**: All files use `#pragma warning disable CS0618` + `#if UNITY_EDITOR && NUKECS_DEBUG`.
- **Unsafe**: `DashboardTopBar.Update()`, `DashboardBottomPanel.DrawMemorySection()`, `DashboardBottomPanel.DrawStatsSection()`, and `DashboardEntityTable.RefreshList()` have `unsafe` blocks accessing `world.UnsafeWorld->` pointers.
- **No `ToolbarSearchField`** — custom `DashboardStyles.CreateSearchField()` uses `TextField` + placeholder `Label` + focus/blur border color changes.
- **No `BorderRadius` shorthand** — all four corners set individually (`borderTopLeftRadius`, etc.).
- **Column alignment**: `MakeFixedCell(element, width)` sets `width`, `flexShrink=0`, `overflow=Hidden`. Both headers and data rows use `CreateRowContainer()` with identical `paddingLeft/Right = RowPaddingH`.
- **Hover effects**: `RegisterCallback<MouseEnterEvent>` / `MouseLeaveEvent>` — never CSS.
- **ComponentProxy**: defined in `ECSDebugWindow.cs:1496`, public, same namespace. Used for IMGUI component field editing.
- **CanWriteToWorld**: `ECSDebugWindowUI.CanWriteToWorld` static flag gates component write-back.

## Entity Table View Modes

### Archetype View (`SelectedArchetypeId >= 0`)
- Headers: `Entity(60) | Name(120) | [CompName1(100)] | [CompName2(100)] | ...`
- Each component column header = short component type name
- Cell content = first public field value (via reflection), or `#tag` if `typeData.isTag`
- `#tag` displayed in `AccentGreen` + bold
- Data extracted during `RefreshList()` into `EntityRowData.componentCellTexts`

### All View (`SelectedArchetypeId < 0`)
- Headers: `Entity(60) | Name(150) | Archetype(70) | Components(flex)`
- Each component rendered as fixed-width cell (`ColComp=100`) with truncated component name in its accent color
- Single row, no wrap, no borders — same visual as archetype view cells

## Inspector Component Cards

- **No borders** — plain `VisualElement` with `DashboardTheme.BgCard` background, `borderWidth=0`.
- 5px left accent bar (absolute positioned).
- Foldout button 20×20, rounded.
- TAG badge: green pill, radius 8.
- Remove "✕" button: circle 20×20, `Color(0.1,0.1,0.15)` bg, red on hover.
- "Add Component" button: pill, 2px border in `AccentPurple.WithAlpha(0.3f)`.
- "Destroy Entity" button: red pill.

## Color Palette (defaults, editable via JSON)

| Color | RGB | Usage |
|-------|-----|-------|
| BgDark | 17, 20, 22 | Root, center panel |
| BgPanel | 19, 21, 22 | Sidebars, top/bottom bars, inspector |
| BgCard | 28, 30, 33 | Component cards, group cards |
| BgCardHover | 38, 40, 46 | Hovered cards |
| BgCardSelected | 50, 36, 70 | Selected rows/cards |
| TextPrimary | 252, 254, 255 | Component names, section titles |
| TextSecondary | 107, 115, 148 | Dimmed text |
| AccentPurple | 184, 77, 255 | Primary accent |
| AccentCyan | 0, 240, 255 | Secondary accent |
| AccentGreen | 57, 255, 20 | Tags, LIVE dot |

## Column Width Constants (`DashboardEntityTable`)

```
ColEntity = 60
ColName = 120
ColArch = 70
ColComp = 100
RowPaddingH = 12
```
