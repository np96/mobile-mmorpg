# Priest Arena ability-icon masters

- Authored: 2026-08-31
- Tool: OpenAI built-in `image_gen`
- Mode: one initial generation call per distinct ability, followed only by
  single-variable image-edit retries for composition scale and padding.
- Style reference: the accepted local `AbilityIconsLevelingV2` Priest masters were
  visually inspected before generation but were not supplied to the image tool.
- Accepted format: 1254 x 1254, opaque RGB PNG.
- Intended downstream treatment: deterministic matte-to-alpha extraction. These
  files are preserved masters and are not runtime Unity assets.

## Exact shared initial prompt direction

Each initial call used the following literal shared requirements with its ability
ID and the ability-specific subject/material/palette details below:

```text
Use case: stylized-concept
Asset type: original game UI ability-icon master for the Priest ability "<AbilityId>"
Primary request: Create one premium serious dark-fantasy action-RPG ability icon painting with mature hand-painted realism and a bold, instantly readable phone-scale silhouette.
Scene/backdrop: one fully opaque, absolutely uniform near-black neutral matte, exact visual color #080A0D, extending cleanly to every edge and corner.
Style/medium: premium serious dark-fantasy realism; painterly but physically convincing <ability-specific materials>.
Composition/framing: square 1:1; one centered compact motif occupying roughly 70-76% of the canvas; at least 12% clean matte padding on every side; readable at 48-96 px; all painted parts fully inside the safe area.
Lighting/mood: dramatic upper-left class-colored rim light, deep localized shadows, and restrained ability energy.
Color palette: <ability-specific palette>.
Constraints: fully opaque RGB-looking artwork on the uniform matte; original composition; no extra objects; no background scene.
Avoid: transparency, checkerboard, simulated transparency, gradient plate, vignette, circular crop, frame, circle, medallion, border, button, badge, text, letters, numbers, readable runes, logos, watermark, person, portrait, face, cartoon rendering, flat vector art, excessive bloom, tiny decorative clutter, copied franchise icon composition.
```

The exact subject and ability-specific clauses appended to that shared direction
were as follows.

## Exact ability-specific initial prompt blocks

### VampiricTouch

```text
Subject: one elegant tarnished-silver skeletal ritual hand touching a single cracked violet heart-shaped reliquary; a taut violet-black energy siphon visibly travels from the reliquary into the fingertips, with only three restrained floating splinters. No person or body.
Materials: aged silver, blackened bone, fractured amethyst glass, and smoky psychic energy.
Palette: violet-black psychic energy, tarnished silver, charcoal, restrained pale lavender highlights.
Composition note: one centered compact diagonal motif; all fingers, energy, and fragments fully inside the safe area.
Exact object constraint: one hand, one heart reliquary, one visible siphon.
Additional exclusions: torso.
```

### Silence

```text
Subject: one heavy blackened-silver temple bell in three-quarter view, its clapper immobilized and its bell mouth tightly crossed by exactly three broad violet-black shadow bands pulled taut like bindings; a few compressed violet motes die against the sealed bell. The bell is intact, unmistakably unable to ring.
Materials: tarnished silver, black iron, worn leather-like shadow bindings, and restrained psychic vapor.
Palette: violet-black psychic energy, tarnished silver, charcoal, restrained pale lavender highlights.
Composition note: one centered compact upright motif; all bands and motes fully inside the safe area.
Exact object constraint: exactly one bell and three dominant binding bands; no letters or symbols engraved on the bell.
Additional exclusions: mouth.
```

### PsychicHorror

```text
Subject: one empty cracked black-iron ritual fear mask, angular and inhuman rather than a portrait, split vertically as exactly five long violet psychic spikes drive inward from different directions toward its hollow center; the metal visibly buckles under inward pressure. No head, eyes, skin, or wearer.
Materials: battered iron, tarnished silver edges, fracture dust, and hard violet psychic energy.
Palette: violet-black psychic energy, tarnished silver, charcoal, restrained pale lavender highlights.
Composition note: one centered compact motif; all spikes and fragments fully inside the safe area.
Exact object constraint: one empty mask and exactly five dominant inward spikes; visually distinct from an outward explosion.
Additional exclusions: skull and eyeballs.
```

### ManaBurn

