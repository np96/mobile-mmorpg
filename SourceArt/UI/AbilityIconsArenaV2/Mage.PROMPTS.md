# Mage Arena ability-icon masters

- Authored: 2026-08-31
- Tool: OpenAI built-in `image_gen`
- Mode: one initial generation call per distinct ability; no sprite sheet, CLI,
  external art, or franchise reference was used.
- Style reference: the accepted local `AbilityIconsLevelingV2` masters were inspected
  before generation but were not passed to the image tool.
- Accepted format: 1254 x 1254, opaque RGB PNG.
- Intended downstream treatment: deterministic matte-to-alpha extraction. These
  files are preserved masters and are not runtime Unity assets.

## Exact shared prompt direction

Every initial call used these literal shared requirements, with the ability ID,
subject, material sentence, palette, and ability-specific exclusions supplied by
the blocks below:

```text
Use case: stylized-concept
Asset type: original game UI ability-icon master for the Mage ability "<AbilityId>"
Primary request: Create one premium serious dark-fantasy action-RPG ability icon painting with mature hand-painted realism and a bold, instantly readable phone-scale silhouette.
Scene/backdrop: one fully opaque, absolutely uniform near-black neutral matte, exact visual color #080A0D, extending cleanly to every edge and corner.
Style/medium: premium serious dark-fantasy realism; painterly but physically convincing <ability-specific materials>.
Composition/framing: square 1:1; one centered compact motif occupying roughly 70-78% of the canvas; at least 10% clean matte padding on every side; readable at 48-96 px.
Lighting/mood: dramatic upper-left rim light, deep localized shadows, and restrained class-colored energy.
Constraints: fully opaque RGB-looking artwork on the uniform matte; original composition; no extra objects; no background scene.
Avoid: transparency, checkerboard, simulated transparency, gradient plate, vignette, circular crop, frame, circle, medallion, border, button, badge, text, letters, numbers, readable runes, logos, watermark, copied franchise icon composition, flat vector art, cartoon rendering, excessive bloom, tiny decorative clutter.
```

For the armor/reset batch, the literal composition line tightened the requested
range to `roughly 70-76%` and made `at least 12% clean matte padding` a hard
invariant. No accepted image contains baked button chrome.

## Exact ability-specific subject prompts

### Scorch

```text
Subject: a single charred black-iron spell focus firing one narrow white-hot orange flame lance diagonally up-right through a compact cracked charcoal fragment; restrained crimson ember spray and a short localized smoke wake communicate a fast searing Scorch cast.
Materials: weathered iron, char, ash, flame, and heat.
Palette: fire orange, deep crimson, charcoal, blackened iron.
Composition note: strong asymmetric diagonal silhouette.
```

### Pyroblast

```text
Subject: one massive cracked obsidian meteor sphere with a blinding white-orange molten core, surging diagonally up-right as a heavy compact crimson fire comet; three large black crust plates, a short turbulent flame crown, restrained sparks and ash.
Materials: volcanic stone, molten cracks, flame, soot, and ash.
Palette: fire orange, deep crimson, charcoal, obsidian black.
Composition note: unmistakably heavier and more destructive than a normal fireball.
Additional exclusions: face or skull.
```

### ConeOfCold

```text
Subject: one frost-bound silver spell focus at lower center projecting a broad triangular fan of exactly three dominant jagged ice blades, spreading upward and outward through a compact blast of freezing mist; the wedge shape must clearly read as a directional cone, not a circular nova.
Materials: worn silver, translucent blue ice, frost, and cold vapor.
Palette: frost cyan, deep blue, silver, charcoal.
Composition note: exactly three large shapes with minimal small shards.
Additional exclusions: snowflake emblem.
```

### Spellsteal

```text
Subject: a single hooked weathered silver mage gauntlet pulling one luminous cyan-blue spell wisp out of a cracked violet glass ward shard; the stolen energy forms one taut curved siphon between shard and grasp, with only a few controlled fragments.
Materials: worn silver, leather, fractured enchanted glass, and restrained arcane energy.
Palette: cyan, deep blue, restrained violet, weathered silver, charcoal.
Composition note: clear pull-and-transfer action; one gauntlet, one ward shard, one energy wisp.
Additional exclusions: extra hands.
```

### IceBlock

```text
Subject: one weathered silver mage helm and upper gorget completely sealed upright inside a single massive faceted cyan ice monolith; the hard unbroken crystal enclosure must communicate total frozen immunity, with only three restrained chips suspended nearby.
Materials: worn silver, thick translucent ice, trapped frost, and hard facets.
Palette: frost cyan, deep blue, silver, charcoal.
Composition note: one complete vertical monolith and helm.
Additional exclusions: human face.
```

### ManaShield

```text
Subject: one compact silver-blue crystal core protected by exactly three intersecting angular translucent arcane shield plates, their facets forming a strong defensive triangular shell; restrained cyan energy seams and two small violet sparks.
Materials: worn silver, blue crystal, thick magical glass, and contained arcane energy.
Palette: deep blue, cyan, weathered silver, restrained violet, charcoal.
Composition note: exactly three dominant shield plates around one core; no circular outline.
Additional exclusions: round bubble.
```

### MoltenArmor

