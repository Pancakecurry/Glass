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

## Phase 3 implementation

`GlassMotionController` in Glass.Rendering maps semantic intents—hover, press, appear, dismiss, snap, reveal, auto-hide, surface lift, flyout, and widget transition—to compositor scale, translation, and opacity animations. Shell and app surfaces request an intent rather than duplicating animation recipes. Magnification changes transforms only while the pointer is over the application region and settles without a permanent animation loop.

Reduced motion must be a first-class state. It should shorten or remove non-essential interpolation while preserving feedback, focus, object identity, and task completion. A global switch must not be the only consideration; the system should also avoid animating when a surface is invisible or occluded.

System, Full, and Reduced preferences resolve through `MotionPolicy`. Reduced mode removes magnification and overshoot, shortens non-essential transitions, and preserves state feedback. Windows runtime timing and high-refresh behavior still require manual hardware validation.
