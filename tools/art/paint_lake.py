#!/usr/bin/env python3
"""v147: painted Fishermen lake hex (blue water, sandy/grassy shore, reeds, lily pads, fish) in the
Blender tile layout. Keeps Desert.png's cardboard bevel frame and paints a lake inside it.
Writes Assets/_Project/Resources/Art/Board/Blender/Lake.png (2048², RGBA)."""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
FOLDER = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Board", "Blender")
SRC = os.path.join(FOLDER, "Desert.png")
OUT = os.path.join(FOLDER, "Lake.png")
N = 2048
C = N / 2


def hex_pts(r, cx=C, cy=C):
    return [(cx + r * math.cos(math.radians(90 + 60 * i)), cy - r * math.sin(math.radians(90 + 60 * i))) for i in range(6)]


def hex_mask(r, blur=0):
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).polygon(hex_pts(r), fill=255)
    return m.filter(ImageFilter.GaussianBlur(blur)) if blur else m


def hex_dist(x, y):
    """Normalised pointy-top hex 'radius' (1 at a vertex circle-ish edge: apothem-based)."""
    dx, dy = np.abs(x - C), np.abs(y - C)
    # pointy-top: edges at |x| = apothem and |x|*cos30 + |y|*sin30... use max of three projections
    a = dx
    b = dx * 0.5 + dy * math.sqrt(3) / 2
    return np.maximum(a, b)


