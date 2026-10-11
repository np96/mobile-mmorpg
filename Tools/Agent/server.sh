#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
server_root="$project_root/Server"
tooling_root="$project_root/build/server-tooling"
dotnet_bin="${SESSION_RPG_DOTNET_BIN:-}"
if [[ -z "$dotnet_bin" ]]; then
    if command -v dotnet >/dev/null 2>&1; then
        dotnet_bin="$(command -v dotnet)"
    else
        dotnet_bin="$tooling_root/dotnet/dotnet"
    fi
fi
if [[ ! -x "$dotnet_bin" ]]; then
    echo "Install the SDK pinned in Server/global.json or set SESSION_RPG_DOTNET_BIN." >&2
    exit 1
fi
export DOTNET_CLI_HOME="$tooling_root/cli-home"
export NUGET_PACKAGES="$tooling_root/nuget-packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
cd "$server_root"
command_name="${1:-doctor}"
if [[ $# -gt 0 ]]; then shift; fi
case "$command_name" in
    doctor) exec "$dotnet_bin" --info ;;
    restore) exec "$dotnet_bin" restore SessionRpg.Server.Tests/SessionRpg.Server.Tests.csproj "$@" ;;
    build) exec "$dotnet_bin" build SessionRpg.Server.Tests/SessionRpg.Server.Tests.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1 "$@" ;;
    test) exec "$dotnet_bin" test SessionRpg.Server.Tests/SessionRpg.Server.Tests.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1 --logger trx "$@" ;;
    run) exec "$dotnet_bin" run --no-launch-profile --no-restore --disable-build-servers -p:UseSharedCompilation=false --project SessionRpg.Server -- --dev-local "$@" ;;
    provision-player) exec "$dotnet_bin" run --no-launch-profile --no-restore --disable-build-servers -p:UseSharedCompilation=false --project SessionRpg.Server -- --dev-local provision-player "$@" ;;
    rotate-credential) exec "$dotnet_bin" run --no-launch-profile --no-restore --disable-build-servers -p:UseSharedCompilation=false --project SessionRpg.Server -- --dev-local rotate-credential "$@" ;;
    *) echo "Usage: Tools/Agent/server.sh {doctor|restore|build|test|run|provision-player|rotate-credential}" >&2; exit 2 ;;
esac
