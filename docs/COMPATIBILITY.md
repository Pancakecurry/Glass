# Compatibility

## Support goals

Primary experience:

- Modern Windows 11.

Architectural support goal:

- Windows 10 version 1809 and later, build 17763+.
- Windows 11.
- x64.
- ARM64.
- x86 where practical for legacy Windows 10 support.

The Windows-specific projects target net10.0-windows10.0.17763.0 and the app declares the same minimum platform intent. Runtime behavior still requires validation on real supported operating systems.

## Capability tiers

The rendering and interaction systems should eventually select capability tiers rather than assume that every compile-time API or hardware effect is available:

### High capability

- Full material effects.
- High-refresh animation.
- Richer compositor treatment where it remains efficient.

### Normal capability

- Normal glass treatment.
- Reduced expensive effects where appropriate.
- Standard animation and refresh behavior.

### Restricted environment

- Simpler translucency or solid fallback.
- Reduced motion and expensive effects.
- Legible, usable surfaces with low resource demand.

Capability selection must be based on runtime facts and observable failures, not only on OS version or GPU branding.

## Runtime API and platform rules

- Check runtime API availability before calling APIs that are absent on older supported builds.
- Keep version-specific code behind Glass.Platform.Windows.
- Do not assume a compile-time Windows App SDK or WinRT API exists on every supported OS.
- Handle missing optional services without taking down the shell or local experience.
- Treat multi-monitor support and per-monitor DPI correctness as first-class requirements.
- Re-evaluate placement, sizing, and scale when display topology or DPI changes.
- Preserve sensible behavior when a monitor is disconnected, reordered, or has a different scale factor.

Phase 1 persists a Windows App SDK `DisplayId` value as the strongest currently available target plus primary status and last-known native bounds for fallback matching. Display topology changes re-resolve every live surface; an exact ID is preferred, overlapping last-known bounds are the next choice, and the primary display is the safe fallback. Placement is retained in logical DIPs relative to the resolved display work area and clamped visible. `WM_DPICHANGED` and settled move/resize messages trigger reapplication through centralized conversion helpers.

These paths have compile-time coverage only until the Phase 1 branch runs in Windows CI. Mixed-DPI transitions, monitor disconnect/reconnect, negative-coordinate arrangements, AppBar competition, x86, ARM64, Windows 10 1809, and Windows 11 still require manual hardware validation.

Phase 2 Windows adapters are capability-sensitive. AppsFolder enumeration, AUMID activation, GSMTC, Core Audio, clipboard history, power APIs, and shell icons must fail locally without taking down bars or unrelated widgets. Runtime API availability remains required across Windows 10 1809 and Windows 11. The x64 Windows CI build is a compile/test gate, not proof of runtime compatibility.
