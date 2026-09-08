# Phase 2 applications and widgets

## Functional milestone

Phase 2 establishes the functional initial product scope without claiming final presentation. Bars contain ordered typed items: pinned applications, one dynamic running-applications slot, widgets, and fixed or flexible spacers. Pins persist stable application identity; process IDs and window handles remain transient.

## Application integration

`WindowsApplicationCatalog` enumerates the Shell Applications namespace. Identity preference is AUMID, Shell parsing identity, then canonical executable path. `RunningWindowTracker` creates an initial `EnumWindows` snapshot and uses rooted out-of-context WinEvent callbacks with one-shot burst coalescing. Pure Core rules filter and group candidates. Launching uses Shell execution or documented AUMID activation. Close is cooperative `WM_CLOSE`; Glass never terminates another process.

## Widgets

`Glass.Widgets.Runtime` owns an explicit trusted registry, idempotent Created/Mounted/Visible/Suspended/Disposed lifecycle, reference-aware shared providers, and versioned per-instance state at `state/widget-<guid>.json`. No reflection scanning or executable plugin loading occurs.

Exactly one host may reference a widget instance. Schema normalization removes duplicate hosts. Standalone widgets use frameless tool windows, remain outside normal taskbar and Alt+Tab participation, persist logical placement, and do not use WorkerW or Progman manipulation.

## Built-in scope

Tier 0: media, clock, date, CPU, RAM, network, storage, battery, and audio. Tier 1: calendar, timer, stopwatch, calculator, Quick Notes, clipboard, storage utility, shortcuts, weather, and Pomodoro. Deterministic logic uses `TimeProvider`; calculator expressions use a local parser, never code evaluation. Providers are shared and visibility-aware.

Weather uses MET Norway Locationforecast 2.0 compact over HTTPS with truthful identification, cache headers, conditional requests, stale offline fallback, explicit coordinates, and MET Norway/CC BY attribution. The core shell remains fully usable offline.

## Deferred validation and design

The Windows CI workflow compiles Release x64 and runs deterministic tests. Real Windows validation remains required for shell discovery, icons, WinEvent behavior, activation, media, audio, clipboard, power, DPI, multi-monitor, input, and visuals. Phase 3 owns final visual design, Liquid Glass, full customization UI, and final motion.
