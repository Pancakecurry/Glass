# Glass

Glass is the internal engineering codename for a native Windows shell-enhancement product. The eventual product will provide a freely positionable and resizable taskbar or dock, native desktop widgets, a configurable translucent material system, and carefully designed motion while remaining local-first, private, and performant.

This repository is at **Phase 1 of 5 — Runtime, Surface, and Taskbar Foundation**. It contains the first production-oriented runtime, local layout persistence, multi-surface bar model, and native surface lifecycle. Its controls and bar contents are deliberately utilitarian engineering tools, not the product's final visual direction.

## Technology baseline

- C# and .NET 10 LTS
- WinUI 3 and XAML
- Windows App SDK stable 2.4.0
- Win32 and WinRT interop at the Windows platform boundary when later required
- Microsoft.UI.Composition for future high-performance rendering and motion
- System.Text.Json for versioned local state
- Pure .NET projects remain platform-independent where their responsibilities allow it

The minimum architectural Windows target is Windows 10 version 1809, build 17763. Windows 11 is the primary experience target. The architecture is prepared for x64 and ARM64, with x86 retained where practical for legacy Windows 10 support.

## Development status

Phase 1 promotes the display, AppBar, and native-message findings from Phase 0B into a runtime that can restore and manage multiple bars. It adds floating, anchored, and docked placement; settled-state persistence; topology recovery; DPI-change handling; snapping; and event-driven auto-hide. Windows runtime, visual, shell, mixed-DPI, multi-monitor, and packaging behavior still requires manual validation on real Windows hardware.

## Repository map

    src/Glass.App                  WinUI executable and composition root
    src/Glass.Core                 Pure domain models and local-state contracts
    src/Glass.Infrastructure       Versioned JSON persistence and local diagnostics
    src/Glass.Platform.Windows     HWND, display, AppBar, and media integration
    src/Glass.Rendering            Native Composition animation primitive
    src/Glass.Shell                Shell runtime, bar surfaces, snapping integration, and auto-hide
    src/Glass.Widgets.Abstractions Platform-light widget contracts and models
    src/Glass.Widgets.BuiltIn      Trusted first-party widget implementation boundary
    tests/Glass.Core.Tests         Small cross-platform tests for deterministic Core logic
    tests/Glass.Infrastructure.Tests Cross-platform persistence and recovery tests
    design/tokens.json              Provisional semantic design-token source
    docs/                           Product, architecture, design, performance, and privacy contracts
    adr/                            Architecture Decision Records

## Architectural principles

- Keep domain logic independent of Windows and UI frameworks.
- Keep Windows-specific APIs behind Glass.Platform.Windows.
- Let the application project compose features without becoming a feature-logic container.
- Prefer native, event-driven mechanisms and compositor-driven motion.
- Treat materials as a semantic system with capability-adaptive fallbacks, not as blur plus transparency.
- Keep state local and understandable; an account or backend is not required.
- Keep public interfaces small and avoid speculative frameworks.
- Preserve a clean path to x64, ARM64, and practical x86 support.

Forbidden technologies for the product architecture include Electron, Tauri, React, Node.js runtime dependencies, web UI frameworks, Python, embedded Chromium, WebView-based application architecture, cloud infrastructure, authentication, telemetry services, analytics SDKs, databases, and unnecessary C++.

## Building on Windows

Use a Windows development machine with the .NET 10 SDK and a Visual Studio installation that supports WinUI 3 and the Windows App SDK. From the repository root:

    dotnet restore Glass.sln
    dotnet build Glass.sln -c Debug -p:Platform=x64
    dotnet test tests/Glass.Core.Tests/Glass.Core.Tests.csproj -c Debug
    dotnet test tests/Glass.Infrastructure.Tests/Glass.Infrastructure.Tests.csproj -c Debug
    dotnet run --project src/Glass.App/Glass.App.csproj -c Debug -p:Platform=x64

The application remains unpackaged in Phase 1. MSIX or MSIXBundle remains the leading distribution direction, but release packaging is still deferred; see ADR 006.

The local commands above cannot be run in this macOS workspace because the .NET SDK is not installed. The Windows CI workflow is the compile/test gate; WinUI runtime behavior still requires manual Windows hardware validation.

## Privacy and local operation

The product is designed to work without an account, required backend, telemetry, or background network traffic. Layouts and settings will be stored locally. Network-dependent widgets, such as weather, must remain optional and isolated. V1 does not allow arbitrary JavaScript widgets, third-party executable plugins, or arbitrary DLL loading.

## Current and next milestone

Current phase: **Phase 1 of 5 — Runtime, Surface, and Taskbar Foundation**

The Phase 0B evidence remains in `docs/TECHNICAL_SPIKE.md`. Phase 2 is gated on orchestrator review and must not begin from this branch.

Read AGENTS.md before making changes, then read the relevant product and architecture documents for the subsystem being changed.
