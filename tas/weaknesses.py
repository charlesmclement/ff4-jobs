#!/usr/bin/env python3
"""Write Route.Weaknesses into route files from local master data.

monster.resistance_attribute -> attribute_group rows; type 1 and 401 mark a
weakness (Octomammoth lightning = 1, Red Mousse fire = 401). Only fire (1),
ice (2) and lightning (3) are kept: the elements Black Magic can hit.

  python3 tas/weaknesses.py tas/routes/mist-octomammoth.json [more routes]
"""
import csv, json, os, sys
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MASTER=os.path.join(ROOT,'research','ff4-master')
groups={}
for r in csv.DictReader(open(os.path.join(MASTER,'attribute_group'),encoding='utf-8')):
    if r['type_id'] in ('1','401') and r['attribute_id'] in ('1','2','3'):
        groups.setdefault(r['group_id'],[]).append(int(r['attribute_id']))
weak={}
for m in csv.DictReader(open(os.path.join(MASTER,'monster'),encoding='utf-8')):
    if m['resistance_attribute'] in groups:weak[m['id']]=sorted(set(groups[m['resistance_attribute']]))
for path in sys.argv[1:]:
    route=json.load(open(path,encoding='utf-8'));route['weaknesses']=weak
    open(path,'w',encoding='utf-8').write(json.dumps(route,indent=2,ensure_ascii=False)+'\n')
    print(path,len(weak),'monsters')
