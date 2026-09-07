# Design system

## Status

Phase 0A defines vocabulary and constraints, not finished visual design. The source of truth is design/tokens.json. Its values are restrained provisional defaults intended to give later work a shared shape while leaving extraordinary user customization possible.

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

## Material principles

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

There is no Liquid Glass implementation in Phase 0A. Do not infer a final aesthetic from the development placeholder window.
