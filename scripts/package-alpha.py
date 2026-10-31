#!/usr/bin/env python3
"""Allowlist release files; never copy the development installation or game assets."""
from pathlib import Path
import hashlib, json, zipfile
root=Path(__file__).resolve().parent.parent
out=root/'dist';out.mkdir(exist_ok=True)
version='0.2.0-alpha1'
sha=json.loads((root/'verified-game.json').read_text())['exe_sha256']
payload={}
for name in ('FF4Jobs.dll','FF4Jobs.Core.dll'):
    payload['BepInEx/plugins/FF4Jobs/'+name]=(root/'src/FF4Jobs.Plugin/bin/Release/net6.0'/name).read_bytes()
payload['BepInEx/plugins/FF4Jobs/gameassembly.sha256']=(sha+'\n').encode()
for name in ('INSTALL.txt','JOBS.txt','CHANGELOG.txt','KNOWN-ISSUES.txt'):
    payload[name]=(root/'release'/name).read_bytes()
manifest={'version':version,'status':'alpha candidate; campaign acceptance pending','steam_build':'20265023','gameassembly_sha256':sha,'loader':'BepInEx Unity IL2CPP Windows x64 6.0.0-be.735','files':{n:hashlib.sha256(b).hexdigest() for n,b in sorted(payload.items())}}
payload['MANIFEST.json']=(json.dumps(manifest,indent=2)+'\n').encode()
archive=out/f'FF4Jobs-{version}.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for name,data in sorted(payload.items()):z.writestr(name,data)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert set(z.namelist())==set(payload)
    for name,data in payload.items():assert z.read(name)==data
    assert all(not any(word in name.lower() for word in ('ff4iso','research/','save','interop/','gameassembly.dll','winhttp','dxvk')) for name in z.namelist())
digest=hashlib.sha256(archive.read_bytes()).hexdigest()
(archive.with_suffix('.zip.sha256')).write_text(digest+'  '+archive.name+'\n')
print(json.dumps({'archive':str(archive),'files':len(payload),'bytes':archive.stat().st_size,'sha256':digest},indent=2))
