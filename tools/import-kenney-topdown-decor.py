#!/usr/bin/env python3
"""Bake Kenney Medieval RTS top-down PNGs into Resources/Art/Board/TopDownDecor.

Uses the Retina (128 px) sprites, trims each to its opaque bounds and pads to a square
so the art fills the quad (decor sizes are given as quad diameters). Transparent pixels
get their RGB bled from the nearest opaque pixel so bilinear sampling has no dark fringe.
The pack has no fence or cactus, so those are drawn here in the same flat outlined style.
"""
from __future__ import annotations

import argparse
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
    (R + "Environment/medievalEnvironment_15.png", "brick_stack.png"),     # clay boulders
    (R + "Environment/medievalEnvironment_17.png", "brick_crates.png"),
    (R + "Structure/medievalStructure_01.png", "smelter.png"),             # terracotta kiln tower
    (R + "Environment/medievalEnvironment_14.png", "desert_rock.png"),     # sandstone rock
)
# Big grey outcrop composed from three boulders (the pack has no mountain sprite).
PEAK_PARTS = (
    (R + "Environment/medievalEnvironment_07.png", 1.35, (8, -6)),
    (R + "Environment/medievalEnvironment_10.png", 1.0, (-34, 26)),
    (R + "Environment/medievalEnvironment_06.png", 1.0, (40, 34)),
)
# Plots cut out of grass tiles (grass removed): (zip path, output name, gold tint)
# gold: 0 = original colours, >0 = ripe-wheat tint strength (cream field reads as a crate on the yellow hex)
PLOTS: tuple[tuple[str, str, float], ...] = (
    (R + "Tile/medievalTile_56.png", "wheat_field_a.png", 0.7),   # ripe striped field, light gold
    (R + "Tile/medievalTile_56.png", "wheat_field_b.png", 1.0),   # deeper gold
    (R + "Tile/medievalTile_55.png", "wheat_field_c.png", 0.0),   # ploughed furrows
    (R + "Tile/medievalTile_54.png", "hay_bale.png", 0.0),
)
MILL_BODY = R + "Structure/medievalStructure_11.png"
MILL_SAILS = R + "Structure/medievalStructure_14.png"

OUTLINE = (92, 58, 30, 255)


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


def make_windmill(zf: zipfile.ZipFile) -> Image.Image:
    body = Image.open(BytesIO(zf.read(MILL_BODY))).convert("RGBA")
    sails = Image.open(BytesIO(zf.read(MILL_SAILS))).convert("RGBA")
    canvas = Image.new("RGBA", (192, 192), (0, 0, 0, 0))
    canvas.alpha_composite(body, (32, 44))
    canvas.alpha_composite(sails.resize((150, 150), Image.LANCZOS), (21, 4))
    return canvas


def make_peak(zf: zipfile.ZipFile) -> Image.Image:
    canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    for src, scale, (dx, dy) in PEAK_PARTS:
        im = Image.open(BytesIO(zf.read(src))).convert("RGBA")
        im = trim_pad(im, 0.0).resize((int(96 * scale),) * 2, Image.LANCZOS)
        canvas.alpha_composite(im, (128 - im.width // 2 + dx, 128 - im.height // 2 + dy))
    return canvas


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
    s = 4
    W = 48 * s
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    green = (48, 128, 62, 255)
    for x, lean in ((W // 2 - 6 * s, -2 * s), (W // 2 + 8 * s, 3 * s)):
        d.line((x, W - 8 * s, x + lean, 12 * s), fill=green, width=3 * s)
        d.ellipse((x - 4 * s, 8 * s, x + 4 * s, 18 * s), fill=(72, 160, 82, 255))
    return im.resize((48, 48), Image.LANCZOS)


def make_wheat_sheaf(zf: zipfile.ZipFile) -> Image.Image:
    im = cut_plot(Image.open(BytesIO(zf.read(R + "Tile/medievalTile_54.png"))))
    bbox = im.getchannel("A").getbbox()
    if bbox:
        im = im.crop(bbox)
    inset = int(min(im.size) * 0.18)
    im = im.crop((inset, inset, im.width - inset, im.height - inset))
    return gold(im, 0.35)


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
        save("windmill.png", make_windmill(zf))
        save("mountain_peak.png", make_peak(zf))
        save("desert_pond_water.png", make_desert_pond_water(zf))
        save("wheat_sheaf.png", make_wheat_sheaf(zf))
    save("cactus_tall.png", make_cactus(True))
    save("pond_reed.png", make_pond_reed())
    save("cactus_short.png", make_cactus(False))
    save("fence.png", make_fence())


if __name__ == "__main__":
    main()
