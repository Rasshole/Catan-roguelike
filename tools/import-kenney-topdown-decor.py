#!/usr/bin/env python3
"""Bake Kenney Medieval RTS top-down PNGs into Resources/Art/Board/TopDownDecor."""
from __future__ import annotations

import argparse
import shutil
import zipfile
from io import BytesIO
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    raise SystemExit("pip install pillow")

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/_Project/Resources/Art/Board/TopDownDecor"
THIRD = ROOT / "Assets/_Project/Art/ThirdParty/Kenney"
ZIP_DEFAULT = Path("/tmp/kenney_medievalRTSpack.zip")

# (source path in zip, output filename)
MEDIEVAL_COPIES: tuple[tuple[str, str], ...] = (
    ("PNG/Default size/Tile/medievalTile_08.png", "wheat_field_a.png"),
    ("PNG/Default size/Tile/medievalTile_10.png", "wheat_field_b.png"),
    ("PNG/Default size/Tile/medievalTile_11.png", "wheat_field_c.png"),
    ("PNG/Default size/Structure/medievalStructure_09.png", "windmill.png"),
    ("PNG/Default size/Tile/medievalTile_14.png", "hay_bale.png"),
    ("PNG/Default size/Environment/medievalEnvironment_09.png", "rock_cluster_a.png"),
    ("PNG/Default size/Environment/medievalEnvironment_10.png", "rock_cluster_b.png"),
    ("PNG/Default size/Environment/medievalEnvironment_11.png", "rock_cluster_c.png"),
    ("PNG/Default size/Structure/medievalStructure_15.png", "mountain_peak.png"),
    ("PNG/Default size/Tile/medievalTile_13.png", "clay_pit.png"),
    ("PNG/Default size/Structure/medievalStructure_01.png", "brick_stack.png"),
    ("PNG/Default size/Structure/medievalStructure_14.png", "brick_crates.png"),
    ("PNG/Default size/Structure/medievalStructure_19.png", "smelter.png"),
    ("PNG/Default size/Structure/medievalStructure_04.png", "fence.png"),
    ("PNG/Default size/Environment/medievalEnvironment_08.png", "desert_rock.png"),
)


def pad_to_square(im: Image.Image, size: int = 64) -> Image.Image:
    im = im.convert("RGBA")
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ox = (size - im.width) // 2
    oy = (size - im.height) // 2
    canvas.paste(im, (ox, oy), im)
    return canvas


def make_cactus(name: str, tall: bool) -> Image.Image:
    size = 96
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(im)
    green = (34, 118, 58, 255)
    green_dark = (22, 92, 44, 255)
    cx, cy = size // 2, size // 2 + 6
    h = 44 if tall else 30
    w = 14
    draw.ellipse((cx - w, cy - h, cx + w, cy + h), fill=green)
    if tall:
        draw.ellipse((cx - w - 18, cy - 8, cx - w + 10, cy + 18), fill=green_dark)
        draw.ellipse((cx + w - 10, cy - 4, cx + w + 18, cy + 22), fill=green_dark)
    return im


def save_png(path: Path, im: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path, "PNG")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--zip", type=Path, default=ZIP_DEFAULT)
    args = parser.parse_args()
    if not args.zip.is_file():
        raise SystemExit(f"Missing Medieval RTS zip: {args.zip}")

    OUT.mkdir(parents=True, exist_ok=True)
    THIRD.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(args.zip) as zf:
        lic = [n for n in zf.namelist() if n.lower().endswith("license.txt")]
        if lic:
            (THIRD / "medieval-rts-CC0.txt").write_bytes(zf.read(lic[0]))
        for src, dest in MEDIEVAL_COPIES:
            data = zf.read(src)
            im = pad_to_square(Image.open(BytesIO(data)))
            save_png(OUT / dest, im)
            print(dest)

    save_png(OUT / "cactus_tall.png", make_cactus("tall", True))
    save_png(OUT / "cactus_short.png", make_cactus("short", False))
    print("cactus_tall.png (procedural CC0-style)")
    print("cactus_short.png (procedural CC0-style)")
    print("done ->", OUT)


if __name__ == "__main__":
    main()
