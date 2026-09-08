# ADR 012: Widget state persistence

## Context

Mutable notes and timers evolve independently from shell placement and should be removable per instance.

## Decision

Keep host and configuration references in shell schema v2 and mutable widget state in atomic versioned `state/widget-<guid>.json` documents. Delete state on permanent instance removal.

## Consequences

Shell migrations remain bounded. Individual state can be recovered or removed without rewriting the whole layout. Sensitive clipboard content is excluded entirely.

## Alternatives considered

One monolithic settings file and a database were rejected. Storing mutable widget state in shell layout was rejected due to write amplification and coupling.
