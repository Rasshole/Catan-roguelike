#!/usr/bin/env python3
"""Download CC0 JPG sets from ambientCG for hex terrain renders."""
from __future__ import annotations

import os
import zipfile
from urllib.request import urlretrieve

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "textures")

# ambientCG material id -> slot
TEXTURES = {
    "GroundForest004": "forest",
    "GroundGrass001": "grass",
    "GroundSand002": "sand",
    "Rock035": "rock",
    "GroundMud005": "clay",
    "GroundForestMoss001": "wheat_base",
}


def fetch_one(material_id: str, slot: str) -> None:
    os.makedirs(OUT, exist_ok=True)
    zip_path = os.path.join(OUT, f"{material_id}.zip")
    url = f"https://ambientcg.com/get?file={material_id}_2K-JPG.zip"
    if not os.path.isfile(zip_path):
        print(f"Downloading {material_id}...")
        urlretrieve(url, zip_path)
    dest = os.path.join(OUT, slot)
    os.makedirs(dest, exist_ok=True)
    with zipfile.ZipFile(zip_path) as zf:
        for name in zf.namelist():
            if not name.lower().endswith((".jpg", ".jpeg", ".png")):
                continue
            base = os.path.basename(name)
            target = os.path.join(dest, base)
            if os.path.isfile(target):
                continue
            zf.extract(name, dest)
            extracted = os.path.join(dest, name)
            if extracted != target and os.path.isfile(extracted):
                os.replace(extracted, target)
    print(f"Ready {slot} in {dest}")


def main() -> None:
    for material_id, slot in TEXTURES.items():
        fetch_one(material_id, slot)


if __name__ == "__main__":
    main()
