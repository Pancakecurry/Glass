# Phase 1 runtime and shell foundation

## Scope

Phase 1 replaces the Phase 0B probe composition with one production-oriented runtime and surface path. The visible controls and zone labels are engineering tools only. Widgets, final taskbar content, the Control Center, product materials/motion, shell replacement, and release packaging remain deferred.

## Runtime ownership

`App` performs only Windows App SDK instance registration/redirection and creates `ApplicationRuntime`. The primary instance owns `ApplicationRuntime`; a secondary invocation redirects its activation and exits. Redirected activation brings the development controls window forward.

`ApplicationRuntime` explicitly composes local data paths, diagnostics, JSON layout storage, `WindowsDisplayService`, `ShellRuntime`, the surface factory, and the development controls window. `ShellRuntime` owns the persisted layout and every live `IBarSurface`. Closing the controls window disposes surfaces first, which unregisters AppBars and native subclasses, then disposes the display watcher before process exit.

## State and persistence

State is local at `%LOCALAPPDATA%/Glass`:

    state/shell-layout.json
    backups/
    logs/glass.log

`settings.json` is reserved for the first meaningful application settings; no empty settings model is manufactured in this phase. Layout JSON is UTF-8, human-readable, deterministic, and wrapped in `schemaVersion` and `payload`. Schema 1 is current; schema 0 is the explicit migration entry. Unsupported future or malformed documents are moved to timestamped backups, defaults are used, and a local diagnostic is written.

Writes use a unique temporary file in the state directory and a same-volume replacement. Persistence keys reject separators, dots, and other filename syntax. Shell state is written after create, remove, reset, control changes, display recovery, and `WM_EXITSIZEMOVE`; pointer movement does not write state.

## Coordinate and placement model

Persisted geometry uses logical device-independent pixels relative to the target display work area. Native screen pixels exist only in the Windows adapter and last-known display metadata. `DpiConverter` owns the boundary conversion.

Each placement has exactly one shape:

- `FloatingPlacement`: target plus relative logical rectangle; no edge.
- `AnchoredPlacement`: target, edge, center-relative along-edge offset, and logical size; no work-area reservation.
- `DockedPlacement`: target, edge, and logical thickness; AppBar reservation spans the edge.

Each bar has a stable GUID `BarId`; `SurfaceId` is available for later non-bar shell surfaces. A `DisplayTarget` stores the Windows App SDK display value, whether it was primary, and last-known native bounds. Resolution prefers the exact ID, then overlapping bounds, then a safe primary/available display. Resulting geometry is clamped into the current work area.

`WM_DPICHANGED` re-applies placement through current conversion. `DisplayAreaWatcher` topology events cause all surfaces to re-resolve and docked bars to re-register against fresh display data. Runtime API and mixed-DPI behavior still require physical Windows validation.

## Snapping

`SnapEngine` is pure deterministic geometry. It finds the nearest of all four work-area edges within a 12-DIP threshold, supports negative desktop coordinates, clamps the rectangle visible, and returns a center-relative along-edge offset. Snapping is applied only when Windows reports a settled move/resize through `WM_EXITSIZEMOVE`.

## Bar model

`BarDefinition` includes stable identity, placement, independent horizontal/vertical orientation, fit/fixed/fill length mode, bounded logical length and thickness, Start/Center/End structural zones, auto-hide, z-order, enabled state, and normalization. The default layout contains one horizontal bottom-anchored centered bar. `ShellRuntime` uses a dictionary keyed by `BarId`, supports multiple bars, and creates/removes/updates windows without a parallel engine.

The production `BarWindow` is frameless, resizable, omitted from normal task switcher/taskbar participation, and shown without activation. It requests Desktop Acrylic with a solid fallback, but its primitive labels are not a material or visual-design commitment.

## AppBar behavior

Docked placement reuses the Phase 0B AppBar sequence behind `BarSurfaceController`: register, query, constrain thickness, set, respond to position callbacks, and unregister. Current display data is supplied on every placement and topology reapplication. Docking failure unregisters and rolls the native window back before the controller converts the definition to a safe anchored fallback and reports a local runtime diagnostic. Disposal always unregisters before the native callback registration and subclass are removed.

## Auto-hide

Auto-hide is an event-driven four-state controller: Visible, PendingHide, Hidden, and Revealing. Pointer exit starts one cancellable 650 ms delay only for an enabled bar. Pointer entry cancels pending work and reveals the surface. Hidden surfaces leave a two-pixel edge reveal strip; there is no cursor polling.

For a docked bar, hiding first removes AppBar registration so a concealed surface does not reserve a permanent empty work-area strip. The hidden bar behaves as an edge overlay; revealing reapplies its docked placement and reservation. Disabling auto-hide cancels pending work and restores normal placement. All delay tokens, AppBar registrations, callbacks, and state handlers are disposed with the surface.

## Deferred work

- Final Control Center information architecture and styling.
- Real taskbar applications, running states, widgets, and edit mode.
- Product material and motion systems; the Phase 0B composition helper is disconnected evidence only.
- Manual Windows 10/11, x64/ARM64/x86, mixed-DPI, multi-monitor, AppBar, auto-hide, focus, and performance validation.
- Release packaging and installation.
