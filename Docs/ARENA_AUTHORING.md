# Arena Authoring Contract

The arena is one deterministic XZ-plane gameplay map. Its visible environment and
Core collision must describe the same space. `Bootstrap.unity` is generated output,
not the place to author either one.

## Current geometry

| Contract | Current value | Source |
|---|---:|---|
| Safe-stone radius | `15.0 m` | `Core/ArenaLayout.cs` |
| Outer kill radius | `20.5 m` | `Core/ArenaLayout.cs` |
| Team spawn X offset | `5.7 m` | `Core/ArenaLayout.cs` |
| Pair spawn Z offset | `4.5 m` | `Core/ArenaLayout.cs` |
| Wardstone centers | `(0, -3.8)` and `(0, +3.8)` in XZ | `Core/ArenaCollisionMap.cs` + `ArenaLayout` |
| Wardstone gameplay radius | `1.05 m` | `Core/ArenaLayout.cs` |
| Wardstone visual height | `2.35 m` | `Core/ArenaLayout.cs` |
| Wardstone collider height | `2.8 m` | `Core/ArenaLayout.cs` |
| Combatant collision radius | `0.55 m` | `Core/ArenaLayout.cs` |
| Arena tick rate | `30 Hz` | `Core/CombatRules.cs` |
| Lava damage | `24 health/s` | `Core/CombatRules.cs` |

Core uses `ArenaCollisionMap.ArenaDefault` for movement sweep/slide, knockback,
placement, Blink, Shadowstep, line of sight, and bot detours. The two wardstones are
circular blockers. Rendering uses matching layout constants in
`Editor/Authoring/EnvironmentAuthoring.cs`.

## Sources of truth

- `Assets/SessionRpg/Core/ArenaLayout.cs`: shared dimensions and spawn offsets.
- `Assets/SessionRpg/Core/ArenaCollisionMap.cs`: authoritative planar collision,
  LOS, placement, mobility, knockback, and detour behavior.
- `Assets/SessionRpg/Core/CombatRules.cs`: safe/kill radii, movement, tick, lava.
- `Assets/SessionRpg/Editor/Authoring/EnvironmentAuthoring.cs`: visible stone,
  lava, runes, cover mesh/proxy, and non-gameplay decoration.
- `Assets/SessionRpg/Content/Arenas/**/Prefabs`: generated presentation prefabs
  with separate `Visuals`, `GameplayProxies`, `FlatGround`, `HazardSurfaces`, and
  bounded capture-anchor hierarchies.
- `Assets/SessionRpg/Settings/ArenaCatalog.asset`: stable Arena, legacy Boss, and
  Hero Siege presentation IDs and the current default for each content mode.
- `Tools/Agent/arena-variants.json`: source manifest for catalog membership,
  locked gameplay-layout IDs, supported generator recipes, output paths, versions,
  art status, and capture briefs.
- `Assets/SessionRpg/Unity/Gameplay/PrototypeGameController.cs`: current Arena
  roster/spawn composition using `ArenaLayout`, plus the runtime selection of the
  Hero Siege presentation and its one-/four-lane variant.
- `Assets/SessionRpg/Application/HeroSiegeRunController.cs`: authoritative Siege
  variant, lane, timed-wave, spawn, objective, and economy contracts.
- `Assets/SessionRpg/Editor/Authoring/SessionRpgAuthoringGenerator.cs`: generates
  the presentation prefabs, catalog, and prefab instances in Bootstrap.

There are not separate menu/Arena/Boss/Siege scenes today. The content contract
supports either prefabs or a future additive scene, but the current flow uses
prefabs. Do not switch a catalog entry to an additive scene unless loading/lifetime
behavior is implemented and tested as part of the same task.

## Invariants

- Authoritative positions are planar. Gameplay Y does not encode elevation.
- Unity colliders support ground presentation and pointer/world queries; they do not
  replace Core collision or decide outcomes.
- `WorldObstacle` is layer 8, `Combatant` is layer 9, and `GroundSurface` is layer
  10. Decoration belongs on the authored decoration/ignore-query layer and must not
  create false gameplay collision.
