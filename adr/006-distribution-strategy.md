# ADR 006: Future packaged single-install distribution

## Context

The eventual product should have a straightforward single-installation experience across supported Windows architectures. Distribution must preserve privacy, predictable updates, and a clean uninstall path without forcing release packaging into the foundation phase.

## Decision

Treat MSIX or MSIXBundle as the leading future distribution direction. Defer the final packaging model, signing, update channel, runtime deployment mode, architecture bundles, and store or direct-distribution choice until a Windows deployment spike can test them. Phase 0A uses an unpackaged WinUI bootstrap and does not produce release packages.

## Consequences

- Packaging decisions remain visible without creating untested release artifacts.
- A later phase must validate installation, upgrade, rollback, uninstall, runtime dependencies, architecture selection, and permissions on real Windows systems.
- The app must avoid assumptions that only work when installed through one distribution channel.
- Signing and update policies will need explicit security and privacy decisions before release.

## Alternatives considered

- Committing to Microsoft Store distribution immediately was rejected because the product and release requirements are not yet validated.
- A portable ZIP-only model was rejected as the sole direction because it does not by itself provide the desired single-installation experience.
- Building release packaging in Phase 0A was rejected because macOS cannot validate Windows installer behavior and would encourage premature assumptions.
