#!/usr/bin/env python3
"""Generate CC0 Catan-style hex terrain albedos (distinct motifs, low noise)."""
from __future__ import annotations

import math
import random
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets/_Project/Resources/Art/Board"
SIZE = 256


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


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def clamp01(x: float) -> float:
    return 0.0 if x < 0.0 else 1.0 if x > 1.0 else x


def soft_noise(rng: random.Random, u: float, v: float, scale: float = 3.0) -> float:
    return (
        math.sin(u * scale * 6.28) * 0.04
        + math.sin(v * scale * 5.1) * 0.03
        + (rng.random() - 0.5) * 0.02
    )


def base_fill(
    rng: random.Random,
    rgb: tuple[float, float, float],
    u: float,
    v: float,
) -> tuple[int, int, int]:
    n = soft_noise(rng, u, v)
    r = int(clamp01(rgb[0] + n) * 255)
    g = int(clamp01(rgb[1] + n) * 255)
    b = int(clamp01(rgb[2] + n) * 255)
    return r, g, b


def paint_wood(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels: list[tuple[int, int, int]] = []
    base = (0.20, 0.46, 0.24)
    canopy = (0.14, 0.38, 0.18)
    centers = [(0.25, 0.28), (0.62, 0.35), (0.42, 0.68), (0.78, 0.72), (0.18, 0.78)]
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            r, g, b = base_fill(rng, base, u, v)
            for cx, cy in centers:
                dx, dy = u - cx, v - cy
                d = math.sqrt(dx * dx + dy * dy)
                if d < 0.11:
                    t = 1.0 - d / 0.11
                    r = int(lerp(r, canopy[0] * 255, t * 0.85))
                    g = int(lerp(g, canopy[1] * 255, t * 0.85))
                    b = int(lerp(b, canopy[2] * 255, t * 0.85))
                if 0.09 < d < 0.13:
                    t = 1.0 - abs(d - 0.11) / 0.02
                    r = int(lerp(r, 40, t * 0.5))
                    g = int(lerp(g, 70, t * 0.5))
                    b = int(lerp(b, 35, t * 0.5))
            pixels.append((r, g, b))
    return pixels


def paint_sheep(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels = []
    base = (0.58, 0.78, 0.42)
    tuft = (0.48, 0.68, 0.34)
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            r, g, b = base_fill(rng, base, u, v)
            for i in range(18):
                cx = (i * 37 % 100) / 100.0
                cy = (i * 53 % 100) / 100.0
                if ((u - cx) ** 2 + (v - cy) ** 2) < 0.0018:
                    r, g, b = int(tuft[0] * 255), int(tuft[1] * 255), int(tuft[2] * 255)
            pixels.append((r, g, b))
    return pixels


def paint_wheat(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels = []
    gold = (0.88, 0.72, 0.22)
    row = (0.78, 0.62, 0.16)
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            stripe = math.sin(v * 28.0 + u * 2.0) * 0.5 + 0.5
            rgb = (
                lerp(gold[0], row[0], stripe * 0.35),
                lerp(gold[1], row[1], stripe * 0.35),
                lerp(gold[2], row[2], stripe * 0.35),
            )
            r, g, b = base_fill(rng, rgb, u, v)
            pixels.append((r, g, b))
    return pixels


def paint_brick(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels = []
    clay = (0.72, 0.38, 0.24)
    brick = (0.58, 0.22, 0.14)
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            r, g, b = base_fill(rng, clay, u, v)
            bx, by = int(u * 8) % 2, int(v * 10)
            if (by + bx) % 2 == 0 and (u * 8) % 1.0 < 0.85 and (v * 10) % 1.0 < 0.75:
                r, g, b = int(brick[0] * 255), int(brick[1] * 255), int(brick[2] * 255)
            if rng.random() < 0.02:
                r, g, b = min(255, r + 15), min(255, g + 8), min(255, b + 5)
            pixels.append((r, g, b))
    return pixels


def paint_ore(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels = []
    slate = (0.48, 0.50, 0.54)
    rock = (0.38, 0.40, 0.44)
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            r, g, b = base_fill(rng, slate, u, v)
            for cx, cy, rad in ((0.3, 0.35, 0.08), (0.65, 0.55, 0.1), (0.45, 0.75, 0.07)):
                d = math.hypot(u - cx, v - cy)
                if d < rad:
                    t = 1.0 - d / rad
                    r = int(lerp(r, rock[0] * 255, t))
                    g = int(lerp(g, rock[1] * 255, t))
                    b = int(lerp(b, rock[2] * 255, t))
            pixels.append((r, g, b))
    return pixels


def paint_desert(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels = []
    sand = (0.90, 0.78, 0.52)
    dune = (0.82, 0.68, 0.42)
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            wave = math.sin(u * 9.0 + v * 4.0) * 0.5 + 0.5
            rgb = (lerp(sand[0], dune[0], wave * 0.25), lerp(sand[1], dune[1], wave * 0.25), lerp(sand[2], dune[2], wave * 0.25))
            r, g, b = base_fill(rng, rgb, u, v)
            pixels.append((r, g, b))
    return pixels


def paint_coast_sand(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels = []
    c = (0.94, 0.88, 0.70)
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            pixels.append(base_fill(rng, c, u, v))
    return pixels


def paint_sea(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels = []
    water = (0.26, 0.52, 0.68)
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            ripple = math.sin(u * 18 + v * 11) * 0.03
            rgb = (water[0] + ripple, water[1] + ripple, water[2] + ripple * 0.5)
            pixels.append(base_fill(rng, rgb, u, v))
    return pixels


def paint_fish_shoal(rng: random.Random) -> list[tuple[int, int, int]]:
    pixels = []
    shallow = (0.52, 0.78, 0.82)
    deep = (0.38, 0.62, 0.72)
    for y in range(SIZE):
        for x in range(SIZE):
            u, v = x / SIZE, y / SIZE
            d = math.hypot(u - 0.5, v - 0.55)
            t = clamp01(d / 0.42)
            rgb = (lerp(shallow[0], deep[0], t), lerp(shallow[1], deep[1], t), lerp(shallow[2], deep[2], t))
            r, g, b = base_fill(rng, rgb, u, v)
            # simple fish glyph
            for fx, fy in ((0.38, 0.48), (0.58, 0.62)):
                if math.hypot(u - fx, v - fy) < 0.06:
                    r, g, b = 55, 95, 110
                if math.hypot(u - (fx + 0.04), v - fy) < 0.025:
                    r, g, b = 220, 230, 235
            pixels.append((r, g, b))
    return pixels


PAINTERS = {
    "Wood": paint_wood,
    "Sheep": paint_sheep,
    "Wheat": paint_wheat,
    "Brick": paint_brick,
    "Ore": paint_ore,
    "Desert": paint_desert,
    "CoastSand": paint_coast_sand,
    "SeaLake": paint_sea,
    "FishShoal": paint_fish_shoal,
}


def main() -> None:
    for name, fn in PAINTERS.items():
        rng = random.Random(hash(name) & 0xFFFFFFFF)
        write_png(ROOT / f"{name}.png", fn(rng))
    print(f"Wrote {len(PAINTERS)} textures to {ROOT}")


if __name__ == "__main__":
    main()
