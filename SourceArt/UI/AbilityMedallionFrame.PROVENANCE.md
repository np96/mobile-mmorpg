# Ability medallion frame source

- Source file: `AbilityMedallionFrame_Source.png`
- SHA-256: `13666cff685c69b44b8655ed7e98d4d3784b8fcc628fc486f677f5dd6a8b3d29`
- Created: 2026-08-31 with Codex built-in image generation (`image_gen`)
- Intended use: first-party UI frame source for the Session RPG prototype
- Runtime derivative: `Assets/SessionRpg/Art/UI/Hud/AbilityMedallionFrame.png`
- Derivation: `Tools/Art/extract_hud_medallion_alpha.py` removes the uniform
  black matte deterministically and verifies a clear center, soft edge, opaque
  metal core, and transparent outer gutter.

## Prompt

> Use case: stylized-concept. Asset type: production mobile action-RPG
> ability-button frame sprite source for deterministic alpha extraction. Create
> one centered, front-facing circular dark-fantasy medallion frame from layered
> blackened forged iron and restrained aged brass, with a clean silhouette,
> beveled rim, narrow brass inlay, subtle hammered texture, and eight low-profile
> rivets. Use a perfectly uniform pure-black matte in the center and outside the
> ring, exact radial symmetry, generous padding, controlled top-left rim light,
> and serious grounded values legible at 128–160 px. No icon, text, runes,
> gemstone, glow, shadow, watermark, crop, spikes, skulls, wings, checkerboard,
> baroque clutter, flat-vector treatment, or cartoon styling.

The built-in generator returned an opaque RGB source, so the tracked transparent
runtime artifact is a reproducible derivative rather than a hand-painted alpha.
