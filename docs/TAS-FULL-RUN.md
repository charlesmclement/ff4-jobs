# FF4 TAS: the whole game

Built overnight on 3 Nov. Segments 1–5 are verified live, and the run to Fabul took 8 minutes. **Segments 6–17 are built from local map and event data plus the guide, but none has been run.** Run All chains all 17; each segment also has its own shortcut, which resumes from a save.

| # | Route | From → to | End (saveable) | Desktop shortcut |
|---|---|---|---|---|
| 1–2 | newgame-octomammoth | New Game → Octomammoth | overworld | Segment 1-2 (Vanilla) |
| 2 | mist-octomammoth | Mist → Octomammoth | overworld | Segment 2 (Vanilla) |
| 3 | octomammoth-antlion | → Antlion | overworld | Segment 3 Vanilla |
| 4 | antlion-mombomb | → Mom Bomb | overworld | Segment 4 Vanilla |
| 5 | hobs-fabul | → Fabul | outside Fabul | Segment 5 Vanilla |
| 6 | fabul-mysidia | Leviathan, Mysidia, Mt. Ordeals, Paladin | outside Mysidia | Segment 6 Vanilla |
| 7 | mysidia-baron | Devil's Road, Yang, Waterway, Baigan, Cagnazzo | by the Enterprise, Baron | Segment 7 Vanilla |
| 8 | baron-magnes | Toroia, Twin Harp, Black Chocobo, Dark Elf | Cave of Magnes mouth | Segment 8 Vanilla |
| 9 | magnes-zot | Toroia clerics, Tower of Zot, Barbariccia | outside Baron | Segment 9 Vanilla |
| 10 | zot-dwarves | Agart, Underworld, Calbrena, Golbez | Underworld, Dwarf Castle | Segment 10 Vanilla |
| 11 | dwarves-babil | Tower of Babil, Lugae, Super Cannon | beside Agart | Segment 11 Vanilla |
| 12 | babil-hook | hook, hovercraft to Eblan | Eblan shallows | Segment 12 Vanilla |
| 13 | eblan-falcon | Edge, Rubicant, Falcon | Underworld, Babil | Segment 13 Vanilla |
| 14 | falcon-sealed-cave | necklace, heat shield, Sealed Cave, drill | beside Agart | Segment 14 Vanilla |
| 15 | mysidia-giant | Lunar Whale, FuSoYa, Giant of Babil | the moon | Segment 15 Vanilla |
| 16 | moon-subterrane | Lunar Subterrane | last save point | Segment 16 Vanilla |
| 17 | subterrane-zeromus | Zemus, Zeromus | the ending | Segment 17 Vanilla |

## Where it's most likely to need a live fix

* **Vehicles:**
  * Black Chocobo boarding: chasing a wandering NPC by name (seg 8). Landing on forest (seg 8–9).
  * The hook: one Confirm over the hovercraft (seg 12). Where the hovercraft lands (seg 13).
  * The Lunar Whale: does boarding or landing put the party inside the ship or in the air (seg 15)? Both are guarded.
  * `goalVehicle` reads the saved vehicle positions. **Checked in a local save:** they are cells. The hovercraft (id 2) is at (157,37) on map 1, exactly where segment 4 left it.
* **Map gimmicks:** the Sealed Cave rope bridges (seg 14); the Babil outer wall (seg 13).
* **Talking:** walking into an NPC goal now also presses Confirm. Not verified for the mirror, the Elder, the engineers, the King and Rubicant.
* **New battle logic:**
  * `realFights` for the 30000+ HP real fights.
  * Monster-tied rules in scripted fights (Dark Knight Defend, Yang/Monk Defend, Dark Elf 1 Defend).
  * `weaknessTier`.
  * Item use on an enemy (the Crystal).
* **Levels:** only segments 4 and 6 grind. Later bosses may need more levels than the story gives.

## Tools added for this

* `tas/route_kit.py`: step helpers and overworld/Underworld/moon planning. `write()` enforces a saveable end.
* `tas/build_segment6.py` … `tas/build_segment17.py`: one generator per segment. Rerun one after a fix.
* `tas/event_chain.py`: `flags`, `chain`, `where` over the event scripts.
* `tas/show_map.py`: a map's collision grid with its doors, triggers and save points.
* `tas/door_graph.py`: the shortest door route through a multi-floor dungeon.
* `tas/map_links.py`, `tas/plan_path.py` and `tas/extract_maps.py` already existed.
