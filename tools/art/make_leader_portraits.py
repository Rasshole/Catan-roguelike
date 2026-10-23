#!/usr/bin/env python3
"""v133 leader portraits: circular gold-rim medallions painted on white (tools/art/leaders-src/*.jpg) keyed to a
round alpha mask -> Assets/_Project/Resources/Art/Leaders/{Merchant,Pioneer,Warlord,Architect}.png (384 px).
The medallion's circle is found from the non-white bounding box; everything outside it becomes transparent
with an anti-aliased edge (so no white halo around the gold rim)."""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "tools", "art", "leaders-src")
DST = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Leaders")
NAMES = {"merchant": "Merchant", "pioneer": "Pioneer", "warlord": "Warlord", "architect": "Architect"}
OUT = 384


def key_round(path):
    im = np.asarray(Image.open(path).convert("RGB"), np.float32)
    dist = np.sqrt(((im - 255.0) ** 2).sum(-1))
    ink = dist > 40
    rows = np.nonzero(ink.sum(1) > 12)[0]
    cols = np.nonzero(ink.sum(0) > 12)[0]
    y0, y1, x0, x1 = rows[0], rows[-1], cols[0], cols[-1]
    cy, cx = (y0 + y1) / 2.0, (x0 + x1) / 2.0
    r = min(y1 - y0, x1 - x0) / 2.0 - 1.5  # just inside the rim's outer edge
    yy, xx = np.mgrid[0:im.shape[0], 0:im.shape[1]]
    d = np.sqrt((yy - cy) ** 2 + (xx - cx) ** 2)
    alpha = np.clip(r - d + 0.5, 0, 1)  # 1px anti-aliased edge
    rgba = np.dstack([im, alpha * 255]).astype(np.uint8)
    side = int(np.ceil(r + 2))
    box = (int(cx - side), int(cy - side), int(cx + side), int(cy + side))
    return Image.fromarray(rgba, "RGBA").crop(box).resize((OUT, OUT), Image.LANCZOS)


def main():
    os.makedirs(DST, exist_ok=True)
    for src, name in NAMES.items():
        key_round(os.path.join(SRC, f"{src}.jpg")).save(os.path.join(DST, f"{name}.png"), optimize=True)
        print("wrote", name)


if __name__ == "__main__":
    main()
