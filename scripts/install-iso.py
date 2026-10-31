#!/usr/bin/env python3
from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parent.parent
game=root.parent/'SteamLibrary/steamapps/common/FINAL FANTASY IV PR'
known=json.loads((root/'verified-game.json').read_text())['exe_sha256']
if hashlib.sha256((game/'GameAssembly.dll').read_bytes()).hexdigest()!=known: raise SystemExit('Unverified game build')
source=root/'src/FF4Iso.Plugin/bin/Release/net6.0/FF4Iso.dll'
target=game/'BepInEx/plugins/FF4Iso'
target.mkdir(parents=True,exist_ok=True)
if (target/'FF4Iso.dll').exists(): shutil.copy2(target/'FF4Iso.dll',target/'FF4Iso.dll.previous')
shutil.copy2(source,target/'FF4Iso.dll')
(target/'gameassembly.sha256').write_text(known+'\n')
print('Installed separate isometric prototype:',target)
