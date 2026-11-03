#!/usr/bin/env python3
"""Helpers for writing route files: the step shapes the existing segments
use (navigate / mash / menu / shop / equip / Tent / formation / checkpoint)
and overworld waypoints from plan_path's terrain planner.

Used by the build_segment*.py scripts; each writes one tas/routes/*.json.
Local data only.
"""
import json, os, sys
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0,os.path.join(ROOT,'tas'))
import plan_path

WALK=['Player','Event','Message','ChangeMap','FieldReady','Battle','FieldHelp','Gimmick','Transport']
MENU=['Player','Menu','Shop','Message','FieldReady','ChangeMap']
STORY=['None','Init','Player','Event','Message','ChangeMap','FieldReady','Battle','FieldHelp','Gimmick','Transport']
OVERWORLD=1
_world=None

def cell(x,y,layer=0):return {'x':x,'y':y,'layer':layer}

def world_path(start,goal,transport=1,avoid=(2,21),turn_cost=3):
    """Overworld waypoints (map 10010) from start to beside goal."""
    global _world
    if _world is None:_world=plan_path.load(os.path.join(ROOT,'research','tas-maps','map_10010'),transport)
    data,width,height,walk=_world
    path=plan_path.plan(data,width,height,walk,tuple(start),tuple(goal),set(avoid),turn_cost)
    if path is None:raise SystemExit(f'no overworld path {start} -> {goal}')
    cells=[p for p,_ in path]
    way=[cells[i] for i in range(1,len(cells)-1) if path[i][1]!=path[i+1][1]]
    if len(cells)>1 and (not way or way[-1]!=cells[-2]):way.append(cells[-2])
    return [cell(x,y) for x,y in way]

def nav(label,map_id,goal,until,waypoints=None,timeout=54000,**extra):
    s={'label':label,'kind':'navigate','mapId':map_id,'goal':cell(*goal),'phases':WALK,'handleBattles':True,
       'until':until,'stableFrames':2,'timeoutFrames':timeout}
    if waypoints:s['waypoints']=[w if isinstance(w,dict) else cell(*w) for w in waypoints]
    s.update(extra);return s

def door(label,map_id,goal,to_map,waypoints=None,**extra):
    """Walk to a door/stairs cell; done when the next map has control."""
    return nav(label,map_id,goal,{'phase':'Player','mapId':to_map},waypoints,**extra)

def walk_world(label,start,goal,to_map,**extra):
    return nav(label,OVERWORLD,goal,{'phase':'Player','mapId':to_map},world_path(start,goal),**extra)

def trigger(label,map_id,goal,flag,waypoints=None,battlePlan=None,choice=None,**extra):
    """Walk onto an event tile / into an NPC until the scene starts, then hold
    Confirm through it (fights included) until the story flag is set and the
    field has control (the Fabul pattern: a walk can't follow a scene that
    changes maps). Two steps."""
    plan={'battlePlan':battlePlan} if battlePlan else {}
    walk=nav(label+' - walk in',map_id,goal,{'phase':'Event'},waypoints,stableFrames=15,**plan,**extra)
    return [walk,mash(label,flag,choice=choice,**plan)]

def mash(label,flag,map_id=None,choice=None,**extra):
    u={'phase':'Player','storyFlag':flag}
    if map_id is not None:u['mapId']=map_id
    s={'label':label,'kind':'mash','keys':['Confirm'],'phases':STORY,'handleBattles':True,'until':u,
       'stableFrames':30,'periodFrames':8,'pressedFrames':2,'timeoutFrames':216000}
    if choice is not None:s['choice']=choice
    s.update(extra);return s

def menu(label,map_id,**extra):
    s={'label':label,'kind':'menu','mapId':map_id,'phases':list(MENU),'until':{'menuDone':True},'stableFrames':2,'timeoutFrames':10800}
    s.update(extra);return s

def heal(label,map_id,**extra):return menu(label,map_id,timeoutFrames=7200,**extra)

def tent(label,map_id,**extra):
    s=menu(label,map_id,menuItem=21,**extra);s['phases'].append('Event');return s

def shop(label,map_id,buy,sell=(),facing='Up',**extra):
    return menu(label,map_id,facing=facing,shop={'buy':[b if isinstance(b,dict) else {'content':b} for b in buy],'sell':list(sell)},**extra)

def equip(label,map_id,who,reset=None,**extra):
    s=menu(label,map_id,equip=list(who),**extra)
    if reset:s['equipReset']=list(reset)
    return s

def formation(label,map_id,order,front,**extra):
    return menu(label,map_id,formation={'order':list(order),'front':list(front)},**extra)

def checkpoint(label,map_id,flag):
    return {'label':label,'kind':'checkpoint','phases':['Player'],'until':{'phase':'Player','mapId':map_id,'storyFlag':flag}}

def write(path,name,steps,**route):
    """Write the route with weaknesses filled from master data (weaknesses.py)."""
    out={'schema':1,'name':name}
    out.update(route);out['steps']=steps
    # Every segment ends on saveable ground (user rule): the overworld maps
    # are the only maps with save_availability 1.
    last=steps[-1]
    assert last['kind']=='checkpoint' and last['until'].get('mapId') in(1,2,3),'segment must end on the overworld (saveable)'
    full=os.path.join(ROOT,path)
    open(full,'w',encoding='utf-8').write(json.dumps(out,indent=2,ensure_ascii=False)+'\n')
    os.system(f'python3 "{os.path.join(ROOT,"tas","weaknesses.py")}" "{full}" >/dev/null')
    print(path,len(steps),'steps')
