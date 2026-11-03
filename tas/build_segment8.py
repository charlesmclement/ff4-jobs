#!/usr/bin/env python3
"""Segment 8: the Enterprise from Baron to Toroia -> Edward and the Twin Harp
-> the Chocobo Village -> a Black Chocobo to the Cave of Magnes -> the Dark
Elf (twice) -> the Earth Crystal -> back out to the cave mouth on the
overworld (saveable).

Writes tas/routes/baron-magnes.json. Cells, doors, triggers and flags from
the local map/event data (event_chain.py, show_map.py, map_links.py); strats
from the guide (Troia Castle and Chocobo Village, Cave of Dark Elf).
"""
from route_kit import *

TOROIA_GROUNDS,EDWARD_ROOM=96,100
TOROIA_TOWN,ITEM_SHOP=86,89
CHOCOBO_VILLAGE=160
MAGNES=[192,193,195,198,199]
SAVE_ROOM,CRYSTAL_ROOM=200,201
PALADIN,CID,TELLAH,YANG=13,5,6,8
TENT,POTION,PHOENIX=21,2,9
ANTIDOTE,EYE_DROPS,ECHO_HERBS,MAIDENS_KISS,MALLET,DIET_FOOD,GOLD_NEEDLE=16,15,14,11,12,13,10
DARK_ELF,DARK_ELF_REAL=172,173
ENTERPRISE,BLACK_CHOCOBO=3,7
BARON_TOWN,WEAPON_SHOP=24,31
FIRE_CLAW,ICE_CLAW,THUNDER_CLAW=162,163,164

# Tellah is the only healer now (the pilot's Cure); he keeps MP for it and
# only casts the weakness spell on big groups. Cecil, Cid and Yang attack.
field=[
    {'character':TELLAH,'command':8,'byWeakness':True,'minEnemies':3,'minMp':60},
    {'character':TELLAH,'command':2,'reserveMp':20},
]
# Dark Elf, first fight (65000 HP, scripted: the metal seal; the guide says
# "Just lose this fight and save your energy"): everyone Defends.
first_elf=[{'character':c,'command':2,'whileMonster':DARK_ELF} for c in (PALADIN,CID,TELLAH,YANG)]
# Second fight (after Edward's harp): "Just keep pounding on this guy ...
# Tellah's magic is basically useless ... have Tellah be your healer."
real_elf=[{'character':TELLAH,'command':2,'whileMonster':DARK_ELF_REAL}]+field

s=[]
s.append(heal('Outside Baron: heal before the flight',OVERWORLD))
# Yang's claws are bought in segment 7, right after he joins (user).
# Segment 7 lands the Enterprise two tiles below the castle, (107,146).
s.append(nav('Baron: step off and back onto the Enterprise (two tiles below the castle)',OVERWORLD,(107,146),{'riding':True},[(107,147)],timeout=3600))
s.append(nav('Enterprise: north-west to Toroia; land beside the town',OVERWORLD,(41,70),{'mapId':OVERWORLD,'riding':False,'atDestination':True},
             world_path((107,146),(41,70),transport=ENTERPRISE,avoid=())))
s.append(door('Overworld: into Toroia Castle',OVERWORLD,(40,69),TOROIA_GROUNDS,[(41,69)]))
s.append(door("Toroia Castle grounds: west door to Edward's room",TOROIA_GROUNDS,(9,12),EDWARD_ROOM))
s.extend(trigger('Edward\'s sickbed: he gives the Twin Harp (flag 34)',EDWARD_ROOM,(9,5),34))
s.append(door("Edward's room: out",EDWARD_ROOM,(3,9),TOROIA_GROUNDS))
s.append(formation('Toroia: formation - Cecil, Tellah, Cid, (empty), Yang; Cecil, Cid and Yang in front',TOROIA_GROUNDS,[PALADIN,TELLAH,CID,0,YANG],[0,2,4]))
s.append(door('Toroia Castle grounds: out',TOROIA_GROUNDS,(16,32),OVERWORLD))
s.append(door('Overworld: into the town of Toroia',OVERWORLD,(40,70),TOROIA_TOWN))
s.append(door('Toroia: item shop',TOROIA_TOWN,(20,18),ITEM_SHOP))
s.append(nav('Item shop: up to the counter',ITEM_SHOP,(6,6),{'atDestination':True}))
s.append(shop('Item shop: Tents, Potions, Phoenix Downs, status cures (Tellah is the only healer)',ITEM_SHOP,
              [{'content':TENT,'count':4},{'content':POTION,'count':15},{'content':PHOENIX,'count':6},{'content':ANTIDOTE,'count':3},{'content':EYE_DROPS,'count':3},{'content':ECHO_HERBS,'count':3},{'content':MAIDENS_KISS,'count':3},{'content':MALLET,'count':3},{'content':DIET_FOOD,'count':2},{'content':GOLD_NEEDLE,'count':2}]))
