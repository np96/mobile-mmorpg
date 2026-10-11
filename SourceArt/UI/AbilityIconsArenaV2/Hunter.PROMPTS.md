# Hunter Arena V2 icon prompts

Generation date: 2026-08-31

Tool: OpenAI built-in `image_gen`

Output contract: one distinct generation call per ability, one square PNG per call,
and no sprite sheets. The accepted files are 1254 x 1254, 8-bit RGB PNG masters.

## Style references

The initial generation calls used these accepted first-party Hunter Leveling V2
masters as style references, in this order:

1. `SourceArt/UI/AbilityIconsLevelingV2/Masters/Hunter/SteadyShot.png`
2. `SourceArt/UI/AbilityIconsLevelingV2/Masters/Hunter/SerpentSting.png`
3. `SourceArt/UI/AbilityIconsLevelingV2/Masters/Hunter/Disengage.png`

They were references for style, materials, lighting, matte, padding, and finish
only. Their subjects and compositions were not reused.

## Shared initial prompt

The following exact prompt template was used for the initial AimedShot through
Readiness calls. `<AbilityId>` and `<Subject>` were replaced by the exact values
listed in the following section.

```text
Use case: stylized-concept
Asset type: original game UI ability-icon master for a mobile dark-fantasy action RPG
Input images: Images 1-3 are accepted first-party Hunter Leveling V2 masters used only as style, material, lighting, matte, padding, and finish references; do not reuse their subjects or compositions.
Primary request: Create one original Hunter ability icon for <AbilityId>.
Scene/backdrop: one fully opaque, perfectly uniform near-black neutral matte #080A0D edge-to-edge.
Subject: <Subject>.
Style/medium: premium serious dark-fantasy hand-painted realism; bold phone-scale silhouette; weathered aged steel, leather, horn, and bone where appropriate; realistic material wear; no cartoon or flat vector rendering.
Composition/framing: square 1:1; one centered compact motif occupying roughly 70–78% of the canvas; at least 10% clear matte padding on every edge; nothing cropped or touching edges; readable at 48–96 px.
Lighting/mood: dramatic upper-left rim light, deep localized shadows, restrained forest-green, amber, and cold-cyan ability energy; mature, dangerous, grounded.
Constraints: exactly the requested subject and count; original composition; preserve the perfectly uniform opaque matte; no text, letters, numbers, readable runes, logos, trademarks, or watermark; no transparency.
Avoid: checkerboard or transparency simulation, tiles, gradient plate, vignette, scenery, circular crop, halo-ring as UI frame, baked frame, medallion, border, button, ornamental UI chrome, excessive bloom, tiny decorative clutter, or any copyrighted game-icon composition.
```

## Exact subject prompts

### AimedShot

```text
one heavy blackened-steel broadhead held precisely against a taut bowstring, aligned through a small split-brass bow sight; a single restrained amber focus glint at the arrow tip, no hands or extra arrows
```

### ScatterShot

```text
exactly one squat blunt-headed hunting arrow striking and denting a weathered steel plate, with exactly three compact amber ricochet sparks diverging outward; no extra arrows
```

### KillShot

```text
exactly one brutal bone-and-blackened-steel broadhead piercing through the crown of one fractured horned beast skull, with a restrained amber execution flare and a few grounded dust fragments; no blood
```

### ScorpidSting

```text
exactly one armored black forest-scorpion tail coiled tightly around one dark barbed arrowhead, its stinger poised beside a single forest-green venom droplet; no full scorpion and no extra arrows
```

### TranquilizingShot

```text
exactly one silver hooked hunting arrow severing exactly three small amber enchantment chains wrapped around one weathered beast fang, while the broken glow settles into a restrained forest-green calming mist; no extra arrows
```

### ConcussiveShot

```text
exactly one blunt blackened-steel hunting arrow striking the brow of one empty dented iron helm, with a compact cold-cyan concussive shock burst; no face, no person, and no extra arrows
```

### FrostTrap

