#!/usr/bin/env bash
set -euo pipefail
task_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
task_dotnet="${FF4JOBS_DOTNET:-dotnet}"
export NUGET_PACKAGES="$task_root/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$task_root"
"$task_dotnet" run --project src/FF4Jobs.Tests/FF4Jobs.Tests.csproj
"$task_dotnet" build src/FF4Jobs.Plugin/FF4Jobs.Plugin.csproj -c Release --nologo
