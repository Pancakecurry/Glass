# ADR 011: Widget host and lifecycle

## Context

Widgets may appear in bars or standalone windows while expensive providers must stop when invisible.

## Decision

Use an explicit trusted registry and idempotent Created, Mounted, Visible, Suspended, and Disposed lifecycle. Each instance has exactly one host. Shared providers start for the first visible consumer and stop after the last. V1 loads no third-party code.

## Consequences

Host transfer preserves identity and state. Visibility, including bar auto-hide, is a scheduling signal. App composes WinUI hosts without making the runtime UI-aware.

## Alternatives considered

Reflection discovery, arbitrary plugins, separate bar and desktop widget engines, and always-running providers were rejected.
