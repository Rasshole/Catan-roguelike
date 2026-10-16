#!/usr/bin/env python3
"""Bake Kenney Hexagon Pack top faces + boardgame dice into Resources/Art."""
from __future__ import annotations

import argparse
import math
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageEnhance
except ImportError:
    raise SystemExit("pip install pillow")

ROOT = Path(__file__).resolve().parents[1]
BOARD = ROOT / "Assets/_Project/Resources/Art/Board"
BOARDGAME = ROOT / "Assets/_Project/Resources/Art/Boardgame"
THIRD = ROOT / "Assets/_Project/Art/ThirdParty"
NATURE_TEX = ROOT / "Assets/_Project/Resources/Art/Models/Nature/Textures"

OUT_SIZE = 256
# Kenney 120×140 pointy-top: coloured top face sits above the dark side band (~lower 28%).
TOP_FACE_HEIGHT_FRAC = 0.72


TERRAIN_SOURCES: dict[str, tuple[str, float, float | None]] = {
    # rel path, rotate_deg, optional color_saturation multiplier
    "Wood": ("hexagon/PNG/Tiles/Medieval/medieval_lumber.png", 0.0, None),
    "Sheep": ("hexagon/PNG/Tiles/Terrain/Grass/grass_01.png", 0.0, 1.08),
    "Wheat": ("hexagon/PNG/Tiles/Terrain/Grass/grass_02.png", 0.0, 1.35),
    "Brick": ("hexagon/PNG/Tiles/Terrain/Dirt/dirt_07.png", 0.0, 1.1),
    "Ore": ("hexagon/PNG/Tiles/Medieval/medieval_mine.png", 0.0, None),
    "Desert": ("hexagon/PNG/Tiles/Terrain/Sand/sand_10.png", 0.0, None),
    "CoastSand": ("hexagon/PNG/Tiles/Terrain/Sand/sand_03.png", 0.0, None),
    "SeaLake": ("hexagon/PNG/Tiles/Terrain/Grass/grass_06.png", 0.0, None),
}


def pointy_hex_corners(cx: float, cy: float, radius: float) -> list[tuple[float, float]]:
    """Pointy-top hex; corner 0 at top (matches Unity HexMath corner 0)."""
    pts = []
    for i in range(6):
        ang = math.radians(60 * i - 90)
        pts.append((cx + radius * math.cos(ang), cy + radius * math.sin(ang)))
    return pts


def point_in_poly(x: float, y: float, poly: list[tuple[float, float]]) -> bool:
    inside = False
    n = len(poly)
    for i in range(n):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % n]
        if ((y0 > y) != (y1 > y)) and (
            x < (x1 - x0) * (y - y0) / (y1 - y0 + 1e-9) + x0
        ):
            inside = not inside
    return inside


def detect_top_face_rows(im: Image.Image) -> int:
    """Last row index (inclusive) of the bright top face, excluding dark side band."""
    w, h = im.size
    px = im.load()
    y_max = int(h * TOP_FACE_HEIGHT_FRAC)
    for y in range(h - 1, int(h * 0.35), -1):
        samples = [px[x, y] for x in range(w) if px[x, y][3] > 80]
        if len(samples) < w * 0.12:
            continue
        avg = sum(sum(c[:3]) for c in samples) / (3 * len(samples))
        if avg < 78:
            y_max = min(y_max, y - 1)
            break
    return max(int(h * 0.55), y_max)


def extract_top_face_albedo(src: Path, rotate_deg: float = 0.0, sat_mul: float | None = None) -> Image.Image:
    im = Image.open(src).convert("RGBA")
    if rotate_deg:
        im = im.rotate(rotate_deg, expand=True, resample=Image.Resampling.BICUBIC)

    w, h = im.size
    y_top_end = detect_top_face_rows(im)
    crop = im.crop((0, 0, w, y_top_end + 1))

    # Fit top-face hex bounds inside crop (exclude side pixels by luminance).
    px = crop.load()
    cw, ch = crop.size
    min_x, max_x = cw, 0
    min_y, max_y = ch, 0
    for y in range(ch):
        for x in range(cw):
            r, g, b, a = px[x, y]
            if a < 48:
                continue
            if r + g + b < 105:
                continue
            min_x = min(min_x, x)
            max_x = max(max_x, x)
            min_y = min(min_y, y)
            max_y = max(max_y, y)

    if max_x <= min_x:
        min_x, max_x = 0, cw - 1
        min_y, max_y = 0, ch - 1

    cx = (min_x + max_x) * 0.5
    cy = (min_y + max_y) * 0.5
    radius = min(max_x - min_x, max_y - min_y) * 0.48
    src_hex = pointy_hex_corners(cx, cy, radius)

    out = Image.new("RGBA", (OUT_SIZE, OUT_SIZE), (0, 0, 0, 255))
    out_px = out.load()
    dst_cx = OUT_SIZE * 0.5
    dst_cy = OUT_SIZE * 0.5
    dst_r = OUT_SIZE * 0.46
    dst_hex = pointy_hex_corners(dst_cx, dst_cy, dst_r)

    for y in range(OUT_SIZE):
        for x in range(OUT_SIZE):
            if not point_in_poly(x + 0.5, y + 0.5, dst_hex):
                continue
            # barycentric-style map: normalize to hex AABB then scale to source hex AABB
            u = (x - (dst_cx - dst_r)) / (2 * dst_r)
            v = (y - (dst_cy - dst_r)) / (2 * dst_r)
            sx = (min_x + u * (max_x - min_x))
            sy = (min_y + v * (max_y - min_y))
            if not point_in_poly(sx, sy, src_hex):
                continue
            ix = int(round(sx))
            iy = int(round(sy))
            if 0 <= ix < cw and 0 <= iy < ch:
                out_px[x, y] = px[ix, iy]

    if sat_mul and sat_mul != 1.0:
        rgb = out.convert("RGB")
        rgb = ImageEnhance.Color(rgb).enhance(sat_mul)
        out = Image.merge("RGBA", (*rgb.split(), out.split()[3]))

    return out


