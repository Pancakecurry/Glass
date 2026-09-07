# Architecture

## Intent

The architecture separates platform-independent product rules from Windows APIs, native rendering, shell surfaces, widget contracts, and application composition. The foundation should make the later product powerful without turning the executable into a catch-all or coupling the domain to WinUI.

The system is local-first. There is no required account, backend, database, telemetry service, or cloud synchronization path.

## Dependency philosophy

The intended dependency direction is:

    Glass.Core
        ↑
    platform and feature projects may depend on Core where needed

    Glass.Widgets.Abstractions
        ↑
    trusted widget implementations and shell surfaces may consume widget contracts

    Glass.App
        composes the feature projects and owns application bootstrap

The arrows describe allowed direction, not a request to add references preemptively. A project reference is added only when source code requires it.

## Project boundaries

| Project | Target | Responsibility | Current direct references |
| --- | --- | --- | --- |
| Glass.Core | net10.0 | Pure domain models, platform-independent geometry, and local-state contracts | None |
| Glass.Widgets.Abstractions | net10.0 | Minimal widget identity, metadata, sizing, capability, and instance-configuration contracts | None |
| Glass.Platform.Windows | net10.0-windows10.0.17763.0 | Win32/WinRT, monitors, DPI, windows, media, system status, AppBar, and shell integration boundary | None in Phase 0A |
| Glass.Rendering | net10.0-windows10.0.17763.0 | Future Microsoft.UI.Composition helpers, materials, shadows, motion, and visual-state primitives | None in Phase 0A |
| Glass.Shell | net10.0-windows10.0.17763.0 | Future desktop surfaces, dock layout, placement, snapping, auto-hide, edit mode, and lifecycle | None in Phase 0A |
| Glass.Widgets.BuiltIn | net10.0-windows10.0.17763.0 | Trusted first-party widget implementations | Glass.Widgets.Abstractions |
| Glass.App | net10.0-windows10.0.17763.0 | WinUI executable, bootstrap, lifecycle, and top-level composition | All feature and contract projects |
| Glass.Core.Tests | net10.0 | Small deterministic tests for Core | Glass.Core |

The Windows-specific project references deliberately contain no OS implementation in this phase. Their Windows target and boundaries are prepared without pretending that shell, DPI, monitor, WinRT, or composition behavior exists.

## Allowed future edges

- Glass.Platform.Windows may reference Glass.Core when a Windows adapter implements a Core contract or consumes a Core model.
- Glass.Rendering may reference Glass.Core when rendering state or motion contracts require a domain concept.
- Glass.Shell may reference Glass.Core, Glass.Platform.Windows, Glass.Rendering, and Glass.Widgets.Abstractions as its implementation requires.
- Glass.Widgets.BuiltIn may reference Glass.Core, Glass.Widgets.Abstractions, Glass.Platform.Windows, and Glass.Rendering as individual widgets require.
- Glass.App may reference the feature projects to compose them, but reusable feature logic must remain outside the application project.

No project may introduce a reverse reference from Core or widget contracts to WinUI, Windows App SDK, Windows namespaces, shell APIs, rendering APIs, cloud services, or a web stack.

## Forbidden dependencies and patterns

- Glass.Core must not reference WinUI, Windows App SDK, Windows namespaces, shell APIs, rendering APIs, or a UI framework.
- Glass.Widgets.Abstractions must not become a third-party plugin SDK in V1.
- Glass.Platform.Windows is the only home for Windows OS integration.
- Glass.Rendering must use native Windows composition facilities when implementation begins; it must not become a web renderer or a blur-only styling package.
- Glass.Shell must not patch Explorer or rely on undocumented shell internals.
- Glass.App must not become a repository for reusable domain or feature logic.
- No circular project references.
- No dependency is justified solely by implementation convenience.

## State and persistence

Core owns only the persistence contract. The first storage implementation should remain local, explicit, and replaceable. System.Text.Json is the initial serialization choice; it should live at the persistence boundary rather than leak a database or network model into Core. Layout, widget instance configuration, preferences, and presets must be locally understandable and recoverable.

State writes should be deliberate and resilient. Later work must define schema versioning, corruption handling, atomic writes, and migration before treating persisted state as stable.

## Application composition

Glass.App currently creates a single development window and activates it. It references the feature projects so the composition root is visible, but it does not instantiate shell surfaces, widgets, platform services, or rendering systems yet. Product-facing strings are centralized in Glass.App/Configuration/ProductBranding.cs.

## Configuration policy

global.json selects a .NET 10 SDK baseline with feature-band roll-forward. Directory.Build.props holds shared compiler and build defaults. Directory.Packages.props centrally pins the intentionally small package set. design/tokens.json is the provisional semantic source for future design-system work; it is not a compiled theme or a claim that visual values are final.
