# ADR 010: Application identity and tracking

## Context

Taskbar pins must survive process restarts while running windows change constantly.

## Decision

Discover applications through the Windows Shell Applications namespace. Persist AUMID, Shell parsing identity, or canonical executable path in that order. Track windows with `EnumWindows` plus WinEvent hooks and pure filtering and grouping rules. Never persist PID or HWND.

## Consequences

Packaged and desktop applications share one projection. Icon and activation adapters remain Windows-specific. Runtime behavior needs Windows validation.

## Alternatives considered

PackageManager-only discovery, recursive Program Files scanning, registry inventory, and polling were rejected as incomplete, invasive, or wasteful.
