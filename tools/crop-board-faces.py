#!/usr/bin/env python3
"""Post-process Kenney board faces: crop the inner hex face (drops the square surround and
coast slivers at the hex edges) to fill the whole texture, then grade mean RGB to the palette."""
from pathlib import Path
from PIL import Image
BOARD = Path(__file__).resolve().parents[1] / "Assets/_Project/Resources/Art/Board"
PALETTE = {"Wood": (46, 158, 56), "Sheep": (158, 235, 140), "Wheat": (232, 197, 71),
           "Brick": (209, 97, 56), "Ore": (125, 128, 135), "Desert": (237, 209, 132)}
BOX = (70, 66, 186, 182)  # inner rectangle well inside the face hex (radius ~107px)
BOX_WOOD = (76, 112, 180, 196)  # below Kenney's baked tree sprite
for name, target in PALETTE.items():
    p = BOARD / f"{name}.png"
    im = Image.open(p).convert("RGB").crop(BOX_WOOD if name == "Wood" else BOX).resize((256, 256), Image.LANCZOS)
    mean = im.resize((1, 1), Image.BOX).getpixel((0, 0))
    k = [t / max(1, m) for t, m in zip(target, mean)]
    im = Image.merge("RGB", [ch.point(lambda v, f=f: min(255, int(v * f))) for ch, f in zip(im.split(), k)])
    im.save(p)
    print(name, mean, "->", im.resize((1, 1), Image.BOX).getpixel((0, 0)))
