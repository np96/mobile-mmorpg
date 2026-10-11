#!/usr/bin/env python3
"""Check the public repository's candidate paths without displaying secret data."""
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
DENIED = (
    "Assets/ThirdParty/Trials/AdobeMixamo/",
    "Assets/ThirdParty/Trials/AdobeMixamo.meta",
    "SourceArt/Trials/AdobeMixamo/",
    "SourceArt/Archive/",
    "SourceArt/Blender/Characters/", "SourceArt/ThirdParty/",
    "SourceArt/Environment/SunbreakVegetationV1/ClusterBakes/",
    "Library/", "Temp/", "Logs/", "UserSettings/", ".codex/", "build/",
)
TEXT_EXTENSIONS = {".cs", ".py", ".sh", ".json", ".md", ".yml", ".yaml", ".toml", ".xml", ".csproj", ".config"}
SECRET = re.compile(
    r"gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|"
    r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|AKIA[0-9A-Z]{16}|"
    r"[?&](?:X-Amz-Signature|X-Goog-Signature|access_token|Signature)=[^\s\"<>]{12,}"
)
REQUIRED = (
    "AGENTS.md", "README.md", "Docs/Screenshots/arena.png",
    "Docs/Screenshots/pve-leveling.png", "Packages/packages-lock.json",
    "Server/SessionRpg.Core/SessionRpg.Core.csproj",
    "Server/SessionRpg.Server/SessionRpg.Server.csproj",
    "Server/SessionRpg.Server.Tests/SessionRpg.Server.Tests.csproj",
    "Assets/SessionRpg/Scenes/Bootstrap.unity",
    "Assets/SessionRpg/Prefabs/Combatant.prefab",
)


def main():
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=ROOT, check=True, capture_output=True,
    )
    names = sorted(set(result.stdout.decode().split("\0")) - {""})
    name_set = set(names)
    failures = []
    total = 0
    for name in names:
        path = ROOT / name
        if not path.is_file():
            failures.append((name, "missing candidate file"))
            continue
        if any(name.startswith(prefix) for prefix in DENIED):
            failures.append((name, "excluded publication path"))
        if name.startswith("Assets/InitTestScene") and (name.endswith(".unity") or name.endswith(".unity.meta")):
            failures.append((name, "temporary Unity test scene"))
        if name.startswith("Assets/SessionRpg/Art/Characters/") and "mixamo" in path.name.lower():
            failures.append((name, "restricted derived character asset"))
        if path.suffix.lower() in {".apk", ".aab", ".keystore", ".jks", ".p12", ".pfx", ".db", ".sqlite"} or (path.name.startswith(".env") and path.name != ".env.example"):
            failures.append((name, "local build, credential or database"))
        size = path.stat().st_size
        total += size
        if size >= 100 * 1024 * 1024:
            failures.append((name, "exceeds GitHub file limit"))
        if name.startswith("Assets/") and not name.endswith(".meta"):
            if name + ".meta" not in name_set:
                failures.append((name, "Unity asset missing tracked metadata"))
        if path.suffix.lower() in TEXT_EXTENSIONS and SECRET.search(path.read_text(errors="replace")):
            failures.append((name, "potential secret; inspect locally, never print value"))
    for name in REQUIRED:
        if name not in name_set:
            failures.append((name, "required checkout file excluded"))
    prefab = ROOT / "Assets/SessionRpg/Prefabs/Combatant.prefab"
    if prefab.exists():
        prefab_text = prefab.read_text()
        if re.search(r"(?:m_Name:|value:)\s*(?:Hunter|Priest|Rogue|Mage)Model_Mixamo", prefab_text):
            failures.append((str(prefab.relative_to(ROOT)), "stale licensed character roots; rebuild CC0 foundation"))
        for role in ("Hunter", "Priest", "Rogue", "Mage"):
            if role + "Model_Quaternius" not in prefab_text:
                failures.append((str(prefab.relative_to(ROOT)), role + " CC0 root missing"))
    for name, reason in failures:
        print(f"FAIL {name}: {reason}")
    print(f"Checked {len(names)} candidate files, {total:,} bytes; {len(failures)} findings.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
