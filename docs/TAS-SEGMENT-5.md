# TAS segment 5: Mount Hobs → Fabul

Status: **route built (`tas/routes/hobs-fabul.json`, 18 steps), not yet run.**
Start: the save outside Mount Hobs on the Fabul side (165,37), story flag 19
(end of segment 4). End: the overworld outside Fabul's south gate (220,46),
which is saveable, story flag 21 (ship promised). The harbor departure and
Leviathan are segment 6. Shortcuts: **FF4 TAS: Segment 5** and
**FF4 TAS Segment 5 (Vanilla)**.

## Story (local event data, extracted map scripts)

1. Field-menu heal on the overworld, then walk east to Fabul (219,44):
   `plan_path.py` from (166,37), 11 turns. Step right first: (165,36) is the
   Mount Hobs door.
2. Courtyard 44 (15,15) → hall 45 (12,5) → corridor 46 (5,2) → king's hall 47.
   The audience trigger `sc_e_0021` is at (16,13), one tile north of the
   arrival cell. Its Select's first choice (0) accepts; the other sends the
   party back to 46.
3. Defense chain (all scripted, `not_escape`): gate 248 → hall 249 → corridor
   248 → king's hall 250, 249, 248 → crystal room: Kain vs Cecil alone (242)
   → Golbez takes Rosa, Rydia rejoins, flag 20, autosave. HP is restored
   before the defense and before Kain.
4. Crystal room 48 (14,26) → 47 (16,15) → 46 (5,10) → 45 (6,8) → guest room
   49. The monk at (23,8) (`sc_e_0022`) offers rest (first choice = yes);
   night talk, full heal, then the king's chamber 56 (`sc_e_0022_1`): the
   Deathbringer, the ship, flag 21, autosave.
5. 56 (5,7) → 55 (5,7) → 54 (6,11) → courtyard 44 (15,32) → overworld.

Note: `research/ff4-events` has an empty `sc_e_0022_1`; the extracted map
copy (`research/tas-maps/.../Map_20051_12/sc_e_0022_1.json`) is the real one.

## Battle plan

Same roles as segment 4 (Yang Kicks 2+ enemies, Cecil attacks, no Darkness).
In the defense the party is Cecil, Yang and Edward; Rosa and Rydia sit it out.

## Next (segment 6)

Harbor trigger on the overworld at (226,43) (on foot; walk 220,46 → 221,46 →
221,45 → 225,45 → 225,43). `sc_e_0023` chain: ship, Leviathan, Cecil alone on
the beach, overworld flag 22 + autosave.

## Not handled

* Shop and equip drivers are new and untested (cursor layout guesses: sell list grid, horizontal equip commands).

## Guide additions (route now 36 steps)

* Grind random fights in front of Fabul until 4100 gil, back to (218,44), Tent.
* Gear shop (east room, map 50, counter at (5,6)): sell Kain's Iron set; buy
  Demon Armor, Demon Helm, Demon Gloves, Fire Claw; Strongest on Cecil and
  Yang; sell the Hades set; buy what's missing; equip again (three passes).
* Item shop (west room, map 49, counter at (26,8)): 5 Phoenix Downs, 10
  Potions, 2 Tents with what's left.
* Morning: Strongest on Cecil (Deathbringer).
