#!/usr/bin/env python3
"""Segment 7: Mysidia -> Devil's Road -> Baron town (guards, Yang) -> the
Old Waterway -> Baron Castle (Baigan, Cagnazzo) -> the twins, Cid, the
Enterprise -> the overworld beside the airship outside Baron (saveable).

Writes tas/routes/mysidia-baron.json. Cells, doors, triggers and flags from
the local map/event data (tas/event_chain.py, tas/show_map.py); strats from
the guide (Return To Baron, Waterway, Return To Baron Castle).
"""
from route_kit import *

MYSIDIA,DEVILS_ROAD=57,66
BARON_TOWN,INN,BARON_ROAD_HOUSE=24,25,29
WATERWAY_IN,WW1,WW2,WW3,WW4,WW_SAVE=318,186,187,188,189,190
CASTLE_OUT,CASTLE_HALL,CASTLE_2F,WALL_ROOM,THRONE=4,5,6,7,8
PALADIN,TELLAH,YANG,PALOM,POROM=13,6,8,9,10
TENT=21
MONK,BAIGAN,RIGHT_ARM,LEFT_ARM,CAGNAZZO=205,168,169,170,171
FIRA,THUNDARA,THUNDAGA=34,40,41
BARON_KEY=68

# Ordinary fights: Palom's Fira on groups, else the weakness spell; Tellah
# keeps 60 MP for the bosses (Thundaga is 30); Porom heals, else Defends;
# Cecil and Yang attack.
field=[
    {'character':PALOM,'command':8,'ability':FIRA,'targetAll':True,'minEnemies':3,'minMp':15},
    {'character':PALOM,'command':8,'byWeakness':True,'minMp':5},
    {'character':PALOM,'command':2},
    {'character':TELLAH,'command':8,'byWeakness':True,'minEnemies':3,'minMp':65},
    {'character':TELLAH,'command':2,'reserveMp':60},
    {'character':POROM,'command':2},
]
# Yang (Monk, 62000 HP: a scripted fight that ends by itself). Guide: his
# Kick hits everyone; the casters Defend through it, Cecil attacks.
monk=[{'character':c,'command':2,'whileMonster':MONK} for c in (PALOM,POROM,TELLAH)]
# Baigan: he Reflects himself (guide), so magic goes at the arms, which
# do most of the damage ("Quickly eliminate the ARMs"); route killFirst
# sends the attackers at the arms too. With the arms gone the casters Defend.
baigan=[]
for arm in (RIGHT_ARM,LEFT_ARM):
    baigan+=[{'character':PALOM,'command':8,'ability':FIRA,'minMp':15,'whileMonster':arm},
             {'character':TELLAH,'command':8,'ability':THUNDARA,'minMp':15,'whileMonster':arm}]
baigan+=[{'character':PALOM,'command':2,'whileMonster':BAIGAN},{'character':TELLAH,'command':2,'whileMonster':BAIGAN}]+field
# Cagnazzo: weak to lightning. Guide: "have Tellah use LIT-3 ... then just
# keep attacking him".
cagnazzo=[
    {'character':TELLAH,'command':8,'ability':THUNDAGA,'minMp':30,'whileMonster':CAGNAZZO},
    {'character':PALOM,'command':8,'ability':THUNDARA,'minMp':15,'whileMonster':CAGNAZZO},
    {'character':PALOM,'command':8,'ability':39,'minMp':5,'whileMonster':CAGNAZZO},
]+field