```text
Subject: one large faceted cobalt-blue mana crystal held diagonally, visibly being consumed from its lower half by a single hungry violet-black flame; the blue core collapses into a dark charred fracture while three small blue sparks are pulled down into the flame. The action must clearly read as magical resource being burned away, not a normal fire attack.
Materials: translucent crystal, blackened fractures, smoky violet fire, and tarnished silver mounting claws.
Palette: saturated cobalt mana blue, violet-black psychic flame, tarnished silver, charcoal.
Composition note: one centered compact diagonal motif; all flame tips and sparks fully inside the safe area.
Exact object constraint: exactly one mana crystal and one dominant consuming flame.
Additional exclusions: ordinary orange fire.
```

### MassDispel

```text
Subject: one substantial tarnished-silver swinging censer at lower center releasing a broad fan-shaped white-gold cleansing wave upward and outward; the wave simultaneously shatters exactly five small curse shards—two violet, one sickly green, one blood red, one icy blue—into restrained dust. The wave is a directional fan, not a circular halo or border.
Materials: engraved worn silver without readable symbols, pale incense vapor, fractured glass curse shards, and restrained holy light.
Palette: tarnished silver, bone-white incense, restrained holy gold, tiny violet/green/red/blue curse accents, charcoal.
Composition note: one centered compact triangular motif; censer, fan, and all shards fully inside the safe area.
Exact object constraint: one censer, one broad fan wave, exactly five small curse shards.
Additional exclusions: circular shockwave and halo.
```

### Fade

```text
Subject: one empty weathered priest hood with tarnished-silver edge trim, turned in slight three-quarter view; its lower half gently dissolves into a narrow trailing veil of violet-black smoke and a few soft motes, as if threat and presence are slipping away. The hood opening is pure featureless shadow with no face, eyes, or wearer.
Materials: heavy worn cloth, frayed silver trim, soft psychic smoke, and sparse fading motes.
Palette: violet-black shadow, tarnished silver, desaturated charcoal cloth, restrained pale lavender.
Composition note: one centered compact slightly diagonal motif; all cloth, smoke, and motes fully inside the safe area.
Exact object constraint: exactly one empty hood and one soft dissolving smoke trail; no shards, shield, defensive shell, extra objects; visually distinct from Dispersion.
Additional exclusions: skull, eyes, and hands.
```

### PrayerOfMending

```text
Subject: exactly three small distinct tarnished-silver shield tokens arranged as a compact open triangle, linked by one strand of weathered holy-gold prayer beads that visibly passes from shield to shield; a warm healing spark is shown transferring along the strand toward the next token. Each shield is plain and materially distinct, with no writing or emblem.
Materials: aged silver, worn gold beads, dark cord, and restrained warm healing light.
Palette: tarnished silver, bone/parchment neutrals, restrained holy gold, tiny warm-white healing core, charcoal.
Composition note: one centered compact triangular motif; all tokens, beads, cord, and sparks fully inside the safe area; open triangle, not a circular border.
Exact object constraint: exactly three shield tokens and one connecting prayer-bead strand.
Additional exclusions: closed circular loop, halo, shield emblems, and hands.
```

### InnerFocus

```text
Subject: one abstract tarnished-silver eye-shaped focusing instrument with a small faceted violet crystal as its pupil, held tightly between exactly four separate inward-curving holy-gold aperture blades; the four blades visibly converge toward the crystal but never join into a complete ring. This is a physical magical focus object, not a biological eye and not a UI frame.
Materials: hammered silver, worn gold, violet crystal, and restrained concentrated light.
Palette: tarnished silver, restrained holy gold, deep violet focal crystal, charcoal.
Composition note: one centered compact horizontal eye motif; all four aperture blades and light accents fully inside the safe area.
Exact object constraint: one abstract silver eye instrument, one violet crystal, exactly four disconnected gold blades; no complete ring and no readable symbols.
Additional exclusions: biological eye, complete circle, halo, and outer frame.
```

### FearWard

