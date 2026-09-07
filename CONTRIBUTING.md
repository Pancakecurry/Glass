# Contributing

Glass is being initialized as a native Windows product. Contributions should preserve the small, explicit architecture and the local-first privacy model.

## Before changing code

1. Read AGENTS.md.
2. Read the relevant document in docs/ and any applicable ADR in adr/.
3. Confirm that the change belongs to the current phase.
4. Check the dependency direction before adding a project reference or package.

## Development environment

Pure .NET projects can be developed and tested on any supported .NET host when the SDK is available. WinUI, Windows App SDK, Win32, WinRT, shell, DPI, composition, multi-monitor, and packaging work requires Windows hardware and the matching Windows development toolchain.

Do not add a cross-platform simulation to make Windows-only behavior appear testable on macOS. A structural change may be validated statically when Windows is unavailable, but the limitation must be stated.

## Change expectations

- Keep public contracts minimal and platform-neutral where possible.
- Put Windows API access in Glass.Platform.Windows.
- Keep reusable feature logic out of Glass.App.
- Prefer event-driven APIs over polling.
- Keep serialization local and explicit.
- Avoid new dependencies unless the dependency is necessary, maintained, and documented.
- Do not introduce accounts, cloud services, telemetry, analytics, arbitrary plugins, or Explorer patches.
- Do not commit credentials, local settings, generated output, certificates, or machine-specific files.
- Keep unrelated work out of the same change.

## Validation

Run only checks supported by the current environment. On Windows, the intended baseline is:

    dotnet restore Glass.sln
    dotnet build Glass.sln -c Debug -p:Platform=x64
    dotnet test tests/Glass.Core.Tests/Glass.Core.Tests.csproj -c Debug

Windows runtime validation is a separate responsibility from pure Core tests. Manual validation should cover the relevant Windows version, architecture, DPI, monitor, shell, input, rendering, performance, accessibility, and packaging requirements once those features exist.
