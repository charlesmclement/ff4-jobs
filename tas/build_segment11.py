#!/usr/bin/env python3
"""Segment 11: the Underworld walk to the Tower of Babil -> Dr. Lugae and
Barnabas, Lugaborg (the Tower Key) -> the Super Cannon (Yang) -> back down
to 1F; Golbez drops the party, Cid and the Enterprise pull them out -> the
surface; land beside Agart (saveable).

Writes tas/routes/dwarves-babil.json. Cells, doors, triggers and flags from
the local map/event data; strats from the guide (Tower of Babil).
"""
from route_kit import *

UNDERWORLD=2
B1F,B2F,B3F,B4F,B4F_SAVE=210,211,214,215,216
B5F,CANNON,B6F,B7F,B7F_SAVE,B8F=220,221,222,223,224,225
PALADIN,KAIN,ROSA,RYDIA,YANG=13,2,3,4,8
TENT=21
DOCTOR,BARNABAS,LUGAE=183,184,185
ENTERPRISE=3
JUMP=11
TOWER_KEY=75

# Kain Jumps on the bosses (guide: "have Kain Jump Dr. Lugae"; Lugaborg:
# "have Kain JUMP"). Route killFirst: the Doctor before Barnabas ("Keep
# pounding on Dr.Lugae until he is gone so that he doesn't constantly heal
# BALNAB").
lugae=[{'character':KAIN,'command':JUMP,'whileMonster':m} for m in (DOCTOR,BARNABAS,LUGAE)]

s=[]
s.append(heal('Outside the Dwarf Castle: heal',UNDERWORLD))
s.append(nav('Underworld: west and north to the Tower of Babil',UNDERWORLD,(58,21),{'phase':'Player','mapId':B1F},
             world_path((110,89),(58,21),world=2)))
s.append(door('Babil 1F: north stairs',B1F,(20,5),B2F))
s.append(door('Babil 2F: middle stairs',B2F,(20,13),B3F))
s.append(door('Babil 3F: on up',B3F,(24,19),B4F))
s.append(door('Babil 4F: top-left stairs (guide: "continue through to the top left")',B4F,(8,8),B5F))
s.append(door('Babil 5F: past the locked middle room, on up',B5F,(30,5),B6F))
s.append(door('Babil 6F: on up',B6F,(18,4),B7F))
s.append(door('Babil 7F: the save room (guide: "take the right side and into a door to find a SAVE POINT")',B7F,(32,8),B7F_SAVE))
s.append(nav('Save room: onto the save point',B7F_SAVE,(4,5),{'atDestination':True}))
s.append(tent('Save room: Tent before Dr. Lugae',B7F_SAVE))
s.append(heal('Save room: heal anyone the Tent missed',B7F_SAVE))
s.append(door('Save room: out',B7F_SAVE,(4,8),B7F))
s.append(door('Babil 7F: on to 8F',B7F,(34,16),B8F))
s.extend(trigger('Babil 8F: Dr. Lugae and Barnabas, then Lugaborg; the Tower Key (flag 47)',B8F,(26,20),47,battlePlan=lugae))
s.append(heal('Babil 8F: heal after Lugaborg (guide: the LASER "will most likely kill off one of your party members")',B8F))
s.append(door('Babil 8F: back down',B8F,(39,16),B7F))
s.append(door('Babil 7F: down',B7F,(19,4),B6F))
s.append(door('Babil 6F: down',B6F,(29,5),B5F))
s.append(nav('Babil 5F: the locked middle room - walk in',B5F,(21,9),{'phase':'Event'},stableFrames=15,skipIf={'flag2':17},group='towerkey'))
s.append(mash('Babil 5F: open it with the Tower Key (item choice)',None,itemChoice=TOWER_KEY,until={'phase':'Player','flag2':17},skipIf={'flag2':17},group='towerkey'))
s.append(door('Babil 5F: into the cannon room',B5F,(21,9),CANNON))
s.extend(trigger('Cannon room: three Goblin Captains; Yang stops the Super Cannon (flag 48, autosave)',CANNON,(5,10),48))
s.append(door('Babil 5F: back down',B5F,(8,8),B4F))
s.append(door('Babil 4F: down',B4F,(25,19),B3F))
s.append(door('Babil 3F: down',B3F,(20,13),B2F))
s.append(door('Babil 2F: down',B2F,(19,4),B1F))
s.extend(trigger('Babil 1F: Golbez breaks the bridge; Cid and the Enterprise; the hook; out of the Underworld (flag 49, autosave)',B1F,(20,22),49))
s[-1]['until']={'storyFlag':49,'mapId':OVERWORLD};s[-1]['stableFrames']=60
s.append(nav('Enterprise: land beside Agart',OVERWORLD,(108,202),{'mapId':OVERWORLD,'riding':False,'atDestination':True},
             world_path((111,199),(108,202),transport=ENTERPRISE,avoid=()),timeout=7200))
s.append(heal('Beside Agart: heal',OVERWORLD))
s.append(checkpoint('Segment 11 complete: the Super Cannon is gone, back on the surface by Agart',OVERWORLD,49))

write('tas/routes/dwarves-babil.json','Dwarf Castle → Tower of Babil (segment 11) — UNTESTED',s,fleeTough=True,killFirst=[DOCTOR,BARNABAS])
