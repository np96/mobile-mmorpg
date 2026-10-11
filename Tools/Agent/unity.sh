#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
PROJECT_DIR="$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)"
REPOSITORY_DIR="$(CDPATH= cd -- "$PROJECT_DIR/.." && pwd)"
VERSION_FILE="$PROJECT_DIR/ProjectSettings/ProjectVersion.txt"
ARTIFACT_DIR="${SESSION_RPG_ARTIFACT_DIR:-$REPOSITORY_DIR/outputs/agent}"
TEMPORARY_BASE="${TMPDIR:-/tmp}"
TEMPORARY_BASE="${TEMPORARY_BASE%/}"

COMMAND=""
FILTER=""
ISOLATED=0
WORK_PROJECT="$PROJECT_DIR"
ISOLATED_ROOT=""

usage()
{
    cat <<'EOF'
Usage: Tools/Agent/unity.sh [--isolated] <command> [--filter <test-filter>]

Commands:
  doctor                 Print resolved paths/version and Editor-lock state.
  validate               Validate tracked third-party and authored foundation.
  rebuild                Regenerate tracked scene/prefab/art/settings outputs.
  test-edit              Run EditMode tests.
  test-play              Run PlayMode tests.
  test-all               Run EditMode, then PlayMode tests serially.
  build-android          Build the development APK.
  build-macos            Build the watermark-free macOS visual capture player.
  install-multiplayer    Install/pin unified Multiplayer Services through UPM.
  configure-quaternius   One-shot tracked Quaternius importer configuration.
  configure-kaykit       One-shot tracked KayKit importer configuration.
  configure-fortress     One-shot original fortress FBX importer configuration.
  configure-arena-floor  One-shot original arena-floor FBX importer configuration.
  configure-hunter-art   One-shot original Hunter albedo importer configuration.
  configure-hunter-production
                         One-shot selected Production Hunter importer configuration.
  configure-mixamo-trial One-shot licensed Mixamo Erika game-base importer configuration.
  configure-mixamo-class-trials
                         One-shot licensed Mixamo Priest/Rogue/Mage game-base importer configuration.
  inspect-mixamo-class-trials
                         Log staged Priest/Rogue/Mage renderer/material structure before locking a new source.

Environment:
  SESSION_RPG_UNITY_BIN       Absolute Unity executable override.
  SESSION_RPG_ARTIFACT_DIR    Log/XML/build-copy directory outside Assets.
  SESSION_RPG_KEEP_ISOLATED=1 Keep a temporary isolated project for diagnosis.
EOF
}

fail()
{
    printf 'error: %s\n' "$*" >&2
    exit 1
}

while [[ $# -gt 0 ]]
do
    case "$1" in
        --isolated)
            ISOLATED=1
            shift
            ;;
        --filter)
            [[ $# -ge 2 ]] || fail "--filter requires a value"
            FILTER="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            [[ -z "$COMMAND" ]] || fail "unexpected argument '$1'"
            COMMAND="$1"
            shift
            ;;
    esac
done

[[ -n "$COMMAND" ]] || { usage; exit 2; }
[[ -f "$VERSION_FILE" ]] || fail "missing $VERSION_FILE"

UNITY_VERSION="$(awk '/^m_EditorVersion:/{print $2; exit}' "$VERSION_FILE")"
[[ -n "$UNITY_VERSION" ]] || fail "could not read m_EditorVersion from $VERSION_FILE"

DEFAULT_UNITY_BIN="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
UNITY_BIN="${SESSION_RPG_UNITY_BIN:-$DEFAULT_UNITY_BIN}"

editor_pid()
{
    local instance_file="$PROJECT_DIR/Library/EditorInstance.json"
    [[ -f "$instance_file" ]] || return 1
    sed -n 's/.*"process_id"[[:space:]]*:[[:space:]]*\([0-9][0-9]*\).*/\1/p' \
        "$instance_file" | head -n 1
}

project_is_open()
{
    local pid
    pid="$(editor_pid || true)"
    [[ -n "$pid" ]] || return 1
    kill -0 "$pid" 2>/dev/null
}

cleanup()
{
    [[ -n "$ISOLATED_ROOT" ]] || return 0
    if [[ "${SESSION_RPG_KEEP_ISOLATED:-0}" == "1" ]]
    then
        printf 'kept isolated project: %s\n' "$ISOLATED_ROOT"
        return 0
    fi

    case "$ISOLATED_ROOT" in
        "$TEMPORARY_BASE"/session-rpg-unity.*|/tmp/session-rpg-unity.*|/private/tmp/session-rpg-unity.*)
            rm -rf -- "$ISOLATED_ROOT"
            ;;
        *)
            printf 'warning: refusing to clean unexpected path %s\n' "$ISOLATED_ROOT" >&2
            ;;
    esac
}

