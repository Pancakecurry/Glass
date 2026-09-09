# Privacy and security

## Default posture

- The product operates locally first.
- No account is required.
- No telemetry is sent by default.
- No analytics SDK is required.
- Settings, layouts, presets, and widget configuration are stored locally.
- The application should not need a backend to provide its core experience.
- Network access is used only by features that genuinely require it.
- Future online integrations must be explicit, understandable, and independently disableable.

## Code and extension trust

- V1 contains trusted built-in widget code only.
- No arbitrary JavaScript widgets.
- No third-party executable plugins.
- No arbitrary DLL or native plugin execution.
- Do not turn widget metadata into a hidden code-loading mechanism.
- Keep optional network-dependent widgets isolated from local shell behavior.

## Secrets and persistence

- Never commit credentials, tokens, certificates, private keys, or machine-specific settings.
- Keep secrets out of source control and out of client-visible configuration.
- Minimize the data retained locally to what the user asked the product to remember.
- Plan schema versioning, atomic writes, corruption handling, and migration before state becomes stable.
- Make local state inspectable and removable without an account.

## Privilege and shell boundaries

- Minimize privileged operations.
- Do not require administrator rights without a proven necessity.
- Do not patch Explorer.
- Do not modify undocumented shell internals in initial releases.
- Prefer documented Win32, WinRT, Windows App SDK, and AppBar mechanisms when later work requires OS integration.

Phase 0B proved local access to media sessions already exposed by Windows. That proof remains in the platform project for future Phase 2 use but is disconnected from Phase 1 startup and shell ownership. Any future packaged capability declaration must be added deliberately with the production media feature. This access is not telemetry, sends no data, and requires no Glass account.

Phase 1 introduces no network, account, telemetry, authentication, arbitrary plugin execution, or privileged behavior. Optional diagnostics remain bounded local text files.

## Phase 2 data boundaries

- Quick Notes text is stored only in its versioned per-instance local state file and is never written to diagnostics.
- Clipboard contents are read from Windows only while the widget is used; Glass never persists or logs clipboard contents.
- Weather is disabled until the user supplies coordinates or explicitly grants one-shot location access. Requests go only to MET Norway over HTTPS, include a truthful product user agent, and use a bounded removable cache.
- Application pins store stable application identity, not process IDs or window handles.
- No widget uses arbitrary JavaScript, DLL loading, reflection discovery, accounts, telemetry, or cloud state.

## Phase 3 presentation boundaries

- Appearance, behavior, sparse surface overrides, and custom presets are versioned in local `settings.json`.
- Edit Mode changes only local layout/settings state and never exposes surface contents externally.
- Clipboard history is read from the Windows API on explicit widget interaction and is never copied into Glass state.
- Weather location is either manually entered or requested once after the user presses Use device location; there is no automatic or background location request.
- Notes and clipboard values are excluded from diagnostics and telemetry remains absent.
- Materials, icons, and motion require no network access.

## Phase 4 release and recovery boundaries

- Onboarding requires no account and requests no weather/location permission.
- Start with Windows uses the packaged user startup-task API and no registry,
  Startup-folder, scheduled-task, elevation, or administrator workaround.
- Session-health data contains only lifecycle timestamps, a failure count, and a
  local recovery category.
- Copied/exported diagnostics exclude note text, clipboard data, weather
  coordinates, and arbitrary user paths; no upload path exists.
- Configuration reset creates a local backup and preserves personal widget state
  by default.
- Package capabilities remain limited to internet client for optional weather,
  explicit one-shot location, and global media control.
- Signing certificates, passwords, private keys, and release-host credentials
  are CI secrets/external inputs and must never enter Git.
