# ADR 013: Weather provider and privacy

## Context

Weather requires network access and location data while Glass must remain offline-first.

## Decision

Use an isolated provider abstraction with MET Norway Locationforecast 2.0 compact as the initial implementation. Require explicit coordinates or optional one-shot location consent; never query location at startup. Use HTTPS, truthful identification, conditional caching, bounded removable state, and attribution.

## Consequences

Weather can fail or remain disabled without affecting Glass. Cached data may be shown as stale offline. No API secret or account is required.

## Alternatives considered

Background geolocation, undocumented endpoints, silent network requests, and making weather a shell dependency were rejected.