make_isolated_project()
{
    ISOLATED_ROOT="$(mktemp -d "$TEMPORARY_BASE/session-rpg-unity.XXXXXX")"
    WORK_PROJECT="$ISOLATED_ROOT/game-rpg"
    mkdir -p "$WORK_PROJECT"
    cp -R "$PROJECT_DIR/Assets" "$WORK_PROJECT/Assets"
    cp -R "$PROJECT_DIR/Packages" "$WORK_PROJECT/Packages"
    cp -R "$PROJECT_DIR/ProjectSettings" "$WORK_PROJECT/ProjectSettings"
    cp -R "$PROJECT_DIR/Tools" "$WORK_PROJECT/Tools"
    # The measured vegetation authoring consumes hash-locked, non-runtime JSON
    # mesh buffers. Keep one source of truth without importing these large text
    # files into player assets or copying unrelated licensed DCC archives.
    mkdir -p "$WORK_PROJECT/SourceArt/Environment"
    cp -R "$PROJECT_DIR/SourceArt/Environment/SunbreakVegetationV1" \
        "$WORK_PROJECT/SourceArt/Environment/SunbreakVegetationV1"
    # Arena content tests also consume this non-runtime source provenance record.
    mkdir -p "$WORK_PROJECT/SourceArt/Blender/Environment/Textures"
    cp "$PROJECT_DIR/SourceArt/Blender/Environment/Textures/FortressMasonryPbr.manifest.json" \
        "$WORK_PROJECT/SourceArt/Blender/Environment/Textures/FortressMasonryPbr.manifest.json"
    printf 'isolated project: %s\n' "$WORK_PROJECT"
}

timestamp()
{
    date '+%Y%m%d-%H%M%S'
}

run_unity()
{
    local label="$1"
    shift
    local run_dir="$ARTIFACT_DIR/$(timestamp)-$label-$$"
    local log_file="$run_dir/unity.log"
    mkdir -p "$run_dir"
    printf 'log: %s\n' "$log_file"
    "$UNITY_BIN" "$@" -logFile "$log_file"
}

run_method()
{
    local label="$1"
    local method="$2"
    shift 2
    run_unity "$label" \
        -batchmode \
        -nographics \
        -quit \
        -projectPath "$WORK_PROJECT" \
        "$@" \
        -executeMethod "$method"
}

run_tests()
{
    local platform="$1"
    local label
    local run_dir
    local result_file
    local log_file

    if [[ "$platform" == "EditMode" ]]
    then
        label="test-edit"
    else
        label="test-play"
    fi

    run_dir="$ARTIFACT_DIR/$(timestamp)-$label-$$"
    result_file="$run_dir/results.xml"
    log_file="$run_dir/unity.log"
    mkdir -p "$run_dir"
    printf 'log: %s\nexpected results: %s\n' "$log_file" "$result_file"

    set -- -batchmode
    if [[ "$platform" == "EditMode" ]]
    then
        set -- "$@" -nographics
    fi

    set -- \
        "$@" \
        -projectPath "$WORK_PROJECT" \
        -runTests \
        -testPlatform "$platform" \
        -testResults "$result_file"
    if [[ -n "$FILTER" ]]
    then
        set -- "$@" -testFilter "$FILTER"
    fi

    local unity_status
    if "$UNITY_BIN" "$@" -logFile "$log_file"
    then
        unity_status=0
    else
        unity_status=$?
    fi

    [[ -s "$result_file" ]] || \
        fail "$platform Unity exited with code $unity_status before producing NUnit XML; see $log_file"
    command -v python3 >/dev/null 2>&1 || fail "python3 is required to validate NUnit result counts"
    local summary
    if ! summary="$(python3 "$SCRIPT_DIR/nunit_result.py" "$result_file")"
    then
        fail "$platform did not report a complete passing/explicitly ignored test run; Unity exit code $unity_status; see $result_file"
    fi
    [[ "$unity_status" -eq 0 ]] || \
        fail "$platform produced passing XML but Unity exited with code $unity_status; see $log_file"

    printf '%s completed: %s\n' "$platform" "$summary"
}

if [[ "$COMMAND" == "doctor" ]]
then
    printf 'project: %s\n' "$PROJECT_DIR"
    printf 'unity version: %s\n' "$UNITY_VERSION"
    printf 'unity executable: %s\n' "$UNITY_BIN"
    printf 'artifacts: %s\n' "$ARTIFACT_DIR"
    if project_is_open
    then
        printf 'editor: OPEN (pid %s); use --isolated for saved validation/tests\n' \
            "$(editor_pid)"
    else
        printf 'editor: not detected as open\n'
    fi
    [[ -x "$UNITY_BIN" ]] || fail "Unity executable is not available/executable"
    exit 0
fi

[[ -x "$UNITY_BIN" ]] || fail "Unity executable is not available: $UNITY_BIN"