```text
Subject: one aged tarnished-silver ward shield in strong three-quarter view, bearing a simple raised abstract closed-eye shape made only from two plain curved metal ridges; exactly three violet-black fear tendrils strike the shield from the right and visibly bend, split, and recoil away from its protective face. A restrained holy-gold seam glows where the tendrils are intercepted.
Materials: battered silver, worn leather rim, plain raised metal relief, smoky psychic tendrils, and restrained ward light.
Palette: tarnished silver, bone/parchment neutrals, restrained holy gold, violet-black psychic energy, charcoal.
Composition note: one centered compact diagonal motif; shield and all recoiling tendrils fully inside the safe area.
Exact object constraint: exactly one shield, one abstract closed-eye relief, and three dominant fear tendrils; no readable symbols.
Additional exclusions: biological eye and skull.
```

## Retry and acceptance record

Every result was inspected at full size. All retries used built-in `image_gen` in
image-edit mode, repeated the original material/palette/matte invariants, and asked
for a scale/placement change only. No retry changed an accepted motif.

| Ability | Exact retry sequence and accepted result |
|---|---|
| VampiricTouch | First pass had the wrist cropped. Retry: uniformly scale the complete hand-plus-heart motif down 18% and move slightly down/right; require 12% clean matte padding. Accepted retry 1. |
| Silence | First pass was too large. Retry: uniformly scale the complete bound-bell motif down 15%, centered; require 12% padding. Accepted retry 1. |
| PsychicHorror | First pass spike tips approached three edges. Retry: uniformly scale the mask-and-five-spikes motif down 21%, centered; preserve exactly five inward spikes and require 12% padding. Accepted retry 1. |
| ManaBurn | Retry 1: scale the crystal-and-flame motif down 18% for 12% padding. It became smaller than the target. Retry 2: enlarge that edge-safe result 18%, centered, targeting about 70% occupancy while retaining 12% padding. Accepted retry 2. |
| MassDispel | First pass wave/debris approached the edge. Retry: uniformly scale the complete censer, fan, five shards, chain, and debris down 18%, centered; require 12% padding. Accepted retry 1. |
| Fade | Retry 1: scale down 16% and move slightly downward for 12% padding. Retry 2: enlarge retry 1 by 17% to restore occupancy. Retry 3: reduce retry 2 by 8% and move down; this over-shrank the motif. Final retry: return to retry 1 and enlarge it 8%, centered, targeting about 70% occupancy with 12% padding. Accepted final retry. |
| PrayerOfMending | Retry 1: scale the complete three-shield motif down 9%, centered, for at least 10% padding. Retry 2: enlarge retry 1 by 8%, centered, targeting about 72% occupancy while retaining 12% padding. Accepted retry 2. |
| InnerFocus | Retry 1: scale the complete eye-and-four-blades motif down 21%, centered, for 12% side padding. Retry 2: enlarge retry 1 by 10% to target 72% width. Retry 3: reduce retry 2 by exactly 5% to restore at least 12% side padding. Accepted retry 3; it is intentionally the safer compact variant because the pointed silhouette otherwise approached the side edges. |
| FearWard | Retry 1: scale the shield-and-three-tendrils motif down 18%, centered, for 12% padding. Retry 2: enlarge retry 1 by 20% to restore roughly 72% occupancy. Retry 3: reduce retry 2 by exactly 5% to retain 12% around tendril tips and shield corners. Accepted retry 3. |

Rejected variants remain only in the image tool's generated-output cache and were
not copied into the project lineage.

## Accepted master hashes

```text
Fade             3102a5f2bdd4a4c5b98449848e1ead529308cac3de6352f00d89e580893f10cf
FearWard         521026f96850c72ef35285e6a72437041d1aa2707200ae1d6aef659d1696b604
InnerFocus       3f48892cc2d34c63aa6494f27562f778bf0f27f313e4478c8e7525ace664d288
ManaBurn         a64b150e13a964c6906d5a648099908809f9ab81082d55e02b1ebe476e368e3c
MassDispel       7edae5400c4de7f1b57a40dfdb99cca8d2389dfe7d8e6ae2a679262168b5967d
PrayerOfMending 33b59f55941af4de0fd96408888a036fa81da109f4493ab7ece6bb0ec63a6835
PsychicHorror    6f14b4878ecf9cb653d1f900a49e37d51ebf9b52f75f2e342ab76cf58cda299e
Silence          903ebcff3777991d486fe35c9202da701b72a5ccf43a936c196411e6e5bf913c
VampiricTouch    32653af1e1631c8ba123ba3516b53db6d548a2d11f8339e9ae9ed5d99269c858
```
