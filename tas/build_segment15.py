#!/usr/bin/env python3
"""Segment 15: the drill Falcon to Mysidia -> the prayer raises the Lunar
Whale -> its crystal to the moon -> FuSoYa at the Lunar Palace -> the
Giant of Babil (the Elemental Lords, the CPU) -> Golbez and FuSoYa leave,
Kain is back -> the moon; land the Whale (saveable: the moon's map 3).

Writes tas/routes/mysidia-giant.json. Cells, doors, triggers, flags and
vehicle ids from the local map/event data; strats from the guide.

The Lunar Whale: boarding it may put the party in its interior (Earth 357,
moon 161: a crystal at (7,9), get-off doors at (2,12)/(12,12)) or in the
air. Each step that depends on which is guarded (until any / skipIf map).
"""
from route_kit import *

MOON=3
MYSIDIA=57
WHALE_EARTH,WHALE_MOON=357,161
PALACE=153
GIANT=[243,244,245,246,247,248]
GIANT_CPU=249
PALADIN,KAIN,ROSA,RYDIA,EDGE,FUSOYA=13,2,3,4,11,12
TENT=21
FALCON_DRILL,WHALE,MOON_WHALE=9,5,11
ELEMENTAL,ELEMENTAL2,CPU,DEFENSE_NODE=194,195,198,199

# Elemental Lords (the four fiends in turn): FuSoYa and Rydia cast the -ga
# spell of whatever the current fiend is weak to (guide: Firaga on Milon Z,
# Blizzaga on Rubicant, Thundaga on Cagnazzo and Barbariccia); Rosa heals.
lords=[{'character':c,'command':8,'byWeakness':True,'weaknessTier':2,'minMp':30} for c in (FUSOYA,RYDIA)]
# CPU: "destroy the Defensive system first ... always leave the ATTACKER
# alone" - route killFirst Defense Node, then the CPU.
cpu=[]

def land_whale(label,world_map,cell,start):
    s.append(nav(f'{label}: fly over the landing spot',world_map,cell,{'atDestination':True},world_path(start,cell,transport=MOON_WHALE if world_map==MOON else WHALE,avoid=(),world=world_map),stableFrames=2))
    s.append(mash(f'{label}: land (Confirm)',None,until={'phase':'Player','riding':False},stableFrames=30))
    s.append(door(f'{label}: out of the Whale (if landing left the party inside)',WHALE_MOON,(2,12),world_map,skipIf={'mapId':world_map}))

def board_whale(label,world_map,vehicle,cell,interior):
    s.append(nav(f'{label}: board the Lunar Whale',world_map,cell,{'any':[{'mapId':interior},{'riding':True}]},timeout=3600,goalVehicle=vehicle))
    s.append(mash(f'{label}: down into the Whale (Confirm)',None,until={'phase':'Player','mapId':interior},skipIf={'mapId':interior}))

s=[]
s.append(heal('Beside Agart: heal',OVERWORLD))
s.append(nav('Agart: onto the Falcon',OVERWORLD,(108,202),{'riding':True},[(107,202)],timeout=3600,goalVehicle=FALCON_DRILL))
s.append(nav('Falcon: east to Mysidia; land',OVERWORLD,(161,187),{'mapId':OVERWORLD,'riding':False,'atDestination':True},
             world_path((108,202),(161,187),transport=FALCON_DRILL,avoid=())))
s.append(door('Overworld: into Mysidia',OVERWORLD,(160,186),MYSIDIA))
s.extend(trigger('Mysidia: the elders pray; the Lunar Whale rises (flag 64, autosave)',MYSIDIA,(28,29),64))
board_whale('Outside Mysidia',OVERWORLD,WHALE,(158,185),WHALE_EARTH)
s.extend(trigger('Lunar Whale: the crystal - to the moon (flag 108, autosave)',WHALE_EARTH,(7,9),108))
s[-1]['until']={'storyFlag':108,'mapId':MOON};s[-1]['stableFrames']=60
land_whale('Moon',MOON,(23,43),(13,51))
s.append(nav('Moon: walk to the Lunar Palace',MOON,(22,37),{'phase':'Player','mapId':PALACE},world_path((23,43),(22,37),world=3)))
s.extend(trigger('Lunar Palace: FuSoYa joins (flag 65)',PALACE,(15,18),65))
s.append(equip('Lunar Palace: Strongest on everyone (FuSoYa)',PALACE,[0]))
s.append(formation('Lunar Palace: formation - Rydia, Cecil, Rosa, Edge, FuSoYa; Cecil and Edge in front',PALACE,[RYDIA,PALADIN,ROSA,EDGE,FUSOYA],[1,3]))
s.append(door('Lunar Palace: out',PALACE,(15,26),MOON))
board_whale('Moon',MOON,MOON_WHALE,(23,43),WHALE_MOON)
s.extend(trigger('Lunar Whale: the crystal - the Giant of Babil; into its mouth (flag 66, autosave)',WHALE_MOON,(7,9),66))
s.append(door('Giant: mouth',GIANT[0],(15,7),GIANT[1]))
s.append(door('Giant: neck',GIANT[1],(8,7),GIANT[2]))
s.append(door('Giant: chest',GIANT[2],(23,9),GIANT[3]))
s.append(door('Giant: stomach',GIANT[3],(7,4),GIANT[4]))
s.append(nav('Giant: inner path, to the save point',GIANT[4],(18,15),{'atDestination':True}))
s.append(tent('Giant: Tent before the Elemental Lords',GIANT[4]))
s.append(heal('Giant: heal anyone the Tent missed',GIANT[4]))
s.append(door('Giant: stairwell',GIANT[4],(25,4),GIANT[5]))
s.extend(trigger('Giant: the four fiends together - the Elemental Lords (flag 67)',GIANT[5],(7,15),67,battlePlan=lords))
s.append(door('Giant: back to the save point (guide: "go back to your save point, heal")',GIANT[5],(7,24),GIANT[4]))
s.append(nav('Giant: onto the save point',GIANT[4],(18,15),{'atDestination':True}))
s.append(tent('Giant: Tent before the CPU',GIANT[4]))
s.append(heal('Giant: heal before the CPU',GIANT[4]))
s.append(door('Giant: stairwell again',GIANT[4],(25,4),GIANT[5]))
s.append(door('Giant: up to the CPU',GIANT[5],(7,4),GIANT_CPU))
s.extend(trigger('Giant: the CPU (Defense Node first, leave the Attack Node); Golbez, FuSoYa leave, Kain is back; the moon (flag 68, autosave)',GIANT_CPU,(9,13),68,battlePlan=cpu))
s[-1]['until']={'storyFlag':68,'mapId':MOON};s[-1]['stableFrames']=60
s.append(mash('Moon: land the Whale where it stands (Confirm)',None,until={'phase':'Player','riding':False},stableFrames=30))
s.append(door('Moon: out of the Whale (if landing left the party inside)',WHALE_MOON,(2,12),MOON,skipIf={'mapId':MOON}))
s.append(heal('Moon: heal',MOON))
s.append(checkpoint('Segment 15 complete: the Giant of Babil is destroyed, Kain is back, on the moon',MOON,68))

write('tas/routes/mysidia-giant.json','Mysidia → Lunar Whale → FuSoYa → Giant of Babil (segment 15) — UNTESTED',s,fleeTough=True,
      killFirst=[DEFENSE_NODE,CPU],realFights=[ELEMENTAL,ELEMENTAL2,CPU])
