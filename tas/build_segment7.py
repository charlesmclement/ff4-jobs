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
BARON_TOWN,INN,BARON_ROAD_HOUSE,ITEM_SHOP,WEAPON_SHOP=24,25,29,26,31
WATERWAY_IN,WW1,WW2,WW3,WW4,WW_SAVE=318,186,187,188,189,190
CASTLE_OUT,CASTLE_HALL,CASTLE_2F,WALL_ROOM,THRONE=4,5,6,7,8
CASTLE_CORRIDOR=9
PALADIN,TELLAH,YANG,PALOM,POROM=13,6,8,9,10
TENT=21
FIRE_CLAW,ICE_CLAW,THUNDER_CLAW=162,163,164
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
# Yang (Monk, 62000 HP; the fight ends by itself). Not Cecil alone (user:
# "you shouldn't make cecil 1 on 1 against yang"): everyone attacks, Porom
# still heals (Yang is a route realFight, so the normal heal logic runs
# instead of the scripted-fight attack-only path), and nobody spends MP on
# him - no spells, no Twincast (these rules name the twins and Tellah, which
# also blocks the twins' Twincast default).
monk=[{'character':c,'command':1,'whileMonster':MONK} for c in (PALOM,POROM,TELLAH)]
# Baigan (user's strat, live): the arms come back only after BOTH are
# dead, so: the arm nearest the party (master 170) by attacks while the
# casters Defend; then the body (route killFirst). He puts up Reflect after
# the first spell cast on him (user: "firaga needs to go off and not fira
# he reflects after the first spell"), so the one spell is Tellah's Firaga
# (he got it back at the summit) - once, as soon as the body is the target;
# Palom casts nothing at him. Then Cecil and Yang attack, the casters
# Defend, Porom heals. No Twincast or AOE (these rules block the twins'
# Twincast default).
FIRAGA=35
# Cecil attacks every turn here and leaves healing to Porom and Palom (user:
# "cecil to focus damage on the body and not heal during this fight").
# Palom backs Porom up on heal duty with Potions (user), before anything
# else he'd do. Tellah casts Firaga whenever the body is the target and has
# no Reflect on it (user: "another firaga as soon as the reflect wears off";
# the pilot reads Baigan's Reflect condition, 28, rather than watching for
# his Haste).
POTION=2
baigan=[{'character':PALADIN,'command':1,'whileMonster':BAIGAN,'first':True},
        {'character':PALOM,'command':3,'item':POTION,'targetInjured':True,'whileMonster':BAIGAN,'first':True},
        {'character':TELLAH,'command':8,'ability':FIRAGA,'minMp':30,'onTarget':BAIGAN,'whileMonster':BAIGAN,'oncePerBattle':True},
        # Later Firagas (each time his Reflect is down) only while 30 MP stay
        # for Cagnazzo's Thundaga (user: "fix it for next time" - Tellah
        # reached Cagnazzo empty). reserveMp keeps his in-fight Cures above 30.
        {'character':TELLAH,'command':8,'ability':FIRAGA,'minMp':60,'onTarget':BAIGAN,'whileMonster':BAIGAN,'reserveMp':30}]
baigan+=[{'character':c,'command':2,'whileMonster':BAIGAN} for c in (PALOM,POROM,TELLAH)]+field
# Cagnazzo (user: "real similar to mist dragon but he goes inside his
# shell", "hit him with Lit3 when he comes out, any time he's out").
# His script (sc_ai_171, research/tas-battle/ai): at 700 HP or less a hit
# sends him into "Turtle defense!" (AI mode 4: Restore, then out again);
# mode 2 is "Torrents of water begin to build!" (Tsunami charging), which a
# lightning spell breaks ("Thunder breaks the barrier!").
# In the shell (mode 4) everyone Defends, like the Mist Dragon's mist. Out
# of it: Tellah Thundaga (Lit-3), else Thundara; Palom Thundara, else
# Thunder; Cecil and Yang attack. Porom heals, else Defends (a Twincast
# would take Palom's turn from the lightning).
SHELL=[4]
# Cecil attacks instead of curing (as at Baigan, user), except in the shell.
cagnazzo=[{'character':PALADIN,'command':2,'whileMonster':CAGNAZZO,'whileModeIn':SHELL,'first':True},
          {'character':PALADIN,'command':1,'whileMonster':CAGNAZZO,'whileModeNot':SHELL,'first':True}]
