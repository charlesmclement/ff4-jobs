# TAS segments 12–13: the hook, the hovercraft, Eblan, Rubicant, the Falcon

Status: **routes built, not yet run.**
* `tas/routes/babil-hook.json` (12 steps, built by `tas/build_segment12.py`)
* `tas/routes/eblan-falcon.json` (40 steps, built by `tas/build_segment13.py`)

## Segment 12: Agart → Baron (hook) → hovercraft → Eblan shallows

* **Start:** beside Agart (108,202), flag 49.
* **End:** the overworld east of the Cave of Eblan (38,221), flag 50, on foot (saveable).
* **Route:**
  1. Board the Enterprise using the new `goalVehicle`, then fly to Baron and land at (107,146).
  2. In the castle courtyard, the engineers at (27,21) run sc_e_0054 (flag 50). The scene ends aboard the hook Enterprise (vehicle 10) at (107,145).
  3. Fly to the hovercraft (`goalVehicle` 2: its saved position; it should still be below Mount Hobs (157,37)). Hold Confirm for 3 frames to hook it ("go over it and press the A button"), then wait 4 s.
  4. Fly to (38,221), land and heal. Only 4 cells around the Cave of Eblan are walkable, so the hovercraft has to cross the shallows.

## Segment 13: Eblan → Babil (outside) → Rubicant → Falcon

* **Start:** (38,221), flag 50. **End:** the Underworld outside Babil (58,22), flag 55 (saveable).

| Flag | Where | What |
|---|---|---|
| 51 | Pass to Babil (228) (29,11) | Edge vs Rubicant (254, scripted); Edge joins |
| 52 | Babil (239) (21,20) | the King and Queen of Eblan (255, 60000 HP each, scripted): everyone Defends |
| 53 | Babil (239), Rubicant | Rubicant (256: 188). Kain Jumps; Rydia casts Bio; the rest attack |
| 54 | crystal room (321) (14,20) | the pitfall to 8F (240), autosave |
| 55 | Babil 6F (242) (16,21) | the Falcon; to the Underworld, autosave |

Rubicant appears only after the parents' scene, so the walk chases the NPC named "ルビカンテ" (`goalEntity`).

## Code added

* Condition `moveState`: the player's FieldPlayerConstants.MoveState (0 walk, 1 dash, 2 airship, 3 ship/hovercraft, 4 low flying, 5 chocobo). Snapshot.MoveState.
* Navigate `goalVehicle`: the goal is the vehicle's saved position, from `UserDataManager.GetOwnedTransportationsClone()` (Id, MapId, Position). Positions are cells: a decoded local save has the hovercraft (id 2) at (157,37), map 1. (World units are still handled when y < 0.)

## Not verified

* Both segments.
* The hook (one Confirm over the hovercraft).
* Where the hovercraft lands when the Enterprise lands.
* That Rubicant's NPC name matches.
