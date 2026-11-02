#!/usr/bin/env python3
"""Control the opt-in scripted FF4 runner. Never operates the normal save prefix."""
import argparse, hashlib, json, os, shutil, subprocess, sys, time
from datetime import datetime, timezone
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

def steam_running():
    # steam.pid and steam.pipe can survive shutdown: inspect live processes.
    for p in Path('/proc').glob('[0-9]*/comm'):
        try:
            if p.read_text().strip()=='steam' and '\nState:\tZ' not in (p.parent/'status').read_text():return True
        except (OSError,PermissionError):pass
    return False

def ensure_steam():
    if steam_running():return
    executable=shutil.which('steam')
    if not executable and Path('/usr/games/steam').is_file():executable='/usr/games/steam'
    if not executable:raise RuntimeError('Steam is not running. Open Steam and sign in, then retry this shortcut.')
    print('Starting Steam first. Sign in if Steam asks you to.',flush=True)
    with (DATA/'steam-startup.log').open('a') as log:
        subprocess.Popen([executable,'-silent'],stdout=log,stderr=log,start_new_session=True)
    deadline=time.monotonic()+60
    while time.monotonic()<deadline:
        if steam_running():
            # A process is not proof of authentication/API readiness. Give a
            # newly started client a short settling interval and retain logs.
            time.sleep(5)
            if steam_running():return
        time.sleep(.5)
    raise RuntimeError('Steam did not stay running. Open Steam, finish any update/sign-in, then retry. See steam-startup.log.')

def archive_previous_logs():
    sources=[DATA/name for name in ('game.log','unity.log','status.json','exit.json','launcher-error.txt','attempt.json','BepInEx-last.log')]
    sources.append(GAME/'BepInEx/LogOutput.log')
    existing=[p for p in sources if p.is_file()]
    if not existing:return
    folder=DATA/'logs'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S.%fZ')
    folder.mkdir(parents=True)
    for p in existing:shutil.copy2(p,folder/p.name)

def write_exit(child,started):
    report={'utc':datetime.now(timezone.utc).isoformat(),'returnCode':child.returncode,
            'routeStarted':started,'steamRunning':steam_running()}
    try:report['lastStatus']=state(require_fresh=False)
    except (OSError,ValueError):pass
    (DATA/'exit.json').write_text(json.dumps(report,indent=2))
    source=GAME/'BepInEx/LogOutput.log'
    if source.is_file():shutil.copy2(source,DATA/'BepInEx-last.log')

def environment(smoke):
    env=os.environ.copy()
    env.update(STEAM_COMPAT_DATA_PATH=str(PREFIX),STEAM_COMPAT_CLIENT_INSTALL_PATH='/home/desktop/.steam/debian-installation',
               SteamAppId='1173800',SteamGameId='1173800',WINEDLLOVERRIDES='winhttp=n,b',WINEDEBUG='-all,+seh',
               FF4JOBS_TEST_MODE='0',FF4JOBS_STARTUP_SMOKE='0',FF4JOBS_EQUIPMENT_PROBE='0',FF4JOBS_TAS='1',
               FF4JOBS_TAS_DIR='Z:'+str(DATA).replace('/','\\'),FF4JOBS_TAS_SMOKE='1' if smoke else '0')
    return env

def launch(route_path,smoke,resume=False):
    if game_running():raise RuntimeError('Close FF4 normally first; this launcher will not interrupt your game')
    expected=json.loads((ROOT/'verified-game.json').read_text())['exe_sha256']
    if hashlib.sha256((GAME/'GameAssembly.dll').read_bytes()).hexdigest()!=expected:raise RuntimeError('Unverified game executable')
    build=ROOT/'tas/FF4Tas.Plugin/bin/Release/net6.0'
    for name in ('FF4Tas.dll','FF4Tas.Core.dll'):
        if not (build/name).is_file():raise RuntimeError('Build first with tas/build.sh --build-only')
    route=json.loads(Path(route_path).read_text()) if route_path else None
    DATA.mkdir(parents=True,exist_ok=True)
    archive_previous_logs()
    for name in ('unity.log','exit.json','launcher-error.txt','BepInEx-last.log'):
        (DATA/name).unlink(missing_ok=True)
    (DATA/'attempt.json').write_text(json.dumps({'utc':datetime.now(timezone.utc).isoformat(),
        'steamRunningBeforeLaunch':steam_running(),'route':route.get('name') if route else None},indent=2))
    ensure_steam()
    # Steam startup can bring back another game. Recheck before touching DLLs.
    if game_running():raise RuntimeError('FF4 is already running; close it normally before using this shortcut')
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
    args+=['-logFile','Z:'+str(DATA/'unity.log').replace('/','\\')]
    print('TAS save directory:',docs/'My Games/FINAL FANTASY IV PR',flush=True)
    if route and resume:print('Load your save. The route starts from where you stand once you have control. F10 stops it.',flush=True)
    elif route:print('Select New Game. The scripted route starts when the game enters Event. F10 stops it.',flush=True)
    with (DATA/'game.log').open('w') as log:
        child=subprocess.Popen([str(PROTON),'run',str(GAME/'FINAL FANTASY IV.exe'),*args],cwd=GAME,env=env,stdout=log,stderr=log)
        try:
            started=False;last=None;deadline=time.monotonic()+90
            while child.poll() is None:
                if smoke and time.monotonic()>deadline:raise RuntimeError('TAS startup smoke check timed out')
                if (DATA/'status.json').exists():
                    s=state(require_fresh=False)
                    if route and not started and s['focused'] and (s['phase']=='Event' if not resume else s['phase']=='Player' and s.get('mapId')):
                        # resume: the player loaded a save; start from the step for this map.
                        started=True
                        if not resume:request('start',route)
                        else:
                            time.sleep(1)
                            try:start_route(route,auto_step(route))
                            except (RuntimeError,ValueError) as e:print('TAS: route not started:',e,flush=True)
                    display=(s['status'],s['step'],s['label'],s['reason'],s['error'])
                    if display!=last:print(display,flush=True);last=display
                time.sleep(.1)
        except KeyboardInterrupt:
            if (DATA/'status.json').exists():request('stop')
            print('Route stop requested. Close the TAS game normally when finished.')
            return
        write_exit(child,started)
        if smoke:
            report=json.loads((DATA/'smoke.json').read_text())
            if not report.get('passed'):raise RuntimeError(str(report))
            print(json.dumps(report,indent=2))
        elif child.returncode:raise RuntimeError(f'TAS game exited with {child.returncode}; see {DATA / "game.log"}')
        elif route and not started:
            raise RuntimeError('FF4 closed before the route started. If this was unexpected, check Steam is signed in. Startup evidence is in unity.log, game.log and exit.json.')

