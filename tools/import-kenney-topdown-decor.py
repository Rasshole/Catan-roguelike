#!/usr/bin/env python3
"""Bake Kenney Medieval RTS top-down PNGs into Resources/Art/Board/TopDownDecor.

Uses the Retina (128 px) sprites, trims each to its opaque bounds and pads to a square
so the art fills the quad (decor sizes are given as quad diameters). Transparent pixels
get their RGB bled from the nearest opaque pixel so bilinear sampling has no dark fringe.
The pack has no fence or cactus, so those are drawn here in the same flat outlined style.
"""
from __future__ import annotations

import argparse
import math
import zipfile
from io import BytesIO
from pathlib import Path

try:
    from PIL import Image, ImageChops, ImageDraw, ImageFilter
except ImportError:
    raise SystemExit("pip install pillow")

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/_Project/Resources/Art/Board/TopDownDecor"
THIRD = ROOT / "Assets/_Project/Art/ThirdParty/Kenney"
ZIP_DEFAULT = Path("/tmp/kenney_medievalRTSpack.zip")
SIZE = 128
R = "PNG/Retina/"

# Whole sprites: (zip path, output name)
COPIES: tuple[tuple[str, str], ...] = (
    (R + "Environment/medievalEnvironment_01.png", "tree_canopy_a.png"),
    (R + "Environment/medievalEnvironment_02.png", "tree_canopy_b.png"),
    (R + "Environment/medievalEnvironment_03.png", "tree_canopy_c.png"),
    (R + "Environment/medievalEnvironment_08.png", "rock_cluster_a.png"),  # grey boulder pair
    (R + "Environment/medievalEnvironment_09.png", "rock_cluster_b.png"),  # flecked (ore)
    (R + "Environment/medievalEnvironment_11.png", "rock_cluster_c.png"),
    (R + "Environment/medievalEnvironment_18.png", "clay_pit.png"),        # flecked clay mound
    (R + "Environment/medievalEnvironment_14.png", "desert_rock.png"),     # sandstone rock
)
# Plots cut out of grass tiles (grass removed): (zip path, output name, gold tint)
# gold: 0 = original colours, >0 = ripe-wheat tint strength (cream field reads as a crate on the yellow hex)
PLOTS: tuple[tuple[str, str, float], ...] = (
    (R + "Tile/medievalTile_56.png", "wheat_field_a.png", 0.7),   # ripe striped field, light gold
    (R + "Tile/medievalTile_56.png", "wheat_field_b.png", 1.0),   # deeper gold
    (R + "Tile/medievalTile_55.png", "wheat_field_c.png", 0.0),   # ploughed furrows
    (R + "Tile/medievalTile_54.png", "hay_bale.png", 0.0),
)
OUTLINE = (92, 58, 30, 255)
SNOW = (245, 248, 252, 255)
ROCK_DARK = (58, 62, 70, 255)
ROCK_MID = (98, 104, 112, 255)
ROCK_LIGHT = (138, 144, 152, 255)


