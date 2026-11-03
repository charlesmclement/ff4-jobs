#!/usr/bin/env python3
"""Shortest door-to-door route through a multi-map dungeon.

Builds a graph from the extracted maps: a node is (map, arrival point);
from it, every door reachable on the collision grid (open cells, 4-way) is
an edge to (target map, point). Prints the door cells to walk, in order.

  python3 tas/door_graph.py 30251 --start 296:1 --goal 317
  python3 tas/door_graph.py 20010,20011 --start 4:14 --goal 5   # Baron Castle
Local extracted data only. A cell is open when it is open (0) on any of the
sub-map's collision layers (Bottom, Ground, ...: towns and castles walk on
Ground, dungeons on Bottom, and stairs join them); doors and arrival cells
count as open. Each leg also prints its walk as corner cells.
"""
import argparse, collections, csv, glob, json, os
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAPDATA=os.path.join(ROOT,'research','tas-maps','container','Assets','GameAssets','Serial','Res','Map')

def load(number):
    ids={r['map_name']:int(r['id']) for r in csv.DictReader(open(os.path.join(ROOT,'research','ff4-master','map'),encoding='utf-8'))}
    maps={}
    folders=[f for n in str(number).split(',') for f in glob.glob(os.path.join(MAPDATA,f'Map_{n}','Map_*'))]
    for folder in folders:
        mid=ids.get(os.path.basename(folder))
        if mid is None or not os.path.exists(os.path.join(folder,'collision.json')):continue
        col=json.load(open(os.path.join(folder,'collision.json')))
        layers=[l for l in col['layers'] if l.get('type')=='tilelayer']
        if not layers:continue
        layer=dict(layers[0]);layer['data']=[0 if any(l['data'][i]==0 for l in layers) else 1 for i in range(len(layers[0]['data']))]
        doors=[];arrive={}
        f=os.path.join(folder,'entity_default.json')
        def walk(o):
            if isinstance(o,dict):
                if isinstance(o.get('properties'),list):
                    p={x['name']:x['value'] for x in o['properties']}
                    x,y=int(o['x'])//16,int(o['y'])//16-(1 if 'gid' in o else 0)
                    if o.get('name')=='GotoMap':doors.append(((x,y),p.get('map_id'),p.get('point_id')))
                    elif o.get('name')=='PointIn':arrive[p.get('object_id')]=(x,y)
                for v in o.values():walk(v)
            elif isinstance(o,list):
                for v in o:walk(v)
        if os.path.exists(f):walk(json.load(open(f)))
        maps[mid]={'w':layer['width'],'h':layer['height'],'data':layer['data'],'doors':doors,'arrive':arrive}
    return maps

def reachable(m,start,parents=None):
    w,h,d=m['w'],m['h'],m['data'];special={c for c,_,_ in m['doors']}|set(m['arrive'].values())
    seen={start};q=collections.deque([start])
    if parents is not None:parents[start]=None
    while q:
        x,y=q.popleft()
        for dx,dy in((1,0),(-1,0),(0,1),(0,-1)):
            n=(x+dx,y+dy)
            if not(0<=n[0]<w and 0<=n[1]<h) or n in seen:continue
            if d[n[1]*w+n[0]]==0 or n in special:
                seen.add(n)
                if parents is not None:parents[n]=(x,y)
                if n in special and d[n[1]*w+n[0]]!=0 and n not in m['arrive'].values():continue
                q.append(n)
    return seen

def route(maps,start,goal):
    prev={start:None};q=collections.deque([start])
    while q:
        node=q.popleft();mid,point=node
        if mid==goal:
            out=[]
            while prev[node]:node,door=prev[node];out.append(door)
            return out[::-1]
        m=maps.get(mid)
        if m is None or point not in m['arrive']:continue
        cells=reachable(m,m['arrive'][point])
        for cell,tmap,tpoint in m['doors']:
            if cell in cells and (tmap,tpoint) not in prev:
                prev[(tmap,tpoint)]=(node,(mid,cell,tmap,tpoint));q.append((tmap,tpoint))
    return None

if __name__=='__main__':
    a=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    a.add_argument('asset');a.add_argument('--start',required=True);a.add_argument('--goal',type=int,required=True)
    o=a.parse_args();m,p=(int(v) for v in o.start.split(':'))
    r=route(load(o.asset),(m,p),o.goal)
    if r is None:print('no route');raise SystemExit(1)
    maps=load(o.asset);node=(m,p)
    for mid,cell,tmap,tpoint in r:
        par={};reachable(maps[mid],maps[mid]['arrive'][node[1]],par)
        path=[];c=cell
        while c:path.append(c);c=par[c]
        path.reverse()
        corners=[path[0]]+[path[i] for i in range(1,len(path)-1) if (path[i][0]-path[i-1][0],path[i][1]-path[i-1][1])!=(path[i+1][0]-path[i][0],path[i+1][1]-path[i][1])]+[path[-1]]
        print(f'map {mid}: door {cell} -> map {tmap} point {tpoint}; walk {corners}')
        node=(tmap,tpoint)