- `FlatGround` contains only the planar safe-surface colliders at combat Y. Lower
  lava belongs in `HazardSurfaces`; visible cover belongs in `Visuals`, with its
  authoritative query collider mirrored under `GameplayProxies`.
- The visible wardstone footprint, collision proxy, Core circle, LOS occlusion, and
  bot detour must agree. Visual mesh ornament may extend upward, not sideways into
  misleading collision.
- The safe-stone edge and lava transition must match Core radii. The additional
  visual lava margin is background coverage, not playable space.
- Spawns, Blink destinations, Shadowstep positions, knockback paths, and recovery
  routes must begin valid and remain deterministic.
- Runes and props provide orientation only. They cannot resemble a telegraph, target
  marker, safe path, or accessible platform when they are not one.
- `ashen_stronghold` is one persistent central-fortress presentation with four
  cardinal approach lanes. Solo Lane activates only the east-lane combat contract;
  Four Lanes activates all four. They share one catalog entry: presentation objects
  cannot add a defender, spawn, wave, objective hit, purchase, or encounter reset.
- Arena retains its reviewed `90° ± 60°` orbit arc. Ashen Stronghold supports full
  wrapped yaw for its cardinal defense, but each representative heading still needs
  fresh obstruction-safe composition and performance review before acceptance.

High ground remains deferred. Do not add visual-only elevation: the prior version
made planar combatants appear airborne and let them move through ledges. A real
elevation change requires a new authoritative geometry representation plus movement,
LOS, knockback, Blink, Shadowstep, bot navigation, camera, and grounding tests before
the visible platform is authored.

## Change recipe

### Presentation-only variant

1. Keep the declared `gameplayLayoutId` unchanged and use the same authoritative
   ground, hazard, cover, spawn, and camera-query contract.
2. Add one entry to `Tools/Agent/arena-variants.json` with a unique stable ID and
   output path. Add an explicit supported recipe in
   `SessionRpgAuthoringGenerator`; unknown recipes fail instead of silently
   producing the wrong arena.
3. Build art only under `Visuals`. Reuse the layout's `GameplayProxies`,
   `FlatGround`, and `HazardSurfaces`; never infer gameplay from decoration.
4. Author Overview, Gameplay, and DenseCombat capture anchors and update the
   bounded visual-review scenarios.
5. Rebuild and verify the generated catalog. A cataloged non-default presentation
   is not runtime-selectable until loading/selection is implemented separately.

### Gameplay-layout change

1. State the gameplay reason and list affected contracts before moving art.
2. Change `ArenaLayout`, `ArenaCollisionMap`, or `CombatRules` for outcome geometry.
3. Add/update Core tests for valid placement, movement sweep, LOS, knockback,
   mobility, and deterministic ordering. Update bot tests for route changes.
4. Make `EnvironmentAuthoring` consume the same constants. Decorative-only work may
   stay there, provided it obeys the invariants.
5. Update spawn composition only through `PrototypeGameController`/shared layout;
   never drag generated combatants in the scene.
6. Close Unity and run:

```sh
Tools/Agent/unity.sh rebuild
Tools/Agent/unity.sh validate
Tools/Agent/unity.sh test-edit
Tools/Agent/unity.sh test-play
```

7. Review generated `Bootstrap.unity` and related assets for only intended churn.
8. Manually check both movement directions around each wardstone, click targeting,
   cast LOS before/during resolution, bot routes, knockback collision, Blink,
   Shadowstep, safe edge, lava, camera obstruction, and all four spawn points.
9. Capture 16:9 and tall-landscape views showing that cover and lava boundaries are
   readable at gameplay zoom.

## Arena rejection conditions

Reject the change if any visible collider differs from Core, a decorative mesh
blocks a ray/movement unexpectedly, a legal ability resolves differently from its
readiness cue, an actor is grounded visually but invalid in Core (or vice versa), a
bot oscillates on cover, a mobility skill enters cover/lava unexpectedly, a spawn
overlaps a wardstone, or a generated-scene manual fix is required after rebuilding.
