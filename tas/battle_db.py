#!/usr/bin/env python3
"""Build the local battle database: every monster and every spell/ability.

Reads the extracted master tables and English text (research/ff4-master,
research/ff4-message) and writes:
  research/tas-battle/battle.json   loaded by the runner (copied into the
                                    TAS session by tas.py launch)
  research/tas-battle/monsters.md   readable table, grouped by map, for
                                    reviewing and coaching fights
  research/tas-battle/spells.md

Attribute ids: 1 fire, 2 ice, 3 lightning, 4 dark, 5 holy, 6 projectile.
attribute_group type 1 and 401 carry the "Vulnerable to ..." message, so they
mark weaknesses; other types (2, 3, 4, 5, 402, 403) are kept raw as resistances.
Proprietary-derived output stays in the ignored research/ tree.

  python3 tas/battle_db.py
"""
import csv, glob, json, os, re
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
R=os.path.join(ROOT,'research');M=os.path.join(R,'ff4-master');OUT=os.path.join(R,'tas-battle')
ELEMENT={1:'fire',2:'ice',3:'lightning',4:'dark',5:'holy',6:'projectile'}
GROUP={1:'White Magic',2:'Black Magic',3:'Summon',4:'Ninjutsu'}

def table(name):return list(csv.DictReader(open(os.path.join(M,name),encoding='utf-8')))
def text():
    out={}
    for f in glob.glob(os.path.join(R,'ff4-message','*_en')):
        for line in open(f,encoding='utf-8'):
            k,_,v=line.rstrip('\n').partition('\t');out[k]=re.sub('<[^>]*>','',v)
    return out

def main():
    t=text();content={r['id']:r for r in table('content')}
    attrs={}
    for r in table('attribute_group'):attrs.setdefault(r['group_id'],[]).append((int(r['attribute_id']),r['type_id']))
    item=lambda cid:t.get(content[cid]['mes_id_name'],cid) if cid in content else None
    # Spells: content rows of type 4 point at ability ids and carry the name.
    names={}
    for c in content.values():
        if c['type_id']=='4':names.setdefault(c['type_value'],t.get(c['mes_id_name'],''))
    spells={}
    for a in table('ability'):
        g=int(a['ability_group_id'])
        if g not in GROUP and a['id'] not in names:continue
        rng=int(a['battle_renge_id'])
        spells[a['id']]={'name':names.get(a['id'],''),'group':GROUP.get(g,'ability'),'mp':int(a['use_value']),
            'element':ELEMENT.get(int(a['attribute_id'])),'range':rng,
            # battle range 6 = single, can switch to all; 4 = all enemies.
            'canTargetAll':rng==6,'allEnemies':rng==4,'power':int(a['standard_value'])}
    monsters={}
    for m in table('monster'):
        weak=[];resist=[]
        for el,typ in attrs.get(m['resistance_attribute'],[]):
            (weak if typ in('1','401') else resist).append({'element':ELEMENT.get(el,el),'type':typ} if typ not in('1','401') else ELEMENT.get(el,el))
        monsters[m['id']]={'name':t.get(m['mes_id_name'],''),'hp':int(m['hp']),'level':int(m['lv']),
            'defense':int(m['defense']),'magicDefense':int(m['ability_defense']),'agility':int(m['agility']),
            'undead':m['species']=='4','boss':m['boss']!='0','weak':weak,'resist':resist,
            # Weak to projectile (arrows): the flyers - Zu, Roc, Cockatrice,
            # Gargoyle, bats, eyes. Rosa's bow exploits it.
            'flying':any(el==6 and typ in('1','401') for el,typ in attrs.get(m['resistance_attribute'],[])),
            'weakIds':sorted({el for el,typ in attrs.get(m['resistance_attribute'],[]) if typ in('1','401') and el in(1,2,3)}),
            'steal':[item(c) for c in (m['steal_content_id1'],m['steal_content_id2']) if c!='0' and item(c)],
            'exp':int(m['exp']),'gil':int(m['gill']),'maps':[]}
    # Where each monster appears: map.monster_set_id -> parties -> monsters.
    parties={r['id']:r for r in table('monster_party')}
    sets={r['id']:r for r in table('monster_set')}
    maps=table('map')
    for mp in maps:
        s=sets.get(mp['monster_set_id'])
        if not s:continue
        for i in range(1,13):
            pid=s.get(f'monster_set{i}')
            if not pid or pid=='0' or pid not in parties:continue
            p=parties[pid]
            for j in range(1,10):
                mid=p.get(f'monster{j}')
                if mid and mid!='0' and mid in monsters and mp['id'] not in monsters[mid]['maps']:monsters[mid]['maps'].append(mp['id'])
    # Encounter groups (EncounterLot): a monster set lists parties with
    # per-mille rates; the roll Random.Range(1,1001) picks the first party
    # whose cumulative range holds it (verified live 3/3). sets: [end, party].
    groups={}
    for sid,s in sets.items():
        cum=0;rows=[]
        for i in range(1,17):
            pid,rate=s.get(f'monster_set{i}','0'),int(s.get(f'monster_set{i}_rate') or 0)
            if pid=='0' or rate==0:continue
            cum+=rate;rows.append([cum,int(pid)])
        if rows:groups[sid]=rows
    party_monsters={pid:[int(p[f'monster{j}']) for j in range(1,10) if p.get(f'monster{j}') not in(None,'','0')] for pid,p in parties.items()}
    os.makedirs(OUT,exist_ok=True)
    db={'monsters':monsters,'spells':spells,'elements':ELEMENT,'sets':groups,'parties':party_monsters,
        'mapSets':{mp['id']:int(mp['monster_set_id']) for mp in maps if mp['monster_set_id']!='0'}}
    json.dump(db,open(os.path.join(OUT,'battle.json'),'w',encoding='utf-8'),ensure_ascii=False)
    with open(os.path.join(OUT,'monsters.md'),'w',encoding='utf-8') as f:
        f.write('| id | name | HP | def | mdef | weak | resist | undead | boss | steal | maps |\n|---|---|---|---|---|---|---|---|---|---|---|\n')
        for mid,m in sorted(monsters.items(),key=lambda kv:int(kv[0])):
            if not m['name']:continue
            f.write(f"| {mid} | {m['name']} | {m['hp']} | {m['defense']} | {m['magicDefense']} | {', '.join(m['weak'])} | "
                    f"{', '.join(str(r['element'])+'/'+r['type'] for r in m['resist'])} | {'yes' if m['undead'] else ''} | {'yes' if m['boss'] else ''} | "
                    f"{', '.join(m['steal'])} | {' '.join(m['maps'][:12])} |\n")
    with open(os.path.join(OUT,'spells.md'),'w',encoding='utf-8') as f:
        f.write('| id | name | group | MP | element | target |\n|---|---|---|---|---|---|\n')
        for sid,s in sorted(spells.items(),key=lambda kv:int(kv[0])):
            if not s['name']:continue
            f.write(f"| {sid} | {s['name']} | {s['group']} | {s['mp']} | {s['element'] or ''} | {'all enemies' if s['allEnemies'] else 'single or all' if s['canTargetAll'] else s['range']} |\n")
    print(f"{len(monsters)} monsters, {len(spells)} spells -> {OUT}")

if __name__=='__main__':main()
