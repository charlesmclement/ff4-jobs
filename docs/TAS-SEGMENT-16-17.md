# TAS segments 16–17: the Lunar Subterrane and Zeromus

Status: **routes built, not yet run.**
* `tas/routes/moon-subterrane.json` (25 steps, built by `tas/build_segment16.py`)
* `tas/routes/subterrane-zeromus.json` (10 steps, built by `tas/build_segment17.py`)

## Segment 16: moon → Lunar Palace → Lunar Subterrane, to its last save point

* **Start:** the moon after the Giant, flag 68. **End:** the save point (3,9) in save room 310, flag 69. A save point counts as saveable ground.
* `route_kit.write()` now accepts a non-overworld end only when the step before the checkpoint stands on a verified save point cell (entity セーブポイント).
* Route:
  1. Lunar Palace 153 → 2F 154 → (12,15): the Subterrane entrance (sc_e_0073, flag 69, autosave).
  2. The door sequence comes from the new `tas/door_graph.py`, a shortest route over the floors' doors. It checks, on each floor's collision grid, which doors the arrival point can reach.
  3. The doors: 296 (25,28) → 297 (23,8) → 298 (24,19) → 299 (32,28) → 301 (14,18) → 299 (23,33) → 302 (20,17) → 303 (30,11) → 302 (23,24) → 304 (5,3) → 302 (29,35) → 307 (30,26) → 308 (10,4) → 309 (21,24) → save room 310.
  4. In the save room: Tent, heal, Strongest on everyone.
* Check: door_graph reproduces the hand-built Cave of Magnes route exactly. It finds no route through the Sealed Cave, whose rope bridges are gimmicks; segment 14 relies on the native search there.

## Segment 17: to the core → Zemus → Zeromus → the ending

* **Start:** save room 310, flag 69. **End:** flag 70, the ending. Nothing is left to save; `final=True` is the one exception to the saveable-end rule.
* Doors: 310 → 309 (25,11) → 313 (19,21) → 314 (24,25) → 315 (25,23) → 316 (9,18) → 317. The final scene trigger is at (15,15).
* Fights: Zemus (436, scripted: FuSoYa and Golbez), Zeromus' first form (437, scripted), then Zeromus (440: 201, then 202 after the Crystal). Both 201 and 202 are in `realFights`.
* Plan (guide):
  1. Cecil uses the Crystal (content 59) once on Zeromus 201: "otherwise all your attacks will miss".
  2. Then against 202: Kain Jumps, Rydia summons Bahamut (75), Rosa casts Holy (23), and Rosa heals by role first.
