#!/usr/bin/env python3
"""Segment 14: the Falcon from Babil to the Dwarf Castle -> Luca's necklace
-> Cid fits the heat shield -> over the lava to the Sealed Cave -> the
crystal, the Demon Wall, Kain's betrayal -> the drill -> up to the surface;
land beside Agart (saveable).

Writes tas/routes/falcon-sealed-cave.json. Cells, doors, triggers, flags
and vehicle ids from the local map/event data (sc_e_0061 swaps vehicle 4
for 8, sc_e_0066 swaps 8 for 9; the drill cells take only vehicle 9);
strats from the guide (Sealed Cave).
"""
from route_kit import *

UNDERWORLD=2
DWARF_GROUNDS,DWARF_HALL,DWARF_THRONE,DWARF_WEST,DWARF_B1,HANGAR=118,119,120,121,125,126
SEALED=[268,269,271,274,281,282,284,287,288,289]
CRYSTAL_ROOM=290
PALADIN,KAIN,ROSA,RYDIA,EDGE=13,2,3,4,11
FALCON,FALCON_SHIELD,FALCON_DRILL=4,8,9
DEMON_WALL=192
JUMP=11
BIO=48
NECKLACE=74

# Demon Wall: "have Kain JUMP the EVILWALL ... have Rydia cast Bio".
wall=[{'character':KAIN,'command':JUMP,'whileMonster':DEMON_WALL},
      {'character':RYDIA,'command':8,'ability':BIO,'minMp':20,'whileMonster':DEMON_WALL}]

def castle_in(label):
    s.append(door(f'Underworld: into the Dwarf Castle ({label})',UNDERWORLD,(110,88),DWARF_GROUNDS,[(111,88)]))
    s.append(door(f'Dwarf Castle grounds: in ({label})',DWARF_GROUNDS,(16,20),DWARF_HALL))
def castle_out(label):
    s.append(door(f'Dwarf Castle hall: out ({label})',DWARF_HALL,(14,16),DWARF_GROUNDS))
    s.append(door(f'Dwarf Castle grounds: out ({label})',DWARF_GROUNDS,(16,33),UNDERWORLD))

s=[]
s.append(nav('Outside Babil: back onto the Falcon',UNDERWORLD,(58,22),{'riding':True},[(58,23)],timeout=3600,goalVehicle=FALCON))
s.append(nav('Falcon: round the lava to the Dwarf Castle; land',UNDERWORLD,(112,88),{'mapId':UNDERWORLD,'riding':False,'atDestination':True},
             world_path((58,22),(112,88),transport=FALCON,avoid=(),world=2)))
castle_in('necklace')
s.append(door('Dwarf Castle hall: up to the throne room',DWARF_HALL,(14,3),DWARF_THRONE))
s.extend(trigger("Throne room: King Giott gives Luca's necklace (flag 56)",DWARF_THRONE,(11,11),56))
s.append(door('Throne room: out',DWARF_THRONE,(11,15),DWARF_HALL))
s.append(door('Dwarf Castle hall: west door',DWARF_HALL,(4,7),DWARF_WEST))
s.append(door('West wing: up the stairs',DWARF_WEST,(17,4),DWARF_B1))
s.append(door('Upper passage: to the hangar',DWARF_B1,(9,8),HANGAR))
s.extend(trigger('Hangar: Cid is back; he fits the heat shield to the Falcon (flag 57)',HANGAR,(15,9),57))
s.append(door('Hangar: out',HANGAR,(19,14),DWARF_B1))
s.append(door('Upper passage: down',DWARF_B1,(4,9),DWARF_WEST))
s.append(door('West wing: back to the hall',DWARF_WEST,(17,13),DWARF_HALL))
castle_out('heat shield')
s.append(nav('Underworld: onto the Falcon (heat shield)',UNDERWORLD,(112,88),{'riding':True},[(111,88)],timeout=3600,goalVehicle=FALCON_SHIELD))
s.append(nav('Falcon: west over the lava to the Sealed Cave; land',UNDERWORLD,(56,116),{'mapId':UNDERWORLD,'riding':False,'atDestination':True},
             world_path((112,88),(56,116),transport=FALCON_SHIELD,avoid=(),world=2)))
