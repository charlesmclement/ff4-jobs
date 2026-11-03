# EXP plan (segments 1–4)

User: "I feel like we're arriving at the Mom Bomb under-levelled." Goal: a
comfortable run, not over-levelled, and the really dangerous fights avoided.

## Where we are (every save, read locally with `tas/save_read.py`)

| Point | Cecil | Rydia | Edward | Rosa | Yang |
|---|---|---|---|---|---|
| After the Mist Dragon (overworld) | 11 | – | – | – | – |
| After Octomammoth (overworld) | 14 | 10 | – | – | – |
| Mom Bomb summit, before the fight (map 180) | **14** | **11** | **10** | **10** | – |
| After the Mom Bomb | 15 | 11–12 | 11–12 | 11 | 11 |

The guide (SNES) lists the party at about 15 for the Antlion and **18 at Mount Hobs**. We arrive about 3–4 levels under it.

## How exp works in this build (verified, code)

* `BattleResultData.get_DistributionExp` = `GetExp / DistributionExpCount`: a fight's exp is split between the members who share it. Fewer members means more each.
* Levels come from `exp_table` by each character's growth group; a save's `currentExp` counts only exp gained after joining (Cecil 1500 + 3438 = 4938 → L14).

## Where to grind: Mount Hobs east cave (map 178), before the Mom Bomb

| Spot | Who shares | Good groups (total exp) |
|---|---|---|
| Waterfall (seg 2) | Cecil, Rydia, Tellah (Tellah leaves) | Killer Fish×2+Alligator 331, Alligator×2 330 |
| Antlion Cave (seg 3) | Cecil, Rydia, Edward | Yellow Jelly×4 404 (slow), Adamantoise (tough, fled) |
| **Mount Hobs east cave (seg 4)** | **Cecil, Rydia, Edward, Rosa: all fight the Mom Bomb** | Cockatrice×2+Gargoyle 607, Cockatrice×3 579, Gargoyle×2 442, Spirit×2 390, Bombs 759–1130 |

Mount Hobs wins: twice the Waterfall's exp per fight, every share goes to a Mom Bomb fighter, and most groups are flyers that Rosa shoots (Gargoyle fights took about 37 s).

## The plan (in `antlion-mombomb.json`, step 19)

* Pace (20,23) ↔ (20,19) in the east cave, fighting random battles until **Cecil 16, Rydia 13, Rosa 12, Edward 12**: two levels over the tested runs. About +1443 exp for Cecil, the slowest; roughly 10 fights at ~145 exp each.
* Skeleton (36) and Bloodbones (48) groups are fled at once (route `rng.avoidMonsters`; 4 Skeletons were already fled). No Cure is spent to dodge them (`cureBurns: 0`), so Rosa's MP stays for the Mom Bomb.
* Everything else is the tested segment: heal after fights, then walk to the summit, heal, formation, Mom Bomb.
* Levels are now in status.json (`levels`) and the trace, so the levels at each boss can be read from any run.

## Spread over the segments (user: "build grinds into the other segments too")

Average exp per fight for each member who stays for the Mom Bomb (battle.json groups weighted by their per-mille rates; avoided/fled groups count 0):

| Spot | Shared by | Per member per fight |
|---|---|---|
| Mist Cave (seg 1) | Cecil, Kain (leaves) | ~50 for Cecil only |
| **Watery Pass 1 before Tellah (seg 2)** | **Cecil, Rydia** | **~145** |
| Watery Pass after Tellah | Cecil, Rydia, Tellah (leaves) | ~96 |
| Antlion's Den 1 (seg 3) | Cecil, Rydia, Edward | ~54 (Yellow Jelly fled; Adamantoise isn't "tough", defense 1) |
| Mount Hobs east cave (seg 4) | all four | ~128 |

Exp to level (exp_table): Cecil 14→16 is 2461 exp, the slowest; Rosa 10→12 is 925. So moving Cecil's levels earlier saves the most seg 4 time.

* **Segment 2** (`mist-octomammoth.json`, `newgame-octomammoth.json`): at the Kaipo inn after Rydia joins, buy 3 Tents and 2 Phoenix Downs. In Watery Pass 1, before the Maiden's Kiss and Tellah, pace (33,27)↔(33,22) near the door in three stages: Cecil 12/Rydia 5, Cecil 13/Rydia 8, Cecil 13/Rydia 10. After each stage, walk out (31,30), use a Tent and walk back in (143,71 → 143,70). Rydia starts at level 1 with almost no MP, so stages rest by level, not by MP. Stages 2–3 are skipped once Cecil 13/Rydia 10 is reached. The guide has the party at 11 in the Watery Pass and 15 at Damcyan.
* **Segment 3**: no grind. Every member gains more per fight at Mount Hobs.
* **Segment 4**: targets unchanged (Cecil 16, Rydia 13, Rosa 12, Edward 12); it now starts closer to them.

## Rests (Tents outside Mount Hobs)

First live run: the grind worked, but Rosa spends Cure after every fight. Kaipo's inn is a hovercraft trip away, so the route rests with a **Tent** instead: Kaipo's shop sells them (100 gil, 3 bought), and the cave door is a few steps from the grind spot.

* Grind until the levels are reached, **or** Rosa's MP < 9 or Rydia's < 7.
* Walk out to the overworld, use a Tent (full HP/MP), walk back in. Re-entering also resets the step counter.
* Four grind/rest blocks; blocks 2–4 are skipped as a whole once the levels are in (`skipIf` + `group`). The last block that runs ends with a Tent, so the Mom Bomb starts at full MP.
* Skeleton groups: the flee now keeps trying for 1800 frames. The first run gave up after 600 and fought 4 Skeletons.

## Not done / open

* Untested live. The step stops when all four targets are reached, or after 60 min of game time.
* If more levels are wanted earlier (Octomammoth, Antlion), the same step type works in the Watery Pass (seg 2, 3-way split with Tellah).
