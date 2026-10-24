#!/usr/bin/env python3
"""Copy selected Kenney Hexagon Kit + Nature Kit FBX into Resources/Art/Models/Kenney."""
from __future__ import annotations

import argparse
import shutil
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/_Project/Resources/Art/Models/Kenney"
TEX_OUT = OUT / "Textures"
THIRD = ROOT / "Assets/_Project/Art/ThirdParty/Kenney"

HEX_ZIP_DEFAULT = Path("/workspace/assets-eval/kenney_hexagon-kit.zip")
NATURE_ZIP_DEFAULT = Path("/tmp/kenney_nature-kit.zip")

HEX_FBX = (
    "building-mill.fbx",
    "building-farm.fbx",
    "building-mine.fbx",
    "building-smelter.fbx",
    "stone-mountain.fbx",
    "stone-rocks.fbx",
    "stone-hill.fbx",
    "dirt-lumber.fbx",
    "sand-rocks.fbx",
)

NATURE_FBX = (
    "crops_wheatStageA.fbx",
    "crops_wheatStageB.fbx",
    "cactus_tall.fbx",
    "cactus_short.fbx",
    "rock_largeA.fbx",
    "rock_largeB.fbx",
    "rock_smallA.fbx",
    "rock_smallFlatA.fbx",
    "fence_planksDouble.fbx",
    "campfire_bricks.fbx",
)


def extract_member(zf: zipfile.ZipFile, suffix: str, dest: Path) -> None:
    for name in zf.namelist():
        if name.endswith(suffix) and not name.endswith("/"):
            dest.parent.mkdir(parents=True, exist_ok=True)
            with zf.open(name) as src, open(dest, "wb") as dst:
                dst.write(src.read())
            print(f"  {dest.name}")
            return
    raise SystemExit(f"Missing {suffix} in archive")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--hex-zip", type=Path, default=HEX_ZIP_DEFAULT)
    parser.add_argument("--nature-zip", type=Path, default=NATURE_ZIP_DEFAULT)
    args = parser.parse_args()

    if not args.hex_zip.is_file():
        raise SystemExit(f"Hexagon kit zip not found: {args.hex_zip}")
    if not args.nature_zip.is_file():
        raise SystemExit(f"Nature kit zip not found: {args.nature_zip}")

    OUT.mkdir(parents=True, exist_ok=True)
    TEX_OUT.mkdir(parents=True, exist_ok=True)
    THIRD.mkdir(parents=True, exist_ok=True)

    print("Hexagon Kit models:")
    with zipfile.ZipFile(args.hex_zip) as zf:
        for fbx in HEX_FBX:
            extract_member(zf, fbx, OUT / fbx)
        extract_member(
            zf,
            "colormap.png",
            TEX_OUT / "hexagon_colormap.png",
        )
        lic = [n for n in zf.namelist() if n.lower().endswith("license.txt")]
        if lic:
            with zf.open(lic[0]) as src:
                (THIRD / "hexagon-kit-CC0.txt").write_bytes(src.read())

    print("Nature Kit models:")
    with zipfile.ZipFile(args.nature_zip) as zf:
        for fbx in NATURE_FBX:
            extract_member(zf, fbx, OUT / fbx)
        lic = [n for n in zf.namelist() if n.lower().endswith("license.txt")]
        if lic:
            with zf.open(lic[0]) as src:
                (THIRD / "nature-kit-CC0.txt").write_bytes(src.read())

    print("done ->", OUT)


if __name__ == "__main__":
    main()