s.append(door('Underworld: into the Sealed Cave',UNDERWORLD,(56,115),SEALED[0],facing='Up'))
s.append(nav('Sealed Cave: the sealed door - walk in',SEALED[0],(13,4),{'phase':'Event'},stableFrames=15,skipIf={'storyFlag':58},group='necklace'))
s.append(mash("Sealed Cave: open it with Luca's necklace (item choice) (flag 58)",58,itemChoice=NECKLACE,skipIf={'storyFlag':58},group='necklace'))
for a,b,goal,label in [(0,1,(13,4),'through the door'),(1,2,(15,8),'west door'),(2,3,(23,12),'south-east door'),(3,4,(13,25),'down'),
                       (4,5,(11,13),'on'),(5,6,(15,12),'down'),(6,7,(17,22),'on'),(7,8,(15,18),'down'),(8,9,(20,10),'down')]:
    s.append(door(f'Sealed Cave: {label}',SEALED[a],goal,SEALED[b],battlePlan=wall))
s.append(door('Sealed Cave B5: past the wall to the crystal room',SEALED[9],(16,4),CRYSTAL_ROOM,battlePlan=wall))
s.extend(trigger('Crystal room: the last Dark Crystal (flag 59)',CRYSTAL_ROOM,(11,9),59))
s.append(heal('Crystal room: heal before the way out (guide: "a trap has been set")',CRYSTAL_ROOM))
s.append(door('Crystal room: out',CRYSTAL_ROOM,(11,22),SEALED[9],battlePlan=wall))
s.extend(trigger('Sealed Cave B5: the Demon Wall (flag 60)',SEALED[9],(16,7),60,battlePlan=wall))
for a,b,goal,label in [(9,8,(16,22),'up'),(8,7,(20,2),'up'),(7,6,(15,2),'up'),(6,5,(14,7),'up'),(5,4,(16,2),'up'),
                       (4,3,(20,12),'up'),(3,2,(29,19),'up'),(2,1,(14,13),'up'),(1,0,(31,3),'up to the entrance')]:
    s.append(door(f'Sealed Cave: {label}',SEALED[a],goal,SEALED[b]))
s.extend(trigger("Sealed Cave entrance: Golbez takes Kain again; Kain leaves with the crystal (flag 61)",SEALED[0],(13,6),61))
s.append(door('Sealed Cave: out',SEALED[0],(13,13),UNDERWORLD))
s.append(nav('Underworld: back onto the Falcon',UNDERWORLD,(56,116),{'riding':True},[(57,116)],timeout=3600,goalVehicle=FALCON_SHIELD))
s.append(nav('Falcon: back to the Dwarf Castle; land',UNDERWORLD,(112,88),{'mapId':UNDERWORLD,'riding':False,'atDestination':True},
             world_path((56,116),(112,88),transport=FALCON_SHIELD,avoid=(),world=2)))
castle_in('drill')
s.append(door('Dwarf Castle hall: up to the throne room',DWARF_HALL,(14,3),DWARF_THRONE))
s.extend(trigger('Throne room: the dwarves fit the drill to the Falcon (flag 62, autosave)',DWARF_THRONE,(11,11),62))
s.append(equip('Throne room: Strongest on everyone',DWARF_THRONE,[0]))
s.append(heal('Throne room: heal',DWARF_THRONE))
s.append(door('Throne room: out',DWARF_THRONE,(11,15),DWARF_HALL))
castle_out('drill')
s.append(nav('Underworld: onto the Falcon (drill)',UNDERWORLD,(112,88),{'riding':True},[(111,88)],timeout=3600,goalVehicle=FALCON_DRILL))
s.append(nav('Falcon: north to the drill site; Confirm to drill up to the surface',UNDERWORLD,(123,22),{'phase':'Event'},
             world_path((112,88),(123,22),transport=FALCON_DRILL,avoid=(),world=2),stableFrames=15,timeout=7200))
s.append(mash('Drilling up to the surface (flag 63, autosave)',63,until={'storyFlag':63,'mapId':OVERWORLD},stableFrames=60))
s.append(nav('Falcon: land beside Agart',OVERWORLD,(108,202),{'mapId':OVERWORLD,'riding':False,'atDestination':True},
             world_path((111,199),(108,202),transport=FALCON_DRILL,avoid=()),timeout=7200))
s.append(heal('Beside Agart: heal',OVERWORLD))
s.append(checkpoint('Segment 14 complete: the Sealed Cave is done, the drill Falcon is on the surface by Agart',OVERWORLD,63))

write('tas/routes/falcon-sealed-cave.json','Falcon → Dwarf Castle → Sealed Cave → drill (segment 14) — UNTESTED',s,fleeTough=True)
