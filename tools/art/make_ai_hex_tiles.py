#!/usr/bin/env python3
"""Bake the AI-painted terrain textures into hex-tile albedos for the colonist board.

Input:  tools/art/ai-tiles-src/{Wood,Wheat,Sheep,Ore,Brick,Desert}.jpg (top-down paintings)
Output: Assets/_Project/Resources/Art/Board/Blender/{slot}.png (1024x1024 RGBA)

UV layout (HexPrismMesh): u = x/(2R)+0.5, v = z/(2R)+0.5 with corner 0 at +Z, i.e. a pointy-top
hex touching v=0 and v=1. The colonist TableCamera (Euler 90,90,0) shows world +X as screen-up and
-Z as screen-right, so the tile is composed upright in *screen* space (a flat-top hex) and then
rotated 90 deg clockwise into texture space (screen-up -> image right).

Each tile gets the cardboard look of the physical board: painted art inset in a rounded hex,
framed by a thin warm sand-yellow bevel (dark outer edge, light inner highlight, soft inset shadow).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageEnhance, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "tools", "art", "ai-tiles-src")
DST = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Board", "Blender")
SIZE = 2048  # v130: 2048 albedos (the 1280px painting is Lanczos-upscaled before masking)
SS = 1  # the SDF frame is analytically anti-aliased
SLOTS = ["Wood", "Wheat", "Sheep", "Ore", "Brick", "Desert"]
# Source painting per slot (Desert is the dune painting with rocks/shrubs from paint_desert.py).
SOURCE = {
    "Desert": "Desert_v127.jpg",
    "Brick": "variants/Brick_A.jpg",
    "Ore": os.environ.get("ORE_SOURCE", "Ore_v127.jpg"),
}
# The v126 ore is the older painting lifted toward snow in-script; the v127 AI ore is already pale.
LIFT_ORE_SOURCES = {"Ore.jpg"}
VARIANT_DIR = os.path.join(SRC, "variants")
VARIANT_OUT = os.path.join(ROOT, "docs", "art", "tile-variants")

# Per-terrain grading: (saturation, brightness, contrast). Gentle, so the HUD still pops.
GRADE = {
    "Wood": (0.92, 1.04, 1.02),
    "Wheat": (0.88, 1.0, 1.0),
    "Sheep": (0.80, 0.98, 1.0),
    "Ore": (1.0, 1.0, 1.05),
    "Brick": (0.88, 1.03, 0.98),
    "Desert": (1.12, 0.94, 1.05),
}

FRAME = 0.075        # frame width as a fraction of the apothem
ART_CORNER = 0.045   # rounding radius of the art inset (fraction of apothem)
SAND = np.array([226, 196, 122], np.float32)
SAND_DARK = np.array([158, 118, 60], np.float32)
SAND_LIGHT = np.array([247, 230, 172], np.float32)


def sd_hex(px, py, apothem):
    """Signed distance to a flat-top hexagon (iq), negative inside."""
    kx, ky, kz = -0.8660254, 0.5, 0.57735027
    px = np.abs(px)
    py = np.abs(py)
    d = np.minimum(kx * px + ky * py, 0.0)
    px = px - 2.0 * d * kx
    py = py - 2.0 * d * ky
    cx = np.clip(px, -kz * apothem, kz * apothem)
    qx = px - cx
    qy = py - apothem
    return np.sqrt(qx * qx + qy * qy) * np.sign(qy)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def lift_ore(src):
    """Snowy pale crags: desaturate, lift shadows/mids toward white, slightly cool, then restore
    local contrast so the ridges still read."""
    a = np.asarray(src, np.float32) / 255.0
    g = a.mean(-1, keepdims=True)
    a = g + (a - g) * 0.6
    a = 1 - (1 - a) ** 1.9
    a = np.clip(a * np.array([0.97, 0.99, 1.03]), 0, 1)
    out = Image.fromarray((a * 255).astype(np.uint8))
    return out.filter(ImageFilter.UnsharpMask(18, 45, 0))


# Desert: a subtle warm-tan shift (v128) and a few small painted cacti away from the centre, where
# the robber sits. Screen-space positions (x right, y down) as fractions of the hex width, and
# height as a fraction of the hex width.
DESERT_TAN = (1.035, 0.985, 0.90)
DESERT_CACTI = [(-0.27, -0.17, 0.115, True), (0.25, 0.20, 0.10, True), (0.31, -0.13, 0.08, False)]


def paint_cactus(img, cx, cy, h, arms, seed):
    """Small saguaro in the painting's soft gouache style: rounded trunk/arms with a lit left side,
    shaded right side, faint ribs, and a soft cast shadow toward the lower right."""
    from PIL import ImageDraw
    rng = np.random.default_rng(seed)
    w = h * 0.24
    pad = int(h * 1.2)
    big = 4
    W = int(2 * pad) * big
    body = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(body)
    ox = oy = W // 2

    def cap(x0, y0, x1, y1, r):
        d.rounded_rectangle((ox + x0 * big, oy + y0 * big, ox + x1 * big, oy + y1 * big), radius=r * big, fill=255)

    base = h * 0.5
    cap(-w / 2, -h / 2, w / 2, base, w / 2)
    if arms:
        aw = w * 0.74
        # left arm: out from the trunk low down, then up
        cap(-w * 1.9, h * 0.02, 0, h * 0.02 + aw, aw / 2)
        cap(-w * 1.9, -h * 0.26, -w * 1.9 + aw, h * 0.02 + aw, aw / 2)
        # right arm: higher and shorter
        cap(0, -h * 0.12, w * 1.75, -h * 0.12 + aw, aw / 2)
        cap(w * 1.75 - aw, -h * 0.36, w * 1.75, -h * 0.12 + aw, aw / 2)
    else:
        aw = w * 0.8
        cap(0, -h * 0.02, w * 1.6, -h * 0.02 + aw, aw / 2)
        cap(w * 1.6 - aw, -h * 0.28, w * 1.6, -h * 0.02 + aw, aw / 2)
    body = body.filter(ImageFilter.GaussianBlur(big * 0.6))
    m = np.asarray(body, np.float32) / 255.0
    # Shading: horizontal gradient inside each column via a blurred-edge distance proxy.
    inner = np.asarray(body.filter(ImageFilter.GaussianBlur(w * big * 0.35)), np.float32) / 255.0
    gx = np.gradient(inner, axis=1)
    light = np.clip(0.5 - gx * w * big * 1.6, 0, 1)  # left edges lit, right edges dark
    yy, xx = np.mgrid[0:W, 0:W]
    ribs = 0.5 + 0.5 * np.sin(xx / (w * big * 0.17) * np.pi)
    dark = np.array([56, 76, 42], np.float32)
    mid = np.array([98, 120, 64], np.float32)
    lit = np.array([146, 160, 96], np.float32)
    col = dark + (mid - dark) * np.clip(light * 1.6, 0, 1)[..., None]
    col = col + (lit - col) * np.clip((light - 0.62) * 2.6, 0, 1)[..., None]
    col = col * (0.88 + 0.12 * ribs)[..., None]
    brush = Image.fromarray((rng.random((W // 8, W // 8)) * 255).astype(np.uint8)).resize((W, W), Image.BICUBIC)
    col = col * (0.92 + 0.16 * np.asarray(brush, np.float32) / 255.0)[..., None]
    # Cast shadow: body shifted down-right, squashed, blurred.
    sh = body.transform(body.size, Image.AFFINE, (1, 0, -w * big * 0.45, 0, 1, -w * big * 0.3)).filter(
        ImageFilter.GaussianBlur(big * w * 0.3))
    shm = np.asarray(sh, np.float32) / 255.0 * 0.55
    small = (W // big, W // big)
    rgba = np.dstack([col, m * 255]).astype(np.uint8)
    cact = Image.fromarray(rgba, "RGBA").resize(small, Image.LANCZOS)
    shadow = Image.fromarray((shm * 255).astype(np.uint8), "L").resize(small, Image.LANCZOS)
    x0, y0 = int(cx - small[0] / 2), int(cy - small[1] / 2)
    region = img.crop((x0, y0, x0 + small[0], y0 + small[1]))
    dark_sand = Image.fromarray((np.asarray(region, np.float32) * 0.62).astype(np.uint8))
    region = Image.composite(dark_sand, region, shadow)
    region.paste(cact, (0, 0), cact)
    img.paste(region, (x0, y0))


# In-game colour compensation (per-channel multiply on the art, in-build bake only): the lit board
# shader lifts and desaturates the clay, so it is pre-darkened until the on-screen colour matches
# the picked reference (docs/art/tile-variants/Brick_A.png). Measured from captures.
IN_GAME_COMP = {"Brick": tuple(float(v) for v in os.environ.get("BRICK_COMP", "0.89,0.63,0.34").split(","))}


def bake(slot, source_path=None, grade_key=None):
    n = SIZE * SS
    radius = n / 2.0
    apothem = radius * np.sqrt(3) / 2.0
    frame = FRAME * apothem
    corner = ART_CORNER * apothem

    # Painted art covering the hex's screen bounding box (2R x sqrt3 R), centre-cropped.
    src = Image.open(source_path or os.path.join(SRC, SOURCE.get(slot, f"{slot}.jpg"))).convert("RGB")
    if slot == "Ore" and os.path.basename(source_path or SOURCE.get("Ore", "")) in LIFT_ORE_SOURCES:
        src = lift_ore(src)
    sat, bri, con = GRADE[grade_key or slot]
    src = ImageEnhance.Color(src).enhance(sat)
    src = ImageEnhance.Brightness(src).enhance(bri)
    src = ImageEnhance.Contrast(src).enhance(con)
    box_w, box_h = n, int(round(2 * apothem))
    scale = max(box_w / src.width, box_h / src.height)
    rw, rh = int(np.ceil(src.width * scale)), int(np.ceil(src.height * scale))
    src = src.resize((rw, rh), Image.LANCZOS)
    left, top = (rw - box_w) // 2, (rh - box_h) // 2
    art_box = src.crop((left, top, left + box_w, top + box_h))
    art = Image.new("RGB", (n, n), tuple(int(c) for c in SAND))
    art.paste(art_box, (0, (n - box_h) // 2))
    if slot in IN_GAME_COMP and source_path is None:
        a = np.asarray(art, np.float32) * np.array(IN_GAME_COMP[slot], np.float32)
        art = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    if slot == "Desert" and source_path is None:
        a = np.asarray(art, np.float32) * np.array(DESERT_TAN, np.float32)
        art = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
        for i, (fx, fy, fh, arms) in enumerate(DESERT_CACTI):
            paint_cactus(art, n / 2 + fx * n, n / 2 + fy * n, fh * n, arms, 128 + i)
    art = np.asarray(art, np.float32)

    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    px = xs + 0.5 - radius
    py = ys + 0.5 - radius
    d_out = sd_hex(px, py, apothem)                       # 0 at the tile edge
    d_art = sd_hex(px, py, apothem - frame - corner) - corner  # rounded art inset

    # Frame: sand with a dark outer bevel and a light inner lip.
    t = np.clip(-d_out / frame, 0.0, 1.0)[..., None]       # 0 at outer edge, 1 at art edge
    col = SAND + (SAND_DARK - SAND) * (1 - smooth(0.0, 0.32, t))
    col = col + (SAND_LIGHT - col) * (smooth(0.45, 0.78, t) * (1 - smooth(0.82, 1.0, t))) * 0.85

    # Art with a soft inset shadow along its rim.
    rim = smooth(-0.11 * apothem, 0.0, d_art)[..., None]    # 0 deep inside, 1 at the rim
    shaded_art = art * (1.0 - 0.32 * rim ** 2)
    a = np.clip(0.5 - d_art / SS, 0.0, 1.0)[..., None]      # anti-aliased art edge
    col = col * (1 - a) + shaded_art * a
    # Thin dark line where the art meets the frame.
    line = np.clip(1.0 - np.abs(d_art) / (0.9 * SS), 0.0, 1.0)[..., None] * 0.35
    col = col * (1 - line) + SAND_DARK * 0.7 * line
    # Outside the hex: keep frame colour so mips do not bleed dark into the edge.
    outside = (d_out > 0)[..., None]
    col = np.where(outside, SAND_DARK, col)

    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB")
    img = img.resize((SIZE, SIZE), Image.LANCZOS).filter(ImageFilter.UnsharpMask(2.0, 55, 2))
    # Screen space -> texture space: screen-up becomes image-right (90 deg clockwise).
    img = img.transpose(Image.Transpose.ROTATE_270).convert("RGBA")
    return img


def bake_variants():
    """Bake every tools/art/ai-tiles-src/variants/<Slot>_<X>.jpg to docs/art/tile-variants and a
    labelled contact sheet per slot (for picking a variant; copy the chosen jpg over <Slot>.jpg)."""
    from PIL import ImageDraw, ImageFont

    os.makedirs(VARIANT_OUT, exist_ok=True)
    by_slot = {}
    for name in sorted(os.listdir(VARIANT_DIR)):
        stem, ext = os.path.splitext(name)
        if ext.lower() not in (".jpg", ".jpeg", ".png") or "_" not in stem or stem.split("_")[0] not in SLOTS:
            continue
        slot, label = stem.split("_", 1)
        img = bake(slot, os.path.join(VARIANT_DIR, name))
        upright = img.convert("RGB").transpose(Image.Transpose.ROTATE_90)
        path = os.path.join(VARIANT_OUT, f"{slot}_{label}.png")
        upright.save(path, optimize=True)
        by_slot.setdefault(slot, []).append((label, upright))
        print("variant", path)
    for slot, items in by_slot.items():
        cell = 720
        sheet = Image.new("RGB", (cell * len(items), cell + 90), (24, 58, 102))
        draw = ImageDraw.Draw(sheet)
        try:
            font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 54)
        except OSError:
            font = ImageFont.load_default()
        for i, (label, img) in enumerate(items):
            sheet.paste(img.resize((cell, cell), Image.LANCZOS), (i * cell, 0), )
            draw.text((i * cell + cell // 2, cell + 45), label, fill=(250, 240, 210), font=font, anchor="mm")
        path = os.path.join(VARIANT_OUT, f"{slot}_variants.png")
        sheet.save(path, optimize=True)
        print("sheet", path)


def main(argv):
    if len(argv) > 1 and argv[1] == "--variants":
        bake_variants()
        return
    out_dir = argv[1] if len(argv) > 1 else DST
    os.makedirs(out_dir, exist_ok=True)
    for slot in SLOTS:
        path = os.path.join(out_dir, f"{slot}.png")
        bake(slot).save(path, optimize=True)
        print("wrote", path)


if __name__ == "__main__":
    main(sys.argv)
