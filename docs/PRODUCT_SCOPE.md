# Product scope

## Product definition

Glass is a native Windows shell-enhancement product, not an Explorer replacement. Its eventual experience centers on a freely positionable and resizable custom taskbar or dock, native desktop widgets, a sophisticated translucent material system, and high-quality motion. It must remain lightweight, local-first, private, and useful without an account or an internet connection.

Phase 2 implements the initial functional application and Tier 0/Tier 1 widget scope. Final visual design, Liquid Glass, complete customization, premium motion, production packaging, and manual Windows hardening remain later phases.

## Initial release boundary

The initial release extends through Tier 1. The scope is intentionally bounded so that quality, performance, accessibility, and compatibility can be treated as product requirements rather than deferred cleanup.

### Shell and surface scope

The eventual shell and surface work may include:

- A floating custom bar.
- Top, bottom, left, and right edge docking.
- Free screen positioning.
- Resizing.
- Horizontal and vertical layouts.
- Multiple-monitor support.
- Snapping.
- User-defined sections or zones.
- Pinned applications.
- Running applications.
- Active and running states.
- Auto-hide.
- Configurable appearance.
- Configurable spacing, corner radius, and material.
- Saved layouts and presets.
- Edit mode.
- Widget attachment to bars.
- Standalone desktop widgets.

The product will not patch Explorer or replace undocumented Windows shell internals in the initial release.

### Tier 0 widgets

Tier 0 is the baseline trusted built-in set:

- Media, including Spotify through Windows media sessions where possible.
- Clock.
- Date.
- CPU.
- RAM.
- Network.
- Storage.
- Battery.
- Volume and audio.

### Tier 1 widgets

Tier 1 may add:

- Calendar and date browser.
- Timer.
- Stopwatch.
- Calculator.
- Quick notes.
- Clipboard utility.
- Richer storage utility.
- App, file, and folder shortcuts.
- Weather.
- Pomodoro.

Weather and other internet-dependent features must be optional, isolated, and incapable of making the rest of the application depend on network availability.

## Explicit V1 non-goals

- Widget marketplace.
- Third-party executable plugins.
- Arbitrary JavaScript widgets.
- Arbitrary DLL or native plugin execution.
- User accounts.
- Cloud sync.
- AI features.
- Explorer patching.
- Shell replacement.
- Dozens of external SaaS integrations.
- A scripting language.

## Scope rules

Any feature proposal must explain its user value, privacy impact, Windows compatibility impact, performance cost, and ownership within the project boundaries. A roadmap item is not permission to implement it before its orchestrated phase.
