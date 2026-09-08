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
