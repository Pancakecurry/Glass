# ADR 007: Runtime and state ownership

## Context

The Phase 0B window directly owned probe services. Production shell surfaces require deterministic shared lifetime, one logical app instance, and recoverable persisted state.

## Decision

Use explicit constructor composition without a DI container. `ApplicationRuntime` owns application-level services and top-level windows; `ShellRuntime` owns layout and live bar surfaces. Use Windows App SDK `AppInstance` for primary-instance registration and activation redirection. Implement versioned local JSON in the plain `Glass.Infrastructure` project.

## Consequences

- Shutdown has one path for saving settled state, closing surfaces, unregistering AppBars/hooks, and disposing display services.
- Core and Infrastructure remain cross-platform and testable.
- App remains a thin composition/activation boundary.
- Adding a lifecycle-owned service requires an explicit constructor and disposal decision.

## Alternatives considered

- A third-party DI container was rejected as unnecessary infrastructure.
- Window-owned global services were rejected because they obscure lifetime and shutdown ordering.
- Mutex/socket single-instancing was rejected because Windows App SDK provides documented activation redirection.
