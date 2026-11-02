"""Shared voxel-sculpt DSL used to author boss guardians.

The same cells and palette are consumed by Unity (Assets/Resources/Models/BossSculpts.json)
and by the honest offline preview in Tools/preview_boss_sculpts.py. Nothing here renders;
it only turns readable shape calls into a sparse voxel grid.

Coordinates are metres. x is left/right, y is up, z is depth (positive = toward the viewer).
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/Models/BossSculpts.json'


class Sculpt:
    def __init__(self, key, name, unit, palette, min_y=0.0):
        self.key = key
        self.name = name
        self.unit = unit
        self.palette = list(palette)
        self.cells = {}
        self.parts = []
        self.anchors = {}
        self.min_y = min_y

    # ---- palette -------------------------------------------------------
    def mat(self, layer, tint, emission=0.0, name=''):
        for i, p in enumerate(self.palette):
            if p['layer'] == layer and p['emission'] == emission and p['tint'] == list(tint):
                return i
        self.palette.append({'name': name or ('layer %d' % layer), 'layer': layer,
                             'tint': list(tint), 'emission': emission})
        return len(self.palette) - 1

    # ---- raw voxel access ---------------------------------------------
    def _key(self, x, y, z):
        return (int(math.floor(x / self.unit)), int(math.floor(y / self.unit)),
                int(math.floor(z / self.unit)))

    def put(self, x, y, z, m):
        if m is None:
            return
        k = self._key(x, y, z) if isinstance(x, float) else (int(x), int(y), int(z))
        if k[1] < 0:
            return
        self.cells[k] = m

    def erase(self, x, y, z):
        k = self._key(x, y, z) if isinstance(x, float) else (int(x), int(y), int(z))
        self.cells.pop(k, None)

    # ---- shape primitives ---------------------------------------------
    def brush(self, p, r, m, flatten=(1, 1, 1), square=2.0):
        """Bevelled ellipsoid. square=2 -> sphere, higher -> boxier voxel chunk."""
        if r <= 0:
            return
        rx, ry, rz = [r * v for v in flatten]
        u = self.unit
        for x in range(math.floor((p[0] - rx) / u), math.ceil((p[0] + rx) / u) + 1):
            for y in range(max(0, math.floor((p[1] - ry) / u)), math.ceil((p[1] + ry) / u) + 1):
                for z in range(math.floor((p[2] - rz) / u), math.ceil((p[2] + rz) / u) + 1):
                    d = [abs(((a + .5) * u - b) / s) for a, b, s in zip((x, y, z), p, (rx, ry, rz))]
                    if max(d) <= 1 and sum(v ** square for v in d) <= 1.0 + .18 * (square - 2):
                        self.cells[(x, y, z)] = m

    def box(self, lo, hi, m):
        u = self.unit
        for x in range(math.floor(lo[0] / u), math.ceil(hi[0] / u)):
            for y in range(max(0, math.floor(lo[1] / u)), math.ceil(hi[1] / u)):
                for z in range(math.floor(lo[2] / u), math.ceil(hi[2] / u)):
                    if m is None and (x, y, z) in self.cells:
                        del self.cells[(x, y, z)]
                    elif m is not None:
                        self.cells[(x, y, z)] = m

    def carve(self, lo, hi):
        self.box(lo, hi, None)

    def path(self, points, radii, m, step=0.42, flatten=None):
        """Catmull-Rom swept tube; final silhouette stays voxel because it is rasterised."""
        n = len(points)
        for i in range(n - 1):
            p0 = points[max(0, i - 1)]
            p1 = points[i]
            p2 = points[i + 1]
            p3 = points[min(n - 1, i + 2)]
            length = math.dist(p1, p2)
            segs = max(2, int(math.ceil(length / (self.unit * step))) + 1)
            for j in range(segs + 1):
                t = j / segs
                p = [.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t
                           + (-a + 3 * b - 3 * c + d) * t * t * t)
                     for a, b, c, d in zip(p0, p1, p2, p3)]
                self.brush(p, radii[i] * (1 - t) + radii[i + 1] * t, m,
                           flatten=flatten or (1, 1, 1))

    def ring(self, center, radius, tube, m, axis='y', tilt=0.0, solid=False):
        """Voxel ring; segments are cubes so the outline stays chunky, never a smooth torus."""
        u = self.unit
        steps = max(12, int(2 * math.pi * radius / (u * 1.6)))
        ct, st = math.cos(tilt), math.sin(tilt)
        for i in range(steps):
            a = 2 * math.pi * i / steps
            if axis == 'y':
                p = [center[0] + math.cos(a) * radius, center[1] + math.sin(a) * radius * st,
                     center[2] + math.sin(a) * radius * ct]
            elif axis == 'x':
                p = [center[0] + math.sin(a) * radius * st, center[1] + math.cos(a) * radius,
                     center[2] + math.sin(a) * radius * ct]
            else:
                p = [center[0] + math.cos(a) * radius, center[1] + math.sin(a) * radius,
                     center[2] + math.sin(a) * radius * st]
            if solid:
                self.brush(p, tube, m, square=3.4)
            else:
                self.brush(p, tube, m, square=2.6)

    def spike(self, base, tip, r0, r1, m, segs=6):
        self.path([list(base), list(tip)], [r0, r1], m, step=0.32)

    def sym(self, fn, *args, **kw):
        """Call fn for the +x side and its mirror on -x."""
        fn(1, *args, **kw)
        fn(-1, *args, **kw)

    # ---- parts ---------------------------------------------------------
    def part(self, name, pivot, fn, rotation=(0, 0, 0)):
        """Author geometry into an isolated part so Unity can animate it independently."""
        saved = dict(self.cells)
        self.cells = {}
        fn()
        piece = {k: v for k, v in self.cells.items() if k not in saved}
        self.cells = saved
        self.cells.update(piece)
        px, py, pz = (int(round(v / self.unit)) for v in pivot)
        self.parts.append({'name': name, 'pivot': [px, py, pz],
                           'cells': [c for k, m in sorted(piece.items()) for c in (k[0], k[1], k[2], m)],
                           'rotation': list(rotation)})

    def anchor(self, name, p):
        self.anchors[name] = [int(round(v / self.unit)) for v in p]

    def _flat_anchors(self):
        names = sorted(self.anchors)
        cells = [c for name in names for c in self.anchors[name]]
        return names, cells

    # ---- export --------------------------------------------------------
    def body_cells(self):
        part_cells = set()
        for part in self.parts:
            flat = part['cells']
            for i in range(0, len(flat), 4):
                part_cells.add((flat[i], flat[i + 1], flat[i + 2]))
        return {k: v for k, v in self.cells.items() if k not in part_cells}

    def export(self):
        body = self.body_cells()
        anchor_names, anchor_cells = self._flat_anchors()
        return {
            'key': self.key,
            'name': self.name,
            'unit': self.unit,
            'palette': self.palette,
            'cells': [c for k, m in sorted(body.items()) for c in (k[0], k[1], k[2], m)],
            'parts': self.parts,
            'anchorNames': anchor_names,
            'anchorCells': anchor_cells,
            'anchors': self.anchors,
        }


def write_all(sculpts, path=OUT):
    doc = {'version': 1, 'sculpts': [s.export() for s in sculpts]}
    text = json.dumps(doc, ensure_ascii=False, separators=(',', ':'))
    path.write_text(text)
    total = sum(len(s['cells']) // 4 + sum(len(p['cells']) // 4 for p in s['parts']) for s in doc['sculpts'])
    print('wrote', len(doc['sculpts']), 'sculpts,', total, 'voxels,', len(text), 'bytes ->', path)
    return doc
