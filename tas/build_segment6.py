#!/usr/bin/env python3
"""Segment 6: Fabul harbor -> Leviathan -> Mysidia -> Mount Ordeals (Milon,
Milon Z, the Dark Knight trial) -> Paladin -> back to Mysidia's Elder (the
Devil's Road opens) -> the overworld outside Mysidia (saveable).

Writes tas/routes/fabul-mysidia.json. Cells, doors, triggers and flags come
from the local event/map data (tas/event_chain.py, tas/show_map.py); strats
from the guide (research/guides/ff4-snes-walkthrough.txt, Mysidia and
Mt. Ordeals).
"""
from route_kit import *

# Map ids (master map table) and character_status ids.
MYSIDIA,MANOR,WEAPONS,ARMOR,ITEMS=57,58,61,62,65
ENTRANCE,RIDGE,PASS,SUMMIT=182,183,184,185
CECIL,PALADIN,TELLAH,PALOM,POROM=1,13,6,9,10
TWINCAST=18
TENT,POTION,PHOENIX=21,2,9
MILON,MILON_Z,DARK_KNIGHT=166,167,206
FIRA=34

# Palom: Fira on groups, else the weakness spell; Tellah the same while he
# keeps MP for the bosses; Porom heals (the pilot's Cure / Cure-on-undead),
# else Defends; Cecil attacks. Mt. Ordeals is undead country (weak to fire).
ordeals=[
    {'character':PALOM,'command':8,'ability':FIRA,'targetAll':True,'minEnemies':2,'minMp':15},
    {'character':PALOM,'command':8,'byWeakness':True,'minMp':5},
    {'character':PALOM,'command':2},
    {'character':TELLAH,'command':8,'byWeakness':True,'minEnemies':3,'minMp':40},
    {'character':TELLAH,'command':2,'reserveMp':30},
    {'character':POROM,'command':2},
]
# Guide (Milon): "Palom use his FIRE2 spell against everybody and have
# Tellah use his FIRE1 spell as well. Always have Cecil attack Milon. Use
# Porom as your healer." Milon Z: "If you have Fire2, use it ... have Porom
# and Tellah be the healers and have Cecil attack". Dark Knight: "All you
# have to do is survive this fight ... defend for about three rounds".
milon=[
    {'character':PALOM,'command':8,'ability':FIRA,'targetAll':True,'minMp':15,'whileMonster':MILON},
    {'character':TELLAH,'command':8,'ability':33,'targetAll':True,'minMp':5,'whileMonster':MILON},
]+ordeals
# Milon Z: Porom Twincasts with Palom when nobody needs healing (user: Cure
# on him "seems underwhelming ... better to use twin"; the guide has Porom
# and Tellah heal and Palom cast Fira, and Porom's heals still come first).
# Palom's turn goes into the Twincast too; Fira when it can't be done.
milon_z=[
    {'character':POROM,'command':TWINCAST,'minMp':20,'whileMonster':MILON_Z},
    {'character':PALOM,'command':8,'ability':FIRA,'minMp':15,'whileMonster':MILON_Z},
    {'character':PALOM,'command':8,'ability':33,'minMp':5,'whileMonster':MILON_Z},
    {'character':TELLAH,'command':8,'ability':33,'minMp':5,'whileMonster':MILON_Z},
]+ordeals
trial=[
    {'character':PALADIN,'command':2,'whileMonster':DARK_KNIGHT},
    {'character':CECIL,'command':2,'whileMonster':DARK_KNIGHT},
]
# Twins' grind (guide: they start at level 10 with ~100 HP; Palom learns
# Fira at 12, learning table). Rest when Porom can't Cure or Palom can't Fira.
TWINS={'levelAtLeast':{str(PALOM):12,str(POROM):12}}
LOW={'mpBelow':{str(POROM):6,str(PALOM):15}}

s=[]
s.append(heal('Outside Fabul: heal before the voyage',OVERWORLD))
s.extend(trigger('Overworld: to Fabul harbor; the voyage, Leviathan, Cecil alone on the beach (flag 22, autosave)',OVERWORLD,(226,43),22,
                 world_path((220,46),(226,43))))