def trim_pad(im: Image.Image, margin: float = 0.04) -> Image.Image:
    im = im.convert("RGBA")
    bbox = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    if bbox:
        im = im.crop(bbox)
    side = int(max(im.size) * (1 + 2 * margin))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(im, ((side - im.width) // 2, (side - im.height) // 2), im)
    return canvas.resize((SIZE, SIZE), Image.LANCZOS)


def cut_plot(im: Image.Image) -> Image.Image:
    """Keep the non-grass plot in a Kenney grass tile."""
    im = im.convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if g > r + 30 and g > b + 20:  # grass greens
                px[x, y] = (r, g, b, 0)
    return im


def strip_frame(im: Image.Image) -> Image.Image:
    """Drop the wooden plot frame so fields read as crop rows, not crates."""
    bbox = im.getchannel("A").getbbox()
    im = im.crop(bbox)
    inset = int(round(min(im.size) * 0.12))
    im = im.crop((inset, inset, im.width - inset, im.height - inset))
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, im.width - 1, im.height - 1), radius=im.width // 6, fill=255)
    from PIL import ImageEnhance
    out.paste(ImageEnhance.Contrast(im).enhance(1.8), (0, 0), mask)
    w = max(2, im.width // 24)
    ImageDraw.Draw(out).rounded_rectangle((0, 0, im.width - 1, im.height - 1), radius=im.width // 6,
                                          outline=(128, 82, 34, 255), width=w)
    return out


def slate_boulder(im: Image.Image) -> Image.Image:
    """Darken grey rocks so they read on the light stone hex."""
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 12:
                continue
            t = y / max(h - 1, 1)
            shade = 0.62 + 0.22 * (1.0 - t)
            hi = 1.0 + 0.12 * (1.0 - t)
            px[x, y] = (
                min(255, int(r * shade * hi)),
                min(255, int(g * shade * hi)),
                min(255, int(b * shade * hi + 4 * (1.0 - t))),
                a,
            )
    return im


def gold(im: Image.Image, k: float) -> Image.Image:
    r, g, b, a = im.split()
    r = r.point(lambda v: min(255, int(v * (1 + 0.02 * k) + 6 * k)))
    g = g.point(lambda v: int(v * (1 - 0.14 * k)))
    b = b.point(lambda v: int(v * (1 - 0.58 * k)))
    return Image.merge("RGBA", (r, g, b, a))


def bleed(im: Image.Image) -> Image.Image:
    """Fill RGB of transparent pixels from opaque neighbours (no dark bilinear fringe)."""
    im = im.convert("RGBA")
    alpha = im.getchannel("A")
    rgb = Image.new("RGB", im.size, (0, 0, 0))
    mask = alpha.point(lambda a: 255 if a > 0 else 0)
    rgb.paste(im.convert("RGB"), (0, 0), mask)
    filled = mask
    for _ in range(12):
        grown = rgb.filter(ImageFilter.MaxFilter(3))
        grown_mask = filled.filter(ImageFilter.MaxFilter(3))
        new = ImageChops.subtract(grown_mask, filled)
        rgb.paste(grown, (0, 0), new)
        filled = grown_mask
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out


def make_windmill_topdown() -> Image.Image:
    """Tower + four sails seen from above; wide stone cap reads as a building at board zoom."""
    s = 4
    W = 128 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, cy = W // 2, W // 2
    stone, roof = (168, 162, 150, 255), (196, 88, 58, 255)
    sail = (248, 244, 232, 255)
    # Thick tower body (most of the sprite) so the hex is not “just an X”.
    d.ellipse((cx - 34 * s, cy - 34 * s, cx + 34 * s, cy + 34 * s), fill=stone, outline=OUTLINE, width=4 * s)
    d.ellipse((cx - 26 * s, cy - 26 * s, cx + 26 * s, cy + 26 * s), fill=(188, 182, 172, 255), outline=(120, 108, 96, 255), width=2 * s)
    d.ellipse((cx - 10 * s, cy - 10 * s, cx + 10 * s, cy + 10 * s), fill=(148, 142, 132, 255))
    d.pieslice((cx - 36 * s, cy - 40 * s, cx + 36 * s, cy + 8 * s), 200, 340, fill=roof, outline=OUTLINE)
    sail_len = 34 * s
    for ang in (0, 90, 180, 270):
        rad = math.radians(ang)
        ex = cx + math.cos(rad) * sail_len
        ey = cy + math.sin(rad) * sail_len
        bx = cx + math.cos(rad) * 20 * s
        by = cy + math.sin(rad) * 20 * s
        d.line((bx, by, ex, ey), fill=OUTLINE, width=9 * s)
        d.line((bx, by, ex, ey), fill=sail, width=5 * s)
    return im.resize((128, 128), Image.LANCZOS)


def make_peak() -> Image.Image:
    """Top-down mountain: grey slopes + white snow cap (opaque, no shadow blob)."""
    s = 4
    W = 160 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = W // 2
    base_y = 118 * s
    peak_y = 18 * s
    d.polygon([(cx, peak_y), (cx - 58 * s, base_y), (cx + 58 * s, base_y)], fill=ROCK_DARK, outline=OUTLINE)
    d.polygon([(cx, peak_y + 8 * s), (cx - 42 * s, base_y - 4 * s), (cx + 42 * s, base_y - 4 * s)], fill=ROCK_MID)
    d.polygon([(cx, peak_y + 4 * s), (cx - 22 * s, peak_y + 38 * s), (cx + 22 * s, peak_y + 38 * s)], fill=ROCK_LIGHT)
    d.polygon([(cx, peak_y), (cx - 20 * s, peak_y + 32 * s), (cx + 20 * s, peak_y + 32 * s)], fill=SNOW, outline=(200, 210, 220, 255))
    return im.resize((128, 128), Image.LANCZOS)


def make_wheat_rows() -> Image.Image:
    s = 4
    W = 128 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    soil = (168, 118, 52, 255)
    stalk = (214, 178, 72, 255)
    d.rounded_rectangle((8 * s, 8 * s, 120 * s, 120 * s), radius=12 * s, fill=soil, outline=OUTLINE, width=2 * s)
    for row in range(7):
        y = (18 + row * 14) * s
        d.line((14 * s, y, 114 * s, y), fill=stalk, width=3 * s)
        for x in range(16, 112, 10):
            d.ellipse((x * s - 2 * s, y - 5 * s, x * s + 2 * s, y + 2 * s), fill=(236, 204, 96, 255))
    return im.resize((128, 128), Image.LANCZOS)


def outline_bricks(im: Image.Image) -> Image.Image:
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 20:
                continue
            # Light brick faces + mortar gaps so stacks read on the orange brick hex.
            px[x, y] = (min(255, int(r * 0.55 + 110)), min(255, int(g * 0.45 + 48)), min(255, int(b * 0.40 + 36)), a)
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.paste(im, (0, 0), im)
    alpha = im.getchannel("A")
    edge = alpha.filter(ImageFilter.FIND_EDGES)
    ep = edge.load()
    op = out.load()
    for y in range(h):
        for x in range(w):
            if ep[x, y] > 28:
                r, g, b, a = op[x, y]
                if a > 0:
                    op[x, y] = (48, 28, 22, a)
    return out


def make_brick_kiln() -> Image.Image:
    """Beehive brick kiln (front-on, like the other upright Kenney structures): coursed brick
    dome, glowing arched firing door, chimney stack."""
    s = 4
    W = 128 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    brick, mortar, shade = (196, 92, 56, 255), (120, 54, 34, 255), (160, 70, 44, 255)
    # chimney
    d.rectangle((82 * s, 10 * s, 98 * s, 50 * s), fill=(150, 66, 42, 255), outline=OUTLINE, width=3 * s)
    d.rectangle((78 * s, 6 * s, 102 * s, 14 * s), fill=(110, 50, 32, 255), outline=OUTLINE, width=3 * s)
    # dome + base
    d.pieslice((14 * s, 24 * s, 114 * s, 124 * s), 180, 360, fill=brick, outline=OUTLINE, width=4 * s)
    d.rectangle((14 * s, 72 * s, 114 * s, 116 * s), fill=brick, outline=OUTLINE, width=4 * s)
    d.rectangle((18 * s, 74 * s, 110 * s, 76 * s), fill=brick)
    for k, y in enumerate(range(40, 114, 10)):  # brick courses
        y *= s
        d.line((18 * s, y, 110 * s, y), fill=mortar, width=s * 2)
        off = (k % 2) * 8
        for x in range(18 + off, 110, 16):
            d.line((x * s, y, x * s, y + 10 * s), fill=mortar, width=s * 2)
    d.pieslice((14 * s, 24 * s, 114 * s, 124 * s), 180, 360, outline=OUTLINE, width=4 * s)
    d.rectangle((14 * s, 112 * s, 114 * s, 118 * s), fill=shade, outline=OUTLINE, width=3 * s)
    # firing door
    d.pieslice((46 * s, 70 * s, 82 * s, 106 * s), 180, 360, fill=(52, 30, 24, 255), outline=OUTLINE, width=3 * s)
    d.rectangle((46 * s, 88 * s, 82 * s, 112 * s), fill=(52, 30, 24, 255), outline=OUTLINE, width=3 * s)
    d.rectangle((49 * s, 88 * s, 79 * s, 109 * s), fill=(52, 30, 24, 255))
    d.ellipse((54 * s, 92 * s, 74 * s, 110 * s), fill=(255, 160, 60, 255))
    d.ellipse((59 * s, 98 * s, 69 * s, 108 * s), fill=(255, 226, 120, 255))
    return im.resize((128, 128), Image.LANCZOS)


def make_brick_pallet() -> Image.Image:
    """Stack of fired bricks on a wooden pallet, light mortar lines so it reads on the clay hex."""
    s = 4
    W = 128 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    brick, hi = (214, 104, 62, 255), (238, 146, 98, 255)
    d.rectangle((12 * s, 100 * s, 116 * s, 116 * s), fill=(150, 100, 52, 255), outline=OUTLINE, width=3 * s)
    for x in (22, 60, 98):
        d.rectangle(((x - 6) * s, 106 * s, (x + 6) * s, 116 * s), fill=(110, 72, 36, 255))
    rows = [(16, 112, 4), (24, 104, 4), (34, 94, 3), (44, 84, 2)]
    y = 100
    for x0, x1, n in rows:
        h = 16
        w = (x1 - x0) / n
        for k in range(n):
            bx0, bx1 = x0 + k * w, x0 + (k + 1) * w
            d.rectangle((bx0 * s, (y - h) * s, bx1 * s, y * s), fill=brick, outline=OUTLINE, width=3 * s)
            d.line(((bx0 + 3) * s, (y - h + 4) * s, (bx1 - 3) * s, (y - h + 4) * s), fill=hi, width=2 * s)
        y -= h
    return im.resize((128, 128), Image.LANCZOS)


def make_windmill_kenney(zf: zipfile.ZipFile) -> Image.Image:
    """Kenney mill body (Structure_11) with sails (Structure_14) on the roof."""
    body = Image.open(BytesIO(zf.read(R + "Structure/medievalStructure_11.png"))).convert("RGBA")
    sails = Image.open(BytesIO(zf.read(R + "Structure/medievalStructure_14.png"))).convert("RGBA")
    canvas = Image.new("RGBA", (192, 192), (0, 0, 0, 0))
    canvas.alpha_composite(body, (32, 44))
    canvas.alpha_composite(sails.resize((150, 150), Image.LANCZOS), (21, 4))
    return canvas


def make_windmill_oblique() -> Image.Image:
    """Stone tower + cap + crossed sails (upright), dark outlines — reads on yellow wheat hex."""
    s = 4
    W, H = 140 * s, 156 * s
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse((26 * s, 128 * s, 114 * s, 146 * s), fill=(0, 0, 0, 48))
    stone, stone_hi = (102, 94, 82, 255), (132, 124, 112, 255)
    roof, sail = (188, 72, 44, 255), (248, 240, 224, 255)
    # tapered tower (three-quarter view)
    d.polygon([(70 * s, 120 * s), (40 * s, 54 * s), (100 * s, 54 * s)], fill=stone, outline=OUTLINE)
    d.polygon([(70 * s, 118 * s), (46 * s, 60 * s), (94 * s, 60 * s)], fill=stone_hi)
    d.rounded_rectangle((58 * s, 92 * s, 82 * s, 118 * s), radius=4 * s, fill=(72, 64, 58, 255), outline=OUTLINE, width=2 * s)
    d.polygon([(70 * s, 30 * s), (36 * s, 56 * s), (104 * s, 56 * s)], fill=roof, outline=OUTLINE)
    cx, cy = 70 * s, 46 * s
    d.rectangle((cx - 3 * s, cy - 38 * s, cx + 3 * s, cy + 10 * s), fill=sail, outline=OUTLINE, width=3 * s)
    d.rectangle((cx - 34 * s, cy - 3 * s, cx + 34 * s, cy + 3 * s), fill=sail, outline=OUTLINE, width=3 * s)
    d.ellipse((cx - 7 * s, cy - 7 * s, cx + 7 * s, cy + 7 * s), fill=(78, 72, 66, 255), outline=OUTLINE, width=2 * s)
    return im


def make_cactus(tall: bool) -> Image.Image:
    s = 4  # supersample
    W = 96 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    dark, mid, light = (31, 94, 48, 255), (56, 150, 72, 255), (120, 200, 110, 255)
    cx = W // 2
    top, bot = (8 if tall else 30) * s, 88 * s
    w = 15 * s
    arms = [(-1, 46, 24), (1, 34, 18)] if tall else [(1, 52, 14)]

    def capsule(x0, y0, x1, y1, fill):
        d.rounded_rectangle((x0, y0, x1, y1), radius=min(x1 - x0, y1 - y0) // 2, fill=fill)

    for pass_, grow in ((dark, 3 * s), (mid, 0)):
        capsule(cx - w - grow, top - grow, cx + w + grow, bot + grow, pass_)
        for side, y, rise in arms:
            ay = y * s
            ax_out = cx + side * 34 * s
            x0, x1 = sorted((cx, ax_out))
            capsule(x0 - grow, ay - 6 * s - grow, x1 + grow, ay + 6 * s + grow, pass_)
            capsule(ax_out - 7 * s - grow, ay - rise * s - grow, ax_out + 7 * s + grow, ay + 6 * s + grow, pass_)
    for dx in (-6, 0, 6):  # ribs
        d.line((cx + dx * s, top + 8 * s, cx + dx * s, bot - 6 * s), fill=dark if dx else light, width=2 * s)
    d.ellipse((cx - 5 * s, top - 3 * s, cx + 5 * s, top + 7 * s), fill=(236, 120, 150, 255), outline=dark, width=s)
    return im.resize((96, 96), Image.LANCZOS)


def make_desert_pond_water(zf: zipfile.ZipFile) -> Image.Image:
    im = Image.open(BytesIO(zf.read(R + "Tile/medievalTile_28.png"))).convert("RGBA")
    side = min(im.size)
    cx, cy = im.width // 2, im.height // 2
    im = im.crop((cx - side // 2, cy - side // 2, cx + side // 2, cy + side // 2))
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).ellipse((4, 4, im.width - 5, im.height - 5), fill=255)
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.paste(im, (0, 0), mask)
    return out


def make_pond_reed() -> Image.Image:
    """Cattail clump: blade leaves + brown heads, outlined like the Kenney art."""
    s = 4
    W = 96 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    dark, leaf, head = (30, 92, 46, 255), (70, 160, 78, 255), (124, 78, 40, 255)
    base = (W // 2, 88 * s)
    for tip, w in (((22, 34), 6), ((74, 30), 6), ((36, 20), 5), ((62, 16), 5), ((50, 8), 5)):
        tx, ty = tip[0] * s, tip[1] * s
        d.line((base[0], base[1], tx, ty), fill=dark, width=(w + 3) * s)
        d.line((base[0], base[1], tx, ty), fill=leaf, width=w * s)
    for hx, hy in ((36, 24), (62, 20)):
        d.rounded_rectangle(((hx - 6) * s, (hy - 2) * s, (hx + 6) * s, (hy + 22) * s), radius=6 * s,
                            fill=head, outline=OUTLINE, width=2 * s)
    return im.resize((96, 96), Image.LANCZOS)


def make_wheat_sheaf(zf: zipfile.ZipFile) -> Image.Image:
    """Tied sheaf: fanned golden stalks with ear tips and a brown band."""
    import math
    s = 4
    W = 128 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    stalk, ear, band = (226, 168, 60, 255), (244, 196, 84, 255), (140, 88, 40, 255)
    cx, tie_y, foot = W // 2, 74 * s, 116 * s
    for i in range(-4, 5):
        a = math.radians(i * 9)
        tx, ty = cx + math.sin(a) * 56 * s, tie_y - math.cos(a) * 58 * s
        fx = cx + i * 3 * s
        for x0, y0, x1, y1 in ((cx, tie_y, tx, ty), (cx, tie_y, fx, foot)):
            d.line((x0, y0, x1, y1), fill=OUTLINE, width=9 * s)
        for x0, y0, x1, y1 in ((cx, tie_y, tx, ty), (cx, tie_y, fx, foot)):
            d.line((x0, y0, x1, y1), fill=stalk, width=5 * s)
        d.ellipse((tx - 7 * s, ty - 11 * s, tx + 7 * s, ty + 11 * s), fill=ear, outline=OUTLINE, width=2 * s)
    d.rounded_rectangle((cx - 18 * s, tie_y - 6 * s, cx + 18 * s, tie_y + 6 * s), radius=4 * s,
                        fill=band, outline=OUTLINE, width=2 * s)
    return im.resize((128, 128), Image.LANCZOS)


GRASS = {(39, 174, 96), (36, 161, 89), (41, 184, 101)}


def make_tree_cluster(zf: zipfile.ZipFile, src: str) -> Image.Image:
    """Kenney forest tile with its grass ground removed: a clump of 4-6 trees."""
    im = Image.open(BytesIO(zf.read(src))).convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if (r, g, b) in GRASS:
                px[x, y] = (r, g, b, 0)
    return im


def make_fence() -> Image.Image:
    s = 4
    W = 128 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    wood, light = (176, 112, 58, 255), (214, 152, 88, 255)
    y0, y1 = 44 * s, 84 * s
    for y in (54 * s, 74 * s):  # rails
        d.rounded_rectangle((8 * s, y - 5 * s, 120 * s, y + 5 * s), radius=3 * s, fill=wood, outline=OUTLINE, width=2 * s)
        d.line((12 * s, y - 2 * s, 116 * s, y - 2 * s), fill=light, width=s)
    for x in (14, 44, 74, 104):  # posts
        d.rounded_rectangle(((x - 6) * s, y0, (x + 6) * s, y1), radius=3 * s, fill=wood, outline=OUTLINE, width=2 * s)
        d.line(((x - 2) * s, y0 + 4 * s, (x - 2) * s, y1 - 4 * s), fill=light, width=s)
    return im.resize((128, 128), Image.LANCZOS)


def save(name: str, im: Image.Image, pad: bool = True) -> None:
    im = trim_pad(im) if pad else im
    bleed(im).save(OUT / name, "PNG")
    print(name)


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--zip", type=Path, default=ZIP_DEFAULT)
    a = p.parse_args()
    if not a.zip.is_file():
        raise SystemExit(f"Missing Medieval RTS zip (kenney.nl/assets/medieval-rts): {a.zip}")
    OUT.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(a.zip) as zf:
        lic = [n for n in zf.namelist() if n.lower().endswith("license.txt")]
        if lic:
            (THIRD / "medieval-rts-CC0.txt").write_bytes(zf.read(lic[0]))
        for src, dest in COPIES:
            im = Image.open(BytesIO(zf.read(src)))
            if dest.startswith("rock_cluster"):
                im = slate_boulder(im)
            save(dest, im)
        for src, dest, tint in PLOTS:
            plot = cut_plot(Image.open(BytesIO(zf.read(src))))
            if dest.startswith("wheat_field"):
                plot = strip_frame(plot)
            save(dest, gold(plot, tint) if tint else plot)
        save("desert_pond_water.png", make_desert_pond_water(zf))
        save("wheat_sheaf.png", make_wheat_sheaf(zf))
        save("tree_cluster_a.png", make_tree_cluster(zf, R + "Tile/medievalTile_44.png"))  # cypress clump
        save("tree_cluster_b.png", make_tree_cluster(zf, R + "Tile/medievalTile_48.png"))  # pine clump
    # v106 review: the oblique tower rendered as a small dark funnel on the board; the Kenney
    # body+sails composite reads as a windmill from the top-down camera.
    with zipfile.ZipFile(a.zip) as zf2:
        save("windmill.png", make_windmill_kenney(zf2))
    save("brick_stack.png", make_brick_pallet())
    save("brick_crates.png", make_brick_pallet())
    save("mountain_peak.png", make_peak())
    save("wheat_rows.png", make_wheat_rows())
    save("brick_kiln.png", make_brick_kiln())
    save("cactus_tall.png", make_cactus(True))
    save("pond_reed.png", make_pond_reed())
    save("cactus_short.png", make_cactus(False))
    save("fence.png", make_fence())


if __name__ == "__main__":
    main()
