# Personal Worlds: Persistent PvE Leveling

Concept record: 2026-09-04.

Status: the direction below is agreed with the user. Detailed rules and the first
region sketch remain proposals. This is a design document, not a claim of runtime
support or an approved implementation schedule. It does not replace the current
Arena-first delivery plan in [DESIGN.md](../DESIGN.md).

## Agreed direction

The player develops a permanent RPG character while exploring their own generated
world. Friends can enter that world to level together, explore, and overcome
difficult enemies.

- Character levels, learned abilities, and equipment persist between adventures.
  There is no roguelite run-level or temporary build-progression reset.
- Each personal world is a collection of regions, reached through a world map.
  A region is a compact, explorable place, not just one encounter room.
- Regions are generated for the player's world and then saved. Logging out,
  returning, or dying does not rearrange discovered geography.
- Exploration, opened routes, and completed world milestones persist. Repeatable
  encounters can return under explicit rules; persistence need not leave an empty
  world.
- A region spans multiple sessions. A single session can still produce a useful
  result: a level, an item, a discovery, or a completed objective.
- Visitors keep their earned character rewards. Permanent changes to the visited
  region belong to that world's owner.
- The intended feel combines the user's MLBB reference for combat immediacy and
  intensity with their WoW reference for class identity, progression, and
  cooperation. These are experience references, not instructions to copy content.

The earlier idea of resetting expedition upgrades is superseded. Ordinary timed
combat buffs are not a separate run-progression system and remain compatible with
this direction.

## Player promise

My character keeps growing, my world remembers my progress, and a friend can join
me for the part of the adventure that interests us today.

Depth should come from how abilities, enemies, terrain, talents, and equipment
interact. Fast mobile play should come from responsive controls, readable fights,
short preparation, and purposeful travel. Neither goal requires making every
ability instant or presenting the whole ability library at once.

| Scale | Meaningful player goal |
|---|---|
| One fight | Use the class well and solve the enemy group's tactical problem. |
| One session | Gain permanent progress or open a useful part of the region. |
| Several sessions | Explore and overcome the region, then reach a new one. |

## World structure

The world map communicates region identity, connections, danger, discoveries, and
unlock conditions. Travel moves the player to an available regional entry point;
movement, exploration, and combat happen inside the region.

Generation should produce different decisions, not merely shuffled scenery. A
player might discover a dangerous shortcut, a useful landmark behind an elite, or
an unusual enemy combination worth inviting a friend to fight.

Proposed generation boundaries:

- Compose validated authored locations, paths, and encounter spaces. Keep combat
  geometry, camera readability, and class mobility reliable.
- Vary compatible connections, encounter combinations, optional locations, and
  reward opportunities. The first prototype does not need every variation axis.
- Guarantee an accessible start, connected objectives, and a feasible progression
  route. Never place the only required unlock behind its own locked gate.
- Preserve the resulting layout and milestone state, with stable content IDs,
  the seed, and the generation/content version. A seed alone does not preserve a
  world when its generator changes. Full-world rerolling, new chapters, and
  version migration need separate decisions.
- Avoid essential character abilities that can only be found in somebody else's
  rare world. Visiting should be attractive, not a compulsory search for a seed.

The exact world/region ownership model for multiple characters is not yet chosen.
Do not silently create a new world for every character or assume shared progression
across all characters.

## Character progress and world progress

| State | Owner's character | Visiting character |
|---|---|---|
| Earned XP, equipment, character unlocks | Retained | Retained |
| Visited world's discoveries, shortcuts, first-clear milestones | Saved in the owner's world | Does not overwrite the visitor's own world |

Character eligibility and a local-world objective are different facts. For example,
an eligible character reward from helping defeat a guardian should not require a
second kill at home merely because the first kill was as a visitor. That does not
automatically open a physical gate in the visitor's home region. Exact reward and
quest-credit rules remain to be specified.

Proposed progression principles:

- Learn core class tools through permanent progression; do not relearn them each
  session. Later talents and equipment can deepen the build.
- Give regions readable danger ranges. Avoid automatically raising every old enemy
  whenever the player levels, so returning stronger remains meaningful.
- Make ordinary leveling viable alone for every supported specialization. Optional
  difficult content can reward coordination without blocking the basic journey.
- Decide the active PvE kit through playtesting. The current full-kit interface
  exposes up to 18 actions; this document does not authorize removing abilities,
  changing bindings, or imposing a new button count.
- Preserve normalized PvP if PvE equipment and levels are shared with Arena.
  Whether PvE changes Arena's product priority remains a separate decision.

## Co-op baseline to validate

World ownership should govern permanent world decisions, not the visitor's build
or already-earned rewards. Each participant should have a reason to be there.

Proposed first rules:

- Invite a friend to an available refuge or safe entry point between encounters.
  Unrestricted mid-fight joining is not required for the first version.
- Fix encounter difficulty when combat starts. Joining, leaving, or reconnecting
  must not weaken a live boss or grant duplicate rewards.
- Give personal rewards rather than last-hit ownership or competition over a
  single loot drop. Healing, control, and rescue count as participation.
- A visitor retains already-earned rewards when leaving or being removed.
- If the owner ends the session, visitors return safely and saved progress remains.
  Host migration and visitors advancing an absent owner's world are not assumed.

