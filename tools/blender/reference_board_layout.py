"""Hand-measured hex centres on docs/art/blender-tiles/reference_board.jpg (v122)."""
from __future__ import annotations

from typing import Dict, List, Tuple

HEX_RADIUS_FRAC = 0.09

# Normalised (x, y) image fractions; first entry used for side_by_side crops.
TERRAIN_CENTERS: Dict[str, List[Tuple[float, float]]] = {
    "Desert": [(0.53, 0.25)],
    "Wheat": [(0.70, 0.28), (0.83, 0.38), (0.82, 0.52), (0.80, 0.65)],
    "Wood": [(0.37, 0.29), (0.20, 0.38), (0.22, 0.51), (0.38, 0.44)],
    "Ore": [(0.54, 0.39), (0.52, 0.52), (0.52, 0.65)],
    "Brick": [(0.67, 0.40), (0.36, 0.55), (0.67, 0.55)],
    "Sheep": [(0.20, 0.66), (0.36, 0.70), (0.52, 0.78), (0.67, 0.69)],
}


def crop_for_terrain(terrain: str, half_size_frac: float = HEX_RADIUS_FRAC) -> Tuple[float, float, float]:
    centers = TERRAIN_CENTERS[terrain]
    fx, fy = centers[0]
    return fx, fy, half_size_frac


def all_ref_crops() -> Dict[str, Tuple[float, float, float]]:
    order = ["Wood", "Wheat", "Sheep", "Ore", "Brick", "Desert"]
    return {name: crop_for_terrain(name) for name in order}
