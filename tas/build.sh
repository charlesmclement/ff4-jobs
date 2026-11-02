#!/usr/bin/env bash
set -euo pipefail
task_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
task_dotnet="${FF4JOBS_DOTNET:-dotnet}"
export NUGET_PACKAGES="$task_root/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"$task_dotnet" run --project "$task_root/tas/FF4Tas.Tests/FF4Tas.Tests.csproj"
"$task_dotnet" build "$task_root/tas/FF4Tas.Plugin/FF4Tas.Plugin.csproj" -c Release --nologo
