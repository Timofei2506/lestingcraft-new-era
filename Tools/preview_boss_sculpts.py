"""Neutral software preview of the EXACT BossSculpts.json sculptures.

This is the same data Unity loads; it is NOT a Unity screenshot and NOT gameplay validation.
Geometry, palette layers, tints, UV scale and emission come from the model file. Lighting is
approximate studio lighting so silhouettes and materials can be judged before a CI build.

Run: python Tools/preview_boss_sculpts.py [out.jpg|out-dir]
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
DATA = json.loads((ROOT / 'Assets/Resources/Models/BossSculpts.json').read_text(encoding='utf-8'))

import re  # noqa: E402

_LAYER_TEXT = (ROOT / 'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text(encoding='utf-8')
NAMES = re.findall(r'"([^"]+)"', re.search(r'Names\s*=\s*\{(.*?)\};', _LAYER_TEXT, re.S).group(1))

CORNERS = np.array([
    [[1, 0, 0], [1, 1, 0], [1, 1, 1], [1, 0, 1]],
    [[0, 0, 1], [0, 1, 1], [0, 1, 0], [0, 0, 0]],
    [[0, 1, 1], [1, 1, 1], [1, 1, 0], [0, 1, 0]],
    [[0, 0, 0], [1, 0, 0], [1, 0, 1], [0, 0, 1]],
    [[1, 0, 1], [1, 1, 1], [0, 1, 1], [0, 0, 1]],
    [[0, 0, 0], [0, 1, 0], [1, 1, 0], [1, 0, 0]],
], float)
NORMALS = np.array([[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]], int)

_TEXTURES = {}


def texture(layer):
    name = NAMES[layer]
    if name not in _TEXTURES:
        found = [f for f in (ROOT / 'Assets/Art/PBR').glob(name + '_albedo.*') if f.suffix != '.meta']
        if not found:
            raise FileNotFoundError('missing albedo for layer ' + name)
        _TEXTURES[name] = (np.asarray(Image.open(found[0]).convert('RGB'), float) / 255) ** 2.2
    return _TEXTURES[name]


def all_cells(sculpt):
    """Body cells plus every animated part, in voxel space, for silhouette review."""
    flat = list(sculpt['cells'])
    for part in sculpt['parts']:
        flat += part['cells']
    cells = {}
    for i in range(0, len(flat), 4):
        key = (flat[i], flat[i + 1], flat[i + 2])
        if key[1] >= 0:
            cells[key] = flat[i + 3]
    return cells


def render(sculpt, path, width=1100, height=1100, yaw=-24.0, pitch=-9.0):
    cells = all_cells(sculpt)
    pal = sculpt['palette']
    unit = sculpt['unit']
    keys = np.array(list(cells.keys()), float)
    centre = (keys.min(0) + keys.max(0)) / 2
    centre[1] = keys[:, 1].min() + (keys[:, 1].max() - keys[:, 1].min()) * .52
    span = float((keys.max(0) - keys.min(0)).max()) * unit

    ya, pa = math.radians(yaw), math.radians(pitch)
    to_eye = np.array([math.sin(ya) * math.cos(pa), math.sin(pa), math.cos(ya) * math.cos(pa)])
    to_eye /= np.linalg.norm(to_eye)
    eye = centre * unit + to_eye * (span * 2.55)
    view = -to_eye
    right = np.cross([0., 1, 0], view)
    right /= np.linalg.norm(right)
    up = np.cross(view, right)
    up /= np.linalg.norm(up)
    light = np.array([-.45, .82, .62])
    light /= np.linalg.norm(light)
    rim = np.array([.62, .30, -.72])
    rim /= np.linalg.norm(rim)
    scale = width * .80 / span
    print('    bbox', keys.min(0), keys.max(0), 'span %.2f m' % span)

    Y, X = np.mgrid[:height, :width]
    rad = ((X - width * .5) / (width * .95)) ** 2 + ((Y - height * .42) / (height * 1.1)) ** 2
    rgb = np.ones((height, width, 3)) * np.array([.075, .085, .095])
    rgb *= np.clip(1.18 - rad[:, :, None] * .55, .30, 1.18)
    ground = np.exp(-(((X - width * .5) / (width * .30)) ** 2 + ((Y - height * .945) / (height * .022)) ** 2) * 1.6)
    rgb *= 1 - ground[:, :, None] * .55
    depth = np.full((height, width), np.inf)
    glow = np.zeros((height, width, 3))
    anchors = {k: np.array(v, float) * unit for k, v in sculpt.get('anchors', {}).items()}
    core = anchors.get('core')
    faces = 0

    for k, mi in cells.items():
        p = pal[mi]
        tex = texture(p['layer'])
        tint = np.array(p['tint'], float) ** 2.2
        for face in range(6):
            n = NORMALS[face]
            if tuple(np.array(k) + n) in cells:
                continue
            verts = (np.array(k) + CORNERS[face]) * unit
            q = verts - eye
            z = q @ view
            if (z <= .05).any():
                continue
            faces += 1
            sx = width * .5 + (q @ right) * scale
            sy = height * .52 - (q @ up) * scale
            if face < 2:
                uv = verts[:, [2, 1]] * .42
            elif face < 4:
                uv = verts[:, [0, 2]] * .42
            else:
                uv = verts[:, [0, 1]] * .42
            shade = .34 + max(0.0, float(n @ light)) * 1.05
            shade += max(0.0, float(n @ rim)) * .22
            # cheap contact shading: count filled neighbours around the face
            occ = 0
            for jump in (2, 4, 6):
                for dx, dy, dz in ((jump, 0, 0), (-jump, 0, 0), (0, 0, jump), (0, 0, -jump), (0, jump, 0)):
                    if (k[0] + dx, k[1] + dy, k[2] + dz) in cells:
                        occ += 1
            shade *= 1 - min(.30, .022 * occ)
            for ids in ((0, 1, 2), (0, 2, 3)):
                pts = np.stack([sx[list(ids)], sy[list(ids)]], 1)
                tuv = uv[list(ids)]
                a, b, c = pts
                x0 = max(0, int(np.floor(pts[:, 0].min())))
                x1 = min(width - 1, int(np.ceil(pts[:, 0].max())))
                y0 = max(0, int(np.floor(pts[:, 1].min())))
                y1 = min(height - 1, int(np.ceil(pts[:, 1].max())))
                if x1 < x0 or y1 < y0:
                    continue
                yy, xx = np.mgrid[y0:y1 + 1, x0:x1 + 1]
                xx = xx + .5
                yy = yy + .5
                den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
                if abs(den) < 1e-9:
                    continue
                u = ((b[1] - c[1]) * (xx - c[0]) + (c[0] - b[0]) * (yy - c[1])) / den
                v = ((c[1] - a[1]) * (xx - c[0]) + (a[0] - c[0]) * (yy - c[1])) / den
                w = 1 - u - v
                sub = depth[y0:y1 + 1, x0:x1 + 1]
                valid = (u >= -1e-6) & (v >= -1e-6) & (w >= -1e-6)
                if not valid.any():
                    continue
                coords = u[:, :, None] * tuv[0] + v[:, :, None] * tuv[1] + w[:, :, None] * tuv[2]
                tx = (np.mod(coords[:, :, 0], 1) * tex.shape[1]).astype(int) % tex.shape[1]
                ty = ((1 - np.mod(coords[:, :, 1], 1)) * tex.shape[0]).astype(int) % tex.shape[0]
                albedo = tex[ty, tx] * tint
                energy = p['emission'] * 1.35
                warm = 0.0
                if core is not None:
                    far = np.linalg.norm(verts.mean(0) - core)
                    warm = max(0.0, float(n @ (core - verts.mean(0)) / max(.01, far))) * 1.9 / (1 + far * far * 1.4)
                linear = albedo * (shade * 1.18 + energy) + np.array([1.0, .38, .10]) * (warm * albedo)
                colour = (linear / (1 + linear * .22)) ** (1 / 2.2)
                # painter's order per quad: nearer quads overwrite farther ones
                quad_z = float(z.mean())
                take = valid & (quad_z < sub)
                if not take.any():
                    continue
                rgb[y0:y1 + 1, x0:x1 + 1][take] = np.clip(colour[take], 0, 1)
                sub[take] = quad_z
                glow[y0:y1 + 1, x0:x1 + 1][take] = np.clip(albedo[take] * energy * .30, 0, 1)

    out = np.uint8(np.clip(rgb, 0, 1) * 255)
    bloom = Image.fromarray(np.uint8(glow * 255)).filter(ImageFilter.GaussianBlur(11))
    out = np.uint8(np.clip(out.astype(float) + np.asarray(bloom, float) * .38, 0, 255))
    img = Image.fromarray(out).resize((760, 760), Image.Resampling.LANCZOS)
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    img.save(path, quality=92, optimize=True)
    print('%-9s %5d voxels, %6d faces -> %s' % (sculpt['key'], len(cells), faces, path))


def animate(sculpt):
    """Bake the same articulation Unity applies, so the preview shows the pose the rig makes."""
    import math as _m
    keys = {'head': (-4.0, 0.0), 'jaw': (5.0, 0.0), 'arm_l': (-16.0, 4.0),
            'arm_r': (-13.0, -4.0), 'halo': (0.0, 22.0)}
    out = dict(sculpt)
    flat = list(sculpt['cells'])
    for part in sculpt['parts']:
        rot = keys.get(part['name'])
        if rot is None:
            flat += part['cells']
            continue
        px, py, pz = (float(v) for v in part['pivot'])
        ax = _m.radians(rot[0])
        cab = _m.cos(ax)
        sab = _m.sin(ax)
        # crowns also spin about the vertical axis
        cy = _m.radians(rot[1])
        ccc, sss = _m.cos(cy), _m.sin(cy)
        for i in range(0, len(part['cells']), 4):
            x = part['cells'][i] + .5 - px
            y = part['cells'][i + 1] + .5 - py
            z = part['cells'][i + 2] + .5 - pz
            y, z = y * cab - z * sab, y * sab + z * cab
            x, z = x * ccc - z * sss, x * sss + z * ccc
            flat += [int(_m.floor(px + x)), int(_m.floor(py + y)), int(_m.floor(pz + z)), part['cells'][i + 3]]
    out['cells'] = flat
    out['parts'] = []
    return out


def main():
    target = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'Builds' / 'BossPreview'
    if target.suffix.lower() in ('.jpg', '.jpeg', '.png'):
        target.parent.mkdir(parents=True, exist_ok=True)
        tiles = []
        for sc in DATA['sculpts']:
            tmp = target.with_name(target.stem + '-' + sc['key'] + target.suffix)
            render(animate(sc), tmp)
            tiles.append(Image.open(tmp).convert('RGB'))
        sheet = Image.new('RGB', (760 * 5, 760), (8, 9, 10))
        for i, tile in enumerate(tiles):
            sheet.paste(tile, (760 * i, 0))
        sheet.save(target, quality=92, optimize=True)
        print('contact sheet ->', target)
    else:
        for sc in DATA['sculpts']:
            render(animate(sc), target / (sc['key'] + '.jpg'))


if __name__ == '__main__':
    main()
