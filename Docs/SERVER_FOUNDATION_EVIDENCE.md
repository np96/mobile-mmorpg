# Local server foundation — 2026-09-04

Scope:

Standalone .NET 10 / SQLite **development control plane** for the approved
personal-world PvE direction. Classification: standalone server, tooling, tests,
documentation. This is not networked gameplay or a persistent-leveling demo in
Unity. User confirmed C#/.NET and local development first; no cloud deployment.

Source changed:

- `Server/SessionRpg.Core/SessionRpg.Core.csproj`: compiles existing Core sources
  as a standalone Unity-free assembly without copying or changing the rules.
- `Server/SessionRpg.Server/{Contracts,GameStore,RegionPlanGenerator,SessionService,DevelopmentServer,Program}.cs`:
  persistent IDs/data, transactional storage, saved graph generation, owner/guest
  admission, authenticated local HTTP, operator provisioning/credential rotation.
- `Server/SessionRpg.Server.Tests/*.cs`: six test classes covering store, region
  generator, session admission, HTTP API, forced-local binding, and shared Core.
- SDK/project/dependency config and package locks under `Server/`, plus
  `Tools/Agent/server.sh`: reproducible standalone build/test/run workflow.
- `Server/README.md`, `Docs/PERSONAL_WORLD_PVE_CONCEPT.md`,
  `Docs/AGENT_WORKFLOW.md`, and this evidence record: operating instructions,
  contract, current boundary, and next slice. Existing unrelated edits preserved.

Generated changed:

None in Unity. NuGet package lockfiles were generated intentionally for the new
standalone projects. SDK/cache, binaries, TRX results and disposable CLI smoke
database are ignored build/test outputs, not source assets. No Unity rebuild ran.

Commands (fresh):

```sh
Tools/Agent/unity.sh doctor
Tools/Agent/server.sh doctor
Tools/Agent/server.sh restore
Tools/Agent/server.sh restore --locked-mode
Tools/Agent/server.sh build
Tools/Agent/server.sh test
bash -n Tools/Agent/server.sh
git diff --check -- Server Tools/Agent/server.sh Docs/PERSONAL_WORLD_PVE_CONCEPT.md
```

SDK 10.0.400 / runtime 10.0.11 was downloaded from Microsoft into ignored
`build/server-tooling/dotnet`; archive SHA-512 matched Microsoft's release metadata.
No global SDK installation. Initial restore was denied DNS by the sandbox;
scoped network approval allowed NuGet restore. Locked restore then passed cached.

The default shared build-server process hung on restricted IPC and was terminated;
the wrapper now disables build servers/shared compilation. An intermediate build
caught six xUnit analyzer errors; the assertions were corrected. The first test
attempt aborted before executing tests because the runner needs loopback sockets.
Approved local socket access allowed the complete suites to execute. These setup
failures are not represented as passing test runs.

Automated result counts:

- Final build: **0 errors, 0 warnings**.
- Final test run: **87 passed, 0 failed, 0 skipped**. Previous complete run also
  passed 87/87. TRX evidence:
  `Server/SessionRpg.Server.Tests/TestResults/_10_2026-09-04_13_21_35_net10.0.trx`.
- Store: 39 cases; session service: 28; region generation: 9; HTTP API: 6;
  hostile inherited-listener configuration: 4; shared Core: 1.
- Coverage includes all seven playable class/spec combinations, persistence after
  store/app recreation, atomic failure rollback, simultaneous creation/admission,
  exact retry receipts, save corruption/schema refusal, credential rotation,
  expiry/revocation, ownership, capacity, and defensive snapshot copies.
- Real HTTP tests use ephemeral `127.0.0.1` ports. They reject unauthorized access,
  client-supplied progress/owner fields, malformed and oversized JSON, and test
  owner/guest/closure/restart flows. Inherited external endpoint settings cannot
  add or replace the loopback listener.
- Shell syntax check passed. Scoped whitespace checks passed. Whole-worktree
  `git diff --check` reports pre-existing generated Unity YAML whitespace;
  those unrelated files were left unchanged.

Manual checks:

CLI smoke: provisioned a disposable developer player, rotated its credential,
checked same player/different 64-character secret without printing tokens; then
started via `Tools/Agent/server.sh run` using the disposable DB at
`build/server-smoke/cli-worlds.db` and port 5088. `curl --fail --silent --show-error
http://127.0.0.1:5088/health` returned
`{"status":"ok","mode":"local-development","gameplay":false}`. Server was stopped
afterward. The CLI run reports Kestrel's expected warning that its explicit
listener overrides the identical hosting URL; it still binds only loopback.

Unity modes/classes/specs and aspect ratios: **not run**, no presentation/input or
Unity source changes. These server tests do not replace Unity EditMode/PlayMode
acceptance when subsequent integration changes those layers.

Performance/device/network checks:

- Allocation/hot-path performance: **not run**; HTTP/SQLite work allocates and is
  outside the simulation loop. No new allocation-free claim.
- Android build: **not run**.
- Physical device: **not run**.
- Local HTTP network/admission checks: **passed** (automated and CLI health smoke).
- Two-client gameplay, WAN, latency, packet loss, replication and reconnection:
  **not run / not implemented**.
- Sustained load, process-kill recovery, live backup/restore and disk failure:
  **not run**. Store rollback and ordinary restart are narrower automated checks.

Known caveats:

One developer character/world per player is a prototype cap, not a settled
multi-character policy. Saved content is a connectivity plan, not terrain. XP is
persisted only at bootstrap level 1 / zero XP. Sessions and invites are in-memory
and disappear on restart; no combat or reward grant runs. One HTTP process per
database; local operator trust, no TLS, no production account/recovery service.
Do not expose this server through a tunnel, proxy or public interface. See
[the complete contract](../Server/README.md).

Next safe action:

Connect one authenticated session to a server-owned deterministic 30 Hz encounter,
with stable participant mapping, sequenced intents and versioned snapshots. Then
agree permanent leveling rules and add trusted idempotent reward receipts before
connecting Unity clients. Never grant permanent progress from a client-reported
boss kill.
