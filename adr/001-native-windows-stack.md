# ADR 001: Native Windows stack

## Context

The product is a Windows shell enhancement whose quality depends on native windowing, input, composition, DPI, monitor, and OS integration behavior. It must remain lightweight, local, and compatible with Windows 10 1809+ and Windows 11.

## Decision

Use C# with .NET 10 LTS, WinUI 3, XAML, and the stable Windows App SDK 2.4.0. Keep Win32 and WinRT interop behind the Windows platform project. Use Microsoft.UI.Composition for future high-performance visual and motion work.

## Consequences

- Native Windows behavior remains accessible without introducing a web runtime.
- The app can share pure .NET domain logic across platforms while keeping OS code isolated.
- Windows development and runtime validation require a real Windows toolchain and hardware.
- The project must respect Windows App SDK and OS API availability across the support range.

## Alternatives considered

- Electron, Tauri, React, WebView, and embedded Chromium were rejected because they add a web runtime and weaken native shell, performance, privacy, and DPI boundaries.
- A C++-first application was rejected because C# is sufficient for the Phase 0A foundation and the requested WinUI stack; C++ remains available only if a future capability proves it necessary.
