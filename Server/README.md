# Personal-world server prototype

This is the first **local development control plane** for persistent personal
worlds. It is separate from Unity and is not yet a playable online game.

Implemented:

- CLI-provisioned developer identities with expiring, hashed bearer credentials.
- Atomic, idempotent character + independently owned world creation in SQLite.
- Level 1 / zero XP bootstrap, using existing playable class/spec IDs from Core.
- Saved Ashen Frontier connectivity plan: seed, generator/content versions, and
  complete nodes/edges. Existing plans are read, not regenerated.
- Owner + one guest admission metadata, revocable one-use invitations, revisions,
  bounded session capacity, expiry, and owner closure.
- Standalone compilation of the actual Unity-independent `SessionRpg.Core`
  source, with a 30 Hz smoke test. Combat is not yet connected to this HTTP API.

Not implemented: real accounts, matchmaking, movement/combat transport, dedicated
combat ticking, Unity client integration, walkable generated terrain, region
unlock mechanics, earned XP, inventory, reward receipts, public deployment,
reconnection into an encounter, or level synchronization. The `lobby` state means
admitted participants, not an active game. No endpoint accepts claimed victories,
XP, levels, items, or world completion.

## Run locally

Prerequisite: the .NET SDK pinned in `global.json`. It is scoped to `Server/`;
Unity's packages and editor are unaffected. Set `SESSION_RPG_DOTNET_BIN` to an
absolute SDK executable if `dotnet` is not on PATH. The wrapper also recognizes a
project-local SDK at `build/server-tooling/dotnet/dotnet`. It never installs one
automatically. Dependencies use NuGet.org and committed package locks.

From the Unity project root:

```sh
Tools/Agent/server.sh doctor
Tools/Agent/server.sh restore --locked-mode
Tools/Agent/server.sh build
Tools/Agent/server.sh test
Tools/Agent/server.sh provision-player
Tools/Agent/server.sh run
```

`provision-player` prints a player ID, bearer token and expiry **once** to the
operator. Keep that output private. Provision a second player to exercise a guest.
Do not paste real tokens into source, tickets, URLs, or committed examples.

The server binds `127.0.0.1:5087`; the wrapper passes the mandatory `--dev-local`.
`--port 5088` chooses another local port. `--database /absolute/path/worlds.db`
chooses a database; use the same path for server and operator commands. The
default is ignored `Server/.data/personal-worlds.db`, independent of invocation
directory when using the wrapper.

Developer tokens expire after 24 hours. To recover access without replacing a
character/world, stop using the old token and rotate it locally:

```sh
Tools/Agent/server.sh rotate-credential --player PLAYER_GUID
```

Only the new secret works after rotation. Rotation does not terminate an existing
session, but every subsequent API request requires the new secret. This is a
machine-operator affordance, not a public password recovery API.

## API walkthrough

All `/v1` endpoints require `Authorization: Bearer <developer-token>`.
`GET /health` is public and explicitly reports `gameplay: false`.
Responses containing player/session data are `Cache-Control: no-store`.

1. `GET /v1/me` reads the authenticated player's profile.
2. `POST /v1/characters` with a new canonical lowercase GUID in
   `Idempotency-Key` and JSON such as
   `{"name":"Ember","classId":3,"specializationId":2}` creates a Mage/Fire
   character plus its owner's personal world. Keep the key when retrying the
   same request. Reuse with a different normalized request returns `409`.
3. Use returned IDs in `POST /v1/sessions`:
   `{"characterId":"...","personalWorldId":"...","regionInstanceId":"..."}`.
   Only that world's owner can start it with their own character.
4. Owner sends `POST /v1/sessions/{sessionId}/invites` with
   `{"expectedRevision":1}` (use the actual current revision). The response
   contains a secret invite and the new revision. Issuing another invite revokes
   the previous one.
5. Guest authenticates as their own developer player and sends
   `POST /v1/sessions/join` with
   `{"characterId":"their-character-id","inviteToken":"..."}`.
6. Both members can `GET /v1/sessions/{sessionId}` to read the host's saved plan.
   Guests cannot read the host's private `/v1/worlds/{worldId}` endpoint or change
   host-world state. Their own `GET /v1/me` remains unchanged.
