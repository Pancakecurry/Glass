# ADR 005: Windows 10 1809 minimum with capability adaptation

## Context

The primary experience is Windows 11, but the product should preserve a practical path to Windows 10 version 1809 and later, including older hardware and multiple processor architectures. OS APIs and visual capabilities vary across that range.

## Decision

Use Windows 10 build 17763 as the architectural minimum and target net10.0-windows10.0.17763.0 for Windows-specific projects. Prepare for x64 and ARM64, and retain x86 where practical. Runtime API availability and capability tiers must be checked at runtime rather than assumed from compile-time availability.

The future rendering model will expose high, normal, and restricted capability behavior. Multi-monitor topology and per-monitor DPI correctness are first-class requirements.

## Consequences

- The product must keep compatibility-sensitive code behind Glass.Platform.Windows.
- Visual effects and motion cannot assume Windows 11-only behavior or high-end hardware.
- Older operating systems may receive simpler material and motion treatment while retaining usable behavior.
- Support testing will require real Windows installations, architectures, monitors, scaling settings, and hardware profiles.

## Alternatives considered

- Windows 11-only support was rejected because it would abandon the stated Windows 10 compatibility goal.
- A lowest-common-denominator visual system was rejected because capability tiers can preserve richer treatment where it is safe.
- Compile-time OS assumptions were rejected because installed OS capabilities and runtime API availability can differ.
