# Phase 0B Windows technical spike

## Purpose

Phase 0B establishes implementation-level evidence for the Windows primitives that carry the most architectural risk before product shell, widget, settings, or visual-system work begins. Its visible surfaces are deliberately utilitarian development controls, not product design.

## Hypotheses

- Frameless WinUI surface.
- Desktop Acrylic.
- Composition animation.
- Multi-monitor placement.
- AppBar docking.
- Global media control.

## Implementation

### Frameless WinUI surface

ProbeBarWindow is a second native WinUI Window. WinUiWindowHandle obtains its documented HWND, WindowId, and AppWindow. The window extends content into a XAML drag region, while OverlappedPresenter.SetBorderAndTitleBar removes normal chrome. WindowPositioner performs native pixel positioning and visibility changes.

### Desktop Acrylic

The probe assigns DesktopAcrylicBackdrop to Window.SystemBackdrop. If constructing the backdrop fails, the root receives a solid brush and the failure appears in development status. Windows can also select its own solid fallback when transparency, hardware, accessibility, power, or remote-session conditions require it.

### Composition animation

Glass.Rendering.Composition.CompositionAnimator obtains an element's underlying Composition Visual, sets its center point, and runs a SpringVector3NaturalMotionAnimation against Scale. Pointer entry/exit and a control-panel button trigger the proof; no storyboard, layout animation, timer, or permanent animation clock is used.

### Multi-monitor placement

WindowsDisplayService uses DisplayArea.FindAll, identifies the primary display, exposes physical-pixel bounds and screen-relative work areas, finds the display containing a WindowId, and refreshes from DisplayAreaWatcher events. Coordinates remain signed, so monitors left of or above the primary display are represented without normalization.

### AppBar docking

AppBarController uses the documented SHAppBarMessage sequence: ABM_NEW, ABM_QUERYPOS, edge-thickness correction, ABM_SETPOS, and ABM_REMOVE. A disposable SetWindowSubclass hook receives the registered callback and reapplies placement for ABN_POSCHANGED. ProbeSurfaceCoordinator keeps floating and docked state explicit and converts logical thickness through the window DPI before reservation.

### Global media control

SystemMediaSessionService requests GlobalSystemMediaTransportControlsSessionManager once, observes the current session, subscribes to media-property and playback changes, publishes a UI-independent snapshot, and exposes previous, play/pause, and next operations. Session replacement and service disposal unsubscribe all handlers. The package manifest records only the required uap7 capability named globalMediaControl.

The Phase 0B executable remains unpackaged under ADR 006; Package.appxmanifest records the capability for the future packaged route without selecting signing or release distribution. The desktop runtime path handles denied or unavailable media access without taking down the rest of the spike.

## Architectural assets produced

- WinUiWindowHandle for HWND, WindowId, and AppWindow acquisition.
- WindowPositioner for centralized DIP conversion and native screen placement.
- NativeWindowMessageHook for composable, disposable Win32 message interception.
- WindowsDisplayService and DisplayInfo for event-driven display topology.
- AppBarController for supported shell registration and cleanup.
- SystemMediaSessionService and MediaSessionSnapshot for event-driven local media control.
- CompositionAnimator as a deliberately small compositor-path proof.
- SurfacePlacement, DockEdge, and ProbeSurfaceCoordinator for coherent floating/docked state.

## Runtime validation status

Not manually validated on Windows yet

The macOS development environment cannot validate WinUI rendering, HWND lifecycle, shell callbacks, media permissions, AppBar work-area behavior, monitor topology, or mixed-DPI transitions. The Windows workflow provides compile-level feedback only.

## Deferred validation checklist

### Surface

- [ ] Launches.
- [ ] Frameless.
- [ ] Draggable.
- [ ] Closes correctly.

### Acrylic

- [ ] Desktop visible through backdrop.
- [ ] Fallback works when transparency or effects are disabled.

### Composition

- [ ] Hover and trigger animation run smoothly.
- [ ] No obvious layout jumping.

### Displays

- [ ] Display list is correct.
- [ ] Negative-coordinate monitors are represented correctly.
- [ ] Movement across monitors works.
- [ ] Mixed-DPI setup behaves correctly.

### AppBar

- [ ] All four edges work.
- [ ] Work area is reserved.
- [ ] Undock restores work area.
- [ ] Closing the application restores work area.
- [ ] Explorer restart behavior is safe.
- [ ] Monitor disconnect behavior is safe.

### Media

- [ ] Spotify session.
- [ ] Browser media session.
- [ ] VLC or another media application, if available.
- [ ] No-session state.
- [ ] Play/pause.
- [ ] Previous.
- [ ] Next.
- [ ] Song changes update through events.