```text
exactly one rugged blackened-steel jaw trap sprung open around one compact cold-cyan frost crystal, with icy teeth, restrained shards, and low freezing mist; no animal and no extra trap
```

### SilencingShot

```text
exactly one blackened barbed hunting arrow piercing and splitting one aged-bronze war horn, with a single dying amber sound ripple breaking apart close to the impact; no extra arrows or instruments
```

### Flare

```text
exactly one rugged blackened-iron flare arrowhead burning upright with one compact amber-white starburst, controlled sparks, and a thin smoke plume that reveals one faint hidden claw-print silhouette behind it; no extra arrows or ground scene
```

### Deterrence

```text
one reinforced dark-leather hunter vambrace braced behind exactly two crossed blackened hunting blades, deflecting exactly two incoming steel arrowheads into restrained amber sparks; no shield and no extra weapons
```

### FeignDeath

```text
one collapsed empty dark-leather hunter hood beside one limp weathered glove and exactly one snapped hunting arrow, with a single fading cold-cyan breath wisp; no visible face, body, blood, or scenery
```

### MastersCall

```text
exactly one weathered leather hunter gauntlet clasping exactly one dark-furred wolf forepaw as together they break one cold-cyan ice chain, with a restrained amber freedom flare; no faces, no full characters, and no extra limbs
```

### WingClip

```text
exactly one hooked blackened-steel hunting blade slicing cleanly across exactly one large dark hawk feather, with a compact amber cut flare and a few severed barbs; no wings, bird, hand, or extra weapon
```

### AspectOfTheDragonhawk

```text
exactly one fierce dragon-hawk head in three-quarter profile, combining a hooked raptor beak, scaled brow, swept feather crest, and one visible amber eye, with restrained forest-green and amber breath wisps; no body and no second head
```

### AspectOfTheViper

```text
exactly one weathered bone viper-head talisman bound with dark leather cord and small aged-steel fittings, open fangs and one visible cold-cyan eye, with a restrained forest-green venom wisp; a carved hunter charm, not a living full serpent
```

### RoarOfSacrifice

```text
exactly one dark wolf head roaring protectively in front of one battered leather-and-steel shoulder guard while exactly one incoming hunting arrow snaps against a compact amber protective blast; no full animal, person, blood, or circular shield
```

### SpiderWeb

```text
exactly one realistic black forest spider crouched over one compact torn radial web knot, its armored legs clearly separated and tipped with cold-cyan dew, with restrained forest-green shadow beneath; the web remains compact and does not reach the canvas edges; no second spider
```

### Readiness

```text
exactly one weathered steel-and-leather hunting hourglass lashed to a compact dark bow grip, amber sand visibly flowing upward inside it, with exactly one blackened broadhead resting ready beside the hourglass and one short incomplete amber rewind streak; no full bow, no clock face, and no extra arrows
```

## ChimeraShot initial prompt

ChimeraShot was generated first. Its wording differs slightly from the later
shared template, so its complete exact prompt is preserved here rather than being
normalized retroactively.

```text
Use case: stylized-concept
Asset type: original game UI ability-icon master for a mobile dark-fantasy action RPG
Input images: Images 1-3 are accepted first-party Hunter Leveling V2 masters used only as style, material, lighting, matte, padding, and finish references; do not reuse their subjects or compositions.
Primary request: Create one original Hunter ability icon for ChimeraShot.
Scene/backdrop: one fully opaque, perfectly uniform near-black neutral matte #080A0D edge-to-edge.
Subject: exactly one blackened-steel hunting arrow with a compact tri-bladed broadhead, while three restrained bestial energy essences—a wolf, serpent, and hawk—converge tightly into the arrowhead in forest-green, amber, and cold-cyan wisps; the three heads are spectral accents, not separate full animals.
Style/medium: premium serious dark-fantasy hand-painted realism; bold phone-scale silhouette; weathered aged steel, leather, horn or bone as appropriate; realistic material wear, no cartoon or flat vector rendering.
Composition/framing: square 1:1; one centered compact motif occupying roughly 70–78% of the canvas; at least 10% clear matte padding on every edge; nothing cropped or touching edges; readable at 48–96 px.
Lighting/mood: dramatic upper-left rim light, deep localized shadows, restrained forest-green, amber, or cold-cyan ability energy appropriate to subject; mature, dangerous, grounded.
Constraints: exactly the requested subject and count; original composition; preserve the perfectly uniform opaque matte; no text, letters, numbers, readable runes, logos, trademarks, or watermark; no transparency.
Avoid: checkerboard or transparency simulation, tiles, gradient plate, vignette, scenery, circular crop, halo-ring as UI frame, baked frame, medallion, border, button, ornamental UI chrome, excessive bloom, tiny decorative clutter, or any copyrighted game-icon composition.
```

