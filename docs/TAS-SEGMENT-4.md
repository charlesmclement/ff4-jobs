# TAS segment 4: Antlion → Mom Bomb

Status: **route built (`tas/routes/antlion-mombomb.json`, 20 steps), not yet
run.** Start: the save outside the Antlion's Den (end of segment 3). End: the
overworld on the Fabul side of Mount Hobs (165,37), which is saveable, story
flag 19. Fabul is segment 5. Shortcut: **FF4 TAS: Segment 4**.

## Story (local event data)

1. Board the hovercraft by the den (141,44); hovercraft to Kaipo (planned
   with `plan_path.py --transport 2`, 19 waypoints); get off at (130,92).
2. Kaipo → Rosa's house (37). Talk to Rosa (16,6) from (15,6) facing Right (the bed blocks (16,7)): the event
   opens the native item list (`SystemCall.Current.ItemSelectWindow` →
   `SelectFieldContentClient`). The route picks the Sand Ruby (content 64)
   through the new `itemChoice`. Rosa joins, then the night scene and Edward's
   scripted Sahagin fight (party 240, 45672 HP). Flag 17.
3. Back to the hovercraft at (130,92); north past Damcyan to Mount Hobs
   (21 waypoints), get off at (157,37), walk in at (157,36).
4. Mount Hobs (178): the ice at (20,25): Rydia learns Fire (flag 18). Skip
   the first cave (save room 179); the east cave (27,6) leads to the summit (180).
5. Summit: (21,18) starts Yang vs 3 bombs (party 241, Yang alone), then the
   Mom Bomb (party 226, 11000 HP, weak to dark). Yang joins (flag 19).
6. North path (20,8) → east slope (181) → exit (23,30) → overworld (165,37).

## Battle plan

* Party roles: Rosa always heals (Rosa, Cecil, Yang first, lowest of them;
  then Rydia and Edward). Rydia summons Chocobo. Edward attacks. Yang Kicks
  on 2+ enemies. Cecil attacks; **no Darkness** (it kills Cecil in PR;
  ignore the strat text's Darkness).
* Summit, before Yang: field-menu heal (Phoenix Down on the fallen, Potions
  under 70%).
* Mom Bomb (community strat, minus Darkness): Cecil Attack. Yang Attack x2,
  Focus x2, Defend until it explodes (it is timed), then Kick all bombs.
  Rydia Blizzard on all after the split. Only Rosa heals while she can.

## New engine support

* `itemChoice` (event item list): reads `SelectFieldContentManager.controller`
  (`SelectFieldContentControllerBase.contentDataList`, `selectCursor.Index`)
  and matches the entry's UniqueId or Value to the content id. Two-column
  movement. The modal-popup Confirm rule skips while the list is open.

## Risks on the first run

* Boarding: the route stands on the hovercraft's tile and presses Confirm.
  If the game boards by walking onto it instead, step 1/10 fails within 60 s.
* Item list entry field (UniqueId vs Value) and its column count.
