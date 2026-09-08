# ADR 009: Bar domain model

## Context

The product needs multiple independently configured bars without coupling their definition to one WinUI window or prematurely designing widget content.

## Decision

Represent each bar with a stable GUID `BarId`, one coherent placement, independent orientation, semantic length mode, bounded logical dimensions, structural Start/Center/End zones, auto-hide policy, z-order, and enabled state. `ShellRuntime` maps IDs to one live surface implementation.

## Consequences

- Layout can round-trip without serializing UI or display objects.
- Multiple bars and future zone content have stable ownership.
- Structural zones establish layout roles but intentionally contain no widget system.
- Invalid dimensions are rejected by placements or normalized by bar constraints.

## Alternatives considered

- One global taskbar settings object was rejected because it blocks multiple surfaces.
- Inheritance-heavy bar subclasses were rejected in favor of one composable definition.
- Implementing widget content in Phase 1 was rejected as phase creep.