s.append(door('Item shop: out',ITEM_SHOP,(6,11),TOROIA_TOWN))
s.append(door('Toroia: out',TOROIA_TOWN,(19,30),OVERWORLD))
s.append(walk_world('Overworld: north to the Chocobo Village (guide: "north of the castle of Toroia")',(41,71),(46,40),CHOCOBO_VILLAGE))
# The Black Chocobos wander (move_type 2): chase the nearest one by name.
s.append(nav('Chocobo Village: walk up to a Black Chocobo and talk to it',CHOCOBO_VILLAGE,(9,10),{'phase':'Event'},stableFrames=15,goalEntity='チョコボ(黒)'))
# Done the moment he's riding: a Confirm in the saddle would land the bird
# again at the village.
s.append(mash('Chocobo Village: mount the Black Chocobo (back on the overworld)',None,until={'mapId':OVERWORLD,'riding':True},stableFrames=1))
s.append(nav('Black Chocobo: fly to the forest below the Cave of Magnes and land (it lands only on forest)',OVERWORLD,(79,41),
             {'mapId':OVERWORLD,'riding':False,'atDestination':True},world_path((46,41),(79,41),transport=BLACK_CHOCOBO,avoid=())))
s.append(door('Overworld: into the Cave of Magnes',OVERWORLD,(79,40),MAGNES[0],facing='Up'))
s.extend(trigger('Cave of Magnes: the metal grows heavy (flag 100)',MAGNES[0],(18,9),100,battlePlan=field))
s.append(door('Magnes B1: down',MAGNES[0],(19,20),MAGNES[1],battlePlan=field))
s.append(door('Magnes B2: west passage',MAGNES[1],(10,7),MAGNES[2],battlePlan=field))
s.append(door('Magnes B3: on west',MAGNES[2],(14,6),MAGNES[3],battlePlan=field))
s.append(door('Magnes B3 (west room): down',MAGNES[3],(8,14),MAGNES[4],battlePlan=field))
s.append(door('Magnes B4: the save room (guide: "yet another chance to save")',MAGNES[4],(31,16),SAVE_ROOM,battlePlan=field))
s.append(nav('Save room: onto the save point',SAVE_ROOM,(14,7),{'atDestination':True}))
s.append(tent('Save room: Tent before the Dark Elf',SAVE_ROOM))
s.append(heal('Save room: heal anyone the Tent missed',SAVE_ROOM))
s.append(door('Save room: out',SAVE_ROOM,(14,12),MAGNES[4]))
s.append(door('Magnes B4: into the crystal room',MAGNES[4],(17,8),CRYSTAL_ROOM,battlePlan=field))
s.extend(trigger('Crystal room: the Dark Elf - lose the first fight (all Defend); Edward plays the harp (flag 97)',CRYSTAL_ROOM,(12,13),97,battlePlan=first_elf))
s.append(equip('Crystal room: Strongest on everyone (the seal is gone; guide: "equip yourself with all your metallic gear")',CRYSTAL_ROOM,[0]))
s.extend(trigger('Crystal room: the Dark Elf again, then the Dark Dragon (flag 36)',CRYSTAL_ROOM,(12,13),36,battlePlan=real_elf))
s.extend(trigger('Crystal room: take the Earth Crystal (flag 98)',CRYSTAL_ROOM,(12,10),98))
s.append(heal('Crystal room: heal',CRYSTAL_ROOM))
s.append(door('Crystal room: out',CRYSTAL_ROOM,(12,28),MAGNES[4],battlePlan=field))
s.append(door('Magnes B4: back up',MAGNES[4],(26,27),MAGNES[3],battlePlan=field))
s.append(door('Magnes B3 (west room): east',MAGNES[3],(17,14),MAGNES[2],battlePlan=field))
s.append(door('Magnes B3: up',MAGNES[2],(33,6),MAGNES[1],battlePlan=field))
s.append(door('Magnes B2: up',MAGNES[1],(32,22),MAGNES[0],battlePlan=field))
s.append(door('Magnes B1: out to the overworld',MAGNES[0],(18,7),OVERWORLD,battlePlan=field))
s.append(checkpoint('Segment 8 complete: Dark Elf defeated, Earth Crystal in hand, outside the Cave of Magnes',OVERWORLD,98))

write('tas/routes/baron-magnes.json','Baron → Toroia → Cave of Magnes (segment 8) — UNTESTED',s,fleeTough=True)
