# Performance

## Product requirement

Performance is a product requirement, not a later optimization pass. The eventual shell must feel immediate on low-end through high-end Windows hardware, remain quiet when idle, and degrade expensive effects before interaction quality or legibility.

## Initial aspirational budgets

These are engineering goals, not claims that Phase 0A has achieved them:

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

No performance number in this document is a benchmark result. Phase 0A creates no instrumentation framework and makes no Windows performance claim.
