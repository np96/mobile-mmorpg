# Leveling Ability Icons V2 provenance

- Authored: 2026-08-31
- Tool: OpenAI built-in `image_gen`
- Mode: one generated raster master per distinct ability; `SteadyShot` received one
  additional image-edit pass to replace a rejected checkerboard background.
- External references: none. The rejected `SteadyShot` draft used by the edit was
  itself generated in this session.
- Intended use: first-party prototype UI art for this project.
- Master format: 1254 x 1254 opaque RGB PNG.
- Runtime source: 256 x 256 opaque RGB PNG, Lanczos downsampled from the matching
  master.
- Runtime derivative: deterministic RGBA extraction by
  `Tools/Art/extract_ability_icon_alpha.py`.

The shared final prompt direction was:

> Premium serious dark-fantasy action RPG ability icon painting with mature,
> hand-painted realism, weathered materials, dramatic upper-left rim light,
> deep localized shadows, restrained class-colored energy, and a bold
> high-contrast silhouette readable at 48-96 px. Square 1:1. One centered compact
> motif occupying roughly 70-78% of the canvas with at least 10% clear matte
> padding. Use one fully opaque, uniform near-black neutral matte (#080A0D) for
> deterministic matte-to-alpha extraction. No transparency simulation,
> checkerboard, tiles, gradient plate, vignette, circular crop, frame, medallion,
> border, button, text, letters, numbers, logos, readable runes, watermark, flat
> vector art, cartoon rendering, excessive bloom, or tiny decorative clutter.

Ability-specific final prompt subjects:

- `SteadyShot`: rugged recurved bow at full tension with one steel-tipped arrow
  launching diagonally up-right, two restrained amber motion streaks, release
  sparks, and localized smoke. The accepted master is the corrected second pass.
- `ArcaneShot`: dark-steel barbed arrowhead flying through a compact cold-cyan
  arcane fracture and geometric shock ring.
- `SerpentSting`: realistic emerald serpent coiled around a blackened barbed
  arrowhead, fangs striking with restrained venom vapor.
- `PetAttack`: realistic dark-furred hunting-beast forepaw and four steel-bright
  claws raking through an amber target spark.
- `Disengage`: two rugged hunter boots in a forceful backward leap with a teal
  wind crescent, dust, and a snapped arrow on the ground.
- `FreezingArrow`: blackened hunting arrow impacting fractured ground, its
  arrowhead encased in frost crystal with compact shards and freezing mist.
- `RapidFire`: exactly three distinct hunting arrows in a tight parallel volley
  with controlled amber speed trails.
- `ShadowWordPain`: jagged obsidian thorn piercing a constricted violet spectral
  heart-wisp, bound by dark psychic pressure.
- `MindFlay`: silver-violet psychic eye held by fractured obsidian with exactly
  three sustained violet mind tendrils.
- `MindBlast`: weathered iron death mask splitting outward from an instantaneous
  violet psychic fracture.
- `DevouringPlague`: cracked iron plague reliquary consumed by violet-black shadow,
  restrained disease vapor, and three inward-circling plague wisps.
- `Dispersion`: hollow hooded priest silhouette dissolving into violet-black glass
  shards and soft shadow motes.
- `Stealth`: fully concealed, faceless leather-hooded rogue crossing two daggers
  inside a restrained ring of violet-black smoke, with no visible eyes or skin.
- `Shadowform`: priest portrait divided vertically between a warm, living holy
  form and a violet-black spectral form dissolving into restrained shadow shards.
- `Smite`: concentrated golden-white holy lance striking cracked dark stone,
  producing one compact radiant impact and restrained sparks.

All prompts explicitly excluded embedded UI chrome because the project supplies
the accepted forged medallion frame at presentation time.

`Stealth`, `Shadowform`, and `Smite` were appended for the level-one fairness
pass. Each used the shared final prompt direction above, the listed subject, and
an existing accepted class icon only as a project-style reference. No external
reference art was supplied.
