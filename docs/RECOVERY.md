# Recovery

## Session health

Glass stores a small local `session-health.json` record. Startup marks a session
active before restoring shell state. Clean shutdown clears the active marker and
failure count. If the marker remains active across three subsequent startups,
Glass enters Safe Mode on the next launch. `--safe-mode` enters it immediately.

This mechanism is not crash reporting and sends no data anywhere.

## Safe Mode

Safe Mode:

- opens Control Center;
- forces Solid rendering and reduced decorative behavior;
- disables bar auto-hide;
- does not restore standalone widget windows;
- omits widgets from bar presentation while preserving their definitions/state;
- provides normal restart, configuration reset, diagnostics, and exit.

Trying normal startup does not delete data. Configuration reset first moves
`settings.json` and `shell-layout.json` into a timestamped backup directory. It
does not delete Quick Notes or other personal widget state. Any future reset that
includes personal widget contents must use a separate explicit confirmation.

## Invalid data

Versioned settings and layouts are loaded through atomic stores. Unsupported or
malformed documents are moved to the backup directory with a redacted diagnostic
reason, then safe defaults are used. Save operations replace complete documents
atomically. Caches are disposable and are not recovery-critical.

## Operating-system recovery

- Explorer recreation: re-register AppBars, reapply surfaces, refresh running
  windows and the application catalogue.
- Suspend/lock: remove provider visibility demand and stop avoidable sampling.
- Resume/unlock: refresh displays, reconcile placements, restore surfaces, and
  refresh event-backed providers.
- Display removal/change: resolve persisted display identities to a remaining
  display, clamp logical bounds, and persist the recovered target.
- Audio/media change: endpoint and session events rebind current state without a
  permanent poll.

Local diagnostics record the latest recovery category but exclude personal
content and precise user locations.
