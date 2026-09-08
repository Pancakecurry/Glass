# Glass

Glass is the internal engineering codename for a native Windows shell-enhancement product. The eventual product will provide a freely positionable and resizable taskbar or dock, native desktop widgets, a configurable translucent material system, and carefully designed motion while remaining local-first, private, and performant.

This repository is at **Phase 0B — Windows Technical Spike**. It contains a deliberately utilitarian development surface and reusable native primitives for the six highest-risk Windows assumptions. It does not contain the product taskbar, widgets, settings experience, material engine, or production visual direction.

## Technology baseline

- C# and .NET 10 LTS
- WinUI 3 and XAML
- Windows App SDK stable 2.4.0
- Win32 and WinRT interop at the Windows platform boundary when later required
- Microsoft.UI.Composition for future high-performance rendering and motion
- System.Text.Json for simple local serialization when persistence is implemented
- Pure .NET projects remain platform-independent where their responsibilities allow it

The minimum architectural Windows target is Windows 10 version 1809, build 17763. Windows 11 is the primary experience target. The architecture is prepared for x64 and ARM64, with x86 retained where practical for legacy Windows 10 support.

## Development status

Phase 0B implements code paths for a frameless probe surface, Desktop Acrylic, compositor animation, display discovery, AppBar docking, and global media-session control. Windows runtime, visual, shell, media, mixed-DPI, multi-monitor, and packaging behavior still requires manual validation on real Windows hardware. This macOS workspace cannot provide that validation.

## Repository map

    src/Glass.App                  WinUI executable and composition root
    src/Glass.Core                 Pure domain models and local-state contracts
    src/Glass.Platform.Windows     HWND, display, AppBar, and media integration
    src/Glass.Rendering            Native Composition animation primitive
    src/Glass.Shell                Probe surface placement coordination
    src/Glass.Widgets.Abstractions Platform-light widget contracts and models
    src/Glass.Widgets.BuiltIn      Trusted first-party widget implementation boundary
    tests/Glass.Core.Tests         Small cross-platform tests for deterministic Core logic
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
    dotnet run --project src/Glass.App/Glass.App.csproj -c Debug -p:Platform=x64

The application remains an unpackaged WinUI technical spike so Phase 0B does not prematurely select release packaging. Package.appxmanifest records the globalMediaControl capability required by the future packaged route. MSIX or MSIXBundle remains the leading distribution direction; see ADR 006.

The commands above have not been run in this macOS workspace because the .NET SDK is not installed and WinUI runtime behavior requires Windows. Do not interpret the existence of the project files as Windows validation.

## Privacy and local operation

The product is designed to work without an account, required backend, telemetry, or background network traffic. Layouts and settings will be stored locally. Network-dependent widgets, such as weather, must remain optional and isolated. V1 does not allow arbitrary JavaScript widgets, third-party executable plugins, or arbitrary DLL loading.

## Current and next milestone

Current phase: **Phase 0B — Windows Technical Spike**

Windows runtime validation is pending. The next phase is gated on orchestrator review.

Read AGENTS.md before making changes, then read the relevant product and architecture documents for the subsystem being changed.
