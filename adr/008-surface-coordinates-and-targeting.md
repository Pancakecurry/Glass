# ADR 008: Surface coordinates and display targeting

## Context

Persisting native pixels or transient monitor objects would make layouts fragile across DPI and topology changes.

## Decision

Persist surface geometry as logical DIPs relative to a `DisplayTarget`. Keep native pixels in the Windows boundary except for last-known display bounds used only as fallback metadata. Model placement as distinct floating, anchored, and docked records. Resolve targets by exact display ID, overlapping historical bounds, then primary/available display, and clamp recovered surfaces visible.

## Consequences

- Placement meaning survives many scale and coordinate changes.
- Conversion is centralized and testable geometry stays Windows-free.
- Display IDs are the strongest practical identifier currently exposed by Windows App SDK, not a promise of permanent hardware identity.
- Mixed-DPI and topology behavior must still be validated on real hardware.

## Alternatives considered

- Absolute native-pixel persistence was rejected because it bakes in scale and desktop coordinates.
- Boolean docking flags were rejected because they allow contradictory states.
- Persisting `DisplayInfo` was rejected because it is runtime topology state.
