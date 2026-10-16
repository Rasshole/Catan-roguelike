#!/usr/bin/env python3
"""Generate CC0 painted-style hex terrain albedos for Resources/Art/Board/."""
from __future__ import annotations

import math
import random
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets/_Project/Resources/Art/Board"
SIZE = 256

PALETTES = {
    "Wood": ((0.18, 0.42, 0.22), (0.28, 0.58, 0.30), (0.12, 0.32, 0.16)),
    "Brick": ((0.62, 0.28, 0.18), (0.78, 0.38, 0.22), (0.48, 0.20, 0.14)),
    "Wheat": ((0.82, 0.68, 0.22), (0.92, 0.78, 0.32), (0.72, 0.58, 0.16)),
    "Sheep": ((0.52, 0.72, 0.38), (0.62, 0.82, 0.48), (0.42, 0.62, 0.30)),
    "Ore": ((0.45, 0.47, 0.50), (0.55, 0.57, 0.60), (0.35, 0.37, 0.40)),
    "Desert": ((0.86, 0.74, 0.48), (0.92, 0.82, 0.58), (0.76, 0.64, 0.38)),
    "CoastSand": ((0.90, 0.82, 0.62), (0.96, 0.90, 0.72), (0.82, 0.74, 0.52)),
    "SeaLake": ((0.22, 0.48, 0.62), (0.32, 0.58, 0.72), (0.16, 0.38, 0.52)),
    "FishShoal": ((0.38, 0.58, 0.52), (0.48, 0.68, 0.60), (0.30, 0.48, 0.44)),
}


def write_png(path: Path, pixels: list[tuple[int, int, int]]) -> None:
    raw = bytearray()
    for y in range(SIZE):
        raw.append(0)
        for x in range(SIZE):
            r, g, b = pixels[y * SIZE + x]
            raw.extend((r, g, b))
    comp = zlib.compress(bytes(raw), 9)

    def chunk(tag: bytes, data: bytes) -> bytes:
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", comp) + chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def paint(name: str, colors: tuple) -> None:
    rng = random.Random(hash(name) & 0xFFFFFFFF)
    c0, c1, c2 = colors
    angle = rng.random() * math.pi
    cos_a, sin_a = math.cos(angle), math.sin(angle)
    pixels: list[tuple[int, int, int]] = []
    for y in range(SIZE):
        for x in range(SIZE):
            u = x / SIZE - 0.5
            v = y / SIZE - 0.5
            su = u * cos_a - v * sin_a + 0.5
            sv = u * sin_a + v * cos_a + 0.5
            n = (
                math.sin(su * 11 + sv * 7) * 0.5
                + math.sin(su * 23 - sv * 17) * 0.25
                + rng.random() * 0.08
            )
            t = (n + 1) * 0.5
            if t < 0.33:
                base = c0
                w = t / 0.33
            elif t < 0.66:
                base = c1
                w = (t - 0.33) / 0.33
            else:
                base = c2
                w = (t - 0.66) / 0.34
            r = int(max(0, min(255, base[0] * 255 * (0.92 + w * 0.12))))
            g = int(max(0, min(255, base[1] * 255 * (0.92 + w * 0.12))))
            b = int(max(0, min(255, base[2] * 255 * (0.92 + w * 0.12))))
            pixels.append((r, g, b))
    write_png(ROOT / f"{name}.png", pixels)


def main() -> None:
    for slot, palette in PALETTES.items():
        paint(slot, palette)
    print(f"Wrote {len(PALETTES)} textures to {ROOT}")


if __name__ == "__main__":
    main()