## Targeted correction prompts

Four initial drafts had obvious edge-contact or cropping problems. Each received
one targeted edit call. The accepted master is the corrected result in every case.

### AimedShot

Reason: the initial arrow shaft was cropped at the left edge.

Input order:

1. Rejected AimedShot draft
2. `SteadyShot.png`
3. `SerpentSting.png`
4. `Disengage.png`

```text
Use case: precise-object-edit
Asset type: original game UI ability-icon master for a mobile dark-fantasy action RPG
Input images: Image 1 is the rejected AimedShot draft and the edit target. Images 2-4 are accepted first-party Hunter Leveling V2 style references only.
Primary request: Correct only the composition and padding of Image 1. Zoom the entire bow, sight, taut string, and single arrow motif out so every physical part is fully reconstructed and fully visible.
Scene/backdrop: preserve one fully opaque, perfectly uniform near-black neutral matte #080A0D edge-to-edge.
Subject: preserve exactly one heavy blackened-steel broadhead held precisely against a taut bowstring and aligned through a small split-brass bow sight, with one restrained amber focus glint; no hands and no extra arrows.
Composition/framing: square 1:1; compact centered motif occupying no more than 70-74% of the canvas; at least 15% visibly clear matte padding on all four edges; the arrow shaft, bow limbs, string, and glow must not touch or cross any edge.
Invariants: preserve the serious dark-fantasy hand-painted realism, aged steel/leather materials, dramatic upper-left rim light, deep shadows, and restrained amber energy. Change only scale/composition and reconstruct the previously clipped shaft.
Constraints: no crop, no text, no letters, no numbers, no readable runes, no logos, no watermark, no transparency, no checkerboard, no gradient plate, no vignette, no scenery, no circular crop, no frame, no medallion, no border, no button, no UI chrome, no excessive bloom, no copyrighted game-icon composition.
```

### Flare

Reason: the initial flare-arrow shaft was cut by the bottom edge.

Input order:

1. Rejected Flare draft
2. `SteadyShot.png`
3. `SerpentSting.png`

```text
Use case: precise-object-edit
Asset type: original game UI ability-icon master for a mobile dark-fantasy action RPG
Input images: Image 1 is the rejected Flare draft and edit target. Preserve exactly one rugged flare arrowhead, its short complete shaft and nock, one compact amber-white starburst, smoke, and one faint claw print. Reconstruct the shaft that was cut at the bottom; its end must terminate visibly inside the matte. Images 2-3 are accepted Hunter style references only.
Scene/backdrop: preserve one fully opaque, perfectly uniform near-black neutral matte #080A0D edge-to-edge.
Style/invariants: preserve the existing serious dark-fantasy hand-painted realism, weathered aged steel and leather, dramatic upper-left rim light, deep localized shadows, restrained energy color, subject identity, object count, and original composition.
Primary edit: change only scale and composition. Zoom the complete motif out and reconstruct every previously clipped object so all physical parts and all energy remain fully visible.
Composition/framing: square 1:1; centered compact motif occupying no more than 68-72% of the canvas; at least 15% visibly clear uniform matte padding on all four edges; no object, spark, smoke, chain, weapon, arrow, or glow may touch or cross an edge.
Constraints: no text, letters, numbers, readable runes, logos, watermark, transparency, checkerboard, gradient plate, vignette, scenery, circular crop, frame, medallion, border, button, UI chrome, excessive bloom, or copyrighted game-icon composition.
```

