# Random encounters in FF4 Pixel Remaster — research notes

Status legend: **verified (code)** = read in this build's compiled code;
**verified (live)** = seen in the running game; **untested** = expected from
the code, not yet seen live; **unknown** = not known yet.

## Environment (Phase 0)

| Item | Value |
|---|---|
| Game | FINAL FANTASY IV (Pixel Remaster), Steam app 1173800, build id 20265023 |
| Executable | `FINAL FANTASY IV.exe`, `GameAssembly.dll` (PE32+ x86-64), SHA-256 `bb2f4c9d…42ed` (the plugin refuses any other) |
| Engine | Unity 2019.4.26f1, **IL2CPP** (no Mono assemblies; managed view through Il2CppInterop) |
| Runtime | Windows build under Proton/Wine on Linux |
| Mod loader | BepInEx 6 (IL2CPP), Harmony |
| TAS | `tas/FF4Tas.Plugin` (C#): route engine with navigate/mash/menu/checkpoint steps; input goes in through Harmony prefixes on `InputListener`/`KeyConfig` key queries and `InputDevice.get_InputAxis` (gameplay inputs only); `tas/tas.py` launches, starts and resumes routes |
| Timing | `Time.timeScale` speed-up (RunClock); the run timer counts game time. Unity variable timestep; no frame stepping yet |
| Memory access | In-process through Il2CppInterop (typed fields and properties, no hard-coded addresses); Harmony hooks on game methods |
| Static analysis | Il2CppDumper output (`research/ff4-metadata/dump.cs`, `script.json`), `objdump`, Mono.Cecil (`research/inspect-il`) |
| Reproducible state | In-game saves and quicksaves; resume picks the route step from map and story flags. No save states |
| Segments | 1–4 run live (New Game → Mom Bomb); 5 (Fabul) partly run; 6 not built |

## How a random battle starts — verified (code)

`Last.Map.EncounterLot` (dump.cs TypeDefIndex 6525) holds the state:

| Field | Offset | Meaning |
|---|---|---|
| `stepsRange` (Min, Max) | 0x1C | the map's `required_steps_min/max` (master `map` table; e.g. overworld 150–200, Mount Hobs 120–200) |
| `subtractSteps` | 0x24 | the **threshold** rolled for this stretch |
| `moveCounter` | 0x28 | accumulated step value |
| `preEncountData` | 0x38 | which enemy group gets drawn |

* `Shuffle()` (RVA 0x26BC80): `subtractSteps = UnityEngine.Random.Range(min, max+1)`; `moveCounter = 0`.
  Called from `FieldController.MapSetup` (every map setup),
  `FieldController.EncountShuffle` and `SetStepsRange` (when the old threshold
  is already used up).
* `CheckEncount()` (RVA 0x26B390), called from
  `FootMonitoring.ImmediatelyBeforeMoveingAction`, i.e. **once per step, before
  the move**: `moveCounter += value`, where `value` is the current terrain's
  `foot_information.subtract_steps` passed through
  `SpcialFlagProvider.GetEncountValue` (the base returns its input unchanged).
  The value is 1 when there is no terrain info. **Every FF4 terrain row has 6.**
  When `subtractSteps - moveCounter <= 0`, the group is drawn
  (`EncounterLot.RandomFraction`: `Random.Range(1, 1001)` against the monster
  set's per-mille rates) and the battle is queued.
* **Steps per battle = ceil(threshold / 6)**: 25–34 on the overworld (150–200),
  20–34 in most dungeons (120–200). No per-step probability roll: once the
  threshold is rolled, the battle's step is fixed.
* Encounters can be switched off (`SaveMapData.encountEnable`, the in-game
  option). Not used: the TAS category keeps encounters on.
* `SaveMapData` stores `moveCount` and `subtractSteps`;
  `EncounterLot.SetRestoreData` puts them back on load. **Untested** whether a
  later `Shuffle` on load overwrites them.

## The random generator — verified (code)

* Encounters use **UnityEngine.Random** (the global Unity generator).
* `UnityEngine.Random.InitState` is called with **`DateTime.Now.Millisecond`**
  (the wall clock, 1000 possible seeds) from `FieldController.Initialize`,
  `EncounterLot`'s constructor, `EncounterLot.RandomFraction(init: true)` and
  `Camera.Shaker.StateInitPlay` (every screen shake). `CommonUtility.SetSeed`
  also seeds from the clock. Level-up HP/MP growth (`GrowthCurveUtility`) and
  `StatusUpProvider` seed with a value they are given.
* Only 26 direct call sites in game code use `Random.RandomRangeInt`. Battle
  rolls (damage, crits, hits) may use another generator: **unknown**.
* The algorithm is assumed to be Unity's Xorshift128 (community-documented).
  **Untested** until `encounter_lab.py verify` matches it against the trace.

## What this means for avoiding battles

| Question | Answer | Status |
|---|---|---|
| Does waiting N frames before walking change the next battle's step? | No: the threshold is already rolled, and only steps add to the counter | verified (code) |
| Does turning or changing direction add to it? | Only `CheckEncount` adds, once per step taken | verified (code); turning without a step is untested |
| Do menus change it? | Not the counter or threshold. They can change **which group** appears if something draws or reseeds Unity's generator in between | verified (code) / untested |
| Do map transitions reset it? | Yes: map setup calls `Shuffle` (counter 0, new threshold) | verified (code); untested live |
| After a battle? | `EncountShuffle` exists; whether it runs after every battle is untested | untested |
| Save/load? | Counter and threshold are stored in the save | verified (code); untested live |
| Can the threshold be chosen? | It depends on the wall-clock millisecond at map setup, plus the draws between the seed and `Shuffle`. If those draws are constant, timing the door press to the right millisecond gives the 200 threshold (34 steps) | untested |
| Can a battle be predicted? | Yes, exactly: steps left = ceil((threshold − counter) / 6) | verified (code); the overlay shows it |

Legitimate avoidance levers, by expected payoff:

1. **Step budgeting**: the overlay says how many steps are left; route so a
   stretch ends (door, stairs, event) before the counter runs out.
2. **Resets**: going through a map transition resets the counter. It pays
   when the transition is shorter than the ~10–40 s a battle costs.
3. **Clock-timed transitions**: once verified, wait for a good millisecond
   before stepping through a door (a timing-only input, no memory edits).

The SNES game's encounter system (fixed table, step routing, "seed"
manipulation) doesn't apply directly: the Pixel Remaster rolls a
threshold with Unity's generator, as above. SNES addresses and formulas
were not used.

## Live results (first run, overworld Hobs → Fabul)

* **Forecast works** (user, live): the battles started when the counter reached the threshold (174/174, 178/180, 127/129).
* **RNG model verified (live)**: 2681 of 2685 traced `Random.RandomRangeInt` results matched Unity Xorshift128 from the last `InitState`. The 4 misses come right after a seed and point to an untraced draw (likely a float `Random.value`/`Range(float)`).
* **Enemy group roll verified (live)**: rolls 892, 295, 995 gave the groups the per-mille ranges predict (monster sets 6/15: 798–922 Bombs, 169–336 Gatlinger/Domovoi, 985–1000 Sword Rats).
* **When the rolls happen**: the enemy roll `Range(1,1001)` and the next threshold `Range(150,201)` are drawn the moment a battle triggers, from whatever state the generator is in. Nothing draws on the field while walking.
* **Seeds**: each battle start reseeds with `(int)DateTime.Now.Ticks` (CommonUtility.SetSeed; 100 ns ticks, not controllable). Level-ups reseed with small values (51, 1684, 2806 seen), and the next field rolls came straight from seed 51: party roll 295 and threshold 178, predicted exactly.
* **Battles use the same generator** (10,619 `Range(0,101)` rolls, plus damage-sized ranges). Field menus draw too (`Range(0,99)` ×92, `Range(0,9)` ×16: likely healing amounts).

So the next battle's group and the threshold after it are fixed when the last battle ends. Legitimate ways to change them before walking into it: anything that draws or reseeds (a map transition runs `Shuffle`, which draws one value; field healing draws), or changing which area's monster set is active where the battle triggers.

## RNG steering in the route (segments 1–2)

User goals: a comfortable run, no over-levelling, the really dangerous fights avoided altogether.

* `Rng.cs` mirrors UnityEngine.Random from the hooks (`InitState`, `Range(int)`, `Range(float)`: the only three in this build). It drops "synced" when a traced result disagrees, until the next seed. Read-only.
* Forecast: walking draws nothing, so the next battle's group is the next `Range(1,1001)` against the monster set where the player stands. The set is the game's own table for the cell, else the map's set. Groups come from battle.json (`sets`, `parties`, `mapSets`, built by battle_db.py). The overlay shows `battle in N steps (...); next: <monsters>`, with `[AVOID]` on dangerous groups.
* Route `rng` block: `{"avoidMonsters": [...], "cureBurns": n}`. During a navigate step, if the forecast group has an avoided monster and the battle comes before this step's path ends, one field Cure is cast to move the generator, at most n per map. The Cure is never Tellah's (he needs his MP for Octomammoth), and the caster keeps 14 MP. Then the forecast is re-checked. Any avoided group that still comes is fled at once (never bosses).
* **Segment 2** (`mist-octomammoth.json`, `newgame-octomammoth.json`): avoid Tiny Mage (9) and Toadgre (19), the Toad casters the guide warns about (Watery Pass 5, Waterfall: Sahagin×4+Tiny Mage×2, Toadgre groups, Tiny Mage×4). `cureBurns: 2`. No extra fights are added, so levels match the tested run.
* **Segment 1** (`baron-mist-dragon.json`): unchanged. Its random fights (overworld near Baron; Mist Cave: Goblins, Sword Rats, Insectus, Flying Eyes) aren't dangerous, Cecil and Kain have no field magic to shift the generator, and the battles give the levels the Mist Dragon needs. Only the forecast shows.
* Not verified live: the Cure draw count (≈13, from 8 casts), whether a Cure on a full-HP member casts at all (if not, the step backs out after 10 tries and walks on), and the game's table lookup (`GetEncountInfo`) on the overworld.
* Next lever, not built: extra map transitions (each `Shuffle` draws once, and entering a map re-rolls the threshold) to skip a fight outright or reach a better group, where a door is close.

## Tools

* `tas/FF4Tas.Plugin/EncounterWatch.cs`: read-only hooks (Harmony postfixes)
  on `EncounterLot.Shuffle/SetRestoreData/CheckEncount` and
  `UnityEngine.Random.InitState/RandomRangeInt`. Writes
  `research/tas-session/encounters.jsonl` and `rng.jsonl` (draw log capped
  at 200k lines a launch). Overlay line on the field: `battle in N steps
  (counter/threshold, range min-max)`. Changes no game value.
* `tas/encounter_lab.py`: `summary` (battles per map vs threshold), `verify`
  (Xorshift128 model vs the traced draws), `table` (threshold per seed
  millisecond).

## First experiment (user runs it; the assistant does not run the game)

1. Load any save on a map with encounters (overworld or Mount Hobs) with the
   TAS launcher (the overlay needs the plugin).
2. Walk. The overlay counts down; the battle should start on the step where
   it reaches 0. **Pass**: the battle starts within one step of the forecast.
3. Step into a building/door and back out: the counter should go to 0 with a
   new threshold.
4. Stand still for 10 s, open and close the menu: the count should not change.
5. Send the logs (they're local): `python3 tas/encounter_lab.py summary` and
   `verify`.

That proves prediction (step 2) and a legitimate reset (step 3). The next
milestone is the clock-timed transition: `verify` must pass first, then
`table` gives the target milliseconds.

## Roadmap (impact × difficulty)

1. Verify the forecast and the transition reset live (now; low difficulty, high impact).
2. Show step budgets in navigate steps: warn when a planned path is longer than the steps left (low, high).
3. Verify the RNG model, then clock-timed door entries for a 200 threshold (medium, high).
4. Route-wide step plan: place resets and battle locations (medium, high).
5. Pause/step: `Time.timeScale = 0` + manual advance, input polled on Update (medium, medium).
6. Battle RNG: find the generator battles use (trace with the same hook approach) (high, medium).
7. Battle search over reproducible states (high, medium; needs 5 and 6).
