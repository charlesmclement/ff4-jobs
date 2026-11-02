#!/usr/bin/env python3
"""List doors, arrival points and event triggers for extracted maps.

Prints, per sub-map, every GotoMap (cell -> target map id / arrival point),
PointIn (arrival cell for a point id) and scripted Event/NPC object, using
cell coordinates that match the runner's (tile objects are bottom-anchored).
Use it to chain navigate steps through multi-floor dungeons and towns.

  python3 tas/map_links.py 30021          # every Map_30021_* sub-map
  python3 tas/map_links.py 20030 20031 --events
Local extracted data only (see tas/extract_maps.py).
"""
import argparse, csv, glob, json, os

ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESEARCH=os.path.join(ROOT,'research')
MAPDATA=os.path.join(RESEARCH,'tas-maps','container','Assets','GameAssets','Serial','Res','Map')

def objects(path):
    def walk(o):
        if isinstance(o,dict):
            if 'properties' in o and isinstance(o['properties'],list):yield o
            for v in o.values():yield from walk(v)
        elif isinstance(o,list):
            for v in o:yield from walk(v)
    yield from walk(json.load(open(path,encoding='utf-8')))

def cell(o):return int(o['x'])//16,int(o['y'])//16-(1 if 'gid' in o else 0)

def main():
    a=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    a.add_argument('maps',nargs='+',help='map asset numbers, e.g. 30021')
    a.add_argument('--events',action='store_true',help='also list scripted NPCs/events')
    o=a.parse_args()
    ids={}
    for r in csv.DictReader(open(os.path.join(RESEARCH,'ff4-master','map'),encoding='utf-8')):ids[r['map_name']]=r['id']
    for number in o.maps:
        for folder in sorted(glob.glob(os.path.join(MAPDATA,f'Map_{number}','Map_*'))):
            name=os.path.basename(folder)
            print(f'\n{name} = map {ids.get(name,"?")}')
            for f in sorted(glob.glob(os.path.join(folder,'*.json'))):
                base=os.path.basename(f)
                if not(base=='entity_default.json' or base.startswith('ev_')):continue
                for e in objects(f):
                    p={x['name']:x['value'] for x in e['properties']}
                    x,y=cell(e)
                    if e['name']=='GotoMap':print(f'  door ({x},{y}) -> map {p.get("map_id")} point {p.get("point_id")}')
                    elif e['name']=='PointIn':print(f'  arrive point {p.get("object_id")} at ({x},{y})')
                    elif o.events and (p.get('script_id') or e['name']=='Event'):
                        print(f'  {base}: {e["name"]} ({x},{y}) action {p.get("action_id")} script {p.get("script_id")}')

if __name__=='__main__':main()