case "$COMMAND" in
    validate|test-edit|test-play|test-all|build-android|build-macos)
        ;;
    rebuild|install-multiplayer|configure-quaternius|configure-kaykit|configure-fortress|configure-arena-floor|configure-hunter-art|configure-hunter-production|configure-mixamo-trial|configure-mixamo-class-trials|inspect-mixamo-class-trials)
        [[ "$ISOLATED" -eq 0 ]] || \
            fail "$COMMAND writes tracked assets; isolated changes would be discarded"
        ;;
    *)
        usage
        fail "unknown command '$COMMAND'"
        ;;
esac

if [[ -n "$FILTER" && "$COMMAND" != "test-edit" && \
      "$COMMAND" != "test-play" && "$COMMAND" != "test-all" ]]
then
    fail "--filter is valid only for test-edit, test-play, or test-all"
fi

if [[ "$ISOLATED" -eq 1 ]]
then
    trap cleanup EXIT
    make_isolated_project
elif project_is_open
then
    fail "Unity already has this project open (pid $(editor_pid)); close it or use --isolated for saved validation/tests"
fi

case "$COMMAND" in
    validate)
        run_method \
            validate \
            SessionRpg.Editor.Authoring.SessionRpgAuthoringGenerator.ValidateForCommandLine
        ;;
    rebuild)
        run_method \
            rebuild \
            SessionRpg.Editor.Authoring.SessionRpgAuthoringGenerator.RebuildForCommandLine
        ;;
    install-multiplayer)
        run_method \
            install-multiplayer \
            SessionRpg.Editor.Packages.MultiplayerPackageInstaller.InstallForCommandLine
        ;;
    test-edit)
        run_tests EditMode
        ;;
    test-play)
        run_tests PlayMode
        ;;
    test-all)
        run_tests EditMode
        run_tests PlayMode
        ;;
    build-android)
        run_method \
            build-android \
            SessionRpg.Editor.Build.SessionRpgAndroidBuilder.BuildDevelopmentForCommandLine \
            -buildTarget Android
        if [[ "$ISOLATED" -eq 1 ]]
        then
            mkdir -p "$ARTIFACT_DIR/builds"
            android_copy="$ARTIFACT_DIR/builds/$(timestamp)-SessionRpg-dev.apk"
            cp "$ISOLATED_ROOT/outputs/SessionRpg-dev.apk" "$android_copy"
            printf 'build: %s\n' "$android_copy"
        else
            printf 'build: %s\n' "$REPOSITORY_DIR/outputs/SessionRpg-dev.apk"
        fi
        ;;
    build-macos)
        run_method \
            build-macos \
            SessionRpg.Editor.Build.SessionRpgMacCaptureBuilder.BuildDevelopmentCaptureForCommandLine \
            -buildTarget StandaloneOSX
        if [[ "$ISOLATED" -eq 1 ]]
        then
            mkdir -p "$ARTIFACT_DIR/builds"
            mac_copy="$ARTIFACT_DIR/builds/$(timestamp)-SessionRpg-mac-capture.app"
            cp -R "$ISOLATED_ROOT/outputs/SessionRpg-mac-capture.app" "$mac_copy"
            printf 'build: %s\n' "$mac_copy"
        else
            printf 'build: %s\n' "$REPOSITORY_DIR/outputs/SessionRpg-mac-capture.app"
        fi
        ;;
    configure-quaternius)
        run_method \
            configure-quaternius \
            SessionRpg.Editor.Assets.QuaterniusAssetValidator.ConfigureForCommandLine
        ;;
    configure-kaykit)
        run_method \
            configure-kaykit \
            SessionRpg.Editor.Assets.KayKitAssetValidator.ConfigureRigCompatibilityForCommandLine
        ;;
    configure-fortress)
        run_method \
            configure-fortress \
            SessionRpg.Editor.Assets.FortressSourceArtValidator.ConfigureForCommandLine
        ;;
    configure-arena-floor)
        run_method \
            configure-arena-floor \
            SessionRpg.Editor.Assets.ArenaFloorSourceArtValidator.ConfigureForCommandLine
        ;;
    configure-hunter-art)
        run_method \
            configure-hunter-art \
            SessionRpg.Editor.Assets.HunterOutfitAlbedoValidator.ConfigureForCommandLine
        ;;
    configure-hunter-production)
        run_method \
            configure-hunter-production \
            SessionRpg.Editor.Assets.HunterProductionAssetValidator.ConfigureForCommandLine
        ;;
    configure-mixamo-trial)
        run_method \
            configure-mixamo-trial \
            SessionRpg.Editor.Assets.MixamoErikaTrialAssetValidator.ConfigureForCommandLine
        ;;
    configure-mixamo-class-trials)
        run_method \
            configure-mixamo-class-trials \
            SessionRpg.Editor.Assets.MixamoPlayableClassTrialAssetValidator.ConfigureForCommandLine
        ;;
    inspect-mixamo-class-trials)
        run_method \
            inspect-mixamo-class-trials \
            SessionRpg.Editor.Assets.MixamoPlayableClassTrialAssetValidator.PrepareInspectionForCommandLine
        ;;
esac
