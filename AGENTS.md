# Session RPG Agent Contract

These instructions apply to this complete repository: the Unity project,
standalone `Server/`, tooling and documentation. Run commands from the repository
root unless a document explicitly says otherwise.

## Start here

1. Read `Docs/AGENT_WORKFLOW.md`.
   Also read `Docs/PUBLIC_SOURCE_DISTRIBUTION.md` before changing character source
   selection, publishing files, or interpreting historical character screenshots.
2. Read `DESIGN.md` for product rules and `Docs/DEMO_READINESS.md` for the honest
   current boundary. `ARCHITECTURE.md` includes target architecture as well as
   implemented systems; do not infer that planned networking or telemetry exists.
3. Run `Tools/Agent/unity.sh doctor` before invoking Unity from a shell.
4. Inspect the current diff and preserve unrelated user changes. Classify the
   change before editing: Core, Application, Unity presentation/input, authoring,
   third-party import, package, server, repository tooling, or documentation.

## Non-negotiable boundaries

- `SessionRpg.Core` and `SessionRpg.Application` have no Unity references.
- Combat outcomes are deterministic Core state. Unity physics, animation, VFX,
  audio, camera, and HUD may present outcomes but may not decide them.
- The active local simulation is 30 Hz. Do not silently restore assumptions from
  the legacy 50 Hz Ringfall project.
- Stable identifiers cross layers. Do not replace `AbilityId`, `AuraId`, class,
  specialization, participant, or team IDs with UI indices or object names.
- Keep the warmed-up simulation tick and steady gameplay frame allocation-free.
  Reuse snapshots only during their documented tick; do not retain reused buffers.
- Add or update tests in the same change as rules, input semantics, generated-scene
  contracts, or pool sizes.

## Authored Unity assets

Under `Assets/SessionRpg/`, `Scenes/Bootstrap.unity`, `Prefabs/Combatant.prefab`,
`Content/Worlds/`, `Content/Arenas/`, and most assets below `Art/Animations`,
`Art/Audio`, `Art/Characters`, `Art/Environment`, `Art/Materials`, `Art/Vfx`, and
`Settings` are generated and tracked. Their source is
`Assets/SessionRpg/Editor/Authoring/*.cs` plus source art. Do not edit those generated
files in the Inspector or as YAML; the next rebuild will overwrite the change.

Arena membership is declared in `Tools/Agent/arena-variants.json`; the generator
consumes that manifest and rejects unknown layout/recipe pairs. Never add a prefab
directly to the generated arena catalog. A manifest entry is authoring support, not
proof that runtime arena selection or final-quality art exists.

World and chunk membership is declared by `Tools/Agent/world-zones.json` and
`Tools/Agent/Worlds/**/zone.json`. Follow `Docs/WORLD_AUTHORING.md` before changing
either. Presentation chunks do not own collision, combat, actor lifetime or XP.

`Assets/SessionRpg/Art/UI/AbilityIcons/*.png` and `Assets/ThirdParty/**` are source
inputs, not generator output. Import settings live in their tracked `.meta` files.
See the complete matrix in `Docs/AGENT_WORKFLOW.md`.

Use:

```sh
Tools/Agent/unity.sh validate
Tools/Agent/unity.sh rebuild
Tools/Agent/unity.sh test-all
```

Run `rebuild` only when authoring inputs or serialized composition changed. It
replaces the Bootstrap scene, rewrites generated assets, and updates selected
ProjectSettings. It must run against the real project with the Unity Editor closed.
Validation and tests may use `--isolated` after open-Editor work has been saved.

Do not repeatedly run a third-party configurator after a failure. Read the first
validator error, correct the named source/import issue, run the one relevant
configuration command once, then validate.

## Server and repository hygiene

- The standalone server links actual Unity-independent Core source. It is a
  local development control plane, not connected Unity multiplayer or progression.
  Read `Server/README.md` before changing it. Use `Tools/Agent/server.sh doctor`,
  `restore --locked-mode`, `build`, and `test`; server-only changes do not require
  a Unity rebuild. Never expose the local HTTP service publicly.
- Never commit credentials, developer tokens, invites, `.env` files, signing keys,
  local SQLite databases, or private machine configuration. Inspect staged paths
  before committing or uploading; do not print secret values during checks.
- Keep Unity caches, IDE state, build products, raw logs, test XML and disposable
  capture iterations out of source control. Curated README screenshots belong in
  `Docs/Screenshots/`; label their capture date, mode, platform and build type.
- Generated Unity assets are required source-checkout content, not disposable
  intermediates. Keep accepted source masters, hashes, manifests, license records
  and every required `.meta`. Check references before trimming any source art.
- Respect `THIRD_PARTY_NOTICES.md` and the source-of-truth matrix. Selected Adobe
  Mixamo files may be embedded in the game but must not be redistributed as
  standalone original/modified Content Files or in a public source package.
  A private repository is not permission to republish licensed content.
- Before a public commit or push, run `python3 Tools/Agent/check-public-source.py`.
  It checks candidate paths, required files, asset metadata, file sizes and
  recognizable secret patterns without printing secret values. It supplements,
  but does not replace, a staged diff and license/provenance review.
- The public distribution uses the existing CC0 Quaternius fallback for all four
  classes and its regenerated foundation. Do not restore licensed files or
  source-derived meshes/materials to the public repository. Optional private
  customization requires independent acquisition, provenance/hash/import review,
  an authored rebuild and fresh tests, not just a source-selector change.
- Documentation-only changes require link/path/command review, not a speculative
  Unity rebuild. Never turn historical screenshots, test counts or desktop FPS
  into claims about the current revision, a physical phone or online gameplay.

## Cross-cutting ability changes

Adding or renumbering an ability requires synchronized review of the enum and
`AbilityCatalog.StorageCapacity`, definitions/effect slices, specialization kit and
alternate binding, bot use, readiness/blocker text, presentation catalog, spell
visual language, icon PNG/catalog list, animation/SFX routing, HUD, tests, and the
generated foundation. `AbilityId` values are dense storage keys; never insert or
renumber casually. Apply the same capacity discipline to `AuraId`.

## Handoff minimum

- Changed source files and reason.
- Generated files intentionally changed, or `none`.
- Exact commands run and fresh pass/fail counts.
- Manual modes/classes/specs and aspect ratios checked.
- Allocation, performance, Android, network, and physical-device checks explicitly
  marked `passed`, `failed`, or `not run`.
- Remaining caveats and the safest next action.
