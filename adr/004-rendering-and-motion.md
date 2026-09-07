# ADR 004: Native rendering and motion

## Context

The product depends on polished translucent materials, spatial surfaces, and responsive interactions. Frequently animated properties must remain smooth on low-end hardware and at 120 Hz or 144 Hz where available.

## Decision

Keep rendering and motion in Glass.Rendering and use Microsoft.UI.Composition for future compositor-driven animation and material primitives. Use semantic motion tokens, preserve object identity, prefer transforms over layout-heavy animation, and define reduced-motion outcomes. Materials must have capability-adaptive fallbacks.

## Consequences

- The rendering system can avoid making a web renderer part of the product.
- Motion behavior can be shared across shell and widget surfaces without duplicating recipes.
- Native composition APIs and runtime capability checks become important implementation constraints.
- Phase 0A defines the boundary and vocabulary but does not implement Liquid Glass, a material engine, or a motion engine.

## Alternatives considered

- Web animation and browser rendering were rejected because they add a prohibited runtime and do not establish the desired native shell boundary.
- A custom CPU-rendered effect system was rejected as a default because it risks unnecessary work and poor high-refresh behavior.
- A global animation duration was rejected because interaction intent, refresh rate, visibility, and reduced-motion preferences require semantic decisions.
