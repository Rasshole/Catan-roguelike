#!/usr/bin/env python3
"""Key the AI-painted round emblems (tools/art/emblems-src/{civ_*,evt_*}.jpg, medallion on white) into
transparent 384px PNGs: civilizations -> Resources/Art/Civilizations/{Key}.png, event categories ->
Resources/Art/Events/{Category}.png. Same keying/outline/shadow as make_painted_icons.py."""
import os
import uuid

import make_painted_icons as icons

ROOT = icons.ROOT
SRC = os.path.join(ROOT, "tools", "art", "emblems-src")
RES = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art")
TARGETS = {"civ_": os.path.join(RES, "Civilizations"), "evt_": os.path.join(RES, "Events")}
META_TEMPLATE = os.path.join(RES, "Icons", "Painted", "Wood.png.meta")


def write_meta(png):
    meta = png + ".meta"
    if os.path.exists(meta):
        return
    lines = open(META_TEMPLATE).read().splitlines()
    lines[1] = "guid: " + uuid.uuid4().hex
    open(meta, "w").write("\n".join(lines) + "\n")


def main():
    icons.OUT = 384
    icons.MARGIN = 0.04
    for name in sorted(os.listdir(SRC)):
        stem, ext = os.path.splitext(name)
        if ext.lower() != ".jpg":
            continue
        prefix = stem[:4]
        dst_dir = TARGETS[prefix]
        os.makedirs(dst_dir, exist_ok=True)
        out = icons.finish(icons.cut(os.path.join(SRC, name)))
        path = os.path.join(dst_dir, stem[4:] + ".png")
        out.save(path, optimize=True)
        write_meta(path)
        print("wrote", os.path.relpath(path, ROOT))


if __name__ == "__main__":
    main()


def make_card_frames():
    """Painted parchment card frames (tools/art/card-frames-src) -> Resources/Art/Cards/*.png (opaque)."""
    from PIL import Image
    src = os.path.join(ROOT, "tools", "art", "card-frames-src")
    dst = os.path.join(RES, "Cards")
    os.makedirs(dst, exist_ok=True)
    for name in sorted(os.listdir(src)):
        stem, ext = os.path.splitext(name)
        if ext.lower() != ".jpg":
            continue
        path = os.path.join(dst, stem + ".png")
        Image.open(os.path.join(src, name)).convert("RGB").save(path, optimize=True)
        write_meta(path)
        print("wrote", os.path.relpath(path, ROOT))


if __name__ == "__main__" and os.environ.get("CARD_FRAMES", "1") == "1":
    make_card_frames()
