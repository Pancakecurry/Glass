# Design system

## Status

Phase 3 establishes the first production visual language. The source of truth is `design/tokens.json`; `Glass.Rendering` and the shared WinUI resource dictionary implement it. Values are curated defaults and semantic constraints, not a restriction on user customization.

## Product character

The product should feel:

- Calm.
- Extremely responsive.
- Tactile.
- Spatially coherent.
- Elegant.
- Modern.
- Premium.
- Minimal without becoming sparse.
- Powerful without looking technical.

The visual system should avoid:

- Generic dashboard aesthetics.
- Giant cards everywhere.
- Excessive gradients.
- Neon gamer UI.
- Gratuitous blur.
- Fake Apple cloning.
- Excessive borders.
- Excessive animation.
- Crowded settings pages.
- Arbitrary visual inconsistency.

## Material model

Glass is a material system, not simply blur plus transparency. Future materials may combine:

- Translucency.
- Luminosity.
- Tint.
- Subtle edge highlights.
- Controlled borders.
- Environmental response.
- Shallow depth.
- Restrained shadows.
- Composited motion.

Material parameters are user-customizable inputs with safe defaults and capability-adaptive fallbacks. A material is successful only when content remains legible, hierarchy remains clear, and the experience still feels coherent when expensive effects are reduced or removed.

Top-level Glass surfaces are composed in this order: one native backdrop, controlled tint/luminosity, a translucent overlay, restrained edge light, semantic border, optional compositor shadow, then content. Clear, Frost, Smoke, Crystal, and Solid profiles share this model. Bars and widgets normally use Desktop Acrylic; Control Center may use Mica. High Contrast and restricted environments use the Solid path. Nested blur is forbidden.

Appearance resolves through `global -> surface override -> widget-specific override`. Overrides are sparse and keyed by stable bar or widget IDs. Reset to Global removes the override instead of copying a full theme.

## Token rules

- Feature code consumes semantic roles rather than hard-coded theme colors.
- Structural tokens describe relationships such as spacing, target sizes, z-layers, and motion semantics.
- Material defaults expose safe ranges and a restricted-quality fallback.
- Future theme values are semantic and provisional until a later design phase approves them.
- User-customizable material settings must not silently bypass contrast, hit-target, or reduced-motion requirements.
- Typography must scale with user settings and DPI.
- A visual hierarchy should not depend on color alone.

## Composition rules

Surfaces should be composed with intentional depth and readable grouping. Controls should be discoverable without making every region a card. Settings should be organized around user goals and visible consequences rather than a wall of technical switches. Repeated patterns should use the same semantic tokens and interaction language.

Shared styles define normal, hover, pressed, focus, selected, and disabled states. Segoe UI Variable is preferred with Windows fallbacks. Fluent/system glyphs identify Glass actions; application icons always remain the applications' own Shell icons. Normal product startup uses Control Center, bars, and widget surfaces; local diagnostics live under Advanced.

## Runtime quality adaptation

Phase 4 applies the same semantic visual language through Full, Balanced,
Reduced, and Solid renderer policies. These are capability/efficiency tiers, not
separate themes. Reduced removes costly shadow and magnification treatment;
Solid removes translucent backdrop work. High Contrast always uses the safe
Solid path. User material, theme, and hierarchy semantics remain recognizable
at every tier.