s=[]
s.append(heal('Outside Mysidia: heal before Baron',OVERWORLD))
s.append(walk_world('Overworld: into Mysidia',(159,187),(159,186),MYSIDIA))
s.append(door("Mysidia: to the Devil's Road",MYSIDIA,(37,18),DEVILS_ROAD))
s.extend(trigger("Devil's Road: across to Baron (flag 30, autosave)",DEVILS_ROAD,(5,6),30))
s.append(door("Baron: out of the Devil's Road house",BARON_ROAD_HOUSE,(6,15),BARON_TOWN))
s.append(door('Baron: into the inn',BARON_TOWN,(20,27),INN))
s.extend(trigger('Inn: talk to Yang - two Baron Guards, then Yang himself; he joins with the Baron Key (flag 31)',INN,(21,5),31,battlePlan=monk+field))
s.append(formation('Inn: formation - Palom, Cecil, Porom, Yang, Tellah; Cecil and Yang in front',INN,[PALOM,PALADIN,POROM,YANG,TELLAH],[1,3]))
s.append(heal('Inn: heal after the fights',INN))
s.append(door('Inn: out',INN,(21,22),BARON_TOWN))
s.append(nav('Baron: west to the locked waterway door - walk in',BARON_TOWN,(4,20),{'phase':'Event'},stableFrames=15))
s.append(mash('Baron: open it with the Baron Key (item choice)',None,itemChoice=BARON_KEY,until={'phase':'Player','flag2':16}))
s.append(door('Baron: through the unlocked door',BARON_TOWN,(4,20),WATERWAY_IN))
s.append(door('Waterway entrance: down the stairs',WATERWAY_IN,(3,5),WW1))
s.append(door('Old Waterway 1: east to the next stairs',WW1,(32,6),WW2,battlePlan=field))
s.append(door('Old Waterway 2: north-east stairs',WW2,(35,2),WW3,battlePlan=field))
s.append(door('Old Waterway 3: north stairs',WW3,(13,2),WW4,battlePlan=field))
s.append(door('Old Waterway 4: the save room (guide: "immediately head for the door and in there you will find a save room")',WW4,(7,9),WW_SAVE,battlePlan=field))
s.append(nav('Save room: onto the save point',WW_SAVE,(9,7),{'atDestination':True}))
s.append(tent('Save room: Tent before the castle',WW_SAVE))
s.append(heal('Save room: heal anyone the Tent missed',WW_SAVE))
s.append(door('Save room: back out',WW_SAVE,(9,13),WW4))
s.append(door('Old Waterway 4: up into Baron Castle',WW4,(19,2),CASTLE_OUT,battlePlan=field))
s.append(door('Baron Castle: round the walls to the inner hall',CASTLE_OUT,(16,15),CASTLE_HALL,battlePlan=field))
s.extend(trigger('Castle hall: Baigan "joins", the twins smell a monster - Baigan and his arms (flag 32)',CASTLE_HALL,(11,11),32,battlePlan=baigan))
s.append(heal('Castle hall: heal after Baigan (guide: "This fight will leave you wounded")',CASTLE_HALL))
s.append(door('Castle hall: north stairs',CASTLE_HALL,(13,2),CASTLE_2F,battlePlan=field))
s.append(door('Castle 2F: to the throne room passage',CASTLE_2F,(25,5),WALL_ROOM,battlePlan=field))
s.append(door('Passage: into the throne room',WALL_ROOM,(6,6),THRONE,battlePlan=field))
s.extend(trigger('Throne room: the King is Cagnazzo; Cid, the twins turn to stone, the Enterprise, Kain\'s message (flags 33, 107; autosave)',THRONE,(9,7),107,battlePlan=cagnazzo))
# The scene ends with the party aboard the Enterprise (SysCall "ride the
# Enterprise"), phase Transport, so the flag and map decide, not the phase.
s[-1]['until']={'storyFlag':107,'mapId':OVERWORLD};s[-1]['stableFrames']=60
s.append(nav('Enterprise: land and step off outside Baron (Confirm at the spot, like the hovercraft)',OVERWORLD,(107,142),
             {'mapId':OVERWORLD,'riding':False},timeout=3600))
s.append(heal('Beside the Enterprise: heal',OVERWORLD))
s.append(checkpoint('Segment 7 complete: Cagnazzo defeated, the Enterprise is ours, outside Baron',OVERWORLD,107))

write('tas/routes/mysidia-baron.json','Mysidia → Baron → Cagnazzo (segment 7) — UNTESTED',s,fleeTough=True,killFirst=[RIGHT_ARM,LEFT_ARM])
