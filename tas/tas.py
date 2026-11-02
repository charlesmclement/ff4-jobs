#!/usr/bin/env python3
"""Control the opt-in scripted FF4 runner. Never operates the normal save prefix."""
import argparse, hashlib, json, os, shutil, subprocess, sys, time
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
GAME=ROOT.parent/'SteamLibrary/steamapps/common/FINAL FANTASY IV PR'
DATA=ROOT/'research/tas-session'
PREFIX=Path('/dev/shm/ff4-tas-proton')
PROTON=Path('/home/desktop/.steam/debian-installation/steamapps/common/Proton - Experimental/proton')

def state(require_fresh=True):
    value=json.loads((DATA/'status.json').read_text())
    from datetime import datetime, timezone
    stamp=datetime.fromisoformat(value['utc'].replace('Z','+00:00'))
    if require_fresh and (datetime.now(timezone.utc)-stamp).total_seconds()>30:raise RuntimeError('Runner status is stale; launch the TAS session first')
    return value

def request(command,route=None):
    s=state(require_fresh=command!='stop');payload={'session':s['session'],'command':command}
    if route is not None:payload['route']=route
    path=DATA/'request.json';temp=DATA/'request.tmp'
    temp.write_text(json.dumps(payload));temp.replace(path)

def game_running():
    for p in Path('/proc').glob('[0-9]*/cmdline'):
        try:
            if any(part.lower().replace(b'\\',b'/').endswith(b'/final fantasy iv.exe') for part in p.read_bytes().split(b'\0')):return True
        except (OSError,PermissionError):pass
    return False

def environment(smoke):
    env=os.environ.copy()
    env.update(STEAM_COMPAT_DATA_PATH=str(PREFIX),STEAM_COMPAT_CLIENT_INSTALL_PATH='/home/desktop/.steam/debian-installation',
               SteamAppId='1173800',SteamGameId='1173800',WINEDLLOVERRIDES='winhttp=n,b',WINEDEBUG='-all',
               FF4JOBS_TEST_MODE='0',FF4JOBS_STARTUP_SMOKE='0',FF4JOBS_EQUIPMENT_PROBE='0',FF4JOBS_TAS='1',
               FF4JOBS_TAS_DIR='Z:'+str(DATA).replace('/','\\'),FF4JOBS_TAS_SMOKE='1' if smoke else '0')
    return env

def launch(route_path,smoke):
    if game_running():raise RuntimeError('Close FF4 normally first; this launcher will not interrupt your game')
    expected=json.loads((ROOT/'verified-game.json').read_text())['exe_sha256']
    if hashlib.sha256((GAME/'GameAssembly.dll').read_bytes()).hexdigest()!=expected:raise RuntimeError('Unverified game executable')
    build=ROOT/'tas/FF4Tas.Plugin/bin/Release/net6.0'
    for name in ('FF4Tas.dll','FF4Tas.Core.dll'):
        if not (build/name).is_file():raise RuntimeError('Run tas/build.sh first')
    DATA.mkdir(parents=True,exist_ok=True)
    PREFIX.mkdir(exist_ok=True)
    env=environment(smoke)
    # Create only a new Proton prefix; reuse the one installed game directory.
    with (DATA/'prefix.log').open('a') as log:
        subprocess.run([str(PROTON),'run','cmd','/c','exit','0'],env=env,stdout=log,stderr=log,check=True)
        docs=PREFIX/'pfx/drive_c/users/steamuser/Documents'
        docs.mkdir(parents=True,exist_ok=True)
        persistent=DATA/'documents';persistent.mkdir(exist_ok=True)
        if docs.is_symlink():
            if docs.resolve()!=persistent.resolve():raise RuntimeError('Unexpected TAS Documents symlink')
        else:
            if not docs.resolve().is_relative_to(PREFIX.resolve()):raise RuntimeError('Unexpected TAS Documents path')
            shutil.copytree(docs,persistent,dirs_exist_ok=True)
            shutil.rmtree(docs);docs.symlink_to(persistent,target_is_directory=True)
        for key in (r'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders',r'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders'):
            subprocess.run([str(PROTON),'run','reg','add',key,'/v','Personal','/t','REG_SZ','/d',r'C:\users\steamuser\Documents','/f'],env=env,stdout=log,stderr=log,check=True)
    target=GAME/'BepInEx/plugins/FF4Tas';target.mkdir(exist_ok=True)
    for name in ('FF4Tas.dll','FF4Tas.Core.dll'):
        temp=target/(name+'.installing');temp.write_bytes((build/name).read_bytes());temp.replace(target/name)
    for name in ('status.json','request.json','smoke.json'):(DATA/name).unlink(missing_ok=True)
    args=['-batchmode','-nographics'] if smoke else ['-screen-fullscreen','0','-screen-width','1280','-screen-height','720']
    route=json.loads(Path(route_path).read_text()) if route_path else None
    print('TAS save directory:',docs/'My Games/FINAL FANTASY IV PR',flush=True)
    if route:print('Select New Game. The opening prototype starts when the game enters Event. F10 stops it.',flush=True)
    with (DATA/'game.log').open('w') as log:
        child=subprocess.Popen([str(PROTON),'run',str(GAME/'FINAL FANTASY IV.exe'),*args],cwd=GAME,env=env,stdout=log,stderr=log)
        try:
            started=False;last=None;deadline=time.monotonic()+90
            while child.poll() is None:
                if smoke and time.monotonic()>deadline:raise RuntimeError('TAS startup smoke check timed out')
                if (DATA/'status.json').exists():
                    s=state(require_fresh=False)
                    if route and not started and s['phase']=='Event' and s['focused']:
                        request('start',route);started=True
                    display=(s['status'],s['step'],s['label'],s['reason'],s['error'])
                    if display!=last:print(display,flush=True);last=display
                time.sleep(.1)
        except KeyboardInterrupt:
            if (DATA/'status.json').exists():request('stop')
            print('Route stop requested. Close the TAS game normally when finished.')
            return
        if smoke:
            report=json.loads((DATA/'smoke.json').read_text())
            if not report.get('passed'):raise RuntimeError(str(report))
            print(json.dumps(report,indent=2))
        elif child.returncode:raise RuntimeError(f'TAS game exited with {child.returncode}; see {DATA / "game.log"}')

p=argparse.ArgumentParser(description=__doc__);sub=p.add_subparsers(dest='command',required=True)
a=sub.add_parser('launch');a.add_argument('--route');a.add_argument('--smoke',action='store_true')
a=sub.add_parser('start');a.add_argument('route');a.add_argument('--delay',type=float,default=5,help='seconds to refocus FF4')
sub.add_parser('stop');sub.add_parser('status')
a=p.parse_args()
try:
    if a.command=='launch':launch(a.route,a.smoke)
    elif a.command=='start':
        route=json.loads(Path(a.route).read_text());print(f'Refocus the TAS game within {a.delay:g} seconds.',flush=True);time.sleep(max(0,a.delay));request('start',route)
    elif a.command=='stop':request('stop')
    else:print(json.dumps(state(),indent=2))
except (OSError,ValueError,RuntimeError,subprocess.CalledProcessError) as e:
    print('TAS:',e,file=sys.stderr);sys.exit(1)
