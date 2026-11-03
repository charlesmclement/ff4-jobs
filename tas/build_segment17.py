#!/usr/bin/env python3
"""Segment 17: from the last save point in the Lunar Subterrane to the
core -> Zemus (FuSoYa and Golbez), Zeromus -> the Crystal -> Zeromus ->
the ending (flag 70).

Writes tas/routes/subterrane-zeromus.json. The final segment ends at the
game's ending, where there is nothing left to save. Guide (Final Battle):
"First have Cecil use the CRYSTAL ... otherwise all your attacks will miss.
Then have Cecil attack normally, have Rosa cast Holy and heal if
neccessary, have Rydia summon BAHAMUT ... and finally have Kain JUMP."
"""
from route_kit import *

SAVE_LAST=310
CORE=[309,313,314,315,316]
FINAL_ROOM=317
PALADIN,KAIN,ROSA,RYDIA,EDGE=13,2,3,4,11
ZEROMUS,ZEROMUS_REAL=201,202
CRYSTAL=59
JUMP,BAHAMUT,HOLY=11,75,23

field=[{'character':RYDIA,'command':8,'byWeakness':True,'weaknessTier':2,'minEnemies':3,'minMp':80}]
zeromus=[
    {'character':PALADIN,'item':CRYSTAL,'targetEnemies':True,'oncePerBattle':True,'whileMonster':ZEROMUS},
    {'character':KAIN,'command':JUMP,'whileMonster':ZEROMUS_REAL},
    {'character':RYDIA,'command':9,'ability':BAHAMUT,'minMp':60,'whileMonster':ZEROMUS_REAL},
    {'character':ROSA,'command':7,'ability':HOLY,'minMp':46,'whileMonster':ZEROMUS_REAL},
]+field

s=[]
s.append(heal('Save room: heal',SAVE_LAST))
s.append(door('Save room: out',SAVE_LAST,(3,16),CORE[0]))
s.append(door('Lunar Subterrane: on toward the core',CORE[0],(25,11),CORE[1],battlePlan=field))
s.append(door('Lunar Subterrane: down',CORE[1],(19,21),CORE[2],battlePlan=field))
s.append(door('Lunar Subterrane: down',CORE[2],(24,25),CORE[3],battlePlan=field))
s.append(door('Lunar Subterrane: down',CORE[3],(25,23),CORE[4],battlePlan=field))
s.append(door('Lunar Subterrane: into the core',CORE[4],(9,18),FINAL_ROOM,battlePlan=field))
s.extend(trigger('The core: Zemus, Zeromus; the Crystal; the final battle; the ending (flag 70)',FINAL_ROOM,(15,15),70,battlePlan=zeromus))
s[-1]['until']={'storyFlag':70};s[-1]['timeoutFrames']=216000
s.append({'label':'The end: FINAL FANTASY IV complete','kind':'checkpoint','phases':STORY,'until':{'storyFlag':70}})

write('tas/routes/subterrane-zeromus.json','Lunar Subterrane → Zeromus (segment 17, the end) — UNTESTED',s,fleeTough=True,
      realFights=[ZEROMUS,ZEROMUS_REAL],final=True)
