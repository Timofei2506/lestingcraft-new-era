"""Собирает короткий вертикальный ролик из настоящих кадров сборки.

Ничего не дорисовывается: только скриншоты, снятые автоматически самой игрой
(`Builds/.../SmokeResults`), плюс подписи и логотип проекта.

Запуск:
    python3 Tools/make_teaser.py            # → Docs/media/teaser.mp4
"""
import os
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[1]
SHOTS = Path(os.environ.get("SHOTS_DIR", "/tmp/shots"))
OUT = Path(os.environ.get("OUT", ROOT / "Docs/media/teaser.mp4"))
FONT_DIR = Path("/usr/share/fonts/truetype/dejavu")

W, H = 1080, 1350
FPS = 30

SCENES = [
    ("menu.png", "LestingCraft: New Era", "Воксельная песочница на Unity 6", 3.0),
    ("biome-dunes.png", "Мир, который строится", "Биомы, пещеры, руда, деревни", 3.0),
    ("workbench-3x3.png", "Крафт", "Верстак 3×3 и книга рецептов", 3.0),
    ("villager-trade.png", "Деревни и жители", "Торговля, животные, открытый мир", 3.0),
    ("world-hud.png", "Кооператив до 8 игроков", "Открыл мир — друг зашёл по адресу", 3.2),
]

END = ("lestingcraft.kroterallitir.workers.dev", "Скачать для Windows и Android", 3.0)


def font(size, bold=True):
    name = "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"
    return ImageFont.truetype(str(FONT_DIR / name), size)


def cover(image, width, height, zoom, dx=0.0, dy=0.0):
    """Кадр без искажений: масштаб с обрезкой и медленным наездом."""
    ratio = max(width / image.width, height / image.height) * zoom
    resized = image.resize((int(image.width * ratio), int(image.height * ratio)), Image.LANCZOS)
    left = int((resized.width - width) * (0.5 + dx))
    top = int((resized.height - height) * (0.5 + dy))
    left = max(0, min(left, resized.width - width))
    top = max(0, min(top, resized.height - height))
    return resized.crop((left, top, left + width, top + height))


def shade(canvas):
    """Тёмная подложка под текст: снизу и сверху, чтобы подписи читались."""
    veil = Image.new("L", canvas.size, 0)
    draw = ImageDraw.Draw(veil)
    for y in range(canvas.height):
        bottom = max(0.0, (y - canvas.height * 0.52) / (canvas.height * 0.48))
        top = max(0.0, (canvas.height * 0.22 - y) / (canvas.height * 0.22))
        draw.line([(0, y), (canvas.width, y)], fill=int(235 * max(bottom ** 1.4, top ** 1.6 * 0.85)))
    canvas.paste(Image.new("RGB", canvas.size, (7, 9, 12)), (0, 0), veil)


def caption(canvas, title, subtitle, alpha=1.0):
    draw = ImageDraw.Draw(canvas)
    pad, x = 64, 56
    title_font, sub_font = font(62), font(34, bold=False)
    lines = []
    words = title.split()
    line = ""
    for word in words:
        probe = (line + " " + word).strip()
        if draw.textlength(probe, font=title_font) > W - 2 * x:
            lines.append(line)
            line = word
        else:
            line = probe
    lines.append(line)
    y = int(H * 0.74)
    for text_line in lines:
        draw.text((x, y), text_line, font=title_font, fill=(255, 255, 255), stroke_width=1, stroke_fill=(0, 0, 0))
        y += 76
    draw.text((x, y + 6), subtitle, font=sub_font, fill=(232, 178, 92))
    draw.line([(x, y + 74), (x + 140, y + 74)], fill=(232, 178, 92), width=4)


def brand(canvas):
    draw = ImageDraw.Draw(canvas)
    draw.text((56, 48), "LESTINGCRAFT", font=font(38), fill=(255, 255, 255))
    draw.text((56, 96), "NEW ERA", font=font(26, bold=False), fill=(232, 178, 92))


def frame(scene, t, duration):
    """Один кадр: наезд камеры + подписи + плавное появление."""
    path, title, subtitle, _ = scene
    image = Image.open(SHOTS / path).convert("RGB")
    progress = t / duration
    canvas = cover(image, W, H, 1.0 + 0.06 * progress, dx=-0.02 + 0.04 * progress)
    shade(canvas)
    caption(canvas, title, subtitle)
    brand(canvas)
    fade = min(1.0, t / 0.45, (duration - t) / 0.45)
    if fade < 1.0:
        canvas = Image.blend(Image.new("RGB", canvas.size, (7, 9, 12)), canvas, max(0.0, fade))
    return canvas


def end_card(t, duration):
    canvas = Image.new("RGB", (W, H), (7, 9, 12))
    image = Image.open(SHOTS / "world-ultra.png").convert("RGB")
    base = cover(image, W, H, 1.08).filter(ImageFilter.GaussianBlur(6))
    canvas.paste(base, (0, 0))
    shade(canvas)
    draw = ImageDraw.Draw(canvas)
    draw.text((56, 120), "LESTINGCRAFT", font=font(74), fill=(255, 255, 255))
    draw.text((56, 210), "NEW ERA", font=font(44, bold=False), fill=(232, 178, 92))
    draw.text((56, int(H * 0.66)), "Windows и Android", font=font(46), fill=(255, 255, 255))
    draw.text((56, int(H * 0.66) + 70), "Скачать без лаунчера:", font=font(34, bold=False), fill=(210, 214, 220))
    draw.text((56, int(H * 0.66) + 124), END[0], font=font(34), fill=(232, 178, 92))
    progress = t / duration
    draw.rectangle([0, H - 12, W * min(1.0, progress + 0.02), H], fill=(232, 178, 92))
    fade = min(1.0, t / 0.4, (duration - t) / 0.4)
    if fade < 1.0:
        canvas = Image.blend(Image.new("RGB", (W, H), (7, 9, 12)), canvas, max(0.0, fade))
    return canvas


def main():
    for path, *_ in SCENES:
        if not (SHOTS / path).exists():
            sys.exit(f"нет кадра {SHOTS / path}")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    command = [
        ffmpeg, "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
        "-r", str(FPS), "-i", "-", "-an",
        "-c:v", "libx264", "-preset", "medium", "-crf", "23",
        "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(OUT),
    ]
    process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    total = 0
    for scene in SCENES:
        duration = scene[3]
        for index in range(int(duration * FPS)):
            process.stdin.write(frame(scene, index / FPS, duration).tobytes())
            total += 1
    for index in range(int(END[2] * FPS)):
        process.stdin.write(end_card(index / FPS, END[2]).tobytes())
        total += 1
    process.stdin.close()
    if process.wait() != 0:
        sys.exit("ffmpeg завершился с ошибкой")
    size = OUT.stat().st_size
    print(f"{OUT} · {total} кадров · {END[2] + sum(s[3] for s in SCENES):.1f} с · {size / 1024:.0f} КиБ")


if __name__ == "__main__":
    main()
