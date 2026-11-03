#!/usr/bin/env python3
"""Random-encounter lab: read the logs EncounterWatch writes and test models.

The plugin (EncounterWatch.cs, read-only) logs in the TAS session folder:
  encounters.jsonl  shuffle / restore / encounter events with the step
                    threshold (EncounterLot.subtractSteps), counter
                    (moveCounter), map, cell, frame and wall-clock ms
  rng.jsonl         every UnityEngine.Random.InitState(seed) and
                    Random.RandomRangeInt(min,max) -> result

Commands:
  summary   encounters per map: steps walked per battle vs the threshold
  verify    replay rng.jsonl through the Unity Xorshift128 model: after each
            InitState, do the traced RandomRangeInt results match? (A
            mismatch means the model is wrong or something else, e.g.
            Random.value / Range(float), drew in between.)
  table     threshold each seed (0..999 = DateTime.Now.Millisecond) would
            roll for a map's step range, after skipping N earlier draws

The Xorshift128 model is the community-documented UnityEngine.Random
(InitState: s0=seed, s(n+1)=s(n)*1812433253+1). It is a hypothesis until
`verify` confirms it against this build's trace.

  python3 tas/encounter_lab.py summary
  python3 tas/encounter_lab.py verify
  python3 tas/encounter_lab.py table --min 150 --max 200 --skip 0
"""
import argparse, collections, json, pathlib
ROOT=pathlib.Path(__file__).resolve().parent.parent
SESSION=ROOT/'research/tas-session'
M=0xFFFFFFFF
STEP=6  # foot_information.subtract_steps: 6 for every FF4 terrain

class UnityRandom:
    def __init__(self,seed):
        s=[seed&M]
        for _ in range(3):s.append((s[-1]*1812433253+1)&M)
        self.s=s
    def next(self):
        s0,s1,s2,s3=self.s
        t=(s0^(s0<<11))&M;t^=t>>8
        s3n=(s3^(s3>>19)^t)&M
        self.s=[s1,s2,s3,s3n]
        return s3n
    def range_int(self,lo,hi):
        if hi>lo:return lo+self.next()%(hi-lo)
        if hi<lo:return lo-self.next()%(lo-hi)
        return lo

def lines(name):
    p=SESSION/name
    if not p.is_file():raise SystemExit(f'No {p}; run the game with this build first')
    for l in p.open(encoding='utf-8',errors='replace'):
        try:yield json.loads(l)
        except ValueError:pass

def summary(_):
    last={};per=collections.defaultdict(list)
    for e in lines('encounters.jsonl'):
        if e['kind'] in('shuffle','restore'):last[e['map']]=e
        elif e['kind']=='encounter':
            s=last.get(e['map'])
            per[e['map']].append((e.get('threshold'),e.get('counter'),s and s['kind']))
    for m,rows in sorted(per.items()):
        print(f'map {m}: {len(rows)} battles')
        for t,c,src in rows:print(f'   threshold {t}  counter {c}  -> {-(-t//STEP) if t else "?"} steps after {src}')

def verify(_):
    rng=None;ok=bad=0;since=0;examples=[]
    for e in lines('rng.jsonl'):
        if e['kind']=='seed':rng=UnityRandom(e['seed']);since=0;continue
        if e['kind']!='range' or rng is None:continue
        want=rng.range_int(e['min'],e['max']);since+=1
        if want==e['result']:ok+=1
        else:
            bad+=1
            if len(examples)<10:examples.append((e['frame'],e['min'],e['max'],e['result'],want,since))
            rng=None  # out of sync until the next InitState
    total=ok+bad
    print(f'{ok}/{total} RandomRangeInt results predicted from the last seed' if total else 'No traced draws after a seed yet')
    for f,lo,hi,got,want,n in examples:print(f'   frame {f}: Range({lo},{hi}) = {got}, model {want} (draw {n} after the seed)')

def table(a):
    rolls=collections.Counter()
    for seed in range(1000):
        r=UnityRandom(seed)
        for _ in range(a.skip):r.next()
        rolls[seed]=r.range_int(a.min,a.max+1)
    best=sorted(rolls,key=lambda s:-rolls[s])[:10]
    print(f'Range({a.min},{a.max}+1) after {a.skip} draws; {STEP}/step')
    for s in best:print(f'   ms {s:3}: threshold {rolls[s]} -> battle after {-(-rolls[s]//STEP)} steps')
    worst=min(rolls.values());print(f'   worst seed: {worst} -> {-(-worst//STEP)} steps')

ap=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
sub=ap.add_subparsers(dest='cmd',required=True)
sub.add_parser('summary');sub.add_parser('verify')
t=sub.add_parser('table');t.add_argument('--min',type=int,required=True);t.add_argument('--max',type=int,required=True);t.add_argument('--skip',type=int,default=0)
a=ap.parse_args();{'summary':summary,'verify':verify,'table':table}[a.cmd](a)
