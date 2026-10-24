#!/usr/bin/env python3
"""Painted port art (v130), in the style of the AI-painted tiles:
  Resources/Art/Board/Ports/PortBadge.png  round badge: carved wooden ring + parchment face
  Resources/Art/Board/Ports/PierPlanks.png four jetty cross-boards (repeats along the pier):
      u across the pier (dark side rails at both edges), v along it; nails, grain, gaps.
"""
import math
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DST = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Board", "Ports")
RNG = np.random.default_rng(130)


def noise(h, w, scale, octaves=4):
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        sh, sw = max(2, int(h / scale) + 2), max(2, int(w / scale) + 2)
        layer = Image.fromarray((RNG.random((sh, sw)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        out += amp * np.asarray(layer, np.float32) / 255.0
        tot += amp
        amp *= 0.5
        scale = max(1.0, scale / 2)
    return out / tot


def badge(size=512):
    n = size
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    cx = cy = (n - 1) / 2
    r = np.hypot(xs - cx, ys - cy) / (n / 2)
    ang = np.arctan2(ys - cy, xs - cx)
    R_OUT, R_RING = 0.93, 0.76
    # Wood ring: warm brown, grain running around the ring, lit from top-left.
    grain = noise(n, n, 40) * 0.6 + 0.4 * (0.5 + 0.5 * np.sin(r * 160 + noise(n, n, 60) * 6))
    wood_dark = np.array([92, 58, 30], np.float32)
    wood_lite = np.array([168, 116, 64], np.float32)
    wood = wood_dark + (wood_lite - wood_dark) * grain[..., None]
    light = 0.5 + 0.5 * np.cos(ang - math.radians(-135))  # top-left highlight
    bevel = np.clip((r - R_RING) / (R_OUT - R_RING), 0, 1)
    ring_shape = np.sin(bevel * math.pi)  # rounded profile
    wood *= (0.72 + 0.38 * ring_shape * (0.55 + 0.45 * light))[..., None]
    # Parchment face: cream with fibres and a soft vignette + inner shadow under the ring.
    fib = noise(n, n, 18) * 0.5 + noise(n, n, 4) * 0.5
    parch = np.array([244, 232, 203], np.float32) * (0.93 + 0.09 * fib)[..., None]
    parch *= (1.0 - 0.10 * np.clip(r / R_RING, 0, 1) ** 3)[..., None]
    inner_sh = np.clip(1 - (R_RING - r) / 0.07, 0, 1) ** 2
    parch *= (1.0 - 0.28 * inner_sh)[..., None]
    col = np.where((r < R_RING)[..., None], parch, wood)
    # Dark carved lines at the ring edges.
    for edge, wdt in ((R_RING, 0.012), (R_OUT, 0.014)):
        line = np.exp(-((r - edge) / wdt) ** 2)
        col = col * (1 - 0.65 * line)[..., None] + np.array([50, 30, 14], np.float32) * (0.65 * line)[..., None]
    # Four brass nails on the ring.
    for k in range(4):
        a = math.radians(45 + 90 * k)
        nx, ny = cx + math.cos(a) * (n / 2) * (R_RING + R_OUT) / 2, cy + math.sin(a) * (n / 2) * (R_RING + R_OUT) / 2
        d = np.hypot(xs - nx, ys - ny) / (n * 0.018)
        m = np.clip(1.2 - d, 0, 1)
        hl = np.clip(1 - np.hypot(xs - nx + n * 0.005, ys - ny + n * 0.005) / (n * 0.008), 0, 1)
        nail = np.array([120, 96, 52], np.float32) + np.array([110, 100, 70], np.float32) * hl[..., None]
        col = col * (1 - m[..., None]) + nail * m[..., None]
    alpha = np.clip((R_OUT + 0.012 - r) / 0.012, 0, 1)
    img = Image.fromarray(np.dstack([np.clip(col, 0, 255), alpha * 255]).astype(np.uint8), "RGBA")
    return img.filter(ImageFilter.UnsharpMask(1.5, 40, 2))


def pier(w=128, boards=4, board_h=48):
    h = boards * board_h
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    base_tones = [np.array(c, np.float32) for c in ((150, 104, 60), (138, 94, 52), (160, 112, 66), (132, 90, 50))]
    col = np.zeros((h, w, 3), np.float32)
    for b in range(boards):
        y0 = b * board_h
        sl = slice(y0, y0 + board_h)
        tone = base_tones[b % len(base_tones)]
        g = noise(board_h, w, 10)
        streak = 0.5 + 0.5 * np.sin((ys[sl] - y0) / board_h * math.pi * (5 + b) + g * 5 + xs[sl] * 0.02)
        # v132: stronger, readable grain (was a soft 0.82-0.98 wash): broad streaks + thin dark grain lines
        # + one knot per board.
        fine = 0.5 + 0.5 * np.sin((ys[sl] - y0) / board_h * math.pi * (13 + 2 * b) + g * 9 + xs[sl] * 0.035)
        lines = np.clip((fine - 0.86) / 0.14, 0, 1)
        c = tone * (0.70 + 0.30 * streak + 0.10 * noise(board_h, w, 3) - 0.30 * lines)[..., None]
        kx, ky = (0.3 + 0.4 * ((b * 0.37) % 1.0)) * w, y0 + board_h * (0.35 + 0.3 * ((b * 0.61) % 1.0))
        kd = np.hypot((xs[sl] - kx) / 7.0, (ys[sl] - ky) / 4.0)
        ring = 0.5 + 0.5 * np.cos(kd * 5.0)
        c *= (1 - 0.35 * np.clip(1.6 - kd, 0, 1) * (0.6 + 0.4 * ring))[..., None]
        yy = (ys[sl] - y0) / board_h
        c *= (0.80 + 0.2 * np.sin(np.clip(yy, 0, 1) * math.pi) ** 0.5)[..., None]  # rounded board edges
        col[sl] = c
        # gap at the board start
        col[y0:y0 + 4] = np.array([46, 30, 16], np.float32)
        # nails near both rails
        for nxp in (0.16 * w, 0.84 * w):
            d = np.hypot(xs[sl] - nxp, ys[sl] - (y0 + board_h * 0.55)) / 3.2
            m = np.clip(1.2 - d, 0, 1)[..., None]
            col[sl] = col[sl] * (1 - m) + np.array([58, 50, 44], np.float32) * m
    # side rails: darker beams along the pier
    for x0, x1 in ((0, int(0.09 * w)), (int(0.91 * w), w)):
        col[:, x0:x1] = col[:, x0:x1] * 0.55 + np.array([40, 26, 14], np.float32) * 0.2
    return Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB")


def main():
    os.makedirs(DST, exist_ok=True)
    badge().save(os.path.join(DST, "PortBadge.png"), optimize=True)
    pier().save(os.path.join(DST, "PierPlanks.png"), optimize=True)
    print("wrote port art")


if __name__ == "__main__":
    main()
