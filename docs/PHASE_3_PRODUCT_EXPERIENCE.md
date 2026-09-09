# Phase 3 product experience

## Outcome

Phase 3 converts the Phase 1 shell and Phase 2 application/widget runtime into
the first cohesive Glass product experience. Normal startup now presents native
bars, standalone widgets, and `ControlCenterWindow`; the retained development
controls are not part of the normal product path. This phase does not include
packaging, signing, updating, startup registration productization, or a release
architecture matrix.

Windows runtime and visual behavior have not yet been manually validated.

## Phase 2 functional closure

Bar application items use cached Windows Shell icons, stable identities, one
running-applications slot, quiet non-color indicators, active and multi-window
states, tooltips, keyboard focus, activation/launch, a native window chooser,
pin/unpin, deterministic reorder, cooperative `WM_CLOSE`, and close-all. The
application catalogue refreshes explicitly and whenever its selector opens.

Every widget type is created by its own trusted instance/controller path.
Visibility follows standalone-window presentation, bar visibility, and bar
auto-hide; `ProviderCoordinator` activates shared providers only while at least
one dependent instance is visible. Moving a widget between desktop and bars
preserves its instance ID and state. Permanent removal deletes per-instance
state. Configuration changes recreate only the affected runtime instance.

Clipboard history uses the Windows history API and reports available, disabled,
denied, and unsupported states without persistence. File and folder selection
uses HWND-owned pickers. Weather can request device location only after an
explicit button press and also accepts manual coordinates.

## Visual language and material model

`design/tokens.json` defines semantic typography, spacing, radii, opacity,
elevation, material, interaction, motion, target-size, and z-layer tokens.
Shared WinUI resources provide reusable control and text styles. Segoe UI
Variable is preferred with Windows fallbacks, and Glass controls use Fluent
system glyphs rather than emoji.

`GlassMaterialController` is the only top-level material entry point. It
combines one supported Windows backdrop with tint/luminosity, overlay, restrained
border and edge highlight, radius, opacity, and shadow. Profiles are Clear,
Frost, Smoke, Crystal, and Solid. Bars/widgets normally use Desktop Acrylic;
Control Center may use Mica. High Contrast forces a solid treatment. No custom
blur-radius control or undocumented DWM behavior is exposed.

The default is a compact 52-DIP bottom bar with 32-DIP application icons, a
20-DIP outer radius, restrained spacing, neutral clear glass, and subtle depth.
One zone model supports Unified, Segmented, and Minimal rendering.

## Appearance and persistence

`settings.json` schema version 1 stores normalized global appearance, theme,
motion, taskbar behavior, widget defaults, sparse per-bar/per-widget overrides,
and custom presets. Writes are atomic and local. Resolution is:

    global appearance -> surface override -> widget override

Reset to Global removes the sparse override. Built-in presets are Glass Clear,
Glass Frost, Glass Smoke, Glass Crystal, and Minimal Solid. A user may save,
rename, duplicate, delete, and continue editing local custom presets.

## Control Center

The native Control Center has Overview, Bars, Widgets, Appearance, Behavior,
and Advanced/About destinations. Overview contains concise status and quick
actions. Bars exposes display, placement, edge, orientation, length, thickness,
surface mode, auto-hide, z-order, applications, widgets, spacers, and ordered
content. Widgets contains a searchable trusted built-in gallery and existing
instance operations. Instance controls support size plus relevant Clock/Date,
Timer, and Weather settings. Appearance provides live theme, preset, material,
density, icon, and magnification controls, with detailed material values grouped
under Advanced Material.

## Widget presentation

Widget view mode derives from logical dimensions and selects compact, standard,
or expanded composition rather than scaling a single tree. The implementation
provides:

- Media: artwork, metadata, previous/play-pause/next, timeline, and seek.
- Clock and Date: timezone/format-aware time and compact or detailed date layouts.
- CPU, RAM, Network, Storage, Battery, and Audio: live provider values; volume,
  mute, volume opening, and Sound Settings are actionable where relevant.
- Calendar: month navigation, Today, and date selection.
- Timer and Stopwatch: start/pause/resume/reset, duration, laps, and elapsed state.
- Calculator: keyboard expression input, keypad, clear, and evaluation.
- Quick Notes: local editor with local save status.
- Clipboard: current text, explicit refresh, Windows history, and unavailable states.
- Storage Utility: volume list, capacity state, and open-volume actions.
- Shortcuts: HWND-owned file/folder addition, open/remove, and broken-target state.
- Weather: manual or one-shot location, current conditions, high/low, short
  forecast, stale-cache state, and MET Norway attribution.
- Pomodoro: phase, timer, completed progression, start/pause/reset/advance.

## Edit Mode and menus

Edit Mode operates on the actual surfaces. Selection lifts a bar or widget,
adds a semantic outline and label, exposes intentional drag/resize behavior, and
keeps Phase 1 placement/geometry authoritative. Native context menus provide
bar editing and creation, application actions, and widget customize/resize,
z-order, attach/detach, duplicate, lock, and remove actions. Control Center gives
keyboard-operable alternatives for reorder, host transfer, placement, size, and
configuration; no critical operation is drag-only.

## Motion and accessibility

`GlassMotionController` owns semantic compositor animations for hover, press,
appear, dismiss, snap, reveal, auto-hide, surface lift, flyout, widget transition,
and dock magnification. Magnification uses transforms for the hovered item and
neighbors, updates only while the pointer is in the application region, and has
no permanent loop. `MotionPolicy` resolves System, Full, and Reduced modes.
Reduced mode disables magnification and spring overshoot and uses short fades or
immediate transitions. Native controls retain focus visuals and automation names;
application state is not communicated by color alone.

## Performance and privacy

There is one backdrop per top-level surface, no nested blur, no decorative
continuous timer, no layout-driven magnification, and no startup network work.
Shared providers are visibility-ref-counted. Weather is explicit and cached;
clipboard values are never persisted; notes and clipboard values are never
logged. Glass still has no account, telemetry, arbitrary JavaScript, executable
plugin loading, cloud dependency, administrator requirement, or Explorer patch.

## Validation boundary

Windows CI is responsible for restore, Release x64 compilation, and deterministic
pure-.NET tests. XML, JSON, repository hygiene, and dependency direction can be
checked on macOS. Rendering quality, WinUI interaction, Windows APIs, shell
behavior, AppBar negotiation, multi-monitor/DPI behavior, accessibility tooling,
and performance budgets require later manual validation on real Windows hardware.
