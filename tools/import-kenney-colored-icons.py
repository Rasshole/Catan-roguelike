#!/usr/bin/env python3
"""Import Kenney Board Game Icons (128px) for colored HUD/port resource art + HUD stat/build icons."""
from __future__ import annotations

import zipfile
from io import BytesIO
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    raise SystemExit("pip install pillow")

ROOT = Path(__file__).resolve().parents[1]
ZIP = Path("/tmp/kenney/board-game-icons.zip")
PREFIX = "PNG/Double (128px)/"
ICONS = ROOT / "Assets/_Project/Resources/Art/Icons"
HUD = ROOT / "Assets/_Project/Resources/Art/HudIcons"

RESOURCE_MAP = {
    "resource_wood.png": "Wood.png",
    "resource_wheat.png": "Wheat.png",
    "resource_planks.png": "Brick.png",
    "resource_iron.png": "Stone.png",
}

HUD_MAP = {
    "crown_a.png": "StatVictory.png",
    "cards_stack.png": "StatResources.png",
    "card.png": "StatDevCard.png",
    "structure_wall.png": "BuildRoad.png",
    "structure_house.png": "BuildSettlement.png",
    "structure_tower.png": "BuildCity.png",
    "chess_knight.png": "StatKnight.png",
    "sword.png": "StatArmy.png",
    "structure_gate.png": "StatRoad.png",
}


def tint_sheep(src: Image.Image) -> Image.Image:
    im = src.convert("RGBA")
    px = im.load()
    wool = (236, 244, 236, 255)
    face = (48, 42, 46, 255)
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 16:
                continue
            lum = (r + g + b) / 3
            if lum < 90:
                px[x, y] = face
            else:
                px[x, y] = (wool[0], wool[1], wool[2], a)
    return im


def main() -> None:
    if not ZIP.is_file():
        raise SystemExit(f"Missing {ZIP}")
    ICONS.mkdir(parents=True, exist_ok=True)
    HUD.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(ZIP) as zf:
        for src, dest in RESOURCE_MAP.items():
            data = zf.read(PREFIX + src)
            (ICONS / dest).write_bytes(data)
            print("resource", dest)
        sheep_src = ICONS / "Sheep.png"
        if sheep_src.is_file():
            tint_sheep(Image.open(sheep_src)).save(ICONS / "Sheep.png", "PNG")
            print("resource Sheep.png (tinted)")
        for src, dest in HUD_MAP.items():
            (HUD / dest).write_bytes(zf.read(PREFIX + src))
            print("hud", dest)


if __name__ == "__main__":
    main()
