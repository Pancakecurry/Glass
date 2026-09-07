# ADR 002: Project and dependency boundaries

## Context

The product combines domain rules, Windows integration, native rendering, shell surfaces, widgets, and application lifecycle. A single project would make platform coupling and feature ownership difficult to control.

## Decision

Use separate projects for Glass.Core, Glass.Widgets.Abstractions, Glass.Platform.Windows, Glass.Rendering, Glass.Shell, Glass.Widgets.BuiltIn, and Glass.App. Core is plain net10.0 and has no UI or Windows references. Widget abstractions remain platform-light and are not a third-party plugin SDK. Glass.App is the composition root, not a feature-logic container.

Add project references only when source code requires them. The allowed future direction is Core toward platform and features, widget contracts toward trusted consumers, and the app toward composition.

## Consequences

- Platform-independent logic can be tested without Windows.
- Windows-specific APIs have one clear ownership boundary.
- Feature projects can evolve without making the application project a catch-all.
- The solution has more project files and requires contributors to understand the dependency graph.
- Empty Phase 0A implementation projects remain intentional boundaries, not invitations to add speculative code.

## Alternatives considered

- A single application project was rejected because it would collapse domain, platform, rendering, and shell concerns.
- A large shared framework or dependency-injection layer was rejected because it would add abstraction before a concrete need.
- A general plugin SDK was rejected because V1 uses trusted built-in widget code only.
