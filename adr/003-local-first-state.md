# ADR 003: Local-first state and no required backend

## Context

The core shell and widget experience should work offline, without an account, and without sending layout or usage data to a service. Settings and layouts need to be available with low latency and remain understandable to the user.

## Decision

Keep product state local-first. Core exposes only a minimal local-state persistence contract. The first implementation will use simple local serialization through System.Text.Json. Network access is optional and limited to features that genuinely require it, such as weather. No backend, authentication, telemetry, analytics SDK, database, or cloud sync is part of the initial architecture.

## Consequences

- Offline operation is a baseline rather than an exceptional mode.
- Local state needs explicit schema versioning, atomic writes, migration, corruption handling, and user-visible recovery as it matures.
- Optional online widgets must be isolated from local shell responsiveness and availability.
- Users avoid an account dependency and default data collection.

## Alternatives considered

- A cloud-first state service was rejected because it conflicts with offline-first operation, privacy, and low-latency local behavior.
- A database was rejected for Phase 0A because the initial state is small and a database would add operational and migration cost without a demonstrated need.
- Automatic telemetry was rejected; any future diagnostics must be explicit, minimized, and separately decided.
