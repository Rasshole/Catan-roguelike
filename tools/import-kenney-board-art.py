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
TOP_FACE_HEIGHT_FRAC = 0.72

# Plain terrain tops only (no buildings). Graded toward Catan-style palette in PALETTE_RGB.
TERRAIN_SOURCES: dict[str, str] = {
    "Wood": "hexagon/PNG/Tiles/Terrain/Grass/grass_11.png",
    "Sheep": "hexagon/PNG/Tiles/Terrain/Grass/grass_04.png",
    "Wheat": "hexagon/PNG/Tiles/Terrain/Sand/sand_05.png",
    "Brick": "hexagon/PNG/Tiles/Terrain/Dirt/dirt_05.png",
    "Ore": "hexagon/PNG/Tiles/Terrain/Stone/stone_04.png",
    "Desert": "hexagon/PNG/Tiles/Terrain/Sand/sand_08.png",
    "CoastSand": "hexagon/PNG/Tiles/Terrain/Sand/sand_03.png",
    "SeaLake": "hexagon/PNG/Tiles/Terrain/Grass/grass_06.png",
}

# Target average RGB (0–255) — matches BoardResourcePalette / physical Catan.
PALETTE_RGB: dict[str, tuple[int, int, int]] = {
    "Wood": (46, 158, 56),
    "Sheep": (158, 235, 140),
    "Wheat": (242, 209, 46),
    "Brick": (209, 97, 56),
    "Ore": (125, 128, 135),
    "Desert": (237, 209, 132),
    "CoastSand": (240, 224, 184),
    "SeaLake": (118, 178, 220),
}


def pointy_hex_corners(cx: float, cy: float, radius: float) -> list[tuple[float, float]]:
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


def extract_top_face_raw(src: Path) -> tuple[Image.Image, list[tuple[float, float]]]:
    im = Image.open(src).convert("RGBA")
    w, h = im.size
    y_top_end = detect_top_face_rows(im)
    crop = im.crop((0, 0, w, y_top_end + 1))
    px = crop.load()
    cw, ch = crop.size
    min_x, max_x = cw, 0
    min_y, max_y = ch, 0
    for y in range(ch):
        for x in range(cw):
            r, g, b, a = px[x, y]
            if a < 48 or r + g + b < 110:
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
    radius = min(max_x - min_x, max_y - min_y) * 0.46
    return crop, pointy_hex_corners(cx, cy, radius)


def sample_top_face_to_hex(crop: Image.Image, src_hex: list[tuple[float, float]]) -> Image.Image:
    px = crop.load()
    cw, ch = crop.size
    min_x = min(p[0] for p in src_hex)
    max_x = max(p[0] for p in src_hex)
    min_y = min(p[1] for p in src_hex)
    max_y = max(p[1] for p in src_hex)

    dst_cx = OUT_SIZE * 0.5
    dst_cy = OUT_SIZE * 0.5
    dst_r = OUT_SIZE * 0.475
    dst_hex = pointy_hex_corners(dst_cx, dst_cy, dst_r)

    rgba = Image.new("RGBA", (OUT_SIZE, OUT_SIZE), (0, 0, 0, 0))
    out_px = rgba.load()
    for y in range(OUT_SIZE):
        for x in range(OUT_SIZE):
            if not point_in_poly(x + 0.5, y + 0.5, dst_hex):
                continue
            u = (x - (dst_cx - dst_r)) / (2 * dst_r)
            v = (y - (dst_cy - dst_r)) / (2 * dst_r)
            sx = min_x + u * (max_x - min_x)
            sy = min_y + v * (max_y - min_y)
            if not point_in_poly(sx, sy, src_hex):
                continue
            ix = int(round(sx))
            iy = int(round(sy))
            if 0 <= ix < cw and 0 <= iy < ch:
                r, g, b, a = px[ix, iy]
                if r + g + b < 100:
                    continue
                out_px[x, y] = (r, g, b, 255)
    return rgba


def average_rgb(im: Image.Image) -> tuple[float, float, float]:
    px = im.load()
    w, h = im.size
    rs = gs = bs = n = 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 20:
                continue
            rs += r
            gs += g
            bs += b
            n += 1
    if n == 0:
        return 128.0, 128.0, 128.0
    return rs / n, gs / n, bs / n


def grade_to_palette(im: Image.Image, target: tuple[int, int, int], strength: float = 0.72) -> Image.Image:
    tr, tg, tb = target
    ar, ag, ab = average_rgb(im)
    ar = max(ar, 1.0)
    ag = max(ag, 1.0)
    ab = max(ab, 1.0)
    sr = tr / ar
    sg = tg / ag
    sb = tb / ab
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 20:
                continue
            nr = r * sr
            ng = g * sg
            nb = b * sb
            r = int(r + (nr - r) * strength)
            g = int(g + (ng - g) * strength)
            b = int(b + (nb - b) * strength)
            px[x, y] = (max(0, min(255, r)), max(0, min(255, g)), max(0, min(255, b)), a)
    return im


