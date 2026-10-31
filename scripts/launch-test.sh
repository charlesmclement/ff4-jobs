#!/usr/bin/env bash
set -euo pipefail
task_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
task_game="$task_root/../SteamLibrary/steamapps/common/FINAL FANTASY IV PR"
task_proton="${FF4JOBS_PROTON:-/home/desktop/.steam/debian-installation/steamapps/common/Proton - Experimental/proton}"
mkdir -p /dev/shm/ff4-jobs-proton "$task_root/research"
export STEAM_COMPAT_DATA_PATH=/dev/shm/ff4-jobs-proton
export STEAM_COMPAT_CLIENT_INSTALL_PATH=/home/desktop/.steam/debian-installation
export SteamAppId=1173800
export SteamGameId=1173800
export WINEDLLOVERRIDES='winhttp=n,b'
export WINEDEBUG=-all
export FF4JOBS_TEST_MODE=1
unset PROTON_LOG
cd "$task_game"
task_display_args=(-screen-fullscreen 1 -window-mode borderless)
if [[ "${FF4JOBS_WINDOWED:-0}" == 1 ]]; then
    task_display_args=(-screen-fullscreen 0 -screen-width 1280 -screen-height 720)
fi
"$task_proton" run "$task_game/FINAL FANTASY IV.exe" "${task_display_args[@]}" > "$task_root/research/proton-test.log" 2>&1
