#!/usr/bin/env python3
"""Delegates rocket & cosmodrome asset generation to build_heavy_rocket_and_pad.py (idempotent)."""
from build_heavy_rocket_and_pad import build_cosmodrome_pad, build_heavy_rocket

if __name__ == "__main__":
    build_heavy_rocket()
    build_cosmodrome_pad()