cagnazzo+=[{'character':c,'command':2,'whileMonster':CAGNAZZO,'whileModeIn':SHELL} for c in (YANG,PALOM,POROM,TELLAH)]
# Tellah spams his strongest lightning (user), the Thunder Rod only once
# he's dry; Palom Thundara, then his Thunder Rod (a free Thunder: Items, Up
# to the held items - user).
THUNDER_ROD_ITEM=184
cagnazzo+=[
    {'character':TELLAH,'command':8,'ability':THUNDAGA,'minMp':30,'whileMonster':CAGNAZZO,'whileModeNot':SHELL},
    {'character':TELLAH,'command':8,'ability':THUNDARA,'minMp':15,'whileMonster':CAGNAZZO,'whileModeNot':SHELL},
    {'character':TELLAH,'command':3,'heldItem':THUNDER_ROD_ITEM,'whileMonster':CAGNAZZO,'whileModeNot':SHELL},
    {'character':PALOM,'command':8,'ability':THUNDARA,'minMp':15,'whileMonster':CAGNAZZO,'whileModeNot':SHELL},
    {'character':PALOM,'command':3,'heldItem':THUNDER_ROD_ITEM,'whileMonster':CAGNAZZO,'whileModeNot':SHELL},
    {'character':TELLAH,'command':2,'whileMonster':CAGNAZZO},
    {'character':PALOM,'command':2,'whileMonster':CAGNAZZO},
    {'character':POROM,'command':2,'whileMonster':CAGNAZZO},
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
# The weapon shop is locked before Yang (the run pushed at its door); it
# opens once he joins (user: "we should be doing it right after getting
# yang"; the town script swaps that door's layer after flag 31), so the claw
# pairs are bought on the way out of the inn.
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
# Strongest only: no Thunder Claws can be owned yet (Baron's weapon shop
# opens after Cagnazzo; segment 8 buys them).
# User: "make yang equip his claw or claws" - Strongest leaves claws off
# (attack 0 in the table), so after it he puts on whatever claws are in the
# bag, one per hand (Thunder, else Fire, else Ice).
# Claws on the moment he joins (user: "add a claw equip right after we get
# yang"): whatever claws are in the bag, Thunder first; the weapon shop
# next door then tops up the pairs and puts the Thunder pair on.
s.append(claws('Inn: Yang takes everything off, Strongest, then a claw in each hand (Thunder first)',INN,LIGHTNING,reset=[YANG]))
s.append(heal('Inn: heal after the fights',INN))
s.append(door('Inn: out',INN,(21,22),BARON_TOWN))
# Claws right after Yang (user): 2 Thunder (Cagnazzo), 2 Fire, 2 Ice; owned
# claws count. Then a claw in each hand, Thunder first (Strongest skips
# claws). Weapon shop: map 31, door (15,17), weaponsmith (11,6).
# The door is (15,17), pushed into from (15,18) (show_map's cells; an ad-hoc
# read without the tile-object offset said (15,18), live it wasn't). Before Yang
# a "can't enter" message sits on it (ev_e_0030). After Yang it is locked
# with the Baron Key, like the waterway door: the item list opens and the
# key (item 68) is one to the right of the default (user). sc_e_0156 sets
# ScenarioFlag2 15 when it opens.
s.append(nav('Baron: walk up to the locked weapon shop door',BARON_TOWN,(15,17),{'phase':'Event'},stableFrames=15,skipIf={'flag2':15},group='weaponkey'))
s.append(mash('Baron: open the weapon shop with the Baron Key (item choice)',None,itemChoice=BARON_KEY,until={'phase':'Player','flag2':15},skipIf={'flag2':15},group='weaponkey'))
s.append(door('Baron: into the weapon shop',BARON_TOWN,(15,17),WEAPON_SHOP))
# Best gear Baron sells (user: "upgrade all weapons and armor to optimal for
# everyone at this point"; master data, product groups 1 and 2):
#   Thunder Rod (atk 10, int +3) for Palom (Flame Rod atk 7) and Tellah;
#   Headband (def 1, eva 10), Silver Armlet (def 4, mdef 4) and Kenpo Gi
#   (def 5 vs Leather Garb 2) for Yang, who had no head or arms; a Silver
#   Armlet for Tellah. Cecil's Paladin set and the twins' Mysidia gear are
#   already the best on offer. Counts include what's worn (the twins wear 2
#   Silver Armlets). Thunder Claws first (Cagnazzo), the 4000-gil Gi after
#   the cheap upgrades, Fire/Ice pairs last.
THUNDER_ROD,HEADBAND,SILVER_ARMLET,KENPO_GI=184,242,298,270
s.append(nav('Weapon shop: up to the weaponsmith',WEAPON_SHOP,(11,8),{'atDestination':True}))
s.append(shop("Weaponsmith: 2 Thunder Claws, 2 Thunder Rods",WEAPON_SHOP,
              [{'content':THUNDER_CLAW,'count':2},{'content':THUNDER_ROD,'count':2}]))
s.append(nav('Weapon shop: over to the armorer',WEAPON_SHOP,(16,8),{'atDestination':True}))
s.append(shop("Armorer: Headband, 2 more Silver Armlets (4 with the twins'), Kenpo Gi",WEAPON_SHOP,
              [{'content':HEADBAND,'count':1},{'content':SILVER_ARMLET,'count':4},{'content':KENPO_GI,'count':1}]))
s.append(nav('Weapon shop: back to the weaponsmith',WEAPON_SHOP,(11,8),{'atDestination':True}))
s.append(shop("Weaponsmith: Fire and Ice Claw pairs for Yang",WEAPON_SHOP,
              [{'content':FIRE_CLAW,'count':2},{'content':ICE_CLAW,'count':2}]))
s.append(equip('Weapon shop: Strongest on everyone (new rods and armor)',WEAPON_SHOP,[0]))
s.append(claws('Weapon shop: Yang - Thunder Claws in both hands (else any claws)',WEAPON_SHOP,LIGHTNING))
s.append(door('Weapon shop: out',WEAPON_SHOP,(13,13),BARON_TOWN))
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
# Claws again right before Baigan: a resume from the castle autosave starts
# here and skips the inn (user: "why didnt yang equip he just ran right in").
s.append(any_claws('Castle hall: Yang - a claw in each hand before Baigan',CASTLE_HALL))
s.extend(trigger('Castle hall: Baigan "joins", the twins smell a monster - Baigan and his arms (flag 32)',CASTLE_HALL,(11,11),32,battlePlan=baigan))
s.append(heal('Castle hall: heal after Baigan (guide: "This fight will leave you wounded")',CASTLE_HALL))
s.append(door('Castle hall: north stairs',CASTLE_HALL,(13,2),CASTLE_2F,battlePlan=field))
s.append(door('Castle 2F: to the throne room passage',CASTLE_2F,(25,5),WALL_ROOM,battlePlan=field))
s.append(door('Passage: into the throne room',WALL_ROOM,(6,6),THRONE,battlePlan=field))
s.extend(trigger('Throne room: the King is Cagnazzo; Cid, the twins turn to stone, the Enterprise, Kain\'s message (flags 33, 107; autosave)',THRONE,(9,7),107,battlePlan=cagnazzo))
# The scene ends with the party aboard the Enterprise (SysCall "ride the
# Enterprise"), phase Transport, so the flag and map decide, not the phase.
s[-1]['until']={'storyFlag':107,'mapId':OVERWORLD};s[-1]['stableFrames']=60
# After Kain's message the party is in the Enterprise right above Baron
# Castle (107,142), and the landing step aimed there kept trying to land on
# the castle (user). Land two tiles below the castle (user), (107,146): the
# Enterprise's landing_group (transportation 3 -> group 2: attributes 3, 4,
# 6, 10, 15, 17, 18, 23) allows it; (110,147) was forest.
LANDING=(107,146)
s.append(nav('Enterprise: fly off the castle and land two tiles below it',OVERWORLD,LANDING,
             {'mapId':OVERWORLD,'riding':False},world_path((107,142),LANDING,transport=3,avoid=()),timeout=3600))
s.append(heal('Beside the Enterprise: heal',OVERWORLD))
s.append(checkpoint('Segment 7 complete: Cagnazzo defeated, the Enterprise is ours, outside Baron',OVERWORLD,107))

write('tas/routes/mysidia-baron.json','Mysidia → Baron → Cagnazzo (segment 7) — UNTESTED',s,fleeTough=True,killFirst=[LEFT_ARM,BAIGAN,RIGHT_ARM],realFights=[MONK])