```text
Subject: one blackened iron mage cuirass with three broad armor plates split by white-orange molten seams, crowned by a short controlled crimson flame mantle at the shoulders; heavy charred metal, no wearer, no body.
Materials: battered iron, lava cracks, soot, heat discoloration, and compact flame.
Palette: fire orange, deep crimson, charcoal, blackened iron.
Composition note: one empty torso-shaped cuirass with exactly three dominant plate masses.
Additional exclusions: person, face, or skull.
```

### MageArmor

```text
Subject: one empty high-collared mage mantle made from three interlocking weathered silver-blue armor plates, with a single restrained violet crystal clasp and thin cyan energy seams; elegant angular shoulders, no wearer and no body.
Materials: aged silver, dark blue enamel, leather backing, crystal, and contained arcane energy.
Palette: deep blue, cyan, weathered silver, restrained violet, charcoal.
Composition note: one empty mantle with exactly three dominant plate masses.
Additional exclusions: person, face, skull, or shield.
```

### IceArmor

```text
Subject: one empty layered silver cuirass armored with three broad overlapping frost plates and two jagged ice shoulder ridges; heavy hoarfrost coats the edges while cold cyan light glows between the metal layers, no wearer and no body.
Materials: battered silver, blue steel, thick frost, translucent ice, and cold vapor.
Palette: frost cyan, deep blue, silver, charcoal.
Composition note: one empty cuirass with exactly three dominant overlapping armor plates.
Additional exclusions: ice monolith, enclosed helm, person, face, or skull.
```

### WaterElementalFreeze

```text
Subject: one large translucent water-forged elemental fist closing tightly around a compact silver-blue orb at the instant it flash-freezes into three dominant jagged ice spikes; a short downward burst of frost and a few water droplets communicate a pet-cast freezing impact.
Materials: turbulent water, refracted cyan light, silver core, translucent ice, and freezing vapor.
Palette: water blue, frost cyan, silver, deep navy, charcoal.
Composition note: one fist, one orb, exactly three dominant ice spikes.
Additional exclusions: full creature, face, or extra hands.
```

### ColdSnap

```text
Subject: one weathered silver hourglass violently snapped into two separated halves along a sharp diagonal break, its stopped blue frost grains exploding inward into one cyan ice fracture; exactly three large ice shards emphasize an instant cooldown reset.
Materials: tarnished silver, cracked glass, frozen grains, ice, and restrained cold mist.
Palette: frost cyan, deep blue, weathered silver, charcoal.
Composition note: one hourglass in exactly two halves and exactly three dominant ice shards.
Additional exclusions: clock face or readable numbers.
```

## Regeneration and acceptance notes

Every output was visually inspected at full size. `IceArmor` passed on its initial
generation. The following initial drafts had correct subjects but allowed a flame,
crystal, wrist, armor point, or frost effect too close to the canvas edge. Each was
regenerated once with an image-edit call that explicitly preserved the subject,
materials, lighting, palette, and uniform matte while changing only scale and
centering:

| Ability | Final targeted correction |
|---|---|
| Scorch | Scale the complete motif down about 18%; require at least 12% matte around every flame tip, ember, smoke wisp, metal point, and fragment. |
| Pyroblast | Scale down about 14%; require at least 11% matte around the flame, ember, ash wisp, and meteor edge. |
| ConeOfCold | Scale down about 15% and move slightly upward; expose the complete focus base and keep exactly three ice blades. |
| Spellsteal | Scale down about 14%; expose the complete wrist, claws, shard, and siphon. |
| IceBlock | Scale down about 15%; expose the complete monolith, base, apex, and three chips. |
| ManaShield | Scale down about 9%; preserve exactly three plates and one core. |
| MoltenArmor | Scale down about 13%; expose all flame tips, shoulder points, molten drips, and the lower armor point. |
| MageArmor | Scale down about 10%; expose the complete collar, shoulders, clasp, and lower plate. |
| WaterElementalFreeze | Scale down about 14%; preserve one fist, one orb, and exactly three dominant ice spikes. |
| ColdSnap | Scale down about 11%; preserve exactly two hourglass halves and exactly three dominant ice shards. |

Rejected first-pass images remain only in the image tool's generated-output cache;
they are not copied into this project lineage.

## Accepted master hashes

```text
ColdSnap             eaf80b964ac26be8930fbbd120a5ff403238ca77b61097e2b058fcb742723011
ConeOfCold           bed815044d1bf93a18ba19fc6058b5f09c852263296f88b65bb36a97b43efe21
IceArmor             a8c930252cf6221ff7d13d9fd39608df9edada8641d163af9c2e9222234f9a06
IceBlock             4cca2a9726d99d6e1e28e415f006b0ed4e892a6a7e99a8cddb4e5bc0d682710d
MageArmor            82f26b265917d29fa5b5999b5fa804fe0fc61f229b8cffaf66b450f406b3df9f
ManaShield           ad959528c4434052db4bdab77bf7694869c44ca305364b5de913e79109d1b3ad
MoltenArmor          d84b195d750af47b1b58b589f303fb4e1f4bd19952448a4e9eb3e1934000631a
Pyroblast            ae2746ffb44ca3e1b40c3b7728ff4e30d95161e84ccf40cc0e8dc18b7aeab70b
Scorch               57ae1bd7f9bc9ca5760603805aaede99bbb61133c0e621ab59bf8f4325fcf5b7
Spellsteal           a8238579b42cea2c810c901b73a8050fa4a10083952e45195a33080302f8c69e
WaterElementalFreeze 41293edbd8cfce1f545a8edcef2599ebe9c7c6d837444e49738e558d6abe5007
```