def tint_sea(im: Image.Image) -> Image.Image:
    base = im.convert("RGBA")
    px = base.load()
    for y in range(base.height):
        for x in range(base.width):
            r, g, b, a = px[x, y]
            if a < 8:
                continue
            px[x, y] = (
                int(r * 0.45 + 55),
                int(g * 0.62 + 95),
                int(b * 0.75 + 125),
                a,
            )
    return base


def make_fish_shoal() -> Image.Image:
    """Fish silhouettes for chevron overlay (alpha); base colour is shader."""
    out = Image.new("RGBA", (OUT_SIZE, OUT_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(out)
    fish = (38, 108, 148, 220)
    cx, cy = OUT_SIZE // 2, OUT_SIZE // 2
    for ox, oy, sc in [(-42, 6, 1.0), (28, -22, 0.9), (6, 32, 0.75)]:
        fx, fy = cx + ox, cy + oy
        draw.ellipse((fx - 20 * sc, fy - 9 * sc, fx + 20 * sc, fy + 9 * sc), fill=fish)
        draw.polygon(
            [
                (fx + 20 * sc, fy),
                (fx + 34 * sc, fy - 12 * sc),
                (fx + 34 * sc, fy + 12 * sc),
            ],
            fill=fish,
        )
    return out


def save_png(path: Path, im: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGBA").save(path, "PNG")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=Path("/tmp/art-import"))
    parser.add_argument(
        "--quaternius",
        type=Path,
        default=Path("/tmp/quaternius-nature/ultimate-stylized-nature/sources"),
    )
    args = parser.parse_args()
    src_root: Path = args.source

    for slot, (rel, rot, sat) in TERRAIN_SOURCES.items():
        path = src_root / rel
        if not path.is_file():
            raise SystemExit(f"Missing terrain source: {path}")
        im = extract_top_face_albedo(path, rot, sat)
        if slot == "SeaLake":
            im = tint_sea(im)
        if slot == "Wheat":
            im = ImageEnhance.Color(im.convert("RGB")).enhance(1.25)
            im = im.convert("RGBA")
        save_png(BOARD / f"{slot}.png", im)
        print(f"board {slot}")

    save_png(BOARD / "FishShoal.png", make_fish_shoal())
    print("board FishShoal")

    dice_src = Path("/tmp/art-import")
    for i in range(1, 7):
        die = dice_src / f"dieWhite{i}.png"
        if die.is_file():
            save_png(BOARDGAME / "Dice" / f"DieWhite{i}.png", Image.open(die).convert("RGBA"))
    card = dice_src / "cardBack_blue1.png"
    if card.is_file():
        save_png(BOARDGAME / "Cards" / "CardBack_blue.png", Image.open(card).convert("RGBA"))

    nature_out = ROOT / "Assets/_Project/Resources/Art/Models/Nature"
    nature_out.mkdir(parents=True, exist_ok=True)
    fbx_dir = args.quaternius / "FBX"
    for name in ("NormalTree_2.fbx", "Rock_3.fbx", "Flower_2_Clump.fbx"):
        fbx = fbx_dir / name
        if not fbx.is_file():
            raise SystemExit(f"Missing Quaternius model: {fbx}")
        (nature_out / name).write_bytes(fbx.read_bytes())
        print(f"model {name}")

    tex_out = NATURE_TEX
    tex_out.mkdir(parents=True, exist_ok=True)
    tex_src = args.quaternius / "Textures"
    for name in (
        "NormalTree_Bark.png",
        "NormalTree_Leaves.png",
        "Rocks.png",
        "Flowers.png",
        "Grass.png",
    ):
        src = tex_src / name
        if src.is_file():
            save_png(tex_out / name, Image.open(src).convert("RGBA"))
            print(f"tex {name}")

    licenses = THIRD / "Kenney"
    licenses.mkdir(parents=True, exist_ok=True)
    for name, url in [
        ("hexagon-pack-CC0.txt", "https://kenney.nl/assets/hexagon-pack"),
        ("boardgame-pack-CC0.txt", "https://kenney.nl/assets/boardgame-pack"),
    ]:
        p = licenses / name
        if not p.exists():
            p.write_text(f"CC0 1.0 — {url}\n", encoding="utf-8")

    print("done")


if __name__ == "__main__":
    main()
