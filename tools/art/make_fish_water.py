#!/usr/bin/env python3
"""Fishermen fish-ground water: the AI painting tools/art/ai-tiles-src/FishWater_v127.jpg
(turquoise shallows with painted fish schools), lightly graded, saved as
Assets/_Project/Resources/Art/Board/FishGroundWater.png. ColonistCoastalFishShoalVisual samples
it per arm, centring one of the SCHOOLS (u, v in Unity texture space, v up) on each arm."""
import os

from PIL import Image, ImageEnhance

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "tools", "art", "ai-tiles-src", "FishWater_v127.jpg")
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Art", "Board", "FishGroundWater.png")


def main():
    im = Image.open(SRC).convert("RGB")
    im = ImageEnhance.Color(im).enhance(0.92)
    im = ImageEnhance.Contrast(im).enhance(1.06)
    im.save(OUT, optimize=True)
    print("wrote", OUT, im.size)


if __name__ == "__main__":
    main()
