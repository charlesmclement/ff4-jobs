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
BARON_TOWN,INN,BARON_ROAD_HOUSE,ITEM_SHOP=24,25,29,26
WATERWAY_IN,WW1,WW2,WW3,WW4,WW_SAVE=318,186,187,188,189,190
CASTLE_OUT,CASTLE_HALL,CASTLE_2F,WALL_ROOM,THRONE=4,5,6,7,8
CASTLE_CORRIDOR=9
PALADIN,TELLAH,YANG,PALOM,POROM=13,6,8,9,10
TENT=21
POTION,PHOENIX,GOLD_NEEDLE,MAIDENS_KISS,EYE_DROPS,ANTIDOTE=2,9,10,11,15,16
MONK,BAIGAN,RIGHT_ARM,LEFT_ARM,CAGNAZZO=205,168,169,170,171
FIRA,THUNDARA,THUNDAGA=34,40,41
TWINCAST=18
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
# Baigan (live, segment 7): the arms grow back at full HP (444) a few turns
# after they die, so the guide's "Quickly eliminate the ARMs" became a
# treadmill and the party wiped. Everything goes at Baigan: the attackers
# (route killFirst), and both twins Twincast (the guide: twin magic "cannot
# be reflected", so his Wall doesn't matter; the first Comet took ~950 off
# him and both arms). Only the first Twincast landed while Porom alone chose
# it and Palom took his own turns, so Palom chooses it too. Tellah heals by
# role and otherwise casts nothing (spells at Baigan bounce off his Wall).
baigan=[{'character':POROM,'command':TWINCAST,'minMp':20,'whileMonster':BAIGAN},
        {'character':PALOM,'command':TWINCAST,'minMp':20,'whileMonster':BAIGAN},
        {'character':TELLAH,'command':2,'whileMonster':BAIGAN}]+field
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
# Item shop before Yang (user: "do this first and buy a little of each
# status item"; the guards Mini people). Baron stocks Potions, Phoenix Downs,
# Gold Needles, Maiden's Kisses, Eye Drops, Antidotes, Tents (product group 3);
# no Mallets, Echo Herbs or Diet Food here. Never Remedies (user: "spendy").
s.append(door('Baron: into the item shop',BARON_TOWN,(13,27),ITEM_SHOP))
s.append(nav('Item shop: up to the counter',ITEM_SHOP,(6,6),{'atDestination':True}))
s.append(shop('Item shop: status cures - Antidotes, Eye Drops, Maiden\'s Kisses, Gold Needles - and Potions, Phoenix Downs',ITEM_SHOP,
              [{'content':ANTIDOTE,'count':3},{'content':EYE_DROPS,'count':3},{'content':MAIDENS_KISS,'count':3},
               {'content':POTION,'count':10},{'content':PHOENIX,'count':5},{'content':GOLD_NEEDLE,'count':2}]))
s.append(door('Item shop: out',ITEM_SHOP,(6,11),BARON_TOWN))
s.append(door('Baron: into the inn',BARON_TOWN,(20,27),INN))
# Rest first (user: "can you rest in the inn before the fight?"): the twins
# and Tellah came off the Devil's Road with 1 MP, so nobody could Esuna the
# guards' Mini. The innkeeper is behind the counter at (21,14); the counter
# relay is (21,15).
s.extend(inn('Inn: rest before Yang',INN,(21,15)))
s.extend(trigger('Inn: talk to Yang - two Baron Guards, then Yang himself; he joins with the Baron Key (flag 31)',INN,(21,5),31,battlePlan=monk+field))
s.append(formation('Inn: formation - Palom, Cecil, Porom, Yang, Tellah; Cecil and Yang in front',INN,[PALOM,PALADIN,POROM,YANG,TELLAH],[1,3]))
s.append(heal('Inn: heal after the fights',INN))
s.append(door('Inn: out',INN,(21,22),BARON_TOWN))
s.append(nav('Baron: west to the locked waterway door - walk in',BARON_TOWN,(4,20),{'phase':'Event'},stableFrames=15))
s.append(mash('Baron: open it with the Baron Key (item choice)',None,itemChoice=BARON_KEY,until={'phase':'Player','flag2':16}))
s.append(door('Baron: through the unlocked door',BARON_TOWN,(4,20),WATERWAY_IN))
s.append(door('Waterway entrance: down the stairs',WATERWAY_IN,(3,5),WW1))
s.append(door('Old Waterway 1: east to the next stairs',WW1,(32,6),WW2,battlePlan=field))
# The arrival (6,4) is on a raised walkway (Ground layer); the stairs down to
# the floor (Bottom) are at x=9. The native search doesn't route across
# layers, so the walk bounced between (9,2) and (9,3): walk round and down
# the stairs first, then the floor route to the far stairs is found.
# On the floor the native search also failed (it found only short partial
# legs and bounced between (29,8) and (30,9)): the whole floor path, corners
# from a BFS over the map's BottomCollision layer.
s.append(door('Old Waterway 2: north-east stairs',WW2,(35,2),WW3,
              [(6,2),(9,2),(9,7),(18,7),(18,6),(19,6),(19,5),(21,5),(21,9),(30,9),(30,11),(31,11),(31,12),(35,12),(35,3)],battlePlan=field))
s.append(door('Old Waterway 3: north stairs',WW3,(13,2),WW4,battlePlan=field))
s.append(door('Old Waterway 4: the save room (guide: "immediately head for the door and in there you will find a save room")',WW4,(7,9),WW_SAVE,battlePlan=field))
s.append(nav('Save room: onto the save point',WW_SAVE,(9,7),{'atDestination':True}))
s.append(tent('Save room: Tent before the castle',WW_SAVE))
s.append(heal('Save room: heal anyone the Tent missed',WW_SAVE))
s.append(door('Save room: back out',WW_SAVE,(9,13),WW4))
s.append(door('Old Waterway 4: up into Baron Castle',WW4,(19,2),CASTLE_OUT,battlePlan=field))
# The hall door (16,15) is reached only through the side corridor (map 9):
# heading straight for it, the walk stepped into the barracks door (14,11)
# (map 12). Route from tas/door_graph.py 20010,20011 --start 4:14 --goal 5.
s.append(door('Baron Castle: west and down the outer wall to the side corridor',CASTLE_OUT,(12,19),CASTLE_CORRIDOR,
              [(4,3),(4,32),(7,32),(7,24),(12,24)],battlePlan=field))
s.append(door('Side corridor: up and back out beside the hall',CASTLE_CORRIDOR,(3,3),CASTLE_OUT,battlePlan=field))
s.append(door('Baron Castle: into the inner hall',CASTLE_OUT,(16,15),CASTLE_HALL,[(12,15)],battlePlan=field))
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

write('tas/routes/mysidia-baron.json','Mysidia → Baron → Cagnazzo (segment 7) — UNTESTED',s,fleeTough=True,killFirst=[BAIGAN])
