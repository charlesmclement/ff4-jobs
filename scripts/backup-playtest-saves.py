#!/usr/bin/env python3
"""Verify the pinned original saves, or back up saves before a new playtest."""
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil

root = Path(__file__).resolve().parent.parent
restore_plan = root / 'research/pending-save-restore.json'
if restore_plan.exists():
    plan = json.loads(restore_plan.read_text())
    if plan.get('backup_policy') == 'originals_only':
        target = Path(plan['backup'])
        manifest = json.loads(Path(plan['manifest']).read_text())
        if not manifest:
            raise RuntimeError('Original save backup manifest is empty; game launch stopped.')
        for relative, digest in manifest.items():
            if hashlib.sha256((target / relative).read_bytes()).hexdigest() != digest:
                raise RuntimeError('Original save backup verification failed; game launch stopped.')
        print(f'Original save backup verified ({len(manifest)} files); no test-save backup created.')
        raise SystemExit(0)
prefix = Path(os.environ.get('STEAM_COMPAT_DATA_PATH', '/dev/shm/ff4-jobs-proton'))
source = prefix / 'pfx/drive_c/users/steamuser/Documents/My Games/FINAL FANTASY IV PR'
if not source.is_dir():
    print('No existing playtest saves to back up.')
else:
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S.%fZ')
    target = root / 'research/saves-before-tests' / stamp
    shutil.copytree(source, target)
    manifest = {}
    for original in source.rglob('*'):
        if not original.is_file():
            continue
        relative = original.relative_to(source)
        digest = hashlib.sha256(original.read_bytes()).hexdigest()
        if hashlib.sha256((target / relative).read_bytes()).hexdigest() != digest:
            raise RuntimeError('Save backup verification failed; game launch stopped.')
        manifest[str(relative)] = digest
    (target / 'backup-sha256.json').write_text(json.dumps(manifest, indent=2))
    print(f'Verified save backup: {target} ({len(manifest)} files)')
