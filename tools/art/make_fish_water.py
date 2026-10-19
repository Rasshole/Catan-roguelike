#!/usr/bin/env python3
"""Paint the Fishermen fish-ground water (turquoise shallows, ripples, small painted fish).

Output: Assets/_Project/Resources/Art/Board/FishGroundWater.png (1024x1024, opaque).
ColonistCoastalFishShoalVisual samples it inside each chevron and bakes the sand frame around it.
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Board", "FishGroundWater.png")
N = 1024
SS = 3


def noise(n, scale, seed):
    rng = np.random.default_rng(seed)
    small = rng.random((n // scale + 2, n // scale + 2)).astype(np.float32)
    img = Image.fromarray((small * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)
    return np.asarray(img, np.float32) / 255.0


def water():
    y, x = np.mgrid[0:N, 0:N].astype(np.float32) / N
    n1 = noise(N, 128, 1)
    n2 = noise(N, 32, 2)
    deep = np.array([38, 120, 168], np.float32)
    mid = np.array([74, 170, 200], np.float32)
    shallow = np.array([140, 214, 222], np.float32)
    t = np.clip(0.55 * n1 + 0.25 * n2 + 0.2 * (1 - y), 0, 1)[..., None]
    col = deep + (mid - deep) * np.clip(t * 1.6, 0, 1) + (shallow - mid) * np.clip(t * 1.6 - 0.8, 0, 1)
    # Painted ripple bands: soft light crests along gently warped sine waves.
    warp = (noise(N, 96, 3) - 0.5) * 0.08
    wave = np.sin((y + warp + 0.04 * np.sin(x * 9)) * math.tau * 7)
    crest = np.clip((wave - 0.82) / 0.18, 0, 1)[..., None] * (0.35 + 0.65 * n2[..., None])
    col = col + (np.array([215, 242, 245], np.float32) - col) * crest * 0.45
    trough = np.clip((-wave - 0.7) / 0.3, 0, 1)[..., None]
    col = col * (1 - 0.10 * trough)
    return Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB")


def fish(draw, cx, cy, length, angle, body, belly, rng):
    """Top-down fish: tapered body, forked tail, side fins, dark eye dots."""
    ca, sa = math.cos(angle), math.sin(angle)

    def pt(u, v):  # u along body (head +), v across
        return (cx + u * ca - v * sa, cy + u * sa + v * ca)

    L, W = length, length * 0.26
    outline = []
    for i in range(0, 181, 10):
        a = math.radians(i)
        u = math.cos(a) * L * 0.5
        taper = 1.0 if u > 0 else 0.55 + 0.45 * (1 + u / (L * 0.5))
        outline.append(pt(u, math.sin(a) * W * 0.5 * taper))
    for i in range(180, 361, 10):
        a = math.radians(i)
        u = math.cos(a) * L * 0.5
        taper = 1.0 if u > 0 else 0.55 + 0.45 * (1 + u / (L * 0.5))
        outline.append(pt(u, math.sin(a) * W * 0.5 * taper))
    tail = [pt(-L * 0.45, 0), pt(-L * 0.78, W * 0.62), pt(-L * 0.66, 0), pt(-L * 0.78, -W * 0.62)]
    fins = [[pt(L * 0.08, W * 0.4), pt(-L * 0.12, W * 0.85), pt(-L * 0.05, W * 0.35)],
            [pt(L * 0.08, -W * 0.4), pt(-L * 0.12, -W * 0.85), pt(-L * 0.05, -W * 0.35)]]
    dark = tuple(int(c * 0.55) for c in body)
    draw.polygon(tail, fill=dark)
    for f in fins:
        draw.polygon(f, fill=dark)
    draw.polygon(outline, fill=body, outline=dark)
    # Back stripe / highlight.
    stripe = [pt(L * 0.38, 0), pt(-L * 0.4, 0)]
    draw.line(stripe, fill=belly, width=max(1, int(W * 0.28)))
    for s in (1, -1):
        e = pt(L * 0.34, s * W * 0.22)
        r = max(1.5, W * 0.09)
        draw.ellipse([e[0] - r, e[1] - r, e[0] + r, e[1] + r], fill=(20, 24, 30))


def main():
    rng = random.Random(7)
    base = water().resize((N * SS, N * SS), Image.BICUBIC)
    shadow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    fishes = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ds = ImageDraw.Draw(shadow)
    df = ImageDraw.Draw(fishes)
    palettes = [((150, 168, 188), (226, 234, 242)), ((232, 124, 44), (252, 198, 116)),
                ((232, 124, 44), (252, 198, 116)), ((214, 168, 52), (250, 226, 140))]
    # Evenly spread pairs/trios (jittered grid) so every arm of every fish ground shows fish.
    placed = []
    cells = 4
    for gy in range(cells):
        for gx in range(cells):
            heading = rng.uniform(0, math.tau)
            pal = palettes[rng.randrange(len(palettes))]
            cx = (gx + 0.5 + rng.uniform(-0.2, 0.2)) / cells * N * SS
            cy = (gy + 0.5 + rng.uniform(-0.2, 0.2)) / cells * N * SS
            for k in range(rng.randint(1, 2)):
                px = cx + (k * 70 + rng.uniform(-20, 20)) * SS * math.cos(heading + 1.9)
                py = cy + (k * 70 + rng.uniform(-20, 20)) * SS * math.sin(heading + 1.9)
                length = rng.uniform(120, 150) * SS
                ang = heading + rng.gauss(0, 0.15)
                fish(ds, px + 16 * SS, py + 22 * SS, length, ang, (10, 50, 80), (10, 50, 80), rng)
                fish(df, px, py, length, ang, pal[0], pal[1], rng)
    shadow = shadow.filter(ImageFilter.GaussianBlur(10 * SS))
    sh = np.asarray(shadow, np.float32)
    sh[..., 3] *= 0.45
    base = base.convert("RGBA")
    base.alpha_composite(Image.fromarray(sh.astype(np.uint8), "RGBA"))
    base.alpha_composite(fishes)
    out = base.convert("RGB").resize((N, N), Image.LANCZOS).filter(ImageFilter.UnsharpMask(1.0, 40, 2))
    out.save(OUT, optimize=True)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
