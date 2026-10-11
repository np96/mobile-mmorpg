# Agent Workflow

## Public source checkout

This public checkout selects bundled CC0 Quaternius character bases. The dated
local-development evidence and Mixamo rows below describe the separate licensed
art build, not the public character selection. Inactive licensed payloads and
rejected/intermediate art are intentionally absent. See
[public source distribution](PUBLIC_SOURCE_DISTRIBUTION.md) before changing
character sources or publishing assets. The source selectors and generated
foundation in this checkout are authoritative for its actual composition.

This is the operational source of truth for changing Session RPG safely. Paths in
this document are relative to `game-rpg/` unless noted otherwise.

## Project facts

| Key | Value |
|---|---|
| Unity | `6000.5.6f1` |
| Scene | `Assets/SessionRpg/Scenes/Bootstrap.unity` |
| Simulation | deterministic Core, 30 Hz |
| Current source composition | local 2v2 Arena; three-participant legacy Boss; five-pack Ashen Causeway Training across seven authored districts with eight hostile NPC archetypes and a `3 -> 7` unlock curve; continuous 12-wave Hero Siege in one-lane solo or four-lane human-plus-three-bot variants; and four-floor Dungeon. Fresh evidence for the redesigned Siege boundary is tracked in `Docs/DEMO_READINESS.md`. |
| Networking | NGO lifecycle/composition foundation only; gameplay is not networked |
| PvE Leveling expansion | Separate no-quest local grinding mode in the 3 × 3 Sunbreak Basin zone, seven repeatable camps, Core-owned two-dimensional cover and device-local 1–10 XP. See `Docs/PVE_LEVELING_MODE.md` and `Docs/PVE_BASIN_EVIDENCE.md`; Training is unchanged. |
| Test assemblies | Core EditMode, Application EditMode, integrated PlayMode |
| CLI entrypoint | `Tools/Agent/unity.sh` |

Machine-readable equivalents live in `Tools/Agent/project-context.json`; the bounded
presentation capture set is `Tools/Agent/visual-review-matrix.json`.

## Dependency and ownership map

| Area | Owns | May depend on |
|---|---|---|
| `Assets/SessionRpg/Core` | rules, IDs, state, deterministic outcomes, geometry | .NET only |
| `Assets/SessionRpg/Application` | flow, bots, recap, presentation-facing state | Core |
| `Assets/SessionRpg/Networking` | dormant NGO session lifecycle boundary | Core, Application, NGO |
| `Assets/SessionRpg/Unity` | input, camera, scene loop, HUD, animation, VFX, audio | Core, Application, Unity |
| `Assets/SessionRpg/Editor` | validation, authored-asset generation, builds | all layers, Editor only |
| `Assets/SessionRpg/Tests` | deterministic and scene-contract verification | matching runtime assemblies |
| `Server/` | standalone local personal-world persistence/admission prototype | linked Core, .NET, SQLite; no Unity references |

Dependency direction points inward. A visual or input fix must not migrate combat
authority into a `MonoBehaviour`. Networking is a future transport path, not a
license to claim multiplayer behavior that is absent.

The arena manifest is deterministic content input. It currently declares three
prototype presentations—Arena, legacy Boss, and Hero Siege—and the generator embeds
only each content mode's default in Bootstrap. The Siege default is the persistent
central-fortress/four-cardinal-lane `ashen_stronghold` presentation; the selected
one- or four-lane gameplay variant does not create a second catalog entry. Catalog
membership does not make a non-default Arena selectable at runtime; selection/
loading work must be implemented and tested explicitly.

The world catalog and per-zone manifests are deterministic authoring inputs. Rebuild
turns them into a runtime catalog, additive presentation scenes, and exact build
scene membership. Runtime streaming does not make the chunks authoritative: Core
still owns position, collision, combat, pack activation, and XP. See
`Docs/WORLD_AUTHORING.md` before changing world membership or recipes.

## Source-of-truth matrix

