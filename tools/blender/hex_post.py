"""Post-process orthographic hex renders: clip to flat-top hex + sand-yellow bevel."""
from __future__ import annotations

import math

try:
    from PIL import Image, ImageDraw, ImageEnhance
except ImportError:
    Image = None  # type: ignore

SAND = (237, 209, 148)

# (center_x_frac, center_y_frac, half_size_frac) on reference_board.jpg
REF_HEX_CROPS = {
    "Wood": (0.36, 0.38, 0.09),
    "Wheat": (0.52, 0.34, 0.09),
    "Sheep": (0.62, 0.42, 0.09),
    "Ore": (0.44, 0.52, 0.09),
    "Brick": (0.56, 0.56, 0.09),
    "Desert": (0.68, 0.50, 0.09),
}


def hex_sdf(x: float, y: float, cx: float, cy: float, radius: float) -> float:
    px = (x - cx) / radius
    py = (y - cy) / radius
    qx = abs(px)
    qy = abs(py)
    return max(qy - 1.0, qx * 0.866025 + qy * 0.5 - 0.866025)


def interior_luma_variance(path: str, inner_radius_frac: float = 0.47) -> float:
    """Std-dev of luma inside the hex (excluding sand bevel band). SDF distance is normalized."""
    if Image is None:
        return 999.0
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    cx, cy = w * 0.5, h * 0.5
    radius = min(w, h) * inner_radius_frac
    bevel_w = 0.020
    lumas: list[float] = []
    px = im.load()
    for y in range(h):
        for x in range(w):
            d = hex_sdf(x, y, cx, cy, radius)
            if d > -bevel_w:
                continue
            r, g, b, a = px[x, y]
            if a < 16:
                continue
            lumas.append(0.299 * r + 0.587 * g + 0.114 * b)
    if len(lumas) < 100:
        return 0.0
    mean = sum(lumas) / len(lumas)
    var = sum((lv - mean) ** 2 for lv in lumas) / len(lumas)
    return math.sqrt(var)


def process_png(
    path: str,
    inner_radius_frac: float = 0.47,
    bevel_frac: float = 0.018,
    saturation: float = 1.08,
) -> None:
    if Image is None:
        return
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    cx, cy = w * 0.5, h * 0.5
    radius = min(w, h) * inner_radius_frac
    bevel_w = bevel_frac
    px = im.load()
    for y in range(h):
        for x in range(w):
            d = hex_sdf(x, y, cx, cy, radius)
            r, g, b, a = px[x, y]
            if d > 0:
                px[x, y] = (0, 0, 0, 0)
            elif d > -bevel_w and a > 12:
                t = (d + bevel_w) / bevel_w
                br, bg, bb = SAND
                px[x, y] = (
                    int(r * (1 - t) + br * t),
                    int(g * (1 - t) + bg * t),
                    int(b * (1 - t) + bb * t),
                    255,
                )
    im = ImageEnhance.Color(im).enhance(saturation)
    im = ImageEnhance.Contrast(im).enhance(1.12)
    im.save(path)


def make_side_by_side(
    reference_path: str,
    render_paths: dict[str, str],
    out_path: str,
    labels: list[str],
) -> None:
    if Image is None:
        return
    ref = Image.open(reference_path).convert("RGB")
    rw, rh = ref.size
    order = ["Wood", "Wheat", "Sheep", "Ore", "Brick", "Desert"]
    ref_crops = {}
    for name in order:
        fx, fy, hs = REF_HEX_CROPS[name]
        cx, cy = int(fx * rw), int(fy * rh)
        half = int(hs * min(rw, rh))
        ref_crops[name] = ref.crop((cx - half, cy - half, cx + half, cy + half)).resize((512, 512))

    tile = 512
    sheet = Image.new("RGB", (tile * 2, tile * len(order)), (32, 32, 32))
    draw = ImageDraw.Draw(sheet)
    for i, name in enumerate(order):
        y = i * tile
        sheet.paste(ref_crops[name], (0, y))
        if name in render_paths:
            rend = Image.open(render_paths[name]).convert("RGBA")
            rend = Image.new("RGB", rend.size, (48, 48, 48))
            rend.paste(Image.open(render_paths[name]).convert("RGBA"), mask=Image.open(render_paths[name]).split()[-1])
            rend = rend.resize((tile, tile))
            sheet.paste(rend, (tile, y))
        draw.text((8, y + 8), f"{name} reference", fill=(255, 255, 200))
        draw.text((tile + 8, y + 8), f"{name} render", fill=(255, 255, 200))
    sheet.save(out_path)
