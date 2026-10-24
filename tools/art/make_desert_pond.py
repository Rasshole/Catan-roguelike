#!/usr/bin/env python3
"""Painted desert oasis pond (Fishermen desert lake): organic shoreline, darker wet-sand rim,
turquoise-to-teal water with soft ripples. Transparent outside the shore.
Output: Assets/_Project/Resources/Art/Board/TopDownDecor/desert_pond_water.png (512x512 RGBA)."""
import math
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Board", "TopDownDecor", "desert_pond_water.png")
N = 512


def main():
    y, x = (np.mgrid[0:N, 0:N].astype(np.float32) + 0.5) / N * 2 - 1
    r = np.sqrt(x * x + y * y)
    a = np.arctan2(y, x)
    wobble = 0.06 * np.sin(3 * a + 0.7) + 0.04 * np.sin(5 * a + 2.1) + 0.025 * np.sin(9 * a)
    edge = 0.86 + wobble            # outer shore line
    water_edge = edge - 0.13         # where the water starts
    d_shore = r - edge
    d_water = r - water_edge

    wet_sand = np.array([196, 160, 98], np.float32)
    dry_sand = np.array([228, 198, 132], np.float32)
    shallow = np.array([132, 206, 210], np.float32)
    deep = np.array([46, 136, 168], np.float32)

    t_rim = np.clip(-d_shore / 0.13, 0, 1)[..., None]
    col = dry_sand + (wet_sand - dry_sand) * t_rim
    depth = np.clip(-d_water / (water_edge * 0.9), 0, 1)[..., None]
    water = shallow + (deep - shallow) * (depth ** 0.8)
    ripple = np.sin((y * 9 + 0.6 * np.sin(x * 4)) * math.pi) * 0.5 + 0.5
    water = water + (np.array([220, 244, 246], np.float32) - water) * (np.clip((ripple - 0.86) / 0.14, 0, 1) * 0.16)[..., None]
    # Light shallow lip just inside the water line.
    lip = np.clip(1 - np.abs(d_water + 0.03) / 0.03, 0, 1)[..., None] * 0.5
    water = water + (np.array([196, 236, 230], np.float32) - water) * lip
    w = np.clip(0.5 - d_water * N / 2, 0, 1)[..., None]
    col = col * (1 - w) + water * w
    alpha = np.clip(0.5 - d_shore * N / 2, 0, 1) * np.clip(1 - np.clip(d_shore + 0.06, 0, 1) / 0.06, 0, 1)
    # Soft feathered outer edge so the wet sand blends into the painted dunes.
    feather = np.clip(-d_shore / 0.05, 0, 1)
    alpha = alpha * feather
    rgba = np.dstack([np.clip(col, 0, 255), alpha * 255]).astype(np.uint8)
    img = Image.fromarray(rgba, "RGBA").filter(ImageFilter.UnsharpMask(1.0, 30, 2))
    img.save(OUT, optimize=True)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
