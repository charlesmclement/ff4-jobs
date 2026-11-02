# FF4 JOBS+ scripted run prototype

The intended run is **scripted**: a route file chooses actions from observed game state. A person can watch and report problems; no person or remote model pilots the run. Start from a Pixel Remaster route, then measure job changes as separate optimizations.

Current coverage: **an untested New Game → Mist Dragon victory route**. It advances the opening, walks through Baron's Rosa/Cid/bedroom sequence, waits through departure, travels to Mist Cave, handles encounters, and attempts Mist Dragon. The final checkpoint requires native ScenarioFlag1[6] and field control in Mist Cave, so escaping an encounter cannot count as boss victory.

This is a scripted run prototype, not a verified speedrun or a finished TAS. There are no deterministic savestates, RNG restoration, frame advance, complete-game route, shop optimization, or jobs optimization. Input durations count observed Unity updates; loading and frame timing can vary.

## Local operation

All game-state reads, route decisions and optional screenshot matching run on this machine. No screen data or game state is sent to an API. The runner contains no network client and does not call an LLM. The existing Steam game/Proton may have their own platform networking; this tool does not manage that.

Native game state is primary. Optional `vision.py` captures only the focused FF4 window and matches user-provided crops with Pillow. It does not perform semantic scene understanding or OCR. Files stay in `research/tas-session`; `latest.png` is replaced rather than accumulating every frame. A visual match expires after one second and cannot advance a route after capture fails or loses focus.

## Build and run

```
FF4JOBS_DOTNET=/path/to/dotnet bash tas/build.sh --build-only
python3 tas/tas.py launch --route tas/routes/baron-mist-dragon.json
```

Use **FF4 Scripted Run (Prototype)** on the desktop. Choose **New Game**; the script arms when the opening enters Event. Leave FF4 focused. It is intended to stop after Mist Dragon's victory scene, with control returned to you inside the cave. The shorter `opening-prototype.json` remains available for isolating opening issues.

The new route has **not been run**. At the user's request, no automated tests or game launches were performed for this extension; only compilation. The user owns all live acceptance testing.

**F10 stops the runner. Losing focus also stops it.** To stop from a terminal:

```
python3 tas/tas.py stop
python3 tas/tas.py status
```

Do not run it alongside another FF4 process. The launcher refuses to interrupt an existing game. It uses the existing game installation and two extra, opt-in DLLs; normal Jobs launches do not activate its input patches. A separate RAM Proton prefix is needed because this drive's filesystem cannot store Wine's DOS-device symlinks. Test Documents/saves remain on this drive at `research/tas-session/documents`, across reboots. The normal campaign is not copied or loaded.

To start another route in the running TAS session, `python3 tas/tas.py start path/to/route.json` gives five seconds to refocus the game. Route requests are tied to the current process session and stale startup requests are discarded.

If you stop or lose focus, the script does not resume by itself. To resume at a numbered step while still in the correct game state:

```
python3 tas/tas.py start tas/routes/baron-mist-dragon.json --from-step 14
```

Step numbers here start at 1; `status.json`'s `step` index starts at 0. Step 14 is overworld travel, 15 is Mist Cave and the boss. Resuming does not load a save, reposition the party, or set story flags. Only choose a step appropriate to where the game actually is.

`status.json` reports the current label, position, story flags, decision and elapsed wall-clock seconds. `trace-<session>.jsonl` records progress locally, including stopped/failed reasons. These traces and saves are ignored by Git.

## Route format

Each step has a label, kind, allowed `phases`, optional expected `mapId`, and a finite `timeoutFrames`. Unknown controls/phases are rejected before execution.

- `hold`: `keys` and `frames`; holds logical inputs for that many observed updates.
- `mash`: press/release pulses using `periodFrames` and `pressedFrames`, until a condition matches.
- `wait`: no input; wait for a condition.
- `checkpoint`: require a matching condition immediately or stop.
- `navigate`: `mapId` plus `goal: {x,y,layer}`. Query native collision-aware pathfinding, then feed ordinary field movement. Recalculate after encounters/events or when movement stalls. Stop after a bounded stall rather than teleporting.
- `interact`: optionally face a direction once, then pulse Confirm through field dialogue/events until the condition matches.
- `battle`: allow the battle controller to run until its condition matches; advance post-battle dialogue.

`handleBattles: true` enables native menu-driven combat during a step. The policy selects Attack on ordinary enemies, uses owned Potions/Phoenix Downs when available, and uses Defend in Mist Dragon modes 1/3. It cancels an unconfirmed attack if the dragon becomes mist. Already committed attacks cannot be recalled; timing still needs live tuning. The script does not deliberately toggle auto-battle or change battle-speed settings.

`until` can match `phase`, `mapId`, `storyFlag` (set in ScenarioFlag1), `atDestination` and/or `visual` (all specified fields must match). `stableFrames` requires repeated observations. Input is released immediately on a matching destination, including while checking stability. An unexpected state/map, timeout, focus loss or F10 stops all synthetic input. Logical names include Confirm, Back, directional keys and Menu; physical A/B assignments are not scripted.

The route engine does not edit inventory, damage, story flags, ABP or job selections. Future job optimizations should use legitimate menu inputs and personal progression.

## Optional local visual checks

```
python3 tas/vision.py
python3 tas/vision.py --watch --profile /absolute/path/to/profile.json
```

A profile is a JSON list, e.g. `[{"label":"battle-menu","template":"battle-menu.png","box":[100,400,200,120],"maxError":5}]`. The template must be a crop of the same window size/resolution. Box values are x, y, width, height. `maxError` is mean absolute RGB error, not a probability. Labels appear in local `vision.json` for native route conditions. Avoid backgrounds that animate. Primary game-state checks are preferable.

## Build status and remaining work

Version 0.2.0 is compiled, **not tested**. The previous 0.1.0 input prototype had 20 core checks and four native input-query checks, but those results do not validate this new movement/battle route. All tests are deferred to the user for this iteration. `tas/build.sh --build-only` deliberately skips the test executable.

Live acceptance still needs to establish: field alignment and stair traversal, all Baron triggers, native battle cursor/target selection, item recovery, mist transitions, and the victory checkpoint. Later work includes extending beyond Mist Dragon, routing supplies and jobs, and reproducible timing/RNG. No full-game completion or measured segment time is claimed.

The route coordinates and milestones were read from this installation's map/event assets. Mist behavior comes from its `sc_ai_162_MistDragon` script. No proprietary map files or scripts are included in the repository.
