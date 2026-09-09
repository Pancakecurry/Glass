# Performance

## Product requirement

Performance is a product requirement, not a later optimization pass. The eventual shell must feel immediate on low-end through high-end Windows hardware, remain quiet when idle, and degrade expensive effects before interaction quality or legibility.

## Initial aspirational budgets

These are engineering goals, not claims that Phase 3 has achieved them:

- Idle CPU below roughly 0.5 percent when realistically achievable.
- Idle GPU effectively near zero.
- Baseline memory target below approximately 120–150 MB.
- No unnecessary continuous timers.
- No polling when an event-driven API exists.
- 60 Hz smoothness as the minimum.
- Architecture capable of 120 Hz and 144 Hz interaction.
- Expensive effects with graceful quality tiers.
- Invisible widgets must not perform expensive rendering.
- Background refresh rates should reduce when data is not visible.
- Fullscreen and high-load scenarios should permit throttling.
- Startup should eventually feel effectively immediate.
- No background network traffic unless a feature explicitly requires it.

## Engineering rules

- Make visibility, occlusion, focus, and load state available to later scheduling decisions.
- Prefer event subscriptions, invalidation, and demand-driven work over polling.
- Keep visual animation on the compositor when it avoids UI-thread or layout work.
- Stop or reduce refresh work when a widget or surface is not visible.
- Isolate optional network work from local shell responsiveness.
- Add measurement before optimizing a hot path.
- Treat startup, steady state, monitor changes, DPI changes, and fullscreen transitions as separate performance scenarios.
- Keep quality-tier changes understandable to users and reversible.

## Phase 1 architecture

- Display topology uses `DisplayAreaWatcher`; it is not polled.
- Native callbacks route by message ID and return directly for unrelated messages without allocating event arguments.
- Placement is persisted only after settled operations such as `WM_EXITSIZEMOVE`, not while the pointer moves.
- Auto-hide uses a cancellable one-shot delay only for enabled bars; there is no cursor polling or permanent timer.
- AppBar position callbacks reapply the last current request and are removed during deterministic disposal.
- Hidden bars do not run animation or refresh loops in this phase.

No performance number in this document is a benchmark result. Phase 1 adds no telemetry or continuous instrumentation and makes no Windows performance claim.

## Phase 2 scheduling

- Running windows use WinEvent hooks plus a short one-shot coalescing delay, never a permanent scan timer.
- System metrics use one shared one-second sampler only while at least one dependent widget is visible.
- Clock and date refresh at semantic boundaries; hidden widgets are suspended.
- Battery, audio, clipboard, display, and media state prefer operating-system events.
- Weather requests are opt-in, serialized, conditionally cached, and isolated from shell startup.
- HICON ownership is bounded and all owned handles are destroyed.

## Phase 3 rendering and interaction

- Each top-level surface has one native backdrop; nested blur surfaces are not created.
- Material brushes are applied from centralized profiles rather than decorative update loops.
- Hover, press, reveal, edit lift, and magnification use compositor transforms and opacity rather than layout animation.
- Pointer-position work exists only while the pointer is inside the application region.
- Widget providers are reference-counted by actual surface visibility, including bar auto-hide.
- Application icons are cached by stable identity, requested at rendered size, and populated on demand.
- Widget and application galleries populate only when their Control Center page is active. Later release hardening may strengthen incremental virtualization after measurement.
- Weather remains explicit, serialized, cached, and absent from startup work.

No Phase 3 budget is claimed as achieved until profiling on supported Windows hardware.

## Phase 4 idle architecture

- Normal startup creates shell surfaces but network providers remain lazy.
- Windows-startup activation does not construct Control Center.
- One shared Clipboard service owns the event subscription for all clipboard
  widget consumers.
- Timer, stopwatch, and Pomodoro presentation timers exist only while loaded and
  semantically running; correctness comes from timestamps, not tick counts.
- Explorer, foreground, display, session, power, audio, media, and clipboard
  changes use event/message paths rather than permanent scans.
- Auto rendering quality reduces shadows, magnification, transparency, and
  decorative motion under energy saver and remote sessions.
- Safe Mode restores only the minimum surface set with Solid rendering.
- Diagnostics are sampled only when the Advanced page requests a snapshot.

The release configuration is self-contained for installation reliability. Its
larger package size is an intentional distribution tradeoff and does not imply a
larger steady-state process footprint. Idle CPU/GPU, startup, and memory budgets
remain aspirational until Phase 5 profiling on real Windows systems.