| Path | Classification | Edit policy |
|---|---|---|
| `Assets/SessionRpg/{Core,Application,Networking,Unity}/**` | runtime source | Edit directly; keep assembly boundary and tests. |
| `Assets/SessionRpg/Editor/Authoring/*.cs` | authored-foundation source | Edit directly, then rebuild and review generated changes. |
| `Assets/SessionRpg/Shaders/**` | render source | Edit directly with shader/material/pass tests and a fresh gameplay-camera check. No shader or displacement may decide collision. |
| `Assets/ThirdParty/PolyHaven/SunbreakSurfaces/**` | immutable CC0 scanned PBR source | Preserve source bytes, official URLs, hash manifest, license and notices. `SunbreakScannedSurfaceAuthoring` owns channel-aware imports (sRGB albedo, linear roughness, real GL normals), 2K desktop/1K Android caps and generated bindings. Verify with `bash Tools/Art/verify_sunbreak_sources.sh`; rebuild after intentional source changes. |
| `Assets/SessionRpg/SourceArt/Environment/SunbreakNaturalV1/**` | first-party generated albedo source | Preserve V1 PNG masters and exact prompt/hash provenance. `SunbreakSurfaceAuthoring` owns bounded import settings and material bindings; rebuild after intentional source changes. These color images are not scanned PBR or height/normal data. |
| `Assets/SessionRpg/Editor/World/*.cs` | world-manifest contract | Edit directly only when the strict schema or validation rules change; update manifest tests in the same change. |
| `Tools/Agent/arena-variants.json` | arena membership/recipe source | Add a stable entry only with a supported generator recipe, capture brief, and tests; then rebuild. |
| `Tools/Agent/world-zones.json` | world/zone membership source | Add stable non-zero identities only with a supported authoring recipe and per-zone manifest; then rebuild. |
| `Tools/Agent/Worlds/**/zone.json` | zone/chunk membership and recipe source | Keep 64 m coordinates, connected footprints, unique seeds/paths, explicit load/retain/cap values, and supported biome/POI recipes; then rebuild. |
| `Assets/SessionRpg/Editor/Assets/*.cs` | third-party import contract | Edit only when the supported source/import contract changes. |
| `Assets/SessionRpg/Art/UI/AbilityIcons/*.png` | source art | Edit/replace deliberately; keep filename equal to `AbilityId`, then rebuild. |
| `SourceArt/UI/AbilityIconsLevelingV2/**` | immutable generation masters/provenance | Preserve the generated masters, prompt record, and hashes. Runtime RGB/alpha files are derived from this evidence; do not import the 1254 px masters into Unity. |
| `SourceArt/UI/AbilityIconsArenaV2/**` | immutable generation masters/provenance | Preserve the accepted class-grouped masters, exact prompt records, and hash manifest. Together with `AbilityIconsLevelingV2`, this evidence backs the complete playable Arena and PvE/Leveling icon union; do not import the 1254 px masters into Unity. |
| `Assets/SessionRpg/Art/UI/Hud/AbilityMedallionFrame.png` | derived first-party UI source | Regenerate only with `Tools/Art/extract_hud_medallion_alpha.py` from the preserved source, then rebuild. Do not hand-paint the runtime derivative. |
| `SourceArt/UI/AbilityMedallionFrame_*` | immutable generation source/provenance | Preserve the original generated source, full prompt, hash, and alpha-derivation record; it is not imported directly by Unity. |
| `Assets/SessionRpg/Art/Characters/HunterOutfitAlbedo.png` | first-party source art | Audited repaint of the CC0 Ranger UV layout. Run `configure-hunter-art`, then rebuild; do not hand-edit `Hunter_Outfit.mat`. |
| `Assets/SessionRpg/SourceArt/Characters/HunterProductionV1/**` | gated first-party character source | Dormant unless `HunterCharacterSourceSelection.Selected` is `ProductionV1`. Only copy a manually accepted DCC export using the validator's locked V1 filenames, select `ProductionV1`, run `configure-hunter-production` once, then rebuild. Validation rejects a stale `Combatant.prefab` source root and audits the generated material, body LODs, auxiliary culler, weapon state, and presenter references. |
| `Assets/ThirdParty/Trials/AdobeMixamo/ErikaArcher/**` | reversible licensed third-party game base | User-approved model-only Hunter base. The historical path preserves Unity identities. Preserve provenance and `.meta`; keep animation import disabled and raw rejected Mixamo animation FBXs outside runtime `Assets/`. Run `configure-mixamo-trial` only after a verified source/import change, then rebuild. The generated Hunter uses rigid project-driven bow/arrow props and deterministic project motion. Never distribute the original or modified Content Files standalone. |
| `SourceArt/Trials/AdobeMixamo/ErikaArcher/**` | immutable licensed source and evaluation evidence | Preserve the signed-in acquisition record, hashes, raw rejected clips, evaluation evidence, and embedded-game/standalone-file boundary. Do not put this directory in a public source distribution. |
| `Assets/ThirdParty/Trials/AdobeMixamo/PlayableClasses/**` | reversible licensed Priest/Rogue/Mage game bases | User-approved, model-only customizable bases. Preserve hashes, provenance, extracted textures, and `.meta`; keep model animation import disabled. Run `configure-mixamo-class-trials` only after a verified source/import change, then rebuild. Retain the generated Healer/Rogue/Mage controllers. Never distribute original or modified Content Files standalone. |
| `SourceArt/Trials/AdobeMixamo/PlayableClasses/**` | immutable licensed class sources and provenance | Preserve the three signed-in originals and aggregate manifest. They may enter embedded game builds but not public source, template, or asset-pack distributions; deformation, customization, and mobile polish remain separate work. |
| `Assets/ThirdParty/**` and license files | immutable source dependency | Do not casually edit, trim, or replace. Preserve `.meta`, provenance, and notices. |
| `Packages/manifest.json`, `Packages/packages-lock.json` | package source/lock | Change through Unity Package Manager; review both together. Never edit `Library/PackageCache`. |
| `Assets/SessionRpg/Scenes/Bootstrap.unity` | tracked generated output | Never hand-edit. Generated by `SessionRpgAuthoringGenerator`. |
| `Assets/SessionRpg/Content/Arenas/**` | tracked generated presentation content | Change the arena authoring source; keep visual, gameplay-proxy, flat-ground, hazard, and capture hierarchies separate. |
| `Assets/SessionRpg/Content/Worlds/**` | tracked generated presentation content | Never hand-edit chunk scenes. Change the catalog/zone manifest and supported world authoring recipe, then rebuild. |
| `Assets/SessionRpg/Prefabs/Combatant.prefab` | tracked generated output | Never hand-edit. Generated by `CharacterAuthoring`. |
| `Assets/SessionRpg/Art/{Animations,Audio,Characters,Environment,Materials,Vfx}/**` | tracked generated output | Change generator/source art, not serialized output. Controllers and environment meshes may be recreated; external manual references are unsupported. |
| `Assets/SessionRpg/Settings/**` | tracked generated output | Generated catalogs and URP assets; do not tune in Inspector. |
| `ProjectSettings/{ProjectSettings,GraphicsSettings,QualitySettings,TagManager,EditorBuildSettings}.asset` | tracked, partly generator-owned | The rebuild sets product/Android/input/URP/quality/layer/build-scene values. Change the generator for those fields. |
| `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, `.utmp`, IDE files | ephemeral Unity state | Never review, copy, or hand off as source. |
| repository `outputs/`, `work/`, `game-rpg/build/`, APKs, `.app`, XML, logs, captures | disposable evidence/build output | Keep outside `Assets`; do not use an old filename as current evidence. |

Every tracked Unity asset travels with its `.meta`. Do not resolve a GUID problem by
deleting a `.meta`. Resolve source code/import settings, then let the explicit
authoring path update assets at their stable paths.

## Safe loop

1. Save open Unity work. Inspect the current diff if version control is available.
2. Run `Tools/Agent/unity.sh doctor`.
3. Make the smallest source change in the owning layer.
4. Run the narrow tests while iterating. `--filter` accepts a Unity test filter.
5. If authoring/layout/serialized references changed, close Unity and run
   `Tools/Agent/unity.sh rebuild` once.
6. Run `validate`, then the required EditMode and PlayMode suites.
7. Review both source and generated diffs. Generated churn unrelated to the change
   is a defect; do not normalize it blindly.
8. Perform the manual checks from `DEMO_READINESS.md` and `VISUAL_QUALITY.md`.
9. Hand off fresh evidence and explicit omissions.

### Command matrix

Run from `game-rpg/`:

```sh
Tools/Agent/unity.sh doctor
Tools/Agent/unity.sh validate
Tools/Agent/unity.sh test-edit
Tools/Agent/unity.sh test-play
Tools/Agent/unity.sh test-all
Tools/Agent/unity.sh test-play --filter SessionRpg.Tests.PlayMode.BootstrapPlayModeTests
Tools/Agent/unity.sh rebuild
Tools/Agent/unity.sh build-android
Tools/Agent/unity.sh build-macos
```

The wrapper reads the pinned Unity version, locates the Hub Editor on macOS, uses
fresh per-run log/XML paths under `../outputs/agent/`, and rejects a direct run when
the project is open. Override the executable only when necessary:

```sh
SESSION_RPG_UNITY_BIN=/absolute/path/to/Unity Tools/Agent/unity.sh validate
```

For read-only validation or tests while the GUI Editor is open, first save and wait
for compilation/import to finish, then use an isolated copy:

```sh
Tools/Agent/unity.sh --isolated validate
Tools/Agent/unity.sh --isolated test-all
```

Isolation copies `Assets`, `Packages`, `ProjectSettings`, and the project-local
`Tools` contracts to a temporary project. Its changes are discarded. Therefore
`rebuild` and third-party configure
commands reject `--isolated`. Tests are serial; never run EditMode and PlayMode in
parallel against one project. PlayMode intentionally does not use `-nographics`.

The wrapper's effective Unity invocations are:

```text
Unity -batchmode -nographics -quit -projectPath <project> -executeMethod SessionRpg.Editor.Authoring.SessionRpgAuthoringGenerator.ValidateForCommandLine
Unity -batchmode -nographics -quit -projectPath <project> -executeMethod SessionRpg.Editor.Authoring.SessionRpgAuthoringGenerator.RebuildForCommandLine
Unity -batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode -testResults <outside-Assets.xml>
Unity -batchmode -projectPath <project> -runTests -testPlatform PlayMode -testResults <outside-Assets.xml>
Unity -batchmode -nographics -quit -projectPath <project> -buildTarget Android -executeMethod SessionRpg.Editor.Build.SessionRpgAndroidBuilder.BuildDevelopmentForCommandLine
```

Do not add `-quit` to test runs: Unity Test Framework owns their exit. A shell exit
code is not enough. The wrapper requires Unity exit code zero and a newly created
NUnit XML checked by `Tools/Agent/nunit_result.py` using Python 3's standard library.
Only `Passed` or `Skipped:Ignored` root results are accepted, with at least one
executed passing test, zero failed/inconclusive tests, and consistent summary and
leaf counts. Every non-passing leaf must be explicitly `Skipped` with the `Ignored`
label; ignored tests are reported separately with their reasons as **not run**,
never added to the pass count. Empty/all-ignored, malformed, unaccounted or failing
runs are rejected. The public CC0 defaults intentionally ignore five
licensed-character-only cases; review any different ignore set before handoff.

## When to rebuild

For the separate local server prototype, use `Tools/Agent/server.sh doctor`,
`restore --locked-mode`, `build`, and `test`; see [Server README](../Server/README.md)
and [server evidence](SERVER_FOUNDATION_EVIDENCE.md). Server-only changes outside
`Assets` do not require a Unity rebuild. Server HTTP tests require loopback socket
access. They do not establish multiplayer gameplay or replace Unity tests when
shared rules, presentation, or integration changes.

Rebuild after changes to authoring code, world/arena manifests, serialized
fields/references, environment or HUD composition, character hierarchy, animations,
generated materials/audio/VFX, ability icon membership/import configuration,
layer/build/URP settings, or source third-party rigs used by the generated
foundation. Core-only arithmetic, bot-only logic, docs, and tests do not require a
rebuild unless they changed a serialized contract.

The rebuild opens new empty scenes while authoring, replaces `Bootstrap.unity` and
every manifest-declared world chunk, updates generated catalogs, the combatant
prefab, generated assets, enabled build-scene order, and selected ProjectSettings.
It can erase unsaved Inspector edits. Do not run it speculatively.

## Source-art import recovery

Validate first. Only after a validator names an importer mismatch may you run one of:

```sh
Tools/Agent/unity.sh configure-quaternius
Tools/Agent/unity.sh configure-kaykit
Tools/Agent/unity.sh configure-fortress
Tools/Agent/unity.sh configure-arena-floor
Tools/Agent/unity.sh configure-hunter-art
Tools/Agent/unity.sh configure-hunter-production
Tools/Agent/unity.sh configure-mixamo-trial
Tools/Agent/unity.sh configure-mixamo-class-trials
Tools/Agent/unity.sh rebuild
Tools/Agent/unity.sh validate
```

`configure-hunter-production` is intentionally a no-op while the source-level
selection is not `ProductionV1`; it does not query or create the dormant production
folder. Selecting `ProductionV1` makes the FBX, three textures, rig, LOD, renderer,
and Android import contracts mandatory.

`configure-mixamo-trial` is a one-shot, direct-project command and rejects
`--isolated`. It configures only the verified model/texture copy under the
historical trial path; animation FBXs remain excluded. Follow it with one rebuild.
The selected licensed game base keeps the current generated controller and procedural Humanoid/AvatarIK
motion. The accepted V3 DCC motion is source-only evidence until a later accepted
FBX export and explicit Unity integration.

`configure-mixamo-class-trials` applies the same one-shot, direct-project boundary
to the selected Uriel Priest, Vampire Rogue, and Demon Mage game bases. It
configures only hash-locked staged models and audited extracted textures; it does
not admit Mixamo animation. Follow the command with one authored rebuild. The
generated class roots retain the existing project controllers and deterministic
combat timing. These sources may enter embedded Development or non-Development
game builds after validation; never redistribute their original or modified
Content Files standalone or through a public source package.

Before promoting any hero or companion source into `Assets/`, follow the DCC,
deformation, mobile-budget, and actual-camera gates in
`Docs/Art/PRODUCTION_CHARACTER_PIPELINE.md`.

These commands intentionally mutate tracked importer `.meta` files and reimport the
models. Never retry in a loop. Missing/invalid source files or licenses require
restoring the verified archive, not repeated configuration.

## Minimum verification by change type

| Change | Required automated evidence | Required manual evidence |
|---|---|---|
| Core rules/catalog/geometry | EditMode; allocation tests when hot path changes | Relevant arena/boss interaction |
| Application flow/bots | EditMode, deterministic soak if decisions change | Result/replay and affected bot behavior |
| Input/HUD/readiness | targeted PlayMode, then full PlayMode | mouse plus multi-touch-equivalent gestures; 16:9 and tall landscape |
| Animation/VFX/audio/character | validate, full PlayMode | every affected class/spec, hit/miss/cancel/interrupt/death where applicable |
| Arena authoring | rebuild, validate, EditMode geometry, full PlayMode | movement, LOS, Blink, Shadowstep, knockback, lava, camera |
| World manifest/chunk authoring | rebuild, validate, world EditMode and PlayMode streaming tests | traverse every changed seam/POI; confirm bounded residency, no visual gap, and clean exit/re-entry |
| Package/import/settings | rebuild if required, validate, both suites | cold open/import and affected build target |
| Documentation only | link/path/command review | none unless claims changed |

## Handoff record

Use this exact shape so the next agent can distinguish fact from assumption:

```text
Scope:
Source changed:
Generated changed:
Commands (fresh):
Automated result counts:
Manual checks:
Performance/device/network checks: passed | failed | not run
Known caveats:
Next safe action:
```

Historical files currently show different test counts because they were produced at
different revisions. They are context only. Never cite them without a fresh run.
