"""Post-process orthographic hex renders: clip to flat-top hex + sand-yellow bevel."""
from __future__ import annotations

import math

try:
    from PIL import Image, ImageDraw, ImageEnhance
except ImportError:
    Image = None  # type: ignore

SAND = (237, 209, 148)

# (center_x_frac, center_y_frac, half_size_frac) on reference_board.jpg
# Expected mean HSV (H 0-1, S 0-1, V 0-1) inside hex interior per terrain slot
REF_MEAN_HSV = {
    "Wood": (0.28, 0.55, 0.35),
    "Wheat": (0.12, 0.62, 0.72),
    "Sheep": (0.30, 0.48, 0.55),
    "Ore": (0.55, 0.18, 0.55),
    "Brick": (0.06, 0.58, 0.48),
    "Desert": (0.10, 0.28, 0.78),
}

REF_HEX_CROPS = {
    "Wood": (0.20, 0.75, 0.09),
    "Wheat": (0.75, 0.70, 0.09),
    "Sheep": (0.33, 0.80, 0.09),
    "Ore": (0.23, 0.20, 0.09),
    "Brick": (0.60, 0.60, 0.09),
    "Desert": (0.53, 0.35, 0.09),
}


def hex_sdf(x: float, y: float, cx: float, cy: float, radius: float) -> float:
    px = (x - cx) / radius
    py = (y - cy) / radius
    qx = abs(px)
    qy = abs(py)
    return max(qy - 1.0, qx * 0.866025 + qy * 0.5 - 0.866025)


def interior_mean_hsv(path: str, inner_radius_frac: float = 0.47) -> tuple[float, float, float]:
    if Image is None:
        return (0.0, 0.0, 0.0)
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    cx, cy = w * 0.5, h * 0.5
    radius = min(w, h) * inner_radius_frac
    bevel_w = 0.020
    px = im.load()
    rs = gs = bs = 0.0
    n = 0
    for y in range(h):
        for x in range(w):
            d = hex_sdf(x, y, cx, cy, radius)
            if d > -bevel_w:
                continue
            r, g, b, a = px[x, y]
            if a < 16:
                continue
            rn, gn, bn = r / 255.0, g / 255.0, b / 255.0
            mx = max(rn, gn, bn)
            mn = min(rn, gn, bn)
            v = mx
            s = 0.0 if mx < 1e-6 else (mx - mn) / mx
            if mx == mn:
                hue = 0.0
            elif mx == rn:
                hue = ((gn - bn) / (mx - mn)) % 6.0
            elif mx == gn:
                hue = (bn - rn) / (mx - mn) + 2.0
            else:
                hue = (rn - gn) / (mx - mn) + 4.0
            hue /= 6.0
            rs += hue
            gs += s
            bs += v
            n += 1
    if n == 0:
        return (0.0, 0.0, 0.0)
    return (rs / n, gs / n, bs / n)


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


def assert_terrain_color(slot: str, path: str, hue_tol: float = 0.14, sat_tol: float = 0.30) -> None:
    if slot not in REF_MEAN_HSV:
        return
    h, s, v = interior_mean_hsv(path)
    eh, es, _ = REF_MEAN_HSV[slot]
    dh = min(abs(h - eh), 1.0 - abs(h - eh))
    if dh > hue_tol:
        raise ValueError(f"{slot}: hue {h:.2f} not near reference {eh:.2f} (delta {dh:.2f})")
    if v < 0.12:
        raise ValueError(f"{slot}: value {v:.2f} too dark; render likely failed")
    if s < es - sat_tol:
        raise ValueError(f"{slot}: saturation {s:.2f} too low vs reference {es:.2f}")
