# FF4 JOBS+ scripted run prototype

The intended run is **scripted**: a route file chooses actions from observed game state. A person can watch and report problems; no person or remote model pilots the run. Start from a Pixel Remaster route, then measure job changes as separate optimizations.

Current coverage: a route engine and an **unverified opening-dialogue segment**, ending at the first stable Player phase. There is no complete-game route, navigation/pathfinding, boss decision policy, save-state system, RNG restoration or deterministic frame advance yet. Do not mistake this prototype for a finished TAS. Input durations count observed Unity updates; loading and frame timing can vary.

## Local operation

All game-state reads, route decisions and optional screenshot matching run on this machine. No screen data or game state is sent to an API. The runner contains no network client and does not call an LLM. The existing Steam game/Proton may have their own platform networking; this tool does not manage that.

Native game state is primary. Optional `vision.py` captures only the focused FF4 window and matches user-provided crops with Pillow. It does not perform semantic scene understanding or OCR. Files stay in `research/tas-session`; `latest.png` is replaced rather than accumulating every frame. A visual match expires after one second and cannot advance a route after capture fails or loses focus.

## Build and run

```
FF4JOBS_DOTNET=/path/to/dotnet bash tas/build.sh
python3 tas/tas.py launch --route tas/routes/opening-prototype.json
```

Use the new **FF4 Scripted Run (Prototype)** desktop shortcut on this machine. Choose **New Game** in its game window; the script arms when the opening enters Event. It repeats Confirm during the opening and stops at first field control. This segment needs live acceptance; it is not a battle strategy or a route through Baron yet.

**F10 stops the runner. Losing focus also stops it.** To stop from a terminal:

```
python3 tas/tas.py stop
python3 tas/tas.py status
```

Do not run it alongside another FF4 process. The launcher refuses to interrupt an existing game. It uses the existing game installation and two extra, opt-in DLLs; normal Jobs launches do not activate its input patches. A separate RAM Proton prefix is needed because this drive's filesystem cannot store Wine's DOS-device symlinks. Test Documents/saves remain on this drive at `research/tas-session/documents`, across reboots. The normal campaign is not copied or loaded.

To start another route in the running TAS session, `python3 tas/tas.py start path/to/route.json` gives five seconds to refocus the game. Route requests are tied to the current process session and stale startup requests are discarded.

## Route format

Each step has a label, kind, allowed `phases`, optional expected `mapId`, and a finite `timeoutFrames`. Unknown controls/phases are rejected before execution.

- `hold`: `keys` and `frames`; holds logical inputs for that many observed updates.
- `mash`: press/release pulses using `periodFrames` and `pressedFrames`, until a condition matches.
- `wait`: no input; wait for a condition.
- `checkpoint`: require a matching condition immediately or stop.

`until` can match `phase`, `mapId` and/or `visual` (all specified fields must match). `stableFrames` requires repeated observations. Input is released immediately on a matching destination, including while checking stability. An unexpected state/map, timeout, focus loss or F10 stops all synthetic input. Logical names include Confirm, Back, directional keys and Menu; physical A/B assignments are not scripted.

The route engine does not edit inventory, damage, story flags, ABP or job selections. Future job optimizations should use legitimate menu inputs and personal progression.

## Optional local visual checks

```
python3 tas/vision.py
python3 tas/vision.py --watch --profile /absolute/path/to/profile.json
```

A profile is a JSON list, e.g. `[{"label":"battle-menu","template":"battle-menu.png","box":[100,400,200,120],"maxError":5}]`. The template must be a crop of the same window size/resolution. Box values are x, y, width, height. `maxError` is mean absolute RGB error, not a probability. Labels appear in local `vision.json` for native route conditions. Avoid backgrounds that animate. Primary game-state checks are preferable.

## Validation and remaining work

- 20 pure route checks passed: input edges, pulse releases, same-frame polling, phase/map mismatch, timeouts, focus loss, checkpoints, backward frames, explicit stop and visual conditions.
- Local image-matching checks passed: synthetic exact-match, mismatch and crop checks.
- Four native input-query checks passed in the installed FF4 build under Proton (Confirm down/held, Back exclusion, direct configured-controller polling). `python3 tas/tas.py launch --smoke` repeats them and quits before loading a campaign.
- Still needed: live opening replay, state/position observation for each route segment, native menus/target selection, encounter recovery, boss strategies, complete run replay and timing/RNG reproducibility.

Reference candidates: [Zic3's FFIV Pixel Remaster notes](https://docs.google.com/document/d/1BDBmvgWAxpJM5qveSEnqOBQlgV7J0T2Ot5jS9HhyOQ4/edit) and [JamesRuns' PR Any% notes](https://drive.google.com/file/d/1hVepwHTRNHVl2dNqRh3ohsrXNs-x0WAd/view). These are route references to inspect, not imported or verified instructions. SNES timing/RNG exploits are not assumed to work on this PR build.
