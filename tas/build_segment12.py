#!/usr/bin/env python3
"""Segment 12: the Enterprise from Agart to Baron -> Cid's engineers fit
the hook (flag 50) -> lift the hovercraft below Mount Hobs -> carry it to
the shallows east of the Cave of Eblan and land (saveable).

Writes tas/routes/babil-hook.json. Guide (Modified Airship): "To attach the
Hovercraft, simply go over it and press the "A" button ... go southwest of
Baron and land". The Cave of Eblan has only four walkable cells around it
(the hovercraft crosses the shallows, segment 13); the nearest cells the
hook Enterprise can land on are east of them (landing_group 9).
"""
from route_kit import *

CASTLE_OUT=4
ENTERPRISE,HOOK_ENTERPRISE,HOVERCRAFT=3,10,2

s=[]
s.append(heal('Beside Agart: heal',OVERWORLD))
s.append(nav('Agart: back onto the Enterprise',OVERWORLD,(108,202),{'riding':True},[(107,202)],timeout=3600,goalVehicle=ENTERPRISE))
s.append(nav('Enterprise: north to Baron; land outside the castle',OVERWORLD,(107,146),{'mapId':OVERWORLD,'riding':False,'atDestination':True},
             world_path((108,202),(107,146),transport=ENTERPRISE,avoid=())))
s.append(door('Overworld: into Baron Castle',OVERWORLD,(107,144),CASTLE_OUT,[(107,145)]))
s.extend(trigger("Baron Castle: Cid's engineers below the east tower fit the hook (flag 50, autosave)",CASTLE_OUT,(27,21),50))
# The scene ends aboard the hook Enterprise (vehicle 10) outside Baron.
s[-1]['until']={'storyFlag':50,'mapId':OVERWORLD};s[-1]['stableFrames']=60
s.append(nav('Hook Enterprise: north-east to the hovercraft below Mount Hobs (where segment 4 left it)',OVERWORLD,(157,37),{'atDestination':True},
             world_path((107,145),(157,37),transport=HOOK_ENTERPRISE,avoid=()),goalVehicle=HOVERCRAFT,stableFrames=2))
s.append({'label':'Hook Enterprise: Confirm over the hovercraft to hook it','kind':'hold','keys':['Confirm'],'frames':3,'phases':WALK,'timeoutFrames':600})
s.append({'label':'Hook Enterprise: wait for the hook','kind':'wait','phases':WALK,'until':{'riding':True},'stableFrames':240,'timeoutFrames':3600})
s.append(nav('Hook Enterprise: south-west to the shallows by the Cave of Eblan; land (the hovercraft comes down with it)',OVERWORLD,(38,221),
             {'mapId':OVERWORLD,'riding':False,'atDestination':True},world_path((157,37),(38,221),transport=HOOK_ENTERPRISE,avoid=())))
s.append(heal('By the Eblan shallows: heal',OVERWORLD))
s.append(checkpoint('Segment 12 complete: hook fitted, hovercraft carried to the Eblan shallows',OVERWORLD,50))

write('tas/routes/babil-hook.json','Agart → Baron hook → hovercraft to Eblan (segment 12) — UNTESTED',s,fleeTough=True)
