#!/usr/bin/env python3
"""Read-only repository hygiene gate.

Fails on missing/orphan Unity .meta files and obvious committed credentials.
Reports byte-identical assets as information: many duplicates are intentional
(shared PBR layers, cross-platform fonts, shared item art) and must not be deleted
blindly because Unity references them by GUID.
"""
from pathlib import Path
import hashlib
import re
import sys
from collections import defaultdict

ROOT = Path(__file__).resolve().parents[1]
EXCLUDE_DIRS = {".git", "Library", "Temp", "Obj", "Build", "Builds", "Logs", "UserSettings", "__pycache__", "node_modules"}
SECRET_PATTERNS = [
    re.compile(r"ghp_[A-Za-z0-9]{36}"),
    re.compile(r"github_pat_[A-Za-z0-9_]{20,}"),
    re.compile(r"AKIA[0-9A-Z]{16}"),
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
]


def files():
    for p in ROOT.rglob("*"):
        if not p.is_file():
            continue
        if any(part in EXCLUDE_DIRS for part in p.relative_to(ROOT).parts):
            continue
        yield p


def main() -> int:
    errors = []
    assets = ROOT / "Assets"
    for p in assets.rglob("*"):
        if any(part in EXCLUDE_DIRS for part in p.relative_to(ROOT).parts):
            continue
        if p.is_file() and p.suffix != ".meta" and not Path(str(p) + ".meta").exists():
            errors.append(f"missing meta: {p.relative_to(ROOT)}")
        if p.suffix == ".meta" and not Path(str(p)[:-5]).exists():
            errors.append(f"orphan meta: {p.relative_to(ROOT)}")

    for p in files():
        if p.suffix in {".png", ".jpg", ".jpeg", ".ttf", ".mp4", ".blend", ".glb", ".zip", ".apk"}:
            continue
        try:
            text = p.read_text(errors="ignore")
        except OSError:
            continue
        for pattern in SECRET_PATTERNS:
            if pattern.search(text):
                errors.append(f"possible committed secret: {p.relative_to(ROOT)}")
                break

    groups = defaultdict(list)
    for p in files():
        if p.suffix == ".meta" or p.stat().st_size < 1024:
            continue
        groups[(p.stat().st_size, hashlib.sha256(p.read_bytes()).digest())].append(p)
    duplicates = [g for g in groups.values() if len(g) > 1]

    for error in errors:
        print("ERROR:", error)
    print(f"Hygiene: {len(errors)} error(s), {len(duplicates)} intentional-or-reviewable duplicate group(s).")
    if "--duplicates" in sys.argv:
        for group in duplicates:
            print("DUPLICATE:", ", ".join(str(p.relative_to(ROOT)) for p in group))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