s.append(walk_world('Beach: east to Mysidia',(150,187),(159,186),MYSIDIA))
s.append(door('Mysidia: north to the Manor of Prayers (no talking to the townsfolk: they turn Cecil into a pig or toad)',MYSIDIA,(28,9),MANOR))
s.extend(trigger('Manor: talk to the Elder; Palom and Porom join (flag 23)',MANOR,(15,6),23))
s.append(door('Manor: back out to the town',MANOR,(15,13),MYSIDIA))
s.append(door('Mysidia: weapon shop',MYSIDIA,(19,24),WEAPONS))
s.append(nav('Weapon shop: up to the counter',WEAPONS,(5,6),{'atDestination':True}))
s.append(shop('Weapon shop: Flame Rod for Palom, Healing Staff for Porom (guide)',WEAPONS,[182,190]))
s.append(door('Weapon shop: out',WEAPONS,(5,11),MYSIDIA))
s.append(door('Mysidia: armor shop',MYSIDIA,(21,24),ARMOR))
s.append(nav('Armor shop: up to the counter',ARMOR,(5,6),{'atDestination':True}))
s.append(shop("Armor shop: Gaia Gear, Wizard's Hats and Silver Armlets for the twins (guide: \"every Gaea equipment you can buy\")",ARMOR,
              [{'content':266,'count':2},{'content':245,'count':2},{'content':298,'count':2}]))
s.append(equip('Armor shop: Strongest on Palom and Porom',ARMOR,[PALOM,POROM]))
s.append(door('Armor shop: out',ARMOR,(5,11),MYSIDIA))
s.append(door('Mysidia: item shop',MYSIDIA,(39,27),ITEMS))
s.append(nav('Item shop: up to the counter',ITEMS,(6,6),{'atDestination':True}))
s.append(shop('Item shop: Tents for the Mt. Ordeals rests, Potions, Phoenix Downs',ITEMS,
              [{'content':TENT,'count':4},{'content':POTION,'count':10},{'content':PHOENIX,'count':5}]))
s.append(door('Item shop: out',ITEMS,(6,11),MYSIDIA))
s.append(door('Mysidia: out by the south gate',MYSIDIA,(28,33),OVERWORLD))
s.append(walk_world("Overworld: the long walk east to Mt. Ordeals (guide: \"its a long walk\")",(159,187),(223,186),ENTRANCE))

# Twins' grind just inside the entrance, resting outside with a Tent.
def grind(n):
    g={} if n==1 else {'skipIf':TWINS,'group':f'ordeals{n}'}
    s.append(nav(f'Mt. Ordeals entrance: twins grind {n} - random battles until Palom and Porom reach 12 (Fira), or their MP runs low',
                 ENTRANCE,(16,31),{'phase':'Player','any':[TWINS,LOW]},[(20,30)],timeout=216000,loop=True,battlePlan=ordeals,**g))
    rest={'group':f'ordeals{n}'} if n>1 else {}
    s.append(nav(f'Mt. Ordeals entrance: out to rest ({n})',ENTRANCE,(20,32),{'phase':'Player','mapId':OVERWORLD},timeout=5400,facing='Down',**rest))
    s.append(tent(f'Outside Mt. Ordeals: Tent ({n})',OVERWORLD,**rest))
    s.append(door(f'Overworld: back into Mt. Ordeals ({n})',OVERWORLD,(223,186),ENTRANCE,[(223,187)],**rest))
for n in range(1,4):grind(n)