7. A member sends `POST /v1/sessions/{sessionId}/leave` with the current
   `expectedRevision`. Guest departure frees its slot; owner departure closes
   the session. There is no host migration.

Class/spec IDs are validated against the linked Core catalog. All seven current
playable specs are supported; Boss IDs are explicitly rejected. Unknown JSON
fields are rejected, so client-supplied progress/ownership fields are not ignored
as if accepted. Missing credentials return `401`, owner-only operations by a
guest `403`, inaccessible IDs `404`, stale revisions and conflicts `409`.

## Persistence and lifecycle contract

- Database schema version 1. SQLite immediate write transactions, full
  synchronization, foreign keys, and a five-second busy timeout. Character,
  world, and creation retry receipt commit together.
- This prototype allows one character and one owned world per developer player.
  Separate entities preserve the distinction; this is not a final multi-character
  ownership decision or a migration strategy.
- Initial character XP/level and world revisions are stored, not earned or
  advanced yet. Training's session-level curve is not used.
- Saved regions contain an **undirected graph**, not coordinates, scenes,
  walkable collision, or implemented encounter templates. Stable server-local
  template IDs are distinct from Core authored `WorldId`/`ZoneId`.
- A plan has two guardian routes, 3–5 ordinary camps, an optional elite branch,
  refuge, crossroads and shortcut. Generation is deterministic/versioned; the
  complete saved output is the source of truth on return.
- Sessions last at most one hour; invites last at most five minutes and are
  stored hashed in memory. Only one active session per player, two members per
  session, and 128 retained sessions. Closed-session readback is best-effort and
  may be evicted when capacity is needed; active sessions are never evicted.
- Sessions and invites disappear on process restart. Characters, worlds and
  creation receipts survive. There is no encounter-resume claim.
- Unsupported schema, corrupt SQLite data, or invalid saved JSON fails closed.
  No automatic save reset or migration is performed. Preserve the file and
  investigate; do not delete valuable data to make startup succeed.
- Back up with the server stopped, retaining the database and any `-wal`/`-shm`
  files together. Live backup/restore drills and machine-crash durability testing
  remain unimplemented/unverified.

This API allocates for HTTP/SQLite/JSON and is outside the simulation tick. No
allocation-free server claim is made. Do not put synchronous database work in a
future 30 Hz combat loop.

## Security boundary

Explicit local-only CLI, loopback binding independent of inherited endpoint
settings, no HTTP account creation, no CORS allowance, random 256-bit credentials
and invite secrets, hashed secrets at rest, 16 KiB request bodies, and a shared
300-request/minute development limit. Expected revisions and a lock serialize
membership changes. Character/world authorization is derived from the bearer
identity, never an owner ID submitted by the client.

This is **not production security**: local HTTP has no TLS; anyone with database
or machine-operator access can modify developer accounts. Do not proxy, tunnel,
expose or deploy it. Multiple server processes against one DB would have
independent in-memory sessions; operate only one HTTP process per database.
SQLite is a local persistence choice, not a horizontal-scaling commitment.

## Next implementation slice

1. Run one server-owned, bounded encounter using existing deterministic Core at
   30 Hz. Map authenticated session members to stable combat participant IDs;
   accept only sequenced intentions and publish versioned snapshots/events.
2. Add trusted, unique encounter-result receipts and atomic character rewards /
   host milestone updates. First agree the permanent leveling/reward rules; a
   client or listen-host victory assertion must never grant permanent rewards.
3. Add Unity admission/transport adapters and a validated authored region view;
   exercise two real clients, disconnect/rejoin, save/restart, and host closure.
4. Only then choose production identity, hosting, backups, migration tooling,
   multi-process session routing, telemetry and security/load acceptance gates.

See [the concept](../Docs/PERSONAL_WORLD_PVE_CONCEPT.md) and
[fresh evidence](../Docs/SERVER_FOUNDATION_EVIDENCE.md). The separate existing
[Unity demo boundary](../Docs/DEMO_READINESS.md) still applies.

Dependency references: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy),
[SQLite transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions),
[ASP.NET Core authentication](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-10.0).
