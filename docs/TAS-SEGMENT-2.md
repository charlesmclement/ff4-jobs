# TAS segment 2 design: Mist Dragon → Octomammoth

Status: **route built (`tas/routes/mist-octomammoth.json`, 28 steps), not yet run.** All map, event and item
facts come from local extracted game data (tools listed at the end) and should
be confirmed by the first live run.

Segment 1 (`tas/routes/baron-mist-dragon.json`) ran start to finish once:
TAS (game) time 15:53.84, ending at the Mist Dragon victory (story flag 6)
inside Mist Cave, map 162.

Segment 2 ends at the Octomammoth victory (story flag 13), which unlocks
Black Mage. Planned file: `tas/routes/mist-octomammoth.json`, started with
`tas.py start ... --from-step 1` while standing in Mist Cave after segment 1.
Later the two will be chained into one route from New Game.

## Route outline

Map ids are the game's master `map` ids. Cells are runner cells (x,y).

| # | Where | Action | Done when |
|---|-------|--------|-----------|
| 1 | Mist Cave 162 | navigate to north exit door (34,2) | Player on map 1 |
| 2 | Overworld 1, arrive (89,107) | waypoints (99,107) (99,106) (100,106) → Mist village (101,106) | Event on map 67 |
| 3 | Mist village 67 | Bomb Ring scene, Rydia fight (party 237, scripted, the Girl has 60000 HP), Titan; mash Confirm, battle pilot on | Player on map 1, flag 7 |
| 4 | Overworld, wake at (108,106); Kain has left | waypoints (109,106) (109,96) (130,96) (130,92) → Kaipo (130,91) | Event in Kaipo |
| 5 | Kaipo 32 → inn 34 | arrival scene, night, Baron General + 3 soldiers (party 238: General 221 HP, Soldier 27 HP) | flag 8 (Rydia joins) |
| 6 | Inn 34 | exit door (30,13) | Player on map 32 |
| 7 | Kaipo 32, arrive (15,17) | walk to house door (29,12) | map 37 |
| 8 | House 37, arrive (13,18) | Rosa scene: Event object at (13,9), action "check". Interact from (13,10) facing Up | flag 9 |
| 9 | House 37 → Kaipo 32 | door (13,21), then town exit (16,30) | Player on map 1 |
| 10 | Overworld, arrive (130,92) | waypoints (140,92) (140,71) (143,71) → Watery Pass (143,70) | map 163 |
| 11 | Watery Pass 163, arrive (31,29) | talk to Tellah at (12,15) (NPC, script 23); spawns only once flag 9 is set | flag 10 |
| 12 | 163 → 165 | door (7,1); in 165 go from (31,28) to door (21,7) **without entering 166** | map 167 |
| 13 | 167 → 168 → 169 | doors (8,7) then (9,10); in 169 exit door (27,7) | Player on map 1 |
| 14 | Overworld, arrive (143,65) | waypoints (140,65) (140,60) (139,60) → waterfall entrance (139,59) | map 170 |
| 15 | Waterfall 170, arrive (13,6) | check the waterfall at (13,9) ("He's down there!") | map 171, flag 12 |
| 16 | 171 → 172 | door (9,15); in 172 walk toward Octomammoth (20,7) | boss battle |
| 17 | Octomammoth (party 224, monster 163) | see battle plan | flag 13, Player on map 172 |

### Skipping the camp (verify live)

Map 166 holds the first-camp scene (flag 11) and a save point. The only later
checks of flag 11 enable an optional tent tutorial in map 169. The waterfall
(map 170) checks only flags 96 and 13, and Octomammoth's room checks flag 12.
So the camp looks **optional**. Skipping it saves a cutscene but gives up the
full heal. Map 165's two collision layers appear to connect (31,28) to the
north door (21,7) without 166, but layer changes go through `ToLayer` objects.
The native search decides this live. If it can't find a route, fall back to
165 (7,17) → 166 → (4,1) → 165 (7,11) and take the camp.

Tellah is still required: he spawns only after the Rosa scene (flag 9), he is
needed for Thunder, and the later Damcyan story expects him.

