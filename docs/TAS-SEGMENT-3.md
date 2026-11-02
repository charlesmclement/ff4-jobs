# TAS segment 3: Octomammoth → Antlion

Status: **route built (`tas/routes/octomammoth-antlion.json`, 27 steps), not
yet run.** Start: the save outside the waterfall cave (end of segment 2).
End: outside the Antlion's Den on the overworld (saveable), story flag 16.
Shortcut: **FF4 TAS: Segment 3** (`tas.py launch --resume`).

## Story (from local event data)

1. Walking west from the waterfall cave, tiles (125–126, 54–55) trigger the
   Damcyan bombing scene (`sc_e_0015`, flag 14).
2. Damcyan (map 38 courtyard → 39 1F → 40 2F → 41 throne room). Stepping on
   (14,13) starts Anna's death, then the scripted Tellah vs Edward fight
   (party 239, a 60000 HP "boss"; the pilot treats it as scripted). The
   hovercraft scene follows: Tellah leaves, Edward joins (flag 15), and the
   party is put on the hovercraft at the overworld gate (124,45).
3. Hovercraft (transport group 2: crosses shoals and rivers) to the land
   tile (141,44) beside the Antlion's Den. Navigate's arrival Confirm gets
   off; the step is done when `riding` is false.
4. Antlion's Den: 173 → east passage (31,23) → 175 → (19,29) → nest 177.
   Stepping on (14,18) starts the Antlion (party 225, monster 164, 1100 HP,
   no weakness). Flag 16 when it's beaten (the Sand Ruby is given).
5. Walk back out: 177 (14,1) → 175 (32,1) → 173 (19,2) → overworld. The
   Emergency Exit warp needs menus.

## Pickups (community route: Damcyan hidden area + den Spider's Silk)

* Damcyan jail B1 (map 43, through the courtyard's east door (29,13) and the
  jail (42)): hidden Elixir (2,6, flag 70) and Ether (4,6, flag 67), both
  opened from (3,6); Phoenix Down (5,6, flag 68), opened from (5,7).
* Antlion's Den 1: Spider's Silk (7,7, flag 73), opened from (8,7).

## Battle plan

* Antlion: Cecil uses Hermes Shoes (item 55) on himself once (Haste), then
  attacks. Edward Defends. Rydia Defends by default. Community notes:
  "Attack when its eyes are red"; that counter timing isn't modelled yet.
* Damcyan's Tellah vs Edward fight is scripted: Attack until the story ends it.
* Random fights: the general rules apply (run from tough or 60 s+ fights,
  Cure undead, Rydia never attacks, Cecil leaves healing to casters).

## New engine support used here

* `Condition.riding` + `Snapshot.Riding` (FieldPlayer.moveState not
  Walk/Dush). Navigation and step input also work in the `Transport` phase.
* Battle-plan item rules: `item`, `targetCharacter` (default self).
* `tas/plan_path.py --transport 2` plans hovercraft legs.

## Risks to watch on the first run

* Whether the hovercraft shows as `riding` (moveState) and whether getting
  off on Confirm works at (141,44).
* The jail and B1 walk (static collision says it's reachable without the
  switch).
* The Antlion counters physical hits; plain attacks may be slow.
