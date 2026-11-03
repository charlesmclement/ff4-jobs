#!/usr/bin/env python3
"""Segment 10: the Enterprise from Baron to Agart -> the Magma Key in the
well opens the mountain -> down into the Underworld (the Red Wings and the
dwarf tanks; the Enterprise crash-lands) -> the Dwarf Castle (Calbrena,
Golbez, Rydia returns) -> back out to the Underworld (saveable).

Writes tas/routes/zot-dwarves.json. Cells, doors, triggers and flags from the
local map/event data; strats from the guide (Town of Agart, Underground
World, Dwarven Castle).
"""
from route_kit import *

UNDERWORLD=2
AGART,AGART_WELL=106,109
DWARF_GROUNDS,DWARF_HALL,DWARF_THRONE=118,119,120
PALADIN,KAIN,ROSA,RYDIA,YANG=13,2,3,4,8
CAL,BRINA=179,211
ENTERPRISE=3
JUMP=11
MAGMA_KEY=72

# Calbrena: "have Cecil attack the CALs, have Kain JUMP the CALs ... have
# Rosa AIM at the BRINAs" (Rosa Aims by role; route killFirst Cals, then
# Brinas).
calbrena=[{'character':KAIN,'command':JUMP,'whileMonster':CAL},{'character':KAIN,'command':JUMP,'whileMonster':BRINA}]

s=[]
s.append(heal('Outside Baron: heal before the flight',OVERWORLD))
s.append(nav('Baron: step off and back onto the Enterprise',OVERWORLD,(107,145),{'riding':True},[(107,146)],timeout=3600))
s.append(nav('Enterprise: south to Agart; land beside the village (guide: "south of Baron")',OVERWORLD,(108,202),
             {'mapId':OVERWORLD,'riding':False,'atDestination':True},world_path((107,145),(108,202),transport=ENTERPRISE,avoid=())))
s.append(door('Overworld: into Agart',OVERWORLD,(109,202),AGART))
s.append(door('Agart: the well house',AGART,(16,22),AGART_WELL))
s.append(nav('Well: walk up to the well',AGART_WELL,(15,10),{'phase':'Event'},stableFrames=15,skipIf={'storyFlag':44},group='well'))
s.append(mash('Well: throw in the Magma Key (item choice); the mountain splits (flag 44)',44,itemChoice=MAGMA_KEY,skipIf={'storyFlag':44},group='well'))
s.append(door('Well house: out',AGART_WELL,(15,15),AGART))
s.append(door('Agart: out',AGART,(17,32),OVERWORLD))
s.append(nav('Overworld: back onto the Enterprise',OVERWORLD,(108,202),{'riding':True},timeout=3600))
s.append(nav('Enterprise: over the new hole in the mountain and down (Confirm)',OVERWORLD,(111,199),{'phase':'Event'},[(110,201),(111,201)],stableFrames=15,timeout=3600))
s.append(mash('Underworld: the Red Wings and the dwarf tanks; the Enterprise crash-lands by the Dwarf Castle (flag 45, autosave)',45,until={'phase':'Player','mapId':UNDERWORLD,'storyFlag':45}))
s.append(heal('Underworld: heal',UNDERWORLD))
s.append(door('Underworld: into the Dwarf Castle',UNDERWORLD,(110,88),DWARF_GROUNDS,[(111,88)]))
s.append(door('Dwarf Castle grounds: in',DWARF_GROUNDS,(16,20),DWARF_HALL))
s.append(door('Dwarf Castle hall: up to the throne room',DWARF_HALL,(14,3),DWARF_THRONE))
s.extend(trigger('Throne room: King Giott; Cid leaves; the dolls (Calbrena), Golbez, Rydia returns (flag 46)',DWARF_THRONE,(11,11),46,battlePlan=calbrena))
s.append(equip('Throne room: Strongest on everyone (Rydia is back)',DWARF_THRONE,[0]))
s.append(formation('Throne room: formation - Rydia, Cecil, Rosa, Yang, Kain; Cecil and Yang in front',DWARF_THRONE,[RYDIA,PALADIN,ROSA,YANG,KAIN],[1,3]))
s.append(heal('Throne room: heal',DWARF_THRONE))
s.append(door('Throne room: out',DWARF_THRONE,(11,15),DWARF_HALL))
s.append(door('Dwarf Castle hall: out',DWARF_HALL,(14,16),DWARF_GROUNDS))
s.append(door('Dwarf Castle grounds: out to the Underworld',DWARF_GROUNDS,(16,33),UNDERWORLD))
s.append(checkpoint('Segment 10 complete: Golbez driven off, Rydia back, outside the Dwarf Castle in the Underworld',UNDERWORLD,46))

write('tas/routes/zot-dwarves.json','Baron → Agart → Underworld → Dwarf Castle (segment 10) — UNTESTED',s,fleeTough=True,killFirst=[CAL,BRINA])