s.extend(trigger('Mt. Ordeals: the burning trees; Palom and Porom put the fire out (flag 24)',ENTRANCE,(11,29),24,battlePlan=ordeals))
s.append(door('Mt. Ordeals entrance: north to the ridge',ENTRANCE,(19,10),RIDGE,battlePlan=ordeals))
s.extend(trigger('Ridge: Tellah again; he joins (flag 25)',RIDGE,(21,12),25,battlePlan=ordeals))
s.append(formation('Ridge: formation - Palom, Cecil, Porom, (empty), Tellah; only Cecil in front',RIDGE,[PALOM,CECIL,POROM,0,TELLAH],[1,3]))
s.append(door('Ridge: north to the pass',RIDGE,(24,7),PASS,battlePlan=ordeals))
s.append(door('Pass: up to the summit',PASS,(11,7),SUMMIT,battlePlan=ordeals))
s.append(nav('Summit: to the save point (the twins hear something on the first step)',SUMMIT,(28,13),{'atDestination':True},[(18,20)],battlePlan=ordeals))
s.append(tent('Summit save point: Tent before Milon (guide: "Save your game and heal your wounds")',SUMMIT))
s.append(heal('Summit save point: heal anyone the Tent missed',SUMMIT))
s.extend(trigger('Summit: the bridge - Milon and four Revenants (flag 26)',SUMMIT,(17,7),26,battlePlan=milon))
s.append(nav('Summit: back to the save point (guide)',SUMMIT,(28,13),{'atDestination':True},battlePlan=ordeals))
s.append(tent('Summit save point: Tent before Milon Z',SUMMIT))
s.append(heal('Summit save point: heal before Milon Z',SUMMIT))
s.extend(trigger('Summit: Milon Z attacks from behind (back attack: Row) (flag 27)',SUMMIT,(12,7),27,battlePlan=milon_z))
s.extend(trigger('Summit: the mirror - the Dark Knight trial, Cecil Defends until it ends; Cecil becomes a Paladin (flag 28)',SUMMIT,(9,6),28,battlePlan=trial))
s.append(equip('Summit: Strongest on everyone (Paladin Cecil)',SUMMIT,[0]))
s.append(door('Summit: back down to the pass',SUMMIT,(18,18),PASS,battlePlan=ordeals))
s.append(door('Pass: down to the ridge',PASS,(23,22),RIDGE,battlePlan=ordeals))
s.append(door('Ridge: down to the entrance',RIDGE,(13,23),ENTRANCE,battlePlan=ordeals))
s.append(door('Mt. Ordeals entrance: out to the overworld',ENTRANCE,(20,32),OVERWORLD,battlePlan=ordeals,facing='Down'))
s.append(tent('Outside Mt. Ordeals: Tent (guide: "outside you can save and use a TENT")',OVERWORLD))
s.append(walk_world('Overworld: back west to Mysidia',(223,187),(159,186),MYSIDIA,battlePlan=ordeals))
s.append(door('Mysidia: north to the Manor of Prayers',MYSIDIA,(28,9),MANOR))
s.extend(trigger('Manor: the Elder sees the Paladin; the twins stay with Cecil, the Devil\'s Road opens (flag 29)',MANOR,(15,6),29))
s.append(door('Manor: back out to the town',MANOR,(15,13),MYSIDIA))
s.append(door('Mysidia: armor shop',MYSIDIA,(21,24),ARMOR))
s.append(nav('Armor shop: up to the counter',ARMOR,(5,6),{'atDestination':True}))
s.append(shop('Armor shop: Paladin gear for Cecil, cheapest first - Light Shield, Gauntlets, Light Helm, Knight\'s Armor (guide)',ARMOR,[231,299,252,275]))
s.append(equip('Armor shop: Strongest on everyone',ARMOR,[0]))
s.append(door('Armor shop: out',ARMOR,(5,11),MYSIDIA))
s.append(door('Mysidia: item shop',MYSIDIA,(39,27),ITEMS))
s.append(nav('Item shop: up to the counter',ITEMS,(6,6),{'atDestination':True}))
s.append(shop('Item shop: restock Potions, Phoenix Downs, Tents',ITEMS,
              [{'content':POTION,'count':10},{'content':PHOENIX,'count':5},{'content':TENT,'count':3}]))
s.append(door('Item shop: out',ITEMS,(6,11),MYSIDIA))
s.append(door('Mysidia: out by the south gate',MYSIDIA,(28,33),OVERWORLD))
s.append(checkpoint('Segment 6 complete: Cecil is a Paladin, the Devil\'s Road is open, outside Mysidia',OVERWORLD,29))

write('tas/routes/fabul-mysidia.json','Fabul → Mysidia → Mt. Ordeals (segment 6) — UNTESTED',s,fleeTough=True,killFirst=[MILON])