def nudge_average_to_target(im: Image.Image, target: tuple[int, int, int]) -> Image.Image:
    """Scale RGB so opaque average matches target (keeps texture detail)."""
    tr, tg, tb = target
    ar, ag, ab = average_rgb(im)
    if ar < 1 or ag < 1 or ab < 1:
        return im
    sr, sg, sb = tr / ar, tg / ag, tb / ab
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 20:
                continue
            px[x, y] = (
                max(0, min(255, int(r * sr))),
                max(0, min(255, int(g * sg))),
                max(0, min(255, int(b * sb))),
                a,
            )
    return im


def fill_outside_hex_with_face(im: Image.Image, outline_strength: float = 0.08) -> Image.Image:
    """No charcoal frame: exterior of UV square uses face colour; thin edge darkening only."""
    dst_cx = OUT_SIZE * 0.5
    dst_cy = OUT_SIZE * 0.5
    dst_r = OUT_SIZE * 0.475
    dst_hex = pointy_hex_corners(dst_cx, dst_cy, dst_r)
    ar, ag, ab = average_rgb(im)
    fill = (int(ar), int(ag), int(ab), 255)
    px = im.load()
    for y in range(OUT_SIZE):
        for x in range(OUT_SIZE):
            if point_in_poly(x + 0.5, y + 0.5, dst_hex):
                continue
            px[x, y] = fill
    # subtle outline on hex border
    draw = ImageDraw.Draw(im)
    int_hex = [(int(x), int(y)) for x, y in dst_hex]
    draw.polygon(int_hex, outline=(int(ar * (1 - outline_strength)),
                                    int(ag * (1 - outline_strength)),
                                    int(ab * (1 - outline_strength)), 255))
    return im


def build_terrain_slot(src: Path, slot: str) -> Image.Image:
    crop, src_hex = extract_top_face_raw(src)
    im = sample_top_face_to_hex(crop, src_hex)
    target = PALETTE_RGB[slot]
    im = grade_to_palette(im, target, strength=0.78)
    if slot == "Wheat":
        im = grade_to_palette(im, target, strength=0.92)
        im = ImageEnhance.Color(im.convert("RGB")).enhance(1.22)
        im = im.convert("RGBA")
        im = grade_to_palette(im, target, strength=0.65)
    if slot == "Wood":
        im = grade_to_palette(im, target, strength=0.88)
        im = ImageEnhance.Color(im.convert("RGB")).enhance(1.18)
        im = im.convert("RGBA")
        im = grade_to_palette(im, target, strength=0.4)
    if slot == "SeaLake":
        px = im.load()
        for y in range(OUT_SIZE):
            for x in range(OUT_SIZE):
                r, g, b, a = px[x, y]
                if a < 20:
                    continue
                px[x, y] = (
                    int(r * 0.5 + target[0] * 0.5),
                    int(g * 0.5 + target[1] * 0.5),
                    int(b * 0.5 + target[2] * 0.5),
                    a,
                )
    im = fill_outside_hex_with_face(im)
    return nudge_average_to_target(im, target)


def make_fish_chevron_albedo() -> Image.Image:
    """Chevron albedo: light blue frame + fish on arms (UV matches CoastalFishShoalMesh)."""
    w = h = OUT_SIZE
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    water = (148, 198, 232, 255)
    cx, cy = w // 2, int(h * 0.52)
    # Pentagon matching mesh UV layout (inland top, arms, sea flat bottom)
    inland = (cx, int(h * 0.08))
    arm_l = (int(w * 0.12), int(h * 0.42))
    arm_r = (int(w * 0.88), int(h * 0.42))
    sea_l = (int(w * 0.08), int(h * 0.95))
    sea_r = (int(w * 0.92), int(h * 0.95))
    draw.polygon([inland, arm_l, arm_r, sea_r, sea_l], fill=water)
    fish = (42, 108, 148, 230)
    for fx, fy, sc in [(cx - 55, cy - 10, 1.0), (cx + 40, cy - 35, 0.85), (cx + 10, cy + 25, 0.7)]:
        draw.ellipse((fx - 18 * sc, fy - 8 * sc, fx + 18 * sc, fy + 8 * sc), fill=fish)
        draw.polygon(
            [(fx + 18 * sc, fy), (fx + 30 * sc, fy - 10 * sc), (fx + 30 * sc, fy + 10 * sc)],
            fill=fish,
        )
    return im


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

    for slot, rel in TERRAIN_SOURCES.items():
        path = src_root / rel
        if not path.is_file():
            raise SystemExit(f"Missing terrain source: {path}")
        im = build_terrain_slot(path, slot)
        save_png(BOARD / f"{slot}.png", im)
        print(f"board {slot}")

    save_png(BOARD / "FishShoal.png", make_fish_chevron_albedo())
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

    print("done")


if __name__ == "__main__":
    main()
