#!/usr/bin/env python3
"""Local-only FF4 screenshot capture and fixed-region template matching (Pillow).
No HTTP client, remote model, or network endpoint is used.
"""
import argparse, json, re, subprocess, sys, time
from datetime import datetime, timezone
from pathlib import Path
from PIL import Image, ImageChops, ImageGrab, ImageStat
DATA=Path(__file__).resolve().parent.parent/'research/tas-session'

def active_id():
    result=subprocess.check_output(['xprop','-root','_NET_ACTIVE_WINDOW'],text=True)
    found=re.search(r'0x[0-9a-fA-F]+',result)
    return int(found.group(),16) if found else None

def capture():
    windows=subprocess.check_output(['wmctrl','-l'],text=True).splitlines()
    candidates=[line.split(None,3) for line in windows if len(line.split(None,3))==4 and line.split(None,3)[3]=='FINAL FANTASY IV']
    if len(candidates)!=1:raise RuntimeError('Expected exactly one FF4 window')
    wid=candidates[0][0]
    if active_id()!=int(wid,16):raise RuntimeError('FF4 is not focused; no screenshot taken')
    geometry=subprocess.check_output(['xwininfo','-id',wid],text=True)
    def number(label):
        match=re.search(re.escape(label)+r':\s*(-?\d+)',geometry)
        if not match:raise RuntimeError('Cannot read FF4 window geometry')
        return int(match[1])
    x,y,w,h=(number(key) for key in ('Absolute upper-left X','Absolute upper-left Y','Width','Height'))
    if min(w,h)<=0:raise RuntimeError('Invalid game window size')
    image=ImageGrab.grab(bbox=(x,y,x+w,y+h)).convert('RGB')
    if active_id()!=int(wid,16):raise RuntimeError('Focus changed during capture; discarded')
    return image

def distance(actual,reference):
    if actual.size!=reference.size:raise ValueError('Template dimensions differ; use the same window size and crop')
    # Mean absolute channel error: 0 exact, 255 opposite black/white.
    difference=ImageChops.difference(actual.convert('RGB'),reference.convert('RGB'))
    return sum(ImageStat.Stat(difference).mean)/3

def analyze(image,profile,base):
    matches=[];scores={}
    for entry in profile:
        label=entry['label'];box=entry['box']
        if not isinstance(label,str) or not label or len(box)!=4:raise ValueError('Invalid template entry')
        x,y,w,h=box
        if min(x,y)<0 or min(w,h)<=0 or x+w>image.width or y+h>image.height:raise ValueError('Template crop outside game window')
        threshold=float(entry.get('maxError',5))
        if not 0<=threshold<=30:raise ValueError('maxError must be 0–30')
        with Image.open(base/entry['template']) as reference:
            score=distance(image.crop((x,y,x+w,y+h)),reference)
        scores[label]=round(score,3)
        if score<=threshold:matches.append(label)
    return matches,scores

def observe(profile_path=None):
    DATA.mkdir(parents=True,exist_ok=True)
    record={'utc':datetime.now(timezone.utc).isoformat(),'matches':[],'scores':{},'error':None}
    try:
        image=capture()
        image.save(DATA/'latest.tmp.png');(DATA/'latest.tmp.png').replace(DATA/'latest.png')
        if profile_path:
            path=Path(profile_path).resolve()
            record['matches'],record['scores']=analyze(image,json.loads(path.read_text()),path.parent)
    except (OSError,ValueError,KeyError,RuntimeError,subprocess.CalledProcessError) as e:record['error']=str(e)
    temp=DATA/'vision.tmp.json';temp.write_text(json.dumps(record,indent=2));temp.replace(DATA/'vision.json')
    return record

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile',help='JSON list of label/template/box/maxError entries')
    parser.add_argument('--watch',action='store_true')
    args=parser.parse_args()
    try:
        while True:
            result=observe(args.profile)
            if not args.watch:print(json.dumps(result,indent=2));break
            time.sleep(.25)
    except KeyboardInterrupt:pass
