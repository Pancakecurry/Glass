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

Phase 3 adds centralized runtime fallback decisions: High Contrast forces Solid material, reduced motion removes magnification and overshoot, optional clipboard history and one-shot location report unavailable or denied states locally, and weather cache failure does not affect the shell. Clear, Frost, Smoke, and Crystal use supported Windows App SDK backdrops only; no undocumented DWM path exists. x86, ARM64, Windows 10, and Windows 11 runtime behavior remain unvalidated until their later release matrix and hardware pass.

## Phase 4 compatibility matrix

| Environment | Intended behavior | Phase 4 evidence |
|---|---|---|
| Windows 10 1809+ | Architectural minimum; guarded optional APIs; simpler effects where required | Compile/package target only |
| Windows 10 22H2 | Supported shell/widget behavior | Compile target; manual QA pending |
| Current Windows 11 | Primary material and interaction experience | Compile target; manual QA pending |
| x64 | Full release path | Windows CI build/package |
| ARM64 | Native release path | Windows CI build/package; hardware QA pending |
| x86 | Practical legacy path | Windows CI build/package; runtime QA pending |
| RDP | Auto Reduced quality; no magnification | Pure policy + compile path |
| High Contrast | Solid material overrides user quality | Pure policy + live settings event |
| Reduced motion | No magnification/overshoot; short or immediate transitions | Policy and motion mapping |
| No battery | Battery widget unavailable state | Existing provider fallback |
| No network | Shell remains usable; weather uses cache/unavailable state | Provider isolation |
| No media session | Media widget disabled/no-session state | Event-backed provider fallback |
| No location permission | Manual coordinates remain available | One-shot permission path only |

Explorer recreation, suspend/resume, session lock/unlock, display changes,
default audio endpoint changes, and media-session changes have documented
message/event recovery paths. Every row still requires Phase 5 runtime validation
on representative real Windows systems.
