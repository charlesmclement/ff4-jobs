#!/usr/bin/env python3
"""Record a successful build, or reject packaging stale sources/binaries."""
from pathlib import Path
import hashlib, json, sys

root = Path(__file__).resolve().parent.parent
record = root / 'dist/build-verification.json'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def snapshot():
    inputs = sorted(p for p in (root / 'src').rglob('*')
                    if p.suffix in ('.cs', '.csproj', '.props', '.targets')
                    and not {'bin', 'obj'} & set(p.parts)
                    and 'FF4Iso.Plugin' not in p.parts)
    inputs += [root / 'scripts/build.sh', root / 'scripts/verify-build.py']
    outputs = [root / 'src/FF4Jobs.Plugin/bin/Release/net6.0' / name
               for name in ('FF4Jobs.dll', 'FF4Jobs.Core.dll')]
    return {kind: {str(p.relative_to(root)): sha(p) for p in paths}
            for kind, paths in (('sources', inputs), ('binaries', outputs))}
current = snapshot()
if sys.argv[1:] == ['--record']:
    record.parent.mkdir(exist_ok=True)
    record.write_text(json.dumps(current, indent=2) + '\n')
    print('Recorded verified build inputs and binaries.')
elif not sys.argv[1:]:
    if not record.exists() or json.loads(record.read_text()) != current:
        raise SystemExit('Sources or binaries changed: run scripts/build.sh before packaging.')
    print('Build provenance verified.')
else:
    raise SystemExit('Usage: verify-build.py [--record]')
