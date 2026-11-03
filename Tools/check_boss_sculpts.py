"""Headless gate for the boss sculpture catalogue.

Fails if an archetype that the boss roster can request has no sculpture, if geometry breaks the
renderer's assumptions (palette bounds, part pivots, ground contact, height band), or if the
catalogue grows past the Android budget. Pure data check: no Unity, no GPU.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = json.loads((ROOT / 'Assets/Resources/Models/BossSculpts.json').read_text(encoding='utf-8'))
Layers = re.findall(r'"([^"]+)"', re.search(
    r'Names\s*=\s*\{(.*?)\};', (ROOT / 'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text(encoding='utf-8'), re.S).group(1))
REQUIRED = {'warden', 'colossus', 'archon', 'titan', 'ember'}
errors = []


def check(ok, message):
    if not ok:
        errors.append(message)


keys = set()
for sculpt in DATA['sculpts']:
    key = sculpt['key']
    check(key in REQUIRED, 'unknown archetype: ' + key)
    keys.add(key)
    unit = sculpt['unit']
    palette = sculpt['palette']
    check(0.05 <= unit <= 0.25, key + ': voxel size is outside the readable band')
    check(4 <= len(palette) <= 16, key + ': palette must stay small enough to read')
    for entry in palette:
        check(0 <= entry['layer'] < len(Layers), key + ': palette layer out of range')
        check(0.0 <= entry['emission'] <= 3.0, key + ': emission out of band')

    body = sculpt['cells']
    check(len(body) % 4 == 0 and len(body) >= 400, key + ': body is empty')
    ys = [body[i + 1] for i in range(0, len(body), 4)]
    height = (max(ys) + 1) * unit
    check(5.0 <= height <= 9.5, key + ': height %.2f m is outside the readable band' % height)
    # Hovering guardians are legal: Ember is a float chassis, the rest must plant their feet.
    if key != 'ember':
        check(min(ys) * unit <= 0.40, key + ': body floats above its arena')

    for part in sculpt['parts']:
        cells = part['cells']
        check(len(cells) >= 16, key + '/' + part['name'] + ': part is empty')
        check(len(part['pivot']) == 3, key + '/' + part['name'] + ': pivot malformed')
        for i in range(0, len(cells), 4):
            check(0 <= cells[i + 3] < len(palette), key + '/' + part['name'] + ': palette index')

    names = {p['name'] for p in sculpt['parts']}
    check({'head', 'arm_l', 'arm_r', 'halo'} <= names, key + ': missing animated parts')
    anchors = sculpt['anchorNames']
    check('core' in anchors and len(sculpt['anchorCells']) == len(anchors) * 3, key + ': anchors malformed')
    core_y = sculpt['anchorCells'][anchors.index('core') * 3 + 1] * unit
    check(core_y > height * .30, key + ': core anchor is too low to be a weak point')

    voxels = len(body) // 4 + sum(len(p['cells']) // 4 for p in sculpt['parts'])
    check(voxels <= 20000, key + ': %d voxels is above the mobile budget' % voxels)

check(REQUIRED <= keys, 'missing archetypes: ' + ', '.join(sorted(REQUIRED - keys)))
check(len(DATA['sculpts']) == len(keys), 'duplicate archetype keys')

if errors:
    for error in errors:
        print('ERROR:', error)
    sys.exit(1)
print('PASS boss sculptures: %d archetypes, %d voxels, palette layers within SurfaceLayers, '
      'animated parts and weak points on every guardian. Data check, not a Unity render.'
      % (len(keys), sum(len(s['cells']) // 4 + sum(len(p['cells']) // 4 for p in s['parts']) for s in DATA['sculpts'])))