Party size, level synchronization, loot eligibility, and save/disconnect recovery
still need concrete rules. A first online proof should use one owner and one remote
guest. Local bots can help test encounters but cannot validate the social loop.

## First region sketch: Ashen Frontier

Working name only; this is not an existing region, recipe, or content ID. The
existing Ashen Causeway visual language and enemy archetypes are possible starting
material, not evidence that this layout is already built.

Purpose: prove that a compact region can support permanent character growth,
recognizable places, a route choice, and a natural invitation to a friend.

Regional goal: defeat the guardian at the eastern stronghold and open passage to
the next region. The next region itself need not be built for this proof.

| Place | Experience and purpose |
|---|---|
| Refuge | Clear arrival point, recovery, return travel, and safe party meeting point. No elaborate town economy is required. |
| Crossroads | Reveal the stronghold goal and offer a legible route choice. |
| Old Road | Ordinary leveling camps and a reliable route toward the regional objective. |
| Ruins route | A more dangerous optional branch with an elite and a useful discovery or reward. |
| Shortcut | A connection opened through exploration that makes later visits easier. |
| Eastern stronghold | A guardian encounter combining lessons from the region; first victory unlocks onward travel. |

Initial content budget hypothesis: one refuge, one crossroads, two routes, three
to five ordinary camps, one optional elite, one shortcut, and one guardian. These
are planning limits, not accepted content requirements or runtime capacities.

The fixed roles above are an encounter/layout grammar, not one permanently fixed
map. Once the authored version works, a small set of validated layouts can vary
the route connections and compatible camp placements while keeping progression
legible. A seed and saved world state must reproduce the chosen arrangement.

### Example play sequence

1. Arrive, see the regional goal, and fight the first nearby camp without a long
   setup sequence. Earn persistent progress and learn what threatens this region.
2. Reach the crossroads. Follow the ordinary route, or investigate a visibly more
   dangerous optional landmark. Discover a shortcut worth remembering.
3. Return on another session with the same character, geography, and recorded
   milestones. Choose the next objective rather than restarting the region.
4. Invite a friend for the elite or stronghold. One player controls a dangerous
   enemy while the other interrupts or focuses a priority target. Other class
   combinations must have viable approaches; a specific pairing is not mandatory.
5. Win together. Both characters retain their rewards. The owner's onward passage
   opens; the visitor's own map is not replaced or silently completed.

The immediate satisfaction of the fight and the permanent consequence of the
victory should both be evident to the player.

## Open decisions before implementation

1. **Death, respawns, and stopping play.** No character/run reset is agreed. The
   recommended starting point is refuge recovery with earned XP/equipment and
   completed milestones retained. Encounter retry, ordinary-camp refresh, and
   saving an unfinished activity still need exact rules.
2. **First leveling arc.** Choose its permanent ability unlocks, talent choices,
   level range, and a minimal equipment/reward model. Do not import Training's
   session-local level 1-6 curve as permanent progression without review.
3. **Different-level friends.** Choose level-sync and reward rules that preserve
   the lower-level player's decisions without making helping unrewarding or
   low-level world hopping the optimal progression strategy.
4. **World lifecycle.** Decide whether completed regions remain indefinitely,
   whether new chapters generate more regions, and whether voluntary regeneration
   exists. Death and logout do not regenerate them.

## Prototype and evidence boundary

The current project has local combat, a linear streamed Training route, and a
four-floor local Dungeon with bots. Its private-session foundation does not yet
transport cooperative combat. A separate local-only [server foundation](../Server/README.md)
now implements character/world bootstrap persistence, saved generated connectivity
plans, and owner/guest admission metadata. It does not implement walkable generated
regions, networked combat, earned RPG progression, or inventory; it is not wired
into the Unity game. Fresh backend evidence is recorded separately in
[Server Foundation Evidence](SERVER_FOUNDATION_EVIDENCE.md).
See [Demo Readiness](DEMO_READINESS.md) and [Architecture](../ARCHITECTURE.md).

Remaining gameplay sequence (the local server foundation was approved separately):

1. Walk through one regional layout and its permanent progression choices on
   paper. Resolve the death/respawn and first-leveling-arc rules.
2. Prove one local region, a bounded generation variant set, and save/return
   behavior. Keep it explicitly local; use existing classes and enemies where
   suitable. One static layout alone does not validate procedural replay value.
3. Add real owner-plus-guest combat and reward/save recovery, then test both
   players' experience. A bot-backed demonstration is not a co-op acceptance test.

Preserve deterministic 30 Hz Core combat, stable identifiers, bounded actor/view
lifetime, and Unity-independent Core/Application rules. This concept is not a
reason to reinterpret legacy mode IDs or hand-edit generated scene assets.

Questions for playtests:

- Can the player identify a useful objective for a short session?
- Does returning feel like continuing their own journey rather than starting over?
- Do the route and encounter choices reward class knowledge, not just larger stats?
- Can the player explain what makes this region different from another seed?
- Do both owner and guest feel their time advanced their own characters?
- Is there a discovery or challenge the player actually wants to share with a friend?

No implementation, manual playtest, performance, device, or networking acceptance
is implied by this document.
