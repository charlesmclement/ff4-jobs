#!/usr/bin/env python3
"""Install only our built plugin. Never overwrites original game files."""
from pathlib import Path
import hashlib,json,shutil

root=Path(__file__).resolve().parent.parent
game=root.parent/'SteamLibrary/steamapps/common/FINAL FANTASY IV PR'
source=root/'src/FF4Jobs.Plugin/bin/Release/net6.0'
target=game/'BepInEx/plugins/FF4Jobs'
known=json.loads((root/'verified-game.json').read_text())['exe_sha256']
actual=hashlib.sha256((game/'GameAssembly.dll').read_bytes()).hexdigest()
if actual!=known: raise SystemExit('Game build changed; re-inspect before installing.')
for name in ['FF4Jobs.dll','FF4Jobs.Core.dll']:
    if not (source/name).is_file(): raise SystemExit('Build first: '+str(source/name))
target.mkdir(parents=True,exist_ok=True)
for name in ['FF4Jobs.dll','FF4Jobs.Core.dll']:
    dest=target/name
    if dest.exists(): shutil.copy2(dest,dest.with_suffix('.dll.previous'))
    shutil.copy2(source/name,dest)
(target/'gameassembly.sha256').write_text(known+'\n')
print('Installed prototype:',target)
