#!/usr/bin/env python3
"""Extract map text assets (collision, attributes, entities, events) locally.

Reads the installed game's Addressables bundles and writes every TextAsset of
the requested maps under research/tas-maps/container/, keeping the original
container hierarchy (submaps share file names, so flattening loses data).
Output is proprietary game data: it stays in the ignored research/ tree and
is never committed or uploaded.

Needs UnityPy; use the project's local interpreter, e.g.
  "/media/desktop/New Volume/Projects/lsl-3d/.tools/venv/bin/python" \
      tas/extract_maps.py 20070 20030 20031 30021
"""
import argparse, glob, os, sys
import UnityPy

ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME='/media/desktop/New Volume/SteamLibrary/steamapps/common/FINAL FANTASY IV PR'
BUNDLES=os.path.join(GAME,'FINAL FANTASY IV_Data','StreamingAssets','aa','StandaloneWindows64')
OUT=os.path.join(ROOT,'research','tas-maps','container')

def main():
    a=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    a.add_argument('maps',nargs='+',help='map asset numbers, e.g. 20070 for map_20070')
    o=a.parse_args()
    for number in o.maps:
        bundles=glob.glob(os.path.join(BUNDLES,f'map_{number}_assets_all_*.bundle'))
        if not bundles:sys.exit(f'No bundle for map_{number}')
        written=0
        for bundle in bundles:
            env=UnityPy.load(bundle)
            for path,obj in env.container.items():
                if obj.type.name!='TextAsset':continue
                data=obj.read()
                target=os.path.normpath(os.path.join(OUT,path))
                if not target.startswith(OUT+os.sep):continue
                os.makedirs(os.path.dirname(target),exist_ok=True)
                script=data.m_Script
                with open(target if os.path.splitext(target)[1] else target+'.json','wb') as f:
                    f.write(script.encode('utf-8','surrogateescape') if isinstance(script,str) else bytes(script))
                written+=1
        print(f'map_{number}: {written} text assets')

if __name__=='__main__':main()
