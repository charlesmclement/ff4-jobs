#!/usr/bin/env python3
"""Segment 16: the moon -> the Lunar Palace -> the Lunar Subterrane down to
its last save point before the core (saveable: a save point).

Writes tas/routes/moon-subterrane.json. The door sequence through the
Subterrane is tas/door_graph.py's shortest route (the floors link back to
themselves; it checks which doors each arrival can reach on the collision
grid). Side rooms with the optional bosses are skipped.
"""
from route_kit import *
import door_graph

MOON=3
PALACE,PALACE_2F=153,154
ENTRY=296
SAVE_MID,SAVE_LAST=306,310
PALADIN,KAIN,ROSA,RYDIA,EDGE=13,2,3,4,11
TENT=21

# Lunar enemies: Rydia's -ga weakness spell on groups; Kain Jumps on big
# single enemies; Rosa heals by role.
field=[{'character':RYDIA,'command':8,'byWeakness':True,'weaknessTier':2,'minEnemies':3,'minMp':60}]

s=[]
s.append(heal('Moon: heal',MOON))
s.append(nav('Moon: walk to the Lunar Palace',MOON,(22,37),{'phase':'Player','mapId':PALACE},world_path((13,51),(22,37),world=3)))
s.append(door('Lunar Palace: up the stairs',PALACE,(15,3),PALACE_2F))
s.extend(trigger('Lunar Palace 2F: into the Lunar Subterrane (flag 69, autosave)',PALACE_2F,(12,15),69))
path=door_graph.route(door_graph.load(30251),(ENTRY,1),313)
for mid,cell,tmap,tpoint in path:
    if mid==309:break
    s.append(door(f'Lunar Subterrane: map {mid} door {cell}',mid,cell,tmap,battlePlan=field))
s.append(door('Lunar Subterrane: the save room before the core',309,(21,24),SAVE_LAST,battlePlan=field))
s.append(nav('Save room: onto the save point',SAVE_LAST,(3,9),{'atDestination':True}))
s.append(tent('Save room: Tent',SAVE_LAST))
s.append(heal('Save room: heal anyone the Tent missed',SAVE_LAST))
s.append(equip('Save room: Strongest on everyone',SAVE_LAST,[0]))
s.append(nav('Save room: back onto the save point',SAVE_LAST,(3,9),{'atDestination':True}))
s.append(checkpoint('Segment 16 complete: on the last save point of the Lunar Subterrane',SAVE_LAST,69))

write('tas/routes/moon-subterrane.json','Moon → Lunar Subterrane (segment 16) — UNTESTED',s,fleeTough=True)
