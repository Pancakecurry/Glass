# Accessibility

Accessibility is a product requirement and must be considered in every surface, widget, material, and interaction decision.

## Requirements

- Provide complete keyboard navigation.
- Preserve logical and visible focus states.
- Consider Windows high-contrast modes and user-selected contrast settings.
- Honor reduced-motion preferences.
- Support scalable text and DPI-aware layout.
- Keep interactive hit targets usable.
- Provide screen-reader semantics where the platform exposes them.
- Do not rely on color alone to communicate state, hierarchy, or errors.
- Do not make critical functionality available only through drag.
- Ensure edit, move, resize, snap, and hide behaviors have non-pointer paths.
- Keep materials and translucency subordinate to text and control legibility.

## Future validation

Later Windows work should validate keyboard traversal, focus visibility, automation names and roles, high contrast, text scaling, pointer and touch targets, reduced motion, multi-monitor DPI, and recovery from interrupted drag or resize operations.

Phase 3 supplies keyboard-reachable native controls, visible theme focus treatment, accessible application names that include pinned/running/active/multi-window state, non-color running indicators, explicit move/attach actions, reduced-motion mapping, and a High Contrast solid material fallback. Runtime conformance, screen-reader traversal, text-scaling limits, and mixed-DPI interaction still require manual Windows validation.

## Phase 4 baseline

High Contrast now overrides every rendering-quality preference with a Solid
policy. Windows animation and accessibility changes update live material/motion
decisions. Edit Mode exposes keyboard operations in addition to pointer dragging:
arrow keys nudge the selected floating bar/widget, Control uses a coarse step,
Shift plus arrows resizes, and Delete removes where safe. Control Center retains
explicit buttons for moving content between zones/hosts, resizing widgets, and
removing surfaces, so critical editing is not drag-only.

Native controls retain keyboard tab semantics, visible focus resources, and
accessible names. Indicators use geometry and labels as well as color. Phase 5
must manually audit Narrator, keyboard traversal, focus restoration, 200% text
scaling, touch targets, High Contrast themes, and mixed-DPI edit operations.