### Deterrence

Reason: the two deflected arrows were clipped by the image edges.

Input order:

1. Rejected Deterrence draft
2. `SteadyShot.png`
3. `SerpentSting.png`

```text
Use case: precise-object-edit
Asset type: original game UI ability-icon master for a mobile dark-fantasy action RPG
Input images: Image 1 is the rejected Deterrence draft and edit target. Preserve one leather vambrace, exactly two crossed hunting blades, and exactly two deflected incoming arrows. Reconstruct both arrows completely, including their shafts and fletching; both must terminate visibly inside the matte. Images 2-3 are accepted Hunter style references only.
Scene/backdrop: preserve one fully opaque, perfectly uniform near-black neutral matte #080A0D edge-to-edge.
Style/invariants: preserve the existing serious dark-fantasy hand-painted realism, weathered aged steel and leather, dramatic upper-left rim light, deep localized shadows, restrained energy color, subject identity, object count, and original composition.
Primary edit: change only scale and composition. Zoom the complete motif out and reconstruct every previously clipped object so all physical parts and all energy remain fully visible.
Composition/framing: square 1:1; centered compact motif occupying no more than 68-72% of the canvas; at least 15% visibly clear uniform matte padding on all four edges; no object, spark, smoke, chain, weapon, arrow, or glow may touch or cross an edge.
Constraints: no text, letters, numbers, readable runes, logos, watermark, transparency, checkerboard, gradient plate, vignette, scenery, circular crop, frame, medallion, border, button, UI chrome, excessive bloom, or copyrighted game-icon composition.
```

### MastersCall

Reason: the broken chain segments ran through the image edges.

Input order:

1. Rejected MastersCall draft
2. `SteadyShot.png`
3. `SerpentSting.png`

```text
Use case: precise-object-edit
Asset type: original game UI ability-icon master for a mobile dark-fantasy action RPG
Input images: Image 1 is the rejected MastersCall draft and edit target. Preserve exactly one leather hunter gauntlet clasping exactly one dark wolf forepaw while one ice chain breaks into two segments. Reconstruct both broken chain segments so their broken ends and terminal links are fully visible and terminate inside the matte. Images 2-3 are accepted Hunter style references only.
Scene/backdrop: preserve one fully opaque, perfectly uniform near-black neutral matte #080A0D edge-to-edge.
Style/invariants: preserve the existing serious dark-fantasy hand-painted realism, weathered aged steel and leather, dramatic upper-left rim light, deep localized shadows, restrained energy color, subject identity, object count, and original composition.
Primary edit: change only scale and composition. Zoom the complete motif out and reconstruct every previously clipped object so all physical parts and all energy remain fully visible.
Composition/framing: square 1:1; centered compact motif occupying no more than 68-72% of the canvas; at least 15% visibly clear uniform matte padding on all four edges; no object, spark, smoke, chain, weapon, arrow, or glow may touch or cross an edge.
Constraints: no text, letters, numbers, readable runes, logos, watermark, transparency, checkerboard, gradient plate, vignette, scenery, circular crop, frame, medallion, border, button, UI chrome, excessive bloom, or copyrighted game-icon composition.
```

## Accepted master list

- `AimedShot.png`
- `AspectOfTheDragonhawk.png`
- `AspectOfTheViper.png`
- `ChimeraShot.png`
- `ConcussiveShot.png`
- `Deterrence.png`
- `FeignDeath.png`
- `Flare.png`
- `FrostTrap.png`
- `KillShot.png`
- `MastersCall.png`
- `Readiness.png`
- `RoarOfSacrifice.png`
- `ScatterShot.png`
- `ScorpidSting.png`
- `SilencingShot.png`
- `SpiderWeb.png`
- `TranquilizingShot.png`
- `WingClip.png`
