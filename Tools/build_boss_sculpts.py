"""Author the five planetary guardian sculptures as one coherent voxel mesh each.

Shared cells + palette are consumed by Unity (Assets/Resources/Models/BossSculpts.json)
and by the honest offline preview (Tools/preview_boss_sculpts.py). No image generation,
no screenshots, no terrain or gameplay changes.

Run: python Tools/build_boss_sculpts.py
"""
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from boss_sculpt_lib import Sculpt, write_all, ROOT  # noqa: E402

U = 0.12


def _layers():
    text = (ROOT / 'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text(encoding='utf-8')
    body = re.search(r'Names\s*=\s*\{(.*?)\};', text, re.S).group(1)
    names = re.findall(r'"([^"]+)"', body)
    return {name: i for i, name in enumerate(names)}


LAYER = _layers()


def L(name):
    if name not in LAYER:
        raise KeyError('unknown surface layer: ' + name)
    return LAYER[name]


def mirrored(points):
    return [(-x, y, z) for x, y, z in points]


def eyes(s, x, y, z, r, m, tilt=0.0):
    for side in (-1, 1):
        s.brush((side * x, y + tilt, z), r, m, square=2.8)
        s.brush((side * x, y - r * 1.25, z + r * .35), r * .8, m, square=3.0)


def brow(s, width, y, z, m, depth=.16):
    s.box((-width, y, z - depth), (width, y + .16, z + depth * .6), m)


def plate(s, cx, cy, cz, w, h, d, m, notch=0):
    """Layered armour plate with a chipped lower edge instead of a clean box."""
    s.box((cx - w, cy - h, cz - d), (cx + w, cy + h, cz + d), m)
    if notch:
        for i in range(notch):
            x = cx - w + (i + .5) * (2 * w / notch)
            s.box((x - w / notch * .45, cy - h - .1, cz - d - .02),
                  (x + w / notch * .45, cy - h + .12 + (i % 2) * .12, cz + d + .02), None)


# ---------------------------------------------------------------- warden
def build_warden():
    s = Sculpt('warden', 'Древний Страж', U, [])
    bark = s.mat(L('bark'), (.58, .46, .32), 0, 'old bark plates')
    heart = s.mat(L('wood-end'), (.82, .66, .44), 0, 'exposed heartwood')
    moss = s.mat(L('mossy-stone'), (.56, .78, .38), 0, 'moss on upward faces')
    brass = s.mat(L('metal-ingot'), (.70, .56, .22), 0, 'aged brass bindings')
    amber = s.mat(L('crystal'), (.95, .40, .06), 1.1, 'amber ember heart')
    ember = s.mat(L('crystal'), (1.0, .58, .14), 1.4, 'hot ember eye')

    # --- legs: root-braided digitigrade, knees forward, ankles back
    for side in (-1, 1):
        hip = [(side * .62, 3.05, -.05), (side * .74, 2.45, .12), (side * .80, 1.85, .30),
               (side * .72, 1.28, .12), (side * .66, .82, -.14), (side * .64, .34, -.22)]
        s.path(hip, [.46, .42, .38, .31, .26, .22], bark, step=.30)
        # three braided roots wrapping the shin
        for k in range(3):
            a = k * 2.1
            s.path([(side * (.66 + .16 * math.cos(a)), 1.55, -.10 + .16 * math.sin(a)),
                    (side * (.72 + .18 * math.cos(a + 1.1)), 1.00, -.06 + .18 * math.sin(a + 1.1)),
                    (side * (.66 + .13 * math.cos(a + 2.2)), .48, -.16 + .13 * math.sin(a + 2.2))],
                   [.13, .12, .10], heart, step=.4)
        # heel spur and three clawed toes
        s.path([(side * .64, .34, -.22), (side * .64, .20, -.62), (side * .62, .16, -.86)],
               [.20, .15, .07], bark, step=.34)
        for j, off in enumerate((-.22, 0.0, .22)):
            s.path([(side * (.62 + off), .30, .02), (side * (.66 + off * 1.15), .18, .46),
                    (side * (.68 + off * 1.2), .12, .78), (side * (.68 + off * 1.25), .10, .96)],
                   [.16, .13, .09, .03], bark, step=.34)
        # knee cap plate
        plate(s, side * .80, 1.90, .42, .30, .26, .12, moss, notch=2)

    # --- pelvis and hips
    s.brush((0, 3.15, -.05), 1.04, bark, flatten=(1.30, .56, .84), square=2.6)
    plate(s, 0, 3.02, .48, .62, .30, .14, moss, notch=3)
    for side in (-1, 1):
        s.brush((side * .92, 3.12, .02), .42, heart, flatten=(.9, .9, .9))

    # --- torso: bent spine, open rib cage, heart suspended inside
    s.path([(0, 3.20, -.24), (0, 3.90, -.34), (0, 4.60, -.26), (0, 5.15, -.10)],
           [.50, .48, .50, .46], heart, step=.30)
    s.path([(0, 3.34, .10), (0, 4.00, .02), (0, 4.72, .06), (0, 5.12, .12)],
           [.38, .32, .28, .24], bark, step=.34)
    for i in range(4):
        y = 3.72 + i * .40
        for side in (-1, 1):
            s.path([(side * .10, y, -.36), (side * .72, y + .10, -.02),
                    (side * .82, y + .04, .34), (side * .52, y - .16, .60)],
                   [.15, .18, .16, .10], bark, step=.36)
            s.brush((side * .84, y + .12, .10), .16, moss, square=2.4)
    s.path([(0, 3.66, .56), (0, 4.30, .62), (0, 4.88, .50)], [.16, .14, .12], heart, step=.4)
    # heart: faceted ember core inside the cage
    s.brush((0, 4.24, .06), .40, amber, flatten=(.80, 1.0, .72), square=2.2)
    s.brush((0, 4.28, .16), .22, amber, flatten=(.7, 1.0, .7), square=2.0)
    s.brush((0, 4.32, .24), .10, amber, square=2.6)

    # --- back: layered bark shield plates
    for i in range(3):
        y = 3.95 + i * .48
        plate(s, 0, y, -.62 - i * .04, .58 - i * .05, .24, .12, bark, notch=2)
    # large asymmetric pauldron on the left, smaller root knot on the right
    s.brush((-1.46, 5.30, -.02), .94, bark, flatten=(1.02, .82, .92), square=2.4)
    plate(s, -1.46, 5.42, .52, .62, .34, .16, moss, notch=3)
    plate(s, -1.72, 5.06, .10, .34, .40, .30, heart, notch=2)
    s.brush((1.36, 5.22, -.04), .56, heart, flatten=(1, .86, .9), square=2.6)
    for side in (-1, 1):
        s.path([(side * 1.05, 5.52, -.22), (side * 1.30, 5.78, -.30), (side * 1.46, 5.92, -.18)],
               [.16, .13, .05], bark, step=.34)

    # --- neck
    s.path([(0, 5.14, .02), (0, 5.52, .08), (0, 5.86, .06)], [.30, .26, .24], heart, step=.32)
    for side in (-1, 1):
        s.path([(side * .26, 5.30, .02), (side * .30, 5.62, .10), (side * .24, 5.86, .10)],
               [.09, .08, .06], brass, step=.4)

    # --- head + jaw as animated parts
    def head():
        s.brush((0, 6.24, .06), .56, bark, flatten=(1.02, 1.04, .98), square=2.4)
        s.brush((0, 6.02, .38), .34, heart, flatten=(1.05, .72, .8), square=2.6)
        brow(s, .46, 6.40, .52, bark, .14)
        eyes(s, .23, 6.26, .52, .09, amber)
        # crown of splintered bark, snapped on the right
        s.path([(-.18, 6.65, -.02), (-.30, 6.92, -.10), (-.40, 7.12, -.16)], [.16, .11, .04], bark, step=.3)
        s.path([(.12, 6.66, -.04), (.22, 6.86, -.14), (.30, 6.98, -.20)], [.14, .09, .03], bark, step=.3)
        for side in (-1, 1):
            s.brush((side * .40, 6.42, .06), .18, moss, square=2.4)
        s.anchor('head', (0, 6.30, .30))

    def jaw():
        s.box((-.30, 5.96, .18), (.30, 6.10, .56), heart)
        for side in (-1, 1):
            s.box((side * .20 - .07, 5.90, .40), (side * .20 + .07, 6.02, .60), brass)
        s.brush((0, 5.96, .30), .18, bark, flatten=(1.5, .5, .9))

    def arm(side):
        shoulder = (side * 1.34, 5.18, -.02)
        s.brush(shoulder, .44, heart, flatten=(1, 1, 1), square=2.4)
        s.path([shoulder, (side * 1.64, 4.52, .10), (side * 1.76, 3.86, .18),
                (side * 1.68, 3.20, .26), (side * 1.58, 2.62, .34)],
               [.46, .40, .36, .33, .28], bark, step=.30)
        # forearm bark plates
        for i in range(2):
            plate(s, side * 1.68, 3.62 - i * .44, .40, .22, .18, .10, heart, notch=2)
        # hand: palm plus four long claws reaching below the knee
        s.brush((side * 1.56, 2.42, .38), .26, heart, flatten=(1.1, .9, 1.0), square=2.6)
        for j, off in enumerate((-.24, -.08, .08, .24)):
            s.path([(side * (1.56 + off), 2.36, .42), (side * (1.60 + off * 1.2), 2.02, .52),
                    (side * (1.58 + off * 1.3), 1.72, .58), (side * (1.52 + off * 1.3), 1.54, .52)],
                   [.10, .08, .05, .02], bark, step=.30)
        s.anchor('hand_' + ('l' if side < 0 else 'r'), (side * 1.56, 2.42, .38))

    def halo():
        # branched antler crown, wider on the left, snapped short on the right
        for side, reach in ((-1, 1.75), (1, 1.20)):
            s.path([(side * .30, 6.72, -.06), (side * .70, 7.05, -.18), (side * reach * .62, 7.35, -.26),
                    (side * reach, 7.52, -.30)], [.20, .15, .10, .04], bark, step=.30)
            for k in range(2):
                base = (side * (.62 + k * .28), 7.06 + k * .18, -.22)
                s.path([base, (side * (.90 + k * .34), 7.34 + k * .20, -.34),
                        (side * (1.06 + k * .36), 7.56 + k * .22, -.42)], [.10, .07, .025], bark, step=.30)
        for side in (-1, 1):
            s.brush((side * .34, 6.86, -.16), .16, moss, square=2.2)

    s.part('head', (0, 5.92, .04), head)
    s.part('jaw', (0, 5.98, .16), jaw)
    s.part('arm_l', (-1.34, 5.18, -.02), lambda: arm(-1))
    s.part('arm_r', (1.34, 5.18, -.02), lambda: arm(1))
    s.part('halo', (0, 6.60, -.10), halo)
    s.anchor('core', (0, 4.24, .10))
    s.anchor('feet', (0, 0.1, 0))
    return s


# ---------------------------------------------------------------- colossus
def build_colossus():
    s = Sculpt('colossus', 'Алый Колосс', U, [])
    rock = s.mat(L('basalt'), (.90, .60, .48), 0, 'sun-baked basalt plates')
    dark = s.mat(L('obsidian'), (.46, .34, .33), 0, 'obsidian underlayer')
    iron = s.mat(L('granite'), (.86, .74, .64), 0, 'iron-grey granite')
    lava = s.mat(L('magma-basalt'), (1.0, .38, .06), 1.9, 'lava cracks')
    ember = s.mat(L('crystal'), (1.0, .30, .08), 2.4, 'ember eye')

    # --- squat legs, wide stance
    for side in (-1, 1):
        s.path([(side * .92, 2.70, -.02), (side * 1.04, 2.05, .12), (side * 1.12, 1.42, .06),
                (side * 1.06, .86, -.02), (side * 1.02, .40, -.06)],
               [.58, .54, .48, .42, .40], dark, step=.28)
        for i in range(3):
            plate(s, side * 1.08, 2.10 - i * .52, .34 - i * .08, .40, .20, .14, rock, notch=2)
        s.box((side * 1.02 - .52, 0, -.42), (side * 1.02 + .52, .42, .46), iron)
        for j, off in enumerate((-.30, 0.0, .30)):
            s.box((side * 1.02 + off - .14, 0, .30), (side * 1.02 + off + .14, .26, .66), dark)
    # --- waist and gut
    s.brush((0, 3.05, .02), 1.06, dark, flatten=(1.25, .58, .92), square=2.8)
    s.brush((0, 3.62, .10), 1.10, rock, flatten=(1.18, .70, .98), square=2.6)
    for i in range(3):
        plate(s, 0, 3.42 + i * .34, .62 - i * .04, .56 - i * .04, .16, .14, iron, notch=2)
    # lava seams between belly plates
    for i in range(3):
        s.box((-.52, 3.56 + i * .34, .70), (.52, 3.62 + i * .34, .78), lava)

    # --- chest and enormous trapezius
    s.brush((0, 4.46, .02), 1.18, rock, flatten=(1.30, .80, 1.02), square=2.5)
    for side in (-1, 1):
        s.brush((side * .62, 5.12, -.02), .62, rock, flatten=(1.05, .70, .95), square=2.4)
        plate(s, side * .58, 4.52, .66, .40, .28, .16, iron, notch=3)
        # lava cracks bleeding down the chest
        s.path([(side * .30, 5.30, .58), (side * .40, 4.86, .70), (side * .34, 4.40, .72)],
               [.05, .045, .03], lava, step=.5)
    s.box((-.78, 4.96, -.60), (.78, 5.34, -.30), dark)
    # broken horn arc across the back of the shoulders
    for side in (-1, 1):
        s.path([(side * .30, 5.58, -.34), (side * .72, 5.94, -.52), (side * 1.06, 6.10, -.60),
                (side * 1.30, 6.04, -.52)], [.26, .20, .14, .05], dark, step=.30)
    s.box((-.44, 5.62, -.48), (.44, 5.92, -.24), iron)

    # --- neck and small sunken head
    s.path([(0, 5.30, .10), (0, 5.62, .18)], [.40, .36], iron, step=.3)

    def head():
        s.brush((0, 5.94, .18), .48, iron, flatten=(1.08, .92, 1.0), square=2.6)
        s.brush((0, 6.14, .10), .40, rock, flatten=(1.1, .7, .95), square=2.4)
        brow(s, .40, 6.02, .50, dark, .12)
        eyes(s, .19, 5.90, .48, .08, ember)
        s.box((-.26, 5.60, .26), (.26, 5.74, .62), rock)
        s.anchor('head', (0, 5.94, .34))

    def jaw():
        s.box((-.30, 5.52, .22), (.30, 5.66, .62), iron)
        for side in (-1, 1):
            s.box((side * .18 - .06, 5.46, .42), (side * .18 + .06, 5.56, .64), rock)

    def arm(side, bigger):
        shoulder = (side * 1.72, 4.92, -.02)
        s.brush(shoulder, .62 if bigger else .52, rock, flatten=(1.05, .95, 1.0), square=2.4)
        s.path([shoulder, (side * 2.06, 4.24, .12), (side * 2.20, 3.56, .22),
                (side * 2.16, 2.92, .30)], [.52, .48, .44, .40], dark, step=.28)
        # ram forearm: stacked plates
        for i in range(3):
            plate(s, side * 2.18, 3.58 - i * .40, .44, .42, .18, .20, rock, notch=2)
        s.path([(side * 2.16, 2.92, .30), (side * 2.12, 2.50, .46)], [.42, .36], iron, step=.30)
        fist = .56 if bigger else .44
        s.brush((side * 2.12, 2.24, .52), fist, rock, flatten=(1.05, .9, 1.05), square=2.3)
        for j, off in enumerate((-.22, 0.0, .22)):
            s.box((side * 2.12 + off - .10, 1.92, .62), (side * 2.12 + off + .10, 2.10, .86), iron)
        s.box((side * 2.12 - .18, 2.24, .86), (side * 2.12 + .18, 2.34, 1.02), lava)
        s.anchor('hand_' + ('l' if side < 0 else 'r'), (side * 2.12, 2.24, .52))

    def halo():
        # broken iron horn crown: two unequal arcs, deliberately snapped
        for side, reach in ((-1, 1.55), (1, .95)):
            s.path([(side * .34, 5.86, -.30), (side * .80, 6.22, -.44), (side * reach, 6.40, -.50)],
                   [.22, .16, .06], dark, step=.30)
        s.brush((0, 6.02, -.36), .34, iron, flatten=(1.4, .6, .8), square=2.6)

    s.part('head', (0, 5.62, .12), head)
    s.part('jaw', (0, 5.58, .20), jaw)
    s.part('arm_l', (-1.72, 4.92, -.02), lambda: arm(-1, True))
    s.part('arm_r', (1.72, 4.92, -.02), lambda: arm(1, False))
    s.part('halo', (0, 5.80, -.26), halo)
    s.anchor('core', (0, 4.96, .46))
    s.anchor('feet', (0, .1, 0))
    return s


# ---------------------------------------------------------------- archon
def build_archon():
    s = Sculpt('archon', 'Корона Андромеды', U, [])
    cloth = s.mat(L('stone'), (.78, .74, .92), 0, 'star-woven mantle')
    stone = s.mat(L('obsidian'), (.58, .52, .70), 0, 'obsidian regalia')
    plate_mat = s.mat(L('marble'), (.92, .90, 1.0), 0, 'pale armour plates')
    gold = s.mat(L('metal-ingot'), (1.0, .80, .36), 0, 'gold trim')
    crystal = s.mat(L('andromeda-crystal'), (.72, .52, 1.0), 2.2, 'andromeda crystal')
    aether = s.mat(L('aether'), (.58, .36, 1.0), 1.4, 'aether glow')
    eye = s.mat(L('crystal'), (.85, .90, 1.0), 2.6, 'wakeful eye')

    # floating skirt: torn blades of solidified robe, no legs
    for i in range(12):
        a = 2 * math.pi * i / 12
        r = .92 + .12 * math.sin(i * 1.7)
        length = .95 + .60 * (i % 3)
        top = (math.cos(a) * r, 2.90, math.sin(a) * r)
        mid = (math.cos(a) * (r + .16), 2.30 - length * .28, math.sin(a) * (r + .16))
        tip = (math.cos(a) * (r + .05), 2.30 - length, math.sin(a) * (r + .05))
        s.path([top, mid, tip], [.42, .30, .06], cloth, step=.30)
        if i % 4 == 0:
            s.path([mid, (math.cos(a) * (r - .06), 1.66, math.sin(a) * (r - .06))],
                   [.10, .03], aether, step=.4)
    s.brush((0, 2.92, 0), .82, cloth, flatten=(1.12, .40, 1.12), square=2.6)
    s.ring((0, 2.86, 0), .82, .10, gold, axis='y')

    # robe torso tapering upward into a high collar
    s.path([(0, 2.98, 0), (0, 3.60, .04), (0, 4.26, .06), (0, 4.82, .02)],
           [.92, .86, .76, .58], cloth, step=.30)
    for i in range(4):
        y = 3.16 + i * .40
        s.ring((0, y, .02), .88 - i * .07, .08, gold, axis='y')
    # floating shoulder plates, heavy and asymmetric
    for side, sc in ((-1, 1.18), (1, .96)):
        s.brush((side * .96, 4.74, -.02), .50 * sc, plate_mat, flatten=(1.15, .72, 1.0), square=2.3)
        s.path([(side * .60, 4.96, -.10), (side * 1.18, 5.10, -.22), (side * 1.42, 5.02, -.26)],
               [.24, .16, .05], gold, step=.30)
    # collar wings rising behind the head
    for side in (-1, 1):
        s.path([(side * .34, 4.78, -.18), (side * .62, 5.28, -.34), (side * .72, 5.78, -.30),
                (side * .58, 6.12, -.18)], [.26, .22, .16, .06], stone, step=.30)
        s.path([(side * .40, 4.96, -.22), (side * .54, 5.42, -.34)], [.06, .04], aether, step=.4)

    # suspended crystal core where the chest would be
    s.brush((0, 4.22, .34), .54, crystal, flatten=(.80, 1.10, .72), square=2.0)
    s.brush((0, 4.22, .54), .30, aether, square=2.2)
    s.ring((0, 4.22, .34), .60, .09, gold, axis='z')

    def head():
        s.brush((0, 5.32, .02), .40, stone, flatten=(1.0, 1.06, .96), square=2.5)
        s.box((-.30, 5.34, .26), (.30, 5.46, .40), stone)
        for side in (-1, 1):
            s.brush((side * .16, 5.40, .38), .07, eye, square=2.8)
        s.path([(0, 5.66, .04), (0, 5.86, -.04)], [.24, .16], gold, step=.4)
        s.anchor('head', (0, 5.34, .20))

    def arm(side):
        shoulder = (side * .90, 4.70, .02)
        s.brush(shoulder, .40, stone, flatten=(1.1, .9, 1.0), square=2.4)
        s.path([shoulder, (side * 1.20, 4.20, .10), (side * 1.30, 3.58, .18),
                (side * 1.24, 3.02, .24)], [.30, .25, .20, .17], cloth, step=.30)
        s.brush((side * 1.24, 2.86, .26), .22, stone, flatten=(1.1, .9, 1.0), square=2.6)
        for j, off in enumerate((-.18, 0.0, .18)):
            s.path([(side * (1.24 + off), 2.80, .28), (side * (1.28 + off * 1.2), 2.50, .34),
                    (side * (1.28 + off * 1.3), 2.28, .36)], [.075, .05, .02], stone, step=.34)

    def halo():
        # tilted crown ring with six floating shards above the head
        s.ring((0, 6.10, -.06), 1.02, .075, gold, axis='y', tilt=.34)
        for i in range(6):
            a = 2 * math.pi * i / 6 + .3
            x, z = math.cos(a) * 1.02, math.sin(a) * 1.02
            y = 6.10 + math.sin(a) * 1.02 * math.sin(.34)
            s.brush((x, y, z - .06), .12 + .03 * (i % 2), crystal, square=1.9)
        for i in range(3):
            a = 2 * math.pi * i / 3 + .8
            s.path([(math.cos(a) * .50, 5.86, math.sin(a) * .50),
                    (math.cos(a) * .78, 6.34 + i * .10, math.sin(a) * .78),
                    (math.cos(a) * .92, 6.62 + i * .12, math.sin(a) * .92)],
                   [.09, .06, .02], aether, step=.4)

    s.part('head', (0, 5.06, .02), head)
    s.part('arm_l', (-.90, 4.70, .02), lambda: arm(-1))
    s.part('arm_r', (.90, 4.70, .02), lambda: arm(1))
    s.part('halo', (0, 5.70, -.06), halo)
    s.anchor('core', (0, 4.22, .30))
    s.anchor('feet', (0, .4, 0))
    return s


# ---------------------------------------------------------------- titan
def build_titan():
    s = Sculpt('titan', 'Ледяной Панцирь', U, [])
    ice = s.mat(L('glacial-ice'), (.62, .80, .92), 0, 'glacial ice armour')
    deepice = s.mat(L('marble'), (.74, .86, .94), 0, 'compacted blue marble ice')
    metal = s.mat(L('titanium-hull'), (.56, .62, .68), 0, 'riveted titanium joints')
    frost = s.mat(L('crystal'), (.40, .82, 1.0), 2.3, 'frost core')
    crack = s.mat(L('aether'), (.30, .70, 1.0), 1.3, 'glacial cracks')
    eye = s.mat(L('crystal'), (.70, .95, 1.0), 2.6, 'cold eye')

    # --- heavy digitigrade legs
    for side in (-1, 1):
        s.path([(side * .78, 3.35, -.04), (side * .92, 2.68, .16), (side * 1.00, 2.02, .26),
                (side * .90, 1.34, -.10), (side * .84, .74, -.30), (side * .82, .30, -.32)],
               [.62, .56, .50, .42, .36, .32], deepice, step=.28)
        for i in range(3):
            plate(s, side * .94, 2.62 - i * .56, .46 - i * .10, .42, .24, .16, ice, notch=2)
        # backpack of berg shards on the calf
        for k in range(2):
            s.path([(side * (.92 + .1 * k), 1.90 - k * .5, -.30),
                    (side * (1.10 + .1 * k), 1.66 - k * .5, -.70)],
                   [.18, .04], ice, step=.4)
        # broad five-toed foot with metal cleats
        s.box((side * .82 - .56, 0, -.62), (side * .82 + .56, .34, .52), deepice)
        for j, off in enumerate((-.36, -.12, .12, .36)):
            s.box((side * .82 + off - .12, 0, .44), (side * .82 + off + .12, .22, .82), ice)
            s.box((side * .82 + off - .13, 0, .70), (side * .82 + off + .13, .10, .90), metal)

    s.brush((0, 3.42, -.02), 1.10, deepice, flatten=(1.28, .56, .90), square=2.7)
    s.ring((0, 3.40, -.02), .94, .11, metal, axis='y')

    # --- torso: berg plates, spine of shards, frost core
    s.brush((0, 4.60, .02), 1.10, deepice, flatten=(1.22, .84, 1.00), square=2.5)
    for i in range(3):
        y = 4.16 + i * .44
        plate(s, 0, y, .74 - i * .03, .52 - i * .04, .18, .14, ice, notch=3)
    for side in (-1, 1):
        s.path([(side * .34, 4.20, .70), (side * .40, 4.62, .74), (side * .30, 4.96, .70)],
               [.05, .045, .03], crack, step=.5)
        # shoulder bergs
        s.path([(side * .70, 5.32, -.06), (side * 1.06, 5.70, -.20), (side * 1.24, 5.94, -.26)],
               [.44, .30, .10], ice, step=.28)
        s.path([(side * 1.04, 5.62, -.18), (side * 1.28, 6.04, -.28), (side * 1.36, 6.30, -.30)],
               [.18, .10, .03], ice, step=.32)
    # spine shards
    for i in range(4):
        s.path([(0, 4.30 + i * .38, -.52), (0, 4.52 + i * .38, -.96 - .06 * i)],
               [.16 - i * .02, .03], ice, step=.4)
    # frost core in an open chest frame
    s.brush((0, 4.40, .34), .46, frost, flatten=(.86, 1.05, .72), square=2.0)
    s.brush((0, 4.40, .48), .26, frost, square=2.2)
    s.ring((0, 4.40, .30), .50, .08, metal, axis='z')

    def head():
        s.brush((0, 5.84, .04), .46, deepice, flatten=(1.02, 1.0, .98), square=2.4)
        brow(s, .42, 5.98, .46, metal, .12)
        eyes(s, .19, 5.84, .46, .08, eye)
        s.box((-.18, 5.66, .28), (.18, 5.76, .58), metal)
        s.anchor('head', (0, 5.86, .28))

    def jaw():
        s.box((-.28, 5.54, .20), (.28, 5.68, .58), ice)
        for side in (-1, 1):
            s.box((side * .16 - .05, 5.48, .40), (side * .16 + .05, 5.58, .60), metal)

    def arm(side):
        shoulder = (side * 1.34, 5.06, -.02)
        s.brush(shoulder, .54, ice, flatten=(1.05, .95, 1.0), square=2.4)
        s.path([shoulder, (side * 1.70, 4.42, .12), (side * 1.84, 3.74, .22),
                (side * 1.78, 3.06, .28)], [.48, .42, .38, .34], deepice, step=.28)
        for i in range(3):
            plate(s, side * 1.80, 3.76 - i * .46, .44, .38, .20, .18, ice, notch=2)
        s.path([(side * 1.78, 3.06, .28), (side * 1.74, 2.62, .44)], [.34, .30], metal, step=.3)
        s.brush((side * 1.74, 2.36, .50), .42, deepice, flatten=(1.05, .92, 1.05), square=2.4)
        for j, off in enumerate((-.22, 0.0, .22)):
            s.path([(side * (1.74 + off), 2.30, .54), (side * (1.78 + off * 1.15), 2.02, .70),
                    (side * (1.78 + off * 1.25), 1.80, .82)], [.11, .08, .03], ice, step=.32)

    def halo():
        # crown of vertical ice spikes, leaning back, one snapped short
        for i, (x, h) in enumerate(((-.34, 7.10), (-.12, 7.30), (.12, 7.22), (.34, 6.96))):
            s.path([(x, 6.10, -.10), (x * 1.35, h - .20, -.30), (x * 1.55, h, -.44)],
                   [.18, .10, .03], ice, step=.30)
        for side in (-1, 1):
            s.path([(side * .30, 6.06, -.22), (side * .58, 6.42, -.44), (side * .70, 6.72, -.52)],
                   [.12, .08, .025], ice, step=.34)

    s.part('head', (0, 5.58, .02), head)
    s.part('jaw', (0, 5.60, .18), jaw)
    s.part('arm_l', (-1.34, 5.06, -.02), lambda: arm(-1))
    s.part('arm_r', (1.34, 5.06, -.02), lambda: arm(1))
    s.part('halo', (0, 6.00, -.14), halo)
    s.anchor('core', (0, 4.40, .34))
    s.anchor('feet', (0, .1, 0))
    return s


# ---------------------------------------------------------------- ember
def build_ember():
    s = Sculpt('ember', 'Страж Солнечного Ядра', U, [])
    hull = s.mat(L('obsidian'), (.18, .16, .17), 0, 'obsidian chassis')
    steel = s.mat(L('hazard-steel'), (.48, .42, .30), 0, 'worn steel housing')
    brass = s.mat(L('metal-ingot'), (.74, .50, .20), 0, 'brass pincers')
    magma = s.mat(L('magma-basalt'), (1.0, .34, .04), 2.6, 'molten core')
    ember = s.mat(L('crystal'), (1.0, .62, .12), 2.2, 'ember glow')
    eye = s.mat(L('crystal'), (1.0, .84, .40), 2.8, 'furnace eye')

    # hovering thruster pods instead of legs
    for side in (-1, 1):
        s.path([(side * .58, 2.46, -.06), (side * .66, 2.06, -.02)], [.36, .30], steel, step=.3)
        s.brush((side * .68, 1.90, -.02), .30, hull, flatten=(1, .7, 1), square=2.8)
        s.brush((side * .68, 1.74, -.02), .20, magma, square=2.2)
        s.path([(side * .68, 2.60, -.40), (side * .74, 3.10, -.52)], [.10, .05], steel, step=.4)

    # compact chassis, wider at the shoulders
    s.brush((0, 3.02, -.06), .86, hull, flatten=(1.15, .62, .92), square=2.6)
    s.brush((0, 3.92, -.02), 1.02, steel, flatten=(1.22, .86, .98), square=2.5)
    for i in range(3):
        plate(s, 0, 3.54 + i * .34, -.52 - i * .03, .46 - i * .05, .15, .12, hull, notch=2)
    # heat vents on the back
    for i in range(3):
        s.box((-.34, 3.60 + i * .30, -.74), (.34, 3.68 + i * .30, -.64), magma)

    # the huge exposed molten core, cradled in front of the chest
    core = (0, 3.66, .80)
    s.brush(core, .86, magma, square=2.0)
    s.brush((core[0], core[1], core[2] + .22), .54, ember, square=2.2)
    s.brush((core[0], core[1] + .10, core[2] + .38), .24, eye, square=2.4)
    s.ring(core, .94, .10, brass, axis='z', tilt=.10)

    # four curved pincer arms holding the core
    for side in (-1, 1):
        for high in (True, False):
            y0 = 4.24 if high else 3.02
            y1 = 4.06 if high else 3.20
            s.path([(side * .66, y0, .12), (side * 1.10, (y0 + y1) / 2, .54),
                    (side * 1.04, y1, .98), (side * .58, y1 + (.14 if high else -.14), 1.10)],
                   [.26, .21, .16, .06], brass, step=.30)
    s.path([(0, 4.52, -.10), (0, 4.72, .18), (0, 4.60, .52)], [.22, .16, .10], steel, step=.34)

    def head():
        s.brush((0, 4.86, .06), .38, hull, flatten=(1.08, .90, .96), square=2.6)
        brow(s, .32, 4.98, .38, steel, .10)
        eyes(s, .15, 4.86, .38, .06, eye)
        # furnace grille on the lower face
        for i in range(3):
            s.box((-.20, 4.62 + i * .08, .34), (.20, 4.66 + i * .08, .42), magma)
        s.anchor('head', (0, 4.86, .22))

    def arm(side):
        shoulder = (side * .78, 4.16, -.02)
        s.brush(shoulder, .32, steel, flatten=(1.05, .95, 1.0), square=2.4)
        s.path([shoulder, (side * 1.06, 3.86, .18), (side * 1.18, 3.44, .34),
                (side * 1.10, 3.04, .44)], [.24, .20, .17, .14], hull, step=.30)
        s.brush((side * 1.10, 2.92, .46), .18, steel, square=2.6)
        for j, off in enumerate((-.14, .02, .18)):
            s.path([(side * (1.10 + off), 2.84, .48), (side * (1.14 + off), 2.56, .60),
                    (side * (1.14 + off), 2.38, .66)], [.06, .04, .02], brass, step=.34)

    def halo():
        # orbiting shards and short brass arcs, no flat disc behind the core
        for i in range(11):
            a = 2 * math.pi * i / 11
            s.brush((math.cos(a) * 1.30, 4.34 + math.sin(a) * 1.16, .34 + math.sin(a * 1.9) * .50),
                    .12 + .025 * (i % 3), magma, square=2.0)
        for i in range(7):
            a = 2 * math.pi * i / 7 + .5
            s.path([(math.cos(a) * 1.52, 3.40 + math.sin(a) * .90, .34 + math.sin(a) * .90),
                    (math.cos(a) * 1.70, 3.30 + math.sin(a) * .98, .34 + math.sin(a) * .98)],
                   [.075, .03], steel, step=.4)
        for i in range(4):
            a = 2 * math.pi * i / 4 + .4
            s.path([(math.cos(a) * 1.10, 3.30, .30 + math.sin(a) * 1.10),
                    (math.cos(a) * 1.34, 3.16, .30 + math.sin(a) * 1.34)],
                   [.07, .03], ember, step=.4)

    s.part('head', (0, 4.56, .06), head)
    s.part('arm_l', (-.78, 4.16, -.02), lambda: arm(-1))
    s.part('arm_r', (.78, 4.16, -.02), lambda: arm(1))
    s.part('halo', (0, 3.62, .30), halo)
    s.anchor('core', core)
    s.anchor('feet', (0, 1.6, 0))
    return s


BUILDERS = {
    'warden': build_warden,
    'colossus': build_colossus,
    'archon': build_archon,
    'titan': build_titan,
    'ember': build_ember,
}


def main():
    sculpts = [fn() for fn in BUILDERS.values()]
    write_all(sculpts)
    for sc in sculpts:
        body = len(sc.cells) - sum(len(p['cells']) // 4 for p in sc.parts)
        ys = [k[1] for k in sc.cells]
        print('%-9s height %4.2f m  body %5d  parts %s' % (
            sc.key, (max(ys) + 1) * sc.unit, body,
            ', '.join('%s:%d' % (p['name'], len(p['cells']) // 4) for p in sc.parts)))


if __name__ == '__main__':
    main()