def auto_step(route):
    """1-based step to resume at, from the live map id, cell and story flags.

    Steps up to the last story flag already set are done; steps past the first
    unset flag aren't reachable yet. Among reachable navigate steps on the
    current map, the one whose next target (first waypoint, else goal) is
    nearest the player wins; the step choice is printed for checking."""
    s=state();m,x,y=s.get('mapId'),s.get('x'),s.get('y');flags=set(s.get('storyFlags') or [])
    best=None;blocked=False
    for i,step in enumerate(route['steps'],1):
        flag=(step.get('until') or {}).get('storyFlag')
        if not blocked and step.get('mapId')==m and step.get('kind')=='navigate' and flag not in flags:
            target=(step.get('waypoints') or [step.get('goal')])[0]
            d=0 if x is None else abs(target['x']-x)+abs(target['y']-y)
            if best is None or d<best[0]:best=(d,i)
        if flag is not None and flag not in flags:blocked=True
        # A satisfied story flag means every step up to here is done.
        elif flag is not None and not blocked:best=None
    if best is None:raise RuntimeError(f'No route step starts on map {m} with story flags {sorted(flags)}; stand where the segment begins')
    return best[1]

def start_route(route,from_step,delay=0):
    route=json.loads(json.dumps(route))
    if not 1<=from_step<=len(route.get('steps',[])):raise ValueError('Step is outside the route')
    print(f'Starting at step {from_step}: {route["steps"][from_step-1]["label"]}',flush=True)
    if from_step>1:
        route['steps']=route['steps'][from_step-1:]
        route['name']+=f' (resume at step {from_step})'
    # Focus loss pauses the runner, so starting while another window is
    # focused is safe: it waits until the game is clicked.
    if delay>0:print(f'Refocus the TAS game within {delay:g} seconds.',flush=True)
    time.sleep(max(0,delay));request('start',route)

def start(path,from_step,delay):
    route=json.loads(Path(path).read_text())
    start_route(route,from_step or auto_step(route),delay)

p=argparse.ArgumentParser(description=__doc__);sub=p.add_subparsers(dest='command',required=True)
a=sub.add_parser('launch');a.add_argument('--route');a.add_argument('--smoke',action='store_true');a.add_argument('--resume',action='store_true',help='after a save loads, start the route at the step for the current map')
a=sub.add_parser('start');a.add_argument('route');a.add_argument('--from-step',type=int,default=1,help='resume at this numbered route step (1-based); 0 = pick from the current map');a.add_argument('--delay',type=float,default=5,help='seconds to refocus FF4')
sub.add_parser('stop');sub.add_parser('status')
a=p.parse_args()
try:
    if a.command=='launch':launch(a.route,a.smoke,a.resume)
    elif a.command=='start':start(a.route,a.from_step,a.delay)
    elif a.command=='stop':request('stop')
    else:print(json.dumps(state(),indent=2))
except (OSError,ValueError,RuntimeError,subprocess.CalledProcessError) as e:
    print('TAS:',e,file=sys.stderr,flush=True)
    if a.command=='launch':
        try:
            DATA.mkdir(parents=True,exist_ok=True)
            (DATA/'launcher-error.txt').write_text(datetime.now(timezone.utc).isoformat()+'\n'+str(e)+'\n')
        except OSError:pass
        if sys.stdin.isatty():
            try:input('Press Enter to close this launcher. ')
            except (EOFError,KeyboardInterrupt):pass
    sys.exit(1)
