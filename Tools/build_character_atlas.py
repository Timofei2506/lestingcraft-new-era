"""Detailed 512x512 block-character texture atlas (16 x 128px tiles):
0: natural warm skin
1: woven ranger tunic (forest sage)
2: expedition slate-charcoal trousers
3: adventurer face (open eyes, crisp emerald iris, natural brows & calm mouth)
4: stitched dark saddle-leather boots & bracers
5: bevelled antique-brass buckle
6: chestnut leather straps & pouches
7: weathered olive-umber travel cloak / hood
8: brushed forged steel armor plate
9: riveted steel pauldron / greave plate
10: stranded hair grain (tintable for hair, braids & beards)
11: adventurer face (closed eyes / blink)
12: shaded hand & knuckles skin (for 1st/3rd person hands)
13: ranger tunic front placket with V-collar, lapel seam & brass buttons
14: neutral tintable villager face (open eyes, friendly expression)
15: neutral tintable villager face (closed eyes / blink)
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

R = Path(__file__).resolve().parents[1]
O = R / 'Assets/Art/Characters'
O.mkdir(parents=True, exist_ok=True)

rng = np.random.default_rng(2506)
S = 128


def make_tile(rgb, grain=2.2, weave_amp=0.0, bevel=0.06):
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    base = np.array(rgb, dtype=np.float32)[None, None, :]
    noise = rng.normal(0.0, grain, (S, S, 1)).astype(np.float32)
    # Subtle vertical shading + soft block bevel so cuboids read with crisp 3D form
    vgrad = (1.04 - 0.08 * (y / (S - 1.0)))[:, :, None]
    edge_x = np.minimum(x, S - 1.0 - x)
    edge_y = np.minimum(y, S - 1.0 - y)
    edge = np.minimum(edge_x, edge_y)
    rim = np.clip((5.0 - edge) / 5.0, 0.0, 1.0)
    top_light = ((y < 5) * 0.5 - (y > S - 6) * 0.7 - (x < 4) * 0.35 - (x > S - 5) * 0.35) * bevel * 255.0
    weave = 0.0
    if weave_amp > 0:
        weave = (((x.astype(int) % 4 == 0) * 1.0 - (y.astype(int) % 4 == 0) * 1.0) * weave_amp)[:, :, None]
    arr = base * vgrad + noise + weave + (top_light * (0.4 + 0.6 * rim))[:, :, None]
    return Image.fromarray(np.uint8(np.clip(arr, 0, 255)))


def draw_stitch_margins(im, rgb, margin=7):
    d = ImageDraw.Draw(im)
    dark = tuple(max(0, int(v * 0.68)) for v in rgb)
    stitch = tuple(min(255, int(v * 1.32 + 14)) for v in rgb)
    for ax in (margin, S - 1 - margin):
        d.line((ax, 0, ax, S - 1), fill=dark, width=2)
        for by in range(4, S - 4, 8):
            d.rectangle((ax, by, ax + 1, by + 3), fill=stitch)


def draw_face(skin_rgb, closed=False, bangs=True, iris_rgb=(54, 114, 96)):
    im = make_tile(skin_rgb, grain=1.8, weave_amp=0.0, bevel=0.04)
    px = np.array(im, dtype=np.float32)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)

    # Subtle warm forehead highlight, soft cheek warmth, and gentle chin/jaw contour
    cheek_l = np.exp(-(((x - 28.0) / 16.0) ** 2 + ((y - 78.0) / 12.0) ** 2))
    cheek_r = np.exp(-(((x - 99.0) / 16.0) ** 2 + ((y - 78.0) / 12.0) ** 2))
    cheeks = (cheek_l + cheek_r)[:, :, None]
    px[:, :, 0] += cheeks[:, :, 0] * 11.0
    px[:, :, 1] -= cheeks[:, :, 0] * 4.0
    px[:, :, 2] -= cheeks[:, :, 0] * 5.0

    # Soft jaw / temple AO
    jaw = np.clip((y - 104.0) / 23.0, 0.0, 1.0)[:, :, None]
    px *= (1.0 - 0.11 * jaw)

    im = Image.fromarray(np.uint8(np.clip(px, 0, 255)))
    d = ImageDraw.Draw(im)

    brow_dark = tuple(max(0, int(v * 0.42)) for v in skin_rgb)
    brow_mid = tuple(max(0, int(v * 0.52)) for v in skin_rgb)
    lid_top = tuple(max(0, int(v * 0.62)) for v in skin_rgb)
    lid_low = tuple(max(0, int(v * 0.86)) for v in skin_rgb)
    nose_side = tuple(max(0, int(v * 0.84)) for v in skin_rgb)
    nose_tip = tuple(min(255, int(v * 1.07)) for v in skin_rgb)
    nose_base = tuple(max(0, int(v * 0.72)) for v in skin_rgb)
    lip_upper = tuple(max(0, int(c * s)) for c, s in zip(skin_rgb, (0.76, 0.60, 0.58)))
    lip_line = tuple(max(0, int(c * s)) for c, s in zip(skin_rgb, (0.56, 0.42, 0.40)))
    lip_lower = tuple(min(255, int(c * s)) for c, s in zip(skin_rgb, (0.93, 0.78, 0.76)))

    # Optional sculpted hair fringe at top of forehead
    if bangs:
        d.rectangle((0, 0, S - 1, 18), fill=(64, 43, 30))
        d.rectangle((8, 16, 119, 24), fill=(78, 53, 37))
        # Natural stepped locks across forehead
        for lx, lw, lh in [(4, 18, 30), (26, 16, 27), (52, 22, 28), (82, 16, 26), (104, 20, 30)]:
            d.rectangle((lx, 14, lx + lw, lh), fill=(72, 48, 33))
            d.rectangle((lx + 3, 10, lx + lw - 3, lh - 4), fill=(92, 63, 44))
        # Subtle forehead shadow directly under bangs
        d.rectangle((10, 25, 117, 28), fill=tuple(max(0, int(v * 0.84)) for v in skin_rgb))

    # Symmetrical expressive eyebrows & crisp eyes
    for idx, x0 in enumerate((25, 77)):
        # Eyebrow with slight natural arch
        if idx == 0:
            d.rectangle((x0, 42, x0 + 26, 46), fill=brow_dark)
            d.rectangle((x0 + 3, 40, x0 + 23, 43), fill=brow_mid)
        else:
            d.rectangle((x0, 42, x0 + 26, 46), fill=brow_dark)
            d.rectangle((x0 + 3, 40, x0 + 23, 43), fill=brow_mid)

        if closed:
            d.rectangle((x0 + 1, 52, x0 + 25, 61), fill=lid_low)
            d.rectangle((x0 + 1, 58, x0 + 25, 61), fill=brow_dark)
            d.rectangle((x0 + 3, 61, x0 + 23, 62), fill=lid_top)
        else:
            # Upper lash crease
            d.rectangle((x0 + 1, 49, x0 + 25, 52), fill=lid_top)
            # Crisp bright sclera
            d.rectangle((x0 + 2, 52, x0 + 24, 65), fill=(242, 240, 235))
            d.rectangle((x0 + 2, 52, x0 + 24, 54), fill=(214, 212, 208))
            # Iris positioned slightly inward so gaze converges naturally
            ix = x0 + (9 if idx == 0 else 7)
            d.rectangle((ix, 52, ix + 10, 65), fill=tuple(max(0, int(v * 0.72)) for v in iris_rgb))
            d.rectangle((ix + 1, 53, ix + 9, 64), fill=iris_rgb)
            d.rectangle((ix + 2, 59, ix + 8, 64), fill=tuple(min(255, int(v * 1.24)) for v in iris_rgb))
            # Pupil
            d.rectangle((ix + 3, 55, ix + 7, 61), fill=(20, 24, 26))
            # Crisp specular catchlights
            d.rectangle((ix + 2, 54, ix + 4, 56), fill=(252, 254, 255))
            d.point((ix + 7, 62), fill=(232, 244, 240))
            # Upper lash line & subtle lower lid
            d.rectangle((x0 + 1, 50, x0 + 25, 52), fill=brow_dark)
            d.rectangle((x0 + 2, 65, x0 + 24, 67), fill=lid_low)

    # Subtle sculpted nose bridge & nostrils (complements 3D nose relief cube)
    d.rectangle((57, 60, 70, 81), fill=nose_side)
    d.rectangle((60, 61, 67, 78), fill=nose_tip)
    d.rectangle((58, 80, 69, 83), fill=nose_base)

    # Clean, calm, natural mouth (no oversized dark rectangle!)
    d.rectangle((48, 95, 79, 98), fill=lip_upper)
    d.rectangle((46, 98, 81, 100), fill=lip_line)
    d.rectangle((50, 100, 77, 104), fill=lip_lower)
    # Subtle corner dimples
    d.rectangle((45, 97, 46, 99), fill=nose_base)
    d.rectangle((81, 97, 82, 99), fill=nose_base)
    return im


def make_hair_tile():
    im = make_tile((138, 106, 80), grain=3.2, weave_amp=0.0, bevel=0.04)
    arr = np.array(im, dtype=np.float32)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    # Vertical hair strand waves + layered highlights so tinting by hairMaterial looks rich
    strand = np.sin(x * 0.55 + np.sin(y * 0.09) * 1.4) * 11.0 + np.sin(x * 1.35) * 5.5
    crown = np.sin(y * 0.11) * 6.0
    arr += (strand + crown)[:, :, None]
    return Image.fromarray(np.uint8(np.clip(arr, 0, 255)))


def make_hand_tile(skin_rgb):
    im = make_tile(skin_rgb, grain=2.0, weave_amp=0.0, bevel=0.07)
    d = ImageDraw.Draw(im)
    knuckle_hi = tuple(min(255, int(v * 1.09)) for v in skin_rgb)
    crease = tuple(max(0, int(v * 0.78)) for v in skin_rgb)
    cuff_ao = tuple(max(0, int(v * 0.84)) for v in skin_rgb)
    # Subtle wrist cuff shadow at top
    d.rectangle((0, 0, S - 1, 9), fill=cuff_ao)
    # Four knuckle highlights & 3 subtle finger seams toward the bottom
    for kx in (16, 44, 72, 100):
        d.rectangle((kx, 38, kx + 14, 48), fill=knuckle_hi)
    for sx in (34, 63, 92):
        d.rectangle((sx, 56, sx + 2, 122), fill=crease)
    d.rectangle((8, 118, 119, 125), fill=crease)
    return im


def make_tunic_front(tunic_rgb):
    im = make_tile(tunic_rgb, grain=2.4, weave_amp=2.0, bevel=0.06)
    draw_stitch_margins(im, tunic_rgb, margin=7)
    d = ImageDraw.Draw(im)
    dark = tuple(max(0, int(v * 0.64)) for v in tunic_rgb)
    lapel = tuple(min(255, int(v * 1.18)) for v in tunic_rgb)
    # Collar V-neck undershirt + lapel trim
    d.polygon([(40, 0), (87, 0), (68, 28), (59, 28)], fill=(182, 168, 142))
    d.line((38, 0, 58, 30), fill=lapel, width=4)
    d.line((89, 0, 69, 30), fill=lapel, width=4)
    # Central placket & brass buttons
    d.rectangle((58, 28, 69, 127), fill=dark)
    d.rectangle((60, 28, 67, 127), fill=lapel)
    for by in (42, 68, 94):
        d.rectangle((61, by, 66, by + 5), fill=(214, 174, 84))
        d.rectangle((62, by + 1, 64, by + 2), fill=(246, 218, 138))
    # Stitched chest pocket on left side
    d.rectangle((18, 44, 46, 74), outline=dark, width=2)
    d.rectangle((18, 44, 46, 51), fill=lapel, outline=dark, width=1)
    return im


def make_armor_tile(variant=0):
    im = make_tile((138, 148, 158), grain=2.4, weave_amp=0.0, bevel=0.08)
    arr = np.array(im, dtype=np.float32)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    sheen = np.exp(-(((x - y * 0.7 - 28.0) / 26.0) ** 2)) * 16.0
    arr += sheen[:, :, None]
    im = Image.fromarray(np.uint8(np.clip(arr, 0, 255)))
    d = ImageDraw.Draw(im)
    d.rectangle((3, 3, 124, 124), outline=(56, 62, 70), width=4)
    d.rectangle((8, 8, 119, 119), outline=(188, 198, 208), width=2)
    if variant == 1:
        d.line((8, 64, 119, 64), fill=(68, 76, 86), width=3)
        d.line((8, 67, 119, 67), fill=(186, 196, 206), width=2)
    for rx, ry in ((16, 16), (111, 16), (16, 111), (111, 111)):
        d.rectangle((rx - 3, ry - 3, rx + 3, ry + 3), fill=(204, 212, 218), outline=(60, 66, 74))
    return im


def make_buckle_tile():
    im = make_tile((98, 68, 42), grain=2.2, weave_amp=1.5, bevel=0.05)
    d = ImageDraw.Draw(im)
    d.rectangle((10, 10, 117, 117), fill=(198, 154, 66), outline=(88, 62, 24), width=5)
    d.rectangle((16, 16, 111, 111), outline=(244, 212, 122), width=4)
    d.rectangle((30, 30, 97, 97), fill=(62, 44, 30), outline=(120, 88, 36), width=4)
    # Central belt prong
    d.rectangle((58, 18, 69, 72), fill=(236, 200, 108), outline=(112, 80, 28), width=2)
    return im


skin_base = (202, 152, 118)
villager_neutral = (236, 214, 198)
tunic_base = (64, 94, 72)
trouser_base = (44, 48, 55)
boot_base = (56, 41, 31)
leather_base = (116, 79, 48)
cloak_base = (82, 74, 56)

atlas = Image.new('RGB', (512, 512))
tiles = {}

tiles[0] = make_tile(skin_base, grain=1.9, weave_amp=0.0, bevel=0.05)

tiles[1] = make_tile(tunic_base, grain=2.4, weave_amp=2.2, bevel=0.06)
draw_stitch_margins(tiles[1], tunic_base)

tiles[2] = make_tile(trouser_base, grain=2.2, weave_amp=1.8, bevel=0.06)
draw_stitch_margins(tiles[2], trouser_base)
d2 = ImageDraw.Draw(tiles[2])
d2.line((10, 62, 117, 62), fill=(32, 35, 40), width=2)
d2.line((10, 65, 117, 65), fill=(60, 66, 74), width=1)

tiles[3] = draw_face(skin_base, closed=False, bangs=True, iris_rgb=(52, 112, 92))

tiles[4] = make_tile(boot_base, grain=2.2, weave_amp=1.2, bevel=0.07)
draw_stitch_margins(tiles[4], boot_base)
d4 = ImageDraw.Draw(tiles[4])
d4.rectangle((0, 0, S - 1, 14), fill=(74, 54, 40))
d4.line((0, 14, S - 1, 14), fill=(138, 102, 66), width=2)

tiles[5] = make_buckle_tile()

tiles[6] = make_tile(leather_base, grain=2.5, weave_amp=1.4, bevel=0.06)
draw_stitch_margins(tiles[6], leather_base)

tiles[7] = make_tile(cloak_base, grain=2.5, weave_amp=2.2, bevel=0.06)
draw_stitch_margins(tiles[7], cloak_base)

tiles[8] = make_armor_tile(0)
tiles[9] = make_armor_tile(1)
tiles[10] = make_hair_tile()
tiles[11] = draw_face(skin_base, closed=True, bangs=True, iris_rgb=(52, 112, 92))
tiles[12] = make_hand_tile(skin_base)
tiles[13] = make_tunic_front(tunic_base)
tiles[14] = draw_face(villager_neutral, closed=False, bangs=False, iris_rgb=(66, 96, 122))
tiles[15] = draw_face(villager_neutral, closed=True, bangs=False, iris_rgb=(66, 96, 122))

for idx in range(16):
    atlas.paste(tiles[idx], ((idx % 4) * S, (idx // 4) * S))

atlas.save(O / 'BlockCharacter.png', optimize=True)
print('Character atlas: 512x512, 16 crisp 128px character/villager/hand/hair/armor tiles')
