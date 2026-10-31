#!/usr/bin/env bash
set -euo pipefail
task_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export NUGET_PACKAGES="$task_root/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"${FF4JOBS_DOTNET:-dotnet}" build "$task_root/src/FF4Iso.Plugin/FF4Iso.Plugin.csproj" -c Release --nologo
