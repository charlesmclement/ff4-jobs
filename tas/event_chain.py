#!/usr/bin/env python3
"""Story timeline and event chains from the extracted event scripts.

  python3 tas/event_chain.py flags [--from 21 --to 60]
      every ScenarioFlag1 set, in flag order, with the script and map asset
  python3 tas/event_chain.py chain sc_e_0023
      follow one event: its flags, bosses, battles, choices, autosaves, and
      the scripts it hands over to (ChangeMap / Encount / EncountBoss
      continuations), recursively
  python3 tas/event_chain.py where sc_e_0023
      which map cells trigger a script (entity_default / ev_ objects)

Reads research/ff4-events (local extracted data only); prints, never writes.
"""
import argparse, csv, glob, json, os, re
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EVENTS=os.path.join(ROOT,'research','ff4-events')
MAPDATA=os.path.join(ROOT,'research','tas-maps','container','Assets','GameAssets','Serial','Res','Map')
KEEP=('SetFlag','ResetFlag','EncountBoss','Encount','Select','AutoSave','ChangeMap','ChangeScript','OpenSaveWindow','GetItem','SetVehicle','OnTheVehicle','LoseVehicle','RecoveryHP')

def scripts():
    out={}
    for f in glob.glob(os.path.join(EVENTS,'*.json')):
        base=os.path.basename(f)[:-5];asset,_,name=base.partition('__')
        out.setdefault(name,[]).append((asset,f))
    return out

def mapnames():
    ids={}
    for r in csv.DictReader(open(os.path.join(ROOT,'research','ff4-master','map'),encoding='utf-8')):ids[r['id']]=r['map_name']
    return ids

def ops(f):
    try:d=json.load(open(f,encoding='utf-8'))
    except ValueError:return []
    return [(i,m['mnemonic'],m['operands']) for i,m in enumerate(d.get('Mnemonics',[])) if 'operands' in m]

def show(n,o,names):
    iv=o['iValues'];sv=[x for x in o['sValues'] if x]
    if n in('SetFlag','ResetFlag'):return f'{n} {sv[0] if sv else ""}[{iv[0]}]'
    if n=='ChangeMap':return f'ChangeMap map {iv[0]} ({names.get(str(iv[0]),"?")}) point {iv[1]} -> {sv}'
    if n in('EncountBoss','Encount'):return f'{n} battle {iv[0]} -> {sv}'
    return f'{n} {iv[:4]} {sv}'

def flags(a):
    rows=[]
    for name,files in scripts().items():
        for asset,f in files:
            for i,n,o in ops(f):
                if n=='SetFlag' and o['sValues'][0]=='ScenarioFlag1':rows.append((o['iValues'][0],asset,name))
    for flag,asset,name in sorted(set(rows)):
        if a.lo<=flag<=a.hi:print(f'flag {flag:3}  {asset:12} {name}')

def chain(a):
    table=scripts();names=mapnames();seen=set()
    def walk(name,depth):
        if name in seen or depth>40:return
        seen.add(name)
        for asset,f in table.get(name,[]):
            print('  '*depth+f'{name} ({asset})')
            for i,n,o in ops(f):
                if n in KEEP:
                    print('  '*depth+f'   {i:4} '+show(n,o,names))
                    for s in o['sValues']:
                        if re.match(r'sc_e_\d',s or ''):walk(s,depth+1)
    walk(a.script,0)

def where(a):
    for f in glob.glob(os.path.join(MAPDATA,'Map_*','Map_*','*.json')):
        try:text=open(f,encoding='utf-8').read()
        except OSError:continue
        if a.script not in text:continue
        d=json.loads(text)
        def walk(o):
            if isinstance(o,dict):
                if isinstance(o.get('properties'),list):
                    p={x['name']:x['value'] for x in o['properties']}
                    if a.script in str(p.values()):
                        x,y=int(o['x'])//16,int(o['y'])//16-(1 if 'gid' in o else 0)
                        print(f'{os.path.basename(os.path.dirname(f))}/{os.path.basename(f)}: {o.get("name")} ({x},{y}) {p}')
                for v in o.values():walk(v)
            elif isinstance(o,list):
                for v in o:walk(v)
        walk(d)

ap=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
sub=ap.add_subparsers(dest='cmd',required=True)
p=sub.add_parser('flags');p.add_argument('--from',dest='lo',type=int,default=0);p.add_argument('--to',dest='hi',type=int,default=999)
p=sub.add_parser('chain');p.add_argument('script')
p=sub.add_parser('where');p.add_argument('script')
a=ap.parse_args();{'flags':flags,'chain':chain,'where':where}[a.cmd](a)
