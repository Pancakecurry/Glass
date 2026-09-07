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

Phase 0A establishes the requirement only. It does not add an accessibility framework or claim runtime conformance.
