# Public repository preparation — 2026-10-11

## Scope and source policy

Clean public source export of the existing local Unity game and separate .NET
server. The original development project, licensed character files and Git history
were preserved; restricted content, caches, research/archive intermediates and
build products were omitted from the export rather than uploaded.

Public-only character-policy changes:

- `Assets/SessionRpg/Editor/Assets/HunterProductionAssetValidator.cs` and
  `PlayableClassTrialSourceSelection.cs`: select the existing CC0 Quaternius
  fallback for all four classes.
- `Assets/SessionRpg/Editor/Assets/MixamoPlayableClassTrialAssetValidator.cs`:
  inactive licensed sources are not required by the public fallback; selected
  licensed-source integrity remains a separate contract.
- `Assets/SessionRpg/Tests/EditMode/Editor/MixamoPlayableClassTrialContractTests.cs`:
  selection-aware licensed checks plus public fallback/policy regressions.
- README, AGENTS, retained rights notices, distribution guide and curated
  screenshots describe the public boundary. Repository hygiene and
  `Tools/Agent/check-public-source.py` check the candidate upload.
- `Tools/Agent/unity.sh`, `nunit_result.py` and `test_nunit_result.py`: validate
  complete NUnit counts and report intentional ignores separately, preserving
  nonzero Unity-exit rejection. The isolated-copy recipe also retains the masonry
  manifest consumed by Arena tests; the isolated workflow was not run this pass.

No combat rules, progression, online transport or deployment were added by this
publication work. See [public source distribution](PUBLIC_SOURCE_DISTRIBUTION.md).

## Generated assets

The supported authoring rebuild regenerated the public Bootstrap, Combatant,
catalog/presentation and generated art foundation with Quaternius character roots.
Required generated Unity assets and `.meta` files remain in the source checkout;
licensed raw and source-derived character payloads do not. Generated YAML was not
hand-edited. These public pixels differ from the historical README characters.

## Fresh commands and results

Commands run from the public project root with pinned Unity **6000.5.6f1** and
.NET SDK **10.0.400**. Unity jobs run serially, without a Burst-disable environment
override. The server used `SESSION_RPG_DOTNET_BIN` to select the pinned local SDK;
on another machine use the installed SDK or set that variable to its executable.
Unity log/XML paths below are
relative to `../outputs/agent/`, outside the repository; server `TestResults/`
is ignored. Raw local evidence is not included in the source publication.

| Command | Fresh result / artifact |
|---|---|
| `Tools/Agent/unity.sh rebuild` | Passed; `20261011-075752-rebuild-57231/unity.log`. |
| `Tools/Agent/unity.sh doctor` | Passed: pinned Editor available, project not open. |
| `Tools/Agent/unity.sh validate` | Passed; final run `20261011-080647-validate-63349/unity.log`. |
| Initial `Tools/Agent/unity.sh test-all` | Stopped after EditMode: XML reports 1,052 total, 1,047 passed, zero failed, five intentionally ignored inactive licensed-model cases; `20261011-080038-test-edit-59809/results.xml`. The old wrapper rejected the ignored root result; this was not a wrapper pass. |
| Final `Tools/Agent/unity.sh test-all` | Passed, shell exit zero. EditMode: 1,047 passed, zero failed/inconclusive, five ignored; `20261011-080657-test-edit-63339/results.xml`. PlayMode: 252 passed, zero failed/inconclusive/ignored; `20261011-080740-test-play-63339/results.xml`. |
| `python3 -m unittest discover -s Tools/Agent -p test_nunit_result.py` | 18 passed, zero failed. |
| `bash -n Tools/Agent/unity.sh` | Passed. |
| `Tools/Agent/server.sh doctor` | Passed: pinned local SDK available. |
| `Tools/Agent/server.sh restore --locked-mode` | Passed. |
| `Tools/Agent/server.sh build` | Passed; zero warnings, zero errors. |
| `Tools/Agent/server.sh test` | 87 passed, zero failed, zero skipped; `Server/SessionRpg.Server.Tests/TestResults/_10_2026-10-11_07_51_26_net10.0.trx`. |
| `bash Tools/Art/verify_sunbreak_sources.sh` | Passed: 18 maps, 41,996,162 bytes. |
| `python3 Tools/Agent/check-public-source.py` | Passed: 3,001 candidate files, zero findings. |
| `git diff --cached --check` | Passed; generated serialization and immutable upstream formatting preserved through targeted attributes. |

The initial EditMode ignores are three non-Hunter licensed-model cases plus two
Hunter licensed-model cases, deliberately inactive under CC0 selection. They are
not failed combat tests or evidence that optional licensed imports were tested.
Historical September test results do not validate this public fallback revision.
An intermediate rerun also produced passing EditMode/PlayMode XML, but its shell
exited nonzero after the wrapper was edited while active. The stable-wrapper
final run above supersedes that attempt; no scripts were edited during it.

The final staged audit found no restricted payloads or references to the 49
original Mixamo/derived-asset GUIDs, no credential/token/signed-URL or private-path
findings, and complete Unity file/folder metadata. No GitHub workflows were added.

## Manual and external checks

No fresh interactive mode/class/spec or aspect-ratio playthrough was performed
for publication. README images were inspected and preserved byte-for-byte from
the 2026-09-08 local macOS Development build: Arena/Fire and PvE/Discipline,
1920×1080. They are historical screenshots, not new public-build captures.

| Check | Publication-pass status |
|---|---|
| Allocation and performance profiling | Not run. |
| Android packaging/install | Not run. |
| Network/multiplayer | Not run; gameplay remains local. |
| Physical iQOO Neo11 / other device | Not run. |
| Public server deployment | Not run; local-only control plane, no deployment claim. |

## Caveats and next safe action

The clean export is ready for its initial public commit. A green source/build
check does not establish production art, touch comfort, sustained mobile quality,
stability or online gameplay. After publication, the safest next check is a clean
clone/import followed by an interactive Arena/PvE pass using the public CC0
characters; device-quality gates remain separate.
