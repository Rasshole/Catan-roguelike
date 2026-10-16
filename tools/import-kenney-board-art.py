#!/usr/bin/env python3
"""Bake Kenney Hexagon Pack tiles + icons into Resources/Art (run after downloading source zips)."""
from __future__ import annotations

import argparse
import struct
import zlib
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageEnhance
except ImportError:
    raise SystemExit("pip install pillow")

ROOT = Path(__file__).resolve().parents[1]
BOARD = ROOT / "Assets/_Project/Resources/Art/Board"
ICONS = ROOT / "Assets/_Project/Resources/Art/Icons"
BOARDGAME = ROOT / "Assets/_Project/Resources/Art/Boardgame"
THIRD = ROOT / "Assets/_Project/Art/ThirdParty"

OUT_SIZE = 256

# Kenney Hexagon Pack terrain PNGs (pointy-top 120×140)
TERRAIN_SOURCES: dict[str, tuple[str, float]] = {
    "Wood": ("hexagon/PNG/Tiles/Terrain/Grass/grass_11.png", 0.0),
    "Sheep": ("hexagon/PNG/Tiles/Terrain/Grass/grass_04.png", 0.0),
    "Wheat": ("hexagon/PNG/Tiles/Terrain/Grass/grass_02.png", 0.0),
    "Brick": ("hexagon/PNG/Tiles/Terrain/Dirt/dirt_04.png", 0.0),
    "Ore": ("hexagon/PNG/Tiles/Terrain/Stone/stone_05.png", 0.0),
    "Desert": ("hexagon/PNG/Tiles/Terrain/Sand/sand_09.png", 0.0),
    "CoastSand": ("hexagon/PNG/Tiles/Terrain/Sand/sand_03.png", 0.0),
    "SeaLake": ("hexagon/PNG/Tiles/Terrain/Grass/grass_06.png", 0.0),
}

ICON_SOURCES: dict[str, str] = {
    "Wood": "bgi/PNG/Double (128px)/resource_wood.png",
    "Brick": "bgi/PNG/Double (128px)/resource_planks.png",
    "Wheat": "bgi/PNG/Double (128px)/resource_wheat.png",
    "Sheep": "bgi/PNG/Double (128px)/structure_farm.png",
    "Stone": "bgi/PNG/Double (128px)/resource_iron.png",
}


def composite_hex_tile(src: Path, rotate_deg: float = 0.0) -> Image.Image:
    im = Image.open(src).convert("RGBA")
    if rotate_deg:
        im = im.rotate(rotate_deg, expand=True, resample=Image.Resampling.BICUBIC)
    canvas = Image.new("RGBA", (OUT_SIZE, OUT_SIZE), (0, 0, 0, 0))
    ox = (OUT_SIZE - im.width) // 2
    oy = (OUT_SIZE - im.height) // 2
    canvas.paste(im, (ox, oy), im)
    return canvas


def tint_sea(im: Image.Image) -> Image.Image:
    r, g, b, a = im.split()
    base = Image.merge("RGB", (r, g, b))
    enh = ImageEnhance.Color(base)
    base = enh.enhance(1.15)
    r, g, b = base.split()
    # push toward light ocean blue
    pixels = base.load()
    for y in range(base.height):
        for x in range(base.width):
            pr, pg, pb = pixels[x, y]
            pixels[x, y] = (
                int(pr * 0.55 + 40),
                int(pg * 0.72 + 70),
                int(pb * 0.85 + 110),
            )
    tinted = base.convert("RGBA")
    tinted.putalpha(a)
    return tinted


