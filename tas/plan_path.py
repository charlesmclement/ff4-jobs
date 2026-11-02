#!/usr/bin/env python3
"""Plan route waypoints offline from an extracted map's terrain attributes.

The native MapRouteSearcher cannot plan on maps without a collision layer
(the overworld, map_10010). Those maps carry a 256x256 GroundAttribute grid;
master table transportation_group says which attribute values each vehicle can
cross (row 1 = on foot, columns typeN = attribute N). This plans a 4-direction
path that prefers few turns and prints the turn cells as route "waypoints".

Example (Baron Castle gate -> Mist Cave):
  python3 tas/plan_path.py --map research/tas-maps/map_10010 \
      --start 107,145 --goal 81,119 --show

Attribute 2 (castle/town footprints) and 21 (cave mouths) are avoided except
at the goal so the path does not wander into another entrance. Bridges (18)
are walkable. Uses only local extracted data; nothing is sent anywhere.
"""
import argparse, csv, heapq, json, os, sys

ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SYMBOLS={16:'~',19:'^',4:'.',9:'"',7:',',15:'T',3:'_',17:'r',22:'/',23:'w',10:'%',2:'C',18:'*',21:'O',11:'b',12:'B'}

def cell(text):
    x,y=(int(v) for v in text.split(','))
    return x,y

def load(map_dir,transport):
    layer=json.load(open(os.path.join(map_dir,'attribute.json')))['layers'][0]
    table=os.path.join(ROOT,'research','ff4-master','transportation_group')
    row=next(r for r in csv.DictReader(open(table)) if r['id']==str(transport))
    walk={int(k.replace('type_','').replace('type','')) for k,v in row.items() if k!='id' and v=='1'}
    return layer['data'],layer['width'],layer['height'],walk

def plan(data,width,height,walk,start,goal,avoid,turn_cost):
    at=lambda p:data[p[1]*width+p[0]]
    allowed=walk-avoid
    dirs=[(1,0),(-1,0),(0,1),(0,-1)]
    queue=[(0,start,-1)];best={(start,-1):0};prev={}
    while queue:
        cost,p,d=heapq.heappop(queue)
        if p==goal:
            path=[];state=(p,d)
            while state in prev:path.append(state);state=prev[state]
            path.append((start,-1));path.reverse();return path
        if best.get((p,d),1e18)<cost:continue
        for i,(dx,dy) in enumerate(dirs):
            q=(p[0]+dx,p[1]+dy)
            if not(0<=q[0]<width and 0<=q[1]<height):continue
            if q!=goal and at(q) not in allowed:continue
            n=cost+1+(turn_cost if d not in(-1,i) else 0)
            if n<best.get((q,i),1e18):best[(q,i)]=n;prev[(q,i)]=(p,d);heapq.heappush(queue,(n,q,i))
    return None

def main():
    a=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    a.add_argument('--map',required=True,help='extracted map directory containing attribute.json')
    a.add_argument('--start',type=cell,required=True,help='x,y start cell (where the player stands)')
    a.add_argument('--goal',type=cell,required=True,help='x,y goal cell (e.g. an entrance tile)')
    a.add_argument('--transport',type=int,default=1,help='transportation_group row (1 = on foot)')
    a.add_argument('--avoid',default='2,21',help='attribute values to avoid except at the goal')
    a.add_argument('--turn-cost',type=int,default=3,help='extra cost per turn; higher = fewer waypoints')
    a.add_argument('--layer',type=int,default=0)
    a.add_argument('--show',action='store_true',help='print an ASCII map with the path')
    o=a.parse_args()
    data,width,height,walk=load(o.map,o.transport)
    avoid={int(v) for v in o.avoid.split(',') if v}
    path=plan(data,width,height,walk,o.start,o.goal,avoid,o.turn_cost)
    if path is None:sys.exit('No walkable path; check --start/--goal or --avoid')
    cells=[p for p,_ in path]
    waypoints=[cells[i] for i in range(1,len(cells)-1) if path[i][1]!=path[i+1][1]]
    # End beside the goal so the runner steps into the trigger tile last.
    if len(cells)>1 and (not waypoints or waypoints[-1]!=cells[-2]):waypoints.append(cells[-2])
    if o.show:
        on=set(cells);xs=[c[0] for c in cells];ys=[c[1] for c in cells]
        for y in range(max(0,min(ys)-3),min(height,max(ys)+4)):
            print('%4d '%y+''.join('#' if (x,y) in on else SYMBOLS.get(data[y*width+x],'?')
                for x in range(max(0,min(xs)-3),min(width,max(xs)+4))),file=sys.stderr)
    print(f'{len(cells)-1} steps, {len(waypoints)} waypoints',file=sys.stderr)
    print(json.dumps([{'x':x,'y':y,'layer':o.layer} for x,y in waypoints],indent=2))

if __name__=='__main__':main()