def main():
    rng = random.Random(146)
    src = Image.open(SRC).convert("RGBA")
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    d = hex_dist(xx, yy)  # apothem-units
    apothem_in = 0.866 * 905  # inner sand edge (inside the bevel)

    # Water: deep teal-blue in the middle, lighter turquoise toward the shore.
    t = np.clip(d / (apothem_in * 0.80), 0, 1)
    deep = np.array([0.13, 0.42, 0.66])
    shallow = np.array([0.38, 0.72, 0.84])
    col = deep[None, None, :] * (1 - t[..., None] ** 1.6) + shallow[None, None, :] * (t[..., None] ** 1.6)
    noise = np.asarray(Image.effect_noise((N // 8, N // 8), 40).resize((N, N), Image.BICUBIC), np.float32) / 255.0
    col *= (0.93 + 0.12 * noise)[..., None]
    water = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).convert("RGBA")

    # Ripple arcs.
    rip = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    dr = ImageDraw.Draw(rip)
    for _ in range(46):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(330, 640)
        cx, cy = C + math.cos(a) * rr, C + math.sin(a) * rr
        w = rng.uniform(70, 150)
        dr.arc([cx - w, cy - w * 0.35, cx + w, cy + w * 0.35], 200, 340, fill=(235, 250, 255, 150), width=7)
    rip = rip.filter(ImageFilter.GaussianBlur(1.5))
    water.alpha_composite(rip)

    img = src.copy()
    # Shore band: wet sand + grass rim between the bevel and the water.
    shore = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    sand = np.array([0.90, 0.80, 0.55])
    grass = np.array([0.48, 0.66, 0.30])
    g = np.clip((d - apothem_in * 0.86) / (apothem_in * 0.12), 0, 1)
    sc = sand[None, None, :] * (1 - g[..., None]) + grass[None, None, :] * g[..., None]
    sc *= (0.9 + 0.16 * noise)[..., None]
    shore = Image.fromarray((np.clip(sc, 0, 1) * 255).astype(np.uint8)).convert("RGBA")
    inner = hex_mask(905, 2)
    img.paste(shore, (0, 0), inner)

    # Water with a wobbly shoreline.
    wm = Image.new("L", (N, N), 0)
    pts = []
    for i in range(72):
        a = math.tau * i / 72
        # hex-ish lake outline inside the shore
        r = 690 * (1 + 0.05 * math.sin(a * 3 + 0.6) + 0.03 * math.sin(a * 7 + 1.3) + rng.uniform(-0.012, 0.012))
        pts.append((C + math.cos(a) * r, C + math.sin(a) * r))
    ImageDraw.Draw(wm).polygon(pts, fill=255)
    wm = wm.filter(ImageFilter.GaussianBlur(5))
    # shoreline foam
    foam = wm.filter(ImageFilter.MaxFilter(25)).filter(ImageFilter.GaussianBlur(6))
    img.paste(Image.new("RGBA", (N, N), (246, 238, 214, 255)), (0, 0), foam)
    img.paste(water, (0, 0), wm)

    over = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    od = ImageDraw.Draw(over)
    shadow = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)

    def lily(cx, cy, r, flower):
        sd.ellipse([cx - r + 10, cy - r * 0.8 + 14, cx + r + 10, cy + r * 0.8 + 14], fill=(0, 30, 50, 90))
        od.pieslice([cx - r, cy - r * 0.8, cx + r, cy + r * 0.8], rng.uniform(0, 360), 0, fill=(70, 140, 60, 255))
        od.ellipse([cx - r, cy - r * 0.8, cx + r, cy + r * 0.8], outline=(40, 95, 40, 255), width=5)
        a0 = rng.uniform(0, 360)
        od.pieslice([cx - r, cy - r * 0.8, cx + r, cy + r * 0.8], a0, a0 + 330, fill=(84, 158, 70, 255))
        if flower:
            for k in range(6):
                a = math.tau * k / 6
                px, py = cx + math.cos(a) * r * 0.28, cy + math.sin(a) * r * 0.22
                od.ellipse([px - 16, py - 12, px + 16, py + 12], fill=(250, 214, 228, 255))
            od.ellipse([cx - 11, cy - 9, cx + 11, cy + 9], fill=(250, 210, 70, 255))

    def fish(cx, cy, s, ang, body, belly):
        L, H = 120 * s, 46 * s
        poly = []
        for k in range(24):
            a = math.tau * k / 24
            poly.append((math.cos(a) * L / 2, math.sin(a) * H / 2 * (1 - 0.25 * math.cos(a))))
        tail = [(-L / 2 + 6, 0), (-L / 2 - 44 * s, -30 * s), (-L / 2 - 36 * s, 0), (-L / 2 - 44 * s, 30 * s)]
        ca, sa = math.cos(ang), math.sin(ang)
        rot = lambda p: (cx + p[0] * ca - p[1] * sa, cy + p[0] * sa + p[1] * ca)
        sd.polygon([(x + 14, y + 18) for x, y in map(rot, poly)], fill=(0, 30, 60, 80))
        sd.polygon([(x + 14, y + 18) for x, y in map(rot, tail)], fill=(0, 30, 60, 80))
        od.polygon(list(map(rot, tail)), fill=body, outline=(40, 40, 50, 255))
        od.polygon(list(map(rot, poly)), fill=body, outline=(40, 40, 50, 255))
        od.polygon(list(map(rot, [(L * 0.1, H * 0.1), (L * 0.4, 0), (L * 0.1, H * 0.38), (-L * 0.25, H * 0.3)])), fill=belly)
        ex, ey = rot((L * 0.3, -H * 0.12))
        od.ellipse([ex - 7 * s, ey - 7 * s, ex + 7 * s, ey + 7 * s], fill=(20, 20, 30, 255))
        fx, fy = rot((-L * 0.05, -H * 0.5))
        od.polygon([rot((-L * 0.15, -H * 0.42)), rot((L * 0.12, -H * 0.42)), (fx, fy - 18 * s)], fill=body)

    # Fish around the token (centre ~ r 300 kept clear).
    fish(C - 430, C - 170, 1.45, math.radians(-15), (236, 132, 52, 255), (250, 214, 160, 255))
    fish(C + 400, C + 250, 1.35, math.radians(160), (120, 150, 170, 255), (210, 225, 230, 255))
    fish(C + 200, C - 440, 1.2, math.radians(200), (236, 150, 60, 255), (250, 220, 170, 255))
    fish(C - 250, C + 420, 1.15, math.radians(20), (110, 140, 160, 255), (205, 220, 228, 255))
    for (x, y, r, f) in [(C + 520, C - 120, 70, True), (C + 470, C - 30, 48, False), (C - 560, C + 140, 64, True),
                         (C - 120, C - 600, 56, False), (C + 60, C + 590, 60, True)]:
        lily(x, y, r, f)

    def reeds(cx, cy, n, lean):
        for k in range(n):
            x = cx + rng.uniform(-70, 70)
            y = cy + rng.uniform(-40, 40)
            h = rng.uniform(150, 240)
            tx = x + lean * h * rng.uniform(0.2, 0.45)
            sd.line([(x + 12, y + 12), (tx + 12, y - h + 12)], fill=(0, 20, 0, 70), width=12)
            od.line([(x, y), (tx, y - h)], fill=(70, 120, 50, 255), width=11)
            od.line([(x, y), (tx * 0.5 + x * 0.5 + 40 * lean, y - h * 0.6)], fill=(96, 150, 62, 255), width=9)
            if k % 2 == 0:
                hx, hy = x + (tx - x) * 0.82, y - h * 0.82
                od.ellipse([hx - 13, hy - 38, hx + 13, hy + 38], fill=(122, 74, 36, 255), outline=(80, 46, 20, 255), width=3)

    for (x, y, n, lean) in [(C - 560, C - 480, 7, 0.3), (C + 600, C + 470, 8, -0.3), (C + 640, C - 380, 6, -0.25),
                            (C - 660, C + 420, 6, 0.3), (C + 60, C + 790, 5, 0.1)]:
        reeds(x, y, n, lean)

    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(8)))
    img.alpha_composite(over)
    # Re-apply the bevel frame (outside the inner hex) from the desert tile so the border matches.
    frame_mask = Image.eval(hex_mask(905, 2), lambda v: 255 - v)
    img.paste(src, (0, 0), frame_mask)
    # Soft inner shadow along the bevel.
    edge = Image.eval(hex_mask(880, 22), lambda v: 255 - v)
    edge = Image.composite(edge, Image.new("L", (N, N), 0), hex_mask(905))
    img.paste(Image.new("RGBA", (N, N), (60, 40, 10, 255)), (0, 0), edge.point(lambda v: int(v * 0.35)))
    img.save(OUT)
    img.resize((512, 512), Image.LANCZOS).save("/workspace/v135/tmp/lake_preview.png")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
