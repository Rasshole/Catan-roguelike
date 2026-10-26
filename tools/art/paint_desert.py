#!/usr/bin/env python3
"""Turn the plain AI sand painting into a Catan-style desert: warm pale dunes with stronger
dune shading, a few painted rocks and dry shrubs (kept away from the centre, where the number
token / pond / robber sit). Writes tools/art/ai-tiles-src/Desert_painted.jpg."""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "ai-tiles-src", "Desert_base.jpg")
OUT = os.path.join(HERE, "ai-tiles-src", "Desert_painted.jpg")
SS = 2


def grade(im):
    a = np.asarray(im, np.float32) / 255.0
    lum = a.mean(-1, keepdims=True)
    # Deepen dune shading around the mean, then tint toward the physical board's warm pale sand.
    m = lum.mean()
    lum2 = np.clip(m + (lum - m) * 2.2, 0, 1)
    sand_hi = np.array([0.98, 0.91, 0.67])
    sand_lo = np.array([0.84, 0.66, 0.38])
    col = sand_lo + (sand_hi - sand_lo) * np.clip((lum2 - 0.55) / 0.42, 0, 1)
    return Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8))


def blob(cx, cy, r, rng, n=11, jitter=0.28):
    pts = []
    for i in range(n):
        a = math.tau * i / n + rng.uniform(-0.15, 0.15)
        rr = r * (1 + rng.uniform(-jitter, jitter))
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr * 0.78))
    return pts


def rock(img, shadow, cx, cy, r, rng):
    d = ImageDraw.Draw(img)
    ds = ImageDraw.Draw(shadow)
    ds.polygon(blob(cx + r * 0.45, cy + r * 0.5, r * 1.05, rng), fill=(60, 40, 20, 150))
    base = blob(cx, cy, r, rng)
    d.polygon(base, fill=(132, 112, 92), outline=(78, 62, 48))
    # Lit facet (top-left) and a mid facet, like painted pebbles.
    d.polygon(blob(cx - r * 0.22, cy - r * 0.22, r * 0.58, rng, 8, 0.2), fill=(178, 160, 136))
    d.polygon(blob(cx - r * 0.32, cy - r * 0.34, r * 0.28, rng, 7, 0.2), fill=(206, 192, 170))


def shrub(img, shadow, cx, cy, r, rng):
    d = ImageDraw.Draw(img)
    ds = ImageDraw.Draw(shadow)
    ds.ellipse([cx - r * 0.7 + r * 0.4, cy - r * 0.45 + r * 0.5, cx + r * 0.7 + r * 0.4, cy + r * 0.45 + r * 0.5],
               fill=(70, 50, 20, 110))
    for _ in range(26):
        a = rng.uniform(0, math.tau)
        L = r * rng.uniform(0.5, 1.0)
        x2, y2 = cx + math.cos(a) * L, cy + math.sin(a) * L * 0.8
        col = rng.choice([(112, 92, 48), (96, 84, 44), (138, 116, 62), (84, 70, 40)])
        d.line([(cx, cy), (x2, y2)], fill=col, width=max(2, int(r * 0.07)))
        if rng.random() < 0.5:
            d.ellipse([x2 - r * 0.06, y2 - r * 0.06, x2 + r * 0.06, y2 + r * 0.06], fill=(164, 146, 82))


def main():
    rng = random.Random(11)
    src = Image.open(SRC).convert("RGB")
    w, h = src.size
    big = grade(src).resize((w * SS, h * SS), Image.BICUBIC)
    shadow = Image.new("RGBA", big.size, (0, 0, 0, 0))
    deco = Image.new("RGBA", big.size, (0, 0, 0, 0))
    # Spots in normalised coords, kept out of the centre ellipse (token + pond + robber).
    spots = [(0.34, 0.22, "rock", 22), (0.37, 0.27, "rock", 12), (0.62, 0.20, "shrub", 30),
             (0.29, 0.60, "shrub", 26), (0.68, 0.70, "rock", 26), (0.72, 0.66, "rock", 12),
             (0.56, 0.84, "shrub", 22), (0.40, 0.80, "rock", 15), (0.71, 0.40, "shrub", 20),
             (0.30, 0.42, "rock", 11), (0.50, 0.14, "rock", 9)]
    for nx, ny, kind, r in spots:
        cx, cy, rr = nx * w * SS, ny * h * SS, r * 1.45 * SS
        (rock if kind == "rock" else shrub)(deco, shadow, cx, cy, rr, rng)
    big = big.convert("RGBA")
    big.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(5 * SS)))
    big.alpha_composite(deco)
    out = big.convert("RGB").resize((w, h), Image.LANCZOS)
    out.save(OUT, quality=95)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
