#!/usr/bin/env python3
"""Colour-grade alternatives of the v125 clay-pit painting (variants B-D) for picking a brick tile.
A = the v125 painting unchanged. Writes tools/art/ai-tiles-src/variants/Brick_{A,B,C,D}.jpg."""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
VAR = os.path.join(HERE, "ai-tiles-src", "variants")
BASE = os.path.join(VAR, "base", "Brick_v125.jpg")


def to_np(im):
    return np.asarray(im.convert("RGB"), np.float32) / 255.0


def save(a, label):
    Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).save(
        os.path.join(VAR, f"Brick_{label}.jpg"), quality=95)


def main():
    a = to_np(Image.open(BASE))
    lum = (a * np.array([0.3, 0.59, 0.11])).sum(-1, keepdims=True)
    save(a, "A")
    # B: physical-board brown: less orange, darker umber clay, like the photo's hills tiles.
    brown_lo, brown_hi = np.array([0.20, 0.12, 0.07]), np.array([0.78, 0.56, 0.38])
    b = brown_lo + (brown_hi - brown_lo) * np.clip(lum * 1.15, 0, 1)
    save(0.65 * b + 0.35 * a, "B")
    # C: bright terracotta: warmer, redder brick-clay, lighter pit walls.
    c = np.clip(a * np.array([1.12, 0.9, 0.78]) + 0.05, 0, 1)
    c = 1 - (1 - c) ** 1.25
    save(c, "C")
    # D: ochre-sienna: yellower sandy clay, lower contrast, reads closer to the board frame.
    g = lum + (a - lum) * 0.75
    d = np.clip(g * np.array([1.08, 1.0, 0.72]) + np.array([0.06, 0.05, 0.0]), 0, 1)
    save(d, "D")
    print("wrote Brick_A..D")


if __name__ == "__main__":
    main()
