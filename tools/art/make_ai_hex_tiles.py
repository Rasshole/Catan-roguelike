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
SIZE = 1024
SS = 2  # supersampling
SLOTS = ["Wood", "Wheat", "Sheep", "Ore", "Brick", "Desert"]

# Per-terrain grading: (saturation, brightness, contrast). Gentle, so the HUD still pops.
GRADE = {
    "Wood": (0.92, 1.04, 1.02),
    "Wheat": (0.88, 1.0, 1.0),
    "Sheep": (0.80, 0.98, 1.0),
    "Ore": (0.95, 1.02, 1.0),
    "Brick": (0.88, 1.03, 0.98),
    "Desert": (0.95, 0.97, 1.04),
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


def bake(slot):
    n = SIZE * SS
    radius = n / 2.0
    apothem = radius * np.sqrt(3) / 2.0
    frame = FRAME * apothem
    corner = ART_CORNER * apothem

    # Painted art covering the hex's screen bounding box (2R x sqrt3 R), centre-cropped.
    src = Image.open(os.path.join(SRC, f"{slot}.jpg")).convert("RGB")
    sat, bri, con = GRADE[slot]
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
    img = img.resize((SIZE, SIZE), Image.LANCZOS).filter(ImageFilter.UnsharpMask(1.2, 40, 2))
    # Screen space -> texture space: screen-up becomes image-right (90 deg clockwise).
    img = img.transpose(Image.Transpose.ROTATE_270).convert("RGBA")
    return img


def main(argv):
    out_dir = argv[1] if len(argv) > 1 else DST
    os.makedirs(out_dir, exist_ok=True)
    for slot in SLOTS:
        path = os.path.join(out_dir, f"{slot}.png")
        bake(slot).save(path, optimize=True)
        print("wrote", path)


if __name__ == "__main__":
    main(sys.argv)
