# Development

## Current workflow

Phase 0A established the repository and architecture foundation. Phase 0B is limited to the Windows technical-feasibility surface described in TECHNICAL_SPIKE.md.

- Project and dependency boundaries.
- Small platform-independent contracts.
- Shared compiler and package policy.
- Provisional design tokens.
- Product, architecture, performance, privacy, compatibility, and accessibility documentation.
- The minimal WinUI application bootstrap.

Do not extend the Phase 0B probe into product taskbar UI, widget UI, settings UI, a material engine, release packaging, browser mocks, screenshot infrastructure, Playwright, or elaborate integration tests.

## Toolchain

The pure .NET projects target net10.0. Windows projects target net10.0-windows10.0.17763.0. A Windows development machine is required for WinUI and Windows App SDK restore/build/runtime work. Use the .NET 10 SDK selected by global.json and a Visual Studio installation with the WinUI 3 and Windows App SDK tooling needed by the current stable SDK.

The repository deliberately does not include Node.js, Python, Electron, Tauri, React, a web framework, WebView, a database, cloud infrastructure, or authentication.

## Suggested Windows commands

From the repository root on Windows:

    dotnet restore Glass.sln
    dotnet build Glass.sln -c Debug -p:Platform=x64
    dotnet test tests/Glass.Core.Tests/Glass.Core.Tests.csproj -c Debug
    dotnet run --project src/Glass.App/Glass.App.csproj -c Debug -p:Platform=x64

These are intended commands, not Phase 0A validation results from macOS.

## Reading order

1. AGENTS.md.
2. docs/PRODUCT_SCOPE.md.
3. docs/ARCHITECTURE.md.
4. The relevant design, compatibility, performance, privacy, or accessibility document.
5. Applicable ADRs.

## Validation discipline

Pure Core tests may run cross-platform when the matching SDK is available. Windows runtime behavior must be manually validated on Windows hardware. Do not simulate Windows to make a result look green. Report missing SDKs, unavailable Windows tooling, restore failures, and untested runtime behavior plainly.

## Future packaging

Release packaging is intentionally not implemented in Phase 0A. The leading future direction is MSIX or MSIXBundle as a single-installation experience; packaging decisions are recorded in ADR 006 and must be revisited with real Windows deployment testing.
