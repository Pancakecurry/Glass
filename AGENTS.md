# Agent rules

These rules apply to every future coding agent working in this repository.

1. Read the relevant architecture, product, and ADR documents before modifying a subsystem.
2. Stay inside the requested phase and scope.
3. Do not independently implement future roadmap features.
4. Never collapse project boundaries for convenience.
5. Do not introduce a dependency without explaining why it is needed.
6. Prefer native and event-driven Windows mechanisms.
7. Avoid unnecessary polling when an event-driven API exists.
8. Do not add speculative abstractions.
9. Do not build parallel implementations of the same system.
10. Preserve local-first and privacy assumptions.
11. Do not introduce cloud services, accounts, telemetry, or analytics.
12. Do not patch Explorer or rely on undocumented shell internals.
13. Do not make large unrelated refactors.
14. Surface architectural conflicts instead of silently working around them.
15. Keep public interfaces small.
16. Prefer composition over giant inheritance trees.
17. Avoid massive classes and catch-all utility modules.
18. Comments should explain non-obvious intent, not narrate obvious code.
19. Do not perform visual QA unless it is explicitly requested and the environment supports it.
20. Do not claim Windows runtime, rendering, shell, DPI, AppBar, WinRT, multi-monitor, or packaging validation that was not performed on Windows.
21. Never create placeholder production logic that silently pretends to work.
22. Leave clear TODOs only when a later planned phase genuinely owns them.

## Phase discipline

Phase 3 establishes the product experience, including the native material system,
Control Center, Edit Mode, final application/widget presentation, and semantic
motion. Do not begin Phase 4 packaging, updater, startup productization,
benchmark campaign, release architecture matrix, or manual Windows hardening
until Phase 3 has passed orchestrator review.

Do not add browser mocks, Playwright, screenshot regression tests, screenshot-based QA infrastructure, shell emulators, fake monitor frameworks, or elaborate integration-test infrastructure.

## Architecture conflicts

When a task conflicts with an ADR or architecture document, stop changing architecture and report the conflict in the completion summary. Do not silently change the decision or create a workaround that hides the conflict.
