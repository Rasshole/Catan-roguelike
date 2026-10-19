"""Geometric 3-4-5-4-3 hex layout on docs/art/blender-tiles/reference_board.jpg."""
from __future__ import annotations

import math
from typing import Dict, Iterable, Tuple

# Flat-top axial coords (q, r) for the 19 land hexes.
def land_hexes() -> Iterable[Tuple[int, int]]:
    specs = [
        (-2, (-1, 0, 1)),
        (-1, (-2, -1, 0, 1)),
        (0, (-2, -1, 0, 1, 2)),
        (1, (-1, 0, 1, 2)),
        (2, (0, 1, 2)),
    ]
    for r, qs in specs:
        for q in qs:
            yield q, r


# Terrain at each hex on the photographed board (for crop labeling only).
TERRAIN_BY_HEX: Dict[Tuple[int, int], str] = {
    (-1, -2): "Ore",
    (0, -2): "Desert",
    (1, -2): "Wheat",
    (-2, -1): "Wood",
    (-1, -1): "Wood",
    (0, -1): "Wheat",
    (1, -1): "Sheep",
    (-2, 0): "Wood",
    (-1, 0): "Sheep",
    (0, 0): "Sheep",
    (1, 0): "Brick",
    (2, 0): "Wheat",
    (-1, 1): "Brick",
    (0, 1): "Wheat",
    (1, 1): "Brick",
    (2, 1): "Wheat",
    (0, 2): "Sheep",
    (1, 2): "Wheat",
    (2, 2): "Ore",
}

# Calibrated on reference_board.jpg (640×768).
BOARD_CENTER_FRAC = (0.50, 0.48)
HEX_FLAT_WIDTH_FRAC = 0.144

PREFERRED_HEX: Dict[str, Tuple[int, int]] = {
    "Wood": (-2, -1),
    "Wheat": (2, 0),
    "Sheep": (-1, 0),
    "Ore": (-1, -2),
    "Brick": (1, 0),
    "Desert": (0, -2),
}


def axial_to_frac(q: int, r: int) -> Tuple[float, float]:
    size = HEX_FLAT_WIDTH_FRAC / math.sqrt(3.0)
    x = size * (math.sqrt(3) * q + math.sqrt(3) / 2 * r)
    y = size * (1.5 * r)
    cx, cy = BOARD_CENTER_FRAC
    return cx + x, cy + y


def crop_for_terrain(terrain: str, half_size_frac: float = 0.09) -> Tuple[float, float, float]:
    q, r = PREFERRED_HEX[terrain]
    if TERRAIN_BY_HEX.get((q, r)) != terrain:
        raise ValueError(f"Preferred hex for {terrain} is mislabeled")
    fx, fy = axial_to_frac(q, r)
    return fx, fy, half_size_frac


def all_ref_crops() -> Dict[str, Tuple[float, float, float]]:
    order = ["Wood", "Wheat", "Sheep", "Ore", "Brick", "Desert"]
    return {name: crop_for_terrain(name) for name in order}
