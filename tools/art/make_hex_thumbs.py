#!/usr/bin/env python3
"""v132: small hex thumbnails cut from the painted hex tops (Resources/Art/Board/Blender/*.png) for the
painted pre-game map cards (Resources/Art/UIKit/Hex{Slot}.png, pointy-top, 128px tall, alpha-masked,
with a thin dark outline)."""
import math
import os
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Board", "Blender")
DST = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "UIKit")
META_SRC = os.path.join(DST, "Badge.png.meta")
R = 0.468  # circumradius of the painted hex as a fraction of the texture side
OUT_H = 128


def hex_mask(size, radius, ss=4):
    n = size * ss
    img = Image.new("L", (n, n), 0)
    cx = cy = n / 2
    pts = [(cx + radius * n * math.cos(math.radians(90 + 60 * k)),
            cy + radius * n * math.sin(math.radians(90 + 60 * k))) for k in range(6)]
    ImageDraw.Draw(img).polygon(pts, fill=255)
    return img.resize((size, size), Image.LANCZOS)


def main():
    for name in ("Wood", "Brick", "Wheat", "Sheep", "Ore", "Desert"):
        src = Image.open(os.path.join(SRC, f"{name}.png")).convert("RGB").resize((512, 512), Image.LANCZOS)
        mask = hex_mask(512, R)
        out = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        outline = hex_mask(512, R + 0.012)
        ink = Image.new("RGBA", (512, 512), (52, 34, 18, 0))
        ink.putalpha(outline)
        out = Image.alpha_composite(out, ink)
        tile = src.convert("RGBA")
        tile.putalpha(mask)
        out = Image.alpha_composite(out, tile)
        h = int(512 * (2 * (R + 0.012))) + 4
        w = int(512 * (2 * (R + 0.012)) * math.sqrt(3) / 2) + 4
        out = out.crop(((512 - w) // 2, (512 - h) // 2, (512 + w) // 2, (512 + h) // 2))
        scale = OUT_H / out.height
        out = out.resize((round(out.width * scale), OUT_H), Image.LANCZOS)
        path = os.path.join(DST, f"Hex{name}.png")
        out.save(path, optimize=True)
        if not os.path.exists(path + ".meta"):
            meta = open(META_SRC).read().splitlines()
            meta = [f"guid: {uuid.uuid4().hex}" if l.startswith("guid:") else l for l in meta]
            open(path + ".meta", "w").write("\n".join(meta) + "\n")
        print("wrote", path, out.size)


if __name__ == "__main__":
    main()
