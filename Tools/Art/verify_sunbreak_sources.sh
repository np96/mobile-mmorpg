#!/usr/bin/env bash
set -euo pipefail

# Read-only integrity check for the immutable POLYHAVEN-2026-002 delivery.
task_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
task_manifest="$task_root/Assets/ThirdParty/PolyHaven/SunbreakSurfaces/manifest.json"
task_count=0
task_bytes=0

jq -e '.schemaVersion == 1 and .intakeId == "POLYHAVEN-2026-002" and
  .license == "CC0-1.0" and .revision == 2 and (.assets | length) == 6 and
  ([.assets[].files[]] | length) == 18 and
  ([.assets[] | .widthPixels == .heightPixels and
    (.widthPixels == 2048 or .widthPixels == 1024)] | all)' \
  "$task_manifest" >/dev/null

while IFS=$'\t' read -r task_relative task_expected_bytes task_expected_sha task_expected_md5; do
  case "$task_relative" in
    Assets/ThirdParty/PolyHaven/SunbreakSurfaces/*/*.jpg) ;;
    *) printf 'Rejected source path: %s\n' "$task_relative" >&2; exit 1 ;;
  esac
  [[ "$task_relative" != *'..'* ]]
  task_path="$task_root/$task_relative"
  [[ -f "$task_path" && ! -L "$task_path" ]]
  task_actual_bytes="$(wc -c < "$task_path" | tr -d '[:space:]')"
  [[ "$task_actual_bytes" == "$task_expected_bytes" ]]
  [[ "$(shasum -a 256 "$task_path" | awk '{print $1}')" == "$task_expected_sha" ]]
  [[ "$(md5 -q "$task_path")" == "$task_expected_md5" ]]
  task_count=$((task_count + 1))
  task_bytes=$((task_bytes + task_actual_bytes))
done < <(jq -r '.assets[].files[] | [.path, .bytes, .sha256, .officialMd5] | @tsv' "$task_manifest")

[[ "$task_count" == 18 ]]
[[ "$task_bytes" == "$(jq -r '.sourceBytes' "$task_manifest")" ]]
task_license="$task_root/$(jq -r '.retainedLicense.path' "$task_manifest")"
[[ "$(shasum -a 256 "$task_license" | awk '{print $1}')" == "$(jq -r '.retainedLicense.sha256' "$task_manifest")" ]]
[[ "$(wc -c < "$task_license" | tr -d '[:space:]')" == "$(jq -r '.retainedLicense.bytes' "$task_manifest")" ]]

printf 'PASS: %s source maps, %s bytes, official MD5 + project SHA-256, retained CC0 license.\n' "$task_count" "$task_bytes"
