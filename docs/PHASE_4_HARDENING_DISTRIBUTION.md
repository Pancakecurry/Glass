# Phase 4 — Hardening and Distribution

## Outcome

Phase 4 converts the feature-complete Glass experience into a recoverable,
capability-adaptive, distributable Windows product. It does not add V1 feature
scope and it does not claim manual Windows validation.

## Maintainability

Control Center behavior is divided into focused Overview, Bars, Widgets,
Appearance, Behavior, and Advanced partial implementations. The window owns
navigation and lifetime coordination. Built-in widget UI is routed through a
small factory into system, productivity, and utility presenters. Widget runtime
types are similarly separated into provider-backed, time-based, and stateful
implementations. Application item rendering is isolated from the bar surface
lifecycle.

## Startup contract

- A normal launch starts shell surfaces and presents Control Center.
- A packaged startup-task activation starts shell surfaces quietly.
- First normal launch presents the short onboarding window.
- Redirected activation presents the existing process's Control Center.
- `--safe-mode` forces the recovery profile.

The packaged startup toggle uses `windows.startupTask`. Unpackaged development
builds report it unavailable and do not install an alternative persistence
mechanism.

## Recovery

The session-health record is marked active before product state is restored and
marked clean only during orderly shutdown. Three consecutive detected unclean
starts cause the next launch to use Safe Mode. Safe Mode uses solid rendering,
disables auto-hide, avoids restoring optional standalone widget surfaces, and
opens Control Center with normal restart, diagnostics, backed-up reset, and exit
commands.

`TaskbarCreated` triggers documented Explorer-restart recovery. Bars re-register
AppBars or restore their anchored/floating placement; running windows and the
application catalogue refresh. Power and session messages suspend provider
visibility and reconcile displays, bars, widgets, running windows, and device
state on resume/unlock. Display watcher events retarget inaccessible bars and
widgets through the existing persisted placement model.

## Adaptive quality

`RenderingPolicy` maps Auto, Full, Balanced, Reduced, and Solid preferences to
material and motion capabilities. Auto considers High Contrast, transparency
and animation preferences, energy saver, remote sessions, and composition
availability. High Contrast always resolves to Solid. Reduced and Solid disable
magnification and expensive decorative work while preserving behavior.

## Efficiency

Providers remain visibility-reference-counted. Weather and its HTTP client are
created on first use. Clipboard uses one shared WinRT event subscription.
Timer, stopwatch, and Pomodoro UI refresh runs only while the view is loaded and
the semantic timer is active; elapsed time remains timestamp-derived. Icon,
artwork, weather, and metric data remain bounded or replace-current caches.

## Diagnostics and privacy

Advanced diagnostics are local and user-initiated. Copy/export contains version,
architecture, OS, package mode, startup duration, process memory, active surface
and provider counts, rendering policy, display count, and recovery state. It
excludes notes, clipboard text, weather coordinates, and user paths. No data is
uploaded.

## Distribution

Development builds remain unpackaged. Release builds use single-project,
self-contained architecture-specific MSIX packages for x64, ARM64, and x86.
The release workflow combines those packages into an MSIXBundle with MakeAppx.
This includes the .NET and Windows App SDK runtime in the package so the public
installation design has no manual runtime prerequisite.

Direct distribution uses an App Installer template with on-launch and background
native update checks. The release base URI and package publisher are repository
configuration, not source constants. Microsoft Store publication uses the same
package identity and codebase.

Unsigned CI artifacts are validation outputs only. Public distribution requires
a certificate whose subject matches the configured package publisher. The PFX
and password enter the release workflow only through GitHub secrets and are never
committed or uploaded.

## Validation boundary

Windows CI owns restore, architecture builds, deterministic tests, package
creation, bundle validation, and optional signature verification. Phase 5 owns
manual Windows runtime, visual, shell, DPI, assistive-technology, installation,
update, and uninstall validation.