## Battle plan

Octomammoth: 2350 HP, level 10, **weak to lightning and dark** (attribute
group 15), steals Potion. Fastest kill:

* Tellah: Black Magic → Thunder (ability 39) while he has ≥5 MP, then Recall.
* MP is conserved: only the Octomammoth step has a battle plan, so every
  random encounter on the way is plain Attack.
* Cecil: Darkness (dark, costs HP) while his HP stays above roughly 40%,
  otherwise Attack.
* Rydia: Attack, or a Potion when someone is low.

Today's battle pilot only does Attack, Potion and Phoenix Down, plus Defend for
the Mist Dragon. A plain-Attack fight should still win, just more slowly. This
needs a new feature: a per-step `battlePlan` (see the Codex notes).

The Kaipo soldiers and the scripted Rydia fight are fine with Attack.

## Item audit (rule: run `tas/audit_items.py` on every segment)

### Segment 1, already routed

Skipped, with distance from the path actually walked:

* Baron 1F (map 5): 480 Gil (18,4) 6 cells, Ether (19,4) 7, Tent (20,4) 8.
* Mist Cave (162): Potion (13,3) 4, Potion (37,18) 4, Eye Drops (31,3) 5, Tent (20,12) 5.

Verdict: nothing was **needed**. The run won, Mist Cave doesn't allow saving,
and none of these speed up a later fight. If later segments show they need
healing, the two Mist Cave Potions are the cheapest pickups (about 8 extra
cells each). The 480 Gil only matters if a shop stop is added.

### Segment 2 candidates

| Item | Where | Speed value |
|------|-------|-------------|
| Hermes Shoes | 172 (28,19), about 19 cells off the boss path | Haste on a hero: more turns against Octomammoth. Worth timing |
| Hades Armor | 172 (29,18), beside the Hermes Shoes | needs equipping (menu automation): defense only, low |
| Hades Helm, Hades Gloves | 171 (11,32), (15,32) | equipping needed, low |
| Antarctic Wind | 169 (8,11) | ice; Octomammoth isn't weak to ice, low |
| Bomb Fragment ×3 | Mist village 67, ×1 in 167 | fire; useful only on random encounters, low |
| Shadowblade, Hourglass, Ice Rod, X-Potion, Phoenix Down, Ethers, Potions | various Watery Pass floors | safety only |
| Ether | Kaipo 32 (25,17) | safety only |

Default: take nothing; the Hermes Shoes detour is the first experiment.

## Jobs (Jobs+ mod)

* Dark Knight and Thief unlock at flag 7, when Kain leaves at Mist.
* Kain learns Cecil's tricks that day (Dark Knight) but is gone until much
  later. Rydia joins at flag 8 and can only take Thief. Dark Knight before the
  unlock is Cecil-only.
* Black Mage unlocks at Octomammoth (flag 13), the end of this segment.
* Speed ideas to measure later: Thief Rydia (+12 Agility: more turns and better
  escape odds, but she loses her native commands). Escaping random encounters
  vs. keeping the EXP the bosses need. ABP banked now pays off in later jobs.
* Blocker: the runner can't drive menus yet, so it can't change jobs or equip
  gear. Segment 2 runs on Original classes.

## Tools for this and later segments

* `tas/extract_maps.py <numbers>`: extract map assets locally (use the UnityPy venv).
* `tas/map_links.py <numbers> [--events]`: doors, arrival points and event objects per sub-map.
* `tas/plan_path.py`: overworld waypoints from terrain (foot walkability, avoids entrances).
* `tas/audit_items.py --trace ... | --maps ...`: chests and pickups vs. the path walked.

## Community route (not yet merged)

Zic3's FF4 PR current-patch notes are linked from speedrun.com
(https://www.speedrun.com/ff4/guides/6ity4 → a Google Doc). The doc couldn't
be fetched from this environment. Segment 1 pickups from it should be added
once someone pastes the relevant section.

## Starting segment 2

Desktop button "FF4 TAS: Segment 2": press once to open the TAS game (no
route), load the save, press again; `tas.py play` picks the step from the live
map and cell, then waits until the game is focused.