def make_fish_shoal() -> Image.Image:
    base = Image.new("RGBA", (OUT_SIZE, OUT_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(base)
    # light fishing-ground water
    cx, cy = OUT_SIZE // 2, OUT_SIZE // 2
    for y in range(OUT_SIZE):
        for x in range(OUT_SIZE):
            dx = (x - cx) / (OUT_SIZE * 0.45)
            dz = (y - cy) / (OUT_SIZE * 0.5)
            if dx * dx + dz * dz < 1.0:
                base.putpixel((x, y), (148, 198, 228, 255))
    fish_color = (52, 118, 158, 255)
    for ox, oy, scale in [( -38, 8, 1.0), (22, -18, 0.85), (8, 28, 0.7)]:
        fx, fy = cx + ox, cy + oy
        draw.ellipse((fx - 18 * scale, fy - 8 * scale, fx + 18 * scale, fy + 8 * scale), fill=fish_color)
        draw.polygon(
            [
                (fx + 18 * scale, fy),
                (fx + 30 * scale, fy - 10 * scale),
                (fx + 30 * scale, fy + 10 * scale),
            ],
            fill=fish_color,
        )
        draw.ellipse((fx - 6 * scale, fy - 3 * scale, fx - 2 * scale, fy + 1 * scale), fill=(240, 250, 255, 255))
    return base


def save_png(path: Path, im: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGBA").save(path, "PNG")


def copy_icon(src: Path, dest: Path) -> None:
    im = Image.open(src).convert("RGBA")
    im = im.resize((128, 128), Image.Resampling.LANCZOS)
    save_png(dest, im)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--source",
        type=Path,
        default=Path("/tmp/art-import"),
        help="Folder containing hexagon/ and bgi/ trees",
    )
    parser.add_argument(
        "--quaternius",
        type=Path,
        default=Path("/tmp/quaternius-nature/ultimate-stylized-nature/sources/FBX"),
    )
    args = parser.parse_args()
    src_root: Path = args.source

    for slot, (rel, rot) in TERRAIN_SOURCES.items():
        path = src_root / rel
        if not path.is_file():
            raise SystemExit(f"Missing terrain source: {path}")
        im = composite_hex_tile(path, rot)
        if slot == "SeaLake":
            im = tint_sea(im)
        save_png(BOARD / f"{slot}.png", im)
        print(f"board {slot}")

    save_png(BOARD / "FishShoal.png", make_fish_shoal())
    print("board FishShoal")

    for resource, rel in ICON_SOURCES.items():
        path = src_root / rel
        if not path.is_file():
            raise SystemExit(f"Missing icon source: {path}")
        copy_icon(path, ICONS / f"{resource}.png")
        print(f"icon {resource}")

    dice_src = Path("/tmp/art-import")
    for i in range(1, 7):
        die = dice_src / f"dieWhite{i}.png"
        if die.is_file():
            im = Image.open(die).convert("RGBA")
            save_png(BOARDGAME / "Dice" / f"DieWhite{i}.png", im)
    card = dice_src / "cardBack_blue1.png"
    if card.is_file():
        save_png(BOARDGAME / "Cards" / "CardBack_blue.png", Image.open(card).convert("RGBA"))

    nature_out = ROOT / "Assets/_Project/Resources/Art/Models/Nature"
    nature_out.mkdir(parents=True, exist_ok=True)
    q = args.quaternius
    for name in ("NormalTree_2.fbx", "Rock_3.fbx", "Flower_2_Clump.fbx"):
        fbx = q / name
        if not fbx.is_file():
            raise SystemExit(f"Missing Quaternius model: {fbx}")
        dest = nature_out / name
        dest.write_bytes(fbx.read_bytes())
        print(f"model {name}")

    licenses = THIRD / "Kenney"
    licenses.mkdir(parents=True, exist_ok=True)
    (licenses / "README.md").write_text(
        "Kenney assets used in this project. Full packs: https://kenney.nl/\n",
        encoding="utf-8",
    )
    for name, url in [
        ("hexagon-pack-CC0.txt", "https://kenney.nl/assets/hexagon-pack"),
        ("boardgame-pack-CC0.txt", "https://kenney.nl/assets/boardgame-pack"),
        ("board-game-icons-CC0.txt", "https://kenney.nl/assets/board-game-icons"),
    ]:
        p = licenses / name
        if not p.exists():
            p.write_text(f"CC0 1.0 — {url}\n", encoding="utf-8")

    qlic = THIRD / "Quaternius" / "ultimate-nature-CC0.txt"
    qlic.parent.mkdir(parents=True, exist_ok=True)
    if not qlic.exists():
        qlic.write_text(
            "CC0 1.0 — Quaternius Ultimate Nature / stylized nature models\n"
            "https://quaternius.com/packs/ultimatenature.html\n",
            encoding="utf-8",
        )

    print("done")


if __name__ == "__main__":
    main()
