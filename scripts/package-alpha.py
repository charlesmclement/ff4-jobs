#!/usr/bin/env python3
"""Compatibility entry point; current releases use package-release.py."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name('package-release.py')),run_name='__main__')
