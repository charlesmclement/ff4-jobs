#!/usr/bin/env python3
"""Segment 9: the Black Chocobo back to Toroia -> the clerics take the Earth
Crystal -> Kain's airship to the Tower of Zot -> the Magus Sisters ->
Golbez and Tellah's Meteor -> Kain and Rosa -> Barbariccia -> Baron (the
Magma Key) -> out of the castle to the Enterprise (saveable).

Writes tas/routes/magnes-zot.json. Cells, doors, triggers and flags from the
local map/event data; strats from the guide (Tower of Zot).
"""
from route_kit import *

TOROIA_GROUNDS,TOROIA_1F,TOROIA_2F=96,97,98
ZOT=[202,203,204,205,206]
ZOT_SAVE,ZOT_6F,ZOT_7F=207,208,209
CHAMBER,WEST_2F,WEST_1F,CASTLE_OUT,CASTLE_HALL=15,14,13,4,5
PALADIN,KAIN,ROSA,CID,TELLAH,YANG=13,2,3,5,6,8
TENT=21
MINDY,CINDY,SANDY,BARBARICCIA=176,175,174,178
BLACK_CHOCOBO=7
JUMP=11

field=[
    {'character':TELLAH,'command':8,'byWeakness':True,'minEnemies':3,'minMp':60},
    {'character':TELLAH,'command':2,'reserveMp':20},
]
# Magus Sisters: "First attack the Fat one (Mindy)" (route killFirst does
# it); Tellah heals and otherwise Defends; Cecil, Cid, Yang attack.
magus=[{'character':TELLAH,'command':2,'whileMonster':m} for m in (MINDY,CINDY,SANDY)]+field
# Barbariccia: "When she turns into a whirlwind, have Kain jump to stop her
# spin ... heal your party quickly" (Rosa heals by role). Cecil, Cid, Yang
# attack.
barbariccia=[{'character':KAIN,'command':JUMP,'whileMonster':BARBARICCIA}]

s=[]
s.append(heal('Outside the Cave of Magnes: heal',OVERWORLD))
s.append(nav('Overworld: step off and back onto the Black Chocobo',OVERWORLD,(79,41),{'riding':True},[(78,42)],timeout=3600))
s.append(nav('Black Chocobo: fly back to the forest beside Toroia and land',OVERWORLD,(42,69),
             {'mapId':OVERWORLD,'riding':False,'atDestination':True},world_path((79,41),(42,69),transport=BLACK_CHOCOBO,avoid=())))
s.append(door('Overworld: into Toroia Castle',OVERWORLD,(40,69),TOROIA_GROUNDS,[(41,69)]))
s.append(door('Toroia Castle grounds: into the castle',TOROIA_GROUNDS,(16,11),TOROIA_1F))
s.append(door('Toroia Castle 1F: up to the clerics',TOROIA_1F,(10,2),TOROIA_2F))
s.extend(trigger('Clerics: Kain calls - take the Earth Crystal to his airship (flag 38)',TOROIA_2F,(6,17),38))
s.append(door('Toroia Castle 2F: down',TOROIA_2F,(6,20),TOROIA_1F))
s.append(door('Toroia Castle 1F: out',TOROIA_1F,(10,19),TOROIA_GROUNDS))
s.append(door('Toroia Castle grounds: out',TOROIA_GROUNDS,(16,32),OVERWORLD))
s.extend(trigger("Overworld: Kain's airship to the Tower of Zot (flag 39, autosave)",OVERWORLD,(41,70),39))
s.append(door('Zot 1F: to the stairs',ZOT[0],(29,4),ZOT[1],battlePlan=field))
s.append(door('Zot 2F: to the stairs',ZOT[1],(7,13),ZOT[2],battlePlan=field))
s.append(door('Zot 3F: to the stairs',ZOT[2],(7,7),ZOT[3],battlePlan=field))
s.append(door('Zot 4F: round to the far right door (guide: "take the room all the way to the right")',ZOT[3],(29,15),ZOT[4],battlePlan=field))
s.append(door('Zot 5F: the save room (guide: "save it in the room before it, heal")',ZOT[4],(27,21),ZOT_SAVE,battlePlan=field))
s.append(nav('Zot save room: onto the save point',ZOT_SAVE,(4,7),{'atDestination':True}))
s.append(tent('Zot save room: Tent before the Magus Sisters',ZOT_SAVE))
s.append(heal('Zot save room: heal anyone the Tent missed',ZOT_SAVE))
s.append(door('Zot save room: out',ZOT_SAVE,(4,11),ZOT[4]))
s.extend(trigger('Zot 5F: the Magus Sisters (Mindy first) (flag 40)',ZOT[4],(20,17),40,battlePlan=magus))
s.append(heal('Zot 5F: heal after the sisters',ZOT[4]))
s.append(door('Zot 5F: on to 6F',ZOT[4],(20,15),ZOT_6F,battlePlan=field))
s.extend(trigger('Zot 6F: Golbez and Kain; Tellah fights alone, Meteor, Tellah dies (flag 41)',ZOT_6F,(10,9),41))
s.extend(trigger('Zot 6F: Kain wakes; Rosa saved from the guillotine (flag 42, autosave)',ZOT_6F,(8,6),42))
s.append(equip('Zot 7F: Strongest on everyone (guide: Rosa arrives with no weapon)',ZOT_7F,[0]))
# Barbariccia is weak to lightning (guide monster table: Valvalis, Bolt):
# Strongest just re-picked Yang's claws, so both Thunder Claws back on.
s.append(claws('Zot 7F: Yang - Thunder Claws in both hands for Barbariccia',ZOT_7F,LIGHTNING))
s.append(formation('Zot 7F: formation - Cecil, Rosa, Yang, Kain, Cid; Cecil, Yang and Cid in front',ZOT_7F,[PALADIN,ROSA,YANG,KAIN,CID],[0,2,4]))
s.append(heal('Zot 7F: heal before Barbariccia',ZOT_7F))
s.extend(trigger("Zot 7F: Barbariccia (Kain Jumps her out of the whirlwind); Rosa's Exit to Baron, the Magma Key (flag 43, autosave)",ZOT_7F,(10,10),43,battlePlan=barbariccia))
s.append(door("Cecil's chamber: out",CHAMBER,(20,5),WEST_2F))
s.append(door('West tower 2F: down',WEST_2F,(6,5),WEST_1F))
s.append(door('West tower 1F: out to the walls',WEST_1F,(6,10),CASTLE_OUT))
s.append(door('Baron Castle: into the hall',CASTLE_OUT,(16,15),CASTLE_HALL))
s.append(door('Castle hall: out the front',CASTLE_HALL,(13,16),CASTLE_OUT))
s.append(door('Baron Castle: out the gate (the Enterprise is parked outside)',CASTLE_OUT,(20,34),OVERWORLD))
s.append(checkpoint('Segment 9 complete: Tower of Zot cleared, Kain and Rosa back, outside Baron by the Enterprise',OVERWORLD,43))

write('tas/routes/magnes-zot.json','Magnes → Toroia → Tower of Zot (segment 9) — UNTESTED',s,fleeTough=True,killFirst=[MINDY,CINDY,SANDY])
