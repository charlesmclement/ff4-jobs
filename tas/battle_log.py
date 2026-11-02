#!/usr/bin/env python3
"""Print every battle from a run's trace: enemies on screen and each decision.

  python3 tas/battle_log.py                 # latest trace
  python3 tas/battle_log.py research/tas-session/trace-<session>.jsonl

Use it to review or coach tactics: each line is "who: what (reason)".
"""
import glob, json, os, sys
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
path=sys.argv[1] if len(sys.argv)>1 else max(glob.glob(os.path.join(ROOT,'research','tas-session','trace-*.jsonl')),key=os.path.getmtime)
fight=0;last=None;lastDecision=None;inBattle=False
for line in open(path,encoding='utf-8'):
    r=json.loads(line)
    if r.get('phase')!='Battle':
        inBattle=False;continue
    if not inBattle:
        fight+=1;inBattle=True;lastDecision=None
        print(f"\nFight {fight}  map {r.get('mapId')}  step {r.get('step')}: {r.get('label')}")
    enemies,_,decision=(r.get('battle') or '').partition(' | ')
    if enemies and enemies!=last:print('   enemies:',enemies);last=enemies
    if decision and decision!=lastDecision:print('   ',decision);lastDecision=decision
