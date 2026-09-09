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
| Glass.Infrastructure | net10.0 | Versioned JSON documents, atomic local files, corruption recovery, and local diagnostics | Glass.Core |
| Glass.Widgets.Abstractions | net10.0 | Minimal widget identity, metadata, sizing, capability, and instance-configuration contracts | None |
| Glass.Widgets.Runtime | net10.0 | Explicit trusted registry, lifecycle, provider visibility, and per-instance state | Core, Widgets.Abstractions |
| Glass.Platform.Windows | net10.0-windows10.0.17763.0 | HWND/AppWindow access, native messages, displays, DPI conversion, AppBar, and media sessions | Glass.Core, Microsoft.WindowsAppSDK |
| Glass.Rendering | net10.0-windows10.0.17763.0 | Native top-level material controller and semantic Microsoft.UI.Composition motion | Glass.Core, Microsoft.WindowsAppSDK |
| Glass.Shell | net10.0-windows10.0.17763.0 | Shell runtime, bar-surface lifecycle, placement, snapping integration, and auto-hide | Glass.Core, Glass.Platform.Windows, Glass.Rendering, Glass.Widgets.Abstractions |
| Glass.Widgets.BuiltIn | net10.0 | Trusted first-party widget metadata and deterministic feature logic | Core, Widgets.Abstractions, Widgets.Runtime |
| Glass.App | net10.0-windows10.0.17763.0 | WinUI executable, bootstrap, lifecycle, and top-level composition | All feature and contract projects |
| Glass.Core.Tests | net10.0 | Small deterministic tests for Core | Glass.Core |
| Glass.Infrastructure.Tests | net10.0 | Deterministic state-store, schema, and recovery tests | Glass.Core, Glass.Infrastructure |

Phase 3 retains one application integration path and one trusted widget runtime. Platform.Windows owns Windows application, window, media, metrics, power, audio, clipboard, picker, and location adapters. Rendering owns the only material and semantic motion implementation. Widgets.Runtime never discovers code through reflection and is not a plugin SDK. The app remains the composition and WinUI host boundary.

## Allowed future edges

- Glass.Platform.Windows may reference Glass.Core when a Windows adapter implements a Core contract or consumes a Core model.
- Glass.Rendering may reference Glass.Core when rendering state or motion contracts require a domain concept.
- Glass.Shell may reference Glass.Core, Glass.Platform.Windows, Glass.Rendering, and Glass.Widgets.Abstractions as its implementation requires.
- Glass.Widgets.BuiltIn may reference Glass.Core, Glass.Widgets.Abstractions, Glass.Platform.Windows, and Glass.Rendering as individual widgets require.
- Glass.Widgets.Runtime may reference Glass.Core and Glass.Widgets.Abstractions only.
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

Core owns the state contracts and domain documents. Glass.Infrastructure implements deterministic System.Text.Json serialization under `%LOCALAPPDATA%/Glass`, same-directory temporary writes followed by replacement, explicit schema versions, a small migration entry point, timestamped corrupt-input backups, and local diagnostics. Infrastructure is plain `net10.0` and cannot reference Windows or the app. Shell state is written only after create/remove/configuration operations, completed native move/resize operations, and display recovery—not during pointer movement.

## Application composition

Glass.App explicitly composes `ApplicationRuntime`, local stores, platform providers, `ProviderCoordinator`, `WidgetRuntime`, `ShellRuntime`, surface factories, and `ControlCenterWindow`. `ApplicationRuntime` retains top-level ownership and coordinates deterministic shutdown. `ShellRuntime` owns authoritative bar/widget definitions and active bar surfaces. App-instance redirection is handled with Windows App SDK `AppInstance`; reusable lifecycle and surface logic remains outside `App.xaml.cs`. Product-facing strings remain centralized in `Glass.App/Configuration/ProductBranding.cs`.

## Forbidden dependency edges

- Glass.Core and Glass.Infrastructure must not reference Windows, WinUI, Windows App SDK, rendering, shell, or application projects.
- Glass.Platform.Windows must not reference Shell, Rendering, Infrastructure, Widgets, or App.
- Glass.Rendering must not reference Platform.Windows, Shell, Infrastructure, Widgets, or App.
- Glass.Shell may consume Core, Platform.Windows, Rendering, and Widgets.Abstractions; it must not reference Infrastructure or App.
- Glass.Widgets.BuiltIn must not reference Shell or App.
- Glass.Widgets.Runtime must not reference Platform.Windows, Rendering, Shell, BuiltIn, or App.
- Glass.App is the composition root; no project may reference it.
- Circular references and parallel surface engines are forbidden.

## Configuration policy

global.json selects a .NET 10 SDK baseline with feature-band roll-forward. Directory.Build.props holds shared compiler and build defaults. Directory.Packages.props centrally pins the intentionally small package set. design/tokens.json is the Phase 3 semantic design source. `Glass.Rendering` and shared WinUI resources translate those semantics into native materials and controls; user settings remain sparse overrides rather than duplicated themes.

## Phase 4 production runtime

ApplicationRuntime now composes activation intent, session health, adaptive
rendering policy, startup-task state, fullscreen observation, and redacted local
diagnostics. These are orchestration responsibilities; pure decision rules stay
in Core and documented Windows adapters stay in Platform.Windows. The production
Control Center code-behind is split by page responsibility, widget factories only
route to focused presenters, and bar application-item presentation is isolated
from surface lifecycle.

Packaged and unpackaged modes share one application project. Unpackaged is the
developer default. Packaged Release is self-contained, identity-bearing MSIX;
architecture-specific packages are combined externally by the release workflow.
Package-associated local data is used when identity exists, while development
continues under `%LOCALAPPDATA%/Glass`. No packaging concern crosses into Core.
