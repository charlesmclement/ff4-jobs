#!/usr/bin/env python3
"""Audit chests and item pickups on the maps a run visited.

For every map in a TAS trace (or a list of map ids), lists each entity that
grants content (treasure chests, pots, hidden pickups) with its item, count,
cell, and how far it was from the nearest cell the run actually stood on.
Run this after every segment to catch items the route walked past.

  python3 tas/audit_items.py --trace research/tas-session/trace-<session>.jsonl
  python3 tas/audit_items.py --maps 162 1

Maps not yet extracted are reported; extract them with tas/extract_maps.py.
Local data only.
"""
import argparse, csv, glob, json, os, re, sys

ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESEARCH=os.path.join(ROOT,'research')
MAPDATA=os.path.join(RESEARCH,'tas-maps','container','Assets','GameAssets','Serial','Res','Map')

def texts():
    out={}
    for f in glob.glob(os.path.join(RESEARCH,'ff4-message','*_en')):
        for line in open(f,encoding='utf-8'):
            k,_,v=line.rstrip('\n').partition('\t');out[k]=re.sub('<[^>]*>','',v)
    return out

def table(name):
    return {r['id']:r for r in csv.DictReader(open(os.path.join(RESEARCH,'ff4-master',name),encoding='utf-8'))}

def entities(path):
    def walk(o):
        if isinstance(o,dict):
            if 'properties' in o and isinstance(o['properties'],list):yield o
            for v in o.values():yield from walk(v)
        elif isinstance(o,list):
            for v in o:yield from walk(v)
    yield from walk(json.load(open(path,encoding='utf-8')))

def entity_file(map_row):
    name=map_row['map_name']                      # e.g. Map_30011_1
    group='_'.join(name.split('_')[:2])            # e.g. Map_30011
    for candidate in (os.path.join(MAPDATA,group,name,'entity_default.json'),
                      os.path.join(MAPDATA,group,group,'entity_default.json')):
        if os.path.exists(candidate):return candidate
    return None

def main():
    a=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    a.add_argument('--trace',help='trace-<session>.jsonl from a run')
    a.add_argument('--maps',nargs='*',default=[],help='map ids (master map table) to audit')
    a.add_argument('--near',type=int,default=6,help='flag items within this many cells of the path')
    o=a.parse_args()
    visited={}
    if o.trace:
        for line in open(o.trace,encoding='utf-8'):
            r=json.loads(line)
            if r.get('mapId') and r.get('x') is not None:
                visited.setdefault(str(r['mapId']),set()).add((int(r['x']),int(r['y'])))
            elif r.get('mapId'):visited.setdefault(str(r['mapId']),set())
    for m in o.maps:visited.setdefault(str(m),set())
    if not visited:sys.exit('Give --trace and/or --maps')
    maps=table('map');content=table('content');text=texts()
    missing=[]
    for map_id in sorted(visited,key=int):
        row=maps.get(map_id)
        if row is None:continue
        path=entity_file(row)
        if path is None:missing.append(f"{map_id} ({row['map_name']})");continue
        cells=visited[map_id];found=[]
        for e in entities(path):
            p={x['name']:x['value'] for x in e['properties']}
            if not p.get('content_id'):continue
            c=content.get(str(p['content_id']))
            name=text.get(c['mes_id_name'],c['mes_id_name']) if c else f"content {p['content_id']}"
            # Tile objects (with a gid) are anchored at their bottom edge.
            x,y=int(e['x'])//16,int(e['y'])//16-(1 if 'gid' in e else 0)
            dist=min((abs(x-cx)+abs(y-cy) for cx,cy in cells),default=None)
            found.append((dist if dist is not None else 9999,name,p.get('content_num',1),x,y,p.get('flag_id')))
        if not found:continue
        title=text.get(row['map_title'],'') if row['map_title']!='None' else ''
        print(f"\nmap {map_id} {row['map_name']} {title} — walked {len(cells)} cells")
        for dist,name,num,x,y,flag in sorted(found):
            note='' if dist==9999 else (f'{dist} cells from path' + ('  <-- NEAR' if dist<=o.near else ''))
            print(f"  {name} x{num} at ({x},{y}) treasure flag {flag}  {note}")
    if missing:print('\nNot extracted (run tas/extract_maps.py):',', '.join(missing))

if __name__=='__main__':main()
