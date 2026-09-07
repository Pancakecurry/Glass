# Motion

## Principles

- Motion communicates physical or state continuity.
- Compositor-driven animation is preferred for frequently animated visual properties.
- Avoid layout-heavy animation when a transform can accomplish the same effect.
- Preserve object identity across state changes where possible.
- Hover should be subtle and immediate.
- Expansion and collapse should feel continuous.
- Drag operations should feel attached to the pointer.
- Snap feedback should be clear but restrained.
- Avoid cartoon bouncing.
- Avoid universal 300 ms animations.
- Motion values should be semantic tokens.
- Respect high-refresh-rate displays.
- Reduced-motion behavior is mandatory.
- Animations must not continue consuming resources when nothing visible is changing.

## Semantic timing

Timing should describe intent such as quick feedback, standard state transition, extended surface transition, or attached interaction. The provisional durations and easing vocabulary live in design/tokens.json. They are not a mandate for a single global duration.

Spring behavior is reserved for physical attachment and continuity. It must be tuned for control and readability, not used as decorative bounce.

## Implementation boundary

Future animation infrastructure belongs in Glass.Rendering and should use Microsoft.UI.Composition where that materially improves smoothness or reduces layout work. Shell and widget projects should request semantic transitions rather than duplicating animation recipes.

Reduced motion must be a first-class state. It should shorten or remove non-essential interpolation while preserving feedback, focus, object identity, and task completion. A global switch must not be the only consideration; the system should also avoid animating when a surface is invisible or occluded.

Phase 0A does not implement a motion engine, visual-state system, animation scheduler, or motion QA harness.
