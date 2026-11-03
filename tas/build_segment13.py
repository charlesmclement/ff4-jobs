#!/usr/bin/env python3
"""Segment 13: the hovercraft to the Cave of Eblan -> the Eblan refugees ->
the Pass to Babil (Edge and Rubicant) -> the Tower of Babil from outside ->
the King and Queen of Eblan, Rubicant -> the pitfall -> the Falcon -> down
into the Underworld; land outside Babil (saveable).

Writes tas/routes/eblan-falcon.json. Cells, doors, triggers and flags from
the local map/event data; strats from the guide (Eblan Caves And The Tower
of Babil).
"""
from route_kit import *

UNDERWORLD=2
CAVE,EBLAN_TOWN,PASS1,PASS2,PASS_SAVE=226,113,227,228,229
BABIL_OUT,BB2,BB3,BB_SAVE,BB4,BB5,TRIGGER_ROOM=234,235,236,237,238,239,321
FALL8,FALL7,FALL6=240,241,242
PALADIN,KAIN,ROSA,RYDIA,EDGE=13,2,3,4,11
TENT=21
KING,QUEEN,RUBICANT=186,187,188
HOVERCRAFT=2
JUMP=11
BIO=48

# King and Queen of Eblan (60000 HP each, scripted): "you don't have to do
# anything. Keep your party healed" - everyone Defends.
parents=[{'character':c,'command':2,'whileMonster':m} for m in (KING,QUEEN) for c in (PALADIN,KAIN,ROSA,RYDIA,EDGE)]
# Rubicant: "Have Kain JUMP ... have Cecil attack ... Rosa AIM ... have Rydia
# use Bio" (no ice: he heals when his cape is shut).
rubicant=[{'character':KAIN,'command':JUMP,'whileMonster':RUBICANT},
          {'character':RYDIA,'command':8,'ability':BIO,'minMp':20,'whileMonster':RUBICANT}]

s=[]
s.append(nav('Eblan shallows: onto the hovercraft (wherever the hook set it down)',OVERWORLD,(39,221),{'moveState':3},[(39,222)],timeout=3600,goalVehicle=HOVERCRAFT))
s.append(nav('Hovercraft: across the shallows to the cave; get off',OVERWORLD,(29,220),{'mapId':OVERWORLD,'riding':False,'atDestination':True},
             world_path((38,221),(29,220),transport=HOVERCRAFT,avoid=())))
s.append(door('Overworld: into the Cave of Eblan',OVERWORLD,(29,218),CAVE,[(29,219)],facing='Up'))
s.append(door('Cave of Eblan: down to the refugees',CAVE,(7,33),EBLAN_TOWN))
s.append(door("Eblan refugees: on to the Pass to Babil",EBLAN_TOWN,(12,6),PASS1))
s.append(door('Pass to Babil: middle passage north',PASS1,(20,9),PASS2))
s.append(door('Pass to Babil: the save room',PASS2,(34,26),PASS_SAVE))
s.append(nav('Save room: onto the save point',PASS_SAVE,(9,7),{'atDestination':True}))
s.append(tent('Save room: Tent',PASS_SAVE))
s.append(heal('Save room: heal anyone the Tent missed',PASS_SAVE))
s.append(door('Save room: out',PASS_SAVE,(9,13),PASS2))
s.extend(trigger('Pass to Babil: Edge fights Rubicant; Edge joins (flag 51)',PASS2,(29,11),51))
s.append(equip('Pass to Babil: Strongest on everyone (Edge)',PASS2,[0]))
s.append(formation('Pass to Babil: formation - Rydia, Cecil, Rosa, Edge, Kain; Cecil and Edge in front',PASS2,[RYDIA,PALADIN,ROSA,EDGE,KAIN],[1,3]))
s.append(door('Pass to Babil: north to the tower',PASS2,(27,5),BABIL_OUT))
s.append(door('Babil (outside): in at the door Edge opens',BABIL_OUT,(33,19),BB2))
s.append(door('Babil: on west',BB2,(14,19),BB3))
s.append(door('Babil: the save room (guide: "enter the first door. Inside this room is a save spot")',BB3,(27,9),BB_SAVE))
s.append(nav('Save room: onto the save point',BB_SAVE,(4,6),{'atDestination':True}))
s.append(tent('Save room: Tent before the King and Queen of Eblan',BB_SAVE))
s.append(heal('Save room: heal anyone the Tent missed',BB_SAVE))
s.append(door('Save room: out',BB_SAVE,(4,10),BB3))
s.append(door('Babil: south door',BB3,(20,25),BB4))
s.append(door('Babil: on to the hall',BB4,(27,7),BB5))
s.extend(trigger("Babil: Edge's parents (all Defend) (flag 52)",BB5,(21,20),52,battlePlan=parents))
s.extend(trigger('Babil: talk to Rubicant - Rubicant (flag 53)',BB5,(21,18),53,battlePlan=rubicant,goalEntity='ルビカンテ'))
s.append(heal('Babil: heal after Rubicant',BB5))
s.append(door('Babil: north door to the crystal room',BB5,(21,15),TRIGGER_ROOM))
s.extend(trigger('Crystal room: the pitfall - down to 8F (flag 54, autosave)',TRIGGER_ROOM,(14,20),54))
s.append(door('Babil 8F: up',FALL8,(9,20),FALL7))
s.append(door('Babil 7F: on',FALL7,(17,4),FALL6))
s.extend(trigger('Babil 6F: the Falcon; Edge takes it; out to the Underworld (flag 55, autosave)',FALL6,(16,21),55))
s[-1]['until']={'storyFlag':55,'mapId':UNDERWORLD};s[-1]['stableFrames']=60
s.append(nav('Falcon: land outside Babil',UNDERWORLD,(58,22),{'mapId':UNDERWORLD,'riding':False,'atDestination':True},timeout=3600))
s.append(heal('Outside Babil: heal',UNDERWORLD))
s.append(checkpoint('Segment 13 complete: Rubicant defeated, Edge and the Falcon, outside Babil in the Underworld',UNDERWORLD,55))

write('tas/routes/eblan-falcon.json','Eblan → Rubicant → Falcon (segment 13) — UNTESTED',s,fleeTough=True,realFights=[RUBICANT])
