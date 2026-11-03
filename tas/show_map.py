#!/usr/bin/env python3
"""Print a dungeon/town sub-map's collision grid with doors, arrivals and
event triggers marked, in the runner's cell coordinates.

  python3 tas/show_map.py 30080            # Map_30080/Map_30080
  python3 tas/show_map.py 30110 Map_30110  # a named sub-map folder

'#' blocked, '.' open (collision value 0), D door, A arrival, E event
trigger, N scripted NPC, S save point. Local extracted data only.
"""
import glob, json, os, sys
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAPDATA=os.path.join(ROOT,'research','tas-maps','container','Assets','GameAssets','Serial','Res','Map')
number=sys.argv[1];sub=sys.argv[2] if len(sys.argv)>2 else f'Map_{number}'
folder=os.path.join(MAPDATA,f'Map_{number}',sub)
col=json.load(open(os.path.join(folder,'collision.json')))
layer=next(l for l in col['layers'] if l.get('type')=='tilelayer')
w,h,data=layer['width'],layer['height'],layer['data']
grid=[['.' if data[y*w+x]==0 else '#' for x in range(w)] for y in range(h)]
marks=[]
for f in glob.glob(os.path.join(folder,'*.json')):
    b=os.path.basename(f)
    if not(b=='entity_default.json' or b.startswith('ev_')):continue
    def walk(o):
        if isinstance(o,dict):
            if isinstance(o.get('properties'),list):
                p={x['name']:x['value'] for x in o['properties']}
                x,y=int(o['x'])//16,int(o['y'])//16-(1 if 'gid' in o else 0)
                n=o.get('name','')
                ch='D' if n=='GotoMap' else 'A' if n=='PointIn' else 'S' if 'セーブ' in n else 'E' if p.get('object_type')==2 and p.get('script_id') else 'N' if p.get('script_id') else None
                if ch and 0<=x<w and 0<=y<h:grid[y][x]=ch;marks.append((ch,x,y,b,n,p.get('script_id'),p.get('map_id'),p.get('point_id') or p.get('object_id')))
            for v in o.values():walk(v)
        elif isinstance(o,list):
            for v in o:walk(v)
    walk(json.load(open(f)))
print('    '+''.join(str(x//10) for x in range(w)));print('    '+''.join(str(x%10) for x in range(w)))
for y in range(h):print(f'{y:3} '+''.join(grid[y]))
for m in sorted(set(marks)):print(*m)
